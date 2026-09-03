using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace ForkPlus
{
	internal static class FileHelper
	{
		[Flags]
		private enum MoveFileFlags
		{
			None = 0,
			ReplaceExisting = 1,
			CopyAllowed = 2,
			DelayUntilReboot = 4,
			WriteThrough = 8,
			CreateHardlink = 0x10,
			FailIfNotTrackable = 0x20
		}

		public static long? GetFileSize(string filePath)
		{
			try
			{
				FileInfo fileInfo = new FileInfo(filePath);
				if (fileInfo.Exists)
				{
					return fileInfo.Length;
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex.Message);
			}
			return null;
		}

		public static string GetReadableFileSize(long fileSize, bool addSizeInBytes = true)
		{
			string text = FileSizeFormatter.Format(fileSize);
			string text2;
			if (!addSizeInBytes)
			{
				text2 = text;
				if (text2 == null)
				{
					return "";
				}
			}
			else
			{
				text2 = text + " (" + GetReadableFileSizeInBytes(fileSize) + ")";
			}
			return text2;
		}

		public static string GetReadableFileSizeInBytes(long fileSize)
		{
			NumberFormatInfo numberFormatInfo = new NumberFormatInfo();
			numberFormatInfo.NumberGroupSizes = new int[1] { 3 };
			numberFormatInfo.NumberGroupSeparator = ",";
			NumberFormatInfo numberFormatInfo2 = numberFormatInfo;
			return fileSize.ToString("N0", numberFormatInfo2) + " B";
		}

		public static bool AtomicWrite(string filepath, string content)
		{
			for (int i = 0; i < 3; i++)
			{
				try
				{
					WriteFile(filepath, content);
				}
				catch (Exception ex)
				{
					Log.Error($"Failed to write to '{filepath}' {i}", ex);
					continue;
				}
				return true;
			}
			return false;
		}

		public static void OpenInWindowsExplorer(string absolutePath)
		{
			// Migration note：跨平台文件管理器定位。原 Windows 专用 explorer.exe /select 在
			// Unix 上静默失败（Process.Start 抛 Win32Exception 被下方 catch 吞掉）。
			// - macOS: open -R（Reveal in Finder，等价 /select 选中文件）
			// - Linux: xdg-open 父目录（org.freedesktop.FileManager1.ShowItems 依赖桌面环境，
			//   无通用"选中"机制，打开所在目录是稳妥等价物）
			try
			{
				if (File.Exists(absolutePath) || Directory.Exists(absolutePath))
				{
					if (OperatingSystem.IsWindows())
					{
						string arguments = BuildWindowsExplorerArguments(absolutePath, File.Exists(absolutePath));
						Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
					}
					else if (OperatingSystem.IsMacOS())
					{
						Process.Start(new ProcessStartInfo("open", "-R \"" + absolutePath + "\""));
					}
					else
					{
						string target = Directory.Exists(absolutePath) ? absolutePath : Path.GetDirectoryName(absolutePath);
						Process.Start(new ProcessStartInfo("xdg-open", "\"" + target + "\""));
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show file in file manager", ex);
			}
		}

		// 兑现 ShowFileInFileExplorerCommand 迁移注释"Windows 分隔符交给 FileHelper 内部处理"的承诺
		// （此前是空头承诺：git 相对路径恒为正斜杠，Path.Combine 后产生混合分隔符路径，如
		// C:\repo\src/App.cs——.NET 的 File.Exists 接受正斜杠使上方守卫通过，但 explorer.exe
		// 解析不了 /select 里的正斜杠，Windows 会忽略 /select 直接打开"文档"库，即用户报告的
		// "在文件资源管理器中显示，一直是打开文档目录"。WPF 原版在命令层 Replace("/", "\\")，
		// 迁移时误删；等价规范化收敛到本层，且只在 Windows 分支调用——Unix 上反斜杠是合法
		// 文件名字符，不能替换。纯函数抽出供 Linux CI 回归测试（无法执行 Windows 分支本身）。
		internal static string BuildWindowsExplorerArguments(string absolutePath, bool isFile)
		{
			string normalized = absolutePath.Replace('/', '\\');
			// explorer /select 语法要求逗号后紧跟路径，中间不能有空格，否则新版 Windows
			// 会忽略 /select 直接打开"文档"库而非选中目标文件。
			return isFile ? "/select,\"" + normalized + "\"" : normalized;
		}

		private static void WriteFile(string filePath, string content)
		{
			string tempFileName = Path.GetTempFileName();
			using (StreamWriter streamWriter = new StreamWriter(tempFileName))
			{
				streamWriter.Write(content);
			}
			try
			{
				// Migration note：原子写跨平台。原 Windows 专用 MoveFileEx(ReplaceExisting) P/Invoke
				// 在 Linux/macOS 抛 DllNotFoundException（Kernel32.dll 不存在），settings.json
				// 等所有原子写全失败。Unix 用 File.Replace（rename(2) 同语义：原子覆盖）。
				if (OperatingSystem.IsWindows())
				{
					MoveFileEx(tempFileName, filePath, MoveFileFlags.ReplaceExisting | MoveFileFlags.CopyAllowed | MoveFileFlags.WriteThrough);
				}
				else
				{
					if (File.Exists(filePath))
					{
						File.Replace(tempFileName, filePath, null);
					}
					else
					{
						File.Move(tempFileName, filePath);
					}
				}
			}
			catch (Exception)
			{
				File.Delete(tempFileName);
				throw;
			}
		}

		[DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern bool MoveFileEx([In] string lpExistingFileName, [In] string lpNewFileName, [In] MoveFileFlags dwFlags);
	}
}
