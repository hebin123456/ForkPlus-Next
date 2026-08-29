using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI
{
	internal static class VisualTreeAttachmentHelper
	{
		public static bool TryAddChild(Panel panel, global::Avalonia.Input.InputElement child, string targetDescription)
		{
			if (panel == null)
			{
				return false;
			}
			if (child == null)
			{
				return true;
			}
			if (!PrepareForNewParent(child, targetDescription))
			{
				return false;
			}
			panel.Children.Add(child);
			return true;
		}

		public static bool TrySetChild(Decorator decorator, global::Avalonia.Input.InputElement child, string targetDescription)
		{
			if (decorator == null)
			{
				return false;
			}
			if (child != null && !PrepareForNewParent(child, targetDescription))
			{
				return false;
			}
			decorator.Child = child;
			return true;
		}

		public static bool TrySetPopupChild(Popup popup, global::Avalonia.Input.InputElement child, string targetDescription)
		{
			if (popup == null)
			{
				return false;
			}
			if (child != null && !PrepareForNewParent(child, targetDescription))
			{
				return false;
			}
			popup.Child = child;
			return true;
		}

		public static bool TrySetContent(ContentControl contentControl, object content, string targetDescription)
		{
			if (contentControl == null)
			{
				return false;
			}
			if (content is global::Avalonia.AvaloniaObject dependencyObject && !PrepareForNewParent(dependencyObject, targetDescription))
			{
				return false;
			}
			contentControl.Content = content;
			return true;
		}

		public static bool PrepareForNewParent(global::Avalonia.AvaloniaObject child, string targetDescription)
		{
			if (child == null)
			{
				return true;
			}
			global::Avalonia.AvaloniaObject parent = GetParent(child);
			if (parent == null)
			{
				return true;
			}
			if (!DetachFromParent(child, parent))
			{
				if (GetParent(child) == null)
				{
					return true;
				}
				Log.Warn("Cannot detach " + Describe(child) + " from " + Describe(parent) + " before attaching to " + targetDescription + ".");
				return false;
			}
			global::Avalonia.AvaloniaObject parent2 = GetParent(child);
			if (parent2 != null)
			{
				Log.Warn("Detached " + Describe(child) + " from " + Describe(parent) + " but it is still parented by " + Describe(parent2) + " before attaching to " + targetDescription + ".");
				return false;
			}
			return true;
		}

		public static string Describe(global::Avalonia.AvaloniaObject item)
		{
			if (item == null)
			{
				return "null";
			}
			if (item is global::Avalonia.Controls.Control frameworkElement && !string.IsNullOrEmpty(frameworkElement.Name))
			{
				return item.GetType().Name + "('" + frameworkElement.Name + "')";
			}
			if (item is FrameworkContentElement frameworkContentElement && !string.IsNullOrEmpty(frameworkContentElement.Name))
			{
				return item.GetType().Name + "('" + frameworkContentElement.Name + "')";
			}
			return item.GetType().Name;
		}

		private static global::Avalonia.AvaloniaObject GetParent(global::Avalonia.AvaloniaObject child)
		{
			global::Avalonia.AvaloniaObject parent = LogicalTreeHelper.GetParent(child);
			if (parent != null)
			{
				return parent;
			}
			if (child is Visual || child is Visual3D)
			{
				return global::Avalonia.VisualTreeExtensions.GetVisualParent(child);
			}
			return null;
		}

		private static bool DetachFromParent(global::Avalonia.AvaloniaObject child, global::Avalonia.AvaloniaObject parent)
		{
			if (parent is Popup popup && child is global::Avalonia.Input.InputElement uIElement && ReferenceEquals(popup.Child, uIElement))
			{
				popup.Child = null;
				return true;
			}
			if (parent is Panel panel && child is global::Avalonia.Input.InputElement uIElement2 && panel.Children.Contains(uIElement2))
			{
				panel.Children.Remove(uIElement2);
				return true;
			}
			if (parent is Decorator decorator && child is global::Avalonia.Input.InputElement uIElement3 && ReferenceEquals(decorator.Child, uIElement3))
			{
				decorator.Child = null;
				return true;
			}
			if (parent is HeaderedContentControl headeredContentControl && ReferenceEquals(headeredContentControl.Header, child))
			{
				headeredContentControl.Header = null;
				return true;
			}
			if (parent is HeaderedItemsControl headeredItemsControl && ReferenceEquals(headeredItemsControl.Header, child))
			{
				headeredItemsControl.Header = null;
				return true;
			}
			if (parent is ContentControl contentControl && ReferenceEquals(contentControl.Content, child))
			{
				contentControl.Content = null;
				return true;
			}
			if (parent is ContentPresenter contentPresenter && ReferenceEquals(contentPresenter.Content, child))
			{
				contentPresenter.Content = null;
				return true;
			}
			if (parent is ItemsControl itemsControl && itemsControl.Items.Contains(child))
			{
				itemsControl.Items.Remove(child);
				return true;
			}
			return false;
		}
	}
}
