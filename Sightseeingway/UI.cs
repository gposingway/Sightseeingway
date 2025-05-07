using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Lumina.Excel.Sheets;
using Dalamud.Utility;

namespace Sightseeingway
{
    public class ConfigWindow : Window, IDisposable
    {
        private readonly Configuration config;
        private Configuration tempConfig = null!;
        
        // Define field names
        private static readonly Dictionary<FilenameField, string> FieldDisplayNames = new()
        {
            { FilenameField.Timestamp, "Timestamp" },
            { FilenameField.CharacterName, "Character Name" },
            { FilenameField.MapName, "Map/Zone Name" },
            { FilenameField.Position, "Position Coordinates" },
            { FilenameField.EorzeaTime, "Eorzea Time Period" },
            { FilenameField.Weather, "Weather" },
            { FilenameField.ShaderPreset, "Shader Preset" }
        };
        
        // UI colors
        private readonly Vector4 headerColor = new(0.7f, 0.9f, 1.0f, 1.0f);
        private readonly Vector4 infoColor = new(0.8f, 0.8f, 0.5f, 1.0f);
        private readonly Vector4 exampleHeaderColor = new(0.5f, 0.9f, 0.5f, 1.0f);
        private readonly Vector4 exampleColor = new(1.0f, 1.0f, 1.0f, 1.0f);
        private readonly Vector4 mandatoryFieldBg = new(0.3f, 0.3f, 0.5f, 0.5f);
        
        // Current example filename
        private string exampleFileName = "";
        private DateTime lastRefreshTime = DateTime.MinValue;
        private bool isFirstDraw = true; // Flag for initial refresh
        private DateTime exampleTimestamp; // Store a fixed timestamp for examples
        
        // Precomputed timestamp examples
        private string compactExample = "";
        private string regularExample = "";
        private string readableExample = "";
        
        // This list holds all possible fields in the order they are displayed in the UI.
        private List<FilenameField> uiOrderedFields = new();
        // This set holds the fields that are currently checked (active).
        private HashSet<FilenameField> uiActiveFields = new();

        public ConfigWindow(Configuration config) : base("Sightseeingway Configuration")
        {
            this.config = config;
            CopyConfigToTemp();
            Size = new Vector2(550, 600);
            SizeCondition = ImGuiCond.FirstUseEver;
            Flags = ImGuiWindowFlags.NoCollapse;
            InitializeUIData();
            exampleFileName = "Loading example...";
            
            // Set a fixed timestamp for examples
            exampleTimestamp = DateTime.Now;
            
            // Precompute timestamp examples using FilenameGenerator
            compactExample = FilenameGenerator.FormatTimestamp(exampleTimestamp, TimestampFormat.Compact);
            regularExample = FilenameGenerator.FormatTimestamp(exampleTimestamp, TimestampFormat.Regular);
            readableExample = FilenameGenerator.FormatTimestamp(exampleTimestamp, TimestampFormat.Readable);
        }
        
        private void CopyConfigToTemp()
        {
            tempConfig = new Configuration
            {
                Version = config.Version,
                SelectedFields = config.SelectedFields,
                TimestampFormat = config.TimestampFormat
            };
        }

        private void InitializeUIData()
        {
            // 1. Initialize uiOrderedFields
            var configuredFields = StringToFieldList(tempConfig.SelectedFields);
            uiOrderedFields = new List<FilenameField> { FilenameField.Timestamp }; // Timestamp always first

            // Add fields from current config, ensuring no duplicates and Timestamp is not re-added
            foreach (var field in configuredFields)
            {
                if (field != FilenameField.Timestamp && !uiOrderedFields.Contains(field))
                {
                    uiOrderedFields.Add(field);
                }
            }

            // Add any missing fields from the enum to the end of uiOrderedFields
            foreach (var fieldEnumMember in Enum.GetValues(typeof(FilenameField)).Cast<FilenameField>())
            {
                if (!uiOrderedFields.Contains(fieldEnumMember))
                {
                    uiOrderedFields.Add(fieldEnumMember);
                }
            }

            // 2. Initialize uiActiveFields based on tempConfig.SelectedFields
            uiActiveFields = StringToFieldList(tempConfig.SelectedFields).ToHashSet();
            uiActiveFields.Add(FilenameField.Timestamp); // Ensure Timestamp is active

            // 3. Synchronize tempConfig.SelectedFields based on the initialized uiOrderedFields and uiActiveFields
            UpdateTempConfigSelectedFields();
        }

        private void UpdateTempConfigSelectedFields()
        {
            tempConfig.SelectedFields = FilenameGenerator.FieldListToString(uiOrderedFields.Where(f => uiActiveFields.Contains(f)));
        }

