using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Sightseeingway.Metadata
{
    /// <summary>
    /// Immutable record of all game state captured at the moment closest to a
    /// screenshot. Once constructed it holds only resolved values (localized
    /// names, integer IDs, normalized strings) — no live game-object references —
    /// so it is safe to read from any thread.
    ///
    /// Serializes to the v1 metadata schema documented in docs/schema/v1.md.
    /// </summary>
    public sealed record StateSnapshot
    {
        [JsonProperty("schema", Order = 0)]
        public string Schema => "sightseeingway/v1";

        [JsonProperty("correlationId", Order = 1)]
        public Guid CorrelationId { get; init; }

        [JsonProperty("timestamp", Order = 2)]
        public DateTime Timestamp { get; init; }

        [JsonProperty("character", NullValueHandling = NullValueHandling.Ignore, Order = 3)]
        public CharacterInfo? Character { get; init; }

        [JsonProperty("freeCompany", NullValueHandling = NullValueHandling.Ignore, Order = 4)]
        public FreeCompanyInfo? FreeCompany { get; init; }

        [JsonProperty("location", NullValueHandling = NullValueHandling.Ignore, Order = 5)]
        public LocationInfo? Location { get; init; }

        [JsonProperty("time", NullValueHandling = NullValueHandling.Ignore, Order = 6)]
        public TimeInfo? Time { get; init; }

        [JsonProperty("weather", NullValueHandling = NullValueHandling.Ignore, Order = 7)]
        public NamedId? Weather { get; init; }

        [JsonProperty("shader", NullValueHandling = NullValueHandling.Ignore, Order = 8)]
        public ShaderInfo? Shader { get; init; }

        [JsonProperty("display", NullValueHandling = NullValueHandling.Ignore, Order = 9)]
        public DisplayInfo? Display { get; init; }

        [JsonProperty("flags", NullValueHandling = NullValueHandling.Ignore, Order = 10)]
        public IReadOnlyList<string>? Flags { get; init; }

        /// <summary>
        /// Returns a copy with branches dropped according to the user's per-field
        /// metadata opt-in configuration. The full snapshot is preserved on disk
        /// in the sidecar; this filter only shapes what gets embedded into the
        /// final image.
        /// </summary>
        public StateSnapshot FilteredFor(Configuration config)
        {
            if (config == null) return this;

            CharacterInfo? filteredCharacter = null;
            if (Character != null)
            {
                var characterData = config.IsMetadataFieldEnabled(MetadataField.CharacterData);
                var characterWorld = config.IsMetadataFieldEnabled(MetadataField.CharacterWorld);
                var characterMount = config.IsMetadataFieldEnabled(MetadataField.CharacterMount);
                var grandCompany = config.IsMetadataFieldEnabled(MetadataField.GrandCompany);

                if (characterData || characterWorld || characterMount || grandCompany)
                {
                    filteredCharacter = new CharacterInfo
                    {
                        Name  = characterData ? Character.Name  : null,
                        Race  = characterData ? Character.Race  : null,
                        Tribe = characterData ? Character.Tribe : null,
                        Sex   = characterData ? Character.Sex   : null,
                        Job   = characterData ? Character.Job   : null,
                        Title = characterData ? Character.Title : null,
                        World = characterWorld ? Character.World : null,
                        Mount  = characterMount ? Character.Mount  : null,
                        Minion = characterMount ? Character.Minion : null,
                        GrandCompany = grandCompany ? Character.GrandCompany : null,
                    };
                }
            }

            return this with
            {
                Character = filteredCharacter,
                FreeCompany = config.IsMetadataFieldEnabled(MetadataField.FreeCompany) ? FreeCompany : null,
                Location = config.IsMetadataFieldEnabled(MetadataField.Location) ? Location : null,
                Time = config.IsMetadataFieldEnabled(MetadataField.Time) ? Time : null,
                Weather = config.IsMetadataFieldEnabled(MetadataField.Weather) ? Weather : null,
                Shader = config.IsMetadataFieldEnabled(MetadataField.Shader) ? Shader : null,
                Display = config.IsMetadataFieldEnabled(MetadataField.Display) ? Display : null,
                Flags = config.IsMetadataFieldEnabled(MetadataField.Flags) ? Flags : null,
            };
        }
    }

    public sealed record NamedId(
        [property: JsonProperty("id")] uint Id,
        [property: JsonProperty("name")] string Name);

    public sealed record CharacterInfo
    {
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string? Name { get; init; }

        [JsonProperty("world", NullValueHandling = NullValueHandling.Ignore)]
        public WorldInfo? World { get; init; }

        [JsonProperty("race", NullValueHandling = NullValueHandling.Ignore)]
        public NamedId? Race { get; init; }

        [JsonProperty("tribe", NullValueHandling = NullValueHandling.Ignore)]
        public NamedId? Tribe { get; init; }

        [JsonProperty("sex", NullValueHandling = NullValueHandling.Ignore)]
        public string? Sex { get; init; }

        [JsonProperty("job", NullValueHandling = NullValueHandling.Ignore)]
        public JobInfo? Job { get; init; }

        [JsonProperty("title", NullValueHandling = NullValueHandling.Ignore)]
        public string? Title { get; init; }

        [JsonProperty("grandCompany", NullValueHandling = NullValueHandling.Ignore)]
        public GrandCompanyInfo? GrandCompany { get; init; }

        [JsonProperty("mount", NullValueHandling = NullValueHandling.Ignore)]
        public NamedId? Mount { get; init; }

        [JsonProperty("minion", NullValueHandling = NullValueHandling.Ignore)]
        public NamedId? Minion { get; init; }
    }

    public sealed record WorldInfo(
        [property: JsonProperty("current", NullValueHandling = NullValueHandling.Ignore)] string? Current,
        [property: JsonProperty("home", NullValueHandling = NullValueHandling.Ignore)] string? Home);

    public sealed record JobInfo(
        [property: JsonProperty("id")] uint Id,
        [property: JsonProperty("name")] string Name,
        [property: JsonProperty("level")] int Level);

    public sealed record GrandCompanyInfo(
        [property: JsonProperty("id")] uint Id,
        [property: JsonProperty("name")] string Name,
        [property: JsonProperty("rank", NullValueHandling = NullValueHandling.Ignore)] string? Rank);

    public sealed record FreeCompanyInfo(
        [property: JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)] string? Name,
        [property: JsonProperty("tag", NullValueHandling = NullValueHandling.Ignore)] string? Tag);

    public sealed record LocationInfo(
        [property: JsonProperty("territory", NullValueHandling = NullValueHandling.Ignore)] NamedId? Territory,
        [property: JsonProperty("map", NullValueHandling = NullValueHandling.Ignore)] NamedId? Map,
        [property: JsonProperty("position", NullValueHandling = NullValueHandling.Ignore)] Position? Position);

    public sealed record Position(
        [property: JsonProperty("x")] float X,
        [property: JsonProperty("y")] float Y,
        [property: JsonProperty("z")] float Z);

    public sealed record TimeInfo(
        [property: JsonProperty("eorzea", NullValueHandling = NullValueHandling.Ignore)] EorzeaTime? Eorzea);

    public sealed record EorzeaTime(
        [property: JsonProperty("period")] string Period,
        [property: JsonProperty("hour")] int Hour);

    public sealed record ShaderInfo(
        [property: JsonProperty("collection", NullValueHandling = NullValueHandling.Ignore)] string? Collection,
        [property: JsonProperty("preset", NullValueHandling = NullValueHandling.Ignore)] string? Preset,
        [property: JsonProperty("path", NullValueHandling = NullValueHandling.Ignore)] string? Path);

    public sealed record DisplayInfo(
        [property: JsonProperty("width")] int Width,
        [property: JsonProperty("height")] int Height,
        [property: JsonProperty("aspectRatio")] double AspectRatio,
        [property: JsonProperty("screenType", NullValueHandling = NullValueHandling.Ignore)] string? ScreenType);
}
