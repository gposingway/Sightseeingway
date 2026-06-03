using System.Collections.Generic;

namespace Sightseeingway.Gear
{
    /// <summary>
    /// Immutable snapshot of one visible equipment slot, captured on the framework
    /// thread and safe to hand to worker threads for texture building/publishing.
    /// </summary>
    public sealed record GearSlotData(
        GlamSlot Slot,
        uint VisibleItemId,
        uint IconId,
        string Name,
        byte Rarity,
        byte Stain0,
        byte Stain1,
        uint Stain0Color,
        uint Stain1Color)
    {
        /// <summary>
        /// A compact change key for this slot — what the player is visibly wearing
        /// and how it's dyed. Two reads with equal signatures need no re-publish.
        /// </summary>
        public string Signature() => $"{Slot.EquipIndex}:{VisibleItemId}:{Stain0}:{Stain1}";
    }

    /// <summary>Raw decoded pixels destined for the bus (already RGBA8 or R8, tightly packed).</summary>
    public sealed record RawTexture(string Format, int Width, int Height, byte[] Data);

    /// <summary>One texture ready to POST: its bus name plus the decoded payload.</summary>
    public sealed record PushTexture(string Name, RawTexture Texture);

    public static class GearSignature
    {
        /// <summary>Stable signature across all currently-populated slots.</summary>
        public static string Of(IReadOnlyList<GearSlotData> slots)
        {
            var parts = new string[slots.Count];
            for (var i = 0; i < slots.Count; i++) parts[i] = slots[i].Signature();
            return string.Join("|", parts);
        }
    }
}
