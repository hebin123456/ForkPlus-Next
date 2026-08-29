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
			// TODO 迁移：WPF DependencyObject 可视树遍历 → Avalonia Visual（GetVisualParent 需要 Visual）。
			global::Avalonia.Visual dependencyObject = source as global::Avalonia.Visual;
			while (dependencyObject != null && !(dependencyObject is global::Avalonia.Controls.ListBoxItem))
			{
				dependencyObject = ((!(dependencyObject is Run)) ? global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(dependencyObject) : ((dependencyObject as Run).Parent as global::Avalonia.Visual));
			}
			if (dependencyObject == null)
			{
				return true;
			}
			return false;
		}
	}
}
