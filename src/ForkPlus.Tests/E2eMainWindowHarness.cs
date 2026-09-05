// E2E 模块5 基建（2026-09-05）：真实 MainWindow 生产路径测试挂具。
// 背景：CommitUserControl.RepositoryStatusUpdated → IsActiveRepository() 检查
//   MainWindow.Instance?.TabManager.ActiveRepositoryUserControl == RepositoryUserControl，
//   此前 E2e01-04 不创建 MainWindow（Instance 为 null），状态刷新被推迟
//   （_pendingRepositoryStatusUiRefresh），Commit 视图文件列表永远装配不上数据。
//   模块5 起：真实 new MainWindow() + TabManager.OpenRepository() 生产入口打开仓库——
//   MainWindow 构造内会把自身挂到 ClassicDesktopStyleApplicationLifetime.MainWindow
//   （headless bootstrap 用的正是该 lifetime），IsActiveRepository 全链路走通，
//   状态刷新 → SetDataAsync → 列表装配按生产行为自动发生。
// 注意（铁律）：
//   1) 绝不能 MainWindow.Close()——构造里订阅了 Closed → lifetime.Shutdown()，
//      会把整个 headless App 关停殃及后续所有测试；收尾只 CloseTab。
//   2) 每个 [Fact] 建自己的 MainWindow：MainWindow.Instance 是"最近创建者"，
//      跨用例复用旧窗口会让 IsActiveRepository 永假（刷新又被推迟）。
using System;
using System.Linq;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	internal static class E2eMainWindowHarness
	{
		/// <summary>创建真实 MainWindow 并按生产入口（TabManager.OpenRepository）打开仓库。
		/// 返回激活的 RepositoryUserControl；窗口经 out 参数交由调用方截图/收尾。</summary>
		public static RepositoryUserControl OpenRepository(string repoPath, out MainWindow window)
		{
			HeadlessAppBootstrap.EnsureStarted();
			window = new MainWindow();
			// 覆盖构造里恢复的窗口几何（保存值可能过小/异常），保证截图布局稳定
			window.Width = 1400;
			window.Height = 900;
			window.Show();
			Dispatcher.UIThread.RunJobs(); // Loaded → RestoreSession（空工作区 → 仓库管理 tab）

			bool opened = window.TabManager.OpenRepository(repoPath);
			Assert.True(opened, "TabManager.OpenRepository 应成功打开 " + repoPath);
			// SelectTab → SelectedTabItemChanged 以 Background 优先级 Post 了 tab.Refresh()
			Dispatcher.UIThread.RunJobs();
			RepositoryUserControl repoControl = window.TabManager.ActiveRepositoryUserControl;
			Assert.True(repoControl != null, "打开后应存在激活的 RepositoryUserControl");
			return repoControl;
		}

		/// <summary>创建真实 MainWindow（不打开仓库，默认停留在仓库管理 tab）。
		/// 供模块6 等无仓库场景测试工具栏禁用态；收尾用 DetachWindow。</summary>
		public static MainWindow CreateWindow()
		{
			HeadlessAppBootstrap.EnsureStarted();
			MainWindow window = new MainWindow();
			window.Width = 1400;
			window.Height = 900;
			window.Show();
			Dispatcher.UIThread.RunJobs(); // Loaded → RestoreSession（空工作区 → 仓库管理 tab）
			return window;
		}

		/// <summary>收尾：关闭仓库 tab（触发 SaveSession 把临时路径从会话里清掉），随后摘除窗口。
		/// 不能 Close()（见铁律 1）；窗口摘除细节见 DetachWindow。
		/// 关 tab 前抓取 RepositoryUserControl 引用，关闭后<b>排空其后台 JobQueue</b>——
		/// 模块16 回归轮全量挂死根因（dotnet-stack + dotnet-dump 实证，2026-09-05）：
		/// 后台刷新任务在 tab 关闭后仍在跑，测试 finally 删仓库目录后刷新的
		/// bt_open_repository 失败（"could not find repository at /tmp/fpe2e_*/work/.git"）
		/// → RefreshRepositoryCommand 排队模态 ErrorWindow → 下个用例 RunJobs 执行它
		/// → headless 无人关闭 → 双层 PushFrame 死锁。这里在目录仍存在时等刷新跑完，
		/// 消除错误源头（HeadlessAppBootstrap 的错误弹窗看门狗兜底防挂死）。</summary>
		public static void CloseRepositoryTab(MainWindow window, string repoPath)
		{
			try
			{
				// tab 移除后控件脱离视觉树就找不到了——先按 GitModule.Path 精确定位再关
				RepositoryUserControl repoControl = window.GetVisualDescendants()
					.OfType<RepositoryUserControl>()
					.FirstOrDefault(delegate (RepositoryUserControl r)
					{
						return string.Equals(r.GitModule?.Path, repoPath, StringComparison.OrdinalIgnoreCase);
					});
				window.TabManager.CloseTab(repoPath);
				Dispatcher.UIThread.RunJobs();
				if (repoControl != null)
				{
					DrainRepositoryJobs(repoControl);
				}
			}
			catch
			{
				// 收尾尽力而为：仓库目录可能已被 finally 清理，失败不掩盖断言
			}
			RemoveTestReposFromManager(repoPath);
			DetachWindow(window);
		}

		/// <summary>排空仓库后台 JobQueue（刷新等 git 任务）：IsIdle 轮询 + RunJobs 泵回 UI 的
		/// Post 回调（与 UiClick.WaitFor 同款模式）。上限 10s——超时放弃（看门狗仍兜底），
		/// 不让个别慢刷新拖垮整个用例。</summary>
		private static void DrainRepositoryJobs(RepositoryUserControl repoControl)
		{
			try
			{
				var sw = System.Diagnostics.Stopwatch.StartNew();
				while (!repoControl.JobQueue.IsIdle && sw.ElapsedMilliseconds < 10000)
				{
					Dispatcher.UIThread.RunJobs();
					if (repoControl.JobQueue.IsIdle)
					{
						break;
					}
					System.Threading.Tasks.Task.Delay(25).GetAwaiter().GetResult();
				}
				Dispatcher.UIThread.RunJobs(); // 末次泵：执行刚完成任务的 Post 回调
			}
			catch
			{
				// 排空尽力而为：队列不可达等异常不掩盖测试断言
			}
		}

		/// <summary>把测试仓库从 RepositoryManager"最近"列表摘除（OpenRepository 经
		/// AddOrUpdateLastOpened 记录、SaveSession 时持久化到用户配置——残留的 fpe2e_*
		/// 条目会污染真实用户机器的仓库列表）。顺带清掉历史测试残留（fpe2e_ 前缀，
		/// 测试中断未收尾时留下的）。</summary>
		private static void RemoveTestReposFromManager(string repoPath)
		{
			try
			{
				var manager = global::ForkPlus.RepositoryManager.Instance;
				string[] toDelete = manager.Repositories
					.Where(delegate (global::ForkPlus.RepositoryManager.Repository r)
					{
						return string.Equals(r.Path, repoPath, StringComparison.OrdinalIgnoreCase)
							|| r.Path.Contains("fpe2e_", StringComparison.Ordinal);
					})
					.Select(r => r.Path)
					.ToArray();
				if (toDelete.Length > 0)
				{
					manager.DeleteRepositories(toDelete, addToIgnore: false);
					manager.Save();
					Dispatcher.UIThread.RunJobs();
				}
			}
			catch
			{
				// 清理尽力而为，不掩盖测试断言
			}
		}

		/// <summary>窗口摘除：Hide + 清 lifetime.MainWindow。不能 Close()（见铁律 1），
		/// 残留的可见窗口会参与后续测试（如 ThemeSystemIntegrityTests 切皮肤）的布局，
		/// ModernTabControl 模板重建时旧 ContentPresenter 与新 ContentPresenter 争抢同一 Grid 抛
		/// "already has a visual parent"（2026-09-05 实证：E2e05 先跑则主题守卫必崩）。</summary>
		public static void DetachWindow(MainWindow window)
		{
			try
			{
				window.Hide();
				// Avalonia 12 lifetime.Windows 是 IReadOnlyList（不可移除项）；Hide 后窗口
				// IsVisible=false，不再参与布局/渲染调度。MainWindow.Instance 是
				// lifetime.MainWindow 的只读投影，从源头清引用避免下个用例误判激活仓库。
				if (Avalonia.Application.Current?.ApplicationLifetime
					is Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime lifetime
					&& lifetime.MainWindow == window)
				{
					lifetime.MainWindow = null;
				}
				Dispatcher.UIThread.RunJobs();
			}
			catch
			{
				// 窗口摘除失败不掩盖断言；下个用例创建新 MainWindow 时 Instance 会被覆盖
			}
		}

		/// <summary>语言无关断言助手（CommitButton 文案等走本地化），与 E2e04 相同约定。</summary>
		public static string Tr(string text)
		{
			return ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Translate(
				text, ForkPlus.Settings.ForkPlusSettings.Default.UiLanguage);
		}

		/// <summary>带占位符的本地化断言助手：按钮文案走 FormatCurrent（键含 {0}），Translate 后再 Format。</summary>
		public static string TrFormat(string text, params object[] args)
		{
			return string.Format(Tr(text), args);
		}
	}
}
