using Dalamud.Bindings.ImGui;
using Sightseeingway.Services;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace Sightseeingway.UI.Components
{
    /// <summary>
    /// Diagnostics block at the bottom of the config window: logging
    /// verbosity, live pipeline status, the recent-events ring buffer,
    /// and one-click support helpers.
    /// </summary>
    public class DiagnosticsComponent
    {
        private bool _eventsExpanded = false;

        /// <summary>
        /// Renders the diagnostics block. Returns true if the verbosity
        /// changed during this draw.
        /// </summary>
        public bool Render(Configuration tempConfig)
        {
            var changed = false;

            ImGui.TextColored(Constants.UI.SectionAccentColor, "Diagnostics");
            ImGui.Spacing();

            // Two labelled fields side by side, mirroring the way the metadata
            // groups present "label + control" rows.
            // Row: Verbosity:  [Quiet] [Status] [Debug]   Pipeline: Idle (0 pending)
            ImGui.Text("Verbosity:");
            ImGui.SameLine();
            var verbosity = tempConfig.LogVerbosity;
            if (VerbositySegmentedControl.Draw(ref verbosity))
            {
                tempConfig.LogVerbosity = verbosity;
                changed = true;
            }

            var pipeline = Plugin.MetadataPipeline;
            var pending = pipeline?.PendingCount ?? 0;
            var status = pipeline?.Status ?? "Idle";
            var pipelineText = $"Pipeline: {status} ({pending} pending)";
            var pipelineWidth = ImGui.CalcTextSize(pipelineText).X;
            var rightEdge = ImGui.GetContentRegionAvail().X;
            ImGui.SameLine(0, 24f);
            // Right-align the pipeline status to the column's trailing edge if there's room.
            var spaceForRightAlign = rightEdge - ImGui.GetCursorPosX() + ImGui.GetWindowContentRegionMin().X;
            if (spaceForRightAlign > pipelineWidth + 8f)
                ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - pipelineWidth);
            ImGui.TextColored(Constants.UI.InfoColor, pipelineText);

            ImGui.Spacing();

            // Action buttons all on one line, with the recent-events toggle leading.
            if (ImGui.Button(_eventsExpanded ? "Hide Recent Events" : "Show Recent Events"))
                _eventsExpanded = !_eventsExpanded;
            ImGui.SameLine();
            if (ImGui.Button("Open Log Folder")) OpenLogFolder();
            ImGui.SameLine();
            if (ImGui.Button("Copy Diagnostic Snapshot")) CopyDiagnosticSnapshot();

            // Recent events panel — already a bordered child window when shown,
            // so it provides its own visual frame below the actions.
            if (_eventsExpanded)
            {
                ImGui.Spacing();
                RenderRecentEvents();
            }

            return changed;
        }

        private static void RenderRecentEvents()
        {
            var pipelineLog = Plugin.PipelineLog;
            if (pipelineLog == null)
            {
                ImGui.TextDisabled("(pipeline log not initialised)");
                return;
            }

            var events = pipelineLog.RecentEvents();
            if (events.Count == 0)
            {
                ImGui.TextDisabled("(no events yet)");
                return;
            }

            ImGui.BeginChild("##RecentEvents", new Vector2(-1, 160), true);
            for (var i = events.Count - 1; i >= 0; i--)
            {
                var ev = events[i];
                var idShort = ev.CorrelationId?.ToString("D").Substring(0, 8) ?? "--------";
                var ts = ev.Timestamp.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                var color = LevelColor(ev.Level);
                ImGui.TextColored(color, $"{ts} {ev.Level,-5} {idShort} {ev.Event}");
                if (!string.IsNullOrEmpty(ev.Fields))
                {
                    ImGui.Indent();
                    ImGui.TextColored(Constants.UI.InfoColor, ev.Fields);
                    ImGui.Unindent();
                }
            }
            ImGui.EndChild();
        }

        private static Vector4 LevelColor(string level) => level switch
        {
            "ERROR" => new Vector4(1.0f, 0.4f, 0.4f, 1f),
            "WARN"  => new Vector4(1.0f, 0.8f, 0.3f, 1f),
            "DEBUG" => new Vector4(0.7f, 0.7f, 0.7f, 1f),
            _       => new Vector4(0.85f, 0.95f, 1.0f, 1f),
        };

        private static void OpenLogFolder()
        {
            try
            {
                var dir = Path.Combine(Plugin.PluginInterface.GetPluginConfigDirectory(), "logs");
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Error("Failed to open log folder", ex);
            }
        }

        private static void CopyDiagnosticSnapshot()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== Sightseeingway diagnostic snapshot ===");
                sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
                sb.AppendLine($"Plugin version: {GetPluginVersion()}");
                sb.AppendLine($"OS: {Environment.OSVersion}");
                sb.AppendLine($".NET: {Environment.Version}");
                sb.AppendLine();

                sb.AppendLine("Configuration:");
                var cfg = Plugin.Config;
                if (cfg != null)
                {
                    sb.AppendLine($"  EmbedMetadata: {cfg.EmbedMetadata}");
                    sb.AppendLine($"  LogVerbosity:  {cfg.LogVerbosity}");
                    sb.AppendLine($"  TimestampFormat: {cfg.TimestampFormat}");
                    sb.AppendLine($"  SelectedFields: {cfg.SelectedFields}");
                    sb.AppendLine($"  MetadataFields enabled: {string.Join(", ", EnabledMetadataFields(cfg))}");
                }

                sb.AppendLine();
                sb.AppendLine("Recent pipeline events:");
                var events = Plugin.PipelineLog?.RecentEvents();
                if (events == null || events.Count == 0)
                {
                    sb.AppendLine("  (none)");
                }
                else
                {
                    foreach (var ev in events)
                    {
                        var idStr = ev.CorrelationId?.ToString("D") ?? "-";
                        sb.Append(ev.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture));
                        sb.Append(' ').Append(ev.Level.PadRight(5));
                        sb.Append(' ').Append(ev.Event.PadRight(24));
                        sb.Append(" id=").Append(idStr);
                        if (!string.IsNullOrEmpty(ev.Fields)) sb.Append(' ').Append(ev.Fields);
                        sb.AppendLine();
                    }
                }

                ImGui.SetClipboardText(sb.ToString());
                Plugin.Logger?.UserMessage("Diagnostic snapshot copied to clipboard.");
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Error("Failed to copy diagnostic snapshot", ex);
            }
        }

        private static string GetPluginVersion()
        {
            try
            {
                var asm = typeof(Plugin).Assembly;
                var name = asm.GetName();
                return name.Version?.ToString() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private static System.Collections.Generic.IEnumerable<string> EnabledMetadataFields(Configuration cfg)
        {
            foreach (var kv in cfg.MetadataFields)
            {
                if (kv.Value) yield return kv.Key;
            }
        }
    }
}
