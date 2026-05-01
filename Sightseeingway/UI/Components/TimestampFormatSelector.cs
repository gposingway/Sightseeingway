using Dalamud.Bindings.ImGui;
using System;

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

            // Sample at render time so examples reflect the current moment.
            var now = DateTime.Now;
            ImGui.TextWrapped("Examples:");
            ImGui.TextColored(Constants.UI.ExampleColor, "Compact: " + FilenameGenerator.FormatTimestamp(now, TimestampFormat.Compact));
            ImGui.TextColored(Constants.UI.ExampleColor, "Regular: " + FilenameGenerator.FormatTimestamp(now, TimestampFormat.Regular));
            ImGui.TextColored(Constants.UI.ExampleColor, "Readable: " + FilenameGenerator.FormatTimestamp(now, TimestampFormat.Readable));

            return changed;
        }
    }
}
