using System;
using System.Collections.Generic;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace Sightseeingway.CharacterCard
{
    /// <summary>
    /// Reads the local character's identity + appearance into an immutable <see cref="CharSnapshot"/>.
    /// Framework-thread only (touches Dalamud services + the rendered actor), mirroring
    /// <c>GearReader</c>: <c>null</c> = unreliable read → hold the last published state; an empty
    /// snapshot (blank name) = logged out → clear the bus.
    /// </summary>
    public static class CharReader
    {
        public static unsafe CharSnapshot? ReadSnapshot()
        {
            try
            {
                // Logged out (title / character select) → clear the bus deterministically.
                if (!Plugin.ClientState.IsLoggedIn) return Empty();

                var player = Plugin.ObjectTable.LocalPlayer;
                if (player == null) return null;                 // transient → hold
                var chara = (Character*)player.Address;
                if (chara == null) return null;                  // transient → hold

                ref var c = ref chara->DrawData.CustomizeData;
                var raw = new byte[26];
                for (var i = 0; i < raw.Length; i++) raw[i] = c[i];

                // ---- identity ----
                var name = player.Name.TextValue ?? string.Empty;
                if (string.IsNullOrEmpty(name)) return null;      // not ready → hold

                var homeWorld    = Safe(() => player.HomeWorld.Value.Name.ExtractText());
                var currentWorld = Safe(() => player.CurrentWorld.Value.Name.ExtractText());
                var dataCenter   = Safe(() => player.HomeWorld.Value.DataCenter.Value.Name.ExtractText());
                var jobName      = Safe(() => player.ClassJob.Value.Name.ExtractText());

                var female = c.Sex != 0;
                var (raceName, clanName) = ResolveRaceClan(c.Race, c.Tribe, female);
                var genderName = female ? "Female" : "Male";

                var (gcName, gcRank) = ReadGrandCompany();

                // ---- numeric customize options (uniform + number text-texture) ----
                var numbers = new List<CharNumber>
                {
                    new(CharNaming.Prefix + "FACE",      c.Face),
                    new(CharNaming.Prefix + "HAIRSTYLE", c.Hairstyle),
                    new(CharNaming.Prefix + "EYEBROWS",  c.Eyebrows),
                    new(CharNaming.Prefix + "NOSE",      c.Nose),
                    new(CharNaming.Prefix + "JAW",       c.Jaw),
                    new(CharNaming.Prefix + "MOUTH",     c.Mouth),
                    new(CharNaming.Prefix + "EYESHAPE",  c.EyeShape),
                    new(CharNaming.Prefix + "TAIL",      c.TailShape),
                    new(CharNaming.Prefix + "BUST",      c.BustSize),
                    new(CharNaming.Prefix + "MUSCLE",    c.MuscleMass),
                    new(CharNaming.Prefix + "HEIGHT",    c.Height),
                    new(CharNaming.Prefix + "FACEPAINT", c.FacePaint),
                    new(CharNaming.Prefix + "GC_RANK",   gcRank),
                };

                // ---- boolean customize options (0/1 uniforms) ----
                var flags = new List<CharFlag>
                {
                    new(CharNaming.Prefix + "HIGHLIGHTS",        c.Highlights),
                    new(CharNaming.Prefix + "LIPSTICK",          c.Lipstick),
                    new(CharNaming.Prefix + "SMALLIRIS",         c.SmallIris),
                    new(CharNaming.Prefix + "FACEPAINTREVERSED", c.FacePaintReversed),
                    new(CharNaming.Prefix + "LEGACYTATTOO",      c.LegacyTattoo),
                    new(CharNaming.Prefix + "FACIALFEATURE1",    c.FacialFeature1),
                    new(CharNaming.Prefix + "FACIALFEATURE2",    c.FacialFeature2),
                    new(CharNaming.Prefix + "FACIALFEATURE3",    c.FacialFeature3),
                    new(CharNaming.Prefix + "FACIALFEATURE4",    c.FacialFeature4),
                    new(CharNaming.Prefix + "FACIALFEATURE5",    c.FacialFeature5),
                    new(CharNaming.Prefix + "FACIALFEATURE6",    c.FacialFeature6),
                    new(CharNaming.Prefix + "FACIALFEATURE7",    c.FacialFeature7),
                };

                // Colours (skin/hair/eyes/lip/…) come in a follow-up once the human.cmp byte-order
                // is confirmed in-game; until then publish an empty colour set (graceful no-op).
                var colors = Array.Empty<CharColor>();

                return new CharSnapshot(
                    name, homeWorld, currentWorld, dataCenter,
                    raceName, clanName, genderName,
                    jobName, JobIconId: 0u,
                    gcName, GcIconId: 0u, gcRank,
                    raw, numbers, flags, colors);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Char read failed: {ex.Message}");
                return null; // unreliable → hold
            }
        }

        /// <summary>The "logged out / nothing to show" snapshot — its blank name tells the
        /// publisher to clear the bus (distinct from a null = hold).</summary>
        public static CharSnapshot Empty() => new(
            string.Empty, string.Empty, string.Empty, string.Empty,
            string.Empty, string.Empty, string.Empty,
            string.Empty, 0u, string.Empty, 0u, 0,
            new byte[26],
            Array.Empty<CharNumber>(), Array.Empty<CharFlag>(), Array.Empty<CharColor>());

        private static (string Race, string Clan) ResolveRaceClan(byte race, byte tribe, bool female)
        {
            var raceName = string.Empty;
            var clanName = string.Empty;
            try
            {
                var raceSheet = Plugin.DataManager.GetExcelSheet<Race>();
                if (raceSheet != null)
                {
                    var row = raceSheet.GetRow(race);
                    if (row.RowId != 0)
                        raceName = (female ? row.Feminine : row.Masculine).ExtractText();
                }

                var tribeSheet = Plugin.DataManager.GetExcelSheet<Tribe>();
                if (tribeSheet != null)
                {
                    var row = tribeSheet.GetRow(tribe);
                    if (row.RowId != 0)
                        clanName = (female ? row.Feminine : row.Masculine).ExtractText();
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Race/clan resolve failed: {ex.Message}");
            }

            return (raceName, clanName);
        }

        private static unsafe (string Name, int Rank) ReadGrandCompany()
        {
            try
            {
                var state = PlayerState.Instance();
                if (state == null) return (string.Empty, 0);

                int gcId = state->GrandCompany;
                if (gcId <= 0) return (string.Empty, 0);

                int rank = state->GetGrandCompanyRank();

                var sheet = Plugin.DataManager.GetExcelSheet<GrandCompany>();
                var gcName = string.Empty;
                if (sheet != null)
                {
                    var row = sheet.GetRow((uint)gcId);
                    if (row.RowId != 0) gcName = row.Name.ExtractText();
                }

                return (gcName, rank);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Grand Company read failed: {ex.Message}");
                return (string.Empty, 0);
            }
        }

        private static string Safe(Func<string> read)
        {
            try { return read() ?? string.Empty; }
            catch { return string.Empty; }
        }
    }
}
