using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls.Editor
{
	public class FloatingButton : Button
	{
		private WeakReference<TextEditor> _weakEditor;

		public FloatingButton(TextEditor editor)
		{
			_weakEditor = new WeakReference<TextEditor>(editor);
		}

		protected override void OnPointerWheelChanged(global::Avalonia.Input.PointerWheelEventArgs e)
		{
			e.Handled = true;
			global::Avalonia.Input.PointerWheelEventArgs mouseWheelEventArgs = new global::Avalonia.Input.PointerWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta);
			mouseWheelEventArgs.RoutedEvent = global::Avalonia.Input.InputElement.MouseWheelEvent;
			mouseWheelEventArgs.Source = this;
			if (_weakEditor.TryGetTarget(out var target))
			{
				target.TextArea.TextView.RaiseEvent(mouseWheelEventArgs);
			}
		}
	}
}
