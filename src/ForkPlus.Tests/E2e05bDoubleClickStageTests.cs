// E2E 模块5b（2026-09-07）：双击穿梭 Stage/Unstage（问题6"Windows 双击 stage 失效（fsmonitor类）"回归）。
// 覆盖：
//   1) 真实指针序列双击：同一 Pointer 的两次 PointerPressed（ClickCount=1 → ClickCount=2）+
//      PointerReleased，走 Avalonia Gestures 手势识别（RouteFinished 匹配 source、ClickCount 奇偶、
//      指针捕获一致性）→ DoubleTapped 在容器上产生 → 冒泡到 MultiselectionTreeView.OnDoubleTapped
//      → FileListUserControl.ItemDoubleClick → StageFileUserControl.Stage → ToggleFileStage → git add。
//      这是对 v3.12 修复（LastClickedItem 延迟清空）的端到端验证：此前只验证了直接 RaiseEvent
//      DoubleTappedEvent 的合成路径，未覆盖真实手势识别链。
//   2) git index 双重验证：UI 列表移动 + git status 独立核对（不经 App 解析管线）。
//   3) 双击穿梭回路：未暂存双击 → 已暂存；已暂存列表双击 → 回到未暂存（Unstage 路径）。
//   4) fsmonitor 仓库变体：core.fsmonitor=true 下双击 stage（"fsmonitor类"命令族对齐回归：
//      StageFileGitCommand 的裸 git add 写 index 后，RefreshFileStatusCommand 的
//      -c core.fsmonitor=false 四件套 status 读回必须一致，否则文件"弹回"未暂存列表）。
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using Xunit;
using Xunit.Abstractions;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e05bDoubleClickStageTests
	{
		private readonly ITestOutputHelper _output;

		public E2e05bDoubleClickStageTests(ITestOutputHelper output)
		{
			_output = output;
		}

		[Fact]
		public void DoubleClick_UnstagedFile_StagesIt_RealGestureSequence()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						repoControl.ActivateCommitView();
						Dispatcher.UIThread.RunJobs();
						StageFileUserControl stage = repoControl.Content.CommitUserControl.StageFileUserControl;
						bool statusLoaded = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 3 && stage.AllStagedFiles.Length == 1;
						});
						Assert.True(statusLoaded, "工作区状态未装配：unstaged=" + stage.AllUnstagedFiles.Length
							+ " staged=" + stage.AllStagedFiles.Length);

						// ===== 双击 a.txt（真实手势序列：ClickCount 1→2 同一 Pointer）=====
						Control row = FindFileRow(stage.UnstagedFilesFileListUserControl, "a.txt", window);
						Assert.True(row != null, "未找到 a.txt 的行容器（布局未完成或容器未物化）");
						DoubleClickRow(row, window);

						bool staged = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 2 && stage.AllStagedFiles.Length == 2;
						});
						Assert.True(staged, "双击后 a.txt 应移入已暂存列表，实际 unstaged="
							+ stage.AllUnstagedFiles.Length + " staged=" + stage.AllStagedFiles.Length);
						Assert.Contains("a.txt", stage.AllStagedFiles.Select(f => f.Path));

						// git index 独立核对（真实 git add 已落盘）
						string status = RunGit(repo, "status --porcelain");
						_output.WriteLine("git status after double-click stage:\n" + status);
						Assert.Contains("M  a.txt", status); // 已暂存位非空

						// ===== 双击穿梭回路：已暂存列表双击 a.txt → 回到未暂存 =====
						Control stagedRow = FindFileRow(stage.StagedFilesFileListUserControl, "a.txt", window);
						Assert.True(stagedRow != null, "未找到已暂存 a.txt 的行容器");
						DoubleClickRow(stagedRow, window);

						bool unstaged = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 3 && stage.AllStagedFiles.Length == 1;
						});
						Assert.True(unstaged, "双击已暂存 a.txt 应移回未暂存，实际 unstaged="
							+ stage.AllUnstagedFiles.Length + " staged=" + stage.AllStagedFiles.Length);
						string status2 = RunGit(repo, "status --porcelain");
						_output.WriteLine("git status after double-click unstage:\n" + status2);
						Assert.Contains(" M a.txt", status2); // 仅工作区位有修改

						ScreenshotHelper.Snap(window, "01-doubleclick-stage-roundtrip", "05-changescommit");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void DoubleClick_StageWithFsmonitorEnabled_StillStages()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			try
			{
				// 启用 fsmonitor（"fsmonitor类"问题族：Git for Windows 大仓常见配置；
				// git 2.50 内置 daemon，Linux 同样可启用，用于验证四件套读回一致性）
				RunGit(repo, "config core.fsmonitor true");
				RunGit(repo, "status --porcelain"); // 预热 daemon
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						repoControl.ActivateCommitView();
						Dispatcher.UIThread.RunJobs();
						StageFileUserControl stage = repoControl.Content.CommitUserControl.StageFileUserControl;
						bool statusLoaded = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 3 && stage.AllStagedFiles.Length == 1;
						});
						Assert.True(statusLoaded, "fsmonitor 仓库状态未装配：unstaged=" + stage.AllUnstagedFiles.Length
							+ " staged=" + stage.AllStagedFiles.Length);

						Control row = FindFileRow(stage.UnstagedFilesFileListUserControl, "a.txt", window);
						Assert.True(row != null, "未找到 a.txt 的行容器");
						DoubleClickRow(row, window);

						// 关键断言：git add（裸命令，fsmonitor 开启时写 index 含 FSMONITOR 扩展）后，
						// App 的 -c core.fsmonitor=false status 读回必须一致——不允许文件"弹回"未暂存列表
						bool staged = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 2 && stage.AllStagedFiles.Length == 2;
						});
						Assert.True(staged, "fsmonitor 仓库双击 stage 后列表应即时一致，实际 unstaged="
							+ stage.AllUnstagedFiles.Length + " staged=" + stage.AllStagedFiles.Length);
						Assert.Contains("a.txt", stage.AllStagedFiles.Select(f => f.Path));

						string status = RunGit(repo, "status --porcelain");
						_output.WriteLine("fsmonitor repo git status after stage:\n" + status);
						Assert.Contains("M  a.txt", status);

						ScreenshotHelper.Snap(window, "02-doubleclick-stage-fsmonitor", "05-changescommit");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		/// <summary>定位文件行容器：从 RootItem 树找到目标 FileListItem 节点，再取其可视化容器。</summary>
		private static Control FindFileRow(FileListUserControl list, string filePath, Window window)
		{
			MultiselectionTreeViewItem node = FindNode(list.TreeView.RootItem, filePath);
			if (node == null)
			{
				return null;
			}
			Control container = list.TreeView.ContainerFromItem(node) as Control;
			return container;
		}

		private static MultiselectionTreeViewItem FindNode(MultiselectionTreeViewItem parent, string filePath)
		{
			foreach (MultiselectionTreeViewItem child in parent.Children)
			{
				if (child is FileListItem fileListItem && !fileListItem.IsDirectory
					&& fileListItem.ChangedFile.Path == filePath)
				{
					return child;
				}
				MultiselectionTreeViewItem found = FindNode(child, filePath);
				if (found != null)
				{
					return found;
				}
			}
			return null;
		}

		/// <summary>
		/// 真实双击手势序列（Avalonia Gestures 识别路径）：
		/// 同一 Pointer 两次按下（ClickCount=1/2）+ 两次释放，全部 RaiseEvent 走完整路由。
		/// RouteFinished 时手势识别器读取 e.Pointer?.Captured ?? e.Source 匹配两次按下源一致
		/// 且 ClickCount 为偶数 → 在容器上产生 DoubleTapped 并冒泡。与 UiClick.DoubleTap
		/// （直接合成 DoubleTappedEvent，绕过手势识别）不同，本序列覆盖 Windows 真实鼠标路径。
		/// </summary>
		private static void DoubleClickRow(Control row, Window window)
		{
			// 行中心（窗口坐标，供 GetPosition 换算与 OnDoubleTapped 内 HitTest 命中该行）
			Point? rowTopLeft = row.TranslatePoint(new Point(0, 0), window);
			Assert.True(rowTopLeft.HasValue, "行容器未完成布局（TranslatePoint 无效）");
			Point center = new Point(rowTopLeft.Value.X + Math.Max(4, row.Bounds.Width / 2),
				rowTopLeft.Value.Y + Math.Max(3, row.Bounds.Height / 2));

			IPointer pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
			PointerPointProperties pressProps = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
			PointerPointProperties releaseProps = new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased);
			ulong t = (ulong)Environment.TickCount64;

			// 第一次按下（ClickCount=1）
			row.RaiseEvent(new PointerPressedEventArgs(row, pointer, window, center, t, pressProps, KeyModifiers.None, 1));
			Dispatcher.UIThread.RunJobs();
			row.RaiseEvent(new PointerReleasedEventArgs(row, pointer, window, center, t + 30, releaseProps, KeyModifiers.None, MouseButton.Left));
			Dispatcher.UIThread.RunJobs();

			// 第二次按下（ClickCount=2，同一 Pointer —— 手势识别的源匹配前提）
			row.RaiseEvent(new PointerPressedEventArgs(row, pointer, window, center, t + 80, pressProps, KeyModifiers.None, 2));
			Dispatcher.UIThread.RunJobs();
			row.RaiseEvent(new PointerReleasedEventArgs(row, pointer, window, center, t + 110, releaseProps, KeyModifiers.None, MouseButton.Left));
			Dispatcher.UIThread.RunJobs();
		}

		private static string RunGit(string cwd, string args)
		{
			ProcessStartInfo psi = new ProcessStartInfo("git", args)
			{
				WorkingDirectory = cwd,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false
			};
			using (Process p = Process.Start(psi))
			{
				string stdout = p.StandardOutput.ReadToEnd();
				p.StandardError.ReadToEnd();
				p.WaitForExit(15000);
				return stdout;
			}
		}
	}
}
