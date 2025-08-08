using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Numerics;
using Sightseeingway.UI.Components;
using Dalamud.Utility;
using Dalamud.Bindings.ImGui;

namespace Sightseeingway
{
    public class ConfigWindow : Window, IDisposable
    {
        private readonly Configuration config;
        private Configuration tempConfig = null!;
        
        // UI component instances
        private readonly TimestampFormatSelector timestampSelector;
        private readonly FieldOrderingComponent fieldOrdering;
        private readonly FilenamePreviewComponent filenamePreview;
        
        // Track if changes have been made
        private bool configChanged = false;
        private bool isFirstDraw = true;

        public ConfigWindow(Configuration config) : base(Constants.Plugin.Name + " Configuration")
        {
            this.config = config;
            CopyConfigToTemp();
            
            Size = Constants.UI.DefaultWindowSize;
            SizeCondition = ImGuiCond.FirstUseEver;
            Flags = ImGuiWindowFlags.NoCollapse;
            
            // Initialize UI components
            timestampSelector = new TimestampFormatSelector();
            fieldOrdering = new FieldOrderingComponent(tempConfig.SelectedFields);
            filenamePreview = new FilenamePreviewComponent();
            
            // Don't update preview here - defer until first Draw
        }
        
        private void CopyConfigToTemp()
        {
            tempConfig = new Configuration
            {
                Version = config.Version,
                SelectedFields = config.SelectedFields,
                TimestampFormat = config.TimestampFormat,
                DebugMode = config.DebugMode,
                ShowNameChangesInChat = config.ShowNameChangesInChat
            };
        }

        private void UpdateFilenamePreview()
        {
            // Use safe access patterns to avoid null reference exceptions
            string character;
            try {
                character = Plugin.ClientState.LocalPlayer?.Name.TextValue ?? "WolOfLight";
            }
            catch {
                character = "WolOfLight"; // Fallback if we can't access LocalPlayer
            }
            
            var map = "Unknown";
            var position = "";
            var eorzeaTime = "";
            var weather = "";
            var shaderPreset = IO.CurrentPresetName ?? "Unknown";

            // Only try to access game data if we can safely do so
            if (Plugin.ClientState.IsLoggedIn && Plugin.ClientState.LocalPlayer != null && Plugin.ClientState.MapId > 0)
            {
                try
                {
                    var mapSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>();
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
                catch (Exception ex)
                {
                    Plugin.Logger?.Warning($"Error getting game data for example: {ex.Message}");
                }
            }

            // Update the preview using the component
            filenamePreview.RefreshPreview(
                tempConfig.TimestampFormat,
                fieldOrdering.GetActiveFieldsInOrder(),
                character,
                map,
                position,
                eorzeaTime,
                weather,
                shaderPreset
            );
        }
        
        public override void Draw()
        {
            // Update the preview only on the first draw when we're sure we're on the game thread
            if (isFirstDraw)
            {
                UpdateFilenamePreview();
                isFirstDraw = false;
            }
            
            configChanged = false;
            
            if (ImGui.BeginChild("##MainScrollingArea", new Vector2(-1, ImGui.GetContentRegionAvail().Y - 182), true))
            {
                // Timestamp Format Section
                var format = tempConfig.TimestampFormat;
                if (timestampSelector.Render(ref format))
                {
                    tempConfig.TimestampFormat = format;
                    configChanged = true;
                }
                
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                // Field Selection and Ordering Section
                configChanged |= fieldOrdering.Render();
                
                ImGui.EndChild();
            }
              ImGui.Spacing();
            
            // Example filename preview
            filenamePreview.Render();
            
            ImGui.Spacing();
              // Notification Settings
            ImGui.TextColored(new Vector4(0.0f, 0.85f, 1.0f, 1.0f), "Notification Settings");
            ImGui.Spacing();
            
            // First checkbox - Show name changes in chat
            bool showNameChangesInChat = tempConfig.ShowNameChangesInChat;
            if (ImGui.Checkbox("Show name changes in chat window", ref showNameChangesInChat))
            {
                tempConfig.ShowNameChangesInChat = showNameChangesInChat;
                configChanged = true;
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("Shows a message in the chat window when a screenshot is renamed.");
                ImGui.Text("Disable this if you don't want to see these notifications.");
                ImGui.EndTooltip();
            }
            
            // Second checkbox - Debug Mode (on same line)
            ImGui.SameLine(ImGui.GetWindowWidth() * 0.7f);
            
            bool debugMode = tempConfig.DebugMode;
            if (ImGui.Checkbox("Debug Mode", ref debugMode))
            {
                tempConfig.DebugMode = debugMode;
                configChanged = true;
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("Enables additional logging and debug information.");
                ImGui.Text("This may affect performance but helps with troubleshooting.");
                ImGui.EndTooltip();
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("Shows a message in the chat window when a screenshot is renamed.");
                ImGui.Text("Disable this if you don't want to see these notifications.");
                ImGui.EndTooltip();
            }
            
            ImGui.Spacing();
            
            // Button row
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
                fieldOrdering.InitializeFromString(tempConfig.SelectedFields);
                UpdateFilenamePreview();
                Client.PrintMessage("Changes reverted to last saved settings.");
            }
            
            ImGui.SameLine(0, 10);
            
            if (ImGui.Button("Reset to Defaults", new Vector2(buttonWidth, buttonHeight)))
            {
                ResetToDefaults();
                UpdateFilenamePreview();
                Client.PrintMessage("Settings reset to defaults.");
            }
            
            // If any changes were made, update the filename preview
            if (configChanged)
            {
                tempConfig.SelectedFields = fieldOrdering.GetSelectedFieldsString();
                UpdateFilenamePreview();
            }
        }
        
        private void ApplyChanges()
        {
            config.SelectedFields = fieldOrdering.GetSelectedFieldsString();
            config.TimestampFormat = tempConfig.TimestampFormat;
            config.ShowNameChangesInChat = tempConfig.ShowNameChangesInChat;
            
            // Update debug mode setting and apply it immediately
            bool debugModeChanged = config.DebugMode != tempConfig.DebugMode;
            config.DebugMode = tempConfig.DebugMode;
            
            // If debug mode changed, update it in the Plugin and Logger
            if (debugModeChanged)
            {
                Plugin.DebugMode = config.DebugMode;
                Plugin.Logger?.SetDebugMode(config.DebugMode);
                Plugin.Logger?.UserMessage($"Debug mode {(config.DebugMode ? "enabled" : "disabled")}");
            }
        }
        
        private void ResetToDefaults()
        {
            tempConfig.SelectedFields = Configuration.GetDefaultSelectedFields();
            tempConfig.TimestampFormat = TimestampFormat.Compact;
            tempConfig.DebugMode = false;
            tempConfig.ShowNameChangesInChat = true;
            fieldOrdering.InitializeFromString(tempConfig.SelectedFields);
        }
        
        public void Dispose() { }
    }
}
