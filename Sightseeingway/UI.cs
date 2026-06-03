using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using Sightseeingway.Metadata;
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

        // Filename column
        private readonly TimestampFormatSelector timestampSelector;
        private readonly FieldOrderingComponent fieldOrdering;
        private readonly FilenamePreviewComponent filenamePreview;

        // Metadata column
        private readonly MetadataConfigComponent metadataConfig;
        private readonly MetadataPreviewComponent metadataPreview;

        // Gear tab
        private readonly GearComponent gear;

        // Diagnostics
        private readonly DiagnosticsComponent diagnostics;

        // State
        private bool configChanged;
        private const string BaseTitle = "Sightseeingway Configuration";

        public ConfigWindow(Configuration config) : base(BaseTitle)
        {
            this.config = config;
            CopyConfigToTemp();

            Size = Constants.UI.DefaultWindowSize;
            SizeCondition = ImGuiCond.FirstUseEver;
            Flags = ImGuiWindowFlags.NoCollapse;

            timestampSelector = new TimestampFormatSelector();
            fieldOrdering = new FieldOrderingComponent(tempConfig.SelectedFields);
            filenamePreview = new FilenamePreviewComponent();
            metadataConfig = new MetadataConfigComponent();
            metadataPreview = new MetadataPreviewComponent();
            gear = new GearComponent();
            diagnostics = new DiagnosticsComponent();
        }

        public override void OnOpen()
        {
            CopyConfigToTemp();
            fieldOrdering.InitializeFromString(tempConfig.SelectedFields);
            RefreshPreviews();
            configChanged = false;
            WindowName = BaseTitle + "###SightseeingwayConfig";
        }

        public override void PreDraw()
        {
            // Asterisk in window title when there are unsaved changes.
            WindowName = (configChanged ? BaseTitle + " *" : BaseTitle) + "###SightseeingwayConfig";
        }

        private void CopyConfigToTemp()
        {
            tempConfig = new Configuration
            {
                Version = config.Version,
                SelectedFields = config.SelectedFields,
                TimestampFormat = config.TimestampFormat,
                EmbedMetadata = config.EmbedMetadata,
                MetadataFields = new System.Collections.Generic.Dictionary<string, bool>(config.MetadataFields),
                LogVerbosity = config.LogVerbosity,
                GearPublishEnabled = config.GearPublishEnabled,
                GearShadingwayPort = config.GearShadingwayPort,
            };
        }

        public override void Draw()
        {
            // Pin the action button row to the bottom of the window. Everything
            // above it lives in a single scrollable region, so expanding the
            // metadata preview or recent events panel never pushes the buttons
            // out of view.
            var buttonRowReserved = Constants.UI.ButtonHeight + 16f;
            var contentHeight = Math.Max(120f, ImGui.GetContentRegionAvail().Y - buttonRowReserved);

            ImGui.BeginChild("##ContentRegion", new Vector2(-1, contentHeight), false);

            if (ImGui.BeginTabBar("##SightseeingwayTabs"))
            {
                if (ImGui.BeginTabItem("Screenshots"))
                {
                    DrawScreenshotsTab();
                    ImGui.EndTabItem();
                }

                if (ImGui.BeginTabItem("Gear"))
                {
                    if (gear.Render(tempConfig)) configChanged = true;
                    ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }

            ImGui.EndChild();

            ImGui.Separator();
            DrawButtonRow();

            // Refresh previews if anything changed during this draw.
            if (configChanged)
            {
                tempConfig.SelectedFields = fieldOrdering.GetSelectedFieldsString();
                RefreshPreviews();
            }
        }

        private void DrawScreenshotsTab()
        {
            if (ImGui.BeginTable("##MainTwoColumn", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                if (DrawFilenameColumn()) configChanged = true;

                ImGui.TableNextColumn();
                if (metadataConfig.Render(tempConfig)) configChanged = true;

                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            filenamePreview.Render();
            metadataPreview.Render();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (diagnostics.Render(tempConfig)) configChanged = true;
        }

        private bool DrawFilenameColumn()
        {
            var changed = false;

            var format = tempConfig.TimestampFormat;
            if (timestampSelector.Render(ref format))
            {
                tempConfig.TimestampFormat = format;
                changed = true;
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (fieldOrdering.Render()) changed = true;

            return changed;
        }

        private void DrawButtonRow()
        {
            var windowWidth = ImGui.GetWindowWidth();
            var buttonWidth = (windowWidth - Constants.UI.ButtonRowMargin) / 3;
            var buttonSize = new Vector2(buttonWidth, Constants.UI.ButtonHeight);

            var dirty = configChanged;

            // Save button — amber tint when dirty, disabled when clean.
            ImGui.BeginDisabled(!dirty);
            if (dirty)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, Constants.UI.SaveDirtyTint);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Constants.UI.SaveDirtyHover);
            }

            if (ImGui.Button("Save Settings", buttonSize))
            {
                // Reset visual state synchronously, before any I/O.
                configChanged = false;
                ApplyChanges();
                config.Save();
                Plugin.Logger?.UserMessage("Settings saved successfully!");
                IsOpen = false;
            }

            if (dirty) ImGui.PopStyleColor(2);
            ImGui.EndDisabled();

            ImGui.SameLine(0, Constants.UI.ButtonGap);

            ImGui.BeginDisabled(!dirty);
            if (ImGui.Button("Revert Changes", buttonSize))
            {
                CopyConfigToTemp();
                fieldOrdering.InitializeFromString(tempConfig.SelectedFields);
                RefreshPreviews();
                configChanged = false;
                Plugin.Logger?.UserMessage("Changes reverted to last saved settings.");
            }
            ImGui.EndDisabled();

            ImGui.SameLine(0, Constants.UI.ButtonGap);

            if (ImGui.Button("Reset to Defaults", buttonSize))
            {
                ResetToDefaults();
                RefreshPreviews();
                configChanged = true;
                Plugin.Logger?.UserMessage("Settings reset to defaults.");
            }
        }

        private void RefreshPreviews()
        {
            // Filename preview — derives from live game state when in-game,
            // sensible defaults otherwise.
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
            var subLocation = "";
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
                            position = roundedCoords.Z == 0
                                ? $" ({roundedCoords.X:0.0},{roundedCoords.Y:0.0})"
                                : $" ({roundedCoords.X:0.0},{roundedCoords.Y:0.0},{roundedCoords.Z:0.0})";
                        }
                    }

                    weather = Client.GetCurrentWeatherName();
                    eorzeaTime = Client.GetCurrentEorzeaDateTime().DetermineDayPeriod(true);
                    // Landmark for the preview: most specific place name, else the zone.
                    subLocation = Client.GetCurrentLandmarkName() ?? map;
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.Warning($"Error getting game data for example: {ex.Message}");
                }
            }

            filenamePreview.RefreshPreview(
                tempConfig.TimestampFormat,
                fieldOrdering.GetActiveFieldsInOrder(),
                characterName: character,
                map: map,
                subLocation: subLocation,
                position: position,
                eorzeaTime: eorzeaTime,
                weather: weather,
                shaderPreset: shaderPreset);

            // Metadata preview — uses live state when available, otherwise example data.
            StateSnapshot snapshot;
            if (Plugin.ClientState.IsLoggedIn && Plugin.ObjectTable.LocalPlayer != null)
            {
                try { snapshot = StateCapture.Capture(Guid.CreateVersion7()); }
                catch { snapshot = MetadataPreviewComponent.ExampleSnapshot(); }
            }
            else
            {
                snapshot = MetadataPreviewComponent.ExampleSnapshot();
            }
            metadataPreview.RefreshPreview(snapshot, tempConfig);
        }

        private void ApplyChanges()
        {
            config.SelectedFields = fieldOrdering.GetSelectedFieldsString();
            config.TimestampFormat = tempConfig.TimestampFormat;
            config.EmbedMetadata = tempConfig.EmbedMetadata;
            config.MetadataFields = new System.Collections.Generic.Dictionary<string, bool>(tempConfig.MetadataFields);

            var verbosityChanged = config.LogVerbosity != tempConfig.LogVerbosity;
            config.LogVerbosity = tempConfig.LogVerbosity;

            if (verbosityChanged)
            {
                Plugin.Logger?.SetVerbosity(config.LogVerbosity);
                Plugin.Logger?.UserMessage($"Logging verbosity: {config.LogVerbosity}");
            }

            // Gear publishing settings. The publisher reads these live each tick;
            // when the feature is switched off, clear what we put on the bus.
            var gearWasEnabled = config.GearPublishEnabled;
            config.GearPublishEnabled = tempConfig.GearPublishEnabled;
            config.GearShadingwayPort = tempConfig.GearShadingwayPort;
            if (gearWasEnabled && !config.GearPublishEnabled)
                _ = Plugin.GearPublisher?.FlushAsync();
        }

        private void ResetToDefaults()
        {
            tempConfig.SelectedFields = Configuration.GetDefaultSelectedFields();
            tempConfig.TimestampFormat = TimestampFormat.Compact;
            tempConfig.EmbedMetadata = false;
            tempConfig.MetadataFields = Configuration.DefaultMetadataFields();
            tempConfig.LogVerbosity = LogVerbosity.Status;
            tempConfig.GearPublishEnabled = false;
            tempConfig.GearShadingwayPort = 48756;
            fieldOrdering.InitializeFromString(tempConfig.SelectedFields);
        }

        // Required by Window's IDisposable contract; nothing managed to release here.
        public void Dispose() { }
    }
}
