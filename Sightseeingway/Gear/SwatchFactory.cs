namespace Sightseeingway.Gear
{
    /// <summary>
    /// Pure helpers for the small solid-colour swatch textures (dye channels,
    /// name rarity colour) and the BGR→RGB conversion the game's packed colours
    /// require. Output buffers are tightly-packed RGBA8, row-major — exactly the
    /// shape Shadingway's <c>/api/v1/textures</c> expects.
    /// </summary>
    public static class SwatchFactory
    {
        public const int SwatchSize = 8;

        /// <summary>
        /// Square stores stain/colour values as BGR (low byte = blue, high byte =
        /// red), so a naive read swaps red and blue. Returns the true (r, g, b).
        /// </summary>
        public static (byte R, byte G, byte B) SeColorToRgb(uint seColor)
        {
            var b = (byte)(seColor & 0xFF);
            var g = (byte)((seColor >> 8) & 0xFF);
            var r = (byte)((seColor >> 16) & 0xFF);
            return (r, g, b);
        }

        /// <summary>Builds a tightly-packed RGBA8 buffer filled with a single colour.</summary>
        public static byte[] SolidRgba(int width, int height, byte r, byte g, byte b, byte a = 255)
        {
            var buffer = new byte[width * height * 4];
            for (var i = 0; i < buffer.Length; i += 4)
            {
                buffer[i] = r;
                buffer[i + 1] = g;
                buffer[i + 2] = b;
                buffer[i + 3] = a;
            }
            return buffer;
        }

        /// <summary>An 8×8 RGBA8 swatch of the given SE-packed colour (BGR-corrected).</summary>
        public static byte[] StainSwatch(uint seColor)
        {
            var (r, g, b) = SeColorToRgb(seColor);
            return SolidRgba(SwatchSize, SwatchSize, r, g, b);
        }

        /// <summary>An 8×8 fully-transparent swatch (used for an undyed / absent dye channel).</summary>
        public static byte[] TransparentSwatch()
            => SolidRgba(SwatchSize, SwatchSize, 0, 0, 0, 0);

        /// <summary>
        /// A dye swatch: the BGR-corrected colour when dyed, or a transparent swatch
        /// when the channel has no colour (undyed, or the item has no such dye slot).
        /// </summary>
        public static byte[] DyeSwatch(uint seColor)
            => seColor == 0 ? TransparentSwatch() : StainSwatch(seColor);

        /// <summary>An 8×8 RGBA8 swatch of an item's name colour, from its rarity.</summary>
        public static byte[] RaritySwatch(byte rarity)
        {
            var (r, g, b) = RarityColor(rarity);
            return SolidRgba(SwatchSize, SwatchSize, r, g, b);
        }

        /// <summary>
        /// FFXIV item-name colour keyed on the Item sheet's Rarity value. Values are
        /// an approximation of the in-game tooltip palette and easy to tune; unknown
        /// rarities fall back to white.
        /// </summary>
        public static (byte R, byte G, byte B) RarityColor(byte rarity) => rarity switch
        {
            1 => (0xEE, 0xEE, 0xEE), // common — white
            2 => (0x59, 0xD0, 0x4C), // aetherial / uncommon — green
            3 => (0x52, 0x9E, 0xE0), // unique / rare — blue
            4 => (0xC0, 0x7E, 0xF0), // relic — purple
            7 => (0xF0, 0x8C, 0xD0), // premium / aetherial — pink
            _ => (0xEE, 0xEE, 0xEE),
        };
    }
}
