using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;
using Sightseeingway.Metadata;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Sightseeingway.UI.Components
{
    /// <summary>
    /// Live preview of the JSON payload that would be embedded into a
    /// screenshot under the user's current metadata configuration. Backed by
    /// an example StateSnapshot — readable on the character-select screen,
    /// updated to live state when in-game.
    /// </summary>
    public class MetadataPreviewComponent
    {
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        private string _json = string.Empty;
        private bool _expanded;

        public void RefreshPreview(StateSnapshot snapshot, Configuration config)
        {
            try
            {
                var filtered = snapshot.FilteredFor(config);
                _json = JsonConvert.SerializeObject(filtered, JsonSettings);
            }
            catch (Exception ex)
            {
                _json = $"// Preview render failed: {ex.Message}";
            }
        }

        public void Render()
        {
            ImGui.TextColored(Constants.UI.ExampleHeaderColor, "Embedded JSON preview:");
            ImGui.SameLine();
            if (ImGui.SmallButton(_expanded ? "Hide" : "Show")) _expanded = !_expanded;

            if (!_expanded) return;

            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.10f, 0.10f, 0.12f, 1.0f));
            ImGui.BeginChild("##MetadataJsonPreview", new Vector2(-1, 220), true);
            ImGui.TextUnformatted(_json);
            ImGui.EndChild();
            ImGui.PopStyleColor();
        }

        /// <summary>
        /// Builds an offline example StateSnapshot for the preview when no
        /// live game state is available (character select, in-menu, etc).
        /// </summary>
        public static StateSnapshot ExampleSnapshot() => new()
        {
            CorrelationId = Guid.CreateVersion7(),
            Timestamp = DateTime.UtcNow,
            Character = new CharacterInfo
            {
                Name = "Wol of Light",
                World = new WorldInfo("Brynhildr", "Brynhildr"),
                Race = new NamedId(1, "Hyur"),
                Tribe = new NamedId(2, "Highlander"),
                Sex = "female",
                Job = new JobInfo(23, "Bard", 100),
                Title = "Warrior of Light",
                GrandCompany = new GrandCompanyInfo(1, "Maelstrom", "Storm Captain"),
                Mount = new NamedId(5, "Magitek Armor"),
                Minion = new NamedId(12, "Wind-up Cursor"),
            },
            FreeCompany = new FreeCompanyInfo("Eorzean Vagabonds", "EVAGB"),
            Location = new LocationInfo(
                new NamedId(129, "Limsa Lominsa Upper Decks"),
                new NamedId(12, "Limsa Lominsa Upper Decks"),
                new Position(10.5f, 15.2f, 0.0f)),
            Time = new TimeInfo(new EorzeaTime("Day", 14)),
            Weather = new NamedId(1, "Clear Skies"),
            Shader = new ShaderInfo("Default", "MyPreset"),
            Flags = new List<string> { "gameplay", "mounted" },
        };
    }
}
