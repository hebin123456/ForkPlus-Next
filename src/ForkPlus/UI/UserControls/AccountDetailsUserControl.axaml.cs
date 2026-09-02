using System;
using ForkPlus.UI.WpfCompat;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup;
using ForkPlus.Accounts;
using ForkPlus.UI.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;

namespace ForkPlus.UI.UserControls
{
	public partial class AccountDetailsUserControl : UserControl
	{
		[Null]
		private Account _account;

		public AccountDetailsUserControl()
		{
			InitializeComponent();
		}

		public void ShowDetails([Null] Account account)
		{
			_account = account;
			Refresh();
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			e.Uri.OpenInBrowser();
		}

		private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.Source is TabControl)
			{
				Refresh();
			}
		}

		private void Refresh()
		{
			if (_account == null)
			{
				FallbackUserControl.Show();
				return;
			}
			FallbackUserControl.Hide();
			Account account = _account;
			string avatarUrl = account.AvatarUrl;
			AvatarImage.Url = null;
			Dispatcher.UIThread.Post(delegate
			{
				if (_account == account)
				{
					AvatarImage.Url = avatarUrl;
				}
			}, DispatcherPriority.Background);
			HeaderUserNameTextBlock.Text = _account.Username;
			string serverUrl = _account.ServerUrl ?? "";
			HeaderProfileUrlHyperlink.NavigateUri = Uri.TryCreate(serverUrl, UriKind.Absolute, out Uri uri) ? uri : null;
			HeaderProfileUrlTextBlock.Text = serverUrl;
			if (AccountDetailsTabControl.SelectedItem is AccountTabItem)
			{
				AccountTabItem.Refresh(_account);
			}
			else if (AccountDetailsTabControl.SelectedItem is AccountRepositoriesTabItem)
			{
				AccountRepositoriesTabItem.Refresh(_account);
			}
		}

	}
}