        private List<FilenameField> StringToFieldList(string? fieldsString)
        {
            return FilenameGenerator.StringToFieldList(fieldsString);
        }

        private void RefreshLiveExample()
        {
            lastRefreshTime = DateTime.Now;
            
            // Format timestamp according to selected format
            string timestamp = FilenameGenerator.FormatTimestamp(exampleTimestamp, tempConfig.TimestampFormat);
            
            var character = Plugin.ClientState.LocalPlayer?.Name.TextValue ?? "WolOfLight";
            var map = "Unknown";
            var position = "";
            var eorzeaTime = "";
            var weather = "";
            var shaderPreset = IO.CurrentPresetName ?? "Unknown";

            if (Plugin.ClientState.LocalPlayer != null && Plugin.ClientState.MapId > 0)
            {
                try
                {
                    var mapSheet = Plugin.DataManager.GetExcelSheet<Map>();
                    if (mapSheet != null)
                    {
                        var mapRow = mapSheet.GetRow(Plugin.ClientState.MapId);
                        if (mapRow.RowId > 0) 
                        {
                            var placeName = mapRow.PlaceName.Value;
                            var extractedName = placeName.Name.ToString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(extractedName)) map = extractedName;

                            var playerPos = Plugin.ClientState.LocalPlayer.Position;
                            var mapCoords = MapUtil.WorldToMap(playerPos, mapRow.OffsetX, mapRow.OffsetY, 0, mapRow.SizeFactor);
                            var roundedCoords = new Vector3(
                                (int)MathF.Round(mapCoords.X * 10f, 1) / 10f,
                                (int)MathF.Round(mapCoords.Y * 10f, 1) / 10f,
                                (int)MathF.Round(mapCoords.Z * 10f, 1) / 10f
                            );
                            position = roundedCoords.Z == 0 ?
                                $" ({roundedCoords.X:0.0},{roundedCoords.Y:0.0})" :
                                $" ({roundedCoords.X:0.0},{roundedCoords.Y:0.0},{roundedCoords.Z:0.0})";
                        }
                    }
                    weather = Client.GetCurrentWeatherName();
                    eorzeaTime = Client.GetCurrentEorzeaDateTime().DetermineDayPeriod(true);
                }
                catch (Exception ex) { Plugin.Log.Warning($"Error getting game data for example: {ex.Message}"); }
            }

            var activeFieldsInOrder = StringToFieldList(tempConfig.SelectedFields);
            
