using System;
using System.Diagnostics;
using System.IO;
using Dalamud.Game.Config;
using SysEnv = System.Environment;

namespace Sightseeingway
{
    /// <summary>
    /// Resolves game-related filesystem paths (FFXIV install dir, screenshot folder, OneDrive overrides).
    /// </summary>
    public static class GameEnvironment
    {
        public static string GetGameDirectory()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                return currentProcess.MainModule?.FileName != null
                    ? Path.GetDirectoryName(currentProcess.MainModule.FileName) ?? "."
                    : ".";
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

            var screenshotPathFromConfig = GetScreenshotFolderFromConfig();
            if (!string.IsNullOrEmpty(screenshotPathFromConfig))
            {
                Plugin.Logger?.Debug($"Screenshot path from ffxiv.cfg: {screenshotPathFromConfig}");
                return screenshotPathFromConfig;
            }

            try
            {
                var myDocuments = SysEnv.GetFolderPath(SysEnv.SpecialFolder.MyDocuments);
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
                Plugin.Logger?.Error("Error getting default screenshot folder (MyDocuments fallback)", ex, true);
                return null;
            }
        }

        public static unsafe string? GetScreenshotFolderFromConfig()
        {
            Plugin.Logger?.Debug("GetScreenshotFolderFromConfig started.");
            try
            {
                var gameConfigPath = GetGameDirectory();
                Plugin.GameConfig.TryGet(SystemConfigOption.ScreenShotDir, out string configScreenshotDir);
                if (string.IsNullOrEmpty(configScreenshotDir)) return null;

                var resolvedPath = configScreenshotDir.Trim();
                if (!Path.IsPathRooted(configScreenshotDir))
                {
                    resolvedPath = Path.GetFullPath(Path.Combine(gameConfigPath, configScreenshotDir));
                    Plugin.Logger?.Debug($"ScreenshotDir is relative, resolved to: {resolvedPath}");
                }

                if (Directory.Exists(resolvedPath)) return resolvedPath;

                // OneDrive may have moved the configured path; if so, redirect to the standard Documents location.
                var standardDocumentsPath = SysEnv.GetFolderPath(SysEnv.SpecialFolder.MyDocuments);
                var oneDriveDocumentsPath = GetOneDriveDocumentPath() ?? GetOneDriveDocumentPathFromRegistry();

                if (oneDriveDocumentsPath != null)
                {
                    var lastOneDriveDocumentsPart = Path.GetFileName(oneDriveDocumentsPath);
                    if (!string.IsNullOrEmpty(lastOneDriveDocumentsPart) && resolvedPath.Contains(lastOneDriveDocumentsPart))
                    {
                        var prefixEnd = resolvedPath.IndexOf(lastOneDriveDocumentsPart) + lastOneDriveDocumentsPart.Length;
                        resolvedPath = resolvedPath.Replace(resolvedPath.Substring(0, prefixEnd), standardDocumentsPath);
                    }
                }

                Plugin.Logger?.Debug($"Screenshot path from ffxiv.cfg (potentially corrected): {resolvedPath}");
                return resolvedPath;
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Error("Error reading ffxiv.cfg", ex, true);
                return null;
            }
        }

        // I hate you, OneDrive.
        public static string? GetOneDriveDocumentPath()
        {
            var oneDrivePath = SysEnv.GetEnvironmentVariable("OneDriveConsumer")
                ?? SysEnv.GetEnvironmentVariable("OneDrive")
                ?? SysEnv.GetEnvironmentVariable("OneDriveCommercial");

            if (string.IsNullOrEmpty(oneDrivePath)) return null;

            var documentsPath = Path.Combine(oneDrivePath, "Documents");
            if (Directory.Exists(documentsPath))
            {
                Plugin.Logger?.Debug($"OneDrive Documents path found via environment variable: {documentsPath}");
                return documentsPath;
            }

            Plugin.Logger?.Debug($"OneDrive Documents path from env var does not exist: {documentsPath}");
            return null;
        }

        public static string? GetOneDriveDocumentPathFromRegistry()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive");
                if (key?.GetValue("UserFolder") is not string oneDrivePath || string.IsNullOrEmpty(oneDrivePath)) return null;

                var documentsPath = Path.Combine(oneDrivePath, "Documents");
                if (Directory.Exists(documentsPath))
                {
                    Plugin.Logger?.Debug($"OneDrive Documents path found via registry: {documentsPath}");
                    return documentsPath;
                }

                Plugin.Logger?.Debug($"OneDrive Documents path from registry does not exist: {documentsPath}");
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Error("Error reading OneDrive path from registry", ex, true);
                return null;
            }
        }
    }
}
