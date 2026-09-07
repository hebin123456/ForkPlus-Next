using System;
using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.Git;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs
{
	public partial class AskPassWindow : ForkPlusDialogWindow
	{
		[Null]
		private AskPassRequest _askPassRequest;

		private string _arguments;

		[Null]
		private string _httpsUsernameHost;

		[Null]
		private string _httpsPasswordHost;

		[Null]
		private string _httpsPasswordUsername;

		public string Result { get; private set; }

		public AskPassWindow(string arguments, string repositoryPath)
		{
			InitializeComponent();
			_askPassRequest = AskPassRequest.Parse(arguments);
			_arguments = arguments;
			RememberCheckBox.Hide();
			RememberAccountCheckBox.Hide();
			RememberPasswordCheckBox.Hide();
			NeverAskCheckBox.Hide();
			base.DialogTitle = ((repositoryPath != "") ? Path.GetFileName(repositoryPath) : PreferencesLocalization.Current("Credentials Required"));
			if (_arguments.StartsWith("Username for"))
			{
				base.DialogDescription = _arguments;
				InputTextBlock.Text = PreferencesLocalization.Current("User Name:");
				InputTextBox.Show();
				InputPasswordBox.Hide();
				InputTextBox.Focus();
				// 凭据记忆（Layer D）：HTTP(S) 用户名询问——预填已记住账号 + 默认勾选"记住账号"
				if (SavedCredentialStore.TryParseUsernamePrompt(_arguments, out _httpsUsernameHost))
				{
					SavedCredentialStore.SavedCredential saved = SavedCredentialStore.Current.FindEntry(_httpsUsernameHost);
					if (saved?.Username != null)
					{
						InputTextBox.Text = saved.Username;
					}
					RememberAccountCheckBox.IsChecked = true;
					RememberAccountCheckBox.Show();
				}
			}
			else if (_askPassRequest is AskPassRequest.SshPassphrase sshPassphrase)
			{
				base.DialogDescription = PreferencesLocalization.FormatCurrent("Passphrase for SSH key '{0}'", sshPassphrase.KeyPath);
				InputTextBlock.Text = PreferencesLocalization.Current("Passphrase:");
				InputTextBox.Hide();
				InputPasswordBox.Show();
				InputPasswordBox.Focus();
				RememberCheckBox.Show();
			}
			else if (_arguments.StartsWith("Enter passphrase"))
			{
				base.DialogDescription = _arguments;
				InputTextBlock.Text = PreferencesLocalization.Current("Passphrase:");
				InputTextBox.Hide();
				InputPasswordBox.Show();
				InputPasswordBox.Focus();
			}
			else if (_askPassRequest is AskPassRequest.SshUserPassword sshUserPassword)
			{
				base.DialogDescription = PreferencesLocalization.FormatCurrent("Passphrase for '{0}'", sshUserPassword.Username + "@" + sshUserPassword.Url.Host);
				InputTextBlock.Text = PreferencesLocalization.Current("Password:");
				InputTextBox.Hide();
				InputPasswordBox.Show();
				InputPasswordBox.Focus();
				RememberCheckBox.Show();
			}
			else if (SavedCredentialStore.TryParsePasswordPrompt(_arguments, out _httpsPasswordHost, out _httpsPasswordUsername))
			{
				// 凭据记忆（Layer D）：HTTP(S) 密码询问——"记住密码"（自动填充）+ "不再询问"选项
				base.DialogDescription = _arguments;
				InputTextBlock.Text = PreferencesLocalization.Current("Password:");
				InputTextBox.Hide();
				InputPasswordBox.Show();
				InputPasswordBox.Focus();
				RememberPasswordCheckBox.Show();
				NeverAskCheckBox.Show();
			}
			else
			{
				base.DialogDescription = _arguments;
				InputTextBlock.Text = PreferencesLocalization.Current("Password:");
				InputTextBox.Hide();
				InputPasswordBox.Show();
				InputPasswordBox.Focus();
			}
			base.SubmitButtonTitle = PreferencesLocalization.Current("OK");
		}

		protected override void OnSubmit()
		{
			if (_arguments.StartsWith("Username for"))
			{
				Result = InputTextBox.Text;
				// 凭据记忆（Layer D）：记住账号（默认勾选）——host → username，下次询问预填
				if (_httpsUsernameHost != null && RememberAccountCheckBox.IsChecked.GetValueOrDefault() && !string.IsNullOrEmpty(Result))
				{
					SavedCredentialStore.Current.RememberUsername(_httpsUsernameHost, Result);
				}
			}
			else if (_httpsPasswordHost != null)
			{
				Result = InputPasswordBox.Text;
				// 凭据记忆（Layer D）：记住密码（勾选后 credential get 静默命中）+ 不再询问
				if (RememberPasswordCheckBox.IsChecked.GetValueOrDefault() && !string.IsNullOrEmpty(Result))
				{
					SavedCredentialStore.Current.RememberPassword(_httpsPasswordHost, _httpsPasswordUsername, Result);
				}
				if (NeverAskCheckBox.IsChecked.GetValueOrDefault())
				{
					SavedCredentialStore.Current.SetNeverAsk(_httpsPasswordHost, true);
				}
			}
			else
			{
				Result = InputPasswordBox.Text;
			}
			if (_askPassRequest is AskPassRequest.SshPassphrase sshPassphrase)
			{
				if (RememberCheckBox.IsChecked.GetValueOrDefault())
				{
					WindowsCredentialManager.StoreSshPassphrase(sshPassphrase.KeyPath, Result);
				}
			}
			else if (_arguments.StartsWith("Enter passphrase"))
			{
				string text = AskPassParser.ParseSshKey(_arguments);
				if (!string.IsNullOrEmpty(text))
				{
					WindowsCredentialManager.StoreSshPassphrase(text, Result);
				}
			}
			else if (_askPassRequest is AskPassRequest.SshUserPassword sshUserPassword && RememberCheckBox.IsChecked.GetValueOrDefault())
			{
				WindowsCredentialManager.StoreSshUserPassword(sshUserPassword.Url, sshUserPassword.Username, Result);
			}
			Close();
		}

	}
}
