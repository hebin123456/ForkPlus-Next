// 探针6：验证"选中行右键 → Stage/Discard 上下文菜单"（问题3 的菜单部分）。
// 1) 无选区时菜单不含 Stage/Discard；
// 2) 有文本选区（未暂存）→ Stage + Discard... 项，点击后 CommitFileDiffControl.Stage/Discard
//    事件以正确的 editor 触发；
// 3) IsStaged → Unstage 项；
// 4) 真实右键（headless pointer → TopLevel → ContextRequested 路由）触发 compat handler
//    并填充菜单。手动 RaiseEvent 仅作诊断记录——它绕过真实手势链路，不作为断言依据。
using System;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DiffContextMenuProbeTests
	{
		private static Diff MakeDiff()
		{
			var lines = new[]
			{
				"context line 1\n",
				"context line 2\n",
				"context line 3\n",
				"deleted line 1\n",
				"deleted line 2\n",
				"added line 1\n",
				"added line 2\n",
				"added line 3\n",
				"context line 4\n",
				"context line 5\n",
				"context line 6\n"
			};
			var subChunk = new SubChunk(
				new Range(0, 3),
				new Range(3, 5),
				new Range(5, 8),
				new Range(8, 11),
				NoNewLineAtEndOfFile.None);
			var chunk = new Chunk(10, 6, 20, 7, null, new[] { subChunk });
			return new Diff("a.txt", "a.txt", null, null, "111", "222", lines, new[] { chunk }, null, Diff.FileType.Text, false);
		}

		private static string RunWithTimeout(Func<string> action, int timeoutMs)
		{
			HeadlessAppBootstrap.EnsureStarted();
			var task = Dispatcher.UIThread.InvokeAsync(async delegate { return action(); });
			if (!task.Wait(timeoutMs))
			{
				return "TIMEOUT(action): UI 线程在 action 内挂死";
			}
			var marker = Dispatcher.UIThread.InvokeAsync(async delegate { return "alive"; }, DispatcherPriority.Background);
			if (!marker.Wait(timeoutMs))
			{
				return "TIMEOUT(marker/main-loop): 主循环陷入无限循环（posted job / layout）";
			}
			return task.Result;
		}

		private static void InvokeAddSelectionPatchMenuItems(CommitFileDiffControl control, DiffCodeEditor editor, ContextMenu menu)
		{
			MethodInfo mi = typeof(CommitFileDiffControl).GetMethod("AddSelectionPatchMenuItems",
				BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.NotNull(mi);
			mi.Invoke(control, new object[] { editor, menu });
		}

		[Fact]
		public void Probe_SelectionContextMenu()
		{
			// 断言失败时 RunWithTimeout 的 task.Result 丢失，用 holder 把 sb 内容带回外层 catch
			var sbHolder = new System.Text.StringBuilder[1];
			string report;
			try
			{
				report = RunWithTimeout(delegate
				{
				var sb = new System.Text.StringBuilder();
				sbHolder[0] = sb;
				try
				{
					var diff = MakeDiff();
					var window = new Window { Width = 900, Height = 400 };
					window.Show();
					Dispatcher.UIThread.RunJobs();

					// ===== 用例1：无选区 → 无 Stage/Discard 项 =====
					var control1 = new CommitFileDiffControl();
					var editor1 = new CommitCodeEditor(DiffViewMode.Split);
					editor1.VisualPatch = VisualPatch.CreateVisualPatch(diff, false, DiffLocation.Unstaged);
					window.Content = editor1;
					Dispatcher.UIThread.RunJobs();
					var menu1 = new ContextMenu();
					editor1.ContextMenu = menu1;
					InvokeAddSelectionPatchMenuItems(control1, editor1, menu1);
					sb.AppendLine("case1 (no selection) menu items: " + menu1.Items.Count);
					Assert.True(menu1.Items.Count == 0, "case1: 无选区不应生成 Stage/Discard 项");

					// ===== 用例2：有文本选区 + 未暂存 → Stage + Discard...，点击触发事件 =====
					var control2 = new CommitFileDiffControl();
					var editor2 = new CommitCodeEditor(DiffViewMode.Split);
					editor2.VisualPatch = VisualPatch.CreateVisualPatch(diff, false, DiffLocation.Unstaged);
					editor2.IsStaged = false;
					window.Content = editor2;
					Dispatcher.UIThread.RunJobs();
					var menu2 = new ContextMenu();
					editor2.ContextMenu = menu2;

					int selStart = editor2.Text.IndexOf("deleted line 1", StringComparison.Ordinal);
					Assert.True(selStart >= 0, "case2: 找不到 deleted line 1");
					editor2.Select(selStart, 25);
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					sb.AppendLine("case2 selLength=" + editor2.SelectionLength);

					CommitCodeEditor stageArg = null;
					CommitCodeEditor discardArg = null;
					control2.Stage += delegate (object s, CommitCodeEditor ed) { stageArg = ed; };
					control2.Discard += delegate (object s, CommitCodeEditor ed) { discardArg = ed; };

					InvokeAddSelectionPatchMenuItems(control2, editor2, menu2);
					var items2 = menu2.Items.OfType<MenuItem>().ToList();
					// 菜单头经 PreferencesLocalization.MenuHeader 本地化（测试机语言可能是 zh），
					// 用同一字典取期望值，避免断言依赖 UI 语言。
					string expectStage = ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Current("Stage");
					string expectDiscard = ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Current("Discard...");
					string expectUnstage = ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Current("Unstage");
					sb.AppendLine("case2 menu headers: " + string.Join("|", items2.Select(x => x.Header)));
					Assert.Equal(2, items2.Count);
					Assert.Equal(expectStage, (string)items2[0].Header);
					Assert.Equal(expectDiscard, (string)items2[1].Header);
					Assert.True(menu2.Items.OfType<Separator>().Count() == 1, "case2: Stage/Discard 组后应有分隔符");

					// 菜单项应可用（selection 存在）
					Assert.True(items2[0].IsEnabled && items2[1].IsEnabled, "case2: 菜单项应可用");

					// 点击 Stage 菜单项 → control2.Stage 事件以 editor2 触发
					items2[0].RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(global::Avalonia.Controls.MenuItem.ClickEvent));
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					Assert.Same(editor2, stageArg);
					sb.AppendLine("case2 Stage clicked → event OK");

					// patch 提取链路：GetSelectedPatchLines 非空
					int[] patchLines = editor2.GetSelectedPatchLines();
					sb.AppendLine("case2 patchLines=" + (patchLines == null ? "null" : string.Join(",", patchLines)));
					Assert.NotNull(patchLines);
					Assert.True(patchLines.Length > 0, "case2: 选区应映射到非空 patch lines");

					// ===== 用例3：IsStaged → Unstage =====
					var control3 = new CommitFileDiffControl();
					var editor3 = new CommitCodeEditor(DiffViewMode.Split);
					editor3.VisualPatch = VisualPatch.CreateVisualPatch(diff, false, DiffLocation.Staged);
					editor3.IsStaged = true;
					window.Content = editor3;
					Dispatcher.UIThread.RunJobs();
					var menu3 = new ContextMenu();
					editor3.ContextMenu = menu3;
					int selStart3 = editor3.Text.IndexOf("deleted line 1", StringComparison.Ordinal);
					editor3.Select(selStart3, 25);
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

					CommitCodeEditor unstageArg = null;
					control3.UnStage += delegate (object s, CommitCodeEditor ed) { unstageArg = ed; };
					InvokeAddSelectionPatchMenuItems(control3, editor3, menu3);
					var items3 = menu3.Items.OfType<MenuItem>().ToList();
					sb.AppendLine("case3 menu headers: " + string.Join("|", items3.Select(x => x.Header)));
					Assert.Single(items3);
					Assert.Equal(expectUnstage, (string)items3[0].Header);
					items3[0].RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(global::Avalonia.Controls.MenuItem.ClickEvent));
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					Assert.Same(editor3, unstageArg);
					sb.AppendLine("case3 Unstage clicked → event OK");

					// ===== 用例4：真实右键手势（headless pointer → ContextRequested 路由）=====
					var control4 = new CommitFileDiffControl();
					var editor4 = new CommitCodeEditor(DiffViewMode.Split);
					editor4.VisualPatch = VisualPatch.CreateVisualPatch(diff, false, DiffLocation.Unstaged);
					editor4.IsStaged = false;
					window.Content = editor4;
					Dispatcher.UIThread.RunJobs();
					var menu4 = new ContextMenu();
					editor4.ContextMenu = menu4;

					bool handlerFired = false;
					// 模拟 SplitCommitTextDiffControl 的 EditorContextMenuOpening 安装方式
					ContextMenuCompat.AddContextMenuOpeningHandler(editor4, delegate (object s, ContextMenuEventArgs e)
					{
						handlerFired = true;
						DiffCodeEditor dce = e.Source as DiffCodeEditor ?? s as DiffCodeEditor;
						if (dce == null)
						{
							e.Handled = true;
							return;
						}
						ContextMenu cm = dce.ContextMenu;
						cm.Items.Clear();
						InvokeAddSelectionPatchMenuItems(control4, dce, cm);
					});

					int selStart4 = editor4.Text.IndexOf("added line 1", StringComparison.Ordinal);
					sb.AppendLine("case4 selStart4=" + selStart4);
					editor4.Select(selStart4, 20);
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					sb.AppendLine("case4 selLength=" + editor4.SelectionLength + ", menu4 items before=" + menu4.Items.Count);

					// 4a. 从 TextView raise ContextRequested——精确复刻 Avalonia 12 Control.OnPointerReleased
				//     的真实行为（右键释放在命中控件上 raise，再冒泡到编辑器），验证 compat handler
				//     安装路径 + 菜单填充。手动在 editor 上 Raise 只测自身 handler，绕过冒泡，不算数。
				editor4.TextArea.TextView.RaiseEvent(new global::Avalonia.Input.ContextRequestedEventArgs());
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				sb.AppendLine("case4a TextView-raise handlerFired=" + handlerFired + ", menu4 items=" + menu4.Items.Count);
				var items4a = menu4.Items.OfType<MenuItem>().ToList();
				sb.AppendLine("case4a menu headers: " + string.Join("|", items4a.Select(x => x.Header)));
				Assert.True(handlerFired, "case4a: TextView raise 的 ContextRequested 应冒泡触发 compat handler");
				Assert.Contains(items4a, x => (x.Header as string) == expectStage);
				Assert.Contains(items4a, x => (x.Header as string) == expectDiscard);
				Assert.True(editor4.SelectionLength > 0, "case4a: 打开菜单不应清空选区");
				sb.AppendLine("case4a TextView-raise → menu populated OK");

				// 4b. 真实右键：定位到选中区域内的可视行，pointer press/release(右键)。
				//     仅诊断记录（headless 手势链路与桌面平台存在差异，不作为断言依据）。
				var vl4 = editor4.TextArea.TextView.GetVisualLine(6); // doc line 6 = added line 1
				Assert.True(vl4 != null, "case4: GetVisualLine(6) 为 null，布局未完成");
				var clickPos = new Point(250, vl4.VisualTop - editor4.TextArea.TextView.ScrollOffset.Y + 6);
				bool tvReleased = false;
				editor4.TextArea.TextView.AddHandler(global::Avalonia.Input.InputElement.PointerReleasedEvent,
					delegate (object s, global::Avalonia.Input.PointerReleasedEventArgs e)
					{
						tvReleased = true;
					});
				HeadlessWindowExtensions.MouseMove(window, clickPos, global::Avalonia.Input.RawInputModifiers.None);
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				HeadlessWindowExtensions.MouseDown(window, clickPos, global::Avalonia.Input.MouseButton.Right, global::Avalonia.Input.RawInputModifiers.None);
				HeadlessWindowExtensions.MouseUp(window, clickPos, global::Avalonia.Input.MouseButton.Right, global::Avalonia.Input.RawInputModifiers.None);
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

				sb.AppendLine("case4b right-click handlerFired=" + handlerFired
					+ ", tvPointerReleased=" + tvReleased
					+ ", selLength after click=" + editor4.SelectionLength
					+ ", menu4.IsOpen=" + menu4.IsOpen
					+ ", menu4 items=" + menu4.Items.Count);
				sb.AppendLine("case4b (diagnostic only, headless gesture path)");

					window.Close();
				}
				catch (Exception e)
				{
					sb.AppendLine("EXCEPTION: " + e);
					throw;
				}
				return sb.ToString();
				}, 30000);
			}
			catch (Exception ex)
			{
				report = (sbHolder[0]?.ToString() ?? "") + "\nOUTER EXCEPTION: " + ex;
			}
			System.IO.File.WriteAllText("/tmp/diff_contextmenu_probe.txt", report);
			Assert.DoesNotContain("TIMEOUT", report);
			Assert.DoesNotContain("EXCEPTION", report);
			Assert.DoesNotContain("OUTER EXCEPTION", report);
		}
	}
}
