using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Sightseeingway.CharacterCard;

namespace Sightseeingway.UI.Components
{
    /// <summary>
    /// "Character" tab: opt-in to publish the local character's identity + appearance to
    /// Shadingway, a live preview of what's captured, and the running publisher's status.
    /// </summary>
    public class CharacterComponent
    {
        private static readonly Vector4 OkColor = new(0.40f, 0.90f, 0.40f, 1f);
        private static readonly Vector4 WarnColor = new(0.95f, 0.65f, 0.25f, 1f);

        private CharSnapshot? _cached;
        private long _lastRefreshTicks;
        private long _lastProbeTicks;

        public bool Render(Configuration tempConfig)
        {
            var changed = false;

            ImGui.TextColored(Constants.UI.FieldHeaderColor, "Character → Shadingway (identity + appearance)");
            ImGui.TextWrapped(
                "Publishes your character — name, world, race/clan/gender, job, and the customize " +
                "sliders and colours — as CHAR_* textures and uniforms, so ReShade shaders and " +
                "presets can compose a character card. On by default; updates only when your " +
                "appearance changes.");
            ImGui.Spacing();

            var enabled = tempConfig.CharacterPublishEnabled;
            if (ImGui.Checkbox("Publish character to Shadingway", ref enabled))
            {
                tempConfig.CharacterPublishEnabled = enabled;
                changed = true;
            }
            ImGui.SameLine();
            ImGui.TextColored(Constants.UI.InfoColor, "(?)");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(
                    "Requires the Shadingway ReShade addon running in this game client.\n" +
                    "On by default; takes effect on Save. Publishes identity, the customize\n" +
                    "sliders + colours, and their captions as CHAR_* textures and uniforms.");

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawStatus();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(Constants.UI.FieldHeaderColor, "Current character");
            DrawPreview();

            return changed;
        }

        private void DrawPreview()
        {
            var now = Environment.TickCount64;
            if (now - _lastRefreshTicks > 500)
            {
                try { _cached = CharReader.ReadSnapshot() ?? _cached; }
                catch (Exception ex) { Plugin.Logger?.Debug($"Char preview read failed: {ex.Message}"); }
                _lastRefreshTicks = now;
            }

            var snap = _cached;
            if (snap == null || string.IsNullOrEmpty(snap.Name))
            {
                ImGui.TextDisabled("No character detected (not logged in).");
                return;
            }

            ImGui.TextUnformatted(snap.Name);
            ImGui.TextDisabled($"{snap.RaceName} · {snap.ClanName} · {snap.GenderName}");
            ImGui.TextDisabled($"{snap.CurrentWorld} ({snap.DataCenter}) · {snap.JobName}");
            if (!string.IsNullOrEmpty(snap.GcName))
                ImGui.TextDisabled($"{snap.GcName} · rank {snap.GcRank}");

            ImGui.Spacing();
            ImGui.TextDisabled(
                $"{snap.Numbers.Count} numeric options + {snap.Flags.Count} toggles → uniforms; " +
                "name in 4 fonts + identity labels → textures");
        }

        private void DrawStatus()
        {
            var pub = Plugin.CharacterPublisher;
            if (pub == null)
            {
                ImGui.TextDisabled("Publisher not initialized.");
                return;
            }

            var now = Environment.TickCount64;
            if (now - _lastProbeTicks > 2500)
            {
                _ = pub.ProbeAsync();
                _lastProbeTicks = now;
            }

            ImGui.TextUnformatted("Shadingway:");
            ImGui.SameLine();
            if (pub.ShadingwayDetected)
                ImGui.TextColored(OkColor, pub.DiscoveredPort is { } port ? $"detected on :{port}" : "detected");
            else
                ImGui.TextColored(WarnColor, "not found (is the addon running?)");
            ImGui.SameLine();
            if (ImGui.SmallButton("Re-check"))
            {
                _ = pub.ProbeAsync();
                _lastProbeTicks = now;
            }

            ImGui.Text($"Resident on bus: {pub.PushedCount}");
            ImGui.SameLine();
            if (ImGui.SmallButton("Re-publish now")) pub.RequestResync();
            ImGui.TextWrapped($"Status: {pub.StatusLine}");
        }
    }
}
