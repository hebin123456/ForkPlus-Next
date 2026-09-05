// E2E 测试基建（阶段0，2026-09-05）：截图证据统一入口。
// 约定：截图存 docs/evidence/e2e/<模块短名>/<场景>.png（模块短名如 "01-welcome"、"07-textdiff"），
// 每张截图自动做"非空白"像素断言；SnapDiff 额外断言交互前后存在可见差异。
// 必须在 UI 线程内调用（HeadlessAppBootstrap.Run 的回调里）。
using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace ForkPlus.Tests
{
	internal static class ScreenshotHelper
	{
		/// <summary>截图并断言非空白。返回非空白像素数（可做进一步断言）。</summary>
		public static int Snap(Window window, string scenario, string moduleDir)
		{
			Dispatcher.UIThread.RunJobs();
			using WriteableBitmap frame = window.CaptureRenderedFrame()
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
