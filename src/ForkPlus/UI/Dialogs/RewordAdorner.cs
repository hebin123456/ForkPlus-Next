using Avalonia;
using ForkPlus.UI.WpfCompat;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using ForkPlus.UI;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs
{
	public class RewordAdorner : Adorner
	{
		private global::Avalonia.Controls.Control _child;

		public global::Avalonia.Controls.Control Child
		{
			get
			{
				return _child;
			}
			set
			{
				if (_child != value)
				{
					if (_child != null)
					{
						RemoveVisualChild(_child);
						RemoveLogicalChild(_child);
					}
					if (value != null && !VisualTreeAttachmentHelper.PrepareForNewParent(value, GetType().Name + ".Child"))
					{
						value = null;
					}
					_child = value;
					if (_child != null)
					{
						AddLogicalChild(_child);
						AddVisualChild(_child);
					}
					InvalidateMeasure();
				}
			}
		}

		protected override int VisualChildrenCount => (Child != null) ? 1 : 0;

		public RewordAdorner(global::Avalonia.Input.InputElement adornernedElement)
			: base(adornernedElement)
		{
		}

		protected override Visual GetVisualChild(int index)
		{
			return Child;
		}

		protected override Size MeasureOverride(Size constraint)
		{
			if (Child == null)
			{
				return default(Size);
			}
			Child.Measure(constraint);
			Size result = Child.DesiredSize;
			if (result.Width < 40.0)
			{
				result = new Size(40.0, result.Height);
			}
			return result;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			if (Child == null)
			{
				return default(Size);
			}
			Child.Arrange(new Rect(finalSize));
			return finalSize;
		}
	}
}
