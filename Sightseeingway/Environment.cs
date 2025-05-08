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
                Plugin.Logger?.Error($"Error getting game directory from current process: {ex}. Falling back to plugin directory.", ex, true);
                return Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? ".";
            }
        }

        public static string? GetDefaultScreenshotFolder()
        {
            Plugin.Logger?.Debug("GetDefaultScreenshotFolder started.");
            // Try to get screenshot path from ffxiv.cfg
            var screenshotPathFromConfig = GetScreenshotFolderFromConfig();
            if (!string.IsNullOrEmpty(screenshotPathFromConfig))
            {
                Plugin.Logger?.Debug($"Screenshot path from ffxiv.cfg: {screenshotPathFromConfig}");
                return screenshotPathFromConfig;
            }

            // Fallback to MyDocuments if not found in config
            try
            {
                string myDocuments = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(myDocuments))
                {
                    Plugin.Logger?.Warning("My Documents folder path is empty.");
                    return null;
                }

                var defaultFolder = Path.Combine(myDocuments, "My Games", "Final Fantasy XIV - A Realm Reborn", "screenshots");
                Plugin.Logger?.Debug($"Default screenshot folder (MyDocuments fallback): {defaultFolder}");
                return defaultFolder;
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Error($"Error getting default screenshot folder (MyDocuments fallback)", ex, true);
                return null;
            }
            finally
            {
                Plugin.Logger?.Debug("GetDefaultScreenshotFolder finished.");
            }
        }

        public static unsafe string? GetScreenshotFolderFromConfig()
        {
            Plugin.Logger?.Debug("GetScreenshotFolderFromConfig started.");
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
                    Plugin.Logger?.Debug($"ScreenshotDir is relative, resolved to: {resolvedPath}");
                }

                // Plugin.Logger?.Debug($"ffxiv.cfg Screenshot path: {resolvedPath}");
                if (Directory.Exists(resolvedPath)) return resolvedPath;

                var standardDocumentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                var oneDriveDocumentsPath = GetOneDriveDocumentPath() ?? GetOneDriveDocumentPathFromRegistry();

                var laststandardDocumentsPart = Path.GetFileName(standardDocumentsPath);

                if (oneDriveDocumentsPath != null) // OneDrive is enabled
                {
                    var lastOneDriveDocumentsPart = Path.GetFileName(oneDriveDocumentsPath);

                    if (resolvedPath.Contains(lastOneDriveDocumentsPart))
                    {
                        // replace the start of resolvedPath up to lastOneDriveDocumentsPart with the equivalent part of standardDocumentsPath
                        resolvedPath = resolvedPath.Replace(resolvedPath.Substring(0, resolvedPath.IndexOf(lastOneDriveDocumentsPart) + lastOneDriveDocumentsPart.Length), standardDocumentsPath);
                    }
                }

                Plugin.Logger?.Debug($"Screenshot path from ffxiv.cfg (potentially corrected): {resolvedPath}");

                return resolvedPath;
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Error($"Error reading ffxiv.cfg", ex, true);
                return null;
            }
            finally
            {
                Plugin.Logger?.Debug("GetScreenshotFolderFromConfig finished.");
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
                    Plugin.Logger?.Debug($"OneDrive Documents path found via environment variable: {documentsPath}");
                    return documentsPath;
                }
                else
                {
                    Plugin.Logger?.Debug($"OneDrive Documents path from env var does not exist: {documentsPath}");
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
                                Plugin.Logger?.Debug($"OneDrive Documents path found via registry: {documentsPath}");
                                return documentsPath;
                            }
                            else
                            {
                                Plugin.Logger?.Debug($"OneDrive Documents path from registry does not exist: {documentsPath}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Error($"Error reading OneDrive path from registry", ex, true);
            }
            return null;
        }
    }
}
