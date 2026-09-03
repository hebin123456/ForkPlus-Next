// 回归测试（2026-09-03，"弹窗 TextBox 选中后颜色肉眼无法识别"两轮修复产物）：
// 第一轮（7b13c1f）：自定义 TextBox ControlTheme 的 SelectionBrush/SelectionForegroundBrush
// 未设置且未传入模板 TextPresenter → 补画刷。但用户仍看不清——画刷非空≠选区可见。
// 第二轮（本轮，像素探针实测定位真根因）：TextPresenter 漏绑
// SelectionStart/SelectionEnd（实测程序化设选区后 presenter 恒 0/0，选区索引根本
// 到不了渲染层，画刷再对也不画任何高亮）。Avalonia 官方 Fluent 主题（12.1.1）
// 全部经 TemplateBinding 下传：SelectionStart/SelectionEnd/CaretIndex/PasswordChar/
// TextAlignment/TextWrapping——漏一个对应功能即静默失效。本轮对齐官方主题补齐全部绑定，
// 同因顺带修复：PasswordChar 未下传（AskPassWindow/SshPassphraseWindow 密码明文）、
// TextWrapping 未下传（StatisticsUserControl 排除框不换行）。
// 本测试守卫三层：1) 选区索引流入 presenter；2) 像素级 accent 高亮真实渲染（含基线对照）；
// 3) PasswordChar 掩码下传。像素层用 RenderTargetBitmap（HeadlessAppBootstrap 已启用
// UseSkia + UseHeadlessDrawing=false，真渲染后端；纯 headless 绘图模式 RTB 渲染为空）。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;
using System;
using System.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class TextBoxSelectionStyleTests
	{
		// 渲染控件并统计满足谓词的像素数（BGRA8888）。分辨率 320x90 足够容纳测试 TextBox。
		private static int CountPixels(Control control, Func<Color, bool> predicate)
		{
			using var rtb = new RenderTargetBitmap(new PixelSize(320, 90));
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			rtb.Render(control);
			using var wb = new WriteableBitmap(rtb.PixelSize, rtb.Dpi);
			int count = 0;
			using (ILockedFramebuffer fb = wb.Lock())
			{
				rtb.CopyPixels(fb);
				int len = fb.RowBytes * fb.Size.Height;
				byte[] pixels = new byte[len];
				System.Runtime.InteropServices.Marshal.Copy(fb.Address, pixels, 0, len);
				const int bpp = 4;
				for (int y = 0; y < fb.Size.Height; y++)
				{
					int row = y * fb.RowBytes;
					for (int x = 0; x < fb.Size.Width; x++)
					{
						int o = row + x * bpp;
						byte b = pixels[o], g = pixels[o + 1], r = pixels[o + 2], a = pixels[o + 3];
						if (predicate(Color.FromArgb(a, r, g, b)))
						{
							count++;
						}
					}
				}
			}
			return count;
		}

		private static Color ResolveAccentColor()
		{
			var brush = Avalonia.Application.Current!.FindResource("AccentBrush") as ISolidColorBrush;
			Assert.NotNull(brush);
			return brush!.Color;
		}

		[Fact]
		public void ErrorWindow_MessageTextBox_HasVisibleSelectionStyle()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var err = new ErrorWindow("fatal: git error output\nsecond line");
				err.Show();
				Dispatcher.UIThread.RunJobs();

				TextBox msgBox = err.GetVisualDescendants().OfType<TextBox>().First((TextBox b) => b.Name == "MessageTextBox");
				TextPresenter presenter = msgBox.GetVisualDescendants().OfType<TextPresenter>().First();

				ISolidColorBrush selBg = presenter.SelectionBrush as ISolidColorBrush;
				ISolidColorBrush selFg = presenter.SelectionForegroundBrush as ISolidColorBrush;

				Assert.NotNull(selBg);
				Assert.NotEqual(Colors.Transparent, selBg.Color);
				Assert.NotEqual(default, selBg.Color);
				Assert.NotNull(selFg);
				// 前景必须与选区背景不同色，否则选中文字不可读（原 bug 的"看不清"形态）。
				Assert.NotEqual(selBg.Color, selFg.Color);

				err.Close();
				return 0;
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void PlainTextBox_HasVisibleSelectionStyle()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var tb = new TextBox { Text = "hello world", Width = 200, Height = 30 };
				var window = new Window { Width = 300, Height = 100, Content = tb };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				TextPresenter presenter = tb.GetVisualDescendants().OfType<TextPresenter>().First();
				ISolidColorBrush selBg = presenter.SelectionBrush as ISolidColorBrush;
				ISolidColorBrush selFg = presenter.SelectionForegroundBrush as ISolidColorBrush;
				Assert.NotNull(selBg);
				Assert.NotEqual(Colors.Transparent, selBg.Color);
				Assert.NotEqual(default, selBg.Color);
				Assert.NotNull(selFg);
				Assert.NotEqual(selBg.Color, selFg.Color);
				window.Close();
				return 0;
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void PlaceholderTextBox_HasVisibleSelectionStyle()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var tb = new PlaceholderTextBox { Text = "hello world", Placeholder = "ph", Width = 200, Height = 30 };
				var window = new Window { Width = 300, Height = 100, Content = tb };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				TextPresenter presenter = tb.GetVisualDescendants().OfType<TextPresenter>().First();
				ISolidColorBrush selBg = presenter.SelectionBrush as ISolidColorBrush;
				ISolidColorBrush selFg = presenter.SelectionForegroundBrush as ISolidColorBrush;
				Assert.NotNull(selBg);
				Assert.NotEqual(Colors.Transparent, selBg.Color);
				Assert.NotEqual(default, selBg.Color);
				Assert.NotNull(selFg);
				Assert.NotEqual(selBg.Color, selFg.Color);
				window.Close();
				return 0;
			}).GetAwaiter().GetResult();
		}

		// ── 第二轮根因守卫：选区索引必须流入 TextPresenter（修复前恒 0/0） ──

		[Fact]
		public void ProgrammaticSelection_ReachesTextPresenter_PlainReadOnlyAndPlaceholder()
		{
			HeadlessAppBootstrap.EnsureStarted();
			(bool plain, bool readOnly, bool placeholder) = HeadlessAppBootstrap.Run(delegate
			{
				bool Check(ControlFactory factory)
				{
					var tb = factory();
					tb.SelectionStart = 2;
					tb.SelectionEnd = 10;
					var window = new Window { Width = 300, Height = 100, Content = tb };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					TextPresenter presenter = tb.GetVisualDescendants().OfType<TextPresenter>().First();
					bool ok = presenter.SelectionStart == 2 && presenter.SelectionEnd == 10;
					window.Close();
					return ok;
				}

				return (
					Check(delegate { return new TextBox { Text = "0123456789ABCDEF", Width = 200, Height = 30 }; }),
					Check(delegate { return new TextBox { Text = "0123456789ABCDEF", Width = 200, Height = 30, IsReadOnly = true }; }),
					Check(delegate { return new PlaceholderTextBox { Text = "0123456789ABCDEF", Placeholder = "ph", Width = 200, Height = 30 }; }));
			});
			// 修复前 presenter.SelectionStart/End 恒为 0/0——画刷非空但选区索引到不了渲染层。
			Assert.True(plain, "普通 TextBox 选区索引未传入 TextPresenter（选区不渲染）");
			Assert.True(readOnly, "只读 TextBox 选区索引未传入 TextPresenter（git mm 输出弹窗场景）");
			Assert.True(placeholder, "PlaceholderTextBox 选区索引未传入 TextPresenter（各类弹窗输入框场景）");
		}

		// ── 像素层守卫：选中后必须真实画出 accent 高亮（有基线对照，防"恒亮"假阳性） ──

		[Fact]
		public void Selection_RendersAccentHighlightPixels_WithCleanBaseline()
		{
			HeadlessAppBootstrap.EnsureStarted();
			(int selected, int baseline) = HeadlessAppBootstrap.Run(delegate
			{
				Color accent = ResolveAccentColor();
				bool IsAccent(Color c) => c.A == accent.A && c.R == accent.R && c.G == accent.G && c.B == accent.B;

				var selected = new TextBox { Text = "0123456789ABCDEF", Width = 260, Height = 32, FontSize = 14 };
				selected.SelectionStart = 2;
				selected.SelectionEnd = 10;
				var w1 = new Window { Width = 320, Height = 90, Content = selected };
				w1.Show();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				int withSel = CountPixels(selected, IsAccent);
				w1.Close();

				var plain = new TextBox { Text = "0123456789ABCDEF", Width = 260, Height = 32, FontSize = 14 };
				var w2 = new Window { Width = 320, Height = 90, Content = plain };
				w2.Show();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				int withoutSel = CountPixels(plain, IsAccent);
				w2.Close();
				return (withSel, withoutSel);
			});
			// 选中后必须出现数百量级的 accent 蓝像素（实测 ~792）；未选中时基线应为 0（高亮只来自选区）。
			Assert.True(selected > 100, "选中后未渲染 accent 高亮像素（实测 " + selected + "）——选区不可见回归");
			Assert.Equal(0, baseline);
		}

		// ── 同因连带修复守卫：PasswordChar 掩码必须下传（密码框明文回归） ──

		[Fact]
		public void PasswordChar_ReachesTextPresenter()
		{
			HeadlessAppBootstrap.EnsureStarted();
			char propagated = HeadlessAppBootstrap.Run(delegate
			{
				var tb = new TextBox { Text = "secret123", Width = 200, Height = 30, PasswordChar = '●' };
				var window = new Window { Width = 300, Height = 100, Content = tb };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				TextPresenter presenter = tb.GetVisualDescendants().OfType<TextPresenter>().First();
				window.Close();
				return presenter.PasswordChar;
			});
			// AskPassWindow/SshPassphraseWindow 用 TextBox+PasswordChar 承担密码框语义，
			// 掩码字符不下传 presenter 即明文显示。
			Assert.Equal('●', propagated);
		}

		private delegate TextBox ControlFactory();
	}
}
