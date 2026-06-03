using System;
using System.Linq;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Sightseeingway.Gear
{
    /// <summary>
    /// Renders an item name to a single-channel (r8) coverage texture: white text
    /// on a transparent field, so a shader can tint it (e.g. with the rarity swatch)
    /// and the preset controls the colour. Pure-managed via ImageSharp + Fonts.
    /// </summary>
    public static class NameTexture
    {
        private const int Width = 256;
        private const int Height = 28;
        private const float FontSize = 16f;

        private static Font? _font;
        private static bool _fontResolveFailed;

        public static RawTexture? Render(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            try
            {
                var font = ResolveFont();
                if (font == null) return null;

                using var img = new Image<L8>(Width, Height); // all-zero = empty coverage
                img.Mutate(ctx => ctx.DrawText(text, font, Color.White, new PointF(2f, 4f)));

                var bytes = new byte[Width * Height]; // L8 = 1 byte/pixel = r8
                img.CopyPixelDataTo(bytes);
                return new RawTexture("r8", Width, Height, bytes);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Name render failed: {ex.Message}");
                return null;
            }
        }

        private static Font? ResolveFont()
        {
            if (_font != null) return _font;
            if (_fontResolveFailed) return null;

            try
            {
                FontFamily family;
                if (SystemFonts.TryGet("Arial", out var arial)) family = arial;
                else if (SystemFonts.TryGet("Segoe UI", out var segoe)) family = segoe;
                else
                {
                    var families = SystemFonts.Families.ToList();
                    if (families.Count == 0)
                    {
                        _fontResolveFailed = true;
                        return null;
                    }
                    family = families[0];
                }

                _font = family.CreateFont(FontSize, FontStyle.Regular);
                return _font;
            }
            catch (Exception ex)
            {
                _fontResolveFailed = true;
                Plugin.Logger?.Debug($"Font resolution failed: {ex.Message}");
                return null;
            }
        }
    }
}
