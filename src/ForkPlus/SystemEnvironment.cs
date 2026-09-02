using System;
using System.IO;

namespace ForkPlus
{
	public static class SystemEnvironment
	{
		/// <summary>
		/// Migration note：跨平台用户主目录。原代码到处用 SystemEnvironment.UserProfileDirectory，
		/// 该 %VAR% 语法仅在 Windows 展开；Linux/macOS 上原样返回字面量 "%userprofile%"，导致
		/// WelcomeWindow 的 IsSubmitAllowed（目录必须存在）永远为 false、"完成"按钮禁用。
		/// 改用 Environment.GetFolderPath(UserProfile)（Windows=C:\Users\x，Unix=$HOME），
		/// 失败时回退 Windows 变量展开，再回退 HOME 环境变量，保证总有可用路径。
		/// </summary>
		public static string UserProfileDirectory
		{
			get
			{
				try
				{
					string text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
					if (!string.IsNullOrEmpty(text))
					{
						return text;
					}
				}
				catch
				{
				}
				try
			{
				string text2 = Environment.ExpandEnvironmentVariables("%userprofile%");
				if (!string.IsNullOrEmpty(text2) && text2.IndexOf('%') < 0)
				{
					return text2;
				}
			}
			catch
			{
			}
				try
				{
					string text3 = Environment.GetEnvironmentVariable("HOME");
					if (!string.IsNullOrEmpty(text3))
					{
						return text3;
					}
				}
				catch
				{
				}
				return AppContext.BaseDirectory;
			}
		}

		[Null]
		public static string LocalSSHDirectory
		{
			get
			{
				try
				{
					string text = UserProfileDirectory;
					if (Directory.Exists(text))
					{
						return Path.Combine(text, ".ssh");
					}
				}
				catch
				{
				}
				return null;
			}
		}

		/// <summary>
		/// Migration note：跨平台 Git 可执行文件名。原代码全仓硬编码 "git.exe"（Windows 惯例），
		/// Linux/macOS 上 git 二进制名是 "git"（无扩展名），文件名校验永不通过，
		/// 导致 ConfigureGitInstanceWindow 的"继续"按钮永远禁用。
		/// </summary>
		public static string GitExecutableName => OperatingSystem.IsWindows() ? "git.exe" : "git";

		/// <summary>
		/// Migration note：判断路径是否为 git 可执行文件（Windows 接受 git.exe，Unix 接受 git 与 git.exe）。
		/// 替代原 `Path.GetFileName(p) == "git.exe"` 的跨平台版本。
		/// </summary>
		public static bool IsGitExecutable(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return false;
			}
			string fileName = Path.GetFileName(path);
			if (OperatingSystem.IsWindows())
			{
				return string.Equals(fileName, "git.exe", StringComparison.OrdinalIgnoreCase);
			}
			return string.Equals(fileName, "git", StringComparison.Ordinal)
				|| string.Equals(fileName, "git.exe", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Migration note：Unix 系统常见 git 安装路径（用于 ConfigureGitInstanceWindow 候选探测）。
		/// Windows 走原 %programfiles% 系列候选，返回空数组。
		/// </summary>
		public static string[] GetUnixCommonGitPaths()
		{
			if (OperatingSystem.IsWindows())
			{
				return Array.Empty<string>();
			}
			string[] candidates =
			{
				"/usr/bin/git",
				"/usr/local/bin/git",
				"/opt/homebrew/bin/git",
				"/opt/git/bin/git",
				"/usr/local/git/bin/git",
				"/snap/bin/git"
			};
			return candidates;
		}

		/// <summary>
		/// Migration note：在系统 PATH 里探测可执行文件（Unix bash/sh 配套校验用）。
		/// </summary>
		public static bool ExistsOnPath(string fileName)
		{
			try
			{
				string pathVariable = Environment.GetEnvironmentVariable("PATH") ?? "";
				foreach (string directory in pathVariable.Split(Path.PathSeparator))
				{
					if (string.IsNullOrWhiteSpace(directory))
					{
						continue;
					}
					if (File.Exists(Path.Combine(directory.Trim(), fileName)))
					{
						return true;
					}
				}
			}
			catch
			{
			}
			return false;
		}
	}
}
