// 探针（问题E，2026-09-04）：git mm 命令参考手册内容无法选中和滚动。
// 手册由 GitMmReferenceWindow → WebView2 stub（ScrollViewer 子类）→
// MarkdownHtmlRenderer 渲染成 TextBlock 树。本探针复刻真实结构验证：
//   1) 长内容下滚轮（文本区 + 空白区）是否滚动；
//   2) 渲染出的文本块是否为 SelectableTextBlock（Avalonia TextBlock 不可选中）。
using System;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Web.WebView2.Wpf;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class GitMmManualProbeTests
	{
		private static PointerWheelEventArgs MakeWheel(object source, Window window, Point pos, Vector delta)
		{
			var pointer = new Avalonia.Input.Pointer(
				Avalonia.Input.Pointer.GetNextFreeId(),
				Avalonia.Input.PointerType.Mouse,
				true);
			return new PointerWheelEventArgs(
				source,
				pointer,
				window,
				pos,
				(ulong)Environment.TickCount64,
				new PointerPointProperties(Avalonia.Input.RawInputModifiers.None, PointerUpdateKind.Other),
				KeyModifiers.None,
				delta);
		}

		private static bool WheelAt(Window window, Point pos, Vector delta, out string hitName, out bool handled)
		{
			var hit = window.InputHitTest(pos);
			hitName = hit?.GetType().Name ?? "null";
			handled = false;
			if (hit is Interactive interactive)
			{
				var args = MakeWheel(hit, window, pos, delta);
				interactive.RaiseEvent(args);
				handled = args.Handled;
				return true;
			}
			return false;
		}

		[Fact]
		public void Probe_WebView2Manual_WheelAndSelection()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var wv = new WebView2();
				var sb = new StringBuilder();
				sb.Append("<h1>git mm Reference</h1>");
				for (int i = 1; i <= 120; i++)
				{
					sb.Append($"<h2>Section {i}</h2><p>Paragraph line {i} with some text to make content tall enough.</p>");
					sb.Append("<pre>git mm start\n  --option value</pre>");
				}
				wv.NavigateToString("<!doctype html><html><body>" + sb + "</body></html>");

				var window = new Window { Width = 860, Height = 560, Content = wv };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);

				var scroll = wv; // WebView2 : ScrollViewer
				var descDiag = string.Join(",", wv.GetVisualDescendants().Take(30).Select(v => v.GetType().Name));
				Console.WriteLine($"[probe] wv.Bounds={wv.Bounds} desired={wv.DesiredSize} template={(wv.Template != null ? "set" : "null")} content={wv.Content?.GetType().Name ?? "null"} window.ClientSize={window.ClientSize}");
				Console.WriteLine($"[probe] scpCount={wv.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ScrollContentPresenter>().Count()} scrollbarCount={wv.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>().Count()}");
				Console.WriteLine($"[probe] visualTree(30)={descDiag}");
				Console.WriteLine($"[probe] extent={scroll.Extent.Height:F0} viewport={scroll.Viewport.Height:F0} offset={scroll.Offset.Y:F0}");
				Assert.True(scroll.Extent.Height > scroll.Viewport.Height + 200,
					$"手册内容必须高于视口才能复现滚动（extent={scroll.Extent.Height:F0}, viewport={scroll.Viewport.Height:F0}）");

				// 1) 文本区滚轮
				var scp = scroll.GetVisualDescendants()
					.OfType<global::Avalonia.Controls.Presenters.ScrollContentPresenter>().First();
				var origin = scp.TranslatePoint(new Point(0, 0), window)!.Value;
				var p1 = new Point(origin.X + 100, origin.Y + 60);
				WheelAt(window, p1, new Vector(0, -1), out var hit1, out var h1);
				Dispatcher.UIThread.RunJobs();
				Console.WriteLine($"[probe] wheel@text ({p1.X:F0},{p1.Y:F0}) hit={hit1} handled={h1} offset={scroll.Offset.Y:F1}");

				// 2) 空白区（两栏边距/块间隙）滚轮
				scroll.Offset = new Vector(0, 0);
				Dispatcher.UIThread.RunJobs();
				Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);
				var p2 = new Point(origin.X + 5, origin.Y + scp.Bounds.Height * 0.5);
				WheelAt(window, p2, new Vector(0, -1), out var hit2, out var h2);
				Dispatcher.UIThread.RunJobs();
				Console.WriteLine($"[probe] wheel@edge ({p2.X:F0},{p2.Y:F0}) hit={hit2} handled={h2} offset={scroll.Offset.Y:F1}");

				// 3) 文本块类型（选中能力）
				var textBlocks = wv.GetVisualDescendants().OfType<TextBlock>().Take(5).ToList();
				foreach (var tb in textBlocks)
				{
					Console.WriteLine($"[probe] textblock type={tb.GetType().Name} selectable={tb is SelectableTextBlock}");
				}
				var selectableCount = wv.GetVisualDescendants().OfType<SelectableTextBlock>().Count();
				Console.WriteLine($"[probe] selectableTextBlocks={selectableCount} totalTextBlocks={wv.GetVisualDescendants().OfType<TextBlock>().Count()}");

				window.Close();
			});
		}
	}
}