            // Use the centralized filename generation method
            exampleFileName = FilenameGenerator.GenerateFilename(
                exampleTimestamp,
                tempConfig.TimestampFormat,
                character,
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
        
        public override void Draw()
        {
            if (isFirstDraw)
            {
                RefreshLiveExample();
                isFirstDraw = false;
            }

            var configChanged = false;
            
            if (ImGui.BeginChild("##MainScrollingArea", new Vector2(-1, ImGui.GetContentRegionAvail().Y - 125), true))
            {
                // Timestamp Format Section
                ImGui.TextColored(headerColor, "Timestamp");
                ImGui.Spacing();
                
                string[] timestampOptions = { "Compact (yyyyMMddHHmmssfff)", "Regular (yyyyMMdd-HHmmss-fff)", "Readable (yyyy-MM-dd_HH-mm-ss.fff)" };
                int currentFormat = (int)tempConfig.TimestampFormat;
                if (ImGui.Combo("Format", ref currentFormat, timestampOptions, timestampOptions.Length))
                {
                    tempConfig.TimestampFormat = (TimestampFormat)currentFormat;
                    configChanged = true;
                }
                
                // Generate timestamp examples using the fixed timestamp
                ImGui.TextWrapped("Examples:");
                ImGui.TextColored(exampleColor, "Compact: " + compactExample);
                ImGui.TextColored(exampleColor, "Regular: " + regularExample);
                ImGui.TextColored(exampleColor, "Readable: " + readableExample);
                
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                // Field Selection Section
                ImGui.TextColored(headerColor, "Field Selection and Order");
                ImGui.TextWrapped("Select which fields to include and the order they'll be used.");
                ImGui.Spacing();

                for (var i = 0; i < uiOrderedFields.Count; i++)
                {
                    var field = uiOrderedFields[i];
                    var name = FieldDisplayNames[field];
                    var isTimestampField = field == FilenameField.Timestamp;

                    ImGui.PushID($"field_{(int)field}");

                    // Fix bug: Need to disable up arrow for the item immediately after Timestamp
                    ImGui.BeginDisabled(isTimestampField || i <= 1); // Changed from "i == 0" to "i <= 1"
                    if (ImGui.ArrowButton("##up", ImGuiDir.Up))
                    {
                        if (i > 1) // Changed from "i > 0" to "i > 1" to ensure second item can't swap with Timestamp
                        {
                            var itemToMove = uiOrderedFields[i];
                            uiOrderedFields.RemoveAt(i);
                            uiOrderedFields.Insert(i - 1, itemToMove);
                            UpdateTempConfigSelectedFields();
                            configChanged = true;
                        }
                    }
                    ImGui.EndDisabled();

                    ImGui.SameLine();

                    ImGui.BeginDisabled(isTimestampField || i == uiOrderedFields.Count - 1);
                    if (ImGui.ArrowButton("##down", ImGuiDir.Down))
                    {
                        if (i < uiOrderedFields.Count - 1)
                        {
                            var itemToMove = uiOrderedFields[i];
                            uiOrderedFields.RemoveAt(i);
                            uiOrderedFields.Insert(i + 1, itemToMove);
                            UpdateTempConfigSelectedFields();
                            configChanged = true;
                        }
                    }
                    ImGui.EndDisabled();

                    ImGui.SameLine();

                    var isChecked = uiActiveFields.Contains(field);
                    ImGui.BeginDisabled(isTimestampField); // Timestamp checkbox is always checked and disabled
                    if (ImGui.Checkbox(name, ref isChecked))
                    {
                        if (isChecked) uiActiveFields.Add(field);
                        else uiActiveFields.Remove(field);
                        UpdateTempConfigSelectedFields(); // Rebuild SelectedFields string
                        configChanged = true;
                    }
                    ImGui.EndDisabled();

                    // Re-add Shadingway link and tooltip for ShaderPreset field
                    if (field == FilenameField.ShaderPreset)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(infoColor, "(?)");
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.Text("Requires Shadingway ReShade addon to be installed and enabled.");
                            ImGui.Text("Current Status: " + (IO.EffectsEnabled ? "Detected & Enabled" : "Not Detected"));
                            ImGui.EndTooltip();
                        }
                        ImGui.SameLine();
                        ImGui.Text("- Requires ");
                        ImGui.SameLine(0,0);
                        var linkColor = new Vector4(0.6f, 0.8f, 1.0f, 1.0f); // A typical link color (light blue)
                        ImGui.PushStyleColor(ImGuiCol.Text, linkColor);
                        ImGui.TextUnformatted("Shadingway ReShade addon");
                        ImGui.PopStyleColor();
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip("Click to open https://github.com/gposingway/shadingway");
                        }
                        if (ImGui.IsItemClicked())
                        {
                            Util.OpenLink("https://github.com/gposingway/shadingway");
                        }
                    }

                    ImGui.PopID();
                }
                ImGui.EndChild();
            }
            
            ImGui.Spacing();
            ImGui.TextColored(exampleHeaderColor, "Example:");
            
            ImGui.BeginChild("##ExampleBox", new Vector2(-1, 60), true); 
            ImGui.PushStyleColor(ImGuiCol.Text, exampleColor);
            ImGui.TextWrapped(exampleFileName);
            ImGui.PopStyleColor();
            ImGui.EndChild();
            
            ImGui.Spacing();
            
            var windowWidth = ImGui.GetWindowWidth();
            var buttonWidth = (windowWidth - 40) / 3;
            float buttonHeight = 24;

            if (ImGui.Button("Save Settings", new Vector2(buttonWidth, buttonHeight)))
            {
                ApplyChanges();
                config.Save();
                Client.PrintMessage("Settings saved successfully!");
                this.IsOpen = false;
            }
            
            ImGui.SameLine(0, 10);
            
            if (ImGui.Button("Revert Changes", new Vector2(buttonWidth, buttonHeight)))
            {
                CopyConfigToTemp();
                InitializeUIData();
                RefreshLiveExample();
                Client.PrintMessage("Changes reverted to last saved settings.");
            }
            
            ImGui.SameLine(0, 10);
            
            if (ImGui.Button("Reset to Defaults", new Vector2(buttonWidth, buttonHeight)))
            {
                ResetToDefaults();
                RefreshLiveExample();
                Client.PrintMessage("Settings reset to defaults.");
            }
            
            if (configChanged)
            {
                RefreshLiveExample();
            }
        }
        
        private void ApplyChanges()
        {
            UpdateTempConfigSelectedFields();
            config.SelectedFields = tempConfig.SelectedFields;
            config.TimestampFormat = tempConfig.TimestampFormat;
        }
        
        private void ResetToDefaults()
        {
            tempConfig.SelectedFields = Configuration.GetDefaultSelectedFields();
            tempConfig.TimestampFormat = TimestampFormat.Compact;
            InitializeUIData();
        }
        
        public void Dispose() { }
    }
}
