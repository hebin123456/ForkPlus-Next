using ForkPlus.UI.Helpers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Helpers
{
	public static class ListViewScrollbarDoubleClickHelper
	{
		public static bool IsClickedOnScrollbar(this global::Avalonia.Input.PointerPressedEventArgs args)
		{
			global::Avalonia.AvaloniaObject dependencyObject = args.OriginalSource as global::Avalonia.AvaloniaObject;
			while (dependencyObject != null && !(dependencyObject is global::Avalonia.Controls.ListBoxItem))
			{
				dependencyObject = ((!(dependencyObject is Run)) ? global::Avalonia.VisualTreeExtensions.GetVisualParent(dependencyObject) : (dependencyObject as Run).Parent);
			}
			if (dependencyObject == null)
			{
				return true;
			}
			return false;
		}
	}
}
