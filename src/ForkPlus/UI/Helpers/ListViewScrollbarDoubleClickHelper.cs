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
			return IsSourceInsideListBoxItem(args.Source);
		}

		// TODO 迁移：WPF MouseButtonEventArgs（双击/单击）→ Avalonia TappedEventArgs 重载。
		public static bool IsClickedOnScrollbar(this global::Avalonia.Input.TappedEventArgs args)
		{
			return IsSourceInsideListBoxItem(args.Source);
		}

		private static bool IsSourceInsideListBoxItem(object source)
		{
			global::Avalonia.AvaloniaObject dependencyObject = source as global::Avalonia.AvaloniaObject;
			while (dependencyObject != null && !(dependencyObject is global::Avalonia.Controls.ListBoxItem))
			{
				dependencyObject = ((!(dependencyObject is Run)) ? global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(dependencyObject) : (dependencyObject as Run).Parent);
			}
			if (dependencyObject == null)
			{
				return true;
			}
			return false;
		}
	}
}
