using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup;
using Avalonia.Media;
using global::Avalonia.Animation;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.UserControls
{
	public partial class StatusUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private static readonly TimeSpan AnimationDuration = TimeSpan.FromSeconds(0.4);

		private static readonly char[] DirtyWorkingDirectoryMark = new char[1] { '*' };

		private static readonly string BranchFilterOnIconName = "BranchFilterOnIcon";

		private static readonly string BranchFilterOffIconName = "BranchFilterOffIcon";

		private readonly DispatcherTimer _refreshTimer = new DispatcherTimer();

		private bool _hovered;

		private bool _isJobManagerPopopOpen;

		// TODO 迁移：Avalonia NameGenerator 只为 StyledElement 生成 x:Name 字段，
		// TranslateTransform 非 StyledElement，手动声明并从 TitleContainer.RenderTransform 取值。
		internal global::Avalonia.Media.TranslateTransform TitleContainerTranslateTransform;

		[Null]
		private RepositoryUserControl _oldRepositoryUserControl;

		public StatusUserControl()
		{
			InitializeComponent();
			TitleContainerTranslateTransform = (global::Avalonia.Media.TranslateTransform)TitleContainer.RenderTransform;
			ApplyLocalization();
			_refreshTimer.Interval = TimeSpan.FromMilliseconds(200.0);
			_refreshTimer.Tick += _refreshTimer_Tick;
			_refreshTimer.Start();
			ActivityManagerPopup.Opened += delegate
			{
				/* TODO 迁移: PopupAnimation 已删除 */;
				ShowActivityManagerToggleButton.Disable();
				ActivityManagerUserControl.Start();
				_isJobManagerPopopOpen = true;
			};
			ActivityManagerPopup.Closed += delegate
			{
				/* TODO 迁移: PopupAnimation 已删除 */;
				ShowActivityManagerToggleButton.Enable();
				ActivityManagerUserControl.Stop();
				_isJobManagerPopopOpen = false;
			};
			base.PointerEntered += delegate
			{
				_hovered = true;
			};
			base.PointerExited += delegate
			{
				_hovered = false;
			};
		}

		private void _refreshTimer_Tick(object sender, EventArgs e)
		{
			Refresh();
		}

		private void Refresh()
	{
		GitMmUserControl activeGitMmUserControl = MainWindow.Instance?.TabManager.ActiveGitMmUserControl;
		RepositoryUserControl activeRepositoryUserControl = MainWindow.ActiveRepositoryUserControl;

		// v3.11.0：git mm 激活时显示 History / Output 按钮
		bool isGitMmActive = activeGitMmUserControl != null;
		GitMmHistoryButton.IsVisible = isGitMmActive ? true : false;
		GitMmOutputButton.IsVisible = isGitMmActive ? true : false;

		// v3.11.0：git mm 命令执行中——优先显示 busy 状态
		if (isGitMmActive && activeGitMmUserControl.IsGitMmBusy)
		{
			string gitMmTitle = activeRepositoryUserControl?.RepositoryTitle
				?? activeGitMmUserControl.SelectedSubrepoTitle
				?? activeGitMmUserControl.WorkspaceTitle;
			TitleTextBlock.Text = !string.IsNullOrWhiteSpace(gitMmTitle) ? gitMmTitle : activeGitMmUserControl.WorkspaceTitle;
			SecondaryTitleTextBlock.Text = "";
			DescriptionTextBlock.Text = activeGitMmUserControl.GitMmStatusText;
			DescriptionIcon.Hide();
			FilterButton.Collapse();
			BusyIndicator.Show();
			CancelButton.Show();
			StatusProgressBar.IsIndeterminate = true;
			StatusProgressBar.IsVisible = true;
			_oldRepositoryUserControl = null;
			return;
		}
		// 退出 git mm busy 后恢复正常进度条模式
		StatusProgressBar.IsIndeterminate = false;

		if (activeRepositoryUserControl == null)
		{
			if (activeGitMmUserControl != null)
			{
				string gitMmTitle = activeGitMmUserControl.ActiveRepositoryUserControl?.RepositoryTitle;
				TitleTextBlock.Text = !string.IsNullOrWhiteSpace(gitMmTitle) ? gitMmTitle : (activeGitMmUserControl.SelectedSubrepoTitle ?? activeGitMmUserControl.WorkspaceTitle);
				SecondaryTitleTextBlock.Text = "";
				DescriptionTextBlock.Text = GitMmDescription(activeGitMmUserControl, activeGitMmUserControl.ActiveRepositoryUserControl?.GitModule?.Path ?? activeGitMmUserControl.WorkspacePath ?? "");
				CancelButton.Collapse();
				DescriptionIcon.Hide();
				BusyIndicator.Hide();
				StatusProgressBar.Hide();
				FilterButton.Collapse();
				_oldRepositoryUserControl = null;
				return;
			}
			TitleTextBlock.Text = Translate("Welcome to ForkPlus!");
			DescriptionTextBlock.Text = Translate("Open a repository to start");
			CancelButton.Collapse();
			DescriptionIcon.Hide();
			BusyIndicator.Hide();
			StatusProgressBar.Hide();
			FilterButton.Collapse();
			_oldRepositoryUserControl = null;
			return;
		}
			BusyIndicator.Hide(activeRepositoryUserControl.JobQueue.IsIdle);
			SecondaryBusyIndicator.Hide(activeRepositoryUserControl.JobQueue.IsIdle);
			Job primaryJob = activeRepositoryUserControl.JobQueue.PrimaryJob;
			if (primaryJob != null)
			{
				UpdateTitle(activeRepositoryUserControl, primaryJob.Name);
				if (primaryJob.Monitor.IsCanceled)
				{
					CancelButton.Collapse();
					DescriptionTextBlock.Text = ((primaryJob.Status == JobStatus.Finished) ? Translate("Canceled") : Translate("Canceling..."));
				}
				else
				{
					CancelButton.IsVisible = ((primaryJob.Status == JobStatus.Finished) ? false : true);
					DescriptionTextBlock.Text = Translate(primaryJob.Monitor.ProgressMessage ?? "");
				}
				double? progress = primaryJob.Monitor.Progress;
				if (progress.HasValue)
				{
					double valueOrDefault = progress.GetValueOrDefault();
					StatusProgressBar.ShowWithProgress(5.0 + valueOrDefault * 0.95);
				}
				else
				{
					StatusProgressBar.Hide();
				}
				DescriptionIcon.Hide();
				FilterButton.Collapse();
				_oldRepositoryUserControl = activeRepositoryUserControl;
				return;
			}
			CancelButton.Hide();
			StatusProgressBar.Hide();
			string repositoryTitle = activeRepositoryUserControl.RepositoryTitle;
			UpdateTitle(activeRepositoryUserControl, !string.IsNullOrWhiteSpace(repositoryTitle) ? repositoryTitle : activeGitMmUserControl?.SelectedSubrepoTitle);
			RepositoryData repositoryData = activeRepositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				DescriptionTextBlock.Text = Translate("loading...");
				DescriptionIcon.Hide();
				FilterButton.Collapse();
				return;
			}
			LocalBranch activeBranch = repositoryData.References.ActiveBranch;
			if (activeBranch == null)
			{
				DescriptionTextBlock.Text = Translate("detached HEAD");
				DescriptionIcon.Hide();
				return;
			}
			DescriptionIcon.Show();
			DescriptionTextBlock.Text = activeGitMmUserControl != null ? GitMmDescription(activeGitMmUserControl, activeBranch.Name) : activeBranch.Name;
			if (repositoryData.References.FilterReferences.Length != 0)
			{
				FilterButton.Show();
				global::Avalonia.Controls.ToolTip.SetTip(FilterButton,Translate("Clear Branch Filter"));
				FilterButtonImage.SetResourceReference(Image.SourceProperty, BranchFilterOnIconName);
			}
			else if (_hovered && repositoryData.References.ActiveBranch != null)
			{
				FilterButton.Show();
				global::Avalonia.Controls.ToolTip.SetTip(FilterButton,Translate("Filter by Active Branch"));
				FilterButtonImage.SetResourceReference(Image.SourceProperty, BranchFilterOffIconName);
			}
			else
			{
				FilterButton.Collapse();
			}
			_oldRepositoryUserControl = activeRepositoryUserControl;
		}

		private static string GitMmDescription(GitMmUserControl gitMmUserControl, string baseDescription)
		{
			string summary = gitMmUserControl?.StagedDiffSummary;
			if (string.IsNullOrWhiteSpace(summary))
			{
				return baseDescription ?? "";
			}
			return string.IsNullOrWhiteSpace(baseDescription) ? summary : summary + "  " + baseDescription;
		}

		public void ApplyLocalization()
	{
		global::Avalonia.Controls.ToolTip.SetTip(ShowActivityManagerToggleButton,PreferencesLocalization.Translate("Activity Manager", ForkPlusSettings.Default.UiLanguage));
		global::Avalonia.Controls.ToolTip.SetTip(GitMmHistoryButton,PreferencesLocalization.Translate("Command History", ForkPlusSettings.Default.UiLanguage));
		global::Avalonia.Controls.ToolTip.SetTip(GitMmOutputButton,PreferencesLocalization.Translate("Command Output", ForkPlusSettings.Default.UiLanguage));
		ActivityManagerUserControl.ApplyLocalization();
	}

		private void UpdateTitle(RepositoryUserControl repositoryUserControl, string newValue)
	{
		newValue = newValue ?? "";
		// v3.4.1：Job.Name 可能是调用方硬编码的英文（如 "Stage"/"Reset File"/"Delete 'X'"），
		// 这里统一走 Translate 二次翻译，配合 PreferencesLocalization 的字典/模式匹配救回大部分情况。
		newValue = Translate(newValue);
		string currentTitle = TitleTextBlock.Text ?? "";
			string currentSecondaryTitle = SecondaryTitleTextBlock.Text ?? "";
			if (!(currentTitle == newValue) && !(currentSecondaryTitle == newValue))
			{
				if (_oldRepositoryUserControl != repositoryUserControl || currentTitle.TrimEnd(DirtyWorkingDirectoryMark) == newValue.TrimEnd(DirtyWorkingDirectoryMark))
				{
					TitleTextBlock.Text = newValue;
					SecondaryTitleTextBlock.Text = "";
				}
				else if (newValue == repositoryUserControl.RepositoryTitle)
				{
					Grid.SetRow(SecondaryTitleGrid, 0);
					Grid.SetRow(TitleGrid, 1);
					TitleTextBlock.Text = newValue;
					RunAnimation(TitleContainerTranslateTransform, 8.0, -8.0, AnimationDuration, newValue, 1, 0);
				}
				else
				{
					Grid.SetRow(SecondaryTitleGrid, 0);
					Grid.SetRow(TitleGrid, 1);
					SecondaryTitleTextBlock.Text = newValue;
					RunAnimation(TitleContainerTranslateTransform, -8.0, 8.0, AnimationDuration, newValue, 0, 1);
				}
			}
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
	{
		// v3.11.0：git mm 命令执行中时优先取消 git mm job
		GitMmUserControl gitMm = MainWindow.Instance?.TabManager.ActiveGitMmUserControl;
		if (gitMm != null && gitMm.IsGitMmBusy)
		{
			gitMm.CancelGitMmActiveJob();
			return;
		}
		MainWindow.ActiveRepositoryUserControl?.JobQueue.PrimaryJob?.Monitor.Cancel();
	}

	private void FilterButton_Click(object sender, RoutedEventArgs e)
	{
		RepositoryUserControl activeRepositoryUserControl = MainWindow.ActiveRepositoryUserControl;
		if (activeRepositoryUserControl != null)
		{
			RepositoryUserControl.Commands.UpdateReferenceFilter.ToggleActiveBranchFilter(activeRepositoryUserControl);
		}
	}

	/// <summary>v3.11.0：git mm History 按钮——弹出命令历史菜单。</summary>
	private void GitMmHistoryButton_Click(object sender, RoutedEventArgs e)
	{
		GitMmUserControl gitMm = MainWindow.Instance?.TabManager.ActiveGitMmUserControl;
		if (gitMm != null)
		{
			gitMm.ShowGitMmCommandHistory(GitMmHistoryButton);
		}
	}

	/// <summary>v3.11.0：git mm Output 按钮——切换命令输出覆盖层。</summary>
	private void GitMmOutputButton_Click(object sender, RoutedEventArgs e)
	{
		GitMmUserControl gitMm = MainWindow.Instance?.TabManager.ActiveGitMmUserControl;
		if (gitMm != null)
		{
			gitMm.ToggleOutputOverlay();
		}
	}

		private void DescriptionTextBlock_MouseUp(object sender, global::Avalonia.Input.PointerPressedEventArgs e)
		{
			ShowJobManager();
		}

		private void TitleTextBlock_MouseUp(object sender, global::Avalonia.Input.PointerPressedEventArgs e)
		{
			ShowJobManager();
		}

		private void ShowJobManager()
	{
		// v3.11.0：git mm 激活时也允许打开活动管理器
		if (!_isJobManagerPopopOpen && (MainWindow.ActiveRepositoryUserControl != null
			|| MainWindow.Instance?.TabManager.ActiveGitMmUserControl != null))
		{
			ShowActivityManagerToggleButton.IsChecked = !ShowActivityManagerToggleButton.IsChecked;
		}
	}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		public bool RunAnimation(TranslateTransform transform, double from, double to, TimeSpan duration, string newValue, int titleRow, int secondaryTitleRow)
		{
			DoubleAnimation doubleAnimation = new DoubleAnimation(from, to, duration);
			doubleAnimation.EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseOut
			};
			doubleAnimation.Completed += delegate
			{
				SecondaryTitleTextBlock.Text = newValue;
				TitleTextBlock.Text = newValue;
				Grid.SetRow(TitleGrid, titleRow);
				Grid.SetRow(SecondaryTitleGrid, secondaryTitleRow);
			};
			global::ForkPlus.UI.WpfCompat.WpfAnimation.BeginAnimation(transform,TranslateTransform.YProperty,doubleAnimation);
			return true;
		}

	}
}
