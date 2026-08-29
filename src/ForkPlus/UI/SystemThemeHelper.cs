using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Avalonia;
using Avalonia.Media;
using ForkPlus.Settings;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.ViewManagement;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;

namespace ForkPlus.UI
{
	internal static class SystemThemeHelper
	{
		[Null]
		private static object _uiSettings;

		private static bool IsWindows11 => App.OSVersion.Build >= 20000;

		public static void SubscribeToSystemEvents()
		{
			UISettings uiSettings = new UISettings();
			uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
			_uiSettings = uiSettings;
		}

		[Null]
		public static Brush GetSystemBrush(Theme.SystemColorType colorType)
		{
			SolidColorBrush solidColorBrush = new SolidColorBrush(GetColor(((UISettings)_uiSettings).GetColorValue(ToUIColorType(colorType))));
			return solidColorBrush;
		}

		private static void UiSettings_ColorValuesChanged(object sender, object args)
		{
			Log.Info("System colors changed");
			global::Avalonia.Threading.Dispatcher.UIThread?.Invoke(delegate
			{
				Theme.Refresh();
			});
		}

		private static UIColorType ToUIColorType(Theme.SystemColorType colorType)
		{
			switch (colorType)
			{
			case Theme.SystemColorType.Accent:
				return (UIColorType)5;
			case Theme.SystemColorType.Accent1:
			if (ForkPlusSettings.Default.Theme.IsDarkBase())
			{
				return (UIColorType)6;
			}
			return (UIColorType)4;
		case Theme.SystemColorType.Accent2:
			if (IsWindows11)
			{
				if (ForkPlusSettings.Default.Theme.IsDarkBase())
				{
						return (UIColorType)7;
					}
					return (UIColorType)4;
				}
				return (UIColorType)5;
			default:
				return (UIColorType)5;
			}
		}

		private static global::Avalonia.Media.Color GetColor(Windows.UI.Color color)
		{
			return global::Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
		}
	}
}
