using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// Migration note（根因）：WPF 原实现 override OnChecked/OnUnchecked（WPF ToggleButton 的虚方法），
	/// Avalonia ToggleButton 无这两个虚方法 → 方法从未被调用，下拉菜单永远打不开。
	/// Avalonia 对应虚方法为 OnIsCheckedChanged（12.1.1 实证 virtual），在此打开/关闭 ContextMenu。
	/// </summary>
	public class DropDownButton : ToggleButton
	{
		protected override void OnIsCheckedChanged(RoutedEventArgs e)
		{
			base.OnIsCheckedChanged(e);
			if (base.IsChecked == true)
			{
				OpenDropdown();
			}
			else
			{
				CloseDropdown();
			}
		}

		private void OpenDropdown()
		{
			ContextMenu contextMenu = base.ContextMenu;
			if (contextMenu == null || contextMenu.IsOpen)
			{
				return;
			}
			// 兼容 WPF 行为：下拉菜单宽度至少与触发按钮等宽（原版不会因某个长文本把菜单撑得很宽）。
			// 这里用 MinWidth，既保证等宽，又避免某些菜单需要更宽时被硬裁剪。
			contextMenu.MinWidth = Math.Max(contextMenu.MinWidth, Bounds.Width);
			contextMenu.PlacementTarget = this;
			contextMenu.Placement = PlacementMode.Bottom;

			global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AttachAutoDismiss(contextMenu, this);
			contextMenu.Closed -= ContextMenu_Closed;
			contextMenu.Closed += ContextMenu_Closed;
			contextMenu.Open();
		}

		private void CloseDropdown()
		{
			ContextMenu contextMenu = base.ContextMenu;
			if (contextMenu == null)
			{
				return;
			}
			contextMenu.Closed -= ContextMenu_Closed;
			contextMenu.Close();
		}

		/// <summary>菜单关闭（选中项/点击别处/按 Esc）后复位按钮选中态，保证再次点击能重新打开。</summary>
		private void ContextMenu_Closed(object sender, RoutedEventArgs e)
		{
			if (sender is ContextMenu contextMenu)
			{
				contextMenu.Closed -= ContextMenu_Closed;
			}
			SetCurrentValue(IsCheckedProperty, false);
		}
	}
}
