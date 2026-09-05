// E2E 测试基建（阶段0，2026-09-05）：临时 git 仓库工厂。
// 为全功能 UI 测试按需构造各形态真实仓库：
//   Basic（多提交多文件） / Branches（本地+远程跟踪分支+tag） / Conflict（合并冲突中）
//   Stash（含 stash 条目） / BareRemote（本地 bare 远程，供 push/pull/fetch 真实执行）
// 所有仓库建在临时目录，调用方 finally 用 Cleanup 删除。
using System;
using System.Diagnostics;
using System.IO;

namespace ForkPlus.Tests
{
	internal static class TestRepoFactory
	{
		/// <summary>基础仓库：4 个提交、2 个文本文件（含未暂存修改）。</summary>
		public static string CreateBasic()
		{
			string root = NewTempDir("basic");
			Init(root);
			Commit(root, "readme.md", "# Test Repo\n", "initial commit");
			Commit(root, "a.txt", "line1\nline2\nline3\n", "add a.txt");
			Commit(root, "src/app.cs", "class App { }\n", "add app.cs");
			Commit(root, "b.txt", "hello world\n", "add b.txt");
			// 未暂存修改（供 working dir diff / stage 测试）
			File.AppendAllText(Path.Combine(root, "a.txt"), "line4-appended\nline5-appended\n");
			return root;
		}

		/// <summary>分支/tag 仓库：main + feature/one + feature/two + 2 个 tag + ahead/behind 状态。</summary>
		public static string CreateBranches()
		{
			string root = NewTempDir("branches");
			Init(root);
			Commit(root, "main.txt", "main v1\n", "c1 on main");
			Run(root, "branch feature/one");
			Run(root, "branch feature/two");
			Commit(root, "main.txt", "main v2\n", "c2 on main");
			Run(root, "tag v1.0");
			Commit(root, "main.txt", "main v3\n", "c3 on main");
			Run(root, "tag v2.0");
			Run(root, "checkout -q feature/one");
			Commit(root, "one.txt", "feature one\n", "c4 on feature/one");
			Run(root, "checkout -q feature/two");
			Commit(root, "two.txt", "feature two\n", "c5 on feature/two");
			Run(root, "checkout -q main");
			return root;
		}

		/// <summary>冲突仓库：两分支各自改同一行，checkout conflict 分支（处于冲突状态需要外部 merge）。</summary>
		public static string CreateConflict()
		{
			string root = NewTempDir("conflict");
			Init(root);
			Commit(root, "conflicted.txt", "base line\n", "base");
			Run(root, "checkout -q -b theirs");
			Commit(root, "conflicted.txt", "their line\n", "theirs change");
			Run(root, "checkout -q main");
			Commit(root, "conflicted.txt", "our line\n", "ours change");
			// 触发冲突（留下 conflicted 状态）
			var psi = GitPsi(root, "merge theirs");
			using var p = Process.Start(psi);
			p.WaitForExit(); // 期望非 0（冲突）
			return root;
		}

		/// <summary>多冲突块仓库（供模块10 冲突块选择测试）：同一文件两个独立区域各产生一个
		/// 冲突块，ours/theirs 两版本各含两处差异。
		/// ⚠️ 冲突块之间必须隔 ≥7 行公共上下文（git 合并的 marker size=7：相邻变更间
		/// 公共行少于该值会被并进同一个冲突 hunk——探针实证 1 行分隔只产生 1 块）。</summary>
		public static string CreateConflictMulti()
		{
			string root = NewTempDir("confmulti");
			Init(root);
			string sep = "sep1\nsep2\nsep3\nsep4\nsep5\nsep6\nsep7\n";
			Commit(root, "multi.txt", "context start\nblock1 base\n" + sep + "block2 base\ncontext end\n", "base");
			Run(root, "checkout -q -b theirs");
			Commit(root, "multi.txt", "context start\nblock1 theirs\n" + sep + "block2 theirs\ncontext end\n", "theirs change");
			Run(root, "checkout -q main");
			Commit(root, "multi.txt", "context start\nblock1 ours\n" + sep + "block2 ours\ncontext end\n", "ours change");
			var psi = GitPsi(root, "merge theirs");
			using var p = Process.Start(psi);
			p.WaitForExit(); // 期望非 0（两块独立冲突）
			return root;
		}

