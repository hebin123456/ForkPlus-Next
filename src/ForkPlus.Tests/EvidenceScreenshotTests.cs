// 证据截图（2026-09-04，"修复9截图上库，修复8修复后也要证据上库"）：
// 用无头 Skia 渲染管线把修复后的真实 UI 截帧存 PNG 到仓库 docs/evidence/，
// 随代码一起提交，供人工核验"修复确实可见、非自欺"。
//   fix9-before-hover.png / fix9-after-hover.png —— diff 悬浮 Stage/Discard 浮窗（修复9）
//   fix8-hexdiff.png —— 二进制文件 Hex Diff 视图（修复8）
// 截图同时做像素断言（非全空白、hover 前后有可见差异），测试本身就是回归证明。
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class EvidenceScreenshotTests
	{
		// 仓库根（…/ForkPlus-Next）：从测试输出目录向上找含 src/ForkPlus.Tests 的目录
		private static string FindRepoRoot()
		{
			string dir = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Parent?.Parent?.Parent?.FullName;
			while (dir != null && !Directory.Exists(Path.Combine(dir, "src", "ForkPlus.Tests")))
			{
				dir = Directory.GetParent(dir)?.FullName;
			}
			return dir ?? throw new InvalidOperationException("找不到仓库根（src/ForkPlus.Tests 不存在）");
		}

		private static string EvidenceDir()
		{
			string dir = Path.Combine(FindRepoRoot(), "docs", "evidence");
			Directory.CreateDirectory(dir);
			return dir;
		}

		private static void SaveFrame(Avalonia.Media.Imaging.WriteableBitmap frame, string fileName)
		{
			string path = Path.Combine(EvidenceDir(), fileName);
			frame.Save(path);
		}

		private static int CountNonBlankPixels(Avalonia.Media.Imaging.WriteableBitmap frame)
		{
			int count = 0;
			using (var l = frame.Lock())
			{
				for (int row = 0; row < frame.PixelSize.Height; row++)
				{
					IntPtr rowPtr = l.Address + row * l.RowBytes;
					for (int x = 0; x < frame.PixelSize.Width; x++)
					{
						byte b = System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, x * 4);
						byte g = System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, x * 4 + 1);
						byte r = System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, x * 4 + 2);
						if (r < 230 || g < 230 || b < 230)
						{
							count++;
						}
					}
				}
			}
			return count;
		}

		// ============================ 修复9：悬浮 Stage/Discard 浮窗 ============================

		private static Diff MakeFix9Diff()
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
				new Range(0, 3), new Range(3, 5), new Range(5, 8), new Range(8, 11),
				NoNewLineAtEndOfFile.None);
			var chunk = new Chunk(10, 6, 20, 7, null, new[] { subChunk });
			return new Diff("a.txt", "a.txt", null, null, "111", "222", lines, new[] { chunk }, null, Diff.FileType.Text, false);
		}

		[Fact]
		public void Fix9_FloatingButtons_ScreenshotEvidence()
		{
			HeadlessAppBootstrap.EnsureStarted();
			int[] changedInAdornerRegion = new int[1];
			string[] regionDesc = new string[1];
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var diff = MakeFix9Diff();
				// 真实窗口模板：CustomWindow 默认主题含 LayoutTransformControl + VisualLayerManager
				var window = new ForkPlus.UI.CustomWindow { Width = 900, Height = 400 };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				var editor = new CommitCodeEditor(DiffViewMode.Split);
				editor.VisualPatch = VisualPatch.CreateVisualPatch(diff, false, DiffLocation.Unstaged);
				editor.IsStaged = false;
				window.Content = editor;
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

				var vl6 = editor.TextArea.TextView.GetVisualLine(6);
				Assert.NotNull(vl6);
				double yHunk = vl6.VisualTop - editor.TextArea.TextView.ScrollOffset.Y + 6;

				// hover 前：基线截图（保留位图供区域差异比较）
				var before = HeadlessWindowExtensions.CaptureRenderedFrame(window);

				// hover 到 hunk → 浮窗出现
				HeadlessWindowExtensions.MouseMove(window, new Point(250, yHunk), global::Avalonia.Input.RawInputModifiers.None);
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				HeadlessWindowExtensions.CaptureRenderedFrame(window);
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				var after = HeadlessWindowExtensions.CaptureRenderedFrame(window);

				// 找到浮窗 Adorner，取其 bounds 区域做前后差异统计（hover 高亮会改变行背景，
				// 全局非空白像素数不是可靠指标；浮窗区域差异才是"浮窗画出来了"的直接证据）
				FieldInfo lf = typeof(CommitCodeEditor).GetField("_diffSelectionLayer",
					BindingFlags.NonPublic | BindingFlags.Instance);
				Assert.NotNull(lf);
				var layer = lf.GetValue(editor);
				FieldInfo af = typeof(ChunkSelectionLayer<CommitDiffSelectedRange>).GetField("_adorner",
					BindingFlags.NonPublic | BindingFlags.Instance);
				Assert.NotNull(af);
				var adorner = af.GetValue(layer) as Control;
				Assert.NotNull(adorner);
				var b = adorner.Bounds;
				int x0 = Math.Max(0, (int)b.X);
				int y0 = Math.Max(0, (int)b.Y);
				int x1 = Math.Min(900, (int)(b.X + b.Width));
				int y1 = Math.Min(400, (int)(b.Y + b.Height));
				regionDesc[0] = x0 + "," + y0 + " - " + x1 + "," + y1;

				int changed = 0;
				using (var lb = before.Lock())
				using (var la = after.Lock())
				{
					for (int row = y0; row < y1; row++)
					{
						IntPtr rowBefore = lb.Address + row * lb.RowBytes;
						IntPtr rowAfter = la.Address + row * la.RowBytes;
						for (int x = x0; x < x1; x++)
						{
							byte bb = System.Runtime.InteropServices.Marshal.ReadByte(rowBefore, x * 4);
							byte bg = System.Runtime.InteropServices.Marshal.ReadByte(rowBefore, x * 4 + 1);
							byte br = System.Runtime.InteropServices.Marshal.ReadByte(rowBefore, x * 4 + 2);
							byte ab = System.Runtime.InteropServices.Marshal.ReadByte(rowAfter, x * 4);
							byte ag = System.Runtime.InteropServices.Marshal.ReadByte(rowAfter, x * 4 + 1);
							byte ar = System.Runtime.InteropServices.Marshal.ReadByte(rowAfter, x * 4 + 2);
							if (Math.Abs(br - ar) > 30 || Math.Abs(bg - ag) > 30 || Math.Abs(bb - ab) > 30)
							{
								changed++;
							}
						}
					}
				}
				changedInAdornerRegion[0] = changed;

				SaveFrame(before, "fix9-before-hover.png");
				SaveFrame(after, "fix9-after-hover.png");
				before.Dispose();
				after.Dispose();
				window.Close();
				return 0;
			}).GetAwaiter().GetResult();

			string beforePath = Path.Combine(EvidenceDir(), "fix9-before-hover.png");
			string afterPath = Path.Combine(EvidenceDir(), "fix9-after-hover.png");
			Assert.True(File.Exists(beforePath) && new FileInfo(beforePath).Length > 1000, "fix9-before-hover.png 应生成");
			Assert.True(File.Exists(afterPath) && new FileInfo(afterPath).Length > 1000, "fix9-after-hover.png 应生成");
			// 浮窗 bounds 区域 hover 前后应有大量像素变化（浮窗真的画出来了）
			Assert.True(changedInAdornerRegion[0] > 100,
				"浮窗区域应渲染出可见变化（changedPixels=" + changedInAdornerRegion[0]
				+ ", region=" + regionDesc[0] + "）");
		}

		// ============================ 修复8：二进制 Hex Diff 视图 ============================

		private static string CreateFix8BinaryRepo()
		{
			string root = Path.Combine(Path.GetTempPath(), "fpevidence_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(root);
			string oldDir = Directory.GetCurrentDirectory();
			try
			{
				Directory.SetCurrentDirectory(root);
				RunGit("init -q");
				RunGit("config user.email test@example.com");
				RunGit("config user.name Test");
				// 初始版本：前 512 字节递增序列（可读 hex）
				byte[] v1 = new byte[512];
				for (int i = 0; i < v1.Length; i++) v1[i] = (byte)(i % 251);
				File.WriteAllBytes(Path.Combine(root, "logo.bin"), v1);
				RunGit("add logo.bin");
				RunGit("commit -q -m base");
				// 修改版：一部分字节变化 + 追加数据（有 diff 高亮也有长度差异）
				byte[] v2 = new byte[640];
				for (int i = 0; i < v2.Length; i++) v2[i] = (byte)((i * 7 + 13) % 253);
				File.WriteAllBytes(Path.Combine(root, "logo.bin"), v2);
			}
			finally
			{
				Directory.SetCurrentDirectory(oldDir);
			}
			return root;
		}

		private static void RunGit(string args)
		{
			var psi = new System.Diagnostics.ProcessStartInfo("git", args)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false
			};
			using var p = System.Diagnostics.Process.Start(psi);
			string err = p.StandardError.ReadToEnd();
			p.WaitForExit();
			if (p.ExitCode != 0)
			{
				throw new Exception("git " + args + " 失败: " + err);
			}
		}

		[Fact]
		public void Fix8_HexDiff_ScreenshotEvidence()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repoRoot = CreateFix8BinaryRepo();
			try
			{
				var module = new GitModule(repoRoot, Path.Combine(repoRoot, ".git"), null, null);
				GitCommandResult<ChangedFilesCollection> statusResult = new GetChangedFilesGitCommand().Execute(module);
				Assert.True(statusResult.Succeeded, "git status 失败: " + statusResult.Error);
				ChangedFile binFile = statusResult.Result.ChangedFiles.FirstOrDefault(f => f.Path.EndsWith(".bin"));
				Assert.NotNull(binFile);

				GitCommandResult<DiffContent> diffResult = new GetWorkingDirectoryFileChangesGitCommand().Execute(
					module, binFile, null, 3, 4, false, false, false, resolvedConflict: false);
				Assert.True(diffResult.Succeeded, "diff 加载失败: " + diffResult.Error);

				int[] pixels = new int[1];
				object[] subView = new object[1];
				Dispatcher.UIThread.InvokeAsync(delegate
				{
					var repoControl = new RepositoryUserControl();
					typeof(RepositoryUserControl).GetProperty("GitModule")!
						.SetValue(repoControl, module);

					var control = new FileDiffControl();
					control.RepositoryUserControl = repoControl;
					var window = new ForkPlus.UI.CustomWindow { Width = 1000, Height = 600, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();

					control.Content = diffResult;
					// 二进制路径：JobQueue + Dispatcher.Post + SetContent 内部 Task.Run
					Task.Delay(600).GetAwaiter().GetResult();
					Dispatcher.UIThread.RunJobs();
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					Task.Delay(200).GetAwaiter().GetResult();
					Dispatcher.UIThread.RunJobs();

					subView[0] = control.CurrentSubView;
					using (var frame = HeadlessWindowExtensions.CaptureRenderedFrame(window))
					{
						SaveFrame(frame, "fix8-hexdiff.png");
						pixels[0] = CountNonBlankPixels(frame);
					}
					window.Close();
					return 0;
				}).GetAwaiter().GetResult();

				string shotPath = Path.Combine(EvidenceDir(), "fix8-hexdiff.png");
				Assert.True(File.Exists(shotPath) && new FileInfo(shotPath).Length > 1000, "fix8-hexdiff.png 应生成");
				Assert.True(subView[0] is ForkPlus.UI.Controls.Editor.Hex.HexDiffUserControl,
					"二进制 diff 应显示 HexDiff 视图，实际: " + (subView[0]?.GetType().Name ?? "<null>"));
				Assert.True(pixels[0] > 2000,
					"Hex Diff 渲染近乎全空白（非空白像素=" + pixels[0] + "）");
			}
			finally
			{
				try { Directory.Delete(repoRoot, true); } catch { }
			}
		}
	}
}
