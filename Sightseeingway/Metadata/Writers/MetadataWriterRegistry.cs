using System;
using System.Collections.Generic;
using System.IO;

namespace Sightseeingway.Metadata.Writers
{
    /// <summary>
    /// Maps file extensions to the writer responsible for that format. New
    /// formats slot in by adding a single entry; nothing else changes.
    /// </summary>
    public static class MetadataWriterRegistry
    {
        private static readonly Dictionary<string, IMetadataWriter> Writers =
            new(StringComparer.OrdinalIgnoreCase)
            {
                [".png"] = new PngMetadataWriter(),
                [".jpg"] = new JpegMetadataWriter(),
                [".jpeg"] = new JpegMetadataWriter(),
            };

        public static IMetadataWriter? GetFor(string filePath)
        {
            var ext = Path.GetExtension(filePath);
            return string.IsNullOrEmpty(ext) ? null
                : Writers.TryGetValue(ext, out var writer) ? writer
                : null;
        }
    }
}
