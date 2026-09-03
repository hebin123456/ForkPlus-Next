// 回归测试（2026-09-03，"git 错误弹窗选中内容没有选中样式"修复产物）：
// 根因：迁移版自定义 TextBox ControlTheme 的模板 TextPresenter 只 TemplateBinding 了 Text，
// SelectionBrush/SelectionForegroundBrush 从未传入 presenter（实测 null），选区无任何高亮。
// WPF 原版 TextBox 选区为系统高亮蓝底白字（IsInactiveSelectionHighlightEnabled 默认 false，
// 活动/非活动选区均为系统蓝）。修复：主题 Setter 设 SelectionBrush=AccentBrush（应用 accent，
// 注意 Avalonia Fluent 无 SystemAccentBrush 画刷资源，DynamicResource 会解析为 null）+
// SelectionForegroundBrush=白，模板 TextPresenter 补两个 TemplateBinding。
// 本测试守卫：ErrorWindow 的错误内容框、普通 TextBox、PlaceholderTextBox 派生主题的
// TextPresenter 选区画刷必须非空且背景/前景不同色（否则选中文字不可读）。
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;
using System.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class TextBoxSelectionStyleTests
	{
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
	}
}
