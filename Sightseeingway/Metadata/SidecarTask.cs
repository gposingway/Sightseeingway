using Newtonsoft.Json;
using System;

namespace Sightseeingway.Metadata
{
    /// <summary>
    /// On-disk durable representation of a single screenshot's pipeline task.
    ///
    /// Persisted as &lt;target&gt;.sw-pending.json next to the file it describes.
    /// Each task moves through two stages — rename and inject — both
    /// independently idempotent. The filesystem is the source of truth on
    /// recovery; <see cref="Renamed"/> and <see cref="Injected"/> are
    /// fast-path hints, not authoritative state.
    /// </summary>
    public sealed record SidecarTask
    {
        /// <summary>The sidecar file extension, including leading dot.</summary>
        public const string Suffix = ".sw-pending.json";

        [JsonProperty("correlationId")]
        public Guid CorrelationId { get; init; }

        [JsonProperty("originalPath")]
        public string OriginalPath { get; init; } = string.Empty;

        [JsonProperty("targetPath")]
        public string TargetPath { get; init; } = string.Empty;

        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; init; }

        [JsonProperty("renamed")]
        public bool Renamed { get; init; }

        [JsonProperty("injected")]
        public bool Injected { get; init; }

        [JsonProperty("snapshot")]
        public StateSnapshot Snapshot { get; init; } = null!;

        public SidecarTask With(bool? renamed = null, bool? injected = null) => this with
        {
            Renamed = renamed ?? Renamed,
            Injected = injected ?? Injected,
        };
    }
}
