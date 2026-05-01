using Sightseeingway.Metadata.Writers;
using Sightseeingway.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Sightseeingway.Metadata
{
    /// <summary>
    /// Orchestrator for the screenshot metadata pipeline.
    ///
    /// Owns the dedicated background worker thread, the wake signal, and the
    /// recovery scan logic. Sidecars on disk are the durable queue; this class
    /// is the runtime view of "what to do with them next."
    ///
    /// Resolves a format-specific <see cref="IMetadataWriter"/> per task and
    /// invokes it once the file is released; the writer itself is pure and
    /// performs the atomic on-disk replacement.
    /// </summary>
    public sealed class MetadataPipeline : IDisposable
    {
        private static readonly TimeSpan WorkerWakeTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan OrphanGracePeriod = TimeSpan.FromSeconds(60);

        private readonly PipelineLog _log;
        private readonly Func<IReadOnlyCollection<string>> _monitoredDirectoriesProvider;
        private readonly ManualResetEventSlim _wake = new(initialState: false);
        private readonly CancellationTokenSource _cts = new();
        private Thread? _workerThread;
        private int _pending;
        private bool _disposed;

        public int PendingCount => Volatile.Read(ref _pending);
        public string Status { get; private set; } = "Idle";

        public MetadataPipeline(
            PipelineLog log,
            Func<IReadOnlyCollection<string>> monitoredDirectoriesProvider)
        {
            _log = log;
            _monitoredDirectoriesProvider = monitoredDirectoriesProvider;
        }

        public void Start()
        {
            if (_workerThread != null) return;

            _workerThread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "Sightseeingway.MetadataWorker",
            };
            _workerThread.Start();
            _log.Info("worker.start");

            // Kick off the first scan to pick up any pending sidecars left from a previous run.
            Signal();
        }

        /// <summary>
        /// Wakes the worker. Safe to call from any thread, idempotent.
        /// </summary>
        public void Signal() => _wake.Set();

        private void WorkerLoop()
        {
            var token = _cts.Token;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    _wake.Wait(WorkerWakeTimeout, token);
                    _wake.Reset();
                    if (token.IsCancellationRequested) break;

                    DrainPendingSidecars(token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.Error("worker.loop.exception", fields: $"exception={ex.GetType().Name}: {ex.Message}");
                    // Don't let a transient failure kill the worker.
                    Thread.Sleep(1000);
                }
            }

            _log.Info("worker.stop");
        }

        private void DrainPendingSidecars(CancellationToken token)
        {
            var dirs = _monitoredDirectoriesProvider();
            if (dirs.Count == 0) return;

            var found = 0;
            var processed = 0;
            var orphans = 0;

            foreach (var dir in dirs)
            {
                if (token.IsCancellationRequested) break;
                if (!Directory.Exists(dir)) continue;

                IEnumerable<string> sidecarFiles;
                try
                {
                    sidecarFiles = Directory.EnumerateFiles(dir, "*" + SidecarTask.Suffix);
                }
                catch (Exception ex)
                {
                    _log.Warn("worker.scan.failed", fields: $"dir={dir} exception={ex.Message}");
                    continue;
                }

                foreach (var sidecarPath in sidecarFiles)
                {
                    if (token.IsCancellationRequested) break;
                    found++;

                    var outcome = ProcessSidecar(sidecarPath);
                    if (outcome == ProcessOutcome.Processed) processed++;
                    else if (outcome == ProcessOutcome.OrphanCleaned) orphans++;
                }
            }

            UpdatePending(0);
            Status = "Idle";

            if (found > 0)
                _log.Info("worker.drain.complete",
                    fields: $"found={found} processed={processed} orphans_cleaned={orphans}");
        }

        private enum ProcessOutcome { Processed, OrphanCleaned, Skipped }

        private ProcessOutcome ProcessSidecar(string sidecarPath)
        {
            var readResult = SidecarRepository.Read(sidecarPath);
            if (!readResult.IsSuccess || readResult.Data == null)
            {
                _log.Warn("sidecar.read.failed", fields: $"path={sidecarPath} error={readResult.ErrorMessage}");
                return ProcessOutcome.Skipped;
            }

            var task = readResult.Data;
            UpdatePending(1);
            Status = $"Processing {Path.GetFileName(task.TargetPath)}";

            try
            {
                // Determine where the .png actually is right now (FS is ground truth).
                var atTarget = File.Exists(task.TargetPath);
                var atOriginal = !string.Equals(task.OriginalPath, task.TargetPath, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(task.OriginalPath);

                // Orphan: target absent for >grace period and original absent.
                if (!atTarget && !atOriginal)
                {
                    var age = DateTime.UtcNow - task.CreatedAt;
                    if (age > OrphanGracePeriod)
                    {
                        SidecarRepository.Delete(sidecarPath);
                        _log.Info("sidecar.orphan.cleaned", task.CorrelationId, $"path={sidecarPath} age_s={(int)age.TotalSeconds}");
                        return ProcessOutcome.OrphanCleaned;
                    }
                    return ProcessOutcome.Skipped;
                }

                // Step 1: rename. If file is at original, perform the move now.
                if (atOriginal)
                {
                    var moveResult = IO.MoveFileWithRetry(task.OriginalPath, task.TargetPath);
                    if (!moveResult.IsSuccess)
                    {
                        _log.Error("rename.failed", task.CorrelationId,
                            $"from={task.OriginalPath} to={task.TargetPath} error={moveResult.ErrorMessage}");
                        return ProcessOutcome.Skipped;
                    }
                    _log.Info("rename.complete", task.CorrelationId,
                        $"from={Path.GetFileName(task.OriginalPath)} to={Path.GetFileName(task.TargetPath)}");

                    // Move the sidecar to track the file's new location.
                    var newSidecarPath = SidecarRepository.PathFor(task.TargetPath);
                    SidecarRepository.Move(sidecarPath, newSidecarPath);
                    sidecarPath = newSidecarPath;
                    task = task.With(renamed: true);
                    SidecarRepository.Write(sidecarPath, task);
                }
                else if (atTarget && !task.Renamed)
                {
                    // Crash mid-rename: file already moved, sidecar may be at original location.
                    var expectedSidecar = SidecarRepository.PathFor(task.TargetPath);
                    if (!string.Equals(sidecarPath, expectedSidecar, StringComparison.OrdinalIgnoreCase))
                    {
                        SidecarRepository.Move(sidecarPath, expectedSidecar);
                        sidecarPath = expectedSidecar;
                    }
                    task = task.With(renamed: true);
                    SidecarRepository.Write(sidecarPath, task);
                }

                // Step 2: inject. Only proceed if the user has opted in via config.
                if (!task.Injected)
                {
                    if (!ShouldInject())
                    {
                        // Embedding is off; treat as injected so the sidecar gets cleaned up.
                        _log.Debug("injection.skipped.disabled", task.CorrelationId);
                        task = task.With(injected: true);
                    }
                    else
                    {
                        var injected = InjectMetadata(task);
                        if (!injected)
                        {
                            // Leave the sidecar so the next pass (or next launch) can retry.
                            return ProcessOutcome.Skipped;
                        }
                        task = task.With(injected: true);
                    }
                }

                // Step 3: cleanup.
                SidecarRepository.Delete(sidecarPath);
                _log.Info("sidecar.delete", task.CorrelationId);
                return ProcessOutcome.Processed;
            }
            finally
            {
                UpdatePending(-1);
            }
        }

        private static bool ShouldInject() => Plugin.Config?.EmbedMetadata ?? false;

        private bool InjectMetadata(SidecarTask task)
        {
            var writer = MetadataWriterRegistry.GetFor(task.TargetPath);
            if (writer == null)
            {
                _log.Warn("injection.unsupported_format", task.CorrelationId,
                    $"path={Path.GetFileName(task.TargetPath)}");
                // Treat unsupported formats as a successful no-op so the sidecar is cleaned up.
                return true;
            }

            // Wait for any external lock to release before opening for read+write.
            var waitResult = IO.WaitForFileReleaseGeneric(task.TargetPath, FileAccess.ReadWrite);
            if (!waitResult.IsSuccess)
            {
                _log.Warn("wait.release.timeout", task.CorrelationId,
                    $"path={Path.GetFileName(task.TargetPath)} stage=injection");
                return false;
            }

            _log.Info("injection.start", task.CorrelationId, $"writer={writer.Name}");
            var sw = Stopwatch.StartNew();
            long bytesIn = 0;
            try { bytesIn = new FileInfo(task.TargetPath).Length; } catch { }

            var snapshotForFile = task.Snapshot.FilteredFor(Plugin.Config);
            var result = writer.Write(task.TargetPath, snapshotForFile, _cts.Token);
            sw.Stop();

            if (!result.IsSuccess)
            {
                _log.Error("injection.failed", task.CorrelationId,
                    $"writer={writer.Name} error={result.ErrorMessage}");
                return false;
            }

            long bytesOut = 0;
            try { bytesOut = new FileInfo(task.TargetPath).Length; } catch { }

            _log.Info("injection.complete", task.CorrelationId,
                $"writer={writer.Name} duration_ms={sw.ElapsedMilliseconds} " +
                $"bytes_in={bytesIn} bytes_out={bytesOut}");

            return true;
        }

        private void UpdatePending(int delta)
        {
            if (delta > 0) Interlocked.Add(ref _pending, delta);
            else if (delta < 0) Interlocked.Add(ref _pending, delta);
            else Interlocked.Exchange(ref _pending, 0);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _cts.Cancel();
                _wake.Set();
                _workerThread?.Join(TimeSpan.FromSeconds(5));
            }
            catch { /* shutdown best-effort */ }

            _wake.Dispose();
            _cts.Dispose();
        }
    }
}
