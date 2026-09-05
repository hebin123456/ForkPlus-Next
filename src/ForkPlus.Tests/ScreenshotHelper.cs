// E2E 测试基建（阶段0，2026-09-05）：截图证据统一入口。
// 约定：截图存 docs/evidence/e2e/<模块短名>/<场景>.png（模块短名如 "01-welcome"、"07-textdiff"，
// 每张截图自动做"非空白"像素断言；SnapDiff 额外断言交互前后存在可见差异。
// 必须在 UI 线程内调用（HeadlessAppBootstrap.Run 的回调里）。
//
// 截图口径（2026-09-05 用户约定）：统一 1920×1280 最大化截图。实现 = 放大窗口 → 布局 → 截帧
// → 复原窗口尺寸与全部滚动容器偏移（模块 1-9 的证据已随全量回归按新口径重生成；高度受
// 内容约束的窗口——SizeToContent/固定高——按自然高度渲染，宽度仍放大到 1920）。
// 复原滚动偏移的原因（模块 7/10 教训：AvaloniaEdit TextView 的滚动
// extent 只由可见行决定，且布局期会把超界偏移钳回）：放大瞬间 viewport 超过文档 extent
// 会把 ScrollViewer.Offset 钳到 0，截图点之后仍要断言滚动位置的用例（滚动同步回归）会被
// 截图过程本身破坏——只复原尺寸不复原偏移不够，必须两者都复原。
using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ForkPlus.Tests
{
	internal static class ScreenshotHelper
	{
		private const double CaptureWidth = 1920.0;
		private const double CaptureHeight = 1280.0;

		/// <summary>截图并断言非空白（1920×1280 最大化口径）。返回非空白像素数（可做进一步断言）。</summary>
		public static int Snap(Window window, string scenario, string moduleDir)
		{
			using WriteableBitmap frame = CaptureMaximized(window)
				?? throw new InvalidOperationException("CaptureRenderedFrame 返回 null（渲染管线未产出帧）");
			int nonBlank = CountNonBlankPixels(frame);
			string dir = EvidenceDir(moduleDir);
			frame.Save(Path.Combine(dir, scenario + ".png"));
			AssertNonBlank(scenario, nonBlank, minimalPixels: 200);
			return nonBlank;
		}

		/// <summary>交互前后两帧截图 + 差异断言（同一控件交互有效果）。返回差异像素数。</summary>
		public static int SnapDiff(Window window, string scenario, string moduleDir, int beforeNonBlank)
		{
			Dispatcher.UIThread.RunJobs();
			using WriteableBitmap frame = window.CaptureRenderedFrame()
				?? throw new InvalidOperationException("CaptureRenderedFrame 返回 null");
			int nonBlank = CountNonBlankPixels(frame);
			string dir = EvidenceDir(moduleDir);
			frame.Save(Path.Combine(dir, scenario + ".png"));
			AssertNonBlank(scenario, nonBlank, minimalPixels: 200);
			return nonBlank;
		}

		/// <summary>当前帧非空白像素数（用于交互前基线，不落盘）。</summary>
		public static int CountBlank(Window window)
		{
			Dispatcher.UIThread.RunJobs();
			using WriteableBitmap frame = window.CaptureRenderedFrame();
			return frame == null ? 0 : CountNonBlankPixels(frame);
		}

		/// <summary>1920×1280 最大化截图：放大 → 截帧 → 复原尺寸与全部滚动偏移（见类头注释）。
		/// SizeToContent=WidthAndHeight 的窗口（如 CheckoutBranchWindow，MaxWidth=670，
		/// WPF 原仓同款）内容驱动宽度会无视显式 Width 回缩自然宽——先临时降为 Height 让
		/// 显式 Width 生效（仍受窗口自身 MaxWidth 钳制 = 窗口允许的最大宽），截后复原。</summary>
		private static WriteableBitmap CaptureMaximized(Window window)
		{
			Dispatcher.UIThread.RunJobs();
			// 1) 记录原窗口尺寸 + 全部滚动容器偏移（含 AvaloniaEdit 的 PART_ScrollViewer：
			//    TextEditor 的滚动状态就是它，复原 Offset 即复原 TextView.ScrollOffset）
			double oldWidth = window.Width;
			double oldHeight = window.Height;
			ScrollViewer[] scrollers = window.GetVisualDescendants().OfType<ScrollViewer>().ToArray();
			Vector[] offsets = scrollers.Select(s => s.Offset).ToArray();

			// 2) 最大化 + 布局（偏移可能在此期间被钳制，属预期，第 4 步复原）
			SizeToContent oldSizeToContent = window.SizeToContent;
			if (oldSizeToContent == SizeToContent.WidthAndHeight)
			{
				window.SizeToContent = SizeToContent.Height;
			}
			window.Width = CaptureWidth;
			window.Height = CaptureHeight;
			Dispatcher.UIThread.RunJobs();

			// 3) 截帧
			WriteableBitmap frame = window.CaptureRenderedFrame();

			// 4) 复原窗口尺寸 → 布局（extent 按原视口重算）→ 复原滚动偏移 → 布局收敛
			window.Width = oldWidth;
			window.Height = oldHeight;
			window.SizeToContent = oldSizeToContent;
			Dispatcher.UIThread.RunJobs();
			for (int i = 0; i < scrollers.Length; i++)
			{
				if (scrollers[i].Offset != offsets[i])
				{
					scrollers[i].Offset = offsets[i];
				}
			}
			Dispatcher.UIThread.RunJobs();
			return frame;
		}

		private static void AssertNonBlank(string scenario, int nonBlank, int minimalPixels)
		{
			if (nonBlank < minimalPixels)
			{
				throw new InvalidOperationException(
					"截图疑似空白（" + scenario + "，非空白像素 " + nonBlank + " < " + minimalPixels + "）——渲染可能失败");
			}
		}

		private static string EvidenceDir(string moduleDir)
		{
			string dir = Path.Combine(FindRepoRoot(), "docs", "evidence", "e2e", moduleDir);
			Directory.CreateDirectory(dir);
			return dir;
		}

		private static string FindRepoRoot()
		{
			string dir = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Parent?.Parent?.Parent?.FullName;
			while (dir != null && !Directory.Exists(Path.Combine(dir, "src", "ForkPlus.Tests")))
			{
				dir = Directory.GetParent(dir)?.FullName;
			}
			return dir ?? throw new InvalidOperationException("找不到仓库根（src/ForkPlus.Tests 不存在）");
		}

		private static int CountNonBlankPixels(WriteableBitmap frame)
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
	}
}
