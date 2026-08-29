using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class PlaceholderTextBox : TextBox
	{
		public static readonly global::Avalonia.StyledProperty<string> PlaceholderProperty =
    global::Avalonia.AvaloniaProperty.Register<PlaceholderTextBox, string>("Placeholder");

		public static readonly global::Avalonia.StyledProperty<global::Avalonia.Media.IImage> IconProperty =
    global::Avalonia.AvaloniaProperty.Register<PlaceholderTextBox, global::Avalonia.Media.IImage>("Icon");

		public string Placeholder
		{
			get
			{
				return (string)GetValue(PlaceholderProperty);
			}
			set
			{
				SetValue(PlaceholderProperty, value);
			}
		}

		public global::Avalonia.Media.IImage Icon
		{
			get
			{
				return (global::Avalonia.Media.IImage)GetValue(IconProperty);
			}
			set
			{
				SetValue(IconProperty, value);
			}
		}

		public PlaceholderTextBox()
		{
			base.Loaded += delegate
			{
				base.ContextMenu = GetContextMenu();
			};
		}

		protected virtual ContextMenu GetContextMenu()
		{
			ContextMenu contextMenu = new ContextMenu();
			contextMenu.AddDefaultTextBoxMenuItems(this);
			return contextMenu;
		}
	}
}
