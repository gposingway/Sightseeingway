using Newtonsoft.Json;
using Sightseeingway.Results;
using System;
using System.IO;

namespace Sightseeingway.Metadata
{
    /// <summary>
    /// Pure persistence layer for <see cref="SidecarTask"/> JSON files.
    ///
    /// All operations are best-effort and return <see cref="OperationResult"/>
    /// rather than throwing — callers (the pipeline) decide whether a failure
    /// is fatal or recoverable.
    /// </summary>
    public static class SidecarRepository
    {
        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        };

        public static string PathFor(string targetFilePath) => targetFilePath + SidecarTask.Suffix;

        public static OperationResult Write(string sidecarPath, SidecarTask task)
        {
            try
            {
                var json = JsonConvert.SerializeObject(task, SerializerSettings);
                var tmp = sidecarPath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(sidecarPath)) File.Delete(sidecarPath);
                File.Move(tmp, sidecarPath);
                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                return OperationResult.Failure($"Failed to write sidecar at {sidecarPath}", ex);
            }
        }

        public static OperationResult<SidecarTask> Read(string sidecarPath)
        {
            try
            {
                if (!File.Exists(sidecarPath))
                    return OperationResult<SidecarTask>.Failure($"Sidecar not found: {sidecarPath}");

                var json = File.ReadAllText(sidecarPath);
                var task = JsonConvert.DeserializeObject<SidecarTask>(json);
                if (task == null)
                    return OperationResult<SidecarTask>.Failure($"Sidecar deserialized to null: {sidecarPath}");

                return OperationResult<SidecarTask>.Success(task);
            }
            catch (Exception ex)
            {
                return OperationResult<SidecarTask>.Failure(ex);
            }
        }

        public static OperationResult Move(string fromPath, string toPath)
        {
            try
            {
                if (string.Equals(fromPath, toPath, StringComparison.OrdinalIgnoreCase))
                    return OperationResult.Success();
                if (!File.Exists(fromPath))
                    return OperationResult.Failure($"Sidecar to move does not exist: {fromPath}");

                if (File.Exists(toPath)) File.Delete(toPath);
                File.Move(fromPath, toPath);
                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                return OperationResult.Failure($"Failed to move sidecar {fromPath} → {toPath}", ex);
            }
        }

        public static OperationResult Delete(string sidecarPath)
        {
            try
            {
                if (File.Exists(sidecarPath)) File.Delete(sidecarPath);
                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                return OperationResult.Failure($"Failed to delete sidecar {sidecarPath}", ex);
            }
        }
    }
}
