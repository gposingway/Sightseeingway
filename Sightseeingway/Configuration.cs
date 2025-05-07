using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace Sightseeingway
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;
        
        // Filename fields toggles
        public bool IncludeCharacterName { get; set; } = true;
        public bool IncludeMapName { get; set; } = true;
        public bool IncludePosition { get; set; } = true;
        public bool IncludeEorzeaTime { get; set; } = true;
        public bool IncludeWeather { get; set; } = true;
        public bool IncludeShaderPreset { get; set; } = true;
        
        // Field order - first field is always Timestamp, other fields can be reordered
        public List<FilenameField> FieldOrder { get; set; } = new List<FilenameField>
        {
            FilenameField.Timestamp,
            FilenameField.CharacterName,
            FilenameField.MapName,
            FilenameField.Position,
            FilenameField.EorzeaTime,
            FilenameField.Weather,
            FilenameField.ShaderPreset
        };
        
        // Save configuration
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
}