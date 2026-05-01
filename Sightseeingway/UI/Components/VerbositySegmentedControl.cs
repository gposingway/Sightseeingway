using Dalamud.Bindings.ImGui;
using Sightseeingway.Services;
using System.Numerics;

namespace Sightseeingway.UI.Components
{
    /// <summary>
    /// Three-button segmented control for picking <see cref="LogVerbosity"/>.
    /// The currently-selected option renders with the "active" button colour
    /// so the user can see their choice without scanning labels.
    /// </summary>
    public static class VerbositySegmentedControl
    {
        private static readonly (LogVerbosity Value, string Label, string Tooltip)[] Options =
        {
            (LogVerbosity.Quiet,  "Quiet",  "Errors only. No notifications about renames or metadata."),
            (LogVerbosity.Status, "Status", "Errors plus rename and metadata milestones. Default."),
            (LogVerbosity.Debug,  "Debug",  "Full pipeline trace in chat. High volume; useful for bug reports."),
        };

        /// <summary>
        /// Renders the control. Returns true if the user picked a different
        /// value, with <paramref name="value"/> updated to the new choice.
        /// </summary>
        public static bool Draw(ref LogVerbosity value)
        {
            var changed = false;

            for (var i = 0; i < Options.Length; i++)
            {
                var (option, label, tooltip) = Options[i];
                var isActive = option == value;

                if (isActive)
                    ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));

                if (ImGui.Button(label))
                {
                    if (option != value)
                    {
                        value = option;
                        changed = true;
                    }
                }

                if (isActive) ImGui.PopStyleColor();

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text(tooltip);
                    ImGui.EndTooltip();
                }

                if (i < Options.Length - 1) ImGui.SameLine(0, 4f);
            }

            return changed;
        }
    }
}
