using System;
using System.Collections.Generic;

namespace Sightseeingway.CharacterCard
{
    /// <summary>
    /// Immutable snapshot of the local character's identity + appearance, captured on the
    /// framework thread and safe to hand to the publish worker. Mirrors the gear pipeline's
    /// <c>GearSlotData</c>: plain data, no Dalamud/Lumina handles.
    ///
    /// The customize appearance changes only rarely (fantasia / aesthetician / Glamourer), so
    /// the publisher re-sends the whole set on any signature change rather than per-field delta.
    /// </summary>
    public sealed record CharSnapshot(
        // ---- identity (rendered as text labels) ----
        string Name,
        string HomeWorld,
        string CurrentWorld,
        string DataCenter,
        string RaceName,
        string ClanName,
        string GenderName,
        string JobName,
        uint JobIconId,
        string GcName,
        uint GcIconId,
        int GcRank,
        string FcName,
        string FcTag,
        // ---- raw 26-byte customize array (drives the change signature) ----
        byte[] Customize,
        // ---- numeric customize options: a uniform (the value) + a "<KEY>_NUM" number label ----
        IReadOnlyList<CharNumber> Numbers,
        // ---- boolean customize options: published as 0/1 uniforms ----
        IReadOnlyList<CharFlag> Flags,
        // ---- customize colours: resolved RGB, published as [r,g,b] uniforms ----
        IReadOnlyList<CharColor> Colors)
    {
        /// <summary>
        /// Change key: the full customize array plus the identity fields that aren't in it.
        /// Two reads with equal signatures need no re-publish.
        /// </summary>
        public string Signature()
            => string.Join("|",
                Convert.ToBase64String(Customize),
                Name, HomeWorld, CurrentWorld, DataCenter,
                JobName, JobIconId.ToString(),
                GcName, GcIconId.ToString(), GcRank.ToString(),
                FcName, FcTag);
    }

    /// <summary>A numeric customize option — published as a uniform (the value) and a small
    /// white-on-transparent number text-texture (<c>&lt;Key&gt;_NUM</c>) so a shader can show it.
    /// <paramref name="Caption"/> is the live, race-correct option name from CharaMakeType when
    /// available (e.g. byte 21 is "Muscle Tone"/"Ear Length"/"Tail Length" per race); null falls
    /// back to the static caption.</summary>
    public sealed record CharNumber(string Key, int Value, string? Caption = null);

    /// <summary>A boolean customize option, published as a 0/1 uniform.</summary>
    public sealed record CharFlag(string Key, bool On);

    /// <summary>A customize colour: its palette index, the resolved RGB (0–255), and the
    /// 1-based grid cell (<c>Col</c>,<c>Row</c>) it occupies in the in-game 8-wide colour picker
    /// (so a card can show the "C1R18" the player recognises). Published as an RGB swatch texture,
    /// a "C{Col}R{Row}" position label, and a <c>[Col,Row]</c> cell uniform.</summary>
    public sealed record CharColor(string Key, byte Index, byte R, byte G, byte B, int Col, int Row);
}
