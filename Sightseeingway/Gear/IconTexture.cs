using System;
using Dalamud.Interface.Textures;
using Lumina.Data.Files;

namespace Sightseeingway.Gear
{
    /// <summary>
    /// Loads an item icon's pixels straight from the game's .tex file via Lumina —
    /// synchronous and thread-safe, with no GPU texture-readback (which yields nothing
    /// off the render path). Returns tightly-packed RGBA8 at the icon's native size.
    /// Note: this reads the vanilla game asset, so Penumbra icon mods are not reflected.
    /// </summary>
    public static class IconTexture
    {
        public static RawTexture? Read(uint iconId)
        {
            if (iconId == 0) return null;

            try
            {
                if (!Plugin.TextureProvider.TryGetIconPath(new GameIconLookup(iconId), out var path)
                    || string.IsNullOrEmpty(path))
                    return null;

                var tex = Plugin.DataManager.GetFile<TexFile>(path);
                if (tex == null) return null;

                int w = tex.Header.Width;
                int h = tex.Header.Height;
                var bgra = tex.ImageData; // Lumina decodes every tex format to B8G8R8A8
                if (w <= 0 || h <= 0 || bgra.Length < w * h * 4) return null;

                // BGRA → RGBA (Shadingway expects rgba8; the swatches confirm RGBA byte order).
                var rgba = new byte[w * h * 4];
                for (var i = 0; i < rgba.Length; i += 4)
                {
                    rgba[i]     = bgra[i + 2];
                    rgba[i + 1] = bgra[i + 1];
                    rgba[i + 2] = bgra[i];
                    rgba[i + 3] = bgra[i + 3];
                }

                Plugin.Logger?.Debug($"Icon {iconId} ({path}): {w}x{h}, {rgba.Length}B");
                return new RawTexture("rgba8", w, h, rgba);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Icon {iconId} read failed: {ex.Message}");
                return null;
            }
        }
    }
}
