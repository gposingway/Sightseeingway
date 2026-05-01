using Dalamud.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sightseeingway
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 4;

        private string _selectedFields = GetDefaultSelectedFields();
        private List<FilenameField>? _cachedFields;

        // Persisted as a comma-separated string for back-compat (e.g. "Timestamp,CharacterName,...").
        public string SelectedFields
        {
            get => _selectedFields;
            set
            {
                _selectedFields = value;
                _cachedFields = null;
            }
        }

        // Typed accessor — parses once and caches until SelectedFields is reassigned.
        [JsonIgnore]
        public IReadOnlyList<FilenameField> Fields =>
            _cachedFields ??= FilenameGenerator.StringToFieldList(_selectedFields);

        public TimestampFormat TimestampFormat { get; set; } = TimestampFormat.Compact;

        public bool DebugMode { get; set; } = false;

        public bool ShowNameChangesInChat { get; set; } = true;

        public static string GetDefaultSelectedFields() =>
            string.Join(",", Enum.GetValues(typeof(FilenameField)).Cast<FilenameField>().Select(f => f.ToString()));

        public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
    }

    public enum FilenameField
    {
        Timestamp,
        CharacterName,
        MapName,
        Position,
        EorzeaTime,
        Weather,
        ShaderPreset
    }

    public enum TimestampFormat
    {
        Compact,    // yyyyMMddHHmmssfff
        Regular,    // yyyyMMdd-HHmmss-fff
        Readable    // yyyy-MM-dd_HH-mm-ss.fff
    }
}
