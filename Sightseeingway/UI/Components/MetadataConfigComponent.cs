using Dalamud.Bindings.ImGui;
using System.Collections.Generic;

namespace Sightseeingway.UI.Components
{
    /// <summary>
    /// The Metadata column of the v1.3 config window. Master embed toggle on
    /// top, then three collapsible field groups (Scene / Character /
    /// Affiliation) each with a tri-state parent checkbox and a list of
    /// per-field child checkboxes.
    /// </summary>
    public class MetadataConfigComponent
    {
        private readonly Dictionary<string, bool> _groupExpanded = new()
        {
            ["Scene"] = true,
            ["Character"] = true,
            ["Affiliation"] = false,
        };

        private static readonly (string Group, MetadataField Field, string Label, string? Tooltip)[] Groups =
        {
            // Scene
            ("Scene", MetadataField.Location, "Location", "Territory, map, and map-space coordinates."),
            ("Scene", MetadataField.Time,     "Time",     "Eorzean time of day (period and hour)."),
            ("Scene", MetadataField.Weather,  "Weather",  "Current in-game weather."),
            ("Scene", MetadataField.Flags,    "Flags",    "Mode and state flags: gpose, mounted, swimming, etc."),
            ("Scene", MetadataField.Shader,   "Shader",   "Active Shadingway preset, when Shadingway is detected."),

            // Character
            ("Character", MetadataField.CharacterName,  "Name",                "Character name."),
            ("Character", MetadataField.CharacterWorld, "World",               "Current and home server."),
            ("Character", MetadataField.CharacterRace,  "Race / Tribe / Sex",  "Visual character identity."),
            ("Character", MetadataField.CharacterJob,   "Job / Level",         "Active class/job and level."),
            ("Character", MetadataField.CharacterTitle, "Title",               "Currently displayed title."),
            ("Character", MetadataField.CharacterMount, "Mount / Minion",      "Currently summoned mount and minion."),

            // Affiliation
            ("Affiliation", MetadataField.FreeCompany,  "Free Company",  "FC name and tag."),
            ("Affiliation", MetadataField.GrandCompany, "Grand Company", "GC affiliation and rank."),
        };

        /// <summary>
        /// Renders the entire metadata column. Returns true if any toggle
        /// changed during this draw.
        /// </summary>
        public bool Render(Configuration tempConfig)
        {
            var changed = false;

            // Master toggle.
            ImGui.TextColored(Constants.UI.HeaderColor, "Metadata");
            ImGui.Spacing();

            var embed = tempConfig.EmbedMetadata;
            if (ImGui.Checkbox("Embed metadata in screenshot files", ref embed))
            {
                tempConfig.EmbedMetadata = embed;
                changed = true;
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("Writes character, location, and shader information");
                ImGui.Text("into the image file (PNG iTXt or JPEG XMP).");
                ImGui.Text("Default off; toggle once you have reviewed the field selection below.");
                ImGui.EndTooltip();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.BeginDisabled(!tempConfig.EmbedMetadata);

            foreach (var groupName in new[] { "Scene", "Character", "Affiliation" })
            {
                if (RenderGroup(tempConfig, groupName))
                    changed = true;
            }

            ImGui.EndDisabled();
            return changed;
        }

        private bool RenderGroup(Configuration tempConfig, string groupName)
        {
            var changed = false;

            // Compute parent state.
            var (on, total) = CountChildren(tempConfig, groupName);
            var parentState = on == 0 ? CheckState.Unchecked
                            : on == total ? CheckState.Checked
                            : CheckState.Indeterminate;

            // Header row: chevron, tri-state checkbox, label.
            var expanded = _groupExpanded[groupName];
            if (ImGui.SmallButton(expanded ? $"v##{groupName}" : $">##{groupName}"))
                _groupExpanded[groupName] = !expanded;

            ImGui.SameLine();

            if (TriStateCheckbox.Draw($"##group_{groupName}", parentState))
            {
                var setTo = parentState == CheckState.Unchecked;
                foreach (var (group, field, _, _) in Groups)
                {
                    if (group != groupName) continue;
                    tempConfig.MetadataFields[field.ToString()] = setTo;
                }
                changed = true;
            }

            ImGui.SameLine();
            ImGui.Text(groupName);
            ImGui.SameLine();
            ImGui.TextColored(Constants.UI.InfoColor, $"({on}/{total})");

            // Children.
            if (_groupExpanded[groupName])
            {
                ImGui.Indent();
                foreach (var (group, field, label, tooltip) in Groups)
                {
                    if (group != groupName) continue;

                    var key = field.ToString();
                    var on_ = tempConfig.MetadataFields.GetValueOrDefault(key, false);
                    if (ImGui.Checkbox($"{label}##{key}", ref on_))
                    {
                        tempConfig.MetadataFields[key] = on_;
                        changed = true;
                    }

                    if (tooltip != null && ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text(tooltip);
                        ImGui.EndTooltip();
                    }
                }
                ImGui.Unindent();
            }

            ImGui.Spacing();
            return changed;
        }

        private static (int On, int Total) CountChildren(Configuration tempConfig, string groupName)
        {
            var on = 0;
            var total = 0;
            foreach (var (group, field, _, _) in Groups)
            {
                if (group != groupName) continue;
                total++;
                if (tempConfig.MetadataFields.GetValueOrDefault(field.ToString(), false)) on++;
            }
            return (on, total);
        }
    }
}
