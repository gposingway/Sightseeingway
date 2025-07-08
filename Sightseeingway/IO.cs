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
using System.Linq;
using Sightseeingway.Results;

namespace Sightseeingway
{
    public static class IO
    {
        public static string? CurrentPresetName { get; private set; }
        public static bool EffectsEnabled { get; private set; }

        public static void SetupWatchers(List<string> foldersToMonitor, List<FileSystemWatcher> watchers)
        {
            foreach (var folder in foldersToMonitor)
            {
                if (!Directory.Exists(folder)) continue;

                var watcher = new FileSystemWatcher(folder) { EnableRaisingEvents = true };

                watcher.Created += OnFileCreated;
                watchers.Add(watcher);
            }
        }

        public static OperationResult<ShadingwayState> LoadShadingwayState(string filePath)
        {
            Plugin.Logger?.Debug($"LoadShadingwayState started for: {filePath}");
            
            var waitResult = WaitForFileReleaseGeneric(filePath, FileAccess.Read);
            if (!waitResult.IsSuccess)
            {
                return OperationResult<ShadingwayState>.Failure(waitResult.ErrorMessage ?? 
                    $"Shadingway state file not released in time for reading: {filePath}");
            }

            try
            {
                using (StreamReader file = File.OpenText(filePath))
                using (JsonTextReader reader = new JsonTextReader(file))
                {
                    var serializer = new JsonSerializer();
                    var state = serializer.Deserialize<ShadingwayState>(reader);
                    
                    if (state != null)
                    {
                        var jsonPid = state.Pid;
                        var currentPid = Process.GetCurrentProcess().Id;

                        if (jsonPid == currentPid)
                        {
                            EffectsEnabled = state.Effects?.Enabled ?? false;
                            CurrentPresetName = state.Preset?.Name;
                            Plugin.Logger?.Debug($"Shadingway State Parsed: EffectsEnabled={EffectsEnabled}, PresetName={CurrentPresetName}");
                        }
                        else
                        {
                            EffectsEnabled = false;
                            CurrentPresetName = null;
                        }
                        
                        return OperationResult<ShadingwayState>.Success(state);
                    }
                    else
                    {
                        return OperationResult<ShadingwayState>.Failure("Failed to deserialize Shadingway state");
                    }
                }
            }
            catch (Exception ex)
            {
                CurrentPresetName = null;
                EffectsEnabled = false;
                return OperationResult<ShadingwayState>.Failure(ex);
            }
        }

        public static OperationResult WaitForFileReleaseGeneric(string filePath, FileAccess fileAccess = FileAccess.Read)
        {
            Plugin.Logger?.Debug($"WaitForFileReleaseGeneric started for: {filePath}, FileAccess: {fileAccess}");
            
            for (var i = 0; i < Constants.FileOperations.MaxFileTries; ++i)
            {
                try
                {
                    Plugin.Logger?.Debug($"Attempt {i + 1}/{Constants.FileOperations.MaxFileTries} to open file: {filePath}");
                    using (var fs = File.Open(filePath, FileMode.Open, fileAccess, FileShare.ReadWrite))
                    {
                        Plugin.Logger?.Debug($"File opened successfully on attempt {i + 1}: {filePath}");
                        return OperationResult.Success();
                    }
                }
                catch (IOException)
                {
                    Thread.Sleep(Constants.FileOperations.FileReleaseWaitTimeMs);
                }
                catch (Exception ex)
                {
                    return OperationResult.Failure($"Error waiting for file release: {filePath}", ex);
                }
            }
            
            return OperationResult.Failure($"File not released after {Constants.FileOperations.MaxFileTries} attempts: {filePath}");
        }

        public static void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            var filePath = e.FullPath;
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (!Constants.FileOperations.SupportedImageExtensions.Contains(extension)) return;

            Plugin.Logger?.Debug($"File Created event triggered for: {filePath}");

            if (Caching.IsInRenameCache(Path.GetFileName(filePath)))
            {
                Plugin.Logger?.Debug($"File '{e.Name}' is in rename cache, ignoring.");
                return;
            }

