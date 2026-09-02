using System;
using System.IO;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Xunit;

namespace ForkPlus.AutomationTests
{
	/// <summary>
	/// UI 回归测试（系统测试，FlaUI 驱动真实 ForkPlus.exe）。
	/// 覆盖用户反馈的关键缺失：标签内容/右键菜单/关闭按钮、工作区重命名、下拉菜单宽度与失焦关闭。
	/// </summary>
	public class UiRegressionTests : AutomationTestBase, IDisposable
	{
		private static AutomationElement FindFirstPopupMenu(LaunchedApp app)
		{
			var desktop = app.Automation.GetDesktop();
			var menus = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Menu));
			return menus.FirstOrDefault(m => m.Properties.ProcessId.Value == app.Application.ProcessId);
		}

		private static int CountPopupMenus(LaunchedApp app)
		{
			var desktop = app.Automation.GetDesktop();
			var menus = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Menu));
			return menus.Count(m => m.Properties.ProcessId.Value == app.Application.ProcessId);
		}

		private static void ClickWindowSafe(FlaUI.Core.AutomationElements.Window window, int xOffset = 10, int yOffset = 10)
		{
			var r = window.BoundingRectangle;
			Mouse.Click(new System.Drawing.Point((int)r.Left + xOffset, (int)r.Top + yOffset));
		}

		[Fact]
		public void Tabs_ShowRepositoryName_CloseButtonAndContextMenuWork()
		{
			string repoPath = CreateTempGitRepo();
			string repoName = new DirectoryInfo(repoPath).Name;

			using (var app = LaunchApp($"\"{repoPath}\""))
			{
				Thread.Sleep(5000);
				var window = app.RefreshMainWindow();
				Assert.NotNull(window);

				// 找到 TabItem（Avalonia UIA 通常暴露为 ControlType.TabItem）
				var tabItems = window.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem));
				Assert.True(tabItems.Length > 0, "未找到任何 TabItem（标签）。");

				// 断言标签文本包含仓库名（WPF 原版：Header = RepositoryTitle）
				var repoTab = tabItems.FirstOrDefault(t => (t.Name ?? "").IndexOf(repoName, StringComparison.OrdinalIgnoreCase) >= 0);
				Assert.NotNull(repoTab);

				// 右键应弹出上下文菜单，包含“Close All”
				try
				{
					repoTab.RightClick();
				}
				catch
				{
					// 某些情况下 RightClick 不稳定，退化为坐标右键
					var r = repoTab.BoundingRectangle;
					Mouse.Click(new System.Drawing.Point((int)(r.Left + r.Width / 2), (int)(r.Top + r.Height / 2)), MouseButton.Right);
				}

				Assert.True(WaitForPopupMenu(app, TimeSpan.FromSeconds(3)), "右键后未出现 ContextMenu popup。");
				var closeAll = FindMenuItemByText(window, "Close All");
				Assert.NotNull(closeAll);

				// 悬停应出现关闭按钮：用点击右侧小区域模拟点击关闭（不依赖内部控件名）。
				var tabRect = repoTab.BoundingRectangle;
				var closePoint = new System.Drawing.Point((int)(tabRect.Right - 10), (int)(tabRect.Top + tabRect.Height / 2));
				Mouse.MoveTo(closePoint);
				Thread.Sleep(250);
				Mouse.Click(closePoint);

				// 关闭后不应再有该仓库名的标签（最后一个标签会回退到 Repository Manager）。
				Thread.Sleep(1500);
				var tabItemsAfter = window.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem));
				bool stillHasRepoTab = tabItemsAfter.Any(t => (t.Name ?? "").IndexOf(repoName, StringComparison.OrdinalIgnoreCase) >= 0);
				Assert.False(stillHasRepoTab, "点击关闭后仍存在该仓库标签，关闭按钮可能未生效/未显示。");
			}
		}

		[Fact]
		public void WorkspacesDropdown_HasHeaderMenuItem()
		{
			using (var app = LaunchApp())
			{
				Assert.NotNull(app.Window);
				var btn = app.Window.FindFirstDescendant(cf => cf.ByAutomationId("WorkspacesToolbarDropdownButton"));
				Assert.NotNull(btn);
				btn.Click();
				Assert.True(WaitForPopupMenu(app, TimeSpan.FromSeconds(3)), "工作区下拉未弹出。");

				var header = FindMenuItemByText(app.Window, "Workspaces");
				Assert.NotNull(header);
			}
		}

		[Fact]
		public void WorkspacesRename_OpensEditorAndUpdatesText()
		{
			using (var app = LaunchApp())
			{
				Assert.NotNull(app.Window);
				// 打开 Workspaces 下拉 → 点击 Configure Workspaces...
				var btn = app.Window.FindFirstDescendant(cf => cf.ByAutomationId("WorkspacesToolbarDropdownButton"));
				Assert.NotNull(btn);
				btn.Click();
				Assert.True(WaitForPopupMenu(app, TimeSpan.FromSeconds(3)), "工作区下拉未弹出。");

				var configure = FindMenuItemByText(app.Window, "Configure Workspaces");
				Assert.NotNull(configure);
				configure.Click();

				// 弹出配置窗口：标题应包含 Workspaces
				var dlg = WaitForTopLevelWindow(app, "Workspaces", TimeSpan.FromSeconds(10));
				Assert.NotNull(dlg);

				// 找到 ListBoxItem（工作区条目），对第一项右键 → Rename
				var items = dlg.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
				Assert.True(items.Length > 0, "未找到任何工作区条目。");
				var first = items[0];
				first.Click();

				var r = first.BoundingRectangle;
				Mouse.Click(new System.Drawing.Point((int)(r.Left + r.Width / 2), (int)(r.Top + r.Height / 2)), MouseButton.Right);
				Assert.True(WaitForPopupMenu(app, TimeSpan.FromSeconds(3)), "工作区条目右键菜单未弹出。");

				var rename = FindMenuItemByText(app.Window, "Rename");
				Assert.NotNull(rename);
				rename.Click();

				// 应出现编辑框（TextBox/编辑控件）
				var deadline = DateTime.UtcNow.AddSeconds(5);
				AutomationElement edit = null;
				while (DateTime.UtcNow < deadline)
				{
					edit = dlg.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
					if (edit != null) break;
					Thread.Sleep(200);
				}
				Assert.NotNull(edit);

				// 输入新名称并回车
				string newName = "Renamed-" + DateTime.UtcNow.Ticks.ToString().Substring(10);
				edit.Focus();
				Thread.Sleep(200);
				Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
				Keyboard.Type(newName);
				Keyboard.Press(VirtualKeyShort.RETURN);
				Keyboard.Release(VirtualKeyShort.RETURN);

				Thread.Sleep(800);
				// 断言列表中出现新名字
				var itemsAfter = dlg.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
				bool found = itemsAfter.Any(i => (i.Name ?? "").IndexOf(newName, StringComparison.OrdinalIgnoreCase) >= 0);
				Assert.True(found, "重命名后列表未显示新名称（编辑可能未提交/绑定未更新）。");

				CloseWindow(dlg);
			}
		}

		[Fact]
		public void AppearanceDropdown_MenuWidth_EqualsButtonWidth_AndClosesOnClickOutside()
		{
			using (var app = LaunchApp())
			{
				Assert.True(OpenAppearanceDropdown(app), "未能打开外观下拉。");

				var btn = app.Window.FindFirstDescendant(cf => cf.ByAutomationId("AppearanceToolbarDropdownButton"));
				Assert.NotNull(btn);

				var menu = FindFirstPopupMenu(app);
				Assert.NotNull(menu);

				double btnW = btn.BoundingRectangle.Width;
				double menuW = menu.BoundingRectangle.Width;

				// 原版：下拉宽度与按钮等宽（允许 2px 抖动）
				Assert.InRange(menuW, btnW - 2.0, btnW + 2.0);

				// 打开二级菜单（Solid Colors），然后点窗口其它位置应全部消失
				var solid = FindMenuItemByText(app.Window, "Solid Colors");
				Assert.NotNull(solid);
				try { solid.Expand(); } catch { solid.Click(); }
				Assert.True(WaitForPopupMenu(app, TimeSpan.FromSeconds(3)), "未出现二级菜单 popup。");
				Assert.True(CountPopupMenus(app) >= 2, "预期至少两个 popup（主菜单+二级菜单）。");

				// 点击主窗口空白处，菜单应关闭
				ClickWindowSafe(app.Window);
				Thread.Sleep(800);
				Assert.Equal(0, CountPopupMenus(app));
			}
		}
	}
}

