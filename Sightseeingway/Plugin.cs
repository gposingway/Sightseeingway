using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Plugin.Services;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Threading;
using Lumina.Excel.Sheets;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace Sightseeingway
{

    public sealed class Plugin : IDalamudPlugin
    {
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
        [PluginService] internal static IFramework Framework { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;
        [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

        private readonly List<FileSystemWatcher> watchers = [];
        private readonly List<string> foldersToMonitor = [];
        private readonly List<string> iniFilesToCheck = ["ReShade.ini", "GShade.ini"];

        private readonly Dictionary<string, DateTime> renamedFilesCache = [];
        private readonly TimeSpan cacheDuration = TimeSpan.FromMinutes(1);

        // Static debug flag
        public static bool DEBUG = false;

        public Plugin()
        {
            Log.Debug("Plugin constructor started.");
            PrintChatMessage("Plugin Initializing...");

            ChatGui.Print($"[Sightseeingway] Ready to help, friend!");

            InitializeFoldersToMonitor();
            InitializeWatchers();

            Log.Debug("Plugin constructor finished.");
            PrintChatMessage("Plugin Initialized. Monitoring screenshot folders with filename caching.");
        }

        private string GetGameDirectory()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                return currentProcess.MainModule?.FileName != null ? Path.GetDirectoryName(currentProcess.MainModule.FileName) : ".";
            }
            catch (Exception ex)
            {
                Log.Error($"Error getting game directory from current process: {ex}. Falling back to plugin directory.");
                ChatGui.PrintError($"[Sightseeingway] Error getting game directory: {ex.Message}");
                return PluginInterface.AssemblyLocation.DirectoryName ?? ".";
            }
        }

        private string? GetDefaultScreenshotFolder()
        {
            Log.Debug("GetDefaultScreenshotFolder started.");
            // Try to get screenshot path from ffxiv.cfg
            var screenshotPathFromConfig = GetScreenshotFolderFromConfig();
            if (!string.IsNullOrEmpty(screenshotPathFromConfig))
            {
                Log.Debug($"Screenshot path from ffxiv.cfg: {screenshotPathFromConfig}");
                return screenshotPathFromConfig;
            }

            // Fallback to MyDocuments if not found in config
            try
            {
                string myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(myDocuments))
                {
                    Log.Warning("My Documents folder path is empty.");
                    return null;
                }

                var defaultFolder = Path.Combine(myDocuments, "My Games", "Final Fantasy XIV - A Realm Reborn", "screenshots");
                Log.Debug($"Default screenshot folder (MyDocuments fallback): {defaultFolder}");
                return defaultFolder;
            }
            catch (Exception ex)
            {
                Log.Error($"Error getting default screenshot folder (MyDocuments fallback): {ex}");
                ChatGui.PrintError($"[Sightseeingway] Error getting default screenshot folder (MyDocuments fallback): {ex.Message}");
                return null;
            }
            finally
            {
                Log.Debug("GetDefaultScreenshotFolder finished.");
            }
        }
        private unsafe string? GetScreenshotFolderFromConfig()
        {
            var CSFrameworkInstance = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();

            Log.Debug("GetScreenshotFolderFromConfig started.");
            try
            {
                var gameConfigPath = CSFrameworkInstance->UserPathString.ToString(); // General game config folder
                var ffxivCfgFile = CSFrameworkInstance->ConfigPath.ToString(); // Direct path to ffxiv.cfg

                if (string.IsNullOrEmpty(ffxivCfgFile) || !File.Exists(ffxivCfgFile))
                {
                    Log.Warning($"ffxiv.cfg file not found at: {ffxivCfgFile}");
                    return null;
                }

                Log.Debug($"Config path: {gameConfigPath}, ffxiv.cfg path: {ffxivCfgFile}");

                var lines = File.ReadAllLines(ffxivCfgFile);
                string? configScreenshotDir = null;

                foreach (var line in lines)
                {
                    if (line.Trim().StartsWith("ScreenShotDir"))
                    {
                        configScreenshotDir = line.Split('\t').LastOrDefault()?.Trim().Trim('"');
                        if (!string.IsNullOrEmpty(configScreenshotDir)) break; // Found the screenshot directory.
                    }
                }

                if (string.IsNullOrEmpty(configScreenshotDir))
                {
                    Log.Debug("ScreenShotDir not found in ffxiv.cfg.");
                    return null; // Not found
                }

                var resolvedPath = configScreenshotDir.Trim();
                if (!Path.IsPathRooted(configScreenshotDir))
                {
                    resolvedPath = Path.GetFullPath(Path.Combine(gameConfigPath, configScreenshotDir));
                    Log.Debug($"ScreenshotDir is relative, resolved to: {resolvedPath}");
                }

                PrintChatMessage($"ffxiv.cfg Screenshot path: {resolvedPath}");
                if (Directory.Exists(resolvedPath)) return resolvedPath;

                var standardDocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var oneDriveDocumentsPath = GetOneDriveDocumentPath() ?? GetOneDriveDocumentPathFromRegistry();

                var laststandardDocumentsPart = Path.GetFileName(standardDocumentsPath);
                var lastOneDriveDocumentsPart = Path.GetFileName(oneDriveDocumentsPath);

                if (resolvedPath.Contains(lastOneDriveDocumentsPart))
                {
                    // replace the start of resolvedPath up to lastOneDriveDocumentsPart with the equivalent part of standardDocumentsPath
                    resolvedPath = resolvedPath.Replace(resolvedPath.Substring(0, resolvedPath.IndexOf(lastOneDriveDocumentsPart) + lastOneDriveDocumentsPart.Length), standardDocumentsPath);
                }

                Log.Debug($"Screenshot path from ffxiv.cfg (potentially corrected): {resolvedPath}");
                PrintChatMessage($"ffxiv.cfg Screenshot path exists? {Directory.Exists(resolvedPath)}");

                return resolvedPath;
            }
            catch (Exception ex)
            {
                Log.Error($"Error reading ffxiv.cfg: {ex}");
                ChatGui.PrintError($"[Sightseeingway] Error reading ffxiv.cfg: {ex}");
                return null;
            }
            finally
            {
                Log.Debug("GetScreenshotFolderFromConfig finished.");
            }
        }

        // I hate you, OneDrive.
        private string? GetOneDriveDocumentPath()
        {
            string? oneDrivePath = Environment.GetEnvironmentVariable("OneDriveConsumer"); // Personal OneDrive
            if (string.IsNullOrEmpty(oneDrivePath))
            {
                oneDrivePath = Environment.GetEnvironmentVariable("OneDrive"); // OneDrive for Business or Personal
            }
            if (string.IsNullOrEmpty(oneDrivePath))
            {
                oneDrivePath = Environment.GetEnvironmentVariable("OneDriveCommercial"); // OneDrive for Business
            }

            if (!string.IsNullOrEmpty(oneDrivePath))
            {
                string documentsPath = Path.Combine(oneDrivePath, "Documents");
                if (Directory.Exists(documentsPath))
                {
                    Log.Debug($"OneDrive Documents path found via environment variable: {documentsPath}");
                    return documentsPath;
                }
                else
                {
                    Log.Debug($"OneDrive Documents path from env var does not exist: {documentsPath}");
                }
            }
            return null;
        }

        private string? GetOneDriveDocumentPathFromRegistry()
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
                                Log.Debug($"OneDrive Documents path found via registry: {documentsPath}");
                                return documentsPath;
                            }
                            else
                            {
                                Log.Debug($"OneDrive Documents path from registry does not exist: {documentsPath}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error reading OneDrive path from registry: {ex}");
                ChatGui.PrintError($"[Sightseeingway] Error reading OneDrive path from registry: {ex.Message}");
            }
            return null;
        }

        private void InitializeFoldersToMonitor()
        {
            Log.Debug("InitializeFoldersToMonitor started.");
            var defaultScreenshotFolder = GetDefaultScreenshotFolder();

            if (defaultScreenshotFolder != null)
            {
                foldersToMonitor.Add(defaultScreenshotFolder);
                Log.Debug($"Default screenshot folder added to monitor list: {defaultScreenshotFolder}");
            }

            var gameBaseDir = GetGameDirectory();

            var dxgiPath = Path.Combine(gameBaseDir, "dxgi.dll");

            if (File.Exists(dxgiPath))
            {
                Log.Debug($"dxgi.dll found, checking for INI files.");
                foreach (var iniFileName in iniFilesToCheck)
                {
                    ProcessIniFile(gameBaseDir, iniFileName);
                }
            }
            else
            {
                Log.Debug($"dxgi.dll not found in game folder, skipping INI file checks.");
                PrintChatMessage($"Debug: dxgi.dll not found, skipping INI file checks.");
            }

            Log.Debug("InitializeFoldersToMonitor finished.");
        }

        private void ProcessIniFile(string gameBaseDir, string iniFileName)
        {
            var iniFilePath = Path.Combine(gameBaseDir, iniFileName);
            Log.Debug($"Checking for {iniFileName} at: {iniFilePath}");

            if (File.Exists(iniFilePath))
            {
                Log.Debug($"{iniFileName} found in game folder.");
                try
                {
                    var lines = File.ReadAllLines(iniFilePath);
                    string? savePath = null;
                    var inScreenshotGroup = false;

                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (trimmedLine == "[SCREENSHOT]")
                        {
                            inScreenshotGroup = true;
                            Log.Debug($"Found [SCREENSHOT] group in {iniFileName}");
                        }
                        else if (inScreenshotGroup && trimmedLine.StartsWith("SavePath="))
                        {
                            savePath = trimmedLine.Substring("SavePath=".Length).Trim().Trim('"');
                            Log.Debug($"{iniFileName} SavePath found in ini: {savePath}");
                            break;
                        }
                        else if (trimmedLine.StartsWith("["))
                        {
                            inScreenshotGroup = false;
                        }
                    }

                    if (savePath != null)
                    {
                        var resolvedSavePath = savePath;
                        if (!Path.IsPathRooted(savePath))
                        {
                            resolvedSavePath = Path.GetFullPath(Path.Combine(gameBaseDir, savePath));
                            Log.Debug($"{iniFileName} SavePath is relative, resolving to absolute path: {resolvedSavePath}");
                        }

                        if (!foldersToMonitor.Contains(resolvedSavePath) && System.IO.Directory.Exists(resolvedSavePath))
                        {
                            foldersToMonitor.Add(resolvedSavePath);
                            Log.Debug($"{iniFileName} SavePath folder added: {resolvedSavePath}");
                            PrintChatMessage($"Debug: {iniFileName} SavePath folder added: {resolvedSavePath}");
                        }
                        else
                        {
                            Log.Debug($"{iniFileName} SavePath folder already monitored or does not exist: {resolvedSavePath}");
                        }
                    }
                    else
                    {
                        Log.Debug($"{iniFileName} SavePath not found in ini.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error reading {iniFileName}: {ex}");
                    ChatGui.PrintError($"[Sightseeingway] Error reading {iniFileName}: {ex}");
                }
            }
            else
            {
                Log.Debug($"{iniFileName} not found in game folder: {iniFilePath}");
                PrintChatMessage($"Debug: {iniFileName} not found in game folder: {iniFilePath}");
            }
        }
        private void InitializeWatchers()
        {
            Log.Debug("InitializeWatchers started.");
            foreach (string folder in foldersToMonitor)
            {
                if (System.IO.Directory.Exists(folder))
                {
                    Log.Debug($"Creating watcher for folder: {folder}");
                    var watcher = new FileSystemWatcher(folder);
                    watcher.Created += OnFileCreated;
                    watcher.EnableRaisingEvents = true;
                    watchers.Add(watcher);
                    Log.Information($"Monitoring folder: {folder}");
                    PrintChatMessage($"Monitoring folder: {folder}");
                }
                else
                {
                    Log.Error($"Folder not found: {folder}");
                    ChatGui.PrintError($"[Sightseeingway] Error: Folder not found: {folder}");
                }
            }
            Log.Debug("InitializeWatchers finished.");
        }

        public void Dispose()
        {
            Log.Debug("Dispose started.");
            PrintChatMessage("Plugin Disposing...");

            foreach (var watcher in watchers)
            {
                Log.Debug($"Disposing watcher for folder: {watcher.Path}");
                watcher.Created -= OnFileCreated;
                watcher.Dispose();
                Log.Debug($"Watcher for folder disposed: {watcher.Path}");
            }
            watchers.Clear();
            Log.Debug("Watcher list cleared.");
            Log.Debug("Dispose finished.");
            PrintChatMessage("Plugin Disposed.");
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            var filePath = e.FullPath;
            var fileName = e.Name;
            Log.Debug($"File Created event triggered for: {filePath}");
            PrintChatMessage($"Debug: File Created: {filePath}");

            if (IsInRenameCache(e.FullPath))
            {
                Log.Debug($"File '{fileName}' is in rename cache, ignoring.");
                PrintChatMessage($"Debug: Ignoring cached filename: {filePath}");
                return;
            }

            if (WaitForFileRelease(filePath))
            {
                RenameFile(filePath);
            }
            else
            {
                Log.Warning($"File not released in time for renaming: {filePath}");
                PrintChatMessage($"Warning: File not released in time: {filePath}");
            }
        }

        private bool WaitForFileRelease(string filePath)
        {
            Log.Debug($"WaitForFileRelease started for: {filePath}");
            var maxTries = 10;
            for (var i = 0; i < maxTries; ++i)
            {
                try
                {
                    Log.Debug($"Attempt {i + 1}/{maxTries} to open file: {filePath}");
                    using (var fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        Log.Debug($"File opened successfully on attempt {i + 1}: {filePath}");
                        return true;
                    }
                }
                catch (IOException)
                {
                    Log.Debug($"IOException on attempt {i + 1}: {filePath} - File likely still in use.");
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    Log.Error($"Error waiting for file release: {filePath} - {ex}");
                    ChatGui.PrintError($"[Sightseeingway] Error waiting for file release: {filePath} - {ex}");
                    return false;
                }
            }
            Log.Warning($"WaitForFileRelease failed after {maxTries} attempts for: {filePath}");
            return false;
        }

        private void RenameFile(string filePath)
        {
            Log.Debug($"RenameFile started for: {filePath}");
            try
            {
                var characterName = ClientState.LocalPlayer?.Name.TextValue.Replace(" ", "") ?? "";
                var mapName = "";
                var posPart = "";

                var mapExcelSheet = DataManager.GetExcelSheet<Map>();
                if (mapExcelSheet != null && ClientState.MapId > 0)
                {
                    var mapType = mapExcelSheet.GetRow(ClientState.MapId);
                    mapName = mapType.PlaceName.Value.Name.ExtractText().Replace(" ", "") ?? "";

                    Log.Debug($"Map name resolved: {mapName}");

                    var position = ClientState.LocalPlayer?.Position ?? Vector3.Zero;
                    var mapPosition = new Vector2(position.X, position.Y);

                    var mapVector = MapUtil.WorldToMap(mapPosition, mapType);

                    posPart = mapPosition == Vector2.Zero ? "" : $"({mapVector.X:0.0},{mapVector.Y:0.0})";
                }
                else
                {
                    Log.Warning("Map sheet not found or MapId is 0.");
                }

                var fileCreationTime = File.GetCreationTime(filePath);
                var timestamp = fileCreationTime.ToString("yyyyMMddHHmmss") + fileCreationTime.Millisecond.ToString("D3");

                // We should have all parts at this point. Let's build the new filename.

                if (!characterName.IsNullOrEmpty()) characterName = "-" + characterName;
                if (!mapName.IsNullOrEmpty()) mapName = "-" + mapName;

                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                var newFilename = $"{timestamp}{characterName}{mapName}{posPart}{extension}";

                var directory = Path.GetDirectoryName(filePath);
                var newFilePath = Path.Combine(directory, newFilename);

                Log.Debug($"Renaming '{filePath}' to '{newFilePath}'");

                File.Move(filePath, newFilePath);

                AddToRenameCache(newFilename);

                Log.Information($"Renamed file to: {newFilePath}");
                ChatGui.Print($"[Sightseeingway] Screenshot renamed: {newFilename}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error during RenameFile for {filePath}: {ex}");
                ChatGui.PrintError($"[Sightseeingway] Error renaming {Path.GetFileName(filePath)}: {ex.Message}");
            }
            Log.Debug($"RenameFile finished for: {filePath}");
        }

        private void AddToRenameCache(string filename)
        {
            if (renamedFilesCache.ContainsKey(filename))
            {
                renamedFilesCache[filename] = DateTime.Now;
                Log.Debug($"Filename cache updated for '{filename}'.");
            }
            else
            {
                renamedFilesCache.Add(filename, DateTime.Now);
                Log.Debug($"Filename '{filename}' added to rename cache.");
            }
            CleanRenameCache();
        }

        private bool IsInRenameCache(string filename)
        {
            if (renamedFilesCache.ContainsKey(filename))
            {
                Log.Debug($"Filename '{filename}' found in rename cache.");
                return true;
            }
            return false;
        }

        private void CleanRenameCache()
        {
            var expiredKeys = renamedFilesCache.Keys.Where(key => DateTime.Now - renamedFilesCache[key] > cacheDuration).ToList();
            if (expiredKeys.Any())
            {
                Log.Debug($"Cleaning {expiredKeys.Count} expired items from rename cache.");
                foreach (var key in expiredKeys)
                {
                    renamedFilesCache.Remove(key);
                    Log.Debug($"Expired filename '{key}' removed from cache.");
                }
            }
        }

        private void PrintChatMessage(string message)
        {
            if (DEBUG)
            {
                ChatGui.Print($"[Sightseeingway]: {message}");
            }
        }
    }
}