            var waitResult = WaitForFileReleaseGeneric(filePath);
            if (waitResult.IsSuccess)
            {
                RenameFile(filePath);
            }
            else
            {
                Plugin.Logger?.Warning($"File not released in time for renaming: {filePath}");
            }
        }

        public static OperationResult MoveFileWithRetry(string sourceFilePath, string destFilePath)
        {
            Plugin.Logger?.Debug($"MoveFileWithRetry started from: {sourceFilePath} to {destFilePath}");
            
            for (int i = 0; i < Constants.FileOperations.MaxMoveTries; ++i)
            {
                var waitResult = WaitForFileReleaseGeneric(sourceFilePath, FileAccess.ReadWrite);
                if (waitResult.IsSuccess)
                {
                    try
                    {
                        Plugin.Logger?.Debug($"Attempt {i + 1}/{Constants.FileOperations.MaxMoveTries} to move file");
                        File.Move(sourceFilePath, destFilePath);
                        Plugin.Logger?.Information($"File moved successfully to: {destFilePath}");
                        return OperationResult.Success();
                    }
                    catch (IOException ex)
                    {
                        Plugin.Logger?.Debug($"IOException during MoveFile on attempt {i + 1}: {ex.Message}");
                        
                        if (i == Constants.FileOperations.MaxMoveTries - 1)
                        {
                            return OperationResult.Failure($"File locked after multiple attempts: {Path.GetFileName(sourceFilePath)}", ex);
                        }
                        
                        Thread.Sleep(Constants.FileOperations.MoveRetryWaitTimeMs);
                    }
                    catch (Exception ex)
                    {
                        return OperationResult.Failure($"Error moving file: {Path.GetFileName(sourceFilePath)}", ex);
                    }
                }
                else
                {
                    if (i == Constants.FileOperations.MaxMoveTries - 1)
                    {
                        return OperationResult.Failure(waitResult.ErrorMessage ?? 
                            $"File not released for move after {Constants.FileOperations.MaxMoveTries} attempts");
                    }
                }
            }
            
            return OperationResult.Failure($"Move operation failed after {Constants.FileOperations.MaxMoveTries} attempts");
        }

        public static void RenameFile(string filePath)
        {
            Plugin.Logger?.Debug($"RenameFile started for: {filePath}");

            Plugin.Framework.RunOnTick(() =>
            {
                try
                {
                    var newFilePath = ResolveNewFileName(filePath);
                    if (newFilePath == null) return;

                    Plugin.Logger?.Debug($"Renaming '{filePath}' to '{newFilePath}'");

                    Caching.AddToRenameCache(Path.GetFileName(newFilePath));
                    QueueRenameOperation(filePath, newFilePath);

                    Plugin.Logger?.Debug($"Queued file for rename to: {newFilePath}");
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.Error($"Error during RenameFile for {filePath}", ex, true);
                }

                Plugin.Logger?.Debug($"RenameFile finished for: {filePath}");
            }, default, 30);
        }

