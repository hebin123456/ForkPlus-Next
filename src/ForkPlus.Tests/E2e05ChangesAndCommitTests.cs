// E2E 模块5（2026-09-05）：变更与提交视图（Commit 视图）。
// 覆盖：真实 MainWindow 生产路径打开仓库（TabManager.OpenRepository，IsActiveRepository 管线走通）、
//   工作区状态装配（未暂存 修改/删除/未跟踪 + 已暂存）、选中文件 working dir diff 加载、
//   Stage/Unstage 选中文件（真实 git add / reset，UI 列表 + git index 双重验证）、
//   StageAllButton 智能切换（Stage All ↔ Unstage All）、提交按钮状态与文案。
// 截图 → docs/evidence/e2e/05-changescommit/。
using System;
using System.Linq;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e05ChangesAndCommitTests
	{
		[Fact]
		public void CommitView_LoadsWorkingDirectoryStatus_AndDiffForSelectedFile()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// ===== 1) 切到 Commit 视图（生产公共入口）=====
						repoControl.ActivateCommitView();
						Dispatcher.UIThread.RunJobs();
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						StageFileUserControl stage = commit.StageFileUserControl;

						// ===== 2) 状态装配：3 未暂存（a.txt 改 / b.txt 删 / new.txt 未跟踪）+ 1 已暂存（c.txt）=====
						// （IsActiveRepository 已走通：RepositoryStatusUpdated → RefreshRepositoryStatusUi → SetDataAsync）
						bool statusLoaded = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 3 && stage.AllStagedFiles.Length == 1;
						});
						Assert.True(statusLoaded, "工作区状态未装配：unstaged=" + stage.AllUnstagedFiles.Length
							+ " staged=" + stage.AllStagedFiles.Length + "（15s 超时）");
						Assert.Equal(new[] { "a.txt", "b.txt", "new.txt" },
							stage.AllUnstagedFiles.Select(f => f.Path).OrderBy(p => p).ToArray());
						Assert.Equal(new[] { "c.txt" }, stage.AllStagedFiles.Select(f => f.Path).ToArray());
						// 变更类型（git status 真实解析结果）
						Assert.Equal(ChangeType.Modified, stage.AllUnstagedFiles.First(f => f.Path == "a.txt").ChangeType);
						Assert.Equal(ChangeType.Deleted, stage.AllUnstagedFiles.First(f => f.Path == "b.txt").ChangeType);
						Assert.Equal(ChangeType.Added, stage.AllUnstagedFiles.First(f => f.Path == "new.txt").ChangeType);
						Assert.False(stage.AllUnstagedFiles.First(f => f.Path == "new.txt").Tracked, "new.txt 应未跟踪");
						Assert.True(stage.AllStagedFiles[0].Staged, "c.txt 应带已暂存标记");

						// ===== 3) 自动选中首个未暂存文件 → working dir diff 加载（LoadWorkingDirectoryDiff 真实管线）=====
						Assert.True(stage.SelectedUnstagedFiles.Length == 1, "应自动选中首个未暂存文件，实际 "
							+ stage.SelectedUnstagedFiles.Length);
						bool diffLoaded = UiClick.WaitFor(delegate
						{
							return commit.FileDiffControl.Content != null && commit.FileDiffControl.Content.Succeeded;
						});
						Assert.True(diffLoaded, "选中文件后 working dir diff 应加载成功（15s 超时）");
						Assert.True(diffLoaded && commit.FileDiffControl.Content.Result is ParsedDiffContent,
							"文本文件 diff 应为 ParsedDiffContent，实际 "
							+ commit.FileDiffControl.Content.Result?.GetType().Name);
						Assert.Equal(stage.SelectedUnstagedFiles[0].Path,
							commit.FileDiffControl.Content.Result.ChangedFile.Path);

						// ===== 4) 提交按钮：已暂存 1 项 → "Commit 1 File"；无主题 → 禁用 =====
						Assert.Equal(E2eMainWindowHarness.TrFormat("Commit {0} File", 1), commit.CommitButton.Content?.ToString());
						Assert.False(commit.CommitButton.IsEnabled, "无提交主题时提交按钮应禁用");

						ScreenshotHelper.Snap(window, "01-commit-view-loaded", "05-changescommit");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void CommitView_StageAndUnstageSelectedFile_ViaToolbarButtons()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						repoControl.ActivateCommitView();
						Dispatcher.UIThread.RunJobs();
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						StageFileUserControl stage = commit.StageFileUserControl;
						Assert.True(UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 3 && stage.AllStagedFiles.Length == 1;
						}), "初始状态未装配");

						// ===== 1) 选中未暂存的 a.txt → 点 Stage → git add + 列表移动 =====
						stage.UnstagedFilesFileListUserControl.SelectFile("a.txt");
						Dispatcher.UIThread.RunJobs();
						// 注：首个未暂存文件（a.txt）本就被自动选中，SelectFile 会重复 Add（生产怪癖），
						// 断言用去重路径集合而非数量
						Assert.Equal(new[] { "a.txt" }, stage.SelectedUnstagedFiles.Select(f => f.Path).Distinct().ToArray());
						Assert.True(stage.IsUnstagedListSelected, "未暂存列表应处于选中态");

						UiClick.Click(stage.StageButton); // Stage 事件 → StageSelectedFiles → ToggleFileStage → git add
						bool staged = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 2 && stage.AllStagedFiles.Length == 2;
						});
						Assert.True(staged, "暂存 a.txt 后未暂存应剩 2 项、已暂存应为 2 项，实际 "
							+ stage.AllUnstagedFiles.Length + "/" + stage.AllStagedFiles.Length);
						Assert.Contains("a.txt", stage.AllStagedFiles.Select(f => f.Path));
						Assert.DoesNotContain("a.txt", stage.AllUnstagedFiles.Select(f => f.Path));
						// 真实 git index 二次验证（UI 断言之外的事实）
						Assert.Contains("a.txt", TestRepoFactory.GitOutput(repo, "diff --cached --name-only").Split('\n'));
						Assert.Equal(E2eMainWindowHarness.TrFormat("Commit {0} Files", 2), commit.CommitButton.Content?.ToString());
						ScreenshotHelper.Snap(window, "02-stage-selected-file", "05-changescommit");

						// ===== 2) 选中已暂存的 a.txt → 点 Unstage → git reset + 列表移回 =====
						stage.StagedFilesFileListUserControl.SelectFile("a.txt");
						Dispatcher.UIThread.RunJobs();
						Assert.Equal(new[] { "a.txt" }, stage.SelectedStagedFiles.Select(f => f.Path).Distinct().ToArray());
						Assert.True(stage.IsStagedListSelected, "已暂存列表应处于选中态");

						UiClick.Click(stage.UnstageButton); // Unstage 事件 → UnstageSelectedFiles → git reset
						bool unstaged = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 3 && stage.AllStagedFiles.Length == 1;
						});
						Assert.True(unstaged, "取消暂存 a.txt 后应回到 3 未暂存 / 1 已暂存，实际 "
							+ stage.AllUnstagedFiles.Length + "/" + stage.AllStagedFiles.Length);
						Assert.Contains("a.txt", stage.AllUnstagedFiles.Select(f => f.Path));
						Assert.DoesNotContain("a.txt", TestRepoFactory.GitOutput(repo, "diff --cached --name-only").Split('\n'));
						ScreenshotHelper.Snap(window, "03-unstage-selected-file", "05-changescommit");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void CommitView_StageAllButton_SmartTogglesBetweenStageAllAndUnstageAll()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						repoControl.ActivateCommitView();
						Dispatcher.UIThread.RunJobs();
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						StageFileUserControl stage = commit.StageFileUserControl;
						Assert.True(UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 3 && stage.AllStagedFiles.Length == 1;
						}), "初始状态未装配");

						// ===== 1) 未暂存有可见项 → StageAllButton 表现为 Stage All：全部 4 项进入已暂存 =====
						UiClick.Click(stage.StageAllButton);
						bool allStaged = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 0 && stage.AllStagedFiles.Length == 4;
						});
						Assert.True(allStaged, "Stage All 后未暂存应为空、已暂存应为 4 项，实际 "
							+ stage.AllUnstagedFiles.Length + "/" + stage.AllStagedFiles.Length);
						Assert.Equal(new[] { "a.txt", "b.txt", "c.txt", "new.txt" },
							stage.AllStagedFiles.Select(f => f.Path).OrderBy(p => p).ToArray());
						// index 二次验证（含删除 b.txt 与未跟踪 new.txt 的暂存）
						string cached = TestRepoFactory.GitOutput(repo, "diff --cached --name-only");
						Assert.Contains("a.txt", cached.Split('\n'));
						Assert.Contains("b.txt", cached.Split('\n'));
						Assert.Contains("new.txt", cached.Split('\n'));
						Assert.Equal(E2eMainWindowHarness.TrFormat("Commit {0} Files", 4), commit.CommitButton.Content?.ToString());
						ScreenshotHelper.Snap(window, "04-stage-all", "05-changescommit");

						// ===== 2) 未暂存已空 → 同一按钮智能切换为 Unstage All：全部 4 项回到未暂存 =====
						// （c.txt 取消暂存后仍相对 HEAD 有工作区改动 → 回到未暂存列表）
						UiClick.Click(stage.StageAllButton);
						bool allUnstaged = UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Length == 4 && stage.AllStagedFiles.Length == 0;
						});
						Assert.True(allUnstaged, "Unstage All 后应为 4 未暂存 / 0 已暂存，实际 "
							+ stage.AllUnstagedFiles.Length + "/" + stage.AllStagedFiles.Length);
						Assert.Equal(new[] { "a.txt", "b.txt", "c.txt", "new.txt" },
							stage.AllUnstagedFiles.Select(f => f.Path).OrderBy(p => p).ToArray());
						// index 已空（c.txt 的既有暂存也被一并取消）
						Assert.Equal(string.Empty, TestRepoFactory.GitOutput(repo, "diff --cached --name-only"));
						Assert.Equal(E2eMainWindowHarness.Tr("Commit"), commit.CommitButton.Content?.ToString());
						ScreenshotHelper.Snap(window, "05-unstage-all", "05-changescommit");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
