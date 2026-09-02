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
			// WPF TextBox.Text defaults to an empty string; keep that contract for migrated callers.
			if (base.Text == null)
			{
				base.Text = string.Empty;
			}
			base.Loaded += delegate
			{
				base.ContextMenu = GetContextMenu();
			};
			// Preserve the WPF-style override points used by derived text boxes.
			TextChanged += delegate (object s, global::Avalonia.Controls.TextChangedEventArgs e)
			{
				OnTextChanged(e);
			};
			PropertyChanged += delegate (object s, global::Avalonia.AvaloniaPropertyChangedEventArgs e)
			{
				if (e.Property == global::Avalonia.Input.InputElement.IsKeyboardFocusWithinProperty)
				{
					OnIsKeyboardFocusWithinChanged(e);
				}
			};
		}

		protected virtual void OnTextChanged(global::Avalonia.Controls.TextChangedEventArgs e)
		{
		}

		protected virtual void OnIsKeyboardFocusWithinChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs e)
		{
		}

		/// <summary>WPF TextBoxBase.SelectionLength（Avalonia 无，由 SelectionStart/SelectionEnd 推导）。</summary>
		public int SelectionLength => global::System.Math.Abs(SelectionEnd - SelectionStart);

		/// <summary>WPF TextBoxBase.IsSelectionActive（Avalonia 无，近似映射 IsFocused）。</summary>
		public bool IsSelectionActive => IsFocused;

		/// <summary>WPF UIElement.IsKeyboardFocused（Avalonia 无，近似映射 IsFocused）。</summary>
		public bool IsKeyboardFocused => IsFocused;

		protected virtual ContextMenu GetContextMenu()
		{
			ContextMenu contextMenu = new ContextMenu();
			contextMenu.AddDefaultTextBoxMenuItems(this);
			global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AttachAutoDismiss(contextMenu, this);
			return contextMenu;
		}
	}
}
