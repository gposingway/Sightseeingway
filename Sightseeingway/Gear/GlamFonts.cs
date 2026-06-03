using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.Fonts;

namespace Sightseeingway.Gear
{
    /// <summary>
    /// Loads the bundled OFL fonts (embedded in the assembly) used to render gear
    /// name labels, so output is identical for every player. Keyed by the bus-name
    /// token in <see cref="TextureNaming.NameFontKeys"/>.
    /// </summary>
    public static class GlamFonts
    {
        private static readonly Dictionary<string, string> KeyToResource = new()
        {
            ["INTER"]     = "Inter.ttf",
            ["CINZEL"]    = "Cinzel.ttf",
            ["GARAMOND"]  = "EBGaramond.ttf",
            ["CORMORANT"] = "Cormorant.ttf",
        };

        private static readonly object Lock = new();
        private static FontCollection? _collection; // held so the FontFamily handles stay valid
        private static Dictionary<string, FontFamily>? _byKey;

        /// <summary>The loaded family for a font key, or null if that font failed to load.</summary>
        public static FontFamily? Get(string key)
        {
            EnsureLoaded();
            return _byKey != null && _byKey.TryGetValue(key, out var family) ? family : null;
        }

        private static void EnsureLoaded()
        {
            if (_byKey != null) return;
            lock (Lock)
            {
                if (_byKey != null) return;
                _byKey = Load();
            }
        }

        private static Dictionary<string, FontFamily> Load()
        {
            var result = new Dictionary<string, FontFamily>();
            try
            {
                _collection = new FontCollection();
                var asm = typeof(GlamFonts).Assembly;
                var resources = asm.GetManifestResourceNames();

                foreach (var key in TextureNaming.NameFontKeys)
                {
                    if (!KeyToResource.TryGetValue(key, out var file)) continue;

                    var resName = resources.FirstOrDefault(n => n.EndsWith(file, StringComparison.OrdinalIgnoreCase));
                    if (resName == null)
                    {
                        Plugin.Logger?.Warning($"Bundled font not found: {file}");
                        continue;
                    }

                    using var stream = asm.GetManifestResourceStream(resName);
                    if (stream == null) continue;

                    result[key] = _collection.Add(stream);
                }

                Plugin.Logger?.Debug($"Loaded {result.Count} bundled fonts: {string.Join(", ", result.Keys)}");
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Error($"Font loading failed: {ex.Message}", ex);
            }
            return result;
        }
    }
}
