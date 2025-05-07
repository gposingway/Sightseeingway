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
            { FilenameField.Timestamp, "Timestamp (yyyyMMddHHmmssfff)" }, // Added Timestamp
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
        
        public ConfigWindow(Configuration config) : base("Sightseeingway Configuration")
        {
            this.config = config;
            
            // Create a working copy of the configuration
            CopyConfigToTemp();
            
            // Set window size and flags
            Size = new Vector2(550, 550); // Adjusted height as Advanced Format is removed
            SizeCondition = ImGuiCond.FirstUseEver;
            Flags = ImGuiWindowFlags.NoCollapse;
            
            // Ensure field order has no duplicates
            EnsureValidFieldOrder();
            
            // DO NOT call RefreshLiveExample() here directly
            // exampleFileName will be initialized on first draw
            exampleFileName = "Loading example..."; 
        }
        
        private void CopyConfigToTemp()
        {
            tempConfig = new Configuration
            {
                Version = config.Version,
                IncludeCharacterName = config.IncludeCharacterName,
                IncludeMapName = config.IncludeMapName,
                IncludePosition = config.IncludePosition,
                IncludeEorzeaTime = config.IncludeEorzeaTime,
                IncludeWeather = config.IncludeWeather,
                IncludeShaderPreset = config.IncludeShaderPreset,
                FieldOrder = new List<FilenameField>(config.FieldOrder)
            };
        }
        
        private void EnsureValidFieldOrder()
        {
            var currentOrder = tempConfig.FieldOrder.ToList(); // Make a copy to iterate over
            var newOrder = new List<FilenameField>();
            var processedFields = new HashSet<FilenameField>();

            // 1. Add Timestamp first and mark it as processed.
            newOrder.Add(FilenameField.Timestamp);
            processedFields.Add(FilenameField.Timestamp);

            // 2. Add unique fields from the current configuration's order,
            //    maintaining their relative sequence after Timestamp.
            foreach (var field in currentOrder)
            {
                if (field != FilenameField.Timestamp && processedFields.Add(field))
                {
                    newOrder.Add(field);
                }
            }

            // 3. Add any remaining FilenameField enum members that were not in the
            //    original list, placing them at the end. This ensures all possible
            //    fields are always represented in the order list.
            var allPossibleFields = Enum.GetValues(typeof(FilenameField)).Cast<FilenameField>();
            foreach (var fieldEnumMember in allPossibleFields)
            {
                if (fieldEnumMember != FilenameField.Timestamp && !processedFields.Contains(fieldEnumMember))
                {
                    newOrder.Add(fieldEnumMember);
                    // No need to add to processedFields here as we're at the end of this logic block.
                }
            }

            // 4. Update tempConfig.FieldOrder with the fully cleaned and complete list.
            tempConfig.FieldOrder = newOrder;
        }
        
        private void RefreshLiveExample()
        {
            lastRefreshTime = DateTime.Now;
            
            // Start with timestamp (always required)
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            exampleFileName = timestamp;
            
            // Get real values from the game client
            var character = Plugin.ClientState.LocalPlayer?.Name.TextValue ?? "WolOfLight";
            var map = "Unknown";
            var position = "";
            var eorzeaTime = "";
            var weather = "";
            var shaderPreset = IO.CurrentPresetName ?? "Unknown";
            
            // Try to get location info if player is in a map
            if (Plugin.ClientState.LocalPlayer != null && Plugin.ClientState.MapId > 0)
            {
                try
                {
                    // Get map name
                    try
                    {
                        var mapSheet = Plugin.DataManager.GetExcelSheet<Map>();
                        if (mapSheet != null)
                        {
                            var mapRow = mapSheet.GetRow(Plugin.ClientState.MapId);
                            if (mapRow.RowId > 0) // Check if valid row
                            {
                                // Get place name carefully
                                try
                                {
                                    var placeName = mapRow.PlaceName.Value;
                                    var extractedName = placeName.Name.ToString();
                                    if (!string.IsNullOrEmpty(extractedName))
                                    {
                                        map = extractedName;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Plugin.Log.Debug($"Error extracting map name: {ex.Message}");
                                }
                                
                                // Calculate player coordinates
                                try
                                {
                                    var playerPos = Plugin.ClientState.LocalPlayer.Position;
                                    var mapCoords = MapUtil.WorldToMap(playerPos, mapRow.OffsetX, mapRow.OffsetY, 0, mapRow.SizeFactor);
                                    
                                    // Round to one decimal place (matching IO.cs logic)
                                    var roundedCoords = new Vector3(
                                        (int)MathF.Round(mapCoords.X * 10f, 1) / 10f,
                                        (int)MathF.Round(mapCoords.Y * 10f, 1) / 10f,
                                        (int)MathF.Round(mapCoords.Z * 10f, 1) / 10f
                                    );
                                    
                                    // Format position string
                                    position = roundedCoords.Z == 0 ? 
                                        $" ({roundedCoords.X:0.0},{roundedCoords.Y:0.0})" : 
                                        $" ({roundedCoords.X:0.0},{roundedCoords.Y:0.0},{roundedCoords.Z:0.0})";
                                }
                                catch (Exception ex)
                                {
                                    Plugin.Log.Debug($"Error calculating coordinates: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Debug($"Error getting map name: {ex.Message}");
                    }
                    
                    // Get current weather and Eorzea time
                    try
                    {
                        weather = Client.GetCurrentWeatherName();
                        eorzeaTime = Client.GetCurrentEorzeaDateTime().DetermineDayPeriod(true);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Debug($"Error getting weather or time: {ex.Message}");
                        weather = "Clear Skies";
                        eorzeaTime = "Day";
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.Warning($"Error getting map data: {ex.Message}");
                }
            }
            
            // Build the filename based on field order
            foreach (var field in tempConfig.FieldOrder)
            {
                switch (field)
                {
                    case FilenameField.CharacterName:
                        if (tempConfig.IncludeCharacterName) exampleFileName += AddNamePart(character);
                        break;
                    case FilenameField.MapName:
                        if (tempConfig.IncludeMapName) exampleFileName += AddNamePart(map);
                        break;
                    case FilenameField.Position:
                        if (tempConfig.IncludePosition) exampleFileName += position;
                        break;
                    case FilenameField.EorzeaTime:
                        if (tempConfig.IncludeEorzeaTime) exampleFileName += AddNamePart(eorzeaTime);
                        break;
                    case FilenameField.Weather:
                        if (tempConfig.IncludeWeather) exampleFileName += AddNamePart(weather);
                        break;
                    case FilenameField.ShaderPreset:
                        if (tempConfig.IncludeShaderPreset) exampleFileName += AddNamePart(shaderPreset);
                        break;
                }
            }
            
            // Add extension
            exampleFileName += ".png";
        }
        
        private string AddNamePart(string part)
        {
            return string.IsNullOrEmpty(part) || part == "Unknown" ? "" : "-" + part;
        }
        
        public override void Draw()
        {
            if (isFirstDraw)
            {
                RefreshLiveExample();
                isFirstDraw = false;
            }

            var configChanged = false;
            
            // Main content area (scrollable)
            // Adjusted the subtraction from 100 to 140 to reserve more space for bottom elements
            if (ImGui.BeginChild("##MainScrollingArea", new Vector2(-1, ImGui.GetContentRegionAvail().Y - 130), true))
            {
                // Field selection and ordering
                ImGui.TextColored(headerColor, "Field Selection and Order");
                ImGui.TextWrapped("Select which fields to include and drag to reorder them. Timestamp is always first and enabled.");
                ImGui.Spacing();
                
                // Show all fields with checkboxes and up/down buttons
                var fields = tempConfig.FieldOrder.ToList(); // Operate on a copy for safe iteration
                
                for (int i = 0; i < fields.Count; i++)
                {
                    var field = fields[i];
                    string name = FieldDisplayNames[field];
                    bool isTimestampField = field == FilenameField.Timestamp;
                    
                    ImGui.PushID($"field_{i}");
                    
                    // Up button (disabled for Timestamp or if it's the first editable item)
                    ImGui.BeginDisabled(isTimestampField || i == 1); // Timestamp is at index 0, first editable is 1
                    if (ImGui.ArrowButton("##up", ImGuiDir.Up) && !isTimestampField && i > 0)
                    {
                        var temp = tempConfig.FieldOrder[i];
                        tempConfig.FieldOrder[i] = tempConfig.FieldOrder[i - 1];
                        tempConfig.FieldOrder[i - 1] = temp;
                        configChanged = true;
                    }
                    ImGui.EndDisabled();
                    
                    ImGui.SameLine();
                    
                    // Down button (disabled for Timestamp or if it's the last item)
                    ImGui.BeginDisabled(isTimestampField || i == tempConfig.FieldOrder.Count - 1);
                    if (ImGui.ArrowButton("##down", ImGuiDir.Down) && !isTimestampField && i < tempConfig.FieldOrder.Count - 1)
                    {
                        var temp = tempConfig.FieldOrder[i];
                        tempConfig.FieldOrder[i] = tempConfig.FieldOrder[i + 1];
                        tempConfig.FieldOrder[i + 1] = temp;
                        configChanged = true;
                    }
                    ImGui.EndDisabled();
                    
                    ImGui.SameLine();
                    
                    // Checkbox with field name (Timestamp is always checked and disabled)
                    bool isChecked = isTimestampField || GetFieldEnabled(field);
                    ImGui.BeginDisabled(isTimestampField);
                    if (ImGui.Checkbox(name, ref isChecked) && !isTimestampField)
                    {
                        SetFieldEnabled(field, isChecked);
                        configChanged = true;
                    }
                    ImGui.EndDisabled();
                    
                    // Special note for Shader Preset field
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
                        ImGui.TextColored(infoColor, "- Requires ");
                        ImGui.SameLine(0,0);
                        // Change from SmallButton to a Text-based link
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
            
            // Live example section (fixed at bottom)
            ImGui.Spacing();
            ImGui.TextColored(exampleHeaderColor, "Live Example:");
            
            // Example filename display
            // Increased height from 40 to 60 to accommodate potential wrapping
            ImGui.BeginChild("##ExampleBox", new Vector2(-1, 60), true); 
            ImGui.PushStyleColor(ImGuiCol.Text, exampleColor);
            ImGui.TextWrapped(exampleFileName);
            ImGui.PopStyleColor();
            ImGui.EndChild();
            
            ImGui.Spacing();
            
            // Action buttons
            float windowWidth = ImGui.GetWindowWidth();
            float buttonWidth = (windowWidth - 40) / 3;
            
            if (ImGui.Button("Save Settings", new Vector2(buttonWidth, 30)))
            {
                ApplyChanges();
                config.Save();
                Client.PrintMessage("Settings saved successfully!");
                this.IsOpen = false; // Close the window after saving
            }
            
            ImGui.SameLine(0, 10);
            
            if (ImGui.Button("Revert Changes", new Vector2(buttonWidth, 30)))
            {
                CopyConfigToTemp();
                RefreshLiveExample();
                Client.PrintMessage("Changes reverted to last saved settings.");
            }
            
            ImGui.SameLine(0, 10);
            
            if (ImGui.Button("Reset to Defaults", new Vector2(buttonWidth, 30)))
            {
                ResetToDefaults();
                RefreshLiveExample();
                Client.PrintMessage("Settings reset to defaults.");
            }
            
            // Update example if config changed
            if (configChanged)
            {
                RefreshLiveExample();
            }
        }
        
        private bool GetFieldEnabled(FilenameField field)
        {
            return field switch
            {
                FilenameField.CharacterName => tempConfig.IncludeCharacterName,
                FilenameField.MapName => tempConfig.IncludeMapName,
                FilenameField.Position => tempConfig.IncludePosition,
                FilenameField.EorzeaTime => tempConfig.IncludeEorzeaTime,
                FilenameField.Weather => tempConfig.IncludeWeather,
                FilenameField.ShaderPreset => tempConfig.IncludeShaderPreset,
                _ => false
            };
        }
        
        private void SetFieldEnabled(FilenameField field, bool enabled)
        {
            switch (field)
            {
                case FilenameField.CharacterName:
                    tempConfig.IncludeCharacterName = enabled;
                    break;
                case FilenameField.MapName:
                    tempConfig.IncludeMapName = enabled;
                    break;
                case FilenameField.Position:
                    tempConfig.IncludePosition = enabled;
                    break;
                case FilenameField.EorzeaTime:
                    tempConfig.IncludeEorzeaTime = enabled;
                    break;
                case FilenameField.Weather:
                    tempConfig.IncludeWeather = enabled;
                    break;
                case FilenameField.ShaderPreset:
                    tempConfig.IncludeShaderPreset = enabled;
                    break;
            }
        }
        
        private void ApplyChanges()
        {
            config.IncludeCharacterName = tempConfig.IncludeCharacterName;
            config.IncludeMapName = tempConfig.IncludeMapName;
            config.IncludePosition = tempConfig.IncludePosition;
            config.IncludeEorzeaTime = tempConfig.IncludeEorzeaTime;
            config.IncludeWeather = tempConfig.IncludeWeather;
            config.IncludeShaderPreset = tempConfig.IncludeShaderPreset;
            config.FieldOrder = new List<FilenameField>(tempConfig.FieldOrder);
        }
        
        private void ResetToDefaults()
        {
            tempConfig.IncludeCharacterName = true;
            tempConfig.IncludeMapName = true;
            tempConfig.IncludePosition = true;
            tempConfig.IncludeEorzeaTime = true;
            tempConfig.IncludeWeather = true;
            tempConfig.IncludeShaderPreset = true;
            
            tempConfig.FieldOrder = new List<FilenameField>
            {
                FilenameField.Timestamp, // Ensure Timestamp is first
                FilenameField.CharacterName,
                FilenameField.MapName,
                FilenameField.Position,
                FilenameField.EorzeaTime,
                FilenameField.Weather,
                FilenameField.ShaderPreset
            };
            EnsureValidFieldOrder(); // Re-validate after reset
        }
        
        public void Dispose() { }
    }
}