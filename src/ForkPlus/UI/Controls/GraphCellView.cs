using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.UI.UserControls;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class GraphCellView : global::Avalonia.Controls.Control
	{
		private static readonly double _defaultCellHeight;

		private static readonly double _defaultCellWidth;

		private static readonly double _commitPointRadius;

		private static readonly double _commitMergePointRadius;

		private static readonly double _chevronSize;

		private static readonly double _penThickness;

		private static readonly Pen _mouseOverPen;

		private static readonly string[] _branchColors;

		private static readonly Pen[] _branchPens;

		private readonly DispatcherTimer _showPopupTimer = new DispatcherTimer();

		private readonly DispatcherTimer _closePopupTimer = new DispatcherTimer();

		[Null]
		private Popup _popup;

		private Sha? _activeMergePointSha;

		// TODO 迁移：字段类型收紧为 StyledProperty<T>（XAML 编译器要求 typed property）。
                public static readonly global::Avalonia.StyledProperty<double> CellHeightProperty;

                public static readonly global::Avalonia.StyledProperty<bool> ShowGraphToolTipProperty;

		private bool _isMouseOver;

		public double CellHeight
		{
			get
			{
				return (double)GetValue(CellHeightProperty);
			}
			set
			{
				SetValue(CellHeightProperty, value);
			}
		}

		public bool ShowGraphToolTip
		{
			get
			{
				return (bool)GetValue(ShowGraphToolTipProperty);
			}
			set
			{
				SetValue(ShowGraphToolTipProperty, value);
			}
		}

		private new bool IsMouseOver
		{
			get
			{
				return _isMouseOver;
			}
			set
			{
				if (_isMouseOver != value)
				{
					_isMouseOver = value;
					InvalidateVisual();
				}
			}
		}

		public event EventHandler ExpandToggle;

		static GraphCellView()
		{
			_defaultCellHeight = 22.0;
			_defaultCellWidth = 12.0;
			_commitPointRadius = 1.7;
			_commitMergePointRadius = 5.75;
			_chevronSize = 3.5;
			_penThickness = 1.5;
			_mouseOverPen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0092FF")), 2.0);
			_branchColors = new string[13]
			{
				"#FF9502", "#FFCC00", "#FF3B30", "#A2845E", "#64DA38", "#1CADF8", "#CB73E1", "#8E8E91", "#FF2968", "#30D5C8",
				"#5856D6", "#B4D435", "#FF6F61"
			};
			_branchPens = _branchColors.Map((string c) => new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(c)), _penThickness));
			CellHeightProperty = global::ForkPlus.UI.WpfCompat.WpfPropertyCompat.Register<GraphCellView, double>("CellHeight", _defaultCellHeight);
			ShowGraphToolTipProperty = global::ForkPlus.UI.WpfCompat.WpfPropertyCompat.Register<GraphCellView, bool>("ShowGraphToolTip", true);
			Pen[] branchPens = _branchPens;
			for (int i = 0; i < branchPens.Length; i++)
			{
			}
		}

		public GraphCellView()
		{
			base.UseLayoutRounding= true;
			if (ShowGraphToolTip)
			{
				_showPopupTimer.Interval = TimeSpan.FromMilliseconds(600.0);
				_closePopupTimer.Interval = TimeSpan.FromMilliseconds(200.0);
				_showPopupTimer.Tick += _showPopupTimer_Tick;
				_closePopupTimer.Tick += _closePopupTimer_Tick;
			}
		}

		protected override void OnPointerEntered(global::Avalonia.Input.PointerEventArgs e)
		{
			e.Handled = true;
			// TODO 迁移：WPF PointerEventArgs.LeftButton != MouseButtonState.Pressed → GetCurrentPoint().Properties.IsLeftButtonPressed
			if (ShowGraphToolTip && base.DataContext is DecoratedRevision decoratedRevision && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			{
				base.OnPointerEntered(e);
				if (decoratedRevision.GetParents().Length > 1)
				{
					_activeMergePointSha = decoratedRevision.Sha;
					_showPopupTimer.Start();
					_closePopupTimer.Stop();
				}
			}
		}

		protected override void OnPointerExited(global::Avalonia.Input.PointerEventArgs e)
		{
			e.Handled = true;
			base.OnPointerExited(e);
			IsMouseOver = false;
			if (ShowGraphToolTip && base.DataContext is DecoratedRevision decoratedRevision && decoratedRevision.GetParents().Length > 1)
			{
				_activeMergePointSha = null;
				_showPopupTimer.Stop();
				_closePopupTimer.Start();
			}
		}

		protected override void OnPointerMoved(global::Avalonia.Input.PointerEventArgs e)
		{
			e.Handled = true;
			base.OnPointerMoved(e);
			if (base.DataContext is DecoratedRevision decoratedRevision)
			{
				int num = (int)((e.GetPosition(this).X + 5.0) / _defaultCellWidth);
				IsMouseOver = num == decoratedRevision.GraphInfo.CurrentCommitColumn;
			}
		}

		protected override void OnPointerPressed(global::Avalonia.Input.PointerPressedEventArgs e)
		{
			e.Handled = true;
			base.OnPointerPressed(e);
			// TODO 迁移：WPF PointerPressedEventArgs.ChangedButton == MouseButton.Left → GetCurrentPoint().Properties.IsLeftButtonPressed
			if (IsMouseOver && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && base.DataContext is DecoratedRevision decoratedRevision)
			{
				this.ExpandToggle?.Invoke(this, EventArgs.Empty);
				if (ShowGraphToolTip && decoratedRevision.GetParents().Length > 1)
				{
					_activeMergePointSha = null;
					ClosePopup(hardClose: true);
					_showPopupTimer.Stop();
				}
			}
		}

		// TODO 迁移：WPF 版在 OnRender 里直接设 this.Width = cellWidth * lines.Length（WPF 允许渲染后
		// 触发下一轮流排；Avalonia 渲染期使布局失效直接抛 InvalidOperationException
		// "Visual was invalidated during the render pass"，实证见 MIGRATION.md 运行时修复链 4）。
		// 正确做法：MeasureOverride 按数据源返回期望尺寸（Auto 列/StackPanel 布局取该值），
		// Render 只绘制；DataContext 变化（容器回收复用换绑）时手动失效量测。
		protected override global::Avalonia.Size MeasureOverride(global::Avalonia.Size availableSize)
		{
			double num = _defaultCellWidth;
			if (base.DataContext is DecoratedRevision decoratedRevision)
			{
				num = _defaultCellWidth * (double)decoratedRevision.GraphInfo.Lines.Length;
			}
			return new global::Avalonia.Size(num, CellHeight);
		}

		protected override void OnDataContextChanged(EventArgs e)
		{
			base.OnDataContextChanged(e);
			InvalidateMeasure();
			InvalidateVisual();
		}

		public override void Render(DrawingContext drawingContext)
		{
			if (base.DataContext is DecoratedRevision decoratedRevision)
			{
				// WPF 版在此构造 GuidelineSet 并 PushGuidelineSet 做像素对齐（列坐标均为
				// cellWidth 整数倍，1px 线条需对齐到设备像素中点）。Avalonia 无 GuidelineSet，
				// TODO 迁移：如需恢复像素级锐利，可在 DrawLine/DrawCommitPoint 内对坐标做
				// Math.Floor(x)+0.5 半像素偏移；当前先按原始坐标绘制（视觉上可能有轻微模糊）。
				GraphLine[] lines = decoratedRevision.GraphInfo.Lines;
				foreach (GraphLine line in lines)
				{
					DrawLine(drawingContext, line, _defaultCellWidth);
				}
				bool isMergeCommit = decoratedRevision.GetParents().Length > 1;
				bool isCollapsed = decoratedRevision.IsCollapsed;
				DrawCommitPoint(drawingContext, decoratedRevision.GraphInfo, _defaultCellWidth, isMergeCommit, isCollapsed);
			}
			base.Render(drawingContext);
		}

		// WPF GuidelineSet 辅助已随 PushGuidelineSet 一并移除（见 Render 内注释）

		private void _showPopupTimer_Tick(object sender, EventArgs e)
		{
			ShowPopup();
			_showPopupTimer.Stop();
		}

		private void _closePopupTimer_Tick(object sender, EventArgs e)
		{
			ClosePopup();
			_closePopupTimer.Stop();
		}

		private void DrawLine(DrawingContext drawingContext, GraphLine line, double columnWidth)
		{
			double num = 0.0;
			Point point = new Point(num + columnWidth * (double)(int)line.Column, CellHeight / 2.0);
			Pen pen = _branchPens[line.Id % _branchPens.Length];
			if (line.TopColumn != byte.MaxValue)
			{
				Point point2 = new Point(num + columnWidth * (double)(int)line.TopColumn, 0.0);
				if (line.BottomColumn != byte.MaxValue)
				{
					Point point3 = new Point(num + columnWidth * (double)(int)line.BottomColumn, CellHeight);
					if (line.TopColumn == line.BottomColumn)
					{
						drawingContext.DrawLine(pen, point2, point3);
						return;
					}
					StreamGeometry streamGeometry = new StreamGeometry();
					using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
					{
						streamGeometryContext.BeginFigure(point2,false);
						streamGeometryContext.CubicBezierTo(new Point(point2.X, point3.Y - 5.0),new Point(point3.X, point2.Y + 5.0),point3,true);
					}
					drawingContext.DrawGeometry(null, pen, streamGeometry);
				}
				else if (line.TopColumn == line.Column)
				{
					drawingContext.DrawLine(pen, point2, point);
				}
				else
				{
					StreamGeometry streamGeometry2 = new StreamGeometry();
					using (StreamGeometryContext streamGeometryContext2 = streamGeometry2.Open())
					{
						streamGeometryContext2.BeginFigure(point2,false);
						streamGeometryContext2.CubicBezierTo(new Point(point2.X, point.Y),new Point(point.X + 5.0, point.Y),point,true);
					}
					drawingContext.DrawGeometry(null, pen, streamGeometry2);
				}
			}
			else
			{
				if (line.BottomColumn == byte.MaxValue)
				{
					return;
				}
				Point point4 = new Point(num + columnWidth * (double)(int)line.BottomColumn, CellHeight);
				if (line.Column == line.BottomColumn)
				{
					drawingContext.DrawLine(pen, point, point4);
					return;
				}
				StreamGeometry streamGeometry3 = new StreamGeometry();
				using (StreamGeometryContext streamGeometryContext3 = streamGeometry3.Open())
				{
					streamGeometryContext3.BeginFigure(point,false);
					streamGeometryContext3.CubicBezierTo(new Point(point4.X, point.Y),new Point(point4.X, point.Y + 5.0),point4,true);
				}
				drawingContext.DrawGeometry(null, pen, streamGeometry3);
			}
		}

		private void DrawCommitPoint(DrawingContext drawingContext, GraphInfo graphInfo, double cellWidth, bool isMergeCommit, bool isCollapsed)
		{
			if (graphInfo.CurrentCommitLineId < 0)
			{
				return;
			}
			Pen pen = _branchPens[graphInfo.CurrentCommitLineId % _branchPens.Length];
			Point center = new Point(cellWidth * (double)(int)graphInfo.CurrentCommitColumn, CellHeight / 2.0);
			if (!isMergeCommit)
			{
				drawingContext.DrawEllipse(pen.Brush, pen, center, _commitPointRadius, _commitPointRadius);
				return;
			}
			Pen pen2 = (IsMouseOver ? _mouseOverPen : pen);
			drawingContext.DrawEllipse(global::ForkPlus.UI.Theme.RevisionList.ItemBackgroundBrush, pen2, center, _commitMergePointRadius, _commitMergePointRadius);
			StreamGeometry streamGeometry = new StreamGeometry();
			using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
			{
				if (isCollapsed)
				{
					streamGeometryContext.BeginFigure(new Point(center.X - _chevronSize * 0.5, center.Y - _chevronSize),false);
					streamGeometryContext.LineTo(new Point(center.X + _chevronSize * 0.5, center.Y),true);
					streamGeometryContext.LineTo(new Point(center.X - _chevronSize * 0.5, center.Y + _chevronSize),true);
				}
				else
				{
					streamGeometryContext.BeginFigure(new Point(center.X - _chevronSize, center.Y - _chevronSize * 0.5),false);
					streamGeometryContext.LineTo(new Point(center.X, center.Y + _chevronSize * 0.5),true);
					streamGeometryContext.LineTo(new Point(center.X + _chevronSize, center.Y - _chevronSize * 0.5),true);
				}
			}
			drawingContext.DrawGeometry(null, pen, streamGeometry);
		}

		private void ShowPopup()
		{
			RepositoryUserControl parent = this.GetParent<RepositoryUserControl>();
			if (parent != null && (_popup == null || !_popup.IsOpen))
			{
				Sha? activeMergePointSha = _activeMergePointSha;
				if (activeMergePointSha.HasValue)
				{
					Sha valueOrDefault = activeMergePointSha.GetValueOrDefault();
					double horizontalOffset = Mouse.GetPosition(this).X + 5.0;
					_popup = CreatePopup(parent, horizontalOffset, valueOrDefault);
					_popup.IsOpen = true;
				}
			}
		}

		private void ClosePopup(bool hardClose = false)
		{
			if (_popup != null && _popup.IsOpen && (!_popup.IsPointerOver|| hardClose))
			{
				_popup.IsOpen = false;
				VisualTreeAttachmentHelper.TrySetPopupChild(_popup, null, GetType().Name + ".Popup");
				_popup = null;
			}
		}

		private Popup CreatePopup(RepositoryUserControl repositoryUserControl, double horizontalOffset, Sha sha)
		{
			Popup popup = new Popup();
			popup.HorizontalOffset = horizontalOffset;
			popup.VerticalOffset = -50.0;
			popup.IsLightDismissEnabled= (!true);
			/* TODO 迁移: AllowsTransparency 已删除 */;
			/* TODO 迁移: PopupAnimation 已删除 */;
			popup.PlacementTarget = this;
			RevisionGraphTooltipUserControl revisionGraphTooltipUserControl = new RevisionGraphTooltipUserControl(repositoryUserControl, sha);
			revisionGraphTooltipUserControl.HeightChanged += delegate(object s, EventArgs<double> e)
			{
				double value = e.Value;
				popup.VerticalOffset = 0.0 - value / 2.0 - 10.0;
			};
			VisualTreeAttachmentHelper.TrySetPopupChild(popup, revisionGraphTooltipUserControl, GetType().Name + ".Popup");
			popup.PointerExited += delegate
			{
				_closePopupTimer.Start();
			};
			return popup;
		}
	}
}
