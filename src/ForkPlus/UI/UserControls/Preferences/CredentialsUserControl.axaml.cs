using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.UserControls.Preferences
{
	/// <summary>
	/// 偏好设置 &gt; Credentials：凭据记忆（Layer D）管理页。
	///
	/// 展示 SavedCredentialStore 的全部条目（host / 账号 / 状态），支持：
	/// - 单条"Ask Again"：清除该主机的"不再询问"标记（重新弹出的开关，逐主机）；
	/// - 单条"Remove"：删除该主机全部凭据记忆（下次询问从零开始）；
	/// - "Ask Again for All Hosts"：全局重开询问（批量版开关）。
	/// 凭据操作即时落盘（弹窗勾选/认证 erase 联动都直接写 store），无需 Save 按钮。
	/// </summary>
	public partial class CredentialsUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private ForkPlusDialogWindow _parentWindow;

		public CredentialsUserControl()
		{
			InitializeComponent();
		}

		public void Initialize(ForkPlusDialogWindow parentWindow)
		{
			_parentWindow = parentWindow;
			LoadCredentials();
		}

		public void ApplyLocalization()
		{
			// 动态行文本经 PreferencesLocalization.Current 构建，语言切换时重建
			LoadCredentials();
		}

		private void LoadCredentials()
		{
			CredentialsListPanel.Children.Clear();
			List<SavedCredentialStore.SavedCredential> all = SavedCredentialStore.Current.GetAll();
			if (all.Count == 0)
			{
				CredentialsListPanel.Children.Add(new TextBlock
				{
					Text = PreferencesLocalization.Current("No saved credentials yet."),
					FontSize = 13,
					Opacity = 0.7,
					Margin = new Thickness(0, 4, 0, 4)
				});
				return;
			}
			foreach (SavedCredentialStore.SavedCredential entry in all)
			{
				CredentialsListPanel.Children.Add(BuildCredentialRow(entry));
			}
		}

		private Control BuildCredentialRow(SavedCredentialStore.SavedCredential entry)
		{
			Grid grid = new Grid
			{
				Margin = new Thickness(0, 3, 0, 3)
			};
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1, GridUnitType.Star)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1, GridUnitType.Star)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			TextBlock host = new TextBlock
			{
				Text = entry.Host,
				FontSize = 13,
				FontWeight = FontWeight.Medium,
				VerticalAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(host, 0);
			TextBlock username = new TextBlock
			{
				Text = string.IsNullOrEmpty(entry.Username) ? "-" : entry.Username,
				FontSize = 13,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(8, 0, 0, 0)
			};
			Grid.SetColumn(username, 1);
			string status = entry.HasPassword
				? PreferencesLocalization.Current("Password saved")
				: PreferencesLocalization.Current("User name only");
			if (entry.NeverAskAgain)
			{
				status = status + ", " + PreferencesLocalization.Current("Never ask");
			}
			TextBlock statusText = new TextBlock
			{
				Text = status,
				FontSize = 12,
				Opacity = 0.7,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(8, 0, 10, 0)
			};
			Grid.SetColumn(statusText, 2);
			grid.Children.Add(host);
			grid.Children.Add(username);
			grid.Children.Add(statusText);
			if (entry.NeverAskAgain)
			{
				Button askAgain = new Button
				{
					Content = PreferencesLocalization.Current("Ask Again"),
					Tag = entry.Host,
					Margin = new Thickness(0, 0, 8, 0)
				};
				askAgain.Click += AskAgainButton_Click;
				Grid.SetColumn(askAgain, 3);
				grid.Children.Add(askAgain);
			}
			Button remove = new Button
			{
				Content = PreferencesLocalization.Current("Remove"),
				Tag = entry.Host
			};
			remove.Click += RemoveButton_Click;
			Grid.SetColumn(remove, 4);
			grid.Children.Add(remove);
			return grid;
		}

		private void AskAgainButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button button && button.Tag is string host)
			{
				SavedCredentialStore.Current.SetNeverAsk(host, enabled: false);
				LoadCredentials();
			}
		}

		private void RemoveButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button button && button.Tag is string host)
			{
				SavedCredentialStore.Current.Remove(host);
				LoadCredentials();
			}
		}

		private void AskAllAgainButton_Click(object sender, RoutedEventArgs e)
		{
			SavedCredentialStore.Current.ClearAllNeverAsk();
			LoadCredentials();
		}
	}
}
