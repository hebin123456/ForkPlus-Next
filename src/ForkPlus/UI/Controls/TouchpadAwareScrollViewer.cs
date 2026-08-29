using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class TouchpadAwareScrollViewer : ScrollViewer
	{
		private const int WM_MOUSEHWHEEL = 526;

		[Null]
		private HwndSource _parentHwndSource;

		private static int HiWord(IntPtr wParam)
		{
			return (short)(wParam.ToInt64() >> 16);
		}

		public TouchpadAwareScrollViewer()
		{
			base.Loaded += delegate
			{
				if (global::ForkPlus.DesignTimeHelper.IsInDesignMode())
				{
					return;
				}
				_parentHwndSource = PresentationSource.FromVisual(this) as HwndSource;
				_parentHwndSource?.AddHook(MouseHorizontalScrollHook);
			};
			base.Unloaded += delegate
			{
				_parentHwndSource?.RemoveHook(MouseHorizontalScrollHook);
				_parentHwndSource = null;
			};
		}

		protected override void OnPointerWheelChanged(global::Avalonia.Input.PointerWheelEventArgs e)
		{
			if (e.Handled)
			{
				return;
			}
			if (base.ScrollInfo != null)
			{
				if (Keyboard.Modifiers == global::Avalonia.Input.KeyModifiers.Shift)
				{
					if (e.Delta < 0)
					{
						base.ScrollInfo.LineRight();
					}
					else
					{
						base.ScrollInfo.LineLeft();
					}
					InvalidateScrollInfo();
				}
				else if (Math.Abs(e.Delta) < 120)
				{
					if (e.Delta < 0)
					{
						base.ScrollInfo.LineDown();
					}
					else
					{
						base.ScrollInfo.LineUp();
					}
					InvalidateScrollInfo();
				}
				else
				{
					if (e.Delta < 0)
					{
						base.ScrollInfo.MouseWheelDown();
					}
					else
					{
						base.ScrollInfo.MouseWheelUp();
					}
					InvalidateScrollInfo();
				}
			}
			e.Handled = true;
		}

		private IntPtr MouseHorizontalScrollHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			if (msg == 526)
			{
				if (base.ScrollInfo == null || !base.IsMouseOver || base.ComputedHorizontalScrollBarVisibility != 0)
				{
					return IntPtr.Zero;
				}
				if (HiWord(wParam) < 0)
				{
					base.ScrollInfo.LineLeft();
				}
				else
				{
					base.ScrollInfo.LineRight();
				}
				InvalidateScrollInfo();
				handled = true;
			}
			return IntPtr.Zero;
		}
	}
}
