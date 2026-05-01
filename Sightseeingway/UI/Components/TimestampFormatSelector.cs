using Dalamud.Bindings.ImGui;

namespace Sightseeingway.UI.Components
{
    /// <summary>
    /// A UI component for selecting timestamp formats
    /// </summary>
    public class TimestampFormatSelector
    {
        private readonly string[] _formatOptions =
        {
            "Compact (yyyyMMddHHmmssfff)",
            "Regular (yyyyMMdd-HHmmss-fff)",
            "Readable (yyyy-MM-dd_HH-mm-ss.fff)"
        };

        public bool Render(ref TimestampFormat currentFormat)
        {
            var changed = false;

            ImGui.TextColored(Constants.UI.HeaderColor, "Timestamp");
            ImGui.Spacing();

            var formatIndex = (int)currentFormat;
            if (ImGui.Combo("Format", ref formatIndex, _formatOptions, _formatOptions.Length))
            {
                currentFormat = (TimestampFormat)formatIndex;
                changed = true;
            }

            // Tiny bottom pad so this column's separator lines up with the
            // master-toggle separator on the metadata column. Use cursor math
            // rather than Dummy() so we don't pick up ImGui's ItemSpacing
            // surcharge on top of the requested pixels.
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 3);

            return changed;
        }
    }
}
