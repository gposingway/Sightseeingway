using System;
using System.Collections.Generic;

namespace Sightseeingway.CharacterCard
{
    /// <summary>
    /// Resolves the 8 customize colour bytes into RGB + their 1-based grid cell, using the game's
    /// <c>chara/xls/charamake/human.cmp</c> palette. Offsets, the skin/hair index, and the RGBA
    /// byte order are harvested verbatim from Glamourer (ColorParameters + CustomizeSetFactory):
    /// the cmp is a flat <c>uint[]</c> of RGBA colours (low byte = R, no swap); the picker grid is
    /// 8 columns. Framework-thread only (reads a game file); the cmp is loaded once and cached.
    /// </summary>
    public static class CharColors
    {
        private const int GridColumns = 8;

        private static uint[]? _cmp;
        private static bool _loaded;

        private static uint[] Cmp()
        {
            if (_loaded) return _cmp ?? Array.Empty<uint>();
            _loaded = true;
            try
            {
                var file = Plugin.DataManager.GetFile("chara/xls/charamake/human.cmp");
                if (file != null && file.Data.Length >= 4)
                {
                    _cmp = new uint[file.Data.Length / 4];
                    Buffer.BlockCopy(file.Data, 0, _cmp, 0, _cmp.Length * 4);
                }
            }
            catch (Exception ex)
            {
                // Absent/modified (e.g. TexTools) → degrade to no colour swatches, not a crash.
                Plugin.Logger?.Debug($"human.cmp load failed: {ex.Message}");
            }
            return _cmp ?? Array.Empty<uint>();
        }

        /// <summary>Resolves all customize colours from the raw 26-byte array. Empty if the cmp
        /// palette is unavailable (the rest of the character card still publishes).</summary>
        public static IReadOnlyList<CharColor> Resolve(byte[] c)
        {
            var cmp = Cmp();
            if (cmp.Length == 0 || c.Length < 26) return Array.Empty<CharColor>();

            var subRace = c[4];                 // Tribe/clan, 1-based (Midlander=1 … Veena=16)
            var female = c[1] != 0;             // Sex byte: 0 male, else female
            var idx = (((subRace - 1) * 2 + (female ? 1 : 0)) * 5 + 3);

            var list = new List<CharColor>(8);
            // Standard pickers — the customize byte IS the dense grid index.
            AddStandard(list, cmp, CharNaming.Prefix + "SKINCOLOR",       c[8],  0x1200 + (idx << 8));
            AddStandard(list, cmp, CharNaming.Prefix + "EYECOLORR",       c[9],  0);
            AddStandard(list, cmp, CharNaming.Prefix + "HAIRCOLOR",       c[10], 0x1200 + ((idx + 1) << 8));
            AddStandard(list, cmp, CharNaming.Prefix + "HIGHLIGHTSCOLOR", c[11], 256);
            AddStandard(list, cmp, CharNaming.Prefix + "TATTOOCOLOR",     c[13], 0);
            AddStandard(list, cmp, CharNaming.Prefix + "EYECOLORL",       c[15], 0);
            // Remapped pickers — Dark (bytes 0–95) ++ Light (bytes 128–223).
            AddRemapped(list, cmp, CharNaming.Prefix + "LIPCOLOR",        c[20], 512,  1024);
            AddRemapped(list, cmp, CharNaming.Prefix + "FACEPAINTCOLOR",  c[25], 640,  1152);
            return list;
        }

        private static void AddStandard(List<CharColor> list, uint[] cmp, string key, byte v, int offset)
        {
            var i = offset + v;
            if (i < 0 || i >= cmp.Length) return;
            var (r, g, b) = Rgb(cmp[i]);
            var (col, row) = Cell(v);
            list.Add(new CharColor(key, v, r, g, b, col, row));
        }

        private static void AddRemapped(List<CharColor> list, uint[] cmp, string key, byte v, int darkOffset, int lightOffset)
        {
            var dense = v < 128 ? v : (v - 128) + 96;
            var i = v < 128 ? darkOffset + v : lightOffset + (v - 128);
            if (i < 0 || i >= cmp.Length) return;
            var (r, g, b) = Rgb(cmp[i]);
            var (col, row) = Cell(dense);
            list.Add(new CharColor(key, v, r, g, b, col, row));
        }

        // cmp uints are RGBA, low byte = R (Glamourer ColorParameters); take RGB, drop alpha.
        private static (byte R, byte G, byte B) Rgb(uint c)
            => ((byte)(c & 0xFF), (byte)((c >> 8) & 0xFF), (byte)((c >> 16) & 0xFF));

        // 1-based (col, row) for a dense index in the 8-column picker grid (row-major).
        private static (int Col, int Row) Cell(int dense)
            => (dense % GridColumns + 1, dense / GridColumns + 1);
    }
}
