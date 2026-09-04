// 回归测试（2026-09-04，"所有提交页分隔条只有 tab 附近能拖、中间找不到 Resize"修复产物）：
// 根因（探针实证）：项目隐式 GridSplitter ControlTheme（x:Key={x:Type GridSplitter}，
// Commonresources.axaml）只带 Background setter、无 Template。WPF 中 GridSplitter 有
// 系统默认模板兜底（透明背景热区仍可命中）；Avalonia 中该 ControlTheme 在
// App.Resources 优先于 Fluent 主题生效，无模板 = 无视觉内容 = 不参与命中测试 →
// 所有未显式引用 HorizontalGridSplitter/VerticalGridSplitter 主题的 GridSplitter
// （SecondColumnHorizontalGridSplitter：32px 热区 + 透明背景 + 顶部 1px 边线）整个
// 不可拖——用户只有 tab 附近（First 分隔条 5px 线）能拖。修复：补 Border 模板
// （Background/BorderBrush/BorderThickness 全 TemplateBinding，与 WPF 系统默认等价）。
// 附带实证：Avalonia 命中测试对 alpha=0 透明色 brush 有效（非 null 即命中，与 WPF 一致），
// 透明不是根因、无模板才是。
// 本测试守卫：复刻 RepositoryContentUserControl.RevisionView 布局，验证中栏区域
// （SecondColumnHorizontalGridSplitter 覆盖范围）全宽命中该分隔条。
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class GridSplitterHitTestProbeTests
	{
		[Fact]
		public void SecondColumnSplitter_HitTestAcrossFullWidth()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				// 复刻 RepositoryContentUserControl.RevisionView 结构
				var grid = new Grid();
				grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				grid.RowDefinitions.Add(new RowDefinition { MinHeight = 110 });
				grid.RowDefinitions.Add(new RowDefinition { MinHeight = 110 });
				grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(400), MinWidth = 300 });
				grid.ColumnDefinitions.Add(new ColumnDefinition());
				grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

				// 详情面板占位：顶部 30px tab 头（实色背景），复刻 RevisionDetailsUserControl
				var details = new Grid();
				Grid.SetRow(details, 2);
				Grid.SetColumn(details, 0);
				Grid.SetColumnSpan(details, 3);
				details.Children.Add(new Border { Height = 30, Background = Brushes.LightGray });
				details.Children.Add(new Border { Background = Brushes.WhiteSmoke });
				grid.Children.Add(details);

				// Second：与生产完全一致（无 Theme 引用 → 隐式 {x:Type GridSplitter} 主题）
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

				var window = new Window { Width = 1200, Height = 600, Content = grid };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				var row2Y = details.TranslatePoint(new Point(0, 0), grid)!.Value.Y;

				// 守卫 1：隐式主题有模板（视觉子元素存在）——修复前 second 无任何子元素
				Assert.True(second.GetVisualChildren().GetEnumerator().MoveNext(),
					"隐式 GridSplitter 主题必须有模板（无模板 = 无视觉内容 = 不可命中，即本 bug 形态）");

				// 守卫 2：中栏区域（260px 边距起）热区中线上全宽命中 second
				double y = row2Y + 16;
				foreach (double x in new[] { 300.0, 500.0, 800.0, 1100.0 })
				{
					var hit = grid.InputHitTest(new Point(x, y));
					bool isSecond = false;
					for (Visual? v = hit as Visual; v != null; v = v.GetVisualParent())
					{
						if (v is GridSplitter gs && gs.Name == "SecondColumnHorizontalGridSplitter")
						{
							isSecond = true;
							break;
						}
					}
					Assert.True(isSecond, $"x={x} y={y:F0} 应命中 SecondColumnHorizontalGridSplitter（实际命中 {hit?.GetType().Name}）——分隔条热区中栏区域不可拖");
				}

				window.Close();
			});
		}
	}
}
