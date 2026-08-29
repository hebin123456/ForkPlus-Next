using System;
using ForkPlus.UI.WpfCompat;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	// TODO 迁移：WPF Control 自带 Padding/FontSize/FontWeight/Foreground 等属性并支持 ControlTheme 的
	// Template Setter；Avalonia 把这些属性下放到 TemplatedControl，故基类由 Control 改为 TemplatedControl
	//（Commonresources.axaml 的 ControlTheme 正是给本控件设置 Template 并 TemplateBinding Padding）。
	public class EditableTextBlock : TemplatedControl
	{
		public static readonly global::Avalonia.StyledProperty<string> ValueProperty =
    global::Avalonia.AvaloniaProperty.Register<EditableTextBlock, string>("Value", null);

		public static readonly global::Avalonia.StyledProperty<bool> IsInEditModeProperty =
    global::Avalonia.AvaloniaProperty.Register<EditableTextBlock, bool>("IsInEditMode", false);

		protected CustomAdorner _adorner;

		public string Value
		{
			get
			{
				return (string)GetValue(ValueProperty);
			}
			set
			{
				SetValue(ValueProperty, value);
			}
		}

		public bool IsInEditMode
		{
			get
			{
				return (bool)GetValue(IsInEditModeProperty);
			}
			set
			{
				SetValue(IsInEditModeProperty, value);
			}
		}

		public void ShowEditor(string text, Action<bool, string> editedCallback, bool centeredHorizontally = false)
		{
			if (_adorner != null)
			{
				HideEditor();
			}
			// TODO 迁移：AdornerLayer 与 Avalonia.Controls.Primitives.AdornerLayer 二义性，显式用 WpfCompat 版本。
			global::ForkPlus.UI.WpfCompat.AdornerLayer adornerLayer = global::ForkPlus.UI.WpfCompat.AdornerLayer.GetAdornerLayer(this);
			if (adornerLayer == null)
			{
				return;
			}
			_adorner = new CustomAdorner(this, centeredHorizontally);
			_adorner.HorizontalAlignment = base.HorizontalAlignment;
			_adorner.VerticalAlignment = base.VerticalAlignment;
			_adorner.Child = CreateAdornerTextBox(text, editedCallback);
			adornerLayer.Add(_adorner);
			IsInEditMode = true;
		}

		public void HideEditor()
		{
			if (_adorner != null)
			{
				_adorner.Child = null;
				global::ForkPlus.UI.WpfCompat.AdornerLayer.GetAdornerLayer(this)?.Remove(_adorner);
				_adorner = null;
				IsInEditMode = false;
			}
		}

		private TextBox CreateAdornerTextBox(string text, Action<bool, string> editedCallback)
		{
			TextBox textBox = new TextBox();
			textBox.HorizontalAlignment = base.HorizontalAlignment;
			textBox.VerticalAlignment = base.VerticalAlignment;
			textBox.MaxWidth = base.MaxWidth;
			textBox.Height = base.Height;
			textBox.Padding = base.Padding;
			textBox.Margin = new Thickness(-3.0, 1.0, 0.0, 0.0);
			textBox.FontSize = base.FontSize;
			textBox.Text = text;
			textBox.SelectAll();
			textBox.LayoutUpdated += delegate
			{
				textBox.Focus();
			};
			textBox.AddHandler(global::Avalonia.Input.InputElement.KeyDownEvent,delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Return)
				{
					e.Handled = true;
					editedCallback(arg1: true, textBox.Text);
				}
				else if (e.Key == Key.Escape)
				{
					e.Handled = true;
					editedCallback(arg1: false, textBox.Text);
				}
			},global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
			// TODO 迁移：Avalonia TextBox 无 LostKeyboardFocus（WPF 键盘焦点事件），等价用 LostFocus。
			textBox.LostFocus += delegate
			{
				if (IsInEditMode)
				{
					editedCallback(arg1: true, textBox.Text);
				}
			};
			return textBox;
		}
	}
}
