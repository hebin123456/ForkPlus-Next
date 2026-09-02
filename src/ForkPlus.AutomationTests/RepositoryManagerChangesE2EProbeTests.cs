using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
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
	public class RepositoryManagerChangesE2EProbeTests : AutomationTestBase
	{
		private static readonly string OutputDirectory = Path.Combine(AppContext.BaseDirectory, "e2e-screenshots");

		[Fact]
		public void ProbeRepositoryRenameAndChangesUi()
		{
			Directory.CreateDirectory(OutputDirectory);
			string repoPath = CreateTempGitRepo("rename-changes-probe");
			File.AppendAllText(Path.Combine(repoPath, "README.md"), "\nmodified line\n");
			File.WriteAllText(Path.Combine(repoPath, "newfile.txt"), "new file\n");
			(string settingsPath, string originalSettings) = WriteProbeSettings();

			try
			{
				using (var app = LaunchApp($"\"{repoPath}\""))
				{
					CloseGitErrorWindows(app);
					var window = GetForkPlusMainWindow(app);
					try
					{
						window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
					}
					catch
					{
					}
					Thread.Sleep(6000);
					SaveScreenshot("01-start.png");
					WriteTree(window, "01-tree.txt");
					var windowBounds = window.BoundingRectangle;

					var repositorySettingsButton = window.FindFirstDescendant(cf => cf.ByAutomationId("RepositorySettingsDropdownButton"));
					TryClickFirst(repositorySettingsButton);
					Thread.Sleep(1000);
					SaveScreenshot("02-repository-settings-menu.png");
					if (repositorySettingsButton != null)
					{
						var r = repositorySettingsButton.BoundingRectangle;
						Mouse.Click(new Point((int)r.Left - 40, (int)r.Bottom + 14));
					}
					Thread.Sleep(1500);
					CloseGitErrorWindows(app);
					SaveScreenshot("03-after-repository-rename-click.png");

					Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
					Keyboard.Type("renamed-e2e");
					Keyboard.Press(VirtualKeyShort.RETURN);
					Keyboard.Release(VirtualKeyShort.RETURN);
					Thread.Sleep(1500);
					SaveScreenshot("04-after-rename-enter.png");

					Mouse.Click(new Point((int)windowBounds.Left + 62, (int)windowBounds.Top + 127));
					Thread.Sleep(2500);
					SaveScreenshot("05-local-changes.png");
					WriteTree(window, "05-local-changes-tree.txt");

					Mouse.Click(new Point((int)windowBounds.Left + 311, (int)windowBounds.Top + 98));
					Thread.Sleep(1000);
					SaveScreenshot("06-after-unstaged-search-button.png");

					Mouse.Click(new Point((int)windowBounds.Left + 541, (int)windowBounds.Top + 98));
					Thread.Sleep(1000);
					SaveScreenshot("07-view-mode-first-click.png");
					Keyboard.Press(VirtualKeyShort.ESCAPE);
					Keyboard.Release(VirtualKeyShort.ESCAPE);
					Thread.Sleep(500);
					Mouse.Click(new Point((int)windowBounds.Left + 541, (int)windowBounds.Top + 98));
					Thread.Sleep(1000);
					SaveScreenshot("08-view-mode-second-click.png");
					Keyboard.Press(VirtualKeyShort.ESCAPE);
					Keyboard.Release(VirtualKeyShort.ESCAPE);
					Thread.Sleep(500);

					Mouse.Click(new Point((int)windowBounds.Left + 371, (int)windowBounds.Top + 148));
					Thread.Sleep(3000);
					SaveScreenshot("09-after-file-click-diff.png");
					WriteTree(window, "09-after-file-click-diff-tree.txt");
					AssertScreenshotRegionHasContent(
						"09-after-file-click-diff.png",
						new Rectangle((int)windowBounds.Left + 320, (int)windowBounds.Top + 120, 900, 260));
				}
			}
			finally
			{
				RestoreProbeSettings(settingsPath, originalSettings);
			}
		}

		[Fact]
		public void ProbeStartupRevisionSearchAndRenameEditor()
		{
			Directory.CreateDirectory(OutputDirectory);
			string repoPath = CreateTempGitRepo("startup-search-rename-probe");
			(string settingsPath, string originalSettings) = WriteProbeSettings();

			try
			{
				using (var app = LaunchApp($"\"{repoPath}\""))
				{
					CloseGitErrorWindows(app);
					var window = GetForkPlusMainWindow(app);
					try
					{
						window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
					}
					catch
					{
					}
					Thread.Sleep(6000);
					SaveScreenshot("10-startup-first-revision-details.png");
					var windowBounds = window.BoundingRectangle;

					var repositorySettingsButton = window.FindFirstDescendant(cf => cf.ByAutomationId("RepositorySettingsDropdownButton"));
					TryClickFirst(repositorySettingsButton);
					Thread.Sleep(1000);
					SaveScreenshot("12-repository-settings-menu-for-rename.png");
					Keyboard.Press(VirtualKeyShort.DOWN);
					Keyboard.Release(VirtualKeyShort.DOWN);
					Keyboard.Press(VirtualKeyShort.RETURN);
					Keyboard.Release(VirtualKeyShort.RETURN);
					Thread.Sleep(1500);
					SaveScreenshot("13-rename-editor-background.png");
					Keyboard.Press(VirtualKeyShort.ESCAPE);
					Keyboard.Release(VirtualKeyShort.ESCAPE);
					Thread.Sleep(500);

					Mouse.Click(new Point((int)windowBounds.Left + 420, (int)windowBounds.Top + 106));
					Thread.Sleep(300);
					Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
					Thread.Sleep(500);
					Keyboard.Type("Initial");
					Keyboard.Press(VirtualKeyShort.RETURN);
					Keyboard.Release(VirtualKeyShort.RETURN);
					Thread.Sleep(2500);
					SaveScreenshot("11-revision-search-enter.png");
				}
			}
			finally
			{
				RestoreProbeSettings(settingsPath, originalSettings);
			}
		}

		[Fact]
		public void ProbeNewReportedRepositoryInteractions()
		{
			Directory.CreateDirectory(OutputDirectory);
			string repoPath = CreateTempGitRepo("new-reported-interactions-probe");
			string dirPath = Path.Combine(repoPath, "folder");
			Directory.CreateDirectory(dirPath);
			File.WriteAllText(Path.Combine(dirPath, "nested.txt"), "nested\n");
			File.WriteAllText(Path.Combine(repoPath, "loose.txt"), "loose\n");
			(string settingsPath, string originalSettings) = WriteProbeSettings();

			try
			{
				using (var app = LaunchApp($"\"{repoPath}\""))
				{
					CloseGitErrorWindows(app);
					var window = GetForkPlusMainWindow(app);
					try
					{
						window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
					}
					catch
					{
					}
					Thread.Sleep(6000);
					SaveScreenshot("30-startup-first-row-state.png");
					WriteTree(window, "30-startup-first-row-state-tree.txt");

					Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("CommitRadioButton")));
					Assert.NotNull(window.FindFirstDescendant(cf => cf.ByAutomationId("FileTreeRadioButton")));
					Assert.NotNull(FindByName(window, "Initial commit"));

					window.Focus();
					Thread.Sleep(300);
					var fileRoot = FindMenuItemByText(window, "文件") ?? FindMenuItemByText(window, "File");
					if (fileRoot != null)
					{
						fileRoot.Click();
					}
					else
					{
						Point fileMenuPoint = new Point((int)window.BoundingRectangle.Left + 18, (int)window.BoundingRectangle.Top + 13);
						Mouse.Click(fileMenuPoint);
					}
					Thread.Sleep(1000);
					WriteDesktopTree(app, "31-file-menu-tree.txt");
					var accounts = FindMenuItemByText(window, "账号") ?? FindMenuItemByText(window, "Accounts") ?? FindByName(app, "账号...", "Accounts...");
					Assert.NotNull(accounts);
					var ar = accounts.BoundingRectangle;
					Mouse.Click(new Point((int)(ar.Left + ar.Width / 2), (int)(ar.Top + ar.Height / 2)));
					var accountsWindow = WaitForTopLevelWindow(app, "账号", TimeSpan.FromSeconds(5)) ?? WaitForTopLevelWindow(app, "Accounts", TimeSpan.FromSeconds(5));
					Assert.NotNull(accountsWindow);
					SaveScreenshot("31-accounts-window.png");
					CloseWindow(accountsWindow);

					var master = FindByName(window, "master");
					Assert.NotNull(master);
					var mr = master.BoundingRectangle;
					Mouse.Click(new Point((int)(mr.Left + mr.Width / 2), (int)(mr.Top + mr.Height / 2)), MouseButton.Right);
					Assert.True(WaitForPopupMenu(app, TimeSpan.FromSeconds(3)), "Branch context menu did not open.");
					Assert.NotNull(FindMenuItemByText(window, "Copy Branch Name") ?? FindMenuItemByText(window, "复制分支名称"));
					SaveScreenshot("32-branch-context-menu.png");
					Keyboard.Press(VirtualKeyShort.ESCAPE);
					Keyboard.Release(VirtualKeyShort.ESCAPE);
					Thread.Sleep(500);

					Mouse.Click(new Point((int)window.BoundingRectangle.Left + 62, (int)window.BoundingRectangle.Top + 127));
					Thread.Sleep(2000);
					var folder = FindByName(window, "folder");
					Assert.NotNull(folder);
					var fr = folder.BoundingRectangle;
					Mouse.DoubleClick(new Point((int)(fr.Left + fr.Width / 2), (int)(fr.Top + fr.Height / 2)));
					Thread.Sleep(3500);
					Assert.Contains("folder/nested.txt", RunGitOutput(repoPath, "diff", "--cached", "--name-only"));
					SaveScreenshot("33-after-folder-doubleclick-stage.png");

					folder = FindByName(window, "folder");
					Assert.NotNull(folder);
					fr = folder.BoundingRectangle;
					Mouse.DoubleClick(new Point((int)(fr.Left + fr.Width / 2), (int)(fr.Top + fr.Height / 2)));
					Thread.Sleep(3500);
					Assert.DoesNotContain("folder/nested.txt", RunGitOutput(repoPath, "diff", "--cached", "--name-only"));
					SaveScreenshot("34-after-folder-doubleclick-unstage.png");
				}
			}
			finally
			{
				RestoreProbeSettings(settingsPath, originalSettings);
			}
		}

		[Fact]
		public void ProbeQuickLaunchPreviewKeyBehavior()
		{
			Directory.CreateDirectory(OutputDirectory);
			string repoPath = CreateTempGitRepo("quick-launch-probe");
			(string settingsPath, string originalSettings) = WriteProbeSettings();

			try
			{
				using (var app = LaunchApp($"\"{repoPath}\""))
				{
					CloseGitErrorWindows(app);
					var window = GetForkPlusMainWindow(app);
					try
					{
						window.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
					}
					catch
					{
					}
					Thread.Sleep(5000);

					var quickLaunchButton = window.FindFirstDescendant(cf => cf.ByAutomationId("OpenQuicklyToolbarButton"));
					Assert.NotNull(quickLaunchButton);
					TryClickFirst(quickLaunchButton);
					var quickLaunchWindow = WaitForWindowContaining(app, "CommandTextBox", TimeSpan.FromSeconds(5));
					Assert.NotNull(quickLaunchWindow);
					SaveScreenshot("20-quick-launch-open.png");
					WriteTree(quickLaunchWindow, "20-quick-launch-open-tree.txt");

					var commandTextBox = quickLaunchWindow.FindFirstDescendant(cf => cf.ByAutomationId("CommandTextBox"));
					Assert.NotNull(commandTextBox);
					commandTextBox.Patterns.Value.Pattern.SetValue("ftrace");
					Thread.Sleep(1000);
					SaveScreenshot("21-quick-launch-filtered.png");
					WriteTree(quickLaunchWindow, "21-quick-launch-filtered-tree.txt");

					Keyboard.Press(VirtualKeyShort.DOWN);
					Keyboard.Release(VirtualKeyShort.DOWN);
					Keyboard.Press(VirtualKeyShort.UP);
					Keyboard.Release(VirtualKeyShort.UP);
					Keyboard.Press(VirtualKeyShort.RETURN);
					Keyboard.Release(VirtualKeyShort.RETURN);
					Thread.Sleep(1000);
					SaveScreenshot("22-quick-launch-after-enter.png");
					Assert.True(WaitForWindowContainingClosed(app, "CommandTextBox", TimeSpan.FromSeconds(5)), "Quick Launch should close after Enter on ftrace.");
				}
			}
			finally
			{
				RestoreProbeSettings(settingsPath, originalSettings);
			}
		}

		private static void TryClickFirst(AutomationElement element)
		{
			if (element == null)
			{
				return;
			}
			try
			{
				element.Click();
			}
			catch
			{
				var r = element.BoundingRectangle;
				Mouse.Click(new Point((int)(r.Left + Math.Min(r.Width / 2, 40)), (int)(r.Top + r.Height / 2)));
			}
		}

		private static Window GetForkPlusMainWindow(LaunchedApp app)
		{
			foreach (var window in app.Application.GetAllTopLevelWindows(app.Automation))
			{
				if ((window.Title ?? "").IndexOf("ForkPlus", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return window;
				}
			}
			return app.Window;
		}

		private static AutomationElement FindByName(LaunchedApp app, params string[] names)
		{
			foreach (var window in app.Application.GetAllTopLevelWindows(app.Automation))
			{
				var found = FindByName(window, names);
				if (found != null)
				{
					return found;
				}
			}
			foreach (var element in app.Automation.GetDesktop().FindAllDescendants())
			{
				string name = Safe(() => element.Name);
				if (names.Any(x => name.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0))
				{
					return element;
				}
			}
			return null;
		}

		private static AutomationElement FindByName(AutomationElement root, params string[] names)
		{
			foreach (var element in root.FindAllDescendants())
			{
				string name = Safe(() => element.Name);
				if (names.Any(x => name.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0))
				{
					return element;
				}
			}
			return null;
		}

		private static Window WaitForWindowContaining(LaunchedApp app, string automationId, TimeSpan timeout)
		{
			DateTime deadline = DateTime.UtcNow + timeout;
			while (DateTime.UtcNow < deadline)
			{
				foreach (var window in app.Application.GetAllTopLevelWindows(app.Automation))
				{
					if (window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)) != null)
					{
						return window;
					}
				}
				Thread.Sleep(200);
			}
			return null;
		}

		private static bool WaitForWindowContainingClosed(LaunchedApp app, string automationId, TimeSpan timeout)
		{
			DateTime deadline = DateTime.UtcNow + timeout;
			while (DateTime.UtcNow < deadline)
			{
				if (WaitForWindowContaining(app, automationId, TimeSpan.FromMilliseconds(200)) == null)
				{
					return true;
				}
				Thread.Sleep(200);
			}
			return false;
		}

		private static void CloseGitErrorWindows(LaunchedApp app)
		{
			foreach (var window in app.Application.GetAllTopLevelWindows(app.Automation))
			{
				string title = window.Title ?? "";
				if (title.IndexOf("Git", StringComparison.OrdinalIgnoreCase) >= 0 ||
					title.IndexOf("Update", StringComparison.OrdinalIgnoreCase) >= 0 ||
					title.IndexOf("更新", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					try
					{
						window.Close();
					}
					catch
					{
					}
				}
			}
			Thread.Sleep(500);
		}

		private static (string settingsPath, string originalSettings) WriteProbeSettings()
		{
			string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ForkPlus", "settings.json");
			string originalSettings = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
			Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
			File.WriteAllText(settingsPath, "{\n  \"Guid\": \"st-test-guid-00000000\",\n  \"CheckForUpdatesAutomatically\": false,\n  \"FetchRemotesAutomatically\": false,\n  \"AutomaticStatusUpdateInterval\": 0,\n  \"FileListMode\": 1\n}\n");
			return (settingsPath, originalSettings);
		}

		private static string RunGitOutput(string workingDirectory, params string[] args)
		{
			var startInfo = new ProcessStartInfo
			{
				FileName = "git",
				WorkingDirectory = workingDirectory,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};
			foreach (string arg in args)
			{
				startInfo.ArgumentList.Add(arg);
			}
			using (var process = Process.Start(startInfo))
			{
				string output = process.StandardOutput.ReadToEnd();
				string error = process.StandardError.ReadToEnd();
				process.WaitForExit();
				Assert.True(process.ExitCode == 0, "git " + string.Join(" ", args) + " failed: " + error);
				return output.Replace("\r\n", "\n");
			}
		}

		private static void RestoreProbeSettings(string settingsPath, string originalSettings)
		{
			if (originalSettings != null)
			{
				File.WriteAllText(settingsPath, originalSettings);
			}
			else if (File.Exists(settingsPath))
			{
				File.Delete(settingsPath);
			}
		}

		private static void SaveScreenshot(string name)
		{
			var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
			string path = Path.Combine(OutputDirectory, name);
			using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
			using (var graphics = Graphics.FromImage(bitmap))
			{
				graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
				bitmap.Save(path, ImageFormat.Png);
			}
			Assert.True(File.Exists(path), "Screenshot was not written: " + path);
		}

		private static void AssertScreenshotRegionHasContent(string name, Rectangle region)
		{
			string path = Path.Combine(OutputDirectory, name);
			using (var bitmap = new Bitmap(path))
			{
				Rectangle bounds = Rectangle.Intersect(region, new Rectangle(Point.Empty, bitmap.Size));
				int nonWhitePixels = 0;
				for (int y = bounds.Top; y < bounds.Bottom; y += 2)
				{
					for (int x = bounds.Left; x < bounds.Right; x += 2)
					{
						Color pixel = bitmap.GetPixel(x, y);
						if (pixel.R < 245 || pixel.G < 245 || pixel.B < 245)
						{
							nonWhitePixels++;
						}
					}
				}
				Assert.True(nonWhitePixels > 200, $"Expected visible FileDiff content in {name}, but only found {nonWhitePixels} non-white sampled pixels.");
			}
		}

		private static void WriteTree(Window window, string name)
		{
			try
			{
				var lines = window.FindAllDescendants()
					.Take(1000)
					.Select((x, i) =>
					{
						var r = x.BoundingRectangle;
						return $"{i:0000} CT={Safe(() => x.ControlType.ToString())} AID={Safe(() => x.AutomationId)} Name={Safe(() => x.Name)} Rect={(int)r.Left},{(int)r.Top},{(int)r.Width},{(int)r.Height}";
					});
				File.WriteAllLines(Path.Combine(OutputDirectory, name), lines);
			}
			catch (Exception ex)
			{
				File.WriteAllText(Path.Combine(OutputDirectory, name), "WriteTree failed: " + ex);
			}
		}

		private static void WriteDesktopTree(LaunchedApp app, string name)
		{
			try
			{
				var lines = app.Automation.GetDesktop().FindAllDescendants()
					.Take(1500)
					.Select((x, i) =>
					{
						var r = x.BoundingRectangle;
						return $"{i:0000} PID={Safe(() => x.Properties.ProcessId.Value.ToString())} CT={Safe(() => x.ControlType.ToString())} AID={Safe(() => x.AutomationId)} Name={Safe(() => x.Name)} Rect={(int)r.Left},{(int)r.Top},{(int)r.Width},{(int)r.Height}";
					});
				File.WriteAllLines(Path.Combine(OutputDirectory, name), lines);
			}
			catch (Exception ex)
			{
				File.WriteAllText(Path.Combine(OutputDirectory, name), "WriteDesktopTree failed: " + ex);
			}
		}

		private static string Safe(Func<string> value)
		{
			try
			{
				return value() ?? "";
			}
			catch
			{
				return "";
			}
		}
	}
}
