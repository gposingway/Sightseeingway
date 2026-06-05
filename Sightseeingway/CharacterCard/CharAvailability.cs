using System;
using System.Collections.Generic;
using Dalamud.Utility;
using Lumina.Excel.Sheets;

namespace Sightseeingway.CharacterCard
{
    /// <summary>
    /// Per-customize-option availability + choice-count + live localized caption, read from the
    /// player's <c>CharaMakeType</c> row (the game's own character-creator menu definition) — the
    /// same source Glamourer's SetAvailability/GetOptionNames use. This is how every race/gender
    /// quirk falls out of the data instead of a hand table:
    ///   • no Bust for males, no Tail/Ear shape for Hyur/Roegadyn, etc. → menu absent / count 0
    ///   • byte 21 is the SAME slider relabelled per race ("Muscle Tone" / "Ear Length" /
    ///     "Tail Length") — the live caption is correct without special-casing.
    /// Keyed by the customize BYTE index. Cached per (tribe, sex); the menu layout never changes.
    /// </summary>
    public static class CharAvailability
    {
        public readonly record struct OptionInfo(bool Available, int Max, string Caption);

        private static readonly Dictionary<int, IReadOnlyDictionary<int, OptionInfo>> Cache = new();

        /// <summary>Availability info per customize byte index for the given tribe (customize
        /// byte 4, 1-based) and sex (customize byte 1: 0 = male). Empty if the sheet is unavailable.</summary>
        public static IReadOnlyDictionary<int, OptionInfo> For(byte tribe, byte sex)
        {
            var key = tribe * 2 + (sex != 0 ? 1 : 0);
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var built = Build(tribe, sex);
            Cache[key] = built;
            return built;
        }

        private static IReadOnlyDictionary<int, OptionInfo> Build(byte tribe, byte sex)
        {
            var map = new Dictionary<int, OptionInfo>();
            if (tribe == 0) return map;
            try
            {
                var sheet = Plugin.DataManager.GetExcelSheet<CharaMakeType>();
                if (sheet == null) return map;
                var lobby = Plugin.DataManager.GetExcelSheet<Lobby>();

                // 32 rows, ordered (clan, gender): rowId = (tribe-1)*2 + sex. (Glamourer uses the
                // Penumbra Gender enum Male=1/Female=2; computing straight off the game bytes —
                // sex 0=male/1=female — lands on the same row without the enum-convention trap.)
                var rowId = (uint)((tribe - 1) * 2 + (sex != 0 ? 1 : 0));
                var row = sheet.GetRow(rowId);

                foreach (var menu in row.CharaMakeStruct)
                {
                    int byteIdx = (int)menu.Customize;
                    if (byteIdx <= 0) continue; // empty menu slot

                    var count = (int)menu.SubMenuNum;

                    var caption = string.Empty;
                    if (lobby != null && menu.Menu.RowId != 0)
                    {
                        var text = lobby.GetRow(menu.Menu.RowId);
                        if (text.RowId != 0) caption = text.Text.ExtractText();
                    }

                    map[byteIdx] = new OptionInfo(count > 0, count, caption);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"CharaMakeType availability read failed: {ex.Message}");
            }

            return map;
        }
    }
}
