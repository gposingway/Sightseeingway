// filepath: f:\Replica\NAS\Files\repo\github\Sightseeingway\Sightseeingway\UI\Components\TimestampFormatSelector.cs
using ImGuiNET;
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
        
        private readonly string[] _formatExamples;
        private readonly DateTime _sampleTimestamp;
        
        public TimestampFormatSelector()
        {
            _sampleTimestamp = DateTime.Now;
            _formatExamples = new string[] 
            {
                FilenameGenerator.FormatTimestamp(_sampleTimestamp, TimestampFormat.Compact),
                FilenameGenerator.FormatTimestamp(_sampleTimestamp, TimestampFormat.Regular),
                FilenameGenerator.FormatTimestamp(_sampleTimestamp, TimestampFormat.Readable)
            };
        }
        
        /// <summary>
        /// Renders the timestamp format selector UI
        /// </summary>
        /// <param name="currentFormat">The current timestamp format</param>
        /// <returns>True if the format was changed, otherwise false</returns>
        public bool Render(ref TimestampFormat currentFormat)
        {
            bool changed = false;
            
            ImGui.TextColored(Constants.UI.HeaderColor, "Timestamp");
            ImGui.Spacing();
            
            int formatIndex = (int)currentFormat;
            if (ImGui.Combo("Format", ref formatIndex, _formatOptions, _formatOptions.Length))
            {
                currentFormat = (TimestampFormat)formatIndex;
                changed = true;
            }
            
            ImGui.TextWrapped("Examples:");
            ImGui.TextColored(Constants.UI.ExampleColor, "Compact: " + _formatExamples[0]);
            ImGui.TextColored(Constants.UI.ExampleColor, "Regular: " + _formatExamples[1]);
            ImGui.TextColored(Constants.UI.ExampleColor, "Readable: " + _formatExamples[2]);
            
            return changed;
        }
    }
}