		/// <summary>大文件冲突仓库（供模块10 三方编辑器滚动同步/冲突块导航回归）：
		/// ~150 行 + 3 个分散冲突块（行 12/62/112 附近，Next/Prev 导航有距离）+
		/// 每 20 行插一行 300 字符宽行（AvaloniaEdit 宽度 extent 只由可见行决定——
		/// 宽行分散放置保证垂直滚动过程中水平 extent 不塌缩，水平同步路径可达）。</summary>
		public static string CreateConflictLarge()
		{
			string root = NewTempDir("conflarge");
			Init(root);
			var @base = new System.Text.StringBuilder();
			var ours = new System.Text.StringBuilder();
			var theirs = new System.Text.StringBuilder();
			for (int i = 1; i <= 150; i++)
			{
				string line;
				if (i % 20 == 0)
				{
					line = new string('w', 300); // 宽行（分散，滚动中保持水平 extent）
				}
				else if (i == 12 || i == 62 || i == 112)
				{
					@base.AppendLine("conflict block " + i);
					ours.AppendLine("ours block " + i);
					theirs.AppendLine("theirs block " + i);
					continue;
				}
				else
				{
					line = "context line " + i;
				}
				@base.AppendLine(line);
				ours.AppendLine(line);
				theirs.AppendLine(line);
			}
			Commit(root, "big.txt", @base.ToString(), "base");
			Run(root, "checkout -q -b theirs");
			Commit(root, "big.txt", theirs.ToString(), "theirs change");
			Run(root, "checkout -q main");
			Commit(root, "big.txt", ours.ToString(), "ours change");
			var psi = GitPsi(root, "merge theirs");
			using var p = Process.Start(psi);
			p.WaitForExit(); // 期望非 0（三块冲突）
			return root;
		}

		/// <summary>stash 仓库：2 个 stash 条目。</summary>
		public static string CreateStash()
		{
			string root = NewTempDir("stash");
			Init(root);
			Commit(root, "f.txt", "committed\n", "base");
			File.WriteAllText(Path.Combine(root, "s1.txt"), "stashed 1\n");
			Run(root, "add s1.txt");
			Run(root, "stash push -m stash-one");
			File.WriteAllText(Path.Combine(root, "s2.txt"), "stashed 2\n");
			Run(root, "add s2.txt");
			Run(root, "stash push -m stash-two");
			return root;
		}

		/// <summary>本地 bare 远程 + 克隆工作仓库：work 推到 origin（origin=本地 bare），
		/// 供 push/pull/fetch/多分支推送的真实执行验证（结果落文件系统，无网络）。</summary>
		public static string CreateBareRemote()
		{
			string root = NewTempDir("bare");
			string bare = Path.Combine(root, "remote.git");
			string work = Path.Combine(root, "work");
			Run(root, "init -q -b main --bare " + Quote(bare));
			Run(root, "clone -q " + Quote(bare) + " " + Quote(work));
			// clone 出的 work 需要自己的用户配置（Init 只配了 root）
			Run(work, "config user.email test@example.com");
			Run(work, "config user.name Test");
			Run(work, "config commit.gpgsign false");
			Commit(work, "r.txt", "remote v1\n", "c1");
			Run(work, "push -q origin main");
			// 让 main 领先 origin/main 一个提交（供 push 测试）
			Commit(work, "r2.txt", "remote v2\n", "c2 ahead");
			return work;
		}

