using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace Sightseeingway.UI.Components
{
    /// <summary>
    /// "Gear" tab: opt-in to publish the player's visible gear to Shadingway, with
    /// live detection/status from the running <see cref="Gear.GearPublisher"/>.
    /// </summary>
    public class GearComponent
    {
        private static readonly Vector4 OkColor = new(0.40f, 0.90f, 0.40f, 1f);
        private static readonly Vector4 WarnColor = new(0.95f, 0.65f, 0.25f, 1f);

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

            ImGui.TextDisabled("Published per equipped slot:");
            ImGui.BulletText("GLAM_<slot>_ICON  — item icon, native resolution");
            ImGui.BulletText("GLAM_<slot>_NAME  — item name (r8 coverage, tint in-shader)");
            ImGui.BulletText("GLAM_<slot>_RARITY — name colour swatch (8x8)");
            ImGui.BulletText("GLAM_<slot>_DYE1 / _DYE2 — dye channel colours (8x8)");
            ImGui.Spacing();
            ImGui.TextDisabled("Each texture also auto-reports its size via the shadingway metric uniform.");

            return changed;
        }

        private static void DrawStatus(Configuration tempConfig)
        {
            var pub = Plugin.GearPublisher;
            if (pub == null)
            {
                ImGui.TextDisabled("Publisher not initialized.");
                return;
            }

            if (!tempConfig.GearPublishEnabled)
            {
                ImGui.TextDisabled("Disabled — enable above and Save to start publishing.");
                return;
            }

            ImGui.TextUnformatted("Shadingway:");
            ImGui.SameLine();
            if (pub.ShadingwayDetected)
                ImGui.TextColored(OkColor, "detected");
            else
                ImGui.TextColored(WarnColor, "not found (is the addon running?)");

            ImGui.Text($"Textures resident on bus: {pub.PushedCount}");
            ImGui.TextWrapped($"Status: {pub.StatusLine}");
        }
    }
}
