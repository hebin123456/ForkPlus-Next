using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI
{
	public static class ControlTemplateExtensions
	{
		public static bool TryFindName<T>(this global::Avalonia.Controls.Templates.IControlTemplate source, string name, global::Avalonia.Controls.Control templatedParent, out T match) where T : class
		{
			object obj = source.FindName(name, templatedParent);
			match = obj as T;
			if (match != null)
			{
				return true;
			}
			return false;
		}
	}
}
