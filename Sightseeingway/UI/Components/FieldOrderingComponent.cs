// filepath: f:\Replica\NAS\Files\repo\github\Sightseeingway\Sightseeingway\UI\Components\FieldOrderingComponent.cs
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Sightseeingway.UI.Components
{
    /// <summary>
    /// UI component for managing field order and selection in filename patterns
    /// </summary>
    public class FieldOrderingComponent
    {
        private readonly Dictionary<FilenameField, string> _fieldDisplayNames = new()
        {
            { FilenameField.Timestamp, "Timestamp" },
            { FilenameField.CharacterName, "Character Name" },
            { FilenameField.MapName, "Map/Zone Name" },
            { FilenameField.Position, "Position Coordinates" },
            { FilenameField.EorzeaTime, "Eorzea Time Period" },
            { FilenameField.Weather, "Weather" },
            { FilenameField.ShaderPreset, "Shader Preset" }
        };

        private List<FilenameField> _orderedFields = new();
        private HashSet<FilenameField> _activeFields = new();
        
        public FieldOrderingComponent(string selectedFields)
        {
            InitializeFromString(selectedFields);
        }

        /// <summary>
        /// Initialize the component with an ordered, comma-separated list of fields
        /// </summary>
        public void InitializeFromString(string selectedFields)
        {
            var configuredFields = FilenameGenerator.StringToFieldList(selectedFields);
            
            // Initialize ordered fields list
            _orderedFields = new List<FilenameField> { FilenameField.Timestamp }; // Timestamp always first

            // Add fields from config, ensuring no duplicates and Timestamp is not re-added
            foreach (var field in configuredFields)
            {
                if (field != FilenameField.Timestamp && !_orderedFields.Contains(field))
                {
                    _orderedFields.Add(field);
                }
            }

            // Add any missing fields from the enum
            foreach (var fieldEnumMember in Enum.GetValues(typeof(FilenameField)).Cast<FilenameField>())
            {
                if (!_orderedFields.Contains(fieldEnumMember))
                {
                    _orderedFields.Add(fieldEnumMember);
                }
            }

            // Initialize active fields
            _activeFields = configuredFields.ToHashSet();
            _activeFields.Add(FilenameField.Timestamp); // Ensure Timestamp is active
        }
        
        /// <summary>
        /// Render the field ordering and selection component
        /// </summary>
        /// <returns>True if any changes were made, otherwise false</returns>
        public bool Render()
        {
            bool changed = false;
            
            ImGui.TextColored(new Vector4(1.0f, 0.85f, 0.0f, 1.0f), "Field Selection and Order");
            ImGui.TextWrapped("Select which fields to include and the order they'll be used.");
            ImGui.Spacing();

            for (int i = 0; i < _orderedFields.Count; i++)
            {
                var field = _orderedFields[i];
                var name = _fieldDisplayNames[field];
                var isTimestampField = field == FilenameField.Timestamp;

                ImGui.PushID($"field_{(int)field}");

                // Disable up arrow for timestamp and the item immediately after it
                ImGui.BeginDisabled(isTimestampField || i <= 1);
                if (ImGui.ArrowButton("##up", ImGuiDir.Up))
                {
                    if (i > 1)
                    {
                        var itemToMove = _orderedFields[i];
                        _orderedFields.RemoveAt(i);
                        _orderedFields.Insert(i - 1, itemToMove);
                        changed = true;
                    }
                }
                ImGui.EndDisabled();

                ImGui.SameLine();

                // Disable down arrow for the last item
                ImGui.BeginDisabled(isTimestampField || i == _orderedFields.Count - 1);
                if (ImGui.ArrowButton("##down", ImGuiDir.Down))
                {
                    if (i < _orderedFields.Count - 1)
                    {
                        var itemToMove = _orderedFields[i];
                        _orderedFields.RemoveAt(i);
                        _orderedFields.Insert(i + 1, itemToMove);
                        changed = true;
                    }
                }
                ImGui.EndDisabled();

                ImGui.SameLine();

                var isChecked = _activeFields.Contains(field);
                ImGui.BeginDisabled(isTimestampField); // Timestamp checkbox is always checked and disabled
                if (ImGui.Checkbox(name, ref isChecked))
                {
                    if (isChecked) _activeFields.Add(field);
                    else _activeFields.Remove(field);
                    changed = true;
                }
                ImGui.EndDisabled();

                // Add Shadingway link for ShaderPreset field
                if (field == FilenameField.ShaderPreset)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(Constants.UI.InfoColor, "(?)");
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text("Requires Shadingway ReShade addon to be installed and enabled.");
                        ImGui.Text("Current Status: " + (IO.EffectsEnabled ? "Detected & Enabled" : "Not Detected"));
                        ImGui.EndTooltip();
                    }
                    ImGui.SameLine();
                    ImGui.Text("- Requires ");
                    ImGui.SameLine(0, 0);
                    ImGui.PushStyleColor(ImGuiCol.Text, Constants.UI.LinkColor);
                    ImGui.TextUnformatted("Shadingway ReShade addon");
                    ImGui.PopStyleColor();
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip("Click to open " + Constants.UI.ShadingwayRepoUrl);
                    }
                    if (ImGui.IsItemClicked())
                    {
                        Util.OpenLink(Constants.UI.ShadingwayRepoUrl);
                    }
                }

                ImGui.PopID();
            }
            
            return changed;
        }
        
        /// <summary>
        /// Gets the current selection as a comma-separated string of field names
        /// </summary>
        public string GetSelectedFieldsString()
        {
            return FilenameGenerator.FieldListToString(_orderedFields.Where(f => _activeFields.Contains(f)));
        }
        
        /// <summary>
        /// Gets the list of active fields in the current order
        /// </summary>
        public List<FilenameField> GetActiveFieldsInOrder()
        {
            return _orderedFields.Where(f => _activeFields.Contains(f)).ToList();
        }
    }
}