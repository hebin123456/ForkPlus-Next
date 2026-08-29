using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls.Editor
{
	public class ClearTypeLineNumberMargin : LineNumberMargin
	{
		public override void Render(DrawingContext drawingContext)
		{
			drawingContext.DrawRectangle(Theme.CodeEditor.BackgroundBrush, null, new Rect(0.0, 0.0, base.RenderSize.Width, base.RenderSize.Height));
		}
	}
}
