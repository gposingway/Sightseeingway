using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Sightseeingway.Services
{
    /// <summary>
    /// Verbosity level for plugin chat output. Disk telemetry via PipelineLog
    /// is always rich; this enum only gates what reaches in-game chat.
    /// </summary>
    public enum LogVerbosity
    {
        Quiet,
        Status,
        Debug,
    }

    /// <summary>
    /// Single recorded pipeline event. Held in the ring buffer for the
    /// in-window Diagnostics panel and written verbatim to the rolling log file.
    /// </summary>
    public sealed record DiagnosticEvent(
        DateTime Timestamp,
        string Level,
        Guid? CorrelationId,
        string Event,
        string Fields);

    /// <summary>
    /// Dedicated, structured, rolling log for the metadata pipeline.
    ///
    /// Writes always-rich plain-text entries to a daily-rotated file under
    /// &lt;PluginConfigDir&gt;/logs/pipeline-yyyy-MM-dd.log, regardless of
    /// chat verbosity. Each entry is also pushed onto an in-memory ring
    /// buffer that backs the Diagnostics panel.
    /// </summary>
    public sealed class PipelineLog : IDisposable
    {
        private const int RingBufferCapacity = 50;
        private const long FileSizeSoftCapBytes = 10L * 1024 * 1024;
        private const int RetentionDays = 7;

        private readonly string _logsDirectory;
        private readonly object _writeLock = new();
        private readonly ConcurrentQueue<DiagnosticEvent> _ringBuffer = new();
        private bool _disposed;

        public PipelineLog(string pluginConfigDirectory)
        {
            _logsDirectory = Path.Combine(pluginConfigDirectory, "logs");
            Directory.CreateDirectory(_logsDirectory);
            PruneOldFiles();
        }

        public IReadOnlyList<DiagnosticEvent> RecentEvents() => _ringBuffer.ToArray();

        public void Info(string evt, Guid? id = null, string? fields = null) =>
            Write("INFO", evt, id, fields);

        public void Debug(string evt, Guid? id = null, string? fields = null) =>
            Write("DEBUG", evt, id, fields);

        public void Warn(string evt, Guid? id = null, string? fields = null) =>
            Write("WARN", evt, id, fields);

        public void Error(string evt, Guid? id = null, string? fields = null) =>
            Write("ERROR", evt, id, fields);

        private void Write(string level, string evt, Guid? id, string? fields)
        {
            if (_disposed) return;

            var entry = new DiagnosticEvent(
                DateTime.UtcNow,
                level,
                id,
                evt,
                fields ?? string.Empty);

            _ringBuffer.Enqueue(entry);
            while (_ringBuffer.Count > RingBufferCapacity && _ringBuffer.TryDequeue(out _)) { }

            try
            {
                lock (_writeLock)
                {
                    var line = Format(entry);
                    var path = CurrentLogPath();
                    File.AppendAllText(path, line + Environment.NewLine);
                }
            }
            catch
            {
                // Log writes must never throw into the pipeline.
            }
        }

        private string CurrentLogPath()
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var basePath = Path.Combine(_logsDirectory, $"pipeline-{today}.log");

            if (!File.Exists(basePath) || new FileInfo(basePath).Length < FileSizeSoftCapBytes)
                return basePath;

            for (var i = 1; i < 1000; i++)
            {
                var rolled = Path.Combine(_logsDirectory, $"pipeline-{today}-{i:00}.log");
                if (!File.Exists(rolled) || new FileInfo(rolled).Length < FileSizeSoftCapBytes)
                    return rolled;
            }

            // Pathological case; fall back to base path and accept oversized file.
            return basePath;
        }

        private static string Format(DiagnosticEvent e)
        {
            var sb = new StringBuilder(160);
            sb.Append(e.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture));
            sb.Append(' ').Append(e.Level.PadRight(5));
            sb.Append(' ').Append(e.Event.PadRight(24));
            if (e.CorrelationId.HasValue)
                sb.Append(" id=").Append(e.CorrelationId.Value.ToString("D"));
            if (!string.IsNullOrEmpty(e.Fields))
                sb.Append(' ').Append(e.Fields);
            return sb.ToString();
        }

        private void PruneOldFiles()
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
                foreach (var file in Directory.EnumerateFiles(_logsDirectory, "pipeline-*.log"))
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                        File.Delete(file);
                }
            }
            catch
            {
                // Best effort; never throw from log housekeeping.
            }
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
