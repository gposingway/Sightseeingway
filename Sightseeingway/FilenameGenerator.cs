using System;
using System.Collections.Generic;
using System.Linq;

namespace Sightseeingway
{
    /// <summary>
    /// Centralizes filename generation logic used by both IO.cs and UI.cs
    /// </summary>
    public static class FilenameGenerator
    {
        /// <summary>
        /// Formats a timestamp according to the specified format
        /// </summary>
        public static string FormatTimestamp(DateTime dateTime, TimestampFormat format)
        {
            return format switch
            {
                TimestampFormat.Regular => dateTime.ToString(Constants.Formats.RegularTimestamp),
                TimestampFormat.Readable => dateTime.ToString(Constants.Formats.ReadableTimestamp),
                _ => dateTime.ToString(Constants.Formats.CompactTimestamp),
            };
        }

        /// <summary>
        /// Adds a separator and the part name if the part isn't empty or "Unknown"
        /// </summary>
        public static string FormatNamePart(string part)
        {
            return string.IsNullOrEmpty(part) || part == "Unknown" ? "" : "-" + part;
        }

        /// <summary>
        /// Creates an ordered list of FilenameField items from a comma-separated string.
        /// </summary>
        public static List<FilenameField> StringToFieldList(string? fieldsString)
        {
            if (string.IsNullOrEmpty(fieldsString)) return new List<FilenameField>();

            return fieldsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Enum.TryParse<FilenameField>(s, out var field) ? (FilenameField?)field : null)
                .Where(f => f.HasValue)
                .Select(f => f!.Value)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Converts a list of FilenameField items to a comma-separated string.
        /// </summary>
        public static string FieldListToString(IEnumerable<FilenameField> fieldList)
        {
            return string.Join(",", fieldList.Select(f => f.ToString()));
        }

        /// <summary>
        /// Returns a new list with Timestamp guaranteed at index 0. Does not mutate the input.
        /// </summary>
        public static List<FilenameField> EnsureTimestampIsFirst(IEnumerable<FilenameField> fields)
        {
            var list = new List<FilenameField> { FilenameField.Timestamp };
            foreach (var f in fields)
            {
                if (f != FilenameField.Timestamp && !list.Contains(f)) list.Add(f);
            }
            return list;
        }

        /// <summary>
        /// Generates a filename based on the provided context and selected fields
        /// </summary>
        public static string GenerateFilename(
            DateTime timestamp, 
            TimestampFormat timestampFormat,
            string character,
            string map,
            string subLocation,
            string position,
            string eorzeaTime,
            string weather,
            string shaderPreset,
            bool effectsEnabled,
            List<FilenameField> activeFieldsInOrder,
            string fileExtension = ".png")
        {
            var formattedTimestamp = FormatTimestamp(timestamp, timestampFormat);
            var parts = new List<string>();

            foreach (var field in activeFieldsInOrder)
            {
                switch (field)
                {
                    case FilenameField.Timestamp:
                        parts.Add(formattedTimestamp);
                        break;
                    case FilenameField.CharacterName:
                        parts.Add(FormatNamePart(character));
                        break;
                    case FilenameField.MapName:
                        parts.Add(FormatNamePart(map));
                        break;
                    case FilenameField.SubLocation:
                        // The landmark resolves to the most specific place name available
                        // (sub-area → area → zone). De-dup: when the Zone (MapName) field is
                        // also active and would emit the same text, suppress the landmark so
                        // the location isn't repeated. Order-independent — keyed on whether
                        // MapName is active, not on emission order.
                        var zoneAlsoShown = activeFieldsInOrder.Contains(FilenameField.MapName)
                            && string.Equals(subLocation, map, StringComparison.Ordinal);
                        if (!zoneAlsoShown)
                            parts.Add(FormatNamePart(subLocation));
                        break;
                    case FilenameField.Position:
                        parts.Add(position); // Position already has spaces or is empty
                        break;
                    case FilenameField.EorzeaTime:
                        parts.Add(FormatNamePart(eorzeaTime));
                        break;
                    case FilenameField.Weather:
                        parts.Add(FormatNamePart(weather));
                        break;
                    case FilenameField.ShaderPreset:
                        if (effectsEnabled && !string.IsNullOrEmpty(shaderPreset))
                        {
                            parts.Add(FormatNamePart(shaderPreset));
                        }
                        break;
                }
            }

            // Join parts, removing empty ones, and add extension
            return string.Join("", parts.Where(s => !string.IsNullOrEmpty(s))) + fileExtension;
        }
    }
}