		/// <summary>远程落后仓库（供模块15 远程交互）：bare 远程 + work 克隆 + other 克隆。
		/// work 推 c1 后，other 推 c2 到 bare（work 未 fetch）——work 处于 behind 1 状态：
		/// 供 Fetch（fetch 后 origin/main 前进而本地 main 不动）与 Pull（合并后 main 前进）
		/// 的真实执行验证。bare 路径 = &lt;work 同级&gt;/remote.git。</summary>
		public static string CreateRemoteBehind()
		{
			string root = NewTempDir("remotebehind");
			string bare = Path.Combine(root, "remote.git");
			string work = Path.Combine(root, "work");
			Run(root, "init -q -b main --bare " + Quote(bare));
			Run(root, "clone -q " + Quote(bare) + " " + Quote(work));
			Run(work, "config user.email test@example.com");
			Run(work, "config user.name Test");
			Run(work, "config commit.gpgsign false");
			Commit(work, "r.txt", "remote v1\n", "c1");
			Run(work, "push -q origin main");
			// 远端前进：另一克隆推 c2（work 不 fetch——behind 状态由真实 fetch/pull 拉平）
			string other = Path.Combine(root, "other");
			Run(root, "clone -q " + Quote(bare) + " " + Quote(other));
			Run(other, "config user.email test@example.com");
			Run(other, "config user.name Test");
			Run(other, "config commit.gpgsign false");
			Commit(other, "b.txt", "behind change\n", "c2 from other");
			Run(other, "push -q origin main");
			return work;
		}

		/// <summary>远程多分支仓库（供模块11 分支操作测试）：bare 远程 + work 克隆。
		/// main 已推送且领先 1 提交；feature/one、feature/two 仅本地未推送（供多分支推送）；
		/// 远程独有分支 remote-only（bare 直建，work 无同名本地分支——供跟踪远程分支）。</summary>
		public static string CreateRemoteBranches()
		{
			string root = NewTempDir("remotebranches");
			string bare = Path.Combine(root, "remote.git");
			string work = Path.Combine(root, "work");
			Run(root, "init -q -b main --bare " + Quote(bare));
			Run(root, "clone -q " + Quote(bare) + " " + Quote(work));
			// clone 出的 work 需要自己的用户配置（Init 只配了 root）
			Run(work, "config user.email test@example.com");
			Run(work, "config user.name Test");
			Run(work, "config commit.gpgsign false");
			Commit(work, "r.txt", "remote v1\n", "c1");
			Run(work, "push -q origin main");
			// 两个未推送的本地分支（供 PushMultipleBranchesWindow 勾选推送）
			Run(work, "branch feature/one");
			Run(work, "branch feature/two");
			// 远程独有分支：把 main 推到不同名的远程分支（本地无 remote-only 跟踪分支）
			Run(work, "push -q origin main:refs/heads/remote-only");
			// 再造两个远程独有分支（供 RemoveRemoteBranchWindow 多分支列表模式）
			Run(work, "push -q origin main:refs/heads/rb-one main:refs/heads/rb-two");
			Run(work, "fetch -q origin");
			return work;
		}

		/// <summary>标签仓库（供模块12 标签操作测试）：3 提交；c1 → 附注标签 ann-1.0
		/// （tagger=Test/test@example.com，消息 "annotated release one"），c2 → 轻量标签
		/// light-2.0（无 tagger——TagDetailsWindow 走 for-each-ref 回退路径，消息=提交消息
		/// "second commit"，探针实证 2026-09-05），c3 无标签（HEAD）。</summary>
		public static string CreateTags()
		{
			string root = NewTempDir("tags");
			Init(root);
			Commit(root, "a.txt", "a\n", "first commit");
			Run(root, "tag -a ann-1.0 -m " + Quote("annotated release one"));
			Commit(root, "b.txt", "b\n", "second commit");
			Run(root, "tag light-2.0");
			Commit(root, "c.txt", "c\n", "third commit");
			return root;
		}

		/// <summary>远程标签仓库（供模块12 推送/远程删除测试）：bare 远程 + work 克隆，main 已推。
		/// 附注标签 rel-1、rel-2 已推远程（供 RemoveTagWindow 勾选"从远程删除"真实删两端）；
		/// rel-3 / rel-4 / rel-5 仅本地（供 PushTagWindow 单推 / PushMultipleTagsWindow 多推）。</summary>
		public static string CreateRemoteTags()
		{
			string root = NewTempDir("remotetags");
			string bare = Path.Combine(root, "remote.git");
			string work = Path.Combine(root, "work");
			Run(root, "init -q -b main --bare " + Quote(bare));
			Run(root, "clone -q " + Quote(bare) + " " + Quote(work));
			Run(work, "config user.email test@example.com");
			Run(work, "config user.name Test");
			Run(work, "config commit.gpgsign false");
			Commit(work, "r.txt", "remote v1\n", "c1");
			Run(work, "push -q origin main");
			Run(work, "tag -a rel-1 -m " + Quote("release one"));
			Run(work, "tag -a rel-2 -m " + Quote("release two"));
			Run(work, "push -q origin rel-1 rel-2");
			Run(work, "tag -a rel-3 -m " + Quote("release three"));
			Run(work, "tag -a rel-4 -m " + Quote("release four"));
			Run(work, "tag -a rel-5 -m " + Quote("release five"));
			return work;
		}

