using Avalonia;
using Avalonia.Controls;
using ForkPlus.UI.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs
{
	public class MultiselectionListView : global::Avalonia.Controls.ListBox
	{
		private readonly DragAutoScrollHelper _dragAutoScroll;

		public MultiselectionListView()
		{
			_dragAutoScroll = new DragAutoScrollHelper(this);
		}

		protected global::Avalonia.AvaloniaObject GetContainerForItemOverride()
		{
			return new MultiselectionListViewItem();
		}

		protected bool IsItemItsOwnContainerOverride(object item)
		{
			return item is MultiselectionListViewItem;
		}

		protected override void PrepareContainerForItemOverride(global::Avalonia.Controls.Control element, object item, int index)
		{
			base.PrepareContainerForItemOverride(element, item, index);
			(element as MultiselectionListViewItem).ParentListView = this;
		}
	}
}
