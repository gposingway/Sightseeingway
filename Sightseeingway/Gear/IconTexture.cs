using System;
using System.Threading.Tasks;
using Dalamud.Interface.Textures;

namespace Sightseeingway.Gear
{
    /// <summary>
    /// Loads a game item icon and reads it back to CPU pixels. Uses the icon at its
    /// native size (80×80 vanilla hi-res, or larger if the player has an icon mod —
    /// the size is taken from the readback, never assumed). Honors Penumbra texture
    /// substitution via <c>GetFromGameIcon</c>.
    /// </summary>
    public static class IconTexture
    {
        public static async Task<RawTexture?> ReadAsync(uint iconId)
        {
            if (iconId == 0) return null;

            try
            {
                // GameIconLookup defaults to hi-res; this is the verified Glamourer path.
                var shared = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId));
                using var wrap = await shared.RentAsync();

                var (spec, bytes) = await Plugin.TextureReadback.GetRawImageAsync(wrap);
                var tight = Pack(bytes, spec.Width, spec.Height);
                return new RawTexture("rgba8", spec.Width, spec.Height, tight);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Icon readback failed for icon {iconId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Strips any per-row padding so the payload is tightly packed
        /// width*height*4 (what Shadingway requires). Channel order is taken as-is
        /// from the readback per the Shadingway producer guide; if an in-game test
        /// shows red/blue swapped, this is the single place to add the swap.
        /// </summary>
        private static byte[] Pack(byte[] src, int width, int height)
        {
            if (height <= 0 || width <= 0) return src;

            var tight = width * 4;
            var stride = src.Length / height;
            if (stride == tight) return src;

            var dst = new byte[tight * height];
            for (var y = 0; y < height; y++)
                Array.Copy(src, y * stride, dst, y * tight, tight);
            return dst;
        }
    }
}
