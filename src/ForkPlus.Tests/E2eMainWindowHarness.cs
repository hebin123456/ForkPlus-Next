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
using Avalonia.Threading;
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

		/// <summary>收尾：关闭仓库 tab（触发 SaveSession 把临时路径从会话里清掉）。
		/// 只关 tab 不关窗口——见类头铁律 1。</summary>
		public static void CloseRepositoryTab(MainWindow window, string repoPath)
		{
			try
			{
				window.TabManager.CloseTab(repoPath);
				Dispatcher.UIThread.RunJobs();
			}
			catch
			{
				// 收尾尽力而为：仓库目录可能已被 finally 清理，失败不掩盖断言
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
