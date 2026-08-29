using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI
{
	public static class ItemsControlExtensions
	{
		public static object GetObjectAtPoint<ItemContainer>(this ItemsControl control, Point p) where ItemContainer : global::Avalonia.AvaloniaObject
		{
			ItemContainer containerAtPoint = control.GetContainerAtPoint<ItemContainer>(p);
			if (containerAtPoint == null)
			{
				return null;
			}
			return control.ItemContainerGenerator.ItemFromContainer(containerAtPoint);
		}

		public static ItemContainer GetContainerAtPoint<ItemContainer>(this ItemsControl control, Point p) where ItemContainer : global::Avalonia.AvaloniaObject
		{
			HitTestResult hitTestResult = VisualTreeHelper.HitTest(control, p);
			if (hitTestResult == null)
			{
				return null;
			}
			global::Avalonia.AvaloniaObject dependencyObject = hitTestResult.VisualHit;
			while (global::Avalonia.VisualTreeExtensions.GetVisualParent(dependencyObject) != null && !(dependencyObject is ItemContainer))
			{
				dependencyObject = global::Avalonia.VisualTreeExtensions.GetVisualParent(dependencyObject);
			}
			return dependencyObject as ItemContainer;
		}

		public static void FocusSelectedItem(this Selector control)
		{
			if (control.SelectedIndex >= 0 && control.ItemContainerGenerator.ContainerFromIndex(control.SelectedIndex) is IInputElement element)
			{
				(element).Focus();
			}
		}
	}
}
