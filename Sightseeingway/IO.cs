using System.IO;
using System;
using System.Numerics;
using System.Threading;
using Lumina.Excel.Sheets;
using Dalamud.Utility;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace Sightseeingway
{
    public static class IO
    {
        public static string? CurrentPresetName { get; private set; }
        public static bool EffectsEnabled { get; private set; }

        public static void SetupWatchers(List<string> foldersToMonitor, List<FileSystemWatcher> watchers)
        {
            Plugin.SendMessage("InitializeWatchers started.");
            foreach (var folder in foldersToMonitor)
            {
                if (!Directory.Exists(folder))
                {
                    Plugin.Log.Error($"Folder not found: {folder}");
                    Plugin.ChatGui.PrintError($"[Sightseeingway] Error: Folder not found: {folder}");
                    continue;
                }

                Plugin.SendMessage($"Creating watcher for folder: {folder}");
                var watcher = new FileSystemWatcher(folder)
                {
                    EnableRaisingEvents = true
                };
                watcher.Created += OnFileCreated;
                watchers.Add(watcher);
                Plugin.Log.Information($"Monitoring folder: {folder}");
                Plugin.SendMessage($"Monitoring folder: {folder}");
            }
            Plugin.SendMessage("InitializeWatchers finished.");
        }

        // Modified to return ShadingwayState and renamed to LoadShadingwayState
        public static ShadingwayState? LoadShadingwayState(string filePath)
        {
            Plugin.SendMessage($"LoadShadingwayState started for: {filePath}");
            Plugin.SendMessage($"Debug: LoadShadingwayState for: {filePath}");
            ShadingwayState? state = null;

            if (!WaitForFileReleaseGeneric(filePath, FileAccess.Read)) // Wait for file release using generic method
            {
                Plugin.Log.Warning($"Shadingway state file not released in time for reading: {filePath}");
                Plugin.ChatGui.PrintError($"[Sightseeingway] Warning: Shadingway state file not released in time, may read incomplete data.");
                return null; // Return null to indicate load failure due to file access
            }

            try
            {
                using (StreamReader file = File.OpenText(filePath))
                using (JsonTextReader reader = new JsonTextReader(file))
                {
                    var serializer = new JsonSerializer();
                    state = serializer.Deserialize<ShadingwayState>(reader);
                    if (state != null)
                    {

                        var jsonPid = state.Pid;
                        var currentPid = Process.GetCurrentProcess().Id;

                        if (jsonPid == currentPid)
                        {

                            EffectsEnabled = state.Effects?.Enabled ?? false;
                            CurrentPresetName = state.Preset?.Name;
                            Plugin.SendMessage($"Shadingway State Parsed: EffectsEnabled={EffectsEnabled}, PresetName={CurrentPresetName}");
                            Plugin.SendMessage($"Debug: Shadingway Preset: {CurrentPresetName}, Effects: {(EffectsEnabled ? "Enabled" : "Disabled")}");
                        }
                        else
                        {
                            EffectsEnabled = false;
                            CurrentPresetName = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CurrentPresetName = null;
                EffectsEnabled = false;
                return null; // Indicate load failure
            }
            Plugin.SendMessage($"LoadShadingwayState finished for: {filePath}");
            return state; // Return the loaded state
        }


        // Generic method to wait for file release
        public static bool WaitForFileReleaseGeneric(string filePath, FileAccess fileAccess = FileAccess.Read)
        {
            Plugin.SendMessage($"WaitForFileReleaseGeneric started for: {filePath}, FileAccess: {fileAccess}");
            const int maxTries = 30;
            for (var i = 0; i < maxTries; ++i)
            {
                try
                {
                    Plugin.SendMessage($"Attempt {i + 1}/{maxTries} to open file: {filePath} with FileAccess.{fileAccess}");
                    using (var fs = File.Open(filePath, FileMode.Open, fileAccess, FileShare.ReadWrite)) // Allow shared read/write for generic use
                    {
                        Plugin.SendMessage($"File opened successfully on attempt {i + 1}: {filePath}");
                        return true;
                    }
                }
                catch (IOException)
                {
                    Plugin.SendMessage($"IOException on attempt {i + 1}: {filePath} - File likely still in use.");
                    Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"Error waiting for file release: {filePath} - {ex}");
                    Plugin.ChatGui.PrintError($"[Sightseeingway] Error waiting for file release: {filePath} - {ex}");
                    return false;
                }
            }
            Plugin.Log.Warning($"WaitForFileReleaseGeneric failed after {maxTries} attempts for: {filePath}");
            return false;
        }

        // Placeholder - No longer used directly by FileSystemWatcher, Plugin has its own handler now
        public static void OnShadingwayStateChanged(object sender, FileSystemEventArgs e)
        {
            // This method is now a placeholder, the Plugin class handles the event directly
            Plugin.SendMessage("IO.OnShadingwayStateChanged PLACEHOLDER - Event handled in Plugin.cs");
        }


        public static void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            var filePath = e.FullPath;
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (extension != ".jpg" && extension != ".jpeg" && extension != ".png") return;

            Plugin.SendMessage($"File Created event triggered for: {filePath}");
            Plugin.SendMessage($"Debug: File Created: {filePath}");

            if (Caching.IsInRenameCache(filePath))
            {
                Plugin.SendMessage($"File '{e.Name}' is in rename cache, ignoring.");
                Plugin.SendMessage($"Debug: Ignoring cached filename: {filePath}");
                return;
            }

            if (WaitForFileReleaseGeneric(filePath)) // Now uses generic WaitForFileReleaseGeneric with FileAccess.ReadWrite
            {
                RenameFile(filePath);
            }
            else
            {
                Plugin.Log.Warning($"File not released in time for renaming: {filePath}");
                Plugin.SendMessage($"Warning: File not released in time: {filePath}");
            }
        }

        public static bool MoveFileWithRetry(string sourceFilePath, string destFilePath)
        {
            Plugin.SendMessage($"MoveFileWithRetry started from: {sourceFilePath} to {destFilePath}");
            const int maxTries = 10;
            for (int i = 0; i < maxTries; ++i)
            {
                if (WaitForFileReleaseGeneric(sourceFilePath, FileAccess.ReadWrite)) // Wait for release with ReadWrite access
                {
                    try
                    {
                        Plugin.SendMessage($"Attempt {i + 1}/{maxTries} to move file: {sourceFilePath} to {destFilePath}");
                        File.Move(sourceFilePath, destFilePath);
                        Plugin.Log.Information($"File moved successfully to: {destFilePath}");
                        return true; // Move successful
                    }
                    catch (IOException ex)
                    {
                        Plugin.Log.Warning($"IOException during MoveFile on attempt {i + 1}: {ex.Message}");
                        if (i == maxTries - 1) // Only print error to chat on the last retry
                        {
                            Plugin.ChatGui.PrintError($"[Sightseeingway] Error moving file {Path.GetFileName(sourceFilePath)} to {Path.GetFileName(destFilePath)}: {ex.Message}");
                        }
                        Thread.Sleep(100); // Wait before retrying
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Error($"Error during MoveFile for {sourceFilePath} to {destFilePath}: {ex}");
                        Plugin.ChatGui.PrintError($"[Sightseeingway] Error moving {Path.GetFileName(sourceFilePath)}: {ex.Message}");
                        return false; // Non-IO exception, stop retrying
                    }
                }
                else
                {
                    Plugin.Log.Warning($"File not released in time for move on attempt {i + 1}: {sourceFilePath}");
                    if (i == maxTries - 1) // Only print warning to chat on the last retry
                    {
                        Plugin.ChatGui.PrintError($"[Sightseeingway] Warning: File {Path.GetFileName(sourceFilePath)} not released in time for renaming after multiple attempts.");
                    }
                    return false; // File not released in time
                }
            }
            Plugin.Log.Warning($"MoveFileWithRetry failed after {maxTries} attempts for: {sourceFilePath} to {destFilePath}");
            return false; // Move failed after all retries
        }

        public static void RenameFile(string filePath)
        {
            Plugin.SendMessage($"RenameFile started for: {filePath}");

            Plugin.Framework.RunOnTick(() =>
            {
                try
                {
                    var newFilePath = ResolveNewFileName(filePath);
                    if (newFilePath == null) return;

                    Plugin.SendMessage($"Renaming '{filePath}' to '{newFilePath}'");

                    Caching.AddToRenameCache(Path.GetFileName(newFilePath));

                    QueueRenameOperation(filePath, newFilePath);

                    Plugin.Log.Information($"Renamed file to: {newFilePath}");
                    Client.PrintMessage($"Screenshot renamed: {Path.GetFileName(newFilePath)}");
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error($"Error during RenameFile for {filePath}: {ex}");
                    Plugin.ChatGui.PrintError($"[Sightseeingway] Error renaming {Path.GetFileName(filePath)}: {ex.Message}");
                }

                Plugin.SendMessage($"RenameFile finished for: {filePath}");
            }, default, 30);
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
            var presetNamePart = "";

            var fileName = Path.GetFileName(filePath);

            // Check if the filename starts with a timestamp; if so, just ignore it.
            if (fileName.Length >= 17 && DateTime.TryParseExact(fileName.Substring(0, 17), "yyyyMMddHHmmssfff", null, System.Globalization.DateTimeStyles.None, out _)) return null;

            var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

            if (!string.IsNullOrEmpty(character))
            {
                var mapExcelSheet = Plugin.DataManager.GetExcelSheet<Map>();
                if (mapExcelSheet != null && Plugin.ClientState.MapId > 0)
                {
                    var mapType = mapExcelSheet.GetRow(Plugin.ClientState.MapId);
                    map = mapType.PlaceName.Value.Name.ExtractText() ?? "";

                    Plugin.SendMessage($"Map name resolved: {map}");

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

            if (EffectsEnabled)
            {
                if (!string.IsNullOrEmpty(CurrentPresetName))
                {
                    presetNamePart = NamePart(CurrentPresetName);
                }
            }

            var newFilename = $"{timestamp}{NamePart(character)}{NamePart(map)}{position}{NamePart(eorzeaTime)}{NamePart(weather)}{presetNamePart}{fileExtension}";

            // Clean up invalid characters from the filename  
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                newFilename = newFilename.Replace(c.ToString(), "");
            }

            return Path.Combine(Path.GetDirectoryName(filePath), newFilename);
        }

        public static string NamePart(string part)
        {
            return string.IsNullOrEmpty(part) ? "" : "-" + part;
        }

        private static readonly ConcurrentQueue<(string SourceNamePath, string FinalName)> renameQueue = new();
        private static readonly Timer renameTimer = new(RenameQueuedFiles, null, Timeout.Infinite, Timeout.Infinite);

        public static void QueueRenameOperation(string sourceNamePath, string finalName)
        {
            renameQueue.Enqueue((sourceNamePath, finalName));
            renameTimer.Change(3000, Timeout.Infinite); // Wait for 3 seconds without writes
        }

        private static void RenameQueuedFiles(object? state)
        {
            while (renameQueue.TryDequeue(out var renameOperation))
            {
                if (WaitForFileReleaseGeneric(renameOperation.SourceNamePath, FileAccess.ReadWrite))
                {
                    try
                    {
                        MoveFileWithRetry(renameOperation.SourceNamePath, renameOperation.FinalName);
                        Plugin.Log.Information($"File renamed from {renameOperation.SourceNamePath} to {renameOperation.FinalName}");
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Error($"Error renaming file from {renameOperation.SourceNamePath} to {renameOperation.FinalName}: {ex}");
                        Plugin.ChatGui.PrintError($"[Sightseeingway] Error renaming {Path.GetFileName(renameOperation.SourceNamePath)}: {ex.Message}");
                    }
                }
                else
                {
                    Plugin.Log.Warning($"File not released in time for renaming: {renameOperation.SourceNamePath}");
                    Plugin.ChatGui.PrintError($"[Sightseeingway] Warning: File {Path.GetFileName(renameOperation.SourceNamePath)} not released in time for renaming.");
                }
            }
        }

    }

    public class ShadingwayState
    {
        public Effects? Effects { get; set; }
        public Preset? Preset { get; set; }
        public int Pid { get; set; }
    }

    public class Effects
    {
        public bool Enabled { get; set; }
        // Add other properties from "effects" if needed
    }

    public class Preset
    {
        public string? Collection { get; set; }
        public string? Name { get; set; }
        public string? Path { get; set; }
        // Add other properties from "preset" if needed
    }



}
