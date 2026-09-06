using System;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands
{
	public class ShowAskPassWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => null;

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(string request, bool noPrompt, string repositoryPath, out string result)
		{
			AskPassRequest askPassRequest = AskPassRequest.Parse(request);
			if (askPassRequest != null)
			{
				string text = QueryFromWindowsCredentialManager(askPassRequest);
				if (text != null)
				{
					result = text;
					return;
				}
			}
			if (noPrompt)
			{
				result = string.Empty;
				return;
			}
			AskPassWindow askPassWindow = new AskPassWindow(request, repositoryPath);
			askPassWindow.ShowDialog();
			result = askPassWindow.Result ?? string.Empty;
		}

		[Null]
		private string QueryFromWindowsCredentialManager(AskPassRequest askPassRequest)
		{
			// 平台守卫（凭据收编 Layer C 顺带修复）：WCM 的 Advapi32 P/Invoke 在非
			// Windows 上抛 DllNotFoundException，而调用链（AskPass IPC 服务线程 →
			// UIThread.Sync）只捕获 IOException，一次 SSH 密钥 askpass 就会把 IPC 线程带崩。
			if (!OperatingSystem.IsWindows())
			{
				return null;
			}
			if (askPassRequest is AskPassRequest.SshPassphrase sshPassphrase)
			{
				string text = WindowsCredentialManager.QuerySshPassphrase(sshPassphrase.KeyPath);
				if (string.IsNullOrEmpty(text))
				{
					return null;
				}
				return text;
			}
			if (askPassRequest is AskPassRequest.SshUserPassword sshUserPassword)
			{
				string text2 = WindowsCredentialManager.QuerySshUserPassword(sshUserPassword.Url, sshUserPassword.Username);
				if (string.IsNullOrEmpty(text2))
				{
					return null;
				}
				return text2;
			}
			return null;
		}
	}
}
