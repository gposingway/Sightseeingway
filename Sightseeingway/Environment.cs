using System.IO;
using System;
using System.Diagnostics;
using System.Linq;
using Dalamud.Game.Config;
using Dalamud.Plugin.Services;

namespace Sightseeingway
{
    public static class Environment
    {
        public static string GetGameDirectory()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                return currentProcess.MainModule?.FileName != null ? Path.GetDirectoryName(currentProcess.MainModule.FileName) : ".";
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error getting game directory from current process: {ex}. Falling back to plugin directory.");
                Plugin.ChatGui.PrintError($"[Sightseeingway] Error getting game directory: {ex.Message}");
                return Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? ".";
            }
        }

        public static string? GetDefaultScreenshotFolder()
        {
            Plugin.Log.Debug("GetDefaultScreenshotFolder started.");
            // Try to get screenshot path from ffxiv.cfg
            var screenshotPathFromConfig = GetScreenshotFolderFromConfig();
            if (!string.IsNullOrEmpty(screenshotPathFromConfig))
            {
                Plugin.Log.Debug($"Screenshot path from ffxiv.cfg: {screenshotPathFromConfig}");
                return screenshotPathFromConfig;
            }

            // Fallback to MyDocuments if not found in config
            try
            {
                string myDocuments = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(myDocuments))
                {
                    Plugin.Log.Warning("My Documents folder path is empty.");
                    return null;
                }

                var defaultFolder = Path.Combine(myDocuments, "My Games", "Final Fantasy XIV - A Realm Reborn", "screenshots");
                Plugin.Log.Debug($"Default screenshot folder (MyDocuments fallback): {defaultFolder}");
                return defaultFolder;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error getting default screenshot folder (MyDocuments fallback): {ex}");
                Plugin.ChatGui.PrintError($"[Sightseeingway] Error getting default screenshot folder (MyDocuments fallback): {ex.Message}");
                return null;
            }
            finally
            {
                Plugin.Log.Debug("GetDefaultScreenshotFolder finished.");
            }
        }

        public static unsafe string? GetScreenshotFolderFromConfig()
        {
            Plugin.Log.Debug("GetScreenshotFolderFromConfig started.");
            try
            {

                string configScreenshotDir;
                var gameConfigPath = GetGameDirectory();
                Plugin.GameConfig.TryGet(SystemConfigOption.ScreenShotDir, out configScreenshotDir);
                if (string.IsNullOrEmpty(configScreenshotDir)) return null;



                var resolvedPath = configScreenshotDir.Trim();
                if (!Path.IsPathRooted(configScreenshotDir))
                {
                    resolvedPath = Path.GetFullPath(Path.Combine(gameConfigPath, configScreenshotDir));
                    Plugin.Log.Debug($"ScreenshotDir is relative, resolved to: {resolvedPath}");
                }

                Plugin.SendMessage($"ffxiv.cfg Screenshot path: {resolvedPath}");
                if (Directory.Exists(resolvedPath)) return resolvedPath;

                var standardDocumentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                var oneDriveDocumentsPath = GetOneDriveDocumentPath() ?? GetOneDriveDocumentPathFromRegistry();

                var laststandardDocumentsPart = Path.GetFileName(standardDocumentsPath);
                var lastOneDriveDocumentsPart = Path.GetFileName(oneDriveDocumentsPath);

                if (resolvedPath.Contains(lastOneDriveDocumentsPart))
                {
                    // replace the start of resolvedPath up to lastOneDriveDocumentsPart with the equivalent part of standardDocumentsPath
                    resolvedPath = resolvedPath.Replace(resolvedPath.Substring(0, resolvedPath.IndexOf(lastOneDriveDocumentsPart) + lastOneDriveDocumentsPart.Length), standardDocumentsPath);
                }

                Plugin.Log.Debug($"Screenshot path from ffxiv.cfg (potentially corrected): {resolvedPath}");
                Plugin.SendMessage($"ffxiv.cfg Screenshot path exists? {Directory.Exists(resolvedPath)}");

                return resolvedPath;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error reading ffxiv.cfg: {ex}");
                Plugin.ChatGui.PrintError($"[Sightseeingway] Error reading ffxiv.cfg: {ex}");
                return null;
            }
            finally
            {
                Plugin.Log.Debug("GetScreenshotFolderFromConfig finished.");
            }
        }

        // I hate you, OneDrive.
        public static string? GetOneDriveDocumentPath()
        {
            var oneDrivePath = System.Environment.GetEnvironmentVariable("OneDriveConsumer"); // Personal OneDrive
            if (string.IsNullOrEmpty(oneDrivePath))
            {
                oneDrivePath = System.Environment.GetEnvironmentVariable("OneDrive"); // OneDrive for Business or Personal
            }
            if (string.IsNullOrEmpty(oneDrivePath))
            {
                oneDrivePath = System.Environment.GetEnvironmentVariable("OneDriveCommercial"); // OneDrive for Business
            }

            if (!string.IsNullOrEmpty(oneDrivePath))
            {
                string documentsPath = Path.Combine(oneDrivePath, "Documents");
                if (Directory.Exists(documentsPath))
                {
                    Plugin.Log.Debug($"OneDrive Documents path found via environment variable: {documentsPath}");
                    return documentsPath;
                }
                else
                {
                    Plugin.Log.Debug($"OneDrive Documents path from env var does not exist: {documentsPath}");
                }
            }
            return null;
        }

        public static string? GetOneDriveDocumentPathFromRegistry()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive"))
                {
                    if (key != null)
                    {
                        var oneDrivePath = key.GetValue("UserFolder") as string;
                        if (!string.IsNullOrEmpty(oneDrivePath))
                        {
                            string documentsPath = Path.Combine(oneDrivePath, "Documents");
                            if (Directory.Exists(documentsPath))
                            {
                                Plugin.Log.Debug($"OneDrive Documents path found via registry: {documentsPath}");
                                return documentsPath;
                            }
                            else
                            {
                                Plugin.Log.Debug($"OneDrive Documents path from registry does not exist: {documentsPath}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error reading OneDrive path from registry: {ex}");
                Plugin.ChatGui.PrintError($"[Sightseeingway] Error reading OneDrive path from registry: {ex.Message}");
            }
            return null;
        }
    }
}
