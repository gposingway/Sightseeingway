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
            };
        }

        public override void Draw()
        {
            // Reserve room at the bottom for the diagnostics + button rows.
            const float reservedHeight = 280f;
            var availableY = Math.Max(120f, ImGui.GetContentRegionAvail().Y - reservedHeight);

            if (ImGui.BeginTable("##MainTwoColumn", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
            {
                ImGui.TableNextRow();

                // Left column — Filename
                ImGui.TableNextColumn();
                ImGui.BeginChild("##FilenameColumn", new Vector2(0, availableY), false);
                if (DrawFilenameColumn()) configChanged = true;
                ImGui.EndChild();

                // Right column — Metadata
                ImGui.TableNextColumn();
                ImGui.BeginChild("##MetadataColumn", new Vector2(0, availableY), false);
                if (metadataConfig.Render(tempConfig)) configChanged = true;
                ImGui.EndChild();

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

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            DrawButtonRow();

            // Refresh previews if anything changed during this draw.
            if (configChanged)
            {
                tempConfig.SelectedFields = fieldOrdering.GetSelectedFieldsString();
                RefreshPreviews();
            }
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
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.Warning($"Error getting game data for example: {ex.Message}");
                }
            }

            filenamePreview.RefreshPreview(
                tempConfig.TimestampFormat,
                fieldOrdering.GetActiveFieldsInOrder(),
                character, map, position, eorzeaTime, weather, shaderPreset);

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
        }

        private void ResetToDefaults()
        {
            tempConfig.SelectedFields = Configuration.GetDefaultSelectedFields();
            tempConfig.TimestampFormat = TimestampFormat.Compact;
            tempConfig.EmbedMetadata = false;
            tempConfig.MetadataFields = Configuration.DefaultMetadataFields();
            tempConfig.LogVerbosity = LogVerbosity.Status;
            fieldOrdering.InitializeFromString(tempConfig.SelectedFields);
        }

        // Required by Window's IDisposable contract; nothing managed to release here.
        public void Dispose() { }
    }
}
