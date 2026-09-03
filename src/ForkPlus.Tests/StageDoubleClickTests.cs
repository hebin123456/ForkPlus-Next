// 回归测试（2026-09-03，"暂存区文件双击还是不能穿梭"修复产物）：
// 根因：MultiselectionTreeView.OnPointerPressed/OnDoubleTapped 里
// GetObjectAtPoint<TreeViewViewItem>(position) as TreeViewViewItem —— GetObjectAtPoint
// 返回 item（Flattener 直出的节点），不是 TreeViewViewItem 容器，cast 恒为 null；
// OnDoubleTapped 的该赋值把容器 OnPointerPressed 已设好的节点清空，
// DoubleTapped 订阅者读到的 LastClickedItem 恒 null → ItemDoubleClick 从不触发。
// WPF 原版 cast 到 MultiselectionTreeViewItem。修复后 headless 真实鼠标双击验证全链路。
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class StageDoubleClickTests
	{
		[Fact]
		public void DoubleClickOnFile_RaisesItemDoubleClickWithClickedFile()
		{
			string diag = HeadlessAppBootstrap.Run(delegate
			{
				var list = new FileListUserControl();
				var window = new Window { Width = 400, Height = 300, Content = list };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				var file = new ChangedFile("a.txt", StatusType.Modified, StatusType.None);
				list.SetItemSource(new[] { file }, forceRefresh: true, restoreSelection: false);
				Dispatcher.UIThread.RunJobs();

				MultiselectionTreeView tree = list.GetVisualDescendants().OfType<MultiselectionTreeView>().First();
				TreeViewControlItem container = tree.GetVisualDescendants().OfType<TreeViewControlItem>().FirstOrDefault();
				Assert.NotNull(container);

				Point center = container.TranslatePoint(
					new Point(container.Bounds.Width / 2, container.Bounds.Height / 2), window) ?? new Point(20, 20);

				bool doubleClickFired = false;
				string firedPath = null;
				list.ItemDoubleClick += delegate (object s, FileListEventArgs a)
				{
					doubleClickFired = true;
					firedPath = a.SelectedFile?.Path;
				};

				// 真实输入管线模拟双击（Down/Up × 2）。
				for (int i = 0; i < 2; i++)
				{
					HeadlessWindowExtensions.MouseDown(window, center, Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.None);
					Dispatcher.UIThread.RunJobs();
					HeadlessWindowExtensions.MouseUp(window, center, Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.None);
					Dispatcher.UIThread.RunJobs();
				}

				// 事件处理完成后 LastClickedItem 被延迟清空（WPF 原版同样在双击处理后清空）。
				string lastClickedAfter = tree.LastClickedItem?.GetType().Name ?? "<null>";

				window.Close();
				return "doubleClickFired=" + doubleClickFired + " firedPath=" + firedPath
					+ " lastClickedAfter=" + lastClickedAfter;
			});
			System.IO.File.WriteAllText("/tmp/stage_double_click.txt", diag);
			Assert.True(diag.Contains("doubleClickFired=True"), "双击必须触发 ItemDoubleClick：" + diag);
			Assert.True(diag.Contains("firedPath=a.txt"), "ItemDoubleClick 必须携带被双击的文件：" + diag);
		}
	}
}
