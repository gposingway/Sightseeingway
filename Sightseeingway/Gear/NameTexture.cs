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
    /// Renders an item name to a white-on-transparent RGBA8 texture: RGB is white
    /// everywhere and alpha is the glyph coverage, so a shader is free to show it
    /// as-is, tint it (e.g. with the rarity swatch), or invert it — the preset
    /// decides. Pure-managed via ImageSharp + Fonts.
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

                var coverage = new byte[Width * Height]; // L8 = 1 byte/pixel = glyph coverage
                img.CopyPixelDataTo(coverage);

                // Expand to white-on-transparent RGBA8: RGB white everywhere, alpha = coverage.
                var rgba = new byte[Width * Height * 4];
                for (var i = 0; i < coverage.Length; i++)
                {
                    var o = i * 4;
                    rgba[o] = 255;
                    rgba[o + 1] = 255;
                    rgba[o + 2] = 255;
                    rgba[o + 3] = coverage[i];
                }

                return new RawTexture("rgba8", Width, Height, rgba);
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
