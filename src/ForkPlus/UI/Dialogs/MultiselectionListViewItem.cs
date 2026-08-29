using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using ForkPlus.UI.Helpers;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs
{
	public class MultiselectionListViewItem : global::Avalonia.Controls.ListBoxItem
	{
		private bool _wasSelected;

		private Point _dragStartPoint;

		private DragAndDropListBoxAdorner _adorner;

		private DropPlaceAdorner _dropAdorner;

		public MultiselectionListView ParentListView { get; internal set; }

		public DropPosition DropPosition { get; internal set; }

		protected override void OnPointerPressed(global::Avalonia.Input.PointerPressedEventArgs e)
		{
			_wasSelected = base.IsSelected;
			if (!base.IsSelected)
			{
				base.OnPointerPressed(e);
			}
			if (Mouse.LeftButton == MouseButtonState.Pressed)
			{
				_dragStartPoint = e.GetPosition(null);
				CaptureMouse();
			}
		}

		protected override void OnPointerReleased(global::Avalonia.Input.PointerReleasedEventArgs e)
		{
			ReleaseMouseCapture();
			if (_wasSelected)
			{
				base.OnPointerPressed(e);
			}
		}

		protected override void OnDoubleTapped(global::Avalonia.Input.TappedEventArgs e)
		{
			e.Handled = true;
			base.OnDoubleTapped(e);
		}

		protected override void OnPointerMoved(global::Avalonia.Input.PointerEventArgs e)
		{
			if (!base.IsMouseCaptured)
			{
				return;
			}
			Point position = e.GetPosition(null);
			if (!ExceedDragDistance(_dragStartPoint - position))
			{
				return;
			}
			RevisionEntry[] array = ParentListView.SelectedItems.CompactMap((object x) => x as RevisionEntry);
			if (array.Length < 1)
			{
				return;
			}
			global::Avalonia.Controls.ListBoxItem[] array2 = array.CompactMap((RevisionEntry x) => ParentListView.ItemContainerGenerator.ContainerFromItem(x) as global::Avalonia.Controls.ListBoxItem);
			ListBoxItem[] listBoxItems = array2;
			_adorner = new DragAndDropListBoxAdorner(this, listBoxItems, e.GetPosition(this));
			if (_adorner != null)
			{
				AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(ParentListView);
				if (adornerLayer != null)
				{
					adornerLayer.Add(_adorner);
					DragDrop.DoDragDrop(this, array, DragDropEffects.Move);
					adornerLayer.Remove(_adorner);
				}
			}
		}

		protected void OnGiveFeedback(GiveFeedbackEventArgs e)
		{
			if (base.IsVisible && _adorner != null)
			{
				Point position = PointFromScreen(MouseHelper.GetMousePosition());
				_adorner.UpdatePosition(position);
			}
		}

		private static bool ExceedDragDistance(Vector diff)
		{
			if (!(Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance))
			{
				return Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance;
			}
			return true;
		}

		protected void OnDragEnter(DragEventArgs e)
		{
			ClearDropAdorner();
			DropPosition = GetDropPositoion(e);
			ShowDropAdorner(DropPosition);
		}

		protected void OnDrop(DragEventArgs e)
		{
			ClearDropAdorner();
		}

		protected void OnDragLeave(DragEventArgs e)
		{
			ClearDropAdorner();
		}

		private DropPosition GetDropPositoion(DragEventArgs e)
		{
			double y = e.GetPosition(this).Y;
			double actualHeight = base.ActualHeight;
			if (!(y < actualHeight / 2.0))
			{
				return DropPosition.Bottom;
			}
			return DropPosition.Top;
		}

		private void ShowDropAdorner(DropPosition dropPosition)
		{
			_dropAdorner = new DropPlaceAdorner(this, dropPosition);
			if (_dropAdorner != null)
			{
				AdornerLayer.GetAdornerLayer(ParentListView)?.Add(_dropAdorner);
			}
		}

		private void ClearDropAdorner()
		{
			if (_dropAdorner != null)
			{
				AdornerLayer.GetAdornerLayer(ParentListView)?.Remove(_dropAdorner);
			}
		}
	}
}
