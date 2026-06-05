using System;
using System.Collections.Generic;
using Dalamud.Game;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Sightseeingway.CharacterCard
{
    /// <summary>
    /// Resolves each customize choice to the game's creator thumbnail icon id — the same icons the
    /// character creator shows — so the publisher can render them as <c>CHAR_*_ICON</c> textures.
    /// Mirrors Glamourer's CustomizeSetFactory: a customize <em>value</em> maps through the menu
    /// sheets to a <c>CharaMakeCustomize</c> row, whose <c>Icon</c> is the thumbnail. Framework-thread
    /// only (reads Lumina sheets); the worker just renders the resolved id via <see cref="Gear.IconTexture"/>.
    ///
    /// Match is always on <c>CharaMakeCustomize.FeatureID == playerByte</c> — never row id or list
    /// position (FeatureID jumps within a row). Unavailable options resolve to 0 (no icon), which
    /// naturally gates them (e.g. no tail/ear for Hyur). Hrothgar hair is face-dependent and deferred.
    /// </summary>
    public static class CharIcons
    {
        private const uint LegacyTattooIcon = 137905; // fixed; not in any sheet (Glamourer SetFacialFeatures)

        public static IReadOnlyList<CharIcon> Resolve(byte[] c)
        {
            var list = new List<CharIcon>();
            if (c.Length < 26) return list;

            try
            {
                byte race = c[0], sex = c[1], tribe = c[4];
                if (tribe == 0) return list;

                var charaMake = Plugin.DataManager.GetExcelSheet<CharaMakeType>();
                var custom = Plugin.DataManager.GetExcelSheet<CharaMakeCustomize>();
                if (custom == null) return list;

                const string P = CharNaming.Prefix;
                var rowId = (uint)((tribe - 1) * 2 + (sex != 0 ? 1 : 0));

                // FACE (byte 5) + TAIL/EAR SHAPE (byte 22) — typed CharaMakeStruct menus. The Faces
                // list order also indexes the facial-feature table below.
                var faces = new List<(byte Value, uint Icon)>();
                if (charaMake != null)
                {
                    var row = charaMake.GetRow(rowId);
                    faces = TypedEntries(row, custom, 5);
                    Add(list, P + "FACE", IconForValue(faces, c[5]));
                    Add(list, P + "TAILEARS", IconForValue(TypedEntries(row, custom, 22), c[22]));

                    // FACIAL FEATURES 1..7 — FacialFeatureOption[faceSlot].OptionN, indexed by the
                    // player's face SLOT (its position in the Faces list); invalid face → slot 0.
                    var slot = IndexForValue(faces, c[5]);
                    if (slot < 0) slot = 0;
                    if (slot < row.FacialFeatureOption.Count)
                    {
                        var opt = row.FacialFeatureOption[slot];
                        Add(list, P + "FACIALFEATURE1", (uint)opt.Option1);
                        Add(list, P + "FACIALFEATURE2", (uint)opt.Option2);
                        Add(list, P + "FACIALFEATURE3", (uint)opt.Option3);
                        Add(list, P + "FACIALFEATURE4", (uint)opt.Option4);
                        Add(list, P + "FACIALFEATURE5", (uint)opt.Option5);
                        Add(list, P + "FACIALFEATURE6", (uint)opt.Option6);
                        Add(list, P + "FACIALFEATURE7", (uint)opt.Option7);
                    }
                }

                // HAIRSTYLE (byte 6) + FACE PAINT (byte 24 & 0x7F) — HairMakeType is a raw sheet
                // (the typed wrapper doesn't expose the count/entry columns), read via RawRow.
                var hairSheet = Plugin.DataManager.GetExcelSheet<RawRow>(ClientLanguage.English, "HairMakeType");
                if (hairSheet != null)
                {
                    var hmt = hairSheet.GetRow(rowId);
                    if (race != 7) // Hrothgar hair is per-face (HairByFace) — defer to v2
                        Add(list, P + "HAIRSTYLE", IconForValue(RawEntries(hmt, custom, 30, 66), c[6]));
                    Add(list, P + "FACEPAINT", IconForValue(RawEntries(hmt, custom, 37, 73), (byte)(c[24] & 0x7F)));
                }

                // LEGACY TATTOO — fixed icon (toggle published separately).
                Add(list, P + "LEGACYTATTOO", LegacyTattooIcon);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Icon resolve failed: {ex.Message}");
            }

            return list;
        }

        // (value, icon) entries for a typed CharaMakeStruct menu (Face, Tail/Ear).
        private static List<(byte Value, uint Icon)> TypedEntries(CharaMakeType row, ExcelSheet<CharaMakeCustomize> custom, uint byteIdx)
        {
            var entries = new List<(byte, uint)>();
            foreach (var menu in row.CharaMakeStruct)
            {
                if (menu.Customize != byteIdx) continue;
                var n = (int)menu.SubMenuNum;
                for (var i = 0; i < n && i < menu.SubMenuParam.Count; i++)
                {
                    var idx = (uint)menu.SubMenuParam[i];
                    if (idx == 0) continue;
                    if (custom.TryGetRow(idx, out var cmc))
                        entries.Add((cmc.FeatureID, cmc.Icon));
                    else
                        entries.Add(((byte)(i + 1), idx)); // unlock fallback (raw idx may not be a real icon)
                }
                break;
            }
            return entries;
        }

        // (value, icon) entries for a HairMakeType raw row: countCol holds the entry count, entries
        // start at startCol with a stride of 9 (a CharaMakeCustomize row id, or uint.MaxValue = none).
        private static List<(byte Value, uint Icon)> RawEntries(RawRow hmt, ExcelSheet<CharaMakeCustomize> custom, int countCol, int startCol)
        {
            var entries = new List<(byte, uint)>();
            int n = hmt.ReadUInt8Column(countCol);
            for (var i = 0; i < n; i++)
            {
                var idx = hmt.ReadUInt32Column(startCol + i * 9);
                if (idx == uint.MaxValue || idx == 0) continue;
                if (custom.TryGetRow(idx, out var cmc))
                    entries.Add((cmc.FeatureID, cmc.Icon)); // unlockables not in the sheet → skipped
            }
            return entries;
        }

        private static uint IconForValue(List<(byte Value, uint Icon)> entries, byte playerByte)
        {
            foreach (var e in entries)
                if (e.Value == playerByte) return e.Icon;
            return 0;
        }

        private static int IndexForValue(List<(byte Value, uint Icon)> entries, byte playerByte)
        {
            for (var i = 0; i < entries.Count; i++)
                if (entries[i].Value == playerByte) return i;
            return -1;
        }

        private static void Add(List<CharIcon> list, string optionKey, uint iconId)
        {
            if (iconId != 0) list.Add(new CharIcon(CharNaming.Icon(optionKey), iconId));
        }
    }
}
