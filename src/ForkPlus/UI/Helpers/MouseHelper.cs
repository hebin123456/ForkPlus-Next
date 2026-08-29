using ForkPlus.UI.Helpers;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Helpers
{
	internal static class MouseHelper
	{
		private struct Win32Point
		{
			public int X;

			public int Y;
		}

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetCursorPos(ref Win32Point pt);

		public static Point GetMousePosition()
		{
			Win32Point pt = default(Win32Point);
			GetCursorPos(ref pt);
			return new Point(pt.X, pt.Y);
		}
	}
}
