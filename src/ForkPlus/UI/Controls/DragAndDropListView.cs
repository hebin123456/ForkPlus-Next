using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	internal class DragAndDropListView : NoUIAutomationListView
	{
		public EventHandler<EventArgs> ItemDrag;

		private readonly DragAutoScrollHelper _dragAutoScroll;

		public DragAndDropListView()
		{
			_dragAutoScroll = new DragAutoScrollHelper(this);
		}

		internal void StopDragAutoScroll()
		{
			_dragAutoScroll.StopAutoScroll();
		}

		protected global::Avalonia.AvaloniaObject GetContainerForItemOverride()
		{
			return new DragAndDropListViewItem();
		}

		protected bool IsItemItsOwnContainerOverride(object item)
		{
			return item is DragAndDropListViewItem;
		}

		protected override void PrepareContainerForItemOverride(global::Avalonia.Controls.Control element, object item, int index)
		{
			base.PrepareContainerForItemOverride(element, item, index);
			(element as DragAndDropListViewItem).ParentListView = this;
		}
	}
}
