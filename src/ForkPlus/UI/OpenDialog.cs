using System;
using System.IO;
using Avalonia;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Win32;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI
{
        internal static class OpenDialog
        {
                public static bool SelectDirectory([Null] Window parent, string title, string initialDirectory, out string directoryPath)
                {
                        try
                        {
                                if (ShowOpen(parent, Translate(title), initialDirectory, folderPicker: true, filters: null, out directoryPath))
                                {
                                        return true;
                                }
                        }
                        catch (Exception ex)
                        {
                                Log.Error("Failed to show open directory dialog", ex);
                        }
                        directoryPath = null;
                        return false;
                }

                public static bool SelectExecutableFile([Null] Window parent, string title, string initialDirectory, out string filePath)
                {
                        return SelectFile(parent, title, initialDirectory, "Applications", "*.exe", out filePath);
                }

                public static bool SelectFile([Null] Window parent, string title, string initialDirectory, string fileTypeName, string extensionPattern, out string filePath)
                {
                        try
                        {
                                var filters = new[] { (Translate(fileTypeName), extensionPattern) };
                                if (ShowOpen(parent, Translate(title), initialDirectory, folderPicker: false, filters, out filePath))
                                {
                                        return true;
                                }
                        }
                        catch (Exception ex)
                        {
                                Log.Error("Failed to show open file dialog", ex);
                        }
                        filePath = null;
                        return false;
                }

                public static bool SelectPatchSaveLocation([Null] Window parent, string title, string initialDirectory, string defaultFileName, out string filePath)
                {
                        try
                        {
                                var filters = new[] { (Translate("Patches"), "*" + Consts.Git.PatchFileExtension) };
                                if (ShowSave(parent, Translate(title), initialDirectory, defaultFileName, filters, out filePath))
                                {
                                        if (!filePath.EndsWith(Consts.Git.PatchFileExtension, StringComparison.CurrentCultureIgnoreCase))
                                        {
                                                filePath += Consts.Git.PatchFileExtension;
                                        }
                                        return true;
                                }
                        }
                        catch (Exception ex)
                        {
                                Log.Error("Failed to show save dialog", ex);
                        }
                        filePath = null;
                        return false;
                }

                public static bool SelectFileSaveLocation([Null] Window parent, string title, string initialDirectory, string defaultFileName, out string resultFilePath)
                {
                        try
                        {
                                string extension = Path.GetExtension(defaultFileName);
                                var filters = new[] { (string.Format(Translate("*{0} files"), extension), extension) };
                                if (ShowSave(parent, Translate(title), initialDirectory, defaultFileName, filters, out resultFilePath))
                                {
                                        return true;
                                }
                        }
                        catch (Exception ex)
                        {
                                Log.Error("Failed to show save dialog", ex);
                        }
                        resultFilePath = null;
                        return false;
                }

                private static bool ShowOpen(Window parent, string title, string initialDirectory, bool folderPicker,
                        (string name, string spec)[] filters, out string path)
                {
                        IntPtr owner = GetOwnerHandle(parent);
                        if (FileDialogInterop.ShowOpenDialog(owner, title, initialDirectory, folderPicker, filters, out path))
                        {
                                NotifyDialogClosed(parent);
                                return true;
                        }
                        NotifyDialogClosed(parent);
                        return false;
                }

                private static bool ShowSave(Window parent, string title, string initialDirectory, string defaultFileName,
                        (string name, string spec)[] filters, out string path)
                {
                        IntPtr owner = GetOwnerHandle(parent);
                        if (FileDialogInterop.ShowSaveDialog(owner, title, initialDirectory, defaultFileName, filters, out path))
                        {
                                NotifyDialogClosed(parent);
                                return true;
                        }
                        NotifyDialogClosed(parent);
                        return false;
                }

                private static void NotifyDialogClosed([Null] Window parent)
                {
                        if (parent == MainWindow.Instance)
                        {
                                MainWindow.Instance.PreventRefreshAfterChildDialogClose("Open File Dialog");
                        }
                }

                private static IntPtr GetOwnerHandle([Null] Window parent)
                {
                        try
                        {
                                if (parent?.TryGetPlatformHandle() is { } handle)
                                {
                                        return handle.Handle;
                                }
                        }
                        catch
                        {
                        }
                        return IntPtr.Zero;
                }

                private static string Translate(string text)
                {
                        return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
                }
        }
}