		/// <summary>二进制 + 图片 diff 仓库：.bin（修改）、两张 png（旧/新）。</summary>
		public static string CreateBinary()
		{
			string root = NewTempDir("binary");
			Init(root);
			byte[] v1 = new byte[256];
			new Random(7).NextBytes(v1);
			File.WriteAllBytes(Path.Combine(root, "data.bin"), v1);
			File.Copy(GetOrMakePng(root, 100), Path.Combine(root, "img.png"));
			Run(root, "add .");
			Run(root, "commit -q -m " + Quote("binary base"));
			byte[] v2 = new byte[512];
			new Random(8).NextBytes(v2);
			File.WriteAllBytes(Path.Combine(root, "data.bin"), v2);
			File.Copy(GetOrMakePng(root, 140), Path.Combine(root, "img.png"), overwrite: true);
			return root;
		}

		/// <summary>工作区仓库：多种未暂存改动（修改 a.txt / 删除 b.txt / 未跟踪 new.txt）
		/// + 1 个已暂存修改（c.txt），供模块5 Commit 视图（暂存/取消暂存/提交）测试。</summary>
		public static string CreateWorkingDir()
		{
			string root = NewTempDir("workdir");
			Init(root);
			Commit(root, "a.txt", "line1\nline2\nline3\n", "base a");
			Commit(root, "b.txt", "keep me\n", "base b");
			Commit(root, "c.txt", "staged content\n", "base c");
			// 未暂存：修改 a.txt（两行追加，供行级 chunk stage 测试）、删除 b.txt、新增未跟踪 new.txt
			File.AppendAllText(Path.Combine(root, "a.txt"), "line4-appended\nline5-appended\n");
			File.Delete(Path.Combine(root, "b.txt"));
			File.WriteAllText(Path.Combine(root, "new.txt"), "untracked\n");
			// 已暂存：c.txt 修改后 add（初始 staged 侧非空，供 unstage 测试）
			File.AppendAllText(Path.Combine(root, "c.txt"), "staged line\n");
			Run(root, "add c.txt");
			return root;
		}

		/// <summary>领先/落后仓库（供模块6 工具栏角标测试）：bare 远程 + 克隆 work。
		/// work 的 main 相对 origin/main：ahead 2（本地两个未推提交）+ behind 1（另一克隆推
		/// 了一个提交后 work 已 fetch，remote-tracking ref 已前进）。UpstreamStatus 基于
		/// 本地 remote-tracking ref 计算，无需网络。</summary>
		public static string CreateAheadBehind()
		{
			string root = NewTempDir("aheadbehind");
			string bare = Path.Combine(root, "remote.git");
			string work = Path.Combine(root, "work");
			Run(root, "init -q -b main --bare " + Quote(bare));
			Run(root, "clone -q " + Quote(bare) + " " + Quote(work));
			Run(work, "config user.email test@example.com");
			Run(work, "config user.name Test");
			Run(work, "config commit.gpgsign false");
			Commit(work, "base.txt", "v1\n", "c1");
			Run(work, "push -q origin main");
			// ahead：本地两个未推提交
			Commit(work, "a1.txt", "a\n", "ahead 1");
			Commit(work, "a2.txt", "a\n", "ahead 2");
			// behind：另一克隆推送一个提交，本仓库 fetch 使 origin/main 前进
			string other = Path.Combine(root, "other");
			Run(root, "clone -q " + Quote(bare) + " " + Quote(other));
			Run(other, "config user.email test@example.com");
			Run(other, "config user.name Test");
			Run(other, "config commit.gpgsign false");
			Commit(other, "b1.txt", "b\n", "behind 1");
			Run(other, "push -q origin main");
			Run(work, "fetch -q origin");
			return work;
		}

