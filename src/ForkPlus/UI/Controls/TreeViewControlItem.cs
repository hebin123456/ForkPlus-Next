using System;
using ForkPlus.UI.WpfCompat;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using ForkPlus.UI.Helpers;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class TreeViewControlItem : global::Avalonia.Controls.ListBoxItem
	{
		private Point _startPoint;

		private bool _wasSelected;

		private DragAdorner _adorner;

		public MultiselectionTreeViewItem Node => base.DataContext as MultiselectionTreeViewItem;

		public MultiselectionTreeView ParentTreeView { get; internal set; }

		protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property == global::Avalonia.Controls.Control.DataContextProperty)
			{
				UpdateDataContext(e.OldValue as MultiselectionTreeViewItem, e.NewValue as MultiselectionTreeViewItem);
			}
		}

		private void UpdateDataContext(MultiselectionTreeViewItem oldNode, MultiselectionTreeViewItem newNode)
		{
			if (newNode != null)
			{
				newNode.PropertyChanged += Node_PropertyChanged;
				if (base.Template != null)
				{
					UpdateTemplate();
				}
			}
			if (oldNode != null)
			{
				oldNode.PropertyChanged -= Node_PropertyChanged;
			}
		}

		private void Node_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "IsExpanded" && Node.IsExpanded)
			{
				ParentTreeView.HandleExpanding(Node);
			}
		}

		private void UpdateTemplate()
		{
		}

		internal double CalculateIndent()
		{
			int num = 19 * Node.Level;
			num -= 19;
			if (num < 0)
			{
				return 0.0;
			}
			return num;
		}

		protected override void OnPointerPressed(global::Avalonia.Input.PointerPressedEventArgs e)
		{
			_wasSelected = base.IsSelected;
			if (!base.IsSelected)
			{
				base.OnPointerPressed(e);
			}
			if (ParentTreeView.AllowDragDrop && Mouse.LeftButton == MouseButtonState.Pressed)
			{
				_startPoint = e.GetPosition(null);
				this.CaptureMouse();
			}
		}

		protected override void OnPointerMoved(global::Avalonia.Input.PointerEventArgs e)
		{
			if (!base.IsMouseCaptured)
			{
				return;
			}
			Point position = e.GetPosition(null);
			if (!(Math.Abs(position.X - _startPoint.X) >= SystemParameters.MinimumHorizontalDragDistance) && !(Math.Abs(position.Y - _startPoint.Y) >= SystemParameters.MinimumVerticalDragDistance))
			{
				return;
			}
			_adorner = new DragAdorner(this, e.GetPosition(this));
			if (_adorner != null)
			{
				AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(ParentTreeView);
				if (adornerLayer != null)
				{
					adornerLayer.Add(_adorner);
					MultiselectionTreeViewItem[] nodes = ParentTreeView.GetTopLevelSelection().ToArray();
					Node?.StartDrag(this, nodes);
					adornerLayer.Remove(_adorner);
				}
			}
		}

		protected void OnGiveFeedback(GiveFeedbackEventArgs e)
		{
			if (base.IsVisible && _adorner != null)
			{
				Point position = this.PointFromScreen(MouseHelper.GetMousePosition());
				_adorner.UpdatePosition(position);
			}
		}

		protected override void OnPointerReleased(global::Avalonia.Input.PointerReleasedEventArgs e)
		{
			this.ReleaseMouseCapture();
			if (_wasSelected)
			{
				base.OnPointerPressed(e);
			}
		}

		protected void OnDragEnter(DragEventArgs e)
		{
			ParentTreeView.HandleDragEnter(this, e);
		}

		protected void OnDragOver(DragEventArgs e)
		{
			ParentTreeView.HandleDragOver(this, e);
		}

		protected void OnDrop(DragEventArgs e)
		{
			ParentTreeView.HandleDrop(this, e);
		}

		protected void OnDragLeave(DragEventArgs e)
		{
			ParentTreeView.HandleDragLeave(this, e);
		}
	}
}
