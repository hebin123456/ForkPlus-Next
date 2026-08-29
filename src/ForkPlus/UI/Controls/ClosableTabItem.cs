using System;
using ForkPlus.UI.WpfCompat;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class ClosableTabItem : TabItem
	{
		private const string CloseButton = "PART_Close";

		private const string TabHeader = "PART_Header";

		private const string TitleTextBlockName = "PART_Title";

		private const string RepositoryManagerTabHeader = "Repository Manager";

		public static readonly SolidColorBrush IsDirtyDefaultBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E8E91"));

		public static readonly global::Avalonia.StyledProperty<SolidColorBrush> TagBrushProperty =
    global::Avalonia.AvaloniaProperty.Register<ClosableTabItem, SolidColorBrush>("TagBrush");

		public static readonly global::Avalonia.StyledProperty<bool> IsDirtyProperty =
    global::Avalonia.AvaloniaProperty.Register<ClosableTabItem, bool>("IsDirty", false);

		private Point _dragStartPoint;

		public TabItemMode Mode { get; private set; }

		[Null]
		public RepositoryManagerUserControl RepositoryManagerUserControl { get; private set; }

		[Null]
		public RepositoryUserControl RepositoryUserControl { get; private set; }

		[Null]
		public GitMmUserControl GitMmUserControl { get; private set; }

		public SolidColorBrush TagBrush
		{
			get
			{
				return (SolidColorBrush)GetValue(TagBrushProperty);
			}
			set
			{
				SetValue(TagBrushProperty, value);
			}
		}

		public bool IsDirty
		{
			get
			{
				return (bool)GetValue(IsDirtyProperty);
			}
			set
			{
				SetValue(IsDirtyProperty, value);
			}
		}

		private EditableTextBlock TitleTextBlock => this.GetTemplateChild("PART_Title") as EditableTextBlock;

		public ClosableTabItem()
		{
			base.PointerPressed += TabItem_PreviewMouseDown;
			base.PointerMoved += TabItem_PreviewMouseMove;
			// TODO 迁移：WPF UIElement.Drop += handler（实例 CLR 事件）在 Avalonia 12 的 TabItem 上
			// 不存在（CS0117）；等价写法是 AddHandler(DragDrop.DropEvent, handler)（Interactive 路由
			// 事件订阅，默认 Direct|Bubble，与 WPF Drop 冒泡行为一致）。
			this.AddHandler(global::Avalonia.Input.DragDrop.DropEvent, TabItem_Drop);
			WeakEventManager<NotificationCenter, EventArgs<RepositoryUserControl>>.AddHandler(NotificationCenter.Current, "RepositoryUserControlTitleChanged", RepositoryUserControlTitleChanged);
			WeakEventManager<NotificationCenter, EventArgs<RepositoryUserControl>>.AddHandler(NotificationCenter.Current, "RepositoryUserControlColorChanged", RepositoryUserControlColorChanged);
			WeakEventManager<NotificationCenter, EventArgs<RepositoryUserControl>>.AddHandler(NotificationCenter.Current, "RepositoryUserControlIsDirtyChanged", RepositoryUserControlIsDirtyChanged);
			WeakEventManager<NotificationCenter, EventArgs<RepositoryManager.Repository>>.AddHandler(NotificationCenter.Current, "RepositoryColorChanged", RepositoryColorChanged);
		}

		public void Close()
		{
			(base.Parent as ClosableTabControl)?.RemoveTab(this);
		}

		protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			if (this.GetTemplateChild("PART_Close") is Button button)
			{
				button.Click += delegate
				{
					Close();
				};
			}
			if (!(this.GetTemplateChild("PART_Header") is CenteredDockPanel centeredDockPanel))
			{
				return;
			}
			centeredDockPanel.PointerPressed += delegate(object s, global::Avalonia.Input.PointerPressedEventArgs e)
			{
				// TODO 迁移：WPF e.MiddleButton == MouseButtonState.Pressed（查鼠标中键状态）在
				// Avalonia 的 PointerPressedEventArgs 上不存在（CS1061）；等价物是当前指针点位的
				// PointerPointProperties.IsMiddleButtonPressed。
				if (e.GetCurrentPoint(null).Properties.IsMiddleButtonPressed)
				{
					Close();
				}
			};
			global::Avalonia.Controls.ToolTip.SetTip(centeredDockPanel,GetToolTip());
			centeredDockPanel.ContextMenu = GetContextMenu();
		}

		public void ActivateRepositoryManagerMode()
		{
			RepositoryUserControl = null;
			GitMmUserControl = null;
			RepositoryManagerUserControl = new RepositoryManagerUserControl();
			Mode = TabItemMode.RepositoryManager;
			VisualTreeAttachmentHelper.TrySetContent(this, RepositoryManagerUserControl, GetType().Name + ".Content");
		}

		public void ActivateRepositoryViewMode(GitModule gitModule)
		{
			RepositoryManagerUserControl = null;
			GitMmUserControl = null;
			RepositoryUserControl = new RepositoryUserControl();
			RepositoryUserControl.OpenRepository(gitModule);
			Mode = TabItemMode.Repository;
			VisualTreeAttachmentHelper.TrySetContent(this, RepositoryUserControl, GetType().Name + ".Content");
			TagBrush = RepositoryColorsUserControl.GetBrush(RepositoryUserControl.RepositoryColor);
			IsDirty = RepositoryUserControl.IsDirty;
		}

		public void ActivateGitMmMode(string workspacePath)
		{
			RepositoryManagerUserControl = null;
			RepositoryUserControl = null;
			GitMmUserControl = new GitMmUserControl(workspacePath);
			Mode = TabItemMode.GitMm;
			VisualTreeAttachmentHelper.TrySetContent(this, GitMmUserControl, GetType().Name + ".Content");
			TagBrush = RepositoryColorsUserControl.GetBrush(RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path == PathHelper.Normalize(workspacePath))?.Color ?? RepositoryColor.None);
			IsDirty = false;
		}

		public void Refresh()
		{
			if (Mode == TabItemMode.Repository)
			{
				RepositoryUserControl repositoryUserControl = RepositoryUserControl;
				if (repositoryUserControl != null)
				{
					if (repositoryUserControl.ViewMode == RepositoryViewMode.CommitViewMode)
					{
						repositoryUserControl.InvalidateAndRefresh(SubDomain.DefaultRefresh, null, RepositoryViewMode.CommitViewMode);
					}
					else
					{
						repositoryUserControl.InvalidateAndRefresh(SubDomain.DefaultRefresh);
					}
					return;
				}
			}
			if (Mode == TabItemMode.RepositoryManager)
			{
				RepositoryManagerUserControl?.Refresh();
			}
			if (Mode == TabItemMode.GitMm)
			{
				GitMmUserControl?.Refresh();
			}
		}

		private void TabItem_PreviewMouseDown(object sender, global::Avalonia.Input.PointerPressedEventArgs e)
		{
			_dragStartPoint = e.GetPosition(null);
		}

		private void TabItem_PreviewMouseMove(object sender, global::Avalonia.Input.PointerEventArgs e)
		{
			// TODO 迁移：WPF Mouse.PrimaryDevice.LeftButton（全局查询鼠标左键状态）在 Avalonia 无
			// 全局鼠标状态 API（CS0117）；拖动语义等价物 = 当前 PointerMoved 事件指针点位的
			// IsLeftButtonPressed（按下并移动才会走到这里，行为一致）。
			if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed && CursorReachedDropDistance(e.GetPosition(null)) && !(e.Source is Button) && e.Source is ClosableTabItem closableTabItem)
			{
				global::ForkPlus.UI.WpfCompat.DragDropLauncher.DoDragDrop(closableTabItem, new WeakReference<ClosableTabItem>(closableTabItem), (global::Avalonia.Input.DragDropEffects)7);
			}
		}

		private void TabItem_Drop(object sender, DragEventArgs e)
		{
			// TODO 迁移：WPF DataObject.GetData(Type) 以类型全名作隐式格式名；Avalonia 侧的
			// WpfDataObject 只有 GetData(string)（CS1503），且 DragDropLauncher.DoDragDrop 把自定义
			// 对象统一存为 "ForkPlusItem" 格式（WpfCompat.Batch2.cs ToTransfer 默认分支），
			// 故按该格式名读取，读取结果仍以类型模式匹配校验，语义等价。
			if (e.WpfData().GetData("ForkPlusItem") is WeakReference<ClosableTabItem> weakReference && weakReference.TryGetTarget(out var target) && e.Source is ClosableTabItem closableTabItem)
			{
				ClosableTabControl closableTabControl = closableTabItem.Parent as ClosableTabControl;
				if (closableTabItem != target)
				{
					closableTabControl.StopSelectionChangedEventWhileDropInProgress = true;
					int num = closableTabControl.Items.IndexOf(closableTabItem);
					ClosableTabItem closableTabItem2 = new ClosableTabItem();
					closableTabControl.Items.Insert(0, closableTabItem2);
					closableTabControl.SelectTab(closableTabItem2);
					closableTabControl.Items.Remove(target);
					closableTabControl.Items.Insert(num + 1, target);
					closableTabControl.Items.Remove(closableTabItem2);
					closableTabControl.StopSelectionChangedEventWhileDropInProgress = false;
					closableTabControl.SelectTab(target);
				}
			}
		}

		private void RenameRepository(string newName)
		{
			string text = RepositoryUserControl?.GitModule.Path;
			if (text != null)
			{
				RenameRepository(text, newName);
			}
		}

		private void RenameRepository(string repositoryPath, string newName)
		{
			EnsureRepositoryManagerEntry(repositoryPath);
			string normalizedPath = PathHelper.Normalize(repositoryPath);
			RepositoryManager.Instance.RenameRepository(normalizedPath, newName);
			NotificationCenter.Current.RaiseRepositoryNameChanged(this, normalizedPath);
			RepositoryManager.Instance.Save();
			RefreshTitle();
		}

		public void RefreshTitle()
		{
			if (Mode == TabItemMode.Repository)
			{
				RepositoryUserControl.RefreshRepositoryTitle();
				base.Header = RepositoryUserControl.RepositoryTitle;
			}
			else if (Mode == TabItemMode.GitMm)
			{
				base.Header = GitMmUserControl?.WorkspaceTitle ?? "git mm";
			}
			else
			{
				base.Header = PreferencesLocalization.Translate("Repository Manager", ForkPlusSettings.Default.UiLanguage);
			}
		}

		[Null]
		private string GetToolTip()
		{
			if (Mode == TabItemMode.Repository)
			{
				return RepositoryUserControl?.GitModule.Path;
			}
			if (Mode == TabItemMode.GitMm)
			{
				return GitMmUserControl?.WorkspacePath;
			}
			return null;
		}

		private void RepositoryUserControlTitleChanged(object sender, EventArgs<RepositoryUserControl> e)
		{
			RefreshTitle();
		}

		private void RepositoryUserControlIsDirtyChanged(object sender, EventArgs<RepositoryUserControl> e)
		{
			if (e.Value == RepositoryUserControl)
			{
				IsDirty = RepositoryUserControl.IsDirty;
			}
		}

		private void RepositoryUserControlColorChanged(object sender, EventArgs<RepositoryUserControl> e)
		{
			if (e.Value == RepositoryUserControl)
			{
				TagBrush = RepositoryColorsUserControl.GetBrush(RepositoryUserControl.RepositoryColor);
			}
		}

		private void RepositoryColorChanged(object sender, EventArgs<RepositoryManager.Repository> e)
		{
			if (Mode == TabItemMode.GitMm && GitMmUserControl != null && e.Value.Path == PathHelper.Normalize(GitMmUserControl.WorkspacePath))
			{
				RepositoryManager.Repository? repository = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path == e.Value.Path);
				TagBrush = RepositoryColorsUserControl.GetBrush(repository?.Color ?? RepositoryColor.None);
			}
		}

		private ContextMenu GetContextMenu()
		{
			ContextMenu contextMenu = new ContextMenu();
			MenuItem menuItem = new MenuItem();
			menuItem.Header = PreferencesLocalization.MenuHeader("Close All");
			menuItem.Click += delegate
			{
				((ClosableTabControl)base.Parent).RemoveAllTabs();
			};
			contextMenu.Items.Add(menuItem);
			MenuItem menuItem2 = new MenuItem();
			menuItem2.Header = PreferencesLocalization.MenuHeader("Close All But This");
			menuItem2.Click += delegate
			{
				((ClosableTabControl)base.Parent).RemoveAllTabs(this);
			};
			contextMenu.Items.Add(menuItem2);
			string managedRepositoryPath = null;
			string managedRepositoryName = null;
			if (Mode == TabItemMode.Repository)
			{
				RepositoryUserControl repositoryUserControl = RepositoryUserControl;
				if (repositoryUserControl != null && repositoryUserControl.GitModule.Type != ModuleType.Submodule && repositoryUserControl.GitModule.Type != ModuleType.Worktree)
				{
					managedRepositoryPath = repositoryUserControl.GitModule.Path;
					managedRepositoryName = repositoryUserControl.RepositoryName;
				}
			}
			else if (Mode == TabItemMode.GitMm && GitMmUserControl != null)
			{
				managedRepositoryPath = GitMmUserControl.WorkspacePath;
				managedRepositoryName = RepositoryManager.Instance.FindRepositoryName(managedRepositoryPath) ?? GitMmUserControl.WorkspaceTitle.Replace("git mm: ", "");
			}
			if (managedRepositoryPath != null)
			{
				contextMenu.Items.Add(new Separator());
				MenuItem menuItem3 = new MenuItem();
				menuItem3.Header = PreferencesLocalization.MenuHeader("Rename");
				menuItem3.Click += delegate
				{
					EditableTextBlock editableTextBlock = TitleTextBlock;
					if (editableTextBlock != null)
					{
						editableTextBlock.ShowEditor(GetCurrentRepositoryName(managedRepositoryPath, managedRepositoryName), delegate(bool success, string newName)
						{
							editableTextBlock.HideEditor();
							if (success)
							{
								RenameRepository(managedRepositoryPath, newName);
							}
						}, centeredHorizontally: true);
					}
				};
				contextMenu.Items.Add(menuItem3);
				if (ForkPlusSettings.Default.Workspaces.All.Length != 0)
				{
					contextMenu.Items.Add(new Separator());
					MenuItem menuItem4 = new MenuItem
					{
						Header = PreferencesLocalization.MenuHeader("Workspaces")
					};
					Workspace[] all = ForkPlusSettings.Default.Workspaces.All;
					foreach (Workspace workspace in all)
					{
						bool isCurrentWorkspace = workspace == ForkPlusSettings.Default.Workspaces.ActiveWorkspace;
						MenuItem menuItem5 = new MenuItem();
						menuItem5.Header = workspace.Name;
						menuItem5.IsChecked = isCurrentWorkspace;
						menuItem5.Click += delegate
						{
							if (!isCurrentWorkspace)
							{
								AddRepositoryToWorkspace(workspace, managedRepositoryPath);
								Close();
							}
						};
						menuItem4.Items.Add(menuItem5);
					}
					contextMenu.Items.Add(menuItem4);
				}
				contextMenu.Items.Add(new Separator());
				RepositoryManager.Repository? repository = EnsureRepositoryManagerEntry(managedRepositoryPath);
				if (repository.HasValue)
				{
					contextMenu.Items.Add(CreateRepositoryColorsMenuItem(repository.GetValueOrDefault()));
				}
			}
			// 若该仓是某个 git mm 工作区的子仓，提供“打开 git mm 仓”快捷入口
			if (Mode == TabItemMode.Repository && !string.IsNullOrWhiteSpace(managedRepositoryPath))
			{
				string gitMmWorkspacePath = MainWindow.Instance?.TabManager?.FindGitMmWorkspacePathForSubrepo(managedRepositoryPath);
				if (!string.IsNullOrWhiteSpace(gitMmWorkspacePath))
				{
					contextMenu.Items.Add(new Separator());
					MenuItem openGitMmItem = new MenuItem();
					openGitMmItem.Header = PreferencesLocalization.MenuHeader("Open git mm Repository");
					string workspacePathCaptured = gitMmWorkspacePath;
					openGitMmItem.Click += delegate
					{
						MainWindow.Instance?.TabManager?.OpenRepository(workspacePathCaptured);
					};
					contextMenu.Items.Add(openGitMmItem);
				}
			}
			return contextMenu;
	}

		private static string GetCurrentRepositoryName(string repositoryPath, string fallbackName)
		{
			return RepositoryManager.Instance.FindRepositoryName(repositoryPath) ?? fallbackName ?? PathHelper.GetReadableFileName(repositoryPath);
		}

		private static Control CreateRepositoryColorsMenuItem(RepositoryManager.Repository repository)
		{
			return global::ForkPlus.UI.WpfCompat.StyleCompat.WithStyle(new MenuItem
			{
				Header = new RepositoryColorsUserControl(repository)			},global::ForkPlus.UI.Theme.CustomContentMenuItemStyle
);
		}

		private static RepositoryManager.Repository? EnsureRepositoryManagerEntry(string repositoryPath)
		{
			string normalizedPath = PathHelper.Normalize(repositoryPath);
			RepositoryManager.Repository? repository = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path == normalizedPath);
			if (!repository.HasValue)
			{
				RepositoryManager.Instance.AddRepositories(new string[1] { normalizedPath });
				RepositoryManager.Instance.Save();
				repository = RepositoryManager.Instance.Repositories.FirstItemStruct((RepositoryManager.Repository x) => x.Path == normalizedPath);
			}
			return repository;
		}

		private static void AddRepositoryToWorkspace(Workspace workspace, string repository)
		{
			List<string> list = new List<string>(workspace.Repositories);
			if (!list.Contains(repository))
			{
				list.Add(repository);
			}
			workspace.Repositories = list.ToArray();
			workspace.ActiveRepository = workspace.ActiveRepository ?? workspace.Repositories.FirstItem();
		}

		private bool CursorReachedDropDistance(Point point)
		{
			if (!(Math.Abs(_dragStartPoint.X - point.X) > 10.0))
			{
				return Math.Abs(_dragStartPoint.Y - point.Y) > 10.0;
			}
			return true;
		}
	}
}
