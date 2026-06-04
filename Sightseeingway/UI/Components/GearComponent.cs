using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Sightseeingway.Gear;

namespace Sightseeingway.UI.Components
{
    /// <summary>
    /// "Gear" tab: opt-in to publish the player's visible gear to Shadingway, a live
    /// preview of exactly what is being published (icons / names / dyes), and the
    /// running publisher's detection/status.
    /// </summary>
    public class GearComponent
    {
        private static readonly Vector4 OkColor = new(0.40f, 0.90f, 0.40f, 1f);
        private static readonly Vector4 WarnColor = new(0.95f, 0.65f, 0.25f, 1f);

        // Cached preview snapshot, refreshed on a throttle (Draw runs on the framework
        // thread, so reading live game state here is safe — just not every frame).
        private IReadOnlyList<GearSlotData> _cached = Array.Empty<GearSlotData>();
        private long _lastRefreshTicks;
        private long _lastProbeTicks;

        public bool Render(Configuration tempConfig)
        {
            var changed = false;

            ImGui.TextColored(Constants.UI.FieldHeaderColor, "Gear → Shadingway (glamour texture bus)");
            ImGui.TextWrapped(
                "Publishes your currently visible gear — icons, names, rarity and dye colours — " +
                "as named textures to Shadingway, so ReShade shaders and presets can compose " +
                "glamour sheets. Content is static: it updates only when your visible gear changes.");
            ImGui.Spacing();

            var enabled = tempConfig.GearPublishEnabled;
            if (ImGui.Checkbox("Publish visible gear to Shadingway", ref enabled))
            {
                tempConfig.GearPublishEnabled = enabled;
                changed = true;
            }
            ImGui.SameLine();
            ImGui.TextColored(Constants.UI.InfoColor, "(?)");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(
                    "Requires the Shadingway ReShade addon running in this game client.\n" +
                    "Takes effect when you press Save. Textures then refresh automatically\n" +
                    "as your visible gear changes.");
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawStatus(tempConfig);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.TextColored(Constants.UI.FieldHeaderColor, "Current visible gear");
            ImGui.TextWrapped("Exactly what gets published — verify your glamour, icons and dyes here.");
            ImGui.Spacing();
            DrawGearPreview();

            ImGui.Spacing();
            if (ImGui.CollapsingHeader("Published texture names (per slot)"))
            {
                ImGui.BulletText("GLAM_<slot>_ICON  — item icon, native resolution");
                ImGui.BulletText("GLAM_<slot>_NAME0..3 — item name, white-on-transparent (128px)");
                ImGui.TextDisabled("    fonts: 0 Inter · 1 Cinzel · 2 EB Garamond · 3 Cormorant");
                ImGui.BulletText("GLAM_<slot>_RARITY — name colour swatch (8x8)");
                ImGui.BulletText("GLAM_<slot>_DYE1 / _DYE2 — dye colours (8x8; transparent if undyed)");
                ImGui.BulletText("GLAM_<slot>_DYE1NAME / _DYE2NAME — dye names (white-on-transparent)");
                ImGui.BulletText("GLAM_<slot>_CATEGORY — item category, e.g. \"Legs\" (white-on-transparent)");
                ImGui.BulletText("GLAM_<slot>_TAGS — \"Unique\" (transparent if not unique)");
                ImGui.BulletText("GLAM_<slot>_LEVELS — \"Lv. 90 · Ilvl 730\" (white-on-transparent)");
                ImGui.TextDisabled("Each also auto-reports its size via the shadingway metric uniform.");
            }

            return changed;
        }

        private void DrawGearPreview()
        {
            var now = Environment.TickCount64;
            if (now - _lastRefreshTicks > 500)
            {
                // null = unreliable read (transient); keep showing the last good snapshot.
                try { if (GearReader.ReadVisibleGear() is { } read) _cached = read; }
                catch (Exception ex) { Plugin.Logger?.Debug($"Gear preview read failed: {ex.Message}"); }
                _lastRefreshTicks = now;
            }

            if (_cached.Count == 0)
            {
                ImGui.TextDisabled("No visible gear detected (not logged in, or nothing equipped).");
                return;
            }

            const ImGuiTableFlags flags =
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingFixedFit;

            if (ImGui.BeginTable("##GearPreview", 4, flags))
            {
                ImGui.TableSetupColumn("Icon");
                ImGui.TableSetupColumn("Slot");
                ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Dyes");
                ImGui.TableHeadersRow();

                foreach (var slot in _cached)
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    DrawIcon(slot.IconId, 32f);

                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(slot.Slot.Key);

                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    var (r, g, b) = SwatchFactory.RarityColor(slot.Rarity);
                    ImGui.TextColored(new Vector4(r / 255f, g / 255f, b / 255f, 1f), slot.Name);
                    var sub = slot.Category;
                    if (!string.IsNullOrEmpty(slot.Levels)) sub += $"   ·   {slot.Levels}";
                    if (!string.IsNullOrEmpty(slot.Tags)) sub += $"   ·   {slot.Tags}";
                    if (!string.IsNullOrEmpty(sub)) ImGui.TextDisabled(sub);

                    ImGui.TableNextColumn();
                    DrawDye(slot.Stain0Color);
                    ImGui.SameLine();
                    DrawDye(slot.Stain1Color);
                }

                ImGui.EndTable();
            }
        }

        private static void DrawIcon(uint iconId, float size)
        {
            if (iconId != 0)
            {
                var shared = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId));
                var wrap = shared.GetWrapOrEmpty();
                ImGui.Image(wrap.Handle, new Vector2(size, size));
                return;
            }
            ImGui.Dummy(new Vector2(size, size));
        }

        private static void DrawDye(uint seColor)
        {
            var box = new Vector2(16f, 16f);
            if (seColor == 0)
            {
                ImGui.Dummy(box);
                return;
            }

            var (r, g, b) = SwatchFactory.SeColorToRgb(seColor);
            ImGui.ColorButton("##dye", new Vector4(r / 255f, g / 255f, b / 255f, 1f),
                ImGuiColorEditFlags.NoTooltip | ImGuiColorEditFlags.NoPicker, box);
        }

        private void DrawStatus(Configuration tempConfig)
        {
            var pub = Plugin.GearPublisher;
            if (pub == null)
            {
                ImGui.TextDisabled("Publisher not initialized.");
                return;
            }

            // Heartbeat: probe Shadingway while this tab is on screen, regardless of
            // the enable toggle, so detection always reflects reality.
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

            if (!tempConfig.GearPublishEnabled)
            {
                ImGui.TextDisabled("Publishing disabled — enable above and Save to start pushing.");
                return;
            }

            ImGui.Text($"Textures resident on bus: {pub.PushedCount}");
            ImGui.SameLine();
            if (ImGui.SmallButton("Re-publish now")) pub.RequestResync();
            ImGui.TextWrapped($"Status: {pub.StatusLine}");
        }
    }
}
