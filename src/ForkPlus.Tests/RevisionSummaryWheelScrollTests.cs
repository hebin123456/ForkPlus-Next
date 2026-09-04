// 回归测试（问题D，2026-09-04）：所有提交 → 提交 tab 面板有滚动条但鼠标滚轮无法滚动。
// 根因：Avalonia 的滚轮滚动由 ScrollContentPresenter（SCP）的类处理器 OnPointerWheelChanged
// 完成（WPF 是 ScrollViewer.OnMouseWheel 自己处理冒泡事件），事件只有路由“经过”SCP 才会触发。
// 无背景的元素不可命中——内容里无背景的空白区域（Grid/Panel）命中会穿透内容落到模板根
// Grid（SCP 的兄弟节点），事件冒泡永远到不了 SCP，滚轮因此失效，只能拖滚动条。
// 修复：ScrollViewer 主题模板给 SCP 加 Background="Transparent"（Scrollviewer.axaml 与
// Listview.axaml 两处），整个视口可命中。本文件端到端复刻 RevisionDetailsUserControl +
// 真实 DiffList + GridSplitter 布局验证：
//   1) 视口内任意位置（含空白区域）滚轮都能滚动面板；
//   2) 垂直滚动条 Thumb 仍然可命中（拖滚动条是用户的兜底操作，不能被透明背景破坏）；
//   3) 子模式 diff 编辑器上的 Tunnel 转发（FileDiffControl.DiffCodeEditor_PreviewMouseWheel）
//      把滚动让给外层面板、编辑器自身不抢滚。
using System;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class RevisionSummaryWheelScrollTests
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

		/// <summary>模拟真实输入：命中 (pos) 处最深的可交互元素并 RaiseEvent 滚轮事件。</summary>
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

		/// <summary>端到端复刻：RepositoryContentUserControl.RevisionView 完整结构（GridSplitter
		/// 与生产一致）+ 真实 RevisionDetailsUserControl + 真实 DiffList 数据。视口内多点滚轮，
		/// 全部必须滚动（修复前无背景空白区域命中模板根 Grid，事件到不了 SCP，一律不滚）。</summary>
		[Fact]
		public void Wheel_InCommitDetailsPanel_AllViewportAreasScroll()
		{
			Console.WriteLine("[test-order] Wheel_InCommitDetailsPanel START");
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				// ===== 复刻 RepositoryContentUserControl.RevisionView =====
				var grid = new Grid();
				grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				grid.RowDefinitions.Add(new RowDefinition { MinHeight = 110 });
				grid.RowDefinitions.Add(new RowDefinition { MinHeight = 110 });
				grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(400), MinWidth = 300 });
				grid.ColumnDefinitions.Add(new ColumnDefinition());
				grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

				// Row1 占位（复刻 RevisionListViewUserControl）
				var listPlaceholder = new Border { Background = Brushes.Beige };
				Grid.SetRow(listPlaceholder, 1);
				Grid.SetColumn(listPlaceholder, 0);
				Grid.SetColumnSpan(listPlaceholder, 3);
				grid.Children.Add(listPlaceholder);

				// Row2：真实 RevisionDetailsUserControl
				var details = new RevisionDetailsUserControl();
				Grid.SetRow(details, 2);
				Grid.SetColumn(details, 0);
				Grid.SetColumnSpan(details, 3);
				grid.Children.Add(details);

				// 4 个分隔条：与生产 XAML 完全一致
				var first = new GridSplitter { Name = "FirstColumnHorizontalGridSplitter" };
				Grid.SetRow(first, 2);
				Grid.SetColumn(first, 0);
				first.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
				first.Theme = global::Avalonia.Application.Current!.FindResource("HorizontalGridSplitter") as global::Avalonia.Styling.ControlTheme;
				grid.Children.Add(first);

				var second = new GridSplitter
				{
					Name = "SecondColumnHorizontalGridSplitter",
					Height = 32,
					Margin = new Thickness(260, 0, 0, 0),
					Background = new SolidColorBrush(Color.Parse("#00FFFFFF")),
					BorderBrush = Brushes.Gray,
					BorderThickness = new Thickness(0, 1, 0, 0),
					HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
				};
				Grid.SetRow(second, 2);
				Grid.SetColumn(second, 0);
				Grid.SetColumnSpan(second, 2);
				second.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
				grid.Children.Add(second);

				var third = new GridSplitter { Name = "ThirdColumnHorizontalGridSplitter" };
				Grid.SetRow(third, 2);
				Grid.SetColumn(third, 2);
				third.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
				third.Theme = global::Avalonia.Application.Current!.FindResource("HorizontalGridSplitter") as global::Avalonia.Styling.ControlTheme;
				grid.Children.Add(third);

				var vertical = new GridSplitter { Name = "VerticalGridSplitter" };
				Grid.SetRow(vertical, 1);
				Grid.SetRowSpan(vertical, 2);
				Grid.SetColumn(vertical, 0);
				vertical.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
				vertical.Theme = global::Avalonia.Application.Current!.FindResource("VerticalGridSplitter") as global::Avalonia.Styling.ControlTheme;
				grid.Children.Add(vertical);

				var window = new Window { Width = 1200, Height = 600, Content = grid };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				// ===== 填充 Summary（Commit tab 默认可见）=====
				var summary = details.SummaryUserControl;
				summary.AuthorTextBlock.Text = "Author Name";
				summary.AuthorEmailTextBlock.Text = "author@example.com";
				summary.AuthorDateTextBlock.Text = "2026-09-04 12:00:00 +0800";
				summary.ShaTextBlock.Text = "0123456789abcdef0123456789abcdef01234567";
				summary.SubjectTextBlock.Text = "Commit subject line";
				var desc = new StringBuilder();
				for (int i = 1; i <= 80; i++)
				{
					desc.AppendLine($"description line {i}");
				}
				summary.DescriptionTextBlock.Text = desc.ToString();
				summary.ReferencesTextBlock.IsVisible = false;
				summary.ReferencePanel.IsVisible = false;
				summary.CommitterDetailsContainer.IsVisible = false;

				// 真实 DiffList 数据（DiffEntry → ItemTemplate → ListBoxItem>Expander>FileDiffControl）
				var entries = new System.Collections.Generic.List<DiffEntry>();
				for (int i = 1; i <= 40; i++)
				{
					var changedFile = new ForkPlus.Git.ChangedFile($"src/module/file{i}.cs", ForkPlus.Git.StatusType.Modified);
					entries.Add(new DiffEntry(null, changedFile));
				}
				summary.DiffList.ItemsSource = entries;
			Dispatcher.UIThread.RunJobs();
			// 强制渲染/组合器同步（InputHitTest 走组合端 AABB 树，渲染线程消费批次前结果是陈旧的）
			Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);

				var scroll = summary.GetVisualDescendants().OfType<ScrollViewer>().First();
				var scp = scroll.GetVisualDescendants()
					.OfType<global::Avalonia.Controls.Presenters.ScrollContentPresenter>().First();
				Assert.True(scroll.Extent.Height > scroll.Viewport.Height + 50,
					$"内容必须高于视口才能复现滚动条场景（extent={scroll.Extent.Height:F0}, viewport={scroll.Viewport.Height:F0}）");

				// ===== 视口内多点滚轮（覆盖文本、空白区域、DiffList 表头等）=====
				var origin = scp.TranslatePoint(new Point(0, 0), window)!.Value;
				double w = scp.Bounds.Width;
				double h = scp.Bounds.Height;
				var points = new[]
				{
					new Point(origin.X + w * 0.15, origin.Y + h * 0.15), // 作者区（文本）
					new Point(origin.X + w * 0.50, origin.Y + h * 0.15), // 作者/提交者之间的空白
					new Point(origin.X + w * 0.85, origin.Y + h * 0.30), // 右侧空白
					new Point(origin.X + w * 0.25, origin.Y + h * 0.55), // 描述区
					new Point(origin.X + w * 0.60, origin.Y + h * 0.70), // DiffList 表头附近
					new Point(origin.X + w * 0.45, origin.Y + h * 0.90), // DiffList 列表项
				};
				foreach (var p in points)
				{
					scroll.Offset = new Vector(0, 0);
					Dispatcher.UIThread.RunJobs();
					WheelAt(window, p, new Vector(0, -1), out var hit, out var handled);
					Dispatcher.UIThread.RunJobs();
					Assert.True(handled, $"滚轮在视口内 ({p.X:F0},{p.Y:F0})（命中 {hit}）必须被处理");
					Assert.True(scroll.Offset.Y >= 40 && scroll.Offset.Y <= 60,
						$"滚轮在视口内 ({p.X:F0},{p.Y:F0})（命中 {hit}）后面板应下滚约50px，实际 {scroll.Offset.Y:F1}");
				}

				// 向上滚：应能滚回顶部
				WheelAt(window, points[3], new Vector(0, 1), out _, out var upHandled);
				Dispatcher.UIThread.RunJobs();
				Assert.True(upHandled, "向上滚轮必须被处理");
				Assert.True(scroll.Offset.Y < 10, $"向上滚轮应把面板滚回顶部，实际 {scroll.Offset.Y:F1}");

				// ===== 垂直滚动条 Thumb 仍可命中（拖滚动条是兜底操作，不能被透明背景破坏）=====
			// 先重置到顶部并同步渲染：Thumb 在轨道内的位置随 Offset 变化写入组合端命中树，
			// 该树由渲染线程消费批次后更新（Avalonia 12 CompositingRenderer.HitTest 走
			// CompositionTarget.TryHitTest 的服务端 AABB 树）——布局完成后立即 InputHitTest
			// 读到的还是 Thumb 移动前的旧位置，命中会落到模板根 Grid 上（间歇性假失败）。
			// CaptureRenderedFrame 强制渲染一帧并等待组合器同步，之后命中结果才是确定性的。
			scroll.Offset = new Vector(0, 0);
			Dispatcher.UIThread.RunJobs();
			Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);
			Dispatcher.UIThread.RunJobs();
				var vbar = scroll.GetVisualDescendants()
					.OfType<global::Avalonia.Controls.Primitives.ScrollBar>()
					.First(s => s.Orientation == Avalonia.Layout.Orientation.Vertical);
				var thumb = vbar.GetVisualDescendants()
					.OfType<global::Avalonia.Controls.Primitives.Thumb>().FirstOrDefault();
				Assert.NotNull(thumb);
				Assert.True(thumb!.Bounds.Height > 0, "Thumb 必须有高度");
				var thumbCenter = thumb.TranslatePoint(
					new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
				var thumbHit = window.InputHitTest(thumbCenter);
				var chain = new StringBuilder();
				bool inThumb = false;
				for (global::Avalonia.Visual? v = thumbHit as global::Avalonia.Visual; v != null; v = v.GetVisualParent())
				{
					if (chain.Length > 0)
					{
						chain.Append(" -> ");
					}
					chain.Append(v.GetType().Name);
					if (ReferenceEquals(v, thumb))
					{
						inThumb = true;
					}
				}
				var bars = scroll.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.ScrollBar>().ToList();
				var barDiag = new StringBuilder();
				foreach (var b in bars)
				{
					var t = b.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.Thumb>().FirstOrDefault();
					barDiag.Append($"[{b.GetType().Name} orient={b.Orientation} bounds={b.Bounds} vis={(b as Visual)!.IsVisible} thumb={(t != null ? t.Bounds.ToString() : "none")}] ");
				}
				var track = vbar.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.Track>().FirstOrDefault();
			var vbarOrigin = vbar.TranslatePoint(new Point(0, 0), window);
			var trackBounds = track != null ? track.TranslatePoint(new Point(0, 0), window)?.ToString() ?? "null" : "no-track";
			var enabledDiag = $"vbar.IsEnabled={vbar.IsEnabled} vbar.IsEffectivelyEnabled={(vbar as global::Avalonia.Input.InputElement)!.IsEffectivelyEnabled} thumb.IsEnabled={(thumb as global::Avalonia.Input.InputElement)!.IsEffectivelyEnabled}";
			// 诊断：Theme/Template/画刷解析状态（定位类跑时 Thumb 不可命中的根因）
			var thumbBorder = thumb.GetVisualDescendants().OfType<Border>().FirstOrDefault();
			var app = global::Avalonia.Application.Current!;
			app.TryGetResource("ScrollBarThumbVertical", app.ActualThemeVariant, out var themeVal);
			app.TryGetResource("ScrollBar.Static.Thumb", app.ActualThemeVariant, out var brushVal);
			app.TryGetResource("ScrollBar.ThumbColor", app.ActualThemeVariant, out var colorVal);
			var themeDiag = $"actualTheme={app.ActualThemeVariant} thumb.Theme={(thumb.Theme != null ? "set:" + thumb.Theme!.TargetType : "null")} thumb.Template={(thumb.Template != null ? "set" : "null")} border={(thumbBorder != null ? $"bg={(thumbBorder!.Background != null ? thumbBorder.Background.ToString() : "NULL")}" : "no-border")} appThemeRes={(themeVal != null ? themeVal.GetType().Name : "null")} appBrushRes={(brushVal != null ? brushVal.GetType().Name + ":" + brushVal : "null")} appColorRes={(colorVal != null ? colorVal.ToString() : "null")}";
				var hitNoFilter = window.InputHitTest(thumbCenter, false);
				var probes = new StringBuilder();
				for (int k = 0; k <= 4; k++)
				{
					var pp = new Point(thumbCenter.X, thumbCenter.Y - 60 + k * 30);
					var ph = window.InputHitTest(pp);
					probes.Append($"({pp.X:F0},{pp.Y:F0})={ph?.GetType().Name ?? "null"} ");
				}
				Console.WriteLine($"[thumb-diag] center={thumbCenter} thumb.Bounds={thumb.Bounds} vbar.Bounds={vbar.Bounds} vbarOrigin={vbarOrigin} trackOrigin={trackBounds} {enabledDiag} {themeDiag} hitNoFilter={hitNoFilter?.GetType().Name ?? "null"} bars={barDiag} probes={probes} hit-chain={chain}");
				Assert.True(inThumb,
					$"Thumb 中心点必须仍命中 Thumb 子树，否则拖拽滚动条会失效。实际命中链：{chain}；滚动条诊断：{barDiag}；探测：{probes}");

				window.Close();
			});
		}

		/// <summary>真实 RevisionSummaryUserControl 单独验证：文本区域与空白区域滚轮都滚动。</summary>
		[Fact]
		public void Wheel_OverSummaryTextAndEmptyArea_ScrollsPanel()
		{
			Console.WriteLine("[test-order] Wheel_OverSummaryTextAndEmptyArea START");
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var control = new RevisionSummaryUserControl();
				control.AuthorTextBlock.Text = "Author Name";
				control.AuthorEmailTextBlock.Text = "author@example.com";
				control.AuthorDateTextBlock.Text = "2026-09-04 12:00:00 +0800";
				control.ShaTextBlock.Text = "0123456789abcdef0123456789abcdef01234567";
				control.SubjectTextBlock.Text = "Commit subject line";
				var desc = new StringBuilder();
				for (int i = 1; i <= 200; i++)
				{
					desc.AppendLine($"description line {i}");
				}
				control.DescriptionTextBlock.Text = desc.ToString();
				control.ReferencesTextBlock.IsVisible = false;
				control.ReferencePanel.IsVisible = false;
				control.CommitterDetailsContainer.IsVisible = false;

				var window = new Window { Width = 900, Height = 350, Content = control };
			window.Show();
			Dispatcher.UIThread.RunJobs();
			// 强制渲染/组合器同步（同上：批次未消费前 InputHitTest 结果为空）
			Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);

				var scroll = control.GetVisualDescendants().OfType<ScrollViewer>().First();
				var scp = scroll.GetVisualDescendants()
					.OfType<global::Avalonia.Controls.Presenters.ScrollContentPresenter>().First();
				Assert.True(scroll.Extent.Height > scroll.Viewport.Height + 50,
					"内容必须高于视口才能复现滚动条场景");

				// 滚轮：作者名文本区域
				var p = control.AuthorTextBlock.TranslatePoint(new Point(5, 5), window)!.Value;
				WheelAt(window, p, new Vector(0, -1), out var hit1, out var h1);
				Dispatcher.UIThread.RunJobs();
				Assert.True(h1, $"滚轮在作者文本上（命中 {hit1}）必须被处理");
				Assert.True(scroll.Offset.Y >= 40 && scroll.Offset.Y <= 60, $"滚轮在作者文本上应下滚约50px，实际 {scroll.Offset.Y:F1}");

				// 滚轮：视口空白区域（修复前命中穿透到模板根 Grid，不滚）
			scroll.Offset = new Vector(0, 0);
			Dispatcher.UIThread.RunJobs();
			Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);
				var origin = scp.TranslatePoint(new Point(0, 0), window)!.Value;
				var empty = new Point(origin.X + scp.Bounds.Width * 0.60, origin.Y + scp.Bounds.Height * 0.10);
				WheelAt(window, empty, new Vector(0, -1), out var hit2, out var h2);
				Dispatcher.UIThread.RunJobs();
				Assert.True(h2, $"滚轮在空白区域（命中 {hit2}）必须被处理");
				Assert.True(scroll.Offset.Y >= 40 && scroll.Offset.Y <= 60, $"滚轮在空白区域应下滚约50px，实际 {scroll.Offset.Y:F1}");

				window.Close();
			});
		}

		/// <summary>子模式 diff 编辑器（DiffList 内 FileDiffControl→TextDiffControl）上的滚轮：
		/// Tunnel 转发（DiffCodeEditor_PreviewMouseWheel）必须把滚动让给外层面板，
		/// 编辑器自身不抢滚，且转发不留下持久性路由破坏。</summary>
		[Fact]
		public void Wheel_OverSubModeDiffEditor_ScrollsOuterPanelNotEditor()
		{
			Console.WriteLine("[test-order] Wheel_OverSubModeDiffEditor START");
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var scroll = new ScrollViewer
				{
					VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
					HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
				};
				var panel = new StackPanel();
				var authorArea = new Border
				{
					Height = 200,
					Background = Brushes.White,
					Child = new SelectableTextBlockWrap("Author Name author@example.com"),
				};
				panel.Children.Add(authorArea);

				var expander = new Expander { Header = "file.txt", IsExpanded = true };
				var diffStandIn = new ContentControl();
				var editor = new ForkPlus.UI.Controls.Editor.Diff.DiffCodeEditor();
				var sbText = new StringBuilder();
				for (int i = 1; i <= 120; i++)
				{
					sbText.AppendLine("line " + i + " the quick brown fox jumps over the lazy dog 0123456789");
				}
				editor.Text = sbText.ToString();
				editor.Height = 500;
				diffStandIn.Content = editor;
				expander.Content = diffStandIn;
				// 与生产 FileDiffControl 完全一致的 Tunnel 注册与转发实现
				diffStandIn.AddHandler(InputElement.PointerWheelChangedEvent, StandInPreviewMouseWheel, RoutingStrategies.Tunnel);
				panel.Children.Add(expander);
				var filler = new Border { Height = 800, Background = Brushes.White };
				panel.Children.Add(filler);
				scroll.Content = panel;

				var window = new Window { Width = 600, Height = 300, Content = scroll };
			window.Show();
			Dispatcher.UIThread.RunJobs();
			// 强制渲染/组合器同步（同上：批次未消费前 InputHitTest 结果为空）
			Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);

				Assert.True(scroll.Extent.Height > scroll.Viewport.Height + 50, "内容必须高于视口");

				var editorScroll = editor.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
				Assert.NotNull(editorScroll);

				// 1) 基线：滚轮在顶部普通区
				var pa = authorArea.TranslatePoint(new Point(30, 30), window)!.Value;
				WheelAt(window, pa, new Vector(0, -1), out var hit1, out var h1);
				Dispatcher.UIThread.RunJobs();
				Assert.True(h1, $"滚轮在普通区（命中 {hit1}）必须被处理");
				Assert.True(scroll.Offset.Y >= 40 && scroll.Offset.Y <= 60, $"普通区滚轮应下滚约50px，实际 {scroll.Offset.Y:F1}");

				// 2) 滚轮在真实 diff 编辑器文本区域（Tunnel 转发路径）：
				//    扫描窗口找出命中链上含 DiffCodeEditor 的点（TranslatePoint 受滚动偏移影响不可靠）
			scroll.Offset = new Vector(0, 250);
			Dispatcher.UIThread.RunJobs();
			Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);
				Point? editorPoint = null;
				for (double y = 5; y < 295 && editorPoint == null; y += 10)
				{
					for (double x = 30; x < 580 && editorPoint == null; x += 40)
					{
						var cand = window.InputHitTest(new Point(x, y));
						for (global::Avalonia.Visual? v = cand as global::Avalonia.Visual; v != null; v = v.GetVisualParent())
						{
							if (v is ForkPlus.UI.Controls.Editor.Diff.DiffCodeEditor)
							{
								editorPoint = new Point(x, y);
								break;
							}
						}
					}
				}
				Assert.True(editorPoint.HasValue, "必须能找到 diff 编辑器上的探测点");
				double beforeEditor = editorScroll!.Offset.Y;
				WheelAt(window, editorPoint!.Value, new Vector(0, -1), out var hit2, out var h2);
				Dispatcher.UIThread.RunJobs();
				Assert.True(h2, $"滚轮在编辑器上（命中 {hit2}）必须被处理");
				Assert.True(scroll.Offset.Y >= 290 && scroll.Offset.Y <= 310,
					$"编辑器上的滚轮应转发给外层面板下滚约50px（250→300），实际 {scroll.Offset.Y:F1}");
				Assert.True(Math.Abs(editorScroll.Offset.Y - beforeEditor) <= 1,
					$"子模式编辑器自身不应抢滚（编辑器 offset {beforeEditor:F1} → {editorScroll.Offset.Y:F1}）");

				// 3) 再回顶部普通区滚一次（确认转发没有留下持久性破坏）
			scroll.Offset = new Vector(0, 0);
			Dispatcher.UIThread.RunJobs();
			Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);
				var pa2 = authorArea.TranslatePoint(new Point(30, 30), window)!.Value;
				WheelAt(window, pa2, new Vector(0, -1), out var hit3, out var h3);
				Dispatcher.UIThread.RunJobs();
				Assert.True(h3, $"转发后普通区滚轮（命中 {hit3}）必须仍被处理");
				Assert.True(scroll.Offset.Y >= 40 && scroll.Offset.Y <= 60, $"转发后普通区滚轮应仍下滚约50px，实际 {scroll.Offset.Y:F1}");

				window.Close();
			});
		}

		/// <summary>与 FileDiffControl.DiffCodeEditor_PreviewMouseWheel 逐行等价的替身实现。</summary>
		private static void StandInPreviewMouseWheel(object sender, PointerWheelEventArgs e)
		{
			if (sender is ContentControl && !e.Handled)
			{
				e.Handled = true;
				Control parent = ((Control)sender).Parent as Control;
				if (parent != null)
				{
					e.Handled = false;
					parent.RaiseEvent(e);
					e.Handled = true;
				}
			}
		}

		private sealed class SelectableTextBlockWrap : global::Avalonia.Controls.SelectableTextBlock
		{
			public SelectableTextBlockWrap(string text)
			{
				Text = text;
				TextWrapping = Avalonia.Media.TextWrapping.Wrap;
			}
		}
	}
}
