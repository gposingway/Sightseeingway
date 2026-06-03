using System.Collections.Generic;

namespace Sightseeingway.Gear
{
    /// <summary>
    /// One visible equipment slot on the glamour bus: a stable <see cref="Key"/>
    /// (used to build the <c>GLAM_&lt;KEY&gt;_*</c> texture semantics) paired with the
    /// index it occupies in the game's EquippedItems inventory container.
    /// </summary>
    public readonly record struct GlamSlot(int EquipIndex, string Key);

    /// <summary>
    /// The fixed set of visible equipment slots published to the glamour bus,
    /// mapping EquippedItems container indices to semantic keys. The waist slot
    /// (index 5, unused in modern FFXIV) and the soul crystal (index 13, not a
    /// visible appearance) are intentionally omitted. Facewear lives in a
    /// separate container and is deferred.
    /// </summary>
    public static class GlamSlots
    {
        public static readonly IReadOnlyList<GlamSlot> All = new[]
        {
            new GlamSlot(0,  "MAINHAND"),
            new GlamSlot(1,  "OFFHAND"),
            new GlamSlot(2,  "HEAD"),
            new GlamSlot(3,  "BODY"),
            new GlamSlot(4,  "HANDS"),
            new GlamSlot(6,  "LEGS"),
            new GlamSlot(7,  "FEET"),
            new GlamSlot(8,  "EARS"),
            new GlamSlot(9,  "NECK"),
            new GlamSlot(10, "WRISTS"),
            new GlamSlot(11, "RINGR"),
            new GlamSlot(12, "RINGL"),
        };
    }
}
