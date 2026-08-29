using Avalonia;
using Avalonia.Controls;
using ForkPlus.UI.CustomCommands;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs
{
	public class CustomCommandCheckBox : CheckBox
	{
		private CustomCommandUI.Control.CheckBox _checkBox;

		public string CheckedValue => _checkBox.CheckedValue;

		public string UncheckedValue => _checkBox.UncheckedValue;

		public CustomCommandCheckBox(CustomCommandUI.Control.CheckBox checkBox)
		{
			this.SetResourceReference(global::Avalonia.Controls.Control.StyleProperty, typeof(CheckBox));
			_checkBox = checkBox;
			base.Content = checkBox.Title;
			base.IsChecked = checkBox.DefaultValue;
		}
	}
}
