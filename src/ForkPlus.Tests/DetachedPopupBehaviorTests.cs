// 真机 bug 复现（本轮25）：git mm"已展示 ××/××"筛选弹窗点击打不开 / 输出弹窗失焦消失。
// 用 Avalonia Headless 平台复现孤立 Popup 与挂树 Popup 的行为差异。
// 环境对齐真机：App.axaml 加载 <FluentTheme/>，Window ControlTheme 模板中的
// VisualLayerManager/PopupOverlayLayer 是 Popup 打开（OverlayLayer 路径）的必要依赖。
// 结论（已在真机修复中应用）：孤立 Popup 打开依赖"平台 IPopupImpl / 窗口 PopupOverlayLayer"
// 兜底路径，失败时抛 InvalidOperationException（真机被全局 UnhandledException 吞掉 →
// 用户视角"点击没反应"）；挂树 Popup 走正常路径打开且资源沿逻辑树解析。
using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	// 同一 Collection：与 ResourceCompatTests 串行执行——两个类都要启动 headless
	// Application（Assembly 级单例），并行会因重复 SetupWithoutStarting 中止测试。
	[Collection("HeadlessAvalonia")]
	public class DetachedPopupBehaviorTests
	{
		private sealed class HeadlessTestApp : Application
		{
			public override void OnFrameworkInitializationCompleted()
			{
				// 与真机一致加载 FluentTheme（真机 App.axaml <FluentTheme/>）：
				// 无主题时 Window 无 ControlTheme 模板 → 无 VisualLayerManager/
				// PopupOverlayLayer → Popup 打开只剩异常路径，测试结果失真。
				Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
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
				// SetupWithClassicDesktopLifetime（而非 SetupWithoutStarting）：挂上
				// ClassicDesktopStyleApplicationLifetime，WpfApp.Windows / ShowDialog
				// 兼容层依赖它取窗口列表；先启动的类创建共享 App，后启动的类直接复用，
				// 所以每个候选启动点都必须带 lifetime。ShutdownMode 改显式关闭，防止单
				// 个测试关掉唯一窗口时把 Dispatcher 整个 shut down。
				AppBuilder.Configure<HeadlessTestApp>()
					.UseHeadless(new AvaloniaHeadlessPlatformOptions())
					.SetupWithClassicDesktopLifetime(Array.Empty<string>(), delegate { });
				if (Application.Current.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime desktopLifetime)
				{
					desktopLifetime.ShutdownMode = global::Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
				}
				started.Set();
				Dispatcher.UIThread.MainLoop(new CancellationToken());
			});
			t.IsBackground = true;
			t.Start();
			SpinWait.SpinUntil(delegate { return started.WaitOne(0); }, 5000);
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
	public void DetachedPopup_TargetNotInVisualTree_SilentlyNeverOpens()
	{
		// 真机 bug 机制（Avalonia 12.1.1 Popup.Open 源码核实）：PlacementTarget 未挂
		// 视觉树 → TopLevel.GetTopLevel 返回 null → Open() 记 _isOpenRequested 后静默
		// return——不抛异常、IsOpen 谎报 true、Opened 事件永不触发（_isOpenRequested 仅在
		// Popup.OnAttachedToVisualTree 消费，孤立 Popup 永不挂树 → 消费不了）。
		// "无异常 = 无日志 = 用户视角点击没反应"——这就是必须把 Popup 挂进 RootGrid
		//（挂树路径走窗口 OverlayLayer，同 MenuFlyout/ComboBox）的原因。
		// 注：早期版本此测试前提是"抛 InvalidOperationException"，加 FluentTheme 对齐
		// 真机环境后实测不抛——真实机制是上面的静默延迟，已按实际行为修正断言。
		bool silentlyBroken = Run(delegate
		{
			var button = new Button { Content = "3/5 shown", Width = 90, Height = 24 }; // 不放入任何窗口
			var popup = new Popup { PlacementTarget = button, Placement = PlacementMode.Bottom };
			popup.Child = new Border { Width = 200, Height = 100, Background = Brushes.Red };

			bool openedFired = false;
			popup.Opened += delegate { openedFired = true; };

			bool threw = false;
			try { popup.IsOpen = true; }
			catch (InvalidOperationException) { threw = true; }

			// IsOpen 报 true（谎报）但 Opened 从未触发 = 静默失败
			return !threw && popup.IsOpen && !openedFired;
		});
		Assert.True(silentlyBroken);
	}

		[Fact]
		public void AttachedPopup_InPanel_OpensViaOverlayLayer()
		{
			// 修复方案验证：Popup 挂进面板（RootGrid.Children.Add）+ 窗口布局完成后，
			// 走窗口 PopupOverlayLayer 路径正常打开（headless 无平台 IPopupImpl 也能开）。
			bool opened = Run(delegate
			{
				var window = new Window { Width = 800, Height = 600 };
				var root = new Panel();
				var button = new Button { Content = "3/5 shown", Width = 90, Height = 24 };
				root.Children.Add(button);
				window.Content = root;
				window.Show();
				window.UpdateLayout();

				var popup = new Popup { PlacementTarget = button, Placement = PlacementMode.Bottom };
				popup.Child = new Border { Width = 200, Height = 100, Background = Brushes.Red };
				root.Children.Add(popup);
				popup.IsOpen = true;
				bool isOpen = popup.IsOpen;
				window.Close();
				return isOpen;
			});
			Assert.True(opened);
		}

		[Fact]
		public void DetachedPopup_ResourceResolution_NullWithoutAppResources()
		{
			// 孤立（未挂树）元素 TryGetResource 走 Application.Current.Resources；
			// headless 测试 App 无 App 级资源 → 解析为 null（证明孤立子控件的
			// DynamicResource 解析脱离主窗口资源链，挂树后才获得完整资源链）。
			Brush background = Run(delegate
			{
				var border = new Border();
				border.SetResourceReference(Border.BackgroundProperty, "BackgroundBrush");
				return border.Background as Brush;
			});
			Assert.Null(background);
		}

		[Fact]
		public void AttachedPopup_ClosesWhenPlacementTargetDetached()
		{
			// Popup.Open 订阅 PlacementTarget.DetachedFromVisualTree → Close：
			// 模拟 git mm 切换 tab / 控件被移除时弹窗自动关闭（不残留悬空浮层）。
			bool stillOpen = Run(delegate
			{
				var window = new Window { Width = 400, Height = 300 };
				var host = new Panel();
				var button = new Button { Content = "btn" };
				host.Children.Add(button);
				window.Content = host;
				window.Show();
				window.UpdateLayout();

				var popup = new Popup { PlacementTarget = button, Placement = PlacementMode.Bottom };
				popup.Child = new Border { Width = 100, Height = 50, Background = Brushes.Red };
				popup.IsOpen = true;
				Assert.True(popup.IsOpen);

				// 模拟 tab 切换：目标从树中移除
				host.Children.Remove(button);
				window.UpdateLayout();
				bool open = popup.IsOpen;
				window.Close();
				return open;
			});
			Assert.False(stillOpen);
		}

		[Fact]
		public void AttachedPopup_ProductionPattern_ClosedRemovesFromHostPanel()
		{
			// 生产修复模式回归（SubrepoFilterButton_Click）：Popup 挂进面板 + Closed 后
			// 从面板移除（防泄漏）。断言打开→关闭全流程面板状态正确。
			bool pass = Run(delegate
			{
				var window = new Window { Width = 400, Height = 300 };
				var root = new Panel();
				var button = new Button { Content = "3/5 shown", Width = 90, Height = 24 };
				root.Children.Add(button);
				window.Content = root;
				window.Show();
				window.UpdateLayout();

				var popup = new Popup { PlacementTarget = button, Placement = PlacementMode.Bottom };
				popup.Child = new Border { Width = 200, Height = 100, Background = Brushes.Red };
				root.Children.Add(popup);
				popup.Closed += delegate
				{
					root.Children.Remove(popup);
				};
				popup.IsOpen = true;
				bool openedInPanel = popup.IsOpen && root.Children.Contains(popup);
				popup.IsOpen = false;
				bool removedAfterClose = !root.Children.Contains(popup);
				window.Close();
				return openedInPanel && removedAfterClose;
			});
			Assert.True(pass);
		}

		[Fact]
		public void OutputOverlayBorder_InTree_NotDismissedByFocusLoss()
		{
			// 生产修复模式回归（输出覆盖层）：树内 Border 覆盖层用 IsVisible 控制，
			// 不存在 Popup 的失焦/失活 dismiss 路径——激活另一窗口后再回来仍可见。
			bool visible = Run(delegate
			{
				var window = new Window { Width = 800, Height = 600 };
				var overlay = new Border
				{
					IsVisible = false,
					Width = 720,
					Height = 360,
					Background = Brushes.Red,
					ZIndex = 10
				};
				var root = new Panel();
				root.Children.Add(new Button { Content = "main" });
				root.Children.Add(overlay);
				window.Content = root;
				window.Show();
				window.UpdateLayout();

				overlay.IsVisible = true;
				// 模拟窗口激活切换（Popup 走 WindowLostFocus/Deactivated 时会被 Close，覆盖层无此路径）
				window.Activate();
				bool stillVisible = overlay.IsVisible;
				window.Close();
				return stillVisible;
			});
			Assert.True(visible);
		}
	}
}
