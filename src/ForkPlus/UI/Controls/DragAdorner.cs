using Avalonia;
using ForkPlus.UI.WpfCompat;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class DragAdorner : Adorner
	{
		private Brush _visualBrush;

		private Point _initialPosition;

		private Point NewPosition { get; set; }

		public DragAdorner(global::Avalonia.Input.InputElement adornedElement, Point position)
			: base(adornedElement)
		{
			_initialPosition = position;
			_visualBrush = new global::Avalonia.Media.ImmutableBrush(base.AdornedElement)
			{
				Opacity = 0.6
			};
			base.IsHitTestVisible = false;
		}

		public void UpdatePosition(Point position)
		{
			NewPosition = position;
			InvalidateVisual();
		}

		public override void Render(DrawingContext context)
		{
			Point newPosition = NewPosition;
			newPosition.Offset(0.0 - _initialPosition.X, 0.0 - _initialPosition.Y);
			context.DrawRectangle(_visualBrush, null, new Rect(newPosition, base.RenderSize));
		}
	}
}