		/// <summary>长行仓库（供模块7 文本 Diff 滚动测试）：120 行全部修改（大 hunk，
		/// 垂直滚动范围）+ 400 字符宽行（水平滚动范围），工作区修改使 old/new 两侧各有长行。</summary>
		public static string CreateLongLines()
		{
			string root = NewTempDir("longlines");
			Init(root);
			var baseText = new System.Text.StringBuilder();
			// 宽行放文件开头（探针实证 2026-09-05：AvaloniaEdit TextView 的宽度 extent 只由
			// 可见行决定，宽行在文件底部且被垂直滚动出视野时水平 extent 塌缩到短行宽度，
			// 水平滚动范围消失。放顶部保证初始视口内即有宽行 → 水平 extent ≈3000px）
			baseText.AppendLine(new string('x', 400));
			baseText.AppendLine(new string('x', 400));
			for (int i = 1; i <= 120; i++)
			{
				baseText.AppendLine("short line " + i);
			}
			Commit(root, "wide.txt", baseText.ToString(), "base");
			// 工作区修改：全部行内容变化（大 hunk → 垂直 extent ≈2100px，Commit diff 默认
			// hunk 视图，只改 1 行时 diff 仅 7 行无垂直滚动范围），宽行字符 x→y。
			var modifiedText = new System.Text.StringBuilder();
			modifiedText.AppendLine(new string('y', 400));
			modifiedText.AppendLine(new string('y', 400));
			for (int j = 1; j <= 120; j++)
			{
				modifiedText.AppendLine("modified line " + j);
			}
			File.WriteAllText(Path.Combine(root, "wide.txt"), modifiedText.ToString());
			return root;
		}

		/// <summary>历史改写仓库（供模块13）：main = c1 "base one"(base.txt) + c2 "base two"(b.txt)，
		/// feature 自 c1 分叉 = f1 "feat: one" / f2 "feat: two" / f3 "feat: three"（各加新文件，
		/// 与 main 无重叠 → 合并/变基/拣选均可干净执行）。checkoutFeature=false → main 活跃
		///（合并/拣选/还原/重置）；true → feature 活跃（变基/交互式变基）。</summary>
		public static string CreateHistoryRewrite(bool checkoutFeature = false)
		{
			string root = NewTempDir("histrewrite");
			Init(root);
			Commit(root, "base.txt", "base\n", "base one");
			Run(root, "checkout -q -b feature");
			Commit(root, "f1.txt", "f1\n", "feat: one");
			Commit(root, "f2.txt", "f2\n", "feat: two");
			Commit(root, "f3.txt", "f3\n", "feat: three");
			Run(root, "checkout -q main");
			Commit(root, "b.txt", "b\n", "base two");
			if (checkoutFeature)
			{
				Run(root, "checkout -q feature");
			}
			return root;
		}

		/// <summary>历史改写冲突仓库（供模块13 冲突预检）：main 与 feature 分叉后各自改
		/// conf.txt 同一行 → MergeBranchWindow 构造期 merge-tree 预检报 "will cause conflicts"</summary>
		public static string CreateHistoryConflict()
		{
			string root = NewTempDir("histconflict");
			Init(root);
			Commit(root, "conf.txt", "base line\n", "base");
			Run(root, "checkout -q -b feature");
			Commit(root, "conf.txt", "feature line\n", "feature change");
			Run(root, "checkout -q main");
			Commit(root, "conf.txt", "main line\n", "main change");
			return root;
		}

		/// <summary>Stash 工作区仓库（供模块14 保存/部分保存）：已提交 a.txt("a base\n")/
		/// b.txt("b base\n")，工作区：修改 a.txt + 修改 b.txt + 未跟踪 c.txt ——
		/// SaveStashWindow（全量 + Stage new files）与 CreatePartialStashWindow（多文件勾选）
		/// 测试基底。工作区干净度由各用例自行断言。</summary>
		public static string CreateStashWork()
		{
			string root = NewTempDir("stashwork");
			Init(root);
			Commit(root, "a.txt", "a base\n", "base a");
			Commit(root, "b.txt", "b base\n", "base b");
			File.WriteAllText(Path.Combine(root, "a.txt"), "a modified\n");
			File.WriteAllText(Path.Combine(root, "b.txt"), "b modified\n");
			File.WriteAllText(Path.Combine(root, "c.txt"), "untracked\n");
			return root;
		}

