using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class RepositoryManagerEditableTextBlock : EditableTextBlock
	{
		protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property != EditableTextBlock.IsInEditModeProperty)
			{
				return;
			}
			if ((bool)e.NewValue)
			{
				ShowEditor(base.Value, delegate(bool success, string newString)
				{
					if (success)
					{
						base.Value = newString;
					}
					base.IsInEditMode = false;
				});
			}
			else
			{
				HideEditor();
				Focus();
			}
		}
	}
}
