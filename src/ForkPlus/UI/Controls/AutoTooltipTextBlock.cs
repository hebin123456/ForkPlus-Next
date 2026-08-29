using Avalonia;
using ForkPlus.UI.WpfCompat;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class AutoTooltipTextBlock : TextBlock
	{
		public static readonly global::Avalonia.StyledProperty<string> CustomToolTipProperty =
    global::Avalonia.AvaloniaProperty.Register<AutoTooltipTextBlock, string>("CustomToolTip");

		public string CustomToolTip
		{
			get
			{
				return (string)GetValue(CustomToolTipProperty);
			}
			set
			{
				SetValue(CustomToolTipProperty, value);
			}
		}

		public AutoTooltipTextBlock()
		{
			base.TextTrimming = TextTrimming.CharacterEllipsis;
			global::Avalonia.Controls.ToolTip.SetTip(base,"");
			// TODO 迁移：WPF FrameworkElement.ToolTipOpening 事件 → Avalonia ToolTip.ToolTipOpeningEvent 路由事件订阅。
			// （lambda 内不可用 base 关键字，改用 this。）
			global::Avalonia.Controls.ToolTip.AddToolTipOpeningHandler(this, (s, e) =>
			{
				if (CustomToolTip != null)
				{
					global::Avalonia.Controls.ToolTip.SetTip(this, CustomToolTip);
				}
				else if (TextIsTrimmed())
				{
					global::Avalonia.Controls.ToolTip.SetTip(this, this.Text);
				}
				else
				{
					e.Handled = true;
				}
			});
		}

		private bool TextIsTrimmed()
		{
			Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
			return base.Bounds.Width < base.DesiredSize.Width;
		}
	}
}
