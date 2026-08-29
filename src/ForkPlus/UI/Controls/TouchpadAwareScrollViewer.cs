using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// WPF 版通过 Win32 HwndSource 钩 WM_MOUSEHWHEEL 处理触控板横向滚动，
	/// 并把小幅滚轮增量（&lt;120）当作"触控板平滑滚动"逐行滚动。
	/// Avalonia 11+ 的 PointerWheelEventArgs.Delta 为 Vector(X=横向, Y=纵向)，
	/// 横向滚动已由基类原生处理，无需 Win32 钩子。
	/// TODO 迁移：保留类名以兼容 XAML 引用；如需恢复"小步长=逐行"的触控板手感，
	/// 可在此 override OnPointerWheelChanged 按 |Delta.Y| 阈值分派 Line/SmallStep 滚动。
	/// </summary>
	public class TouchpadAwareScrollViewer : ScrollViewer
	{
	}
}
