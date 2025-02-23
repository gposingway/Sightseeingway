using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Plugin.Services;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Threading;
using Lumina.Excel.Sheets;

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

        private const string CommandName = "/sightseeingway";
        private readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
        private readonly List<string> foldersToMonitor = new List<string>();


        public Sightseeingway()
        {
            InitializeFoldersToMonitor();
            InitializeWatchers();
        }

        private void InitializeFoldersToMonitor()
        {
            // Default screenshot folder
            foldersToMonitor.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Final Fantasy XIV - A Realm Reborn", "screenshots"));

            // Check for reshade.ini and SavePath
            var pluginBaseDir = PluginInterface.AssemblyLocation.DirectoryName ?? ".";
            var reshadeIniPath = Path.Combine(pluginBaseDir, "reshade.ini");

            if (File.Exists(reshadeIniPath))
            {
                try
                {
                    var lines = File.ReadAllLines(reshadeIniPath);
                    string? savePath = null;
                    bool inScreenshotGroup = false;

                    foreach (string line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (trimmedLine == "[SCREENSHOT]")
                        {
                            inScreenshotGroup = true;
                        }
                        else if (inScreenshotGroup && trimmedLine.StartsWith("SavePath="))
                        {
                            savePath = trimmedLine.Substring("SavePath=".Length).Trim().Trim('"'); // Extract and trim value
                            break; // Stop searching once SavePath is found
                        }
                        else if (trimmedLine.StartsWith("[")) // Optimization: Stop if a new group starts
                        {
                            inScreenshotGroup = false; // No longer in screenshot group
                        }
                    }

                    if (savePath != null)
                    {
                        var resolvedSavePath = savePath;
                        if (!Path.IsPathRooted(savePath))
                        {
                            resolvedSavePath = Path.GetFullPath(Path.Combine(pluginBaseDir, savePath)); // Make relative path absolute
                        }

                        if (!foldersToMonitor.Contains(resolvedSavePath) && Directory.Exists(resolvedSavePath)) // Check if not already added and exists
                        {
                            foldersToMonitor.Add(resolvedSavePath);
                            Log.Debug($"Reshade SavePath folder added: {resolvedSavePath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error reading reshade.ini: {ex}");
                }
            }
            else
            {
                Log.Debug($"reshade.ini not found at: {reshadeIniPath}");
            }
        }


        private void InitializeWatchers()
        {
            foreach (string folder in foldersToMonitor)
            {
                if (Directory.Exists(folder))
                {
                    var watcher = new FileSystemWatcher(folder);
                    watcher.Created += OnFileCreated;
                    watcher.EnableRaisingEvents = true;
                    watchers.Add(watcher);
                    Log.Debug($"Monitoring folder: {folder}");
                }
                else
                {
                    Log.Error($"Folder not found: {folder}");
                }
            }
        }

        public void Dispose()
        {
            foreach (var watcher in watchers)
            {
                watcher.Created -= OnFileCreated;
                watcher.Dispose();
            }
            watchers.Clear();
        }


        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            var filePath = e.FullPath;
            Log.Debug($"File Created: {filePath}");

            if (WaitForFileRelease(filePath))
            {
                RenameFile(filePath);
            }
            else
            {
                Log.Warning($"File not released in time: {filePath}");
            }
        }

        private bool WaitForFileRelease(string filePath)
        {
            int maxTries = 10;
            for (int i = 0; i < maxTries; ++i)
            {
                try
                {
                    using (var fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        return true;
                    }
                }
                catch (IOException)
                {
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    Log.Error($"Error waiting for file release: {filePath} - {ex}");
                    return false;
                }
            }
            return false;
        }


        private void RenameFile(string filePath)
        {
            try
            {
                var characterName = ClientState.LocalPlayer?.Name.TextValue ?? "nochar";
                var mapName = "nowhere";

                var mapExcelSheet = DataManager.GetExcelSheet<Map>(); // More descriptive name
                if (mapExcelSheet != null && ClientState.MapId > 0)
                {
                    var mapType = mapExcelSheet.GetRow(ClientState.MapId);
                    mapName = mapType.PlaceName.Value.Name.ToString() ?? "nowhere";
                }

                var position = ClientState.LocalPlayer?.Position ?? Vector3.Zero;
                var posPart = position == Vector3.Zero ? "" : $"({position.X:0.00},{position.Y:0.00},{position.Z:0.00})";

                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                var filename = $"{timestamp}_{characterName}_{mapName}_{posPart}.png";
                var newFilePath = Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, filename);

                File.Move(filePath, newFilePath);
                Log.Information($"Renamed file: {filePath} to {newFilePath}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error renaming file: {filePath} - {ex}");
            }
        }
    }
}
