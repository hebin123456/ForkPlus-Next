using System;
using ForkPlus.UI.WpfCompat;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using ForkPlus.Settings;
using AvaloniaEdit.Rendering;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls.Editor
{
	internal class CodeEditorLineNumberMargin : ClearTypeLineNumberMargin
	{
		private static readonly Typeface _typeface;

		private static readonly Brush _lightBrush;

		private static readonly Brush _darkBrush;

		private static readonly Pen _separatorPenLight;

		private static readonly Pen _separatorPenDark;

		private static readonly double HorizontalMargin;

		private Brush _brush;

		private Pen _separatorPen;

		private int _lineNumberLength = 2;

		// Migration note：WPF 原基类（AvalonEdit TextEditorMargin 系）的 typeface/emSize 实例字段，
		// AvaloniaEdit 侧无此基类，在此补声明（CreateFormattedText 使用）。
		private Typeface typeface = _typeface;
		private double emSize = 11.0;

		static CodeEditorLineNumberMargin()
		{
			_typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal);
			_lightBrush = new SolidColorBrush(Color.FromRgb(192, 192, 192));
			_darkBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160));
			_separatorPenLight = new Pen(new SolidColorBrush(Color.FromRgb(218, 218, 215)), 1.0);
			_separatorPenDark = new Pen(new SolidColorBrush(Color.FromRgb(110, 110, 110)), 1.0);
			HorizontalMargin = 5.0;
		}

		public CodeEditorLineNumberMargin()
		{
			typeface = _typeface;
			emSize = 11.0;
			RefreshBrushes();
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
			RenderOptionsShim.SetClearTypeHint(this, ClearTypeHint.Enabled);
		}

		public void UpdateLineNumbersData()
		{
			int num = Math.Max(_lineNumberLength, base.Document.LineCount.ToString().Length);
			if (num != _lineNumberLength)
			{
				_lineNumberLength = num;
				InvalidateMeasure();
			}
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			return new Size(CreateFormattedText(new string('9', _lineNumberLength)).Width + HorizontalMargin * 3.0, 0.0);
		}

		public override void Render(DrawingContext drawingContext)
		{
			base.Render(drawingContext);
			foreach (VisualLine visualLine in base.TextView.VisualLines)
			{
				drawingContext.DrawText(CreateFormattedText(visualLine.FirstDocumentLine.LineNumber.ToString()), new Point(base.Bounds.Size.Width - HorizontalMargin * 2.0, visualLine.VisualTop - base.TextView.ScrollOffset.Y));
			}
			drawingContext.DrawLine(_separatorPen, new Point(base.Bounds.Size.Width - HorizontalMargin, 0.0), new Point(base.Bounds.Size.Width - HorizontalMargin, base.Bounds.Size.Height));
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			RefreshBrushes();
		}

		private void RefreshBrushes()
	{
		// 优先读资源（CustomColorsDialog 覆盖或主题字典），取不到回退到 light/dark 静态画刷。
		_brush = TryFindColorBrush("LineNumber.ForegroundColor")
			?? (ForkPlusSettings.Default.Theme.IsDarkBase() ? _darkBrush : _lightBrush);
		Color? sepColor = TryFindColor("LineNumber.SeparatorColor");
		_separatorPen = sepColor.HasValue
			? new Pen(new SolidColorBrush(sepColor.Value), 1.0)
			: (ForkPlusSettings.Default.Theme.IsDarkBase() ? _separatorPenDark : _separatorPenLight);
	}

	private static Color? TryFindColor(string key)
	{
		object res = Application.Current?.TryFindResource(key);
		if (res is Color c) return c;
		if (res is SolidColorBrush b) return b.Color;
		return null;
	}

	private static Brush TryFindColorBrush(string key)
	{
		Color? c = TryFindColor(key);
		return c.HasValue ? new SolidColorBrush(c.Value) : null;
	}

		private FormattedText CreateFormattedText(string text)
		{
			// Migration note：Avalonia FormattedText 无 pixelsPerDip 参数（6 参构造）
			return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.RightToLeft, typeface, emSize, _brush);
		}
	}
}
