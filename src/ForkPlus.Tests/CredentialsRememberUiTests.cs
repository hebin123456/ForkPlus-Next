// 凭据记忆（Layer D）UI 专项测试：
// - AskPassWindow HTTP(S) 增强：Username 询问预填已记住账号 + "记住账号"默认勾选；
//   Password 询问的"记住密码"/"不再询问"勾选后提交 → store 写入（真实 OnSubmit 链）。
// - CredentialsUserControl（偏好设置 > Credentials）：列表装配 + Remove / Ask Again /
//   Ask Again for All Hosts 的即时生效（SwapForTests 注入隔离 store，不污染用户数据目录）。
// 模式与 E2e25 一致：生产构造器直构 + Show() 非模态 + Footer 按钮经视觉树定位。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.UI.UserControls.Preferences;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class CredentialsRememberUiTests
	{
		private static void RunJobs()
		{
			Dispatcher.UIThread.RunJobs();
		}

		private static string Tr(string text)
		{
			return E2eMainWindowHarness.Tr(text);
		}

		private static Button FindButton(Visual root, string content)
		{
			return UiClick.FindAll<Button>(root)
				.FirstOrDefault((Button b) => UiClick.ContentText(b) == content && b.IsVisible);
		}

		private SavedCredentialStore CreateIsolatedStore(out SavedCredentialStore previous)
		{
			string path = Path.Combine(Path.GetTempPath(), "fp-creds-ui-" + Guid.NewGuid().ToString("N") + ".json");
			var store = new SavedCredentialStore(path);
			previous = SavedCredentialStore.SwapForTests(store);
			return store;
		}

		// ============================ AskPassWindow：Username 询问 ============================

		[Fact]
		public void AskPass_UsernamePrompt_PrefillsAndDefaultsRememberAccount()
		{
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				store.RememberUsername("github.com", "octocat");
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new global::ForkPlus.UI.Dialogs.AskPassWindow("Username for 'https://github.com':", "/tmp/myrepo");
					window.Show();
					RunJobs();

					// 装配：明文框 + 预填已记住账号 + "记住账号"可见且默认勾选
					Assert.True(window.InputTextBox.IsVisible);
					Assert.Equal("octocat", window.InputTextBox.Text);
					Assert.True(window.RememberAccountCheckBox.IsVisible, "HTTPS Username 询问应显示记住账号");
					Assert.True(window.RememberAccountCheckBox.IsChecked.GetValueOrDefault(), "记住账号应默认勾选");
					Assert.False(window.RememberPasswordCheckBox.IsVisible);
					Assert.False(window.NeverAskCheckBox.IsVisible);
					Assert.False(window.RememberCheckBox.IsVisible);

					// 改账号 → 提交 → store 更新（默认勾选即记住）
					window.InputTextBox.Text = "newcat";
					RunJobs();
					UiClick.Click(FindButton(window, Tr("OK")));
					RunJobs();

					Assert.Equal("newcat", window.Result);
					Assert.Equal("newcat", store.FindEntry("github.com").Username);
				});
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void AskPass_UsernamePrompt_Unchecked_DoesNotRemember()
		{
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new global::ForkPlus.UI.Dialogs.AskPassWindow("Username for 'https://github.com':", "");
					window.Show();
					RunJobs();

					window.RememberAccountCheckBox.IsChecked = false;
					window.InputTextBox.Text = "octocat";
					RunJobs();
					UiClick.Click(FindButton(window, Tr("OK")));
					RunJobs();
				});
				Assert.Null(store.FindEntry("github.com"));
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		// ============================ AskPassWindow：Password 询问 ============================

		[Fact]
		public void AskPass_HttpsPasswordPrompt_RememberAndNeverAsk()
		{
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new global::ForkPlus.UI.Dialogs.AskPassWindow("Password for 'https://octocat@github.com':", "");
					window.Show();
					RunJobs();

					// 装配：密码框 + 两个新选项可见，SSH 的 Remember 不显示
					Assert.True(window.InputPasswordBox.IsVisible);
					Assert.True(window.RememberPasswordCheckBox.IsVisible, "HTTPS Password 询问应显示记住密码");
					Assert.True(window.NeverAskCheckBox.IsVisible, "HTTPS Password 询问应显示不再询问");
					Assert.False(window.RememberAccountCheckBox.IsVisible);
					Assert.False(window.RememberCheckBox.IsVisible);
					Assert.False(window.RememberPasswordCheckBox.IsChecked.GetValueOrDefault(), "记住密码默认不勾选");

					// 勾选两项 → 输入 → 提交 → store 写入
					window.RememberPasswordCheckBox.IsChecked = true;
					window.NeverAskCheckBox.IsChecked = true;
					window.InputPasswordBox.Text = "secret123";
					RunJobs();
					UiClick.Click(FindButton(window, Tr("OK")));
					RunJobs();

					Assert.Equal("secret123", window.Result);
				});
				// 提交后断言（store 写入在 OnSubmit 同步完成）
				Assert.True(store.TryGetPassword("github.com", out string username, out string password));
				Assert.Equal("octocat", username);
				Assert.Equal("secret123", password);
				Assert.True(store.FindEntry("github.com").NeverAskAgain);
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void AskPass_HttpsPasswordPrompt_Unchecked_NeitherRemembered()
		{
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new global::ForkPlus.UI.Dialogs.AskPassWindow("Password for 'https://octocat@github.com':", "");
					window.Show();
					RunJobs();

					window.InputPasswordBox.Text = "secret123";
					RunJobs();
					UiClick.Click(FindButton(window, Tr("OK")));
					RunJobs();
					Assert.Equal("secret123", window.Result);
				});
				Assert.Null(store.FindEntry("github.com"));
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		// UserControl 无 Show()：套宿主 Window 挂视觉树（模块 25 窗口直构模式的变体）
		private Window HostInWindow(UserControl control)
		{
			var host = new Window
			{
				Content = control,
				Width = 640,
				Height = 480
			};
			host.Show();
			return host;
		}

		// ============================ 偏好设置：Credentials 页 ============================

		[Fact]
		public void PreferencesCredentialsPage_ListAndButtons()
		{
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				store.RememberPassword("a.example.com", "alice", "pw-a");
				store.RememberUsername("b.example.com", "bob");
				store.SetNeverAsk("b.example.com", enabled: true);

				HeadlessAppBootstrap.Run(delegate
				{
					var control = new CredentialsUserControl();
					control.Initialize(null);
					Window host = HostInWindow(control);
					RunJobs();

					// 列表装配：2 行（host 排序），状态文本区分"记住密码/仅账号/不再询问"
					var texts = UiClick.FindAll<global::Avalonia.Controls.TextBlock>(control)
						.Select((global::Avalonia.Controls.TextBlock t) => t.Text)
						.Where((string t) => !string.IsNullOrEmpty(t))
						.ToList();
					Assert.Contains("a.example.com", texts);
					Assert.Contains("alice", texts);
					Assert.Contains("b.example.com", texts);
					Assert.Contains(Tr("Password saved"), texts);
					Assert.Contains(Tr("User name only") + ", " + Tr("Never ask"), texts);

					// ===== Remove 单条：a.example.com 整条删除 =====
					Button removeA = UiClick.FindAll<Button>(control)
						.FirstOrDefault((Button b) => UiClick.ContentText(b) == Tr("Remove")
							&& (b.Tag as string) == "a.example.com");
					Assert.NotNull(removeA);
					UiClick.Click(removeA);
					RunJobs();
					Assert.Null(store.FindEntry("a.example.com"));

					// ===== Ask Again 单条：b.example.com 清"不再询问"（账号记忆保留） =====
					Button askAgainB = UiClick.FindAll<Button>(control)
						.FirstOrDefault((Button b) => UiClick.ContentText(b) == Tr("Ask Again")
							&& (b.Tag as string) == "b.example.com");
					Assert.NotNull(askAgainB);
					UiClick.Click(askAgainB);
					RunJobs();
					Assert.False(store.FindEntry("b.example.com").NeverAskAgain);
					Assert.Equal("bob", store.FindEntry("b.example.com").Username);

					// ===== Ask Again for All Hosts：批量重开（重新构造一个 neverAsk 场景）=====
					store.SetNeverAsk("b.example.com", enabled: true);
					Button askAll = UiClick.FindAll<Button>(control)
						.FirstOrDefault((Button b) => UiClick.ContentText(b) == Tr("Ask Again for All Hosts"));
					Assert.NotNull(askAll);
					UiClick.Click(askAll);
					RunJobs();
					Assert.False(store.FindEntry("b.example.com").NeverAskAgain);

					host.Close();
					RunJobs();
				});
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void PreferencesCredentialsPage_EmptyState()
		{
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var control = new CredentialsUserControl();
					control.Initialize(null);
					Window host = HostInWindow(control);
					RunJobs();

					var texts = UiClick.FindAll<global::Avalonia.Controls.TextBlock>(control)
						.Select((global::Avalonia.Controls.TextBlock t) => t.Text)
						.Where((string t) => !string.IsNullOrEmpty(t))
						.ToList();
					Assert.Contains(Tr("No saved credentials yet."), texts);

					host.Close();
					RunJobs();
				});
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void PreferencesWindow_HasCredentialsTab()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new global::ForkPlus.UI.Dialogs.PreferencesWindow();
				window.Show();
				RunJobs();

				// Tab 装配：Credentials 页存在且 Header 已本地化装配
				Assert.NotNull(window.CredentialsTabItem);
				Assert.Equal(Tr("Credentials"), window.CredentialsTabItem.Header?.ToString());

				window.Close();
				RunJobs();
			});
		}
	}
}
