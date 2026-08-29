using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	internal class HighlightableTextBlock : TextBlock
	{
		public static readonly global::Avalonia.StyledProperty<string> HighlightPatternProperty =
    global::Avalonia.AvaloniaProperty.RegisterAttached<HighlightableTextBlock, global::Avalonia.AvaloniaObject, string>("HighlightString");

		public string HighlightString
		{
			get
			{
				return (string)GetValue(HighlightPatternProperty);
			}
			set
			{
				SetValue(HighlightPatternProperty, value);
			}
		}
	}
}
