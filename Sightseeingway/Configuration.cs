using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sightseeingway
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 3; // Incremented version due to adding timestamp format option

        // Stores selected fields as a comma-separated string in display order.
        // Example: "Timestamp,CharacterName,MapName,Position,EorzeaTime,Weather,ShaderPreset"
        public string SelectedFields { get; set; } = GetDefaultSelectedFields();

        // The format to use for timestamps in filenames
        public TimestampFormat TimestampFormat { get; set; } = TimestampFormat.Compact;

        public static string GetDefaultSelectedFields() =>
            string.Join(",", Enum.GetValues(typeof(FilenameField)).Cast<FilenameField>().Select(f => f.ToString()));

        public void Save()
        {
            Plugin.PluginInterface.SavePluginConfig(this);
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
        ShaderPreset
    }

    public enum TimestampFormat
    {
        Compact,    // yyyyMMddHHmmssfff (e.g., 20250507123045678)
        Regular,    // yyyyMMdd-HHmmss-fff (e.g., 20250507-123045-678)
        Readable    // yyyy-MM-dd_HH-mm-ss.fff (e.g., 2025-05-07_12-30-45.678)
    }
}
