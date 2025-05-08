// filepath: f:\Replica\NAS\Files\repo\github\Sightseeingway\Sightseeingway\UI\Components\FilenamePreviewComponent.cs
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sightseeingway.UI.Components
{
    /// <summary>
    /// UI component for previewing generated filenames
    /// </summary>
    public class FilenamePreviewComponent
    {
        private string _previewFilename = "Loading example...";
        private DateTime _lastRefreshTime = DateTime.MinValue;
        private readonly DateTime _exampleTimestamp;
        
        public FilenamePreviewComponent()
        {
            _exampleTimestamp = DateTime.Now;
        }
        
        /// <summary>
        /// Refreshes the filename preview using the provided parameters
        /// </summary>
        public void RefreshPreview(
            TimestampFormat timestampFormat,
            List<FilenameField> activeFieldsInOrder,
            string characterName = "",
            string map = "Unknown",
            string position = "",
            string eorzeaTime = "",
            string weather = "",
            string shaderPreset = "")
        {
            _lastRefreshTime = DateTime.Now;
            
            // Use the central filename generator to create a preview
            _previewFilename = FilenameGenerator.GenerateFilename(
                _exampleTimestamp,
                timestampFormat,
                characterName,
                map,
                position,
                eorzeaTime,
                weather,
                shaderPreset,
                IO.EffectsEnabled,
                activeFieldsInOrder,
                ".png"
            );
        }
        
        /// <summary>
        /// Renders the filename preview box
        /// </summary>
        public void Render()
        {
            ImGui.Spacing();
            ImGui.TextColored(Constants.UI.ExampleHeaderColor, "Example:");
            
            ImGui.BeginChild("##ExampleBox", new Vector2(-1, 60), true);
            ImGui.PushStyleColor(ImGuiCol.Text, Constants.UI.ExampleColor);
            ImGui.TextWrapped(_previewFilename);
            ImGui.PopStyleColor();
            ImGui.EndChild();
        }
        
        /// <summary>
        /// Gets the last refresh time
        /// </summary>
        public DateTime GetLastRefreshTime() => _lastRefreshTime;
        
        /// <summary>
        /// Gets the current preview filename
        /// </summary>
        public string GetPreviewFilename() => _previewFilename;
    }
}