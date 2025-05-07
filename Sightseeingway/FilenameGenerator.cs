using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Lumina.Excel.Sheets;

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
                TimestampFormat.Regular => dateTime.ToString("yyyyMMdd-HHmmss-fff"),
                TimestampFormat.Readable => dateTime.ToString("yyyy-MM-dd_HH-mm-ss.fff"),
                TimestampFormat.Compact or _ => dateTime.ToString("yyyyMMddHHmmssfff"),
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
        /// Creates an ordered list of FilenameField items from a comma-separated string
        /// </summary>
        public static List<FilenameField> StringToFieldList(string? fieldsString)
        {
            if (string.IsNullOrEmpty(fieldsString)) return new List<FilenameField>();
            
            return fieldsString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Enum.TryParse<FilenameField>(s.Trim(), out var field) ? field : (FilenameField?)null)
                .Where(f => f.HasValue)
                .Select(f => f!.Value)
                .Distinct() // Ensure no duplicates from string
                .ToList();
        }

        /// <summary>
        /// Converts a list of FilenameField items to a comma-separated string
        /// </summary>
        public static string FieldListToString(IEnumerable<FilenameField> fieldList)
        {
            return string.Join(",", fieldList.Select(f => f.ToString()));
        }

        /// <summary>
        /// Ensures Timestamp is the first field in the list, adding it if missing
        /// </summary>
        public static List<FilenameField> EnsureTimestampIsFirst(List<FilenameField> fields)
        {
            if (!fields.Any() || fields[0] != FilenameField.Timestamp)
            {
                fields.Remove(FilenameField.Timestamp); // Remove if it exists elsewhere
                fields.Insert(0, FilenameField.Timestamp); // Add to the beginning
            }
            return fields;
        }

        /// <summary>
        /// Generates a filename based on the provided context and selected fields
        /// </summary>
        public static string GenerateFilename(
            DateTime timestamp, 
            TimestampFormat timestampFormat,
            string character,
            string map,
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