		public static void Cleanup(string root)
		{
			try
			{
				if (Directory.Exists(root))
				{
					Directory.Delete(root, recursive: true);
				}
			}
			catch
			{
				// Windows 句柄延迟释放：忽略（临时目录，OS 会清理）
			}
		}

		/// <summary>执行 git 命令并返回 stdout（测试断言真实仓库/索引状态用，非 UI 层断言）。</summary>
		public static string GitOutput(string root, string args)
		{
			using var p = Process.Start(GitPsi(root, args));
			string output = p.StandardOutput.ReadToEnd();
			string err = p.StandardError.ReadToEnd();
			p.WaitForExit();
			if (p.ExitCode != 0)
			{
				throw new InvalidOperationException("git " + args + " 失败: " + err);
			}
			return output;
		}

		// ============================ 内部工具 ============================

		// 8x8 单色 PNG（无外部依赖；不同 seed 生成不同颜色，供图片 diff 对比）
		private static string GetOrMakePng(string dir, int seed)
		{
			string path = Path.Combine(Path.GetTempPath(), "fp_png_" + seed + ".png");
			if (!File.Exists(path))
			{
				File.WriteAllBytes(path, MakeSolidPng(seed));
			}
			return path;
		}

		/// <summary>图片对比仓库（供模块9 二进制/图片 diff 测试）：360x240 大图（Swipe/OnionSkin
		/// 视觉可辨），committed 版本纯绿、工作区改为纯橙（未暂存修改 → Commit 视图两侧都加载）。
		/// 同尺寸双图 → DiffImageSource（品红差异图）可生成，HighlightPixels 开关路径可达。</summary>
		public static string CreateImageDiff()
		{
			string root = NewTempDir("imgdiff");
			Init(root);
			File.WriteAllBytes(Path.Combine(root, "img.png"), MakePngBytes(360, 240, 40, 150, 80));
			Run(root, "add .");
			Run(root, "commit -q -m " + Quote("image base"));
			File.WriteAllBytes(Path.Combine(root, "img.png"), MakePngBytes(360, 240, 230, 140, 30));
			return root;
		}

		/// <summary>任意尺寸纯色 PNG（RGBA8，无滤波）。MakeSolidPng 只支持 8x8（IHDR 单字节宽高），
		/// 这里宽高用 4 字节大端——供需要大图的视图测试（Swipe 分割线/OnionSkin 透明度可见）。
		/// 块序按 PNG 规范 [length][type][data][CRC]，CRC 覆盖 type+data（探针实证 2026-09-05：
		/// 旧 MakeSolidPng 的 [type][length] 序 + CRC 覆盖 length+data 是错的，Skia 拒绝解码——
		/// "Unable to load bitmap from provided data"，从未暴露是因为没人真解过码）。</summary>
		private static byte[] MakePngBytes(int w, int h, byte r, byte g, byte b)
		{
			byte[] raw = new byte[h * (1 + w * 4)];
			for (int y = 0; y < h; y++)
			{
				int off = y * (1 + w * 4);
				raw[off] = 0;
				for (int x = 0; x < w; x++)
				{
					raw[off + 1 + x * 4] = r;
					raw[off + 2 + x * 4] = g;
					raw[off + 3 + x * 4] = b;
					raw[off + 4 + x * 4] = 255;
				}
			}
			byte[] compressed;
			using (var ms = new MemoryStream())
			{
				ms.WriteByte(0x78);
				ms.WriteByte(0x01);
				using (var ds = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
				{
					ds.Write(raw, 0, raw.Length);
				}
				uint adler = Adler32(raw);
				ms.WriteByte((byte)(adler >> 24));
				ms.WriteByte((byte)(adler >> 16));
				ms.WriteByte((byte)(adler >> 8));
				ms.WriteByte((byte)adler);
				compressed = ms.ToArray();
			}
			using var png = new MemoryStream();
			png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
			// IHDR：[length=13][IHDR][宽4][高4][深度8][色型6][压0][滤0][交0][CRC(type+data)]
			byte[] ihdr = new byte[13 + 12];
			ihdr[0] = 0; ihdr[1] = 0; ihdr[2] = 0; ihdr[3] = 13;
			ihdr[4] = (byte)'I'; ihdr[5] = (byte)'H'; ihdr[6] = (byte)'D'; ihdr[7] = (byte)'R';
			// 宽高 4 字节大端（360x240 超单字节）
			ihdr[8] = (byte)(w >> 24); ihdr[9] = (byte)(w >> 16); ihdr[10] = (byte)(w >> 8); ihdr[11] = (byte)w;
			ihdr[12] = (byte)(h >> 24); ihdr[13] = (byte)(h >> 16); ihdr[14] = (byte)(h >> 8); ihdr[15] = (byte)h;
			ihdr[16] = 8; ihdr[17] = 6; ihdr[18] = 0; ihdr[19] = 0; ihdr[20] = 0;
			uint crc = Crc32(ihdr, 4, 17); // type(4)+data(13)
			ihdr[21] = (byte)(crc >> 24); ihdr[22] = (byte)(crc >> 16); ihdr[23] = (byte)(crc >> 8); ihdr[24] = (byte)crc;
			png.Write(ihdr, 0, 25);
			// IDAT：[length][IDAT][zlib 数据][CRC(type+data)]
			byte[] idat = new byte[compressed.Length + 12];
			int cl = compressed.Length;
			idat[0] = (byte)(cl >> 24); idat[1] = (byte)(cl >> 16); idat[2] = (byte)(cl >> 8); idat[3] = (byte)cl;
			idat[4] = (byte)'I'; idat[5] = (byte)'D'; idat[6] = (byte)'A'; idat[7] = (byte)'T';
			Buffer.BlockCopy(compressed, 0, idat, 8, cl);
			uint crc2 = Crc32(idat, 4, 4 + cl); // type(4)+data(cl)
			idat[8 + cl] = (byte)(crc2 >> 24); idat[9 + cl] = (byte)(crc2 >> 16); idat[10 + cl] = (byte)(crc2 >> 8); idat[11 + cl] = (byte)crc2;
			png.Write(idat, 0, idat.Length);
			byte[] iend = { 0, 0, 0, 0, (byte)'I', (byte)'E', (byte)'N', (byte)'D', 0xAE, 0x42, 0x60, 0x82 };
			png.Write(iend, 0, iend.Length);
			return png.ToArray();
		}

		private static byte[] MakeSolidPng(int channel)
		{
			int w = 8, h = 8;
			byte r = (byte)(channel % 256), g = (byte)((channel * 3) % 256), b = (byte)((channel * 7) % 256);
			byte[] raw = new byte[h * (1 + w * 4)];
			for (int y = 0; y < h; y++)
			{
				int off = y * (1 + w * 4);
				raw[off] = 0;
				for (int x = 0; x < w; x++)
				{
					raw[off + 1 + x * 4] = r;
					raw[off + 2 + x * 4] = g;
					raw[off + 3 + x * 4] = b;
					raw[off + 4 + x * 4] = 255;
				}
			}
			byte[] compressed;
			using (var ms = new MemoryStream())
			{
				ms.WriteByte(0x78);
				ms.WriteByte(0x01);
				using (var ds = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
				{
					ds.Write(raw, 0, raw.Length);
				}
				uint adler = Adler32(raw);
				ms.WriteByte((byte)(adler >> 24));
				ms.WriteByte((byte)(adler >> 16));
				ms.WriteByte((byte)(adler >> 8));
				ms.WriteByte((byte)adler);
				compressed = ms.ToArray();
			}
			using var png = new MemoryStream();
			png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
			// 块序 [length][type][data][CRC]、CRC 覆盖 type+data（与 MakePngBytes 同步修复：
			// 旧实现的 [type][length] 序 + CRC 覆盖 length+data 不符合 PNG 规范，Skia 拒绝解码）
			byte[] ihdr = new byte[13 + 12];
			ihdr[0] = 0; ihdr[1] = 0; ihdr[2] = 0; ihdr[3] = 13;
			ihdr[4] = (byte)'I'; ihdr[5] = (byte)'H'; ihdr[6] = (byte)'D'; ihdr[7] = (byte)'R';
			ihdr[8] = 0; ihdr[9] = 0; ihdr[10] = 0; ihdr[11] = (byte)w;
			ihdr[12] = 0; ihdr[13] = 0; ihdr[14] = 0; ihdr[15] = (byte)h;
			ihdr[16] = 8; ihdr[17] = 6; ihdr[18] = 0; ihdr[19] = 0; ihdr[20] = 0;
			uint crc = Crc32(ihdr, 4, 17);
			ihdr[21] = (byte)(crc >> 24); ihdr[22] = (byte)(crc >> 16); ihdr[23] = (byte)(crc >> 8); ihdr[24] = (byte)crc;
			png.Write(ihdr, 0, 25);
			byte[] idat = new byte[compressed.Length + 12];
			int cl = compressed.Length;
			idat[0] = (byte)(cl >> 24); idat[1] = (byte)(cl >> 16); idat[2] = (byte)(cl >> 8); idat[3] = (byte)cl;
			idat[4] = (byte)'I'; idat[5] = (byte)'D'; idat[6] = (byte)'A'; idat[7] = (byte)'T';
			Buffer.BlockCopy(compressed, 0, idat, 8, cl);
			uint crc2 = Crc32(idat, 4, 4 + cl);
			idat[8 + cl] = (byte)(crc2 >> 24); idat[9 + cl] = (byte)(crc2 >> 16); idat[10 + cl] = (byte)(crc2 >> 8); idat[11 + cl] = (byte)crc2;
			png.Write(idat, 0, idat.Length);
			byte[] iend = { 0, 0, 0, 0, (byte)'I', (byte)'E', (byte)'N', (byte)'D', 0xAE, 0x42, 0x60, 0x82 };
			png.Write(iend, 0, iend.Length);
			return png.ToArray();
		}

		private static uint Adler32(byte[] data)
		{
			uint a = 1, b = 0;
			foreach (byte x in data)
			{
				a = (a + x) % 65521;
				b = (b + a) % 65521;
			}
			return (b << 16) | a;
		}

		private static uint[] _crcTable;

		private static uint Crc32(byte[] data, int offset, int count)
		{
			if (_crcTable == null)
			{
				_crcTable = new uint[256];
				for (int n = 0; n < 256; n++)
				{
					uint c = (uint)n;
					for (int k = 0; k < 8; k++)
					{
						c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
					}
					_crcTable[n] = c;
				}
			}
			uint crc = 0xFFFFFFFFu;
			for (int i = 0; i < count; i++)
			{
				crc = _crcTable[(crc ^ data[offset + i]) & 0xFF] ^ (crc >> 8);
			}
			return crc ^ 0xFFFFFFFFu;
		}

		private static string NewTempDir(string kind)
		{
			string root = Path.Combine(Path.GetTempPath(), "fpe2e_" + kind + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(root);
			return root;
		}

		private static void Init(string root)
		{
			// -b main：显式初始分支（沙箱 git 版本默认 master，测试统一假定 main）
			Run(root, "init -q -b main");
			Run(root, "config user.email test@example.com");
			Run(root, "config user.name Test");
			Run(root, "config commit.gpgsign false");
		}

		private static void Commit(string root, string relPath, string content, string message)
		{
			string full = Path.Combine(root, relPath);
			Directory.CreateDirectory(Path.GetDirectoryName(full));
			File.WriteAllText(full, content);
			Run(root, "add " + Quote(relPath));
			Run(root, "commit -q -m " + Quote(message));
		}

		private static void Run(string cwd, string args)
		{
			using var p = Process.Start(GitPsi(cwd, args));
			string err = p.StandardError.ReadToEnd();
			p.StandardOutput.ReadToEnd();
			p.WaitForExit();
			if (p.ExitCode != 0)
			{
				throw new InvalidOperationException("git " + args + " 失败: " + err);
			}
		}

		private static ProcessStartInfo GitPsi(string cwd, string args)
		{
			return new ProcessStartInfo("git", args)
			{
				WorkingDirectory = cwd,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false
			};
		}

		private static string Quote(string s)
		{
			return "\"" + s + "\"";
		}
	}
}
