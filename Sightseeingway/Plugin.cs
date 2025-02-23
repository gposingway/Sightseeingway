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
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Data.Parsing.Layer;
using System.Text.RegularExpressions;

namespace Sightseeingway
{
    public sealed class Sightseeingway : IDalamudPlugin
    {
        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
        [PluginService] internal static IFramework Framework { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;
        [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

        private const string CommandName = "/sightseeingway";
        private readonly List<FileSystemWatcher> watchers = [];
        private readonly List<string> foldersToMonitor = [];

        private readonly Dictionary<string, DateTime> renamedFilesCache = [];
        private readonly TimeSpan cacheDuration = TimeSpan.FromMinutes(1);

        public Sightseeingway()
        {
            Log.Debug("Plugin constructor started.");
            ChatGui.Print($"[Sightseeingway] Plugin Initializing...");

            InitializeFoldersToMonitor();
            InitializeWatchers();
            Log.Debug("Plugin constructor finished.");
            ChatGui.Print($"[Sightseeingway] Plugin Initialized. Monitoring screenshot folders with filename caching.");
        }

        private string GetGameDirectory()
        {
            try
            {
                Process currentProcess = Process.GetCurrentProcess();
                return currentProcess.MainModule?.FileName != null ? Path.GetDirectoryName(currentProcess.MainModule.FileName) : ".";
            }
            catch (Exception ex)
            {
                Log.Error($"Error getting game directory from current process: {ex}. Falling back to plugin directory.");
                return PluginInterface.AssemblyLocation.DirectoryName ?? ".";
            }
        }

        private void InitializeFoldersToMonitor()
        {
            Log.Debug("InitializeFoldersToMonitor started.");
            var defaultScreenshotFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Final Fantasy XIV - A Realm Reborn", "screenshots");
            foldersToMonitor.Add(defaultScreenshotFolder);
            Log.Debug($"Default screenshot folder added to monitor list: {defaultScreenshotFolder}");

            var gameBaseDir = GetGameDirectory();
            var reshadeIniPath = Path.Combine(gameBaseDir, "reshade.ini");
            Log.Debug($"Checking for reshade.ini at: {reshadeIniPath}");

            if (File.Exists(reshadeIniPath))
            {
                Log.Debug($"reshade.ini found in game folder.");
                try
                {
                    var lines = File.ReadAllLines(reshadeIniPath);
                    string? savePath = null;
                    var inScreenshotGroup = false;

                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (trimmedLine == "[SCREENSHOT]")
                        {
                            inScreenshotGroup = true;
                            Log.Debug("Found [SCREENSHOT] group in reshade.ini");
                        }
                        else if (inScreenshotGroup && trimmedLine.StartsWith("SavePath="))
                        {
                            savePath = trimmedLine.Substring("SavePath=".Length).Trim().Trim('"');
                            Log.Debug($"Reshade SavePath found in ini: {savePath}");
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
                            Log.Debug($"Reshade SavePath is relative, resolving to absolute path: {resolvedSavePath}");
                        }

                        if (!foldersToMonitor.Contains(resolvedSavePath) && System.IO.Directory.Exists(resolvedSavePath))
                        {
                            foldersToMonitor.Add(resolvedSavePath);
                            Log.Debug($"Reshade SavePath folder added: {resolvedSavePath}");
                            ChatGui.Print($"[Sightseeingway] Debug: Reshade SavePath folder added: {resolvedSavePath}");
                        }
                        else
                        {
                            Log.Debug($"Reshade SavePath folder already monitored or does not exist: {resolvedSavePath}");
                        }
                    }
                    else
                    {
                        Log.Debug("Reshade SavePath not found in ini.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error reading reshade.ini: {ex}");
                    ChatGui.PrintError($"[Sightseeingway] Error reading reshade.ini: {ex}");
                }
            }
            else
            {
                Log.Debug($"reshade.ini not found in game folder: {reshadeIniPath}");
                ChatGui.Print($"[Sightseeingway] Debug: reshade.ini not found in game folder: {reshadeIniPath}");
            }
            Log.Debug("InitializeFoldersToMonitor finished.");
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
                    ChatGui.Print($"[Sightseeingway] Monitoring folder: {folder}");
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
            ChatGui.Print($"[Sightseeingway] Plugin Disposing...");
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
            ChatGui.Print($"[Sightseeingway] Plugin Disposed.");
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            var filePath = e.FullPath;
            var fileName = e.Name;
            Log.Debug($"File Created event triggered for: {filePath}");
            ChatGui.Print($"[Sightseeingway] Debug: File Created: {filePath}");

            if (IsInRenameCache(e.FullPath))
            {
                Log.Debug($"File '{fileName}' is in rename cache, ignoring.");
                ChatGui.Print($"[Sightseeingway] Debug: Ignoring cached filename: {filePath}");
                return;
            }

            if (WaitForFileRelease(filePath))
            {
                RenameFile(filePath);
            }
            else
            {
                Log.Warning($"File not released in time for renaming: {filePath}");
                ChatGui.Print($"[Sightseeingway] Warning: File not released in time: {filePath}");
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
                var characterName = ClientState.LocalPlayer?.Name.TextValue ?? "nochar";
                var mapName = "nowhere";
                var posPart = "";
                string presetName = null; // Store the preset name

                // Extract preset name if filename matches the expected format
                string fileName = Path.GetFileName(filePath);
                string pattern = @"^(\d{4}-\d{2}-\d{2}) (\d{2}-\d{2}-\d{2}) (.*?) (.*?)(\..+)$"; // Regex pattern
                Match match = Regex.Match(fileName, pattern);

                if (match.Success)
                {
                    presetName = match.Groups[3].Value.Trim(); // Extract the preset name
                    Log.Debug($"Extracted preset name: {presetName}");
                }


                var mapExcelSheet = DataManager.GetExcelSheet<Map>();
                if (mapExcelSheet != null && ClientState.MapId > 0)
                {
                    var mapType = mapExcelSheet.GetRow(ClientState.MapId);
                    mapName = mapType.PlaceName.Value.Name.ExtractText() ?? "nowhere";

                    Log.Debug($"Map name resolved: {mapName}");

                    var position = ClientState.LocalPlayer?.Position ?? Vector3.Zero;
                    var mapPosition = new Vector2(position.X, position.Y);

                    var mapVector = Dalamud.Utility.MapUtil.WorldToMap(mapPosition, mapType);

                    posPart = mapPosition == Vector2.Zero ? "" : $" ({mapVector.X:0.0},{mapVector.Y:0.0})";
                }
                else
                {
                    Log.Warning("Map sheet not found or MapId is 0, using default 'nowhere' for map name.");
                }

                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var newFilename = $"{timestamp}_{characterName}_{mapName}{posPart}";

                if (presetName != null)
                {
                    newFilename = $"{timestamp}_{characterName}_{mapName}{posPart} {presetName}{Path.GetExtension(filePath).ToLowerInvariant()}";
                }
                else
                {
                    newFilename = $"{timestamp}_{characterName}_{mapName}{posPart}{Path.GetExtension(filePath).ToLowerInvariant()}";
                }


                var directory = Path.GetDirectoryName(filePath);
                var newFilePath = Path.Combine(directory, newFilename);

                Log.Debug($"Renaming '{filePath}' to '{newFilePath}'");

                File.Move(filePath, newFilePath);

                AddToRenameCache(newFilename);

                Log.Information($"Renamed file to: {newFilePath}");
                ChatGui.Print($"[Sightseeingway] Renamed: {newFilename}");
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
    }
}
