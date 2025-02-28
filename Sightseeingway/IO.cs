using System.IO;
using System;
using System.Numerics;
using System.Threading;
using Lumina.Excel.Sheets;
using Dalamud.Utility;
using System.Collections.Generic;

namespace Sightseeingway
{
    public static class IO
    {
        public static void SetupWatchers(List<string> foldersToMonitor, List<FileSystemWatcher> watchers)
        {
            Plugin.Log.Debug("InitializeWatchers started.");
            foreach (var folder in foldersToMonitor)
            {
                if (!Directory.Exists(folder))
                {
                    Plugin.Log.Error($"Folder not found: {folder}");
                    Plugin.ChatGui.PrintError($"[Sightseeingway] Error: Folder not found: {folder}");
                    continue;
                }

                Plugin.Log.Debug($"Creating watcher for folder: {folder}");
                var watcher = new FileSystemWatcher(folder)
                {
                    EnableRaisingEvents = true
                };
                watcher.Created += OnFileCreated;
                watchers.Add(watcher);
                Plugin.Log.Information($"Monitoring folder: {folder}");
                Plugin.SendMessage($"Monitoring folder: {folder}");
            }
            Plugin.Log.Debug("InitializeWatchers finished.");
        }

        public static void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            var filePath = e.FullPath;
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png") return;

            Plugin.Log.Debug($"File Created event triggered for: {filePath}");
            Plugin.SendMessage($"Debug: File Created: {filePath}");

            if (Caching.IsInRenameCache(filePath))
            {
                Plugin.Log.Debug($"File '{e.Name}' is in rename cache, ignoring.");
                Plugin.SendMessage($"Debug: Ignoring cached filename: {filePath}");
                return;
            }

            if (WaitForFileRelease(filePath))
            {
                RenameFile(filePath);
            }
            else
            {
                Plugin.Log.Warning($"File not released in time for renaming: {filePath}");
                Plugin.SendMessage($"Warning: File not released in time: {filePath}");
            }
        }

        public static bool WaitForFileRelease(string filePath)
        {
            Plugin.Log.Debug($"WaitForFileRelease started for: {filePath}");
            const int maxTries = 10;
            for (var i = 0; i < maxTries; ++i)
            {
                try
                {
                    Plugin.Log.Debug($"Attempt {i + 1}/{maxTries} to open file: {filePath}");
                    using (var fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        Plugin.Log.Debug($"File opened successfully on attempt {i + 1}: {filePath}");
                        return true;
                    }
                }
                catch (IOException)
                {
                    Plugin.Log.Debug($"IOException on attempt {i + 1}: {filePath} - File likely still in use.");
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"Error waiting for file release: {filePath} - {ex}");
                    Plugin.ChatGui.PrintError($"[Sightseeingway] Error waiting for file release: {filePath} - {ex}");
                    return false;
                }
            }
            Plugin.Log.Warning($"WaitForFileRelease failed after {maxTries} attempts for: {filePath}");
            return false;
        }

        public static void RenameFile(string filePath)
        {
            Plugin.Log.Debug($"RenameFile started for: {filePath}");
            try
            {
                var newFilePath = ResolveNewFileName(filePath);
                if (newFilePath == null)
                {
                    Plugin.Log.Error($"Failed to resolve new file name for: {filePath}");
                    return;
                }

                Plugin.Log.Debug($"Renaming '{filePath}' to '{newFilePath}'");

                Caching.AddToRenameCache(Path.GetFileName(newFilePath));
                File.Move(filePath, newFilePath);

                Plugin.Log.Information($"Renamed file to: {newFilePath}");
                Client.PrintMessage($"Screenshot renamed: {Path.GetFileName(newFilePath)}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error during RenameFile for {filePath}: {ex}");
                Plugin.ChatGui.PrintError($"[Sightseeingway] Error renaming {Path.GetFileName(filePath)}: {ex.Message}");
            }
            Plugin.Log.Debug($"RenameFile finished for: {filePath}");
        }

        public static string ResolveNewFileName(string filePath)
        {
            var character = Plugin.ClientState.LocalPlayer?.Name.TextValue ?? "";
            var fileCreationTime = File.GetCreationTime(filePath);
            var timestamp = fileCreationTime.ToString("yyyyMMddHHmmssfff");

            var map = "";
            var position = "";
            var eorzeaTime = "";
            var weather = "";

            if (!string.IsNullOrEmpty(character))
            {
                var mapExcelSheet = Plugin.DataManager.GetExcelSheet<Map>();
                if (mapExcelSheet != null && Plugin.ClientState.MapId > 0)
                {
                    var mapType = mapExcelSheet.GetRow(Plugin.ClientState.MapId);
                    map = mapType.PlaceName.Value.Name.ExtractText() ?? "";

                    Plugin.Log.Debug($"Map name resolved: {map}");

                    var mapVector = MapUtil.WorldToMap(Plugin.ClientState.LocalPlayer?.Position ?? Vector3.Zero, mapType.OffsetX, mapType.OffsetY, 0, mapType.SizeFactor);

                    var mapPlace = new Vector3(
                        (int)MathF.Round(mapVector.X * 10, 1) / 10f,
                        (int)MathF.Round(mapVector.Y * 10, 1) / 10f,
                        (int)MathF.Round(mapVector.Z * 10, 1) / 10f
                    );

                    position = mapPlace == Vector3.Zero ? "" :
                        mapPlace.Z == 0.0 ? $" ({mapPlace.X:0.0},{mapPlace.Y:0.0})" :
                        $" ({mapPlace.X:0.0},{mapPlace.Y:0.0},{mapPlace.Z:0.0})";
                }
                else
                {
                    Plugin.Log.Warning("Map sheet not found or MapId is 0.");
                }

                if (!string.IsNullOrEmpty(map))
                {
                    eorzeaTime = Client.GetCurrentEorzeaDateTime().DetermineDayPeriod(true);
                    weather = Client.GetCurrentWeatherName();
                }
            }

            var newFilename = $"{timestamp}{NamePart(character)}{NamePart(map)}{position}{NamePart(eorzeaTime)}{NamePart(weather)}{Path.GetExtension(filePath).ToLowerInvariant()}";
            return Path.Combine(Path.GetDirectoryName(filePath), newFilename);
        }

        public static string NamePart(string part)
        {
            return string.IsNullOrEmpty(part) ? "" : "-" + part;
        }
    }
}
