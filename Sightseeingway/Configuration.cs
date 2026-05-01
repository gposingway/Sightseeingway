using Dalamud.Configuration;
using Newtonsoft.Json;
using Sightseeingway.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sightseeingway
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public const int CurrentVersion = 5;

        public int Version { get; set; } = CurrentVersion;

        // --- Filename composition (unchanged from v1.2) ---

        private string _selectedFields = GetDefaultSelectedFields();
        private List<FilenameField>? _cachedFields;

        public string SelectedFields
        {
            get => _selectedFields;
            set
            {
                _selectedFields = value;
                _cachedFields = null;
            }
        }

        [JsonIgnore]
        public IReadOnlyList<FilenameField> Fields =>
            _cachedFields ??= FilenameGenerator.StringToFieldList(_selectedFields);

        public TimestampFormat TimestampFormat { get; set; } = TimestampFormat.Compact;

        // --- v1.3 additions ---

        public bool EmbedMetadata { get; set; } = false;

        /// <summary>
        /// Per-field opt-in for metadata embedding. Keys are
        /// <see cref="MetadataField"/> enum names; missing keys default to off.
        /// </summary>
        public Dictionary<string, bool> MetadataFields { get; set; } = DefaultMetadataFields();

        public LogVerbosity LogVerbosity { get; set; } = LogVerbosity.Status;

        // --- Deprecated (kept for one version cycle for cross-version safety) ---

        [Obsolete("Migrated into LogVerbosity (true → Status, false → Quiet) in v5.")]
        public bool ShowNameChangesInChat { get; set; } = true;

        [Obsolete("Migrated into LogVerbosity (true → Debug) in v5.")]
        public bool DebugMode { get; set; } = false;

        public static string GetDefaultSelectedFields() =>
            string.Join(",", Enum.GetValues(typeof(FilenameField)).Cast<FilenameField>().Select(f => f.ToString()));

        public static Dictionary<string, bool> DefaultMetadataFields() => new()
        {
            // Scene group (defaults all on)
            [nameof(MetadataField.Location)]    = true,
            [nameof(MetadataField.Time)]        = true,
            [nameof(MetadataField.Weather)]     = true,
            [nameof(MetadataField.Flags)]       = true,
            [nameof(MetadataField.Shader)]      = true,
            [nameof(MetadataField.Display)]     = true,

            // Character group
            [nameof(MetadataField.CharacterData)]   = true,   // bundled: name, race/tribe/sex, job, title
            [nameof(MetadataField.CharacterWorld)]  = false,
            [nameof(MetadataField.CharacterMount)]  = true,

            // Affiliation group (defaults all off)
            [nameof(MetadataField.FreeCompany)]   = false,
            [nameof(MetadataField.GrandCompany)]  = false,
        };

        public bool IsMetadataFieldEnabled(MetadataField field) =>
            MetadataFields.TryGetValue(field.ToString(), out var enabled) && enabled;

        public void Save() => Plugin.PluginInterface.SavePluginConfig(this);

        /// <summary>
        /// Migrates a configuration loaded from disk to <see cref="CurrentVersion"/>.
        /// Idempotent — re-running on an already-current config is a no-op.
        /// </summary>
        public void Migrate()
        {
#pragma warning disable CS0618 // referencing obsolete migration-only fields
            if (Version < 5)
            {
                LogVerbosity = DebugMode
                    ? LogVerbosity.Debug
                    : (ShowNameChangesInChat ? LogVerbosity.Status : LogVerbosity.Quiet);

                MetadataFields ??= DefaultMetadataFields();

                Version = CurrentVersion;
            }
#pragma warning restore CS0618
        }
    }

    public enum FilenameField
    {
        Timestamp,
        CharacterName,
        MapName,
        Position,
        EorzeaTime,
        Weather,
        ShaderPreset,
    }

    public enum TimestampFormat
    {
        Compact,    // yyyyMMddHHmmssfff
        Regular,    // yyyyMMdd-HHmmss-fff
        Readable,   // yyyy-MM-dd_HH-mm-ss.fff
    }

    /// <summary>
    /// Field keys for metadata embedding. Each maps to one or more
    /// <see cref="StateSnapshot"/> branches that can be independently
    /// included or omitted from the embedded JSON payload based on user
    /// configuration.
    /// </summary>
    public enum MetadataField
    {
        // Scene group
        Location,
        Time,
        Weather,
        Flags,
        Shader,
        Display,

        // Character group
        CharacterData,    // bundle: name, race/tribe/sex, job/level, title
        CharacterWorld,
        CharacterMount,   // bundle: mount, minion

        // Affiliation group
        FreeCompany,
        GrandCompany,
    }
}
