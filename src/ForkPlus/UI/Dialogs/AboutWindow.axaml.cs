using System;
using ForkPlus.UI.WpfCompat;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs
{
	public partial class AboutWindow : ForkPlusDialogWindow
	{

		public AboutWindow()
		{
			base.ShowLogo = false;
			base.ShowFooter = false;
			InitializeComponent();
			string title = Translate("About " + App.AppName);
			base.Title = title;
			base.DialogTitle = title;
			VersionTextBlock.Text = string.Format(Translate("Version {0}"), App.Version);
			CopyrightTextBlock.Text = string.Format(Translate("Copyright © {0} Hebin"), DateTime.Now.Year);
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			e.Uri.OpenInBrowser();
			e.Handled = true;
		}

		private void LegalHyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			new LegalWindow().ShowDialog();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