        public static string? ResolveNewFileName(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

            if (fileName.Length >= 17 && DateTime.TryParseExact(fileName.Substring(0, 17), Constants.Formats.CompactTimestamp, null, System.Globalization.DateTimeStyles.None, out _))
            {
                return null;
            }

            var config = Plugin.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            var activeFieldsInOrder = FilenameGenerator.StringToFieldList(config.SelectedFields);
            activeFieldsInOrder = FilenameGenerator.EnsureTimestampIsFirst(activeFieldsInOrder);

            var fileCreationTime = File.GetCreationTime(filePath);
            
            var character = Plugin.ClientState.LocalPlayer?.Name.TextValue ?? "";
            var map = "";
            var position = "";
            var eorzeaTime = "";
            var weather = "";
            var presetNamePart = "";

            if (!string.IsNullOrEmpty(character))
            {
                var mapExcelSheet = Plugin.DataManager.GetExcelSheet<Map>();
                if (mapExcelSheet != null && Plugin.ClientState.MapId > 0)
                {
                    var mapType = mapExcelSheet.GetRow(Plugin.ClientState.MapId);
                    if (mapType.RowId > 0)
                    {
                        try
                        {
                            var placeName = mapType.PlaceName.Value;
                            var extractedName = placeName.Name.ToString();
                            if (!string.IsNullOrEmpty(extractedName))
                            {
                                map = extractedName;
                            }
                        }
                        catch (Exception ex)
                        {
                            Plugin.Logger?.Debug($"Error extracting map name: {ex.Message}");
                        }

                        Plugin.Logger?.Debug($"Map name resolved: {map}");

                        try
                        {
                            var playerPos = Plugin.ClientState.LocalPlayer?.Position ?? Vector3.Zero;
                            var mapVector = MapUtil.WorldToMap(playerPos, mapType.OffsetX, mapType.OffsetY, 0, mapType.SizeFactor);
                            var mapPlace = new Vector3(
                                (int)MathF.Round(mapVector.X * 10, 1) / 10f,
                                (int)MathF.Round(mapVector.Y * 10, 1) / 10f,
                                (int)MathF.Round(mapVector.Z * 10, 1) / 10f
                            );
                            position = mapPlace == Vector3.Zero ? "" :
                                mapPlace.Z == 0.0 ? $" ({mapPlace.X:0.0},{mapPlace.Y:0.0})" :
                                $" ({mapPlace.X:0.0},{mapPlace.Y:0.0},{mapPlace.Z:0.0})";
                        }
                        catch (Exception ex)
                        {
                            Plugin.Logger?.Debug($"Error calculating coordinates: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Plugin.Logger?.Warning("Map sheet not found or MapId is 0.");
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
                    presetNamePart = CurrentPresetName;
                }
            }

            var constructedFilename = FilenameGenerator.GenerateFilename(
                fileCreationTime,
                config.TimestampFormat,
                character,
                map,
                position,
                eorzeaTime,
                weather,
                presetNamePart,
                EffectsEnabled,
                activeFieldsInOrder,
                fileExtension
            );

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                constructedFilename = constructedFilename.Replace(c.ToString(), "");
            }

            return Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, constructedFilename);
        }

        private static readonly ConcurrentQueue<(string SourceNamePath, string FinalName)> RenameQueue = new();
        private static readonly Timer RenameTimer = new(RenameQueuedFiles, null, Timeout.Infinite, Timeout.Infinite);

        public static void QueueRenameOperation(string sourceNamePath, string finalName)
        {
            RenameQueue.Enqueue((sourceNamePath, finalName));
            RenameTimer.Change(Constants.FileOperations.RenameQueueDelayMs, Timeout.Infinite);
        }

        private static void RenameQueuedFiles(object? state)
        {
            while (RenameQueue.TryDequeue(out var renameOperation))
            {
                var waitResult = WaitForFileReleaseGeneric(renameOperation.SourceNamePath, FileAccess.ReadWrite);
                if (waitResult.IsSuccess)
                {
                    var moveResult = MoveFileWithRetry(renameOperation.SourceNamePath, renameOperation.FinalName);
                    if (moveResult.IsSuccess)
                    {
                        // Get the config and check if we should show chat notifications
                        var config = Plugin.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
                        
                        Plugin.Logger?.Information($"File renamed from {renameOperation.SourceNamePath} to {renameOperation.FinalName}");
                        
                        if (config.ShowNameChangesInChat)
                        {
                            Client.PrintMessage($"Screenshot renamed: {Path.GetFileName(renameOperation.FinalName)}");
                        }
                    }
                    else
                    {
                        Plugin.Logger?.Error(moveResult.ErrorMessage ?? 
                            $"Unknown error renaming {Path.GetFileName(renameOperation.SourceNamePath)}", 
                            moveResult.Exception, true);
                    }
                }
                else
                {
                    Plugin.Logger?.Warning($"File not released in time for renaming: {renameOperation.SourceNamePath}");
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
    }

    public class Preset
    {
        public string? Collection { get; set; }
        public string? Name { get; set; }
        public string? Path { get; set; }
    }
}
