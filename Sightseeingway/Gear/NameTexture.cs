using System;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Sightseeingway.Gear
{
    /// <summary>
    /// Renders an item name to a white-on-transparent RGBA8 texture, sized to fit the
    /// text at a target pixel height: RGB is white everywhere and alpha is the glyph
    /// coverage, so a shader can show it as-is, tint it, or invert it. Pure-managed
    /// via ImageSharp + Fonts.
    /// </summary>
    public static class NameTexture
    {
        private const int MaxWidth = 4096;

        public static RawTexture? Render(string text, FontFamily family, int heightPx)
        {
            if (string.IsNullOrWhiteSpace(text) || heightPx <= 0) return null;

            try
            {
                var fontSize = heightPx * 0.74f; // leave room for ascenders/descenders within the strip
                var font = family.CreateFont(fontSize, FontStyle.Regular);
                var pad = Math.Max(2, heightPx / 12);

                var measure = TextMeasurer.MeasureSize(text, new TextOptions(font));
                var w = Math.Min(MaxWidth, (int)Math.Ceiling(measure.Width) + pad * 2);
                var h = heightPx;
                if (w <= 0) return null;

                using var img = new Image<L8>(w, h); // 0 = empty coverage
                var y = (h - measure.Height) / 2f;   // vertical centre
                img.Mutate(ctx => ctx.DrawText(text, font, Color.White, new PointF(pad, y)));

                var coverage = new byte[w * h];
                img.CopyPixelDataTo(coverage);

                // Expand to white-on-transparent RGBA8: RGB white everywhere, alpha = coverage.
                var rgba = new byte[w * h * 4];
                for (var i = 0; i < coverage.Length; i++)
                {
                    var o = i * 4;
                    rgba[o] = 255;
                    rgba[o + 1] = 255;
                    rgba[o + 2] = 255;
                    rgba[o + 3] = coverage[i];
                }

                return new RawTexture("rgba8", w, h, rgba);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.Debug($"Name render failed: {ex.Message}");
                return null;
            }
        }
    }
}
