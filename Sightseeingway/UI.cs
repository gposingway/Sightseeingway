using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Sightseeingway.Services;
using Sightseeingway.UI.Components;
using System;
using System.Numerics;

namespace Sightseeingway
{
    public class ConfigWindow : Window, IDisposable
    {
        private readonly Configuration config;
        private Configuration tempConfig = null!;

        private readonly TimestampFormatSelector timestampSelector;
        private readonly FieldOrderingComponent fieldOrdering;
        private readonly FilenamePreviewComponent filenamePreview;

        private bool configChanged;

        public ConfigWindow(Configuration config) : base(Constants.Plugin.Name + " Configuration")
        {
            this.config = config;
            CopyConfigToTemp();

            Size = Constants.UI.DefaultWindowSize;
            SizeCondition = ImGuiCond.FirstUseEver;
            Flags = ImGuiWindowFlags.NoCollapse;

            timestampSelector = new TimestampFormatSelector();
            fieldOrdering = new FieldOrderingComponent(tempConfig.SelectedFields);
            filenamePreview = new FilenamePreviewComponent();
        }

        public override void OnOpen()
        {
            // Reset to last-saved values whenever the window is opened.
            CopyConfigToTemp();
            fieldOrdering.InitializeFromString(tempConfig.SelectedFields);
            UpdateFilenamePreview();
        }

        private void CopyConfigToTemp()
        {
#pragma warning disable CS0618 // intermediate UI still surfaces the legacy toggles; Track C replaces this.
            tempConfig = new Configuration
            {
                Version = config.Version,
                SelectedFields = config.SelectedFields,
                TimestampFormat = config.TimestampFormat,
                LogVerbosity = config.LogVerbosity,
                DebugMode = config.DebugMode,
                ShowNameChangesInChat = config.ShowNameChangesInChat
            };
#pragma warning restore CS0618
        }

        private void UpdateFilenamePreview()
        {
            string character;
            try
            {
                character = Plugin.ObjectTable.LocalPlayer?.Name.TextValue ?? "WolOfLight";
            }
            catch
            {
                character = "WolOfLight";
            }

            var map = "Unknown";
            var position = "";
            var eorzeaTime = "";
            var weather = "";
            var shaderPreset = IO.CurrentPresetName ?? "Unknown";

            if (Plugin.ClientState.IsLoggedIn && Plugin.ObjectTable.LocalPlayer != null && Plugin.ClientState.MapId > 0)
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

                            var playerPos = Plugin.ObjectTable.LocalPlayer.Position;
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
            configChanged = false;

            // BeginChild/EndChild must be matched regardless of return value.
            ImGui.BeginChild(
                "##MainScrollingArea",
                new Vector2(-1, ImGui.GetContentRegionAvail().Y - Constants.UI.ButtonRowReservedHeight),
                true);

            var format = tempConfig.TimestampFormat;
            if (timestampSelector.Render(ref format))
            {
                tempConfig.TimestampFormat = format;
                configChanged = true;
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            configChanged |= fieldOrdering.Render();

            ImGui.EndChild();

            ImGui.Spacing();
            filenamePreview.Render();
            ImGui.Spacing();

            ImGui.TextColored(Constants.UI.SectionAccentColor, "Notification Settings");
            ImGui.Spacing();

#pragma warning disable CS0618 // Track C replaces this section with the Diagnostics panel.
            var showNameChangesInChat = tempConfig.ShowNameChangesInChat;
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

            ImGui.SameLine(ImGui.GetWindowWidth() * Constants.UI.DebugCheckboxOffsetRatio);

            var debugMode = tempConfig.DebugMode;
            if (ImGui.Checkbox("Debug Mode", ref debugMode))
            {
                tempConfig.DebugMode = debugMode;
                configChanged = true;
            }
#pragma warning restore CS0618
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("Enables additional logging and debug information.");
                ImGui.Text("This may affect performance but helps with troubleshooting.");
                ImGui.EndTooltip();
            }

            ImGui.Spacing();

            var windowWidth = ImGui.GetWindowWidth();
            var buttonWidth = (windowWidth - Constants.UI.ButtonRowMargin) / 3;
            var buttonSize = new Vector2(buttonWidth, Constants.UI.ButtonHeight);

            if (ImGui.Button("Save Settings", buttonSize))
            {
                ApplyChanges();
                config.Save();
                Plugin.Logger?.UserMessage("Settings saved successfully!");
                IsOpen = false;
            }

            ImGui.SameLine(0, Constants.UI.ButtonGap);

            if (ImGui.Button("Revert Changes", buttonSize))
            {
                CopyConfigToTemp();
                fieldOrdering.InitializeFromString(tempConfig.SelectedFields);
                UpdateFilenamePreview();
                Plugin.Logger?.UserMessage("Changes reverted to last saved settings.");
            }

            ImGui.SameLine(0, Constants.UI.ButtonGap);

            if (ImGui.Button("Reset to Defaults", buttonSize))
            {
                ResetToDefaults();
                UpdateFilenamePreview();
                Plugin.Logger?.UserMessage("Settings reset to defaults.");
            }

            if (configChanged)
            {
                tempConfig.SelectedFields = fieldOrdering.GetSelectedFieldsString();
                UpdateFilenamePreview();
            }
        }

        private void ApplyChanges()
        {
#pragma warning disable CS0618 // intermediate UI; Track C replaces this section with the Diagnostics panel.
            config.SelectedFields = fieldOrdering.GetSelectedFieldsString();
            config.TimestampFormat = tempConfig.TimestampFormat;
            config.ShowNameChangesInChat = tempConfig.ShowNameChangesInChat;
            config.DebugMode = tempConfig.DebugMode;

            // Derive LogVerbosity from the legacy toggles until the Diagnostics panel ships.
            var newVerbosity = tempConfig.DebugMode
                ? LogVerbosity.Debug
                : (tempConfig.ShowNameChangesInChat ? LogVerbosity.Status : LogVerbosity.Quiet);

            if (config.LogVerbosity != newVerbosity)
            {
                config.LogVerbosity = newVerbosity;
                Plugin.Logger?.SetVerbosity(newVerbosity);
                Plugin.Logger?.UserMessage($"Logging verbosity: {newVerbosity}");
            }
#pragma warning restore CS0618
        }

        private void ResetToDefaults()
        {
#pragma warning disable CS0618
            tempConfig.SelectedFields = Configuration.GetDefaultSelectedFields();
            tempConfig.TimestampFormat = TimestampFormat.Compact;
            tempConfig.DebugMode = false;
            tempConfig.ShowNameChangesInChat = true;
            tempConfig.LogVerbosity = LogVerbosity.Status;
            fieldOrdering.InitializeFromString(tempConfig.SelectedFields);
#pragma warning restore CS0618
        }

        // Required by Window's IDisposable contract; nothing managed to release here.
        public void Dispose() { }
    }
}
