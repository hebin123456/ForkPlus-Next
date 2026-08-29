using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// WPF 版通过返回 StubWindowAutomationPeer 屏蔽 UI Automation 暴露（性能优化）。
	/// Avalonia 无 FrameworkElementAutomationPeer 等价 API，已随迁移移除。
	/// TODO 迁移：UpdateResizableColumnWidth 依赖 WPF ListView+GridView 列宽模型，
	/// Avalonia ListBox 无 GridView；该方法暂为 no-op，列宽自适应待重新设计。
	/// </summary>
	public class NoUIAutomationListView : global::Avalonia.Controls.ListBox
	{
		public enum SelectOptions
		{
			None,
			ScrollIntoView,
			Focus
		}

		public bool IsMultiselectionInProgress { get; set; }

		public double AvailableWidth => base.Bounds.Width - 15.0 - 4.0 - 4.0;

		public void Select(int row, SelectOptions options = (SelectOptions)3)
		{
			Select(new int[1] { row }, options);
		}

		public void Select(IReadOnlyList<int> rows, SelectOptions options = (SelectOptions)3)
		{
			if (rows.Count == 0)
			{
				return;
			}
			IsMultiselectionInProgress = true;
			base.SelectedItems.Clear();
			for (int i = 0; i < rows.Count; i++)
			{
				if (i == rows.Count - 1)
				{
					IsMultiselectionInProgress = false;
				}
				base.SelectedItems.Add(base.Items[rows[i]]);
			}
			if ((options & SelectOptions.ScrollIntoView) != 0)
			{
				ScrollRowIntoView(this, rows[0]);
			}
			if ((options & SelectOptions.Focus) != 0)
			{
				SetKeyboardFocus(this, rows[0]);
			}
		}

		private static void ScrollRowIntoView(ListBox listBox, int row)
		{
			// WPF 版沿视觉树找内嵌 ScrollViewer 控制偏移；Avalonia ListBox 自带
			// ScrollIntoView（生成容器并滚动到位），直接使用。
			listBox.ScrollIntoView(listBox.Items[row < listBox.ItemCount ? row : listBox.ItemCount - 1]);
		}

		private static void SetKeyboardFocus(ListBox listBox, int row)
		{
			if (row >= 0 && row < listBox.ItemCount && MainWindow.Instance.IsActive
				&& listBox.ContainerFromIndex(row) is ListBoxItem element)
			{
				element.Focus();
			}
		}

		public void UpdateResizableColumnWidth(int resizableColumnIndex)
		{
			// no-op：见类注释 TODO
		}
	}
}
