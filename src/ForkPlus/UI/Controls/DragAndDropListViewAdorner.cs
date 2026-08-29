using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class DragAndDropListViewAdorner : Adorner
	{
		private readonly double _visualBrushYOffset;

		private readonly Brush[] _visualBrushes;

		private readonly Point _initialPosition;

		private Point NewPosition { get; set; }

		public DragAndDropListViewAdorner(global::Avalonia.Input.InputElement adornedElement, ListBoxItem[] listBoxItems, Point position)
			: base(adornedElement)
		{
			_initialPosition = position;
			Brush[] visualBrushes = listBoxItems.Map((ListBoxItem x) => new global::Avalonia.Media.ImmutableBrush(x)
			{
				Opacity = 0.4
			});
			_visualBrushes = visualBrushes;
			_visualBrushYOffset = listBoxItems.FirstItem()?.ActualHeight ?? 0.0;
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
			for (int i = 0; i < _visualBrushes.Length; i++)
			{
				Brush brush = _visualBrushes[i];
				if (i > 0)
				{
					newPosition.Offset(0.0, _visualBrushYOffset);
				}
				context.DrawRectangle(brush, null, new Rect(newPosition, base.RenderSize));
			}
		}
	}
}
