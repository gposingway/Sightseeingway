using System;
using System.Collections.Generic;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
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
                var fcTag = Safe(() => player.CompanyTag.TextValue);
                var fcName = ReadFreeCompanyName();

                // ---- customize options, gated by per-race/gender availability ----
                // CharaMakeType tells us which options this race/gender actually has, the slider
                // max, and the live localized caption — so a male's Bust / an Elezen's muscle slot
                // never publish, and byte 21 is captioned correctly per race (Muscle Tone /
                // Ear Length / Tail Length) without special-casing.
                const string P = CharNaming.Prefix;
                var avail = CharAvailability.For(c.Tribe, c.Sex);
                var isHrothgar = c.Race == 7;

                bool Has(int b) => avail.TryGetValue(b, out var info) && info.Available;
                string? Caption(int b) => avail.TryGetValue(b, out var info) && !string.IsNullOrEmpty(info.Caption) ? info.Caption : null;

                var numbers = new List<CharNumber>();
                void Num(string key, int value, int b, bool always = false, string? fallback = null)
                {
                    if (always || Has(b)) numbers.Add(new CharNumber(key, value, Caption(b) ?? fallback));
                }

                Num(P + "FACE",       c.Face,       5);
                Num(P + "HAIRSTYLE",  c.Hairstyle,  6,  always: true);
                Num(P + "EYEBROWS",   c.Eyebrows,   14);
                Num(P + "EYESHAPE",   c.EyeShape,   16);
                Num(P + "NOSE",       c.Nose,       17);
                Num(P + "JAW",        c.Jaw,        18);
                Num(P + "MOUTH",      c.Mouth,      19);
                // byte 21/22 are relabelled per race — pass a per-race fallback so the caption is
                // never the generic combined form even if the live CharaMakeType read comes back empty.
                Num(P + "BODYSLIDER", c.MuscleMass, 21, always: true, fallback: BodySliderName(c.Race));
                Num(P + "TAILEARS",   c.TailShape,  22, fallback: TailEarName(c.Race));
                Num(P + "BUST",       c.BustSize,   23);               // female only
                Num(P + "FACEPAINT",  c.FacePaint,  24);
                Num(P + "HEIGHT",     c.Height,     3,  always: true);
                numbers.Add(new CharNumber(P + "GC_RANK", gcRank));    // not a customize option

                // ---- boolean customize options (0/1 uniforms) ----
                var flags = new List<CharFlag>
                {
                    new(P + "HIGHLIGHTS",     c.Highlights),
                    new(P + "SMALLIRIS",      c.SmallIris),
                    new(P + "LEGACYTATTOO",   c.LegacyTattoo),
                    new(P + "FACIALFEATURE1", c.FacialFeature1),
                    new(P + "FACIALFEATURE2", c.FacialFeature2),
                    new(P + "FACIALFEATURE3", c.FacialFeature3),
                    new(P + "FACIALFEATURE4", c.FacialFeature4),
                    new(P + "FACIALFEATURE5", c.FacialFeature5),
                    new(P + "FACIALFEATURE6", c.FacialFeature6),
                    new(P + "FACIALFEATURE7", c.FacialFeature7),
                };
                if (!isHrothgar) flags.Add(new CharFlag(P + "LIPSTICK", c.Lipstick));          // Hrothgar: no lipstick
                if (Has(24)) flags.Add(new CharFlag(P + "FACEPAINTREVERSED", c.FacePaintReversed));

                // Customize colours from the human.cmp palette (resolved on the framework thread;
                // lip colour skipped for Hrothgar, whose slot is a fur pattern, not a colour).
                var colors = CharColors.Resolve(raw, isHrothgar);

                // Creator thumbnails per option (hairstyle/face-paint/face/tail-ear/facial-features)
                // + any resolved names (an unlockable style's unlock-item name); resolved here on
                // the framework thread, rendered to textures by the worker.
                var iconResult = CharIcons.Resolve(raw);

                return new CharSnapshot(
                    name, homeWorld, currentWorld, dataCenter,
                    raceName, clanName, genderName,
                    jobName, JobIconId: 0u,
                    gcName, GcIconId: 0u, gcRank,
                    fcName, fcTag,
                    raw, numbers, flags, colors, iconResult.Icons, iconResult.Labels);
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
            string.Empty, string.Empty,
            new byte[26],
            Array.Empty<CharNumber>(), Array.Empty<CharFlag>(), Array.Empty<CharColor>(),
            Array.Empty<CharIcon>(), Array.Empty<CharLabel>());

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

        // The full Free Company name from the FC info proxy (populated for FC members). The short
        // tag comes from the player's CompanyTag; the proxy carries the full name.
        private static unsafe string ReadFreeCompanyName()
        {
            try
            {
                var fc = InfoProxyFreeCompany.Instance();
                return fc != null ? fc->NameString ?? string.Empty : string.Empty;
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"FC name read failed: {ex.Message}");
                return string.Empty;
            }
        }

        // Per-race fallback captions for the two relabelled body sliders — used only when the live
        // CharaMakeType caption is unavailable, so the label is still per-character (never the
        // generic combined "Tail / Ears"). Race ids (customize byte 0, = Race sheet RowId):
        // 1 Hyur · 2 Elezen · 3 Lalafell · 4 Miqo'te · 5 Roegadyn · 6 Au Ra · 7 Hrothgar · 8 Viera.
        // Groups: muscular Hyur/Roegadyn · tailed Miqo'te/Au Ra/Hrothgar · eared Elezen/Lalafell/Viera.
        private static string BodySliderName(byte race) => race switch  // byte 21
        {
            1 or 5      => "Muscle Tone",
            4 or 6 or 7 => "Tail Length",
            2 or 3 or 8 => "Ear Length",
            _           => "Body",
        };

        private static string TailEarName(byte race) => race switch     // byte 22 (eared/tailed only)
        {
            4 or 6 or 7 => "Tail Shape",
            2 or 3 or 8 => "Ear Shape",
            _           => "Tail / Ears",
        };

        private static string Safe(Func<string> read)
        {
            try { return read() ?? string.Empty; }
            catch { return string.Empty; }
        }
    }
}
