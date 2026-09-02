// Linux GUI 冒烟（高效路径，替代 Xvfb 截图 + xdotool 坐标点击）：
// Avalonia.Headless in-process 驱动——继承真实 App 拿到全套 App.axaml 资源/样式，
// 控件级"构造-显示-断言"，崩溃时异常堆栈直接进测试输出。
// Windows 侧等价物是 ForkPlus.AutomationTests（FlaUI/UIA3，见其 csproj 注释）。
using System;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using ForkPlus.UI.Commands;
using Xunit;

namespace ForkPlus.Tests
{
	// 同一 Collection：与 DetachedPopupBehaviorTests/ResourceCompatTests 串行（共享
	// Assembly 级 headless Application 单例，并行会因重复启动中止）。
	[Collection("HeadlessAvalonia")]
	public class MenuWindowSmokeTests
	{
		// 继承真实 App：构造函数 InitializeComponent() 加载 App.axaml 全部资源/样式/DataTemplates；
		// 只 override 掉启动逻辑（RunStartup：IPC 单例、主窗口、git 版本弹窗等），保真且不依赖桌面。
		private sealed class HeadlessRealApp : global::ForkPlus.App
		{
			public override void OnFrameworkInitializationCompleted()
			{
			}
		}

		private static void EnsureStarted()
		{
			if (Application.Current != null)
			{
				return;
			}
			var started = new ManualResetEvent(false);
			var t = new Thread(delegate()
			{
				// 正常由 StartWithClassicDesktopLifetime 创建 lifetime；headless 需用
				// SetupWithClassicDesktopLifetime（只 Setup 不进 Start 主循环）手动挂上：
				// WpfApp.Windows / WpfApp.MainWindow / ShowDialog 兼容层都从它取窗口列表。
				// 注意 lifetime 赋值必须发生在 Setup 之前（之后赋值 Application 会抛
				// InvalidOperationException），所以不能用 SetupWithoutStarting + 后补。
				AppBuilder.Configure<HeadlessRealApp>()
					.UseHeadless(new AvaloniaHeadlessPlatformOptions())
					.SetupWithClassicDesktopLifetime(Array.Empty<string>(), delegate { });
				// 默认 ShutdownMode.OnLastWindowClose：单个测试关闭唯一窗口会把 Dispatcher
				// 整个 shut down，后续测试的 InvokeAsync 全部 TaskCanceledException。
				// 冒烟测试由 xunit 进程托管生命周期，改显式关闭。
				if (Application.Current.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime desktopLifetime)
				{
					desktopLifetime.ShutdownMode = global::Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
				}
				started.Set();
				Dispatcher.UIThread.MainLoop(new CancellationToken());
			});
			t.IsBackground = true;
			t.Start();
			SpinWait.SpinUntil(delegate { return started.WaitOne(0); }, 30000);
		}

		private static T Run<T>(Func<T> func)
		{
			EnsureStarted();
			return Dispatcher.UIThread.InvokeAsync(delegate
			{
				T result = func();
				Dispatcher.UIThread.RunJobs();
				return result;
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void AccountsWindow_ConstructsAndShows_WithoutCrash()
		{
			// 真机 bug：菜单"文件→账号..."点击后进程退出。第一步先隔离窗口本身：
			// 真实 App 资源环境下直接构造 + 非模态 Show，构造路径任何异常都会带完整堆栈冒出来。
			Exception crash = Run(delegate
			{
				Exception ex = null;
				try
				{
					var window = new global::ForkPlus.UI.Dialogs.Accounts.AccountsWindow();
					window.Show();
					Dispatcher.UIThread.RunJobs();
					window.Close();
				}
				catch (Exception e)
				{
					ex = e;
				}
				return ex;
			});
			Assert.True(crash == null, "账号窗口崩溃堆栈：\n" + crash);
		}

		[Fact]
		public void ShowAccountsWindowCommand_PostedJob_OpensAccountsWindow()
		{
			// 菜单"文件→账号..."项点击最终执行的就是 ShowAccountsWindowCommand.Execute（见
			// MainWindowMenuManager.CreateFileMenuItems），其内部 Dispatcher.Post 打开模态窗口。
			// MainLoop 后台线程处理 posted job；等待 AccountsWindow 出现（超时=静默失败），
			// posted job 内抛异常会带堆栈打到测试进程（复现真机崩溃路径）。
			EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				new ShowAccountsWindowCommand().Execute();
			}).GetAwaiter().GetResult();
			bool appeared = SpinWait.SpinUntil(delegate
			{
				return Dispatcher.UIThread.InvokeAsync(delegate
				{
					return global::ForkPlus.UI.WpfCompat.WpfApp.Windows.Any(delegate (Window w)
					{
						return w is global::ForkPlus.UI.Dialogs.Accounts.AccountsWindow && w.IsVisible;
					});
				}).GetAwaiter().GetResult();
			}, 15000);
			Assert.True(appeared, "账号窗口未打开（posted job 崩溃或静默失败）");
			// 清理：关闭窗口让模态 PushFrame 退出，避免 posted job 悬挂。
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				global::ForkPlus.UI.WpfCompat.WpfApp.Windows
					.FirstOrDefault(delegate (Window w) { return w is global::ForkPlus.UI.Dialogs.Accounts.AccountsWindow; })
					?.Close();
			}).GetAwaiter().GetResult();
		}
	}
}
