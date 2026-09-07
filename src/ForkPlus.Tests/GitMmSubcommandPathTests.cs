using System;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Settings;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// git mm 子命令可见性回归测试（2026-09-07，用户报告：Linux 命令行 `git mm sync` 可用，
	/// GUI 弹 "git: 'mm' is not a git command"，见 "初始化 git mm 仓库" 对话框）。
	///
	/// 根因两层：
	/// ① App.GitMmPath 硬编码查找 git-mm.exe——Unix 上可执行文件无扩展名，永远找不到；
	/// ② git 查找 mm 这类自定义子命令沿"自身 exec-path + 进程 PATH"——GUI 的自带 git 实例
	///    （gitInstance/2.50.1）exec-path 与命令行的系统 git 不同（企业 git-mm 常装系统 git 的
	///    git-core 目录），桌面启动的 GUI 进程 PATH 又可能缺 ~/.local/bin 等用户 bin——两处差异
	///    都会让"命令行可用、GUI 不可用"（沙盒已实证复现）。
	///
	/// 修复：跨平台可执行名 + 系统位置探测（各 git 的 exec-path / 用户 bin）+ GitRequest psi 路径
	/// 注入 PrependGitMmDirectoryToPath（Bt 路径同一 helper）。防线：命名断言 + 注入幂等性 +
	/// 端到端（git-mm 装在 PATH 外目录，`git mm version` 仍成功——修复前此用例红：
	/// git 子进程 PATH 不含该目录，报 not a git command）。
	///
	/// 端到端经 GitMmInstancePath（用户设置，解析链最高优先级）指定 PATH 外的 fake git-mm，
	/// 不污染系统目录——exec-path/用户 bin 探测（GitMmPathFromSystemLocations）是进程级缓存
	/// 且有全局副作用（会让 missing 警告测试失效），无法在共享测试进程里安全验证。
	/// 同属 "HeadlessAvalonia" 集合：与 E2e17WorkflowTests（missing 警告依赖 GitMmPath==null）
	/// 串行，避免全局 ForkPlusSettings 竞态。
	/// </summary>
	[Collection("HeadlessAvalonia")]
	public class GitMmSubcommandPathTests
	{
		// ============================ 1) 跨平台可执行名 ============================

		[Fact]
		public void GitMmExecutableName_IsPlatformCorrect()
		{
			// Unix 无扩展名——原硬编码 "git-mm.exe" 是 GUI 在 Linux 上找不到 git-mm 的根因之一
			Assert.Equal(OperatingSystem.IsWindows() ? "git-mm.exe" : "git-mm", App.GitMmExecutableName);
		}

		// ============================ 2) PATH 注入纯函数 ============================

		[Fact]
		public void PrependGitMmDirectoryToPath_InjectsWhenDirMissing()
		{
			string root = CreateTempRoot();
			try
			{
				string fakeGitMm = WriteFakeGitMmFile(root);
				string saved = ForkPlusSettings.Default.GitMmInstancePath;
				try
				{
					ForkPlusSettings.Default.GitMmInstancePath = fakeGitMm;
					string path = App.PrependGitMmDirectoryToPath("/usr/bin:/bin");
					Assert.True(path != null, "git-mm 目录不在 PATH 中时应注入");
					// 注入目录前置于首段（优先于原有段，防 PATH 里的同名旧版抢先）
					Assert.StartsWith(Path.GetDirectoryName(fakeGitMm) + Path.PathSeparator, path);
					// 原有段保留
					Assert.EndsWith("/usr/bin:/bin", path);
				}
				finally
				{
					ForkPlusSettings.Default.GitMmInstancePath = saved;
				}
			}
			finally
			{
				TryDelete(root);
			}
		}

		[Fact]
		public void PrependGitMmDirectoryToPath_IdempotentWhenDirPresent()
		{
			string root = CreateTempRoot();
			try
			{
				string fakeGitMm = WriteFakeGitMmFile(root);
				string saved = ForkPlusSettings.Default.GitMmInstancePath;
				try
				{
					ForkPlusSettings.Default.GitMmInstancePath = fakeGitMm;
					string gitMmDir = Path.GetDirectoryName(fakeGitMm);
					// 目录已在 PATH（含非首段）→ 无需注入，返回 null
					Assert.Null(App.PrependGitMmDirectoryToPath("/usr/bin" + Path.PathSeparator + gitMmDir + Path.PathSeparator + "/bin"));
					Assert.Null(App.PrependGitMmDirectoryToPath(gitMmDir));
					// 段级比较：目录名互为子串不误判为已包含
					string siblingDir = gitMmDir + "-suffix";
					string injected = App.PrependGitMmDirectoryToPath(siblingDir);
					Assert.True(injected != null, "仅子串相同（非同段）时应注入");
					Assert.StartsWith(gitMmDir + Path.PathSeparator, injected);
				}
				finally
				{
					ForkPlusSettings.Default.GitMmInstancePath = saved;
				}
			}
			finally
			{
				TryDelete(root);
			}
		}

		[Fact]
		public void PrependGitMmDirectoryToPath_EmptyPathGetsDirOnly()
		{
			string root = CreateTempRoot();
			try
			{
				string fakeGitMm = WriteFakeGitMmFile(root);
				string saved = ForkPlusSettings.Default.GitMmInstancePath;
				try
				{
					ForkPlusSettings.Default.GitMmInstancePath = fakeGitMm;
					// 空/缺失 PATH（极端桌面环境）→ 只剩注入目录本身
					Assert.Equal(Path.GetDirectoryName(fakeGitMm), App.PrependGitMmDirectoryToPath(null));
					Assert.Equal(Path.GetDirectoryName(fakeGitMm), App.PrependGitMmDirectoryToPath(""));
				}
				finally
				{
					ForkPlusSettings.Default.GitMmInstancePath = saved;
				}
			}
			finally
			{
				TryDelete(root);
			}
		}

		// ============================ 3) 端到端：git mm 经注入的 PATH 找到 git-mm ============================

		[Fact]
		public void GitRequest_Execute_GitMm_FindsExecutableOutsidePath()
		{
			// fake git-mm 是 sh 脚本，仅 Unix 可执行（与 E2e17 MakeFakeGitMm 同口径；CI 为 ubuntu）
			if (OperatingSystem.IsWindows())
			{
				return;
			}
			Assert.True(File.Exists(App.GitPath), "环境前置：App.GitPath 应存在（gitInstance）");

			string root = CreateTempRoot();
			try
			{
				// fake git-mm 装在测试进程 PATH 之外的目录——模拟用户场景
				// （系统 git exec-path / 用户 bin：命令行 git 找得到，GUI 的 git 实例 + 进程 PATH 找不到）
				string fakeGitMm = WriteFakeGitMmFile(root);
				string saved = ForkPlusSettings.Default.GitMmInstancePath;
				try
				{
					ForkPlusSettings.Default.GitMmInstancePath = fakeGitMm;
					GitRequestResult result = default(GitRequest)
						.Command(new GitCommand("mm", "version"))
						.CurrentDir(root)
						.Execute();
					// 修复前：git 子进程 PATH 不含 fake 目录 → "git: 'mm' is not a git command"
					Assert.True(result.Success, "git mm 应经注入的 PATH 找到 git-mm。ExitCode=" + result.ExitCode + " Stderr: " + result.Stderr);
					Assert.Contains("git-mm version 3.2.0", result.Stdout);
				}
				finally
				{
					ForkPlusSettings.Default.GitMmInstancePath = saved;
				}
			}
			finally
			{
				TryDelete(root);
			}
		}

		// ============================ helpers ============================

		private static string CreateTempRoot()
		{
			string root = Path.Combine(Path.GetTempPath(), "fpgitmmpath_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(root);
			return root;
		}

		/// <summary>写一个仅输出版本号的 fake git-mm（Unix 上 chmod +x——端到端用例里 git 要真正执行它；
		/// Windows 无 chmod，纯函数用例只查存在性，端到端用例已平台守卫跳过）。</summary>
		private static string WriteFakeGitMmFile(string root)
		{
			string path = Path.Combine(root, App.GitMmExecutableName);
			File.WriteAllText(path, "#!/bin/sh\necho \"git-mm version 3.2.0\"\n");
			if (!OperatingSystem.IsWindows())
			{
				System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("chmod", "+x " + path)).WaitForExit();
			}
			return path;
		}

		private static void TryDelete(string root)
		{
			try
			{
				Directory.Delete(root, recursive: true);
			}
			catch
			{
			}
		}
	}
}
