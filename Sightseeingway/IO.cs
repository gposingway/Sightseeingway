using Newtonsoft.Json;
using Sightseeingway.Metadata;
using Sightseeingway.Results;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sightseeingway
{
    public static class IO
    {
        public static string? CurrentPresetName { get; private set; }
        public static bool EffectsEnabled { get; private set; }

        public static MetadataPipeline? Pipeline { get; set; }

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
                using var file = File.OpenText(filePath);
                using var reader = new JsonTextReader(file);

                var serializer = new JsonSerializer();
                var state = serializer.Deserialize<ShadingwayState>(reader);

                if (state == null)
                    return OperationResult<ShadingwayState>.Failure("Failed to deserialize Shadingway state");

                if (state.Pid == Process.GetCurrentProcess().Id)
                {
                    EffectsEnabled = state.Effects?.Enabled ?? false;
                    CurrentPresetName = state.Preset?.Name;
                    Plugin.Logger?.Debug(
                        $"Shadingway State Parsed: EffectsEnabled={EffectsEnabled}, PresetName={CurrentPresetName}");
                }
                else
                {
                    EffectsEnabled = false;
                    CurrentPresetName = null;
                }

                return OperationResult<ShadingwayState>.Success(state);
            }
            catch (Exception ex)
            {
                CurrentPresetName = null;
                EffectsEnabled = false;
                return OperationResult<ShadingwayState>.Failure(ex);
            }
        }

        // Polls until the OS releases the file's exclusive lock or we exhaust the retry budget.
        // Runs on FileSystemWatcher / Timer / worker threads where blocking via Thread.Sleep is acceptable.
        public static OperationResult WaitForFileReleaseGeneric(string filePath, FileAccess fileAccess = FileAccess.Read)
        {
            for (var i = 0; i < Constants.FileOperations.MaxFileTries; ++i)
            {
                try
                {
                    using var fs = File.Open(filePath, FileMode.Open, fileAccess, FileShare.ReadWrite);
                    return OperationResult.Success();
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

            return OperationResult.Failure(
                $"File not released after {Constants.FileOperations.MaxFileTries} attempts: {filePath}");
        }

        public static OperationResult MoveFileWithRetry(string sourceFilePath, string destFilePath)
        {
            for (var i = 0; i < Constants.FileOperations.MaxMoveTries; ++i)
            {
                var waitResult = WaitForFileReleaseGeneric(sourceFilePath, FileAccess.ReadWrite);
                if (waitResult.IsSuccess)
                {
                    try
                    {
                        File.Move(sourceFilePath, destFilePath);
                        return OperationResult.Success();
                    }
                    catch (IOException ex)
                    {
                        if (i == Constants.FileOperations.MaxMoveTries - 1)
                            return OperationResult.Failure(
                                $"File locked after multiple attempts: {Path.GetFileName(sourceFilePath)}", ex);
                        Thread.Sleep(Constants.FileOperations.MoveRetryWaitTimeMs);
                    }
                    catch (Exception ex)
                    {
                        return OperationResult.Failure(
                            $"Error moving file: {Path.GetFileName(sourceFilePath)}", ex);
                    }
                }
                else if (i == Constants.FileOperations.MaxMoveTries - 1)
                {
                    return OperationResult.Failure(waitResult.ErrorMessage ??
                        $"File not released for move after {Constants.FileOperations.MaxMoveTries} attempts");
                }
            }

            return OperationResult.Failure(
                $"Move operation failed after {Constants.FileOperations.MaxMoveTries} attempts");
        }

        public static void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            var filePath = e.FullPath;
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            if (!Constants.FileOperations.SupportedImageExtensions.Contains(extension)) return;

            var fileName = Path.GetFileName(filePath);
            if (Caching.IsInRenameCache(fileName))
            {
                Plugin.Logger?.Debug($"File '{fileName}' is in rename cache, ignoring.");
                return;
            }

            // Skip files that already follow our timestamp-prefixed naming convention.
            if (LooksAlreadyRenamed(fileName)) return;

            var correlationId = Guid.CreateVersion7();
            Plugin.PipelineLog?.Info("fsw.created", correlationId, $"path={fileName}");

            // Pattern B: dispatch the snapshot capture to the framework tick before
            // waiting for file release, so state reflects the moment of the event.
            Plugin.Framework.RunOnTick(() =>
            {
                StateSnapshot snapshot;
                try
                {
                    Plugin.PipelineLog?.Debug("state.capture.start", correlationId);
                    var sw = Stopwatch.StartNew();
                    snapshot = StateCapture.Capture(correlationId);
                    sw.Stop();
                    Plugin.PipelineLog?.Info("state.capture.complete", correlationId,
                        $"duration_ms={sw.ElapsedMilliseconds}");
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.Error(
                        $"State capture failed for {fileName}", ex, correlationId: correlationId);
                    return;
                }

                // Hop off the framework thread for the I/O-bound rest of the work.
                Task.Run(() => HandleNewFile(filePath, snapshot, correlationId));
            });
        }

        private static void HandleNewFile(string originalPath, StateSnapshot snapshot, Guid correlationId)
        {
            try
            {
                var waitResult = WaitForFileReleaseGeneric(originalPath);
                if (!waitResult.IsSuccess)
                {
                    Plugin.PipelineLog?.Warn("wait.release.timeout", correlationId,
                        $"path={Path.GetFileName(originalPath)}");
                    Plugin.Logger?.Warning(
                        $"File not released in time for renaming: {originalPath}",
                        correlationId: correlationId);
                    return;
                }

                var targetPath = BuildTargetPath(originalPath, snapshot, Plugin.Config);
                if (string.Equals(targetPath, originalPath, StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.PipelineLog?.Debug("rename.skipped.identity", correlationId);
                    return;
                }

                // Persist the sidecar before the rename so durability covers any crash from here on.
                var task = new SidecarTask
                {
                    CorrelationId = correlationId,
                    OriginalPath = originalPath,
                    TargetPath = targetPath,
                    CreatedAt = DateTime.UtcNow,
                    Renamed = false,
                    Injected = false,
                    Snapshot = snapshot,
                };

                var initialSidecarPath = SidecarRepository.PathFor(originalPath);
                var writeResult = SidecarRepository.Write(initialSidecarPath, task);
                if (!writeResult.IsSuccess)
                {
                    Plugin.Logger?.Error(
                        $"Sidecar write failed for {Path.GetFileName(originalPath)}",
                        writeResult.Exception, correlationId: correlationId);
                    return;
                }
                Plugin.PipelineLog?.Info("sidecar.write", correlationId,
                    $"path={Path.GetFileName(initialSidecarPath)}");

                // Pre-cache the target name so the FSW.Created event for the renamed file ignores it.
                Caching.AddToRenameCache(Path.GetFileName(targetPath));

                var moveResult = MoveFileWithRetry(originalPath, targetPath);
                if (!moveResult.IsSuccess)
                {
                    Plugin.PipelineLog?.Error("rename.failed", correlationId,
                        $"from={Path.GetFileName(originalPath)} to={Path.GetFileName(targetPath)} " +
                        $"error={moveResult.ErrorMessage}");
                    Plugin.Logger?.Error(
                        moveResult.ErrorMessage ?? $"Unknown error renaming {Path.GetFileName(originalPath)}",
                        moveResult.Exception, correlationId: correlationId);
                    // Leave the sidecar so recovery can retry on next launch.
                    return;
                }

                Plugin.PipelineLog?.Info("rename.complete", correlationId,
                    $"from={Path.GetFileName(originalPath)} to={Path.GetFileName(targetPath)}");

                // Move the sidecar to follow the file.
                var finalSidecarPath = SidecarRepository.PathFor(targetPath);
                SidecarRepository.Move(initialSidecarPath, finalSidecarPath);
                Plugin.PipelineLog?.Info("sidecar.move", correlationId,
                    $"to={Path.GetFileName(finalSidecarPath)}");

                // Update the sidecar's renamed flag.
                SidecarRepository.Write(finalSidecarPath, task.With(renamed: true));

                if (Plugin.Logger != null && Plugin.Config.LogVerbosity != Services.LogVerbosity.Quiet)
                {
                    Plugin.Logger.UserMessage($"Screenshot renamed: {Path.GetFileName(targetPath)}");
                }

                // Wake the worker to perform metadata injection.
                Pipeline?.Signal();
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Error(
                    $"Unexpected failure handling {Path.GetFileName(originalPath)}", ex,
                    correlationId: correlationId);
            }
        }

        public static string BuildTargetPath(string originalPath, StateSnapshot snapshot, Configuration config)
        {
            var fileExtension = Path.GetExtension(originalPath).ToLowerInvariant();
            var fileCreationTime = File.GetCreationTime(originalPath);

            var character = snapshot.Character?.Name ?? string.Empty;
            var map = snapshot.Location?.Map?.Name ?? string.Empty;
            var position = FormatPositionForFilename(snapshot.Location?.Position);
            var eorzeaTime = snapshot.Time?.Eorzea?.Period ?? string.Empty;
            var weather = snapshot.Weather?.Name ?? string.Empty;
            var preset = snapshot.Shader?.Preset ?? string.Empty;
            var effectsEnabled = !string.IsNullOrEmpty(preset);

            var activeFields = FilenameGenerator.EnsureTimestampIsFirst(config.Fields);

            var constructedFilename = FilenameGenerator.GenerateFilename(
                fileCreationTime,
                config.TimestampFormat,
                character,
                map,
                position,
                eorzeaTime,
                weather,
                preset,
                effectsEnabled,
                activeFields,
                fileExtension);

            constructedFilename = StripInvalidFileNameChars(constructedFilename);
            return Path.Combine(Path.GetDirectoryName(originalPath) ?? string.Empty, constructedFilename);
        }

        private static string FormatPositionForFilename(Position? position)
        {
            if (position == null) return string.Empty;
            if (position.X == 0 && position.Y == 0 && position.Z == 0) return string.Empty;

            return position.Z == 0
                ? $" ({position.X.ToString("0.0", CultureInfo.InvariantCulture)},{position.Y.ToString("0.0", CultureInfo.InvariantCulture)})"
                : $" ({position.X.ToString("0.0", CultureInfo.InvariantCulture)},{position.Y.ToString("0.0", CultureInfo.InvariantCulture)},{position.Z.ToString("0.0", CultureInfo.InvariantCulture)})";
        }

        private static bool LooksAlreadyRenamed(string fileName)
        {
            if (fileName.Length < Constants.Formats.CompactTimestamp.Length) return false;
            return DateTime.TryParseExact(
                fileName.Substring(0, Constants.Formats.CompactTimestamp.Length),
                Constants.Formats.CompactTimestamp,
                null,
                DateTimeStyles.None,
                out _);
        }

        private static readonly HashSet<char> InvalidFileNameChars = new(Path.GetInvalidFileNameChars());

        private static string StripInvalidFileNameChars(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var buffer = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                if (!InvalidFileNameChars.Contains(c)) buffer.Append(c);
            }
            return buffer.Length == name.Length ? name : buffer.ToString();
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
