using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.UserControls.Preferences
{
	public class DragAndDropListBox : ListBox
	{
		protected global::Avalonia.AvaloniaObject GetContainerForItemOverride()
		{
			return new DragAndDropListBoxItem();
		}

		protected bool IsItemItsOwnContainerOverride(object item)
		{
			return item is DragAndDropListBoxItem;
		}

		protected override void PrepareContainerForItemOverride(global::Avalonia.Controls.Control element, object item, int index)
		{
			base.PrepareContainerForItemOverride(element, item, index);
			(element as DragAndDropListBoxItem).ParentListBox = this;
		}
	}
}
