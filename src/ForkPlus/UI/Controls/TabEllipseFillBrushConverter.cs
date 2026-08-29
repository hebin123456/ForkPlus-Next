using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Markup;
using Avalonia.Media;
using Avalonia.Data.Converters;

namespace ForkPlus.UI.Controls
{
	public class TabEllipseFillBrushConverter : global::Avalonia.Markup.Xaml.MarkupExtension, IMultiValueConverter
	{
		public object Convert(global::System.Collections.Generic.IList<object> values, Type targetType, object parameter, CultureInfo culture)
		{
			if (values.Length < 2)
			{
				return Brushes.Transparent;
			}
			SolidColorBrush solidColorBrush = (SolidColorBrush)values[0];
			if (!(bool)values[1])
			{
				return Brushes.Transparent;
			}
			return solidColorBrush ?? ClosableTabItem.IsDirtyDefaultBrush;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			return this;
		}
	}
}
