using System.IO;
using System;
using System.Numerics;
using System.Threading;
using Lumina.Excel.Sheets;
using Dalamud.Utility;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace Sightseeingway
{
    public static class IO
    {
        public static void InitializeWatchers(List<string> foldersToMonitor, List<FileSystemWatcher> watchers)
        {
            Plugin.Log.Debug("InitializeWatchers started.");
            foreach (var folder in foldersToMonitor)
            {
                if (Directory.Exists(folder))
                {
                    Plugin.Log.Debug($"Creating watcher for folder: {folder}");
                    var watcher = new FileSystemWatcher(folder);
                    watcher.Created += OnFileCreated;
                    watcher.EnableRaisingEvents = true;
                    watchers.Add(watcher);
                    Plugin.Log.Information($"Monitoring folder: {folder}");
                    Plugin.Chat($"Monitoring folder: {folder}");
                }
                else
                {
                    Plugin.Log.Error($"Folder not found: {folder}");
                    Plugin.ChatGui.PrintError($"[Sightseeingway] Error: Folder not found: {folder}");
                }
            }
            Plugin.Log.Debug("InitializeWatchers finished.");
        }

        public static void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            var filePath = e.FullPath;
            var fileName = e.Name;
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png") return;

            Plugin.Log.Debug($"File Created event triggered for: {filePath}");
            Plugin.Chat($"Debug: File Created: {filePath}");

            if (Caching.IsInRenameCache(e.FullPath))
            {
                Plugin.Log.Debug($"File '{fileName}' is in rename cache, ignoring.");
                Plugin.Chat($"Debug: Ignoring cached filename: {filePath}");
                return;
            }

            if (WaitForFileRelease(filePath))
            {
                RenameFile(filePath);
            }
            else
            {
                Plugin.Log.Warning($"File not released in time for renaming: {filePath}");
                Plugin.Chat($"Warning: File not released in time: {filePath}");
            }
        }

        public static bool WaitForFileRelease(string filePath)
        {
            Plugin.Log.Debug($"WaitForFileRelease started for: {filePath}");
            var maxTries = 10;
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
                var map = "";
                var position = "";
                var eorzeaTime = "";
                var weather = "";
                var character = Plugin.ClientState.LocalPlayer?.Name.TextValue ?? "";

                var fileCreationTime = File.GetCreationTime(filePath);
                var timestamp = fileCreationTime.ToString("yyyyMMddHHmmss") + fileCreationTime.Millisecond.ToString("D3");

                if (character != "")
                {
                    var mapExcelSheet = Plugin.DataManager.GetExcelSheet<Map>();
                    if (mapExcelSheet != null && Plugin.ClientState.MapId > 0)
                    {
                        var mapType = mapExcelSheet.GetRow(Plugin.ClientState.MapId);
                        map = mapType.PlaceName.Value.Name.ExtractText() ?? "";

                        Plugin.Log.Debug($"Map name resolved: {map}");

                        var mapVector = MapUtil.WorldToMap(Plugin.ClientState.LocalPlayer?.Position ?? Vector3.Zero, mapType.OffsetX, mapType.OffsetY, 0, mapType.SizeFactor);

                        Vector3 mapPlace = new(
                            (int)MathF.Round(mapVector.X * 10, 1) / 10f,
                            (int)MathF.Round(mapVector.Y * 10, 1) / 10f,
                            (int)MathF.Round(mapVector.Z * 10, 1) / 10f
                            );

                        position =
                            mapPlace == Vector3.Zero ? "" :
                            mapPlace.Z == 0.0 ? $" ({mapPlace.X:0.0},{mapPlace.Y:0.0})" :
                            $" ({mapPlace.X:0.0},{mapPlace.Y:0.0},{mapPlace.Z:0.0})";
                    }
                    else
                    {
                        Plugin.Log.Warning("Map sheet not found or MapId is 0.");
                    }

                    if (map != "")
                    {
                        // Get in-game Eorzea time
                        eorzeaTime = Client.GetCurrentEorzeaTime().GetDayPeriodWithGoldenHour(true);
                        weather = Client.GetCurrentWeather();
                    }
                }
                // We should have all parts at this point. Let's build the new filename.

                character = PrepareNamePart(character);
                map = PrepareNamePart(map);
                weather = PrepareNamePart(weather);
                eorzeaTime = PrepareNamePart(eorzeaTime);

                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                var newFilename = $"{timestamp}{character}{map}{position}{eorzeaTime}{weather}{extension}";

                var directory = Path.GetDirectoryName(filePath);
                var newFilePath = Path.Combine(directory, newFilename);

                Plugin.Log.Debug($"Renaming '{filePath}' to '{newFilePath}'");

                Caching.AddToRenameCache(newFilename);
                File.Move(filePath, newFilePath);

                Plugin.Log.Information($"Renamed file to: {newFilePath}");

                Client.Print($"Screenshot renamed: {newFilename}");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Error during RenameFile for {filePath}: {ex}");
                Plugin.ChatGui.PrintError($"[Sightseeingway] Error renaming {Path.GetFileName(filePath)}: {ex.Message}");
            }
            Plugin.Log.Debug($"RenameFile finished for: {filePath}");
        }

        public static string PrepareNamePart(string part)
        {
            if (part.IsNullOrEmpty()) return "";
            return "-" + part;
        }


    }
}
