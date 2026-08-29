using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI
{
	public static class DependencyObjectExtensions
	{
		[Null]
		public static T GetParent<T>(this global::Avalonia.AvaloniaObject _this) where T : global::Avalonia.AvaloniaObject
		{
			global::Avalonia.AvaloniaObject dependencyObject = _this;
			while (dependencyObject != null && !(dependencyObject is T))
			{
				dependencyObject = global::Avalonia.VisualTreeExtensions.GetVisualParent(dependencyObject);
			}
			return dependencyObject as T;
		}
	}
}
