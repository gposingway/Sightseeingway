using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Sightseeingway.Gear
{
    /// <summary>
    /// Per-slot textures with a single fixed name. The name label is not here — it
    /// has font/height variants (see <see cref="TextureNaming.Name"/>).
    /// </summary>
    public enum GlamTextureKind
    {
        Icon,
        Rarity,
        Dye1,
        Dye2,
        Dye1Name,
        Dye2Name,
        Category,
        Tags,
        Levels,
    }

    /// <summary>
    /// Builds the bus texture names and validates that they satisfy Shadingway's
    /// identifier rule (it emits the name verbatim as a ReShade semantic).
    /// </summary>
    public static class TextureNaming
    {
        public const string Prefix = "GLAM_";

        /// <summary>
        /// Fonts for name-label variants, in index order — the index is the bus-name
        /// token (NAME0..3). 0 = Inter (sans), 1 = Cinzel (epic caps), 2 = Tangerine
        /// (script), 3 = Cormorant (glamour serif). Must match <see cref="GlamFonts"/>.
        /// </summary>
        public static readonly string[] NameFontKeys = { "INTER", "CINZEL", "TANGERINE", "CORMORANT" };

        /// <summary>Render height (px) for item-name labels (supersampled by the shader).</summary>
        public const int NameHeight = 128;

        /// <summary>Render height (px) for the smaller dye-name labels.</summary>
        public const int DyeNameHeight = 28;

        // Shadingway requires identifier-safe names: [A-Za-z_][A-Za-z0-9_]*, <= 64 chars.
        private static readonly Regex IdentifierPattern =
            new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        public static string For(GlamSlot slot, GlamTextureKind kind)
            => $"{Prefix}{slot.Key}_{Suffix(kind)}";

        public static string Suffix(GlamTextureKind kind) => kind switch
        {
            GlamTextureKind.Icon     => "ICON",
            GlamTextureKind.Rarity   => "RARITY",
            GlamTextureKind.Dye1     => "DYE1",
            GlamTextureKind.Dye2     => "DYE2",
            GlamTextureKind.Dye1Name => "DYE1NAME",
            GlamTextureKind.Dye2Name => "DYE2NAME",
            GlamTextureKind.Category => "CATEGORY",
            GlamTextureKind.Tags     => "TAGS",
            GlamTextureKind.Levels   => "LEVELS",
            _ => "UNKNOWN",
        };

        /// <summary>
        /// A name-label texture: <c>GLAM_&lt;SLOT&gt;_NAME&lt;index&gt;</c>. The index selects
        /// the bundled font (see <see cref="NameFontKeys"/>).
        /// </summary>
        public static string Name(GlamSlot slot, int fontIndex)
            => $"{Prefix}{slot.Key}_NAME{fontIndex}";

        /// <summary>Every texture name a slot can publish — used for precise stale-name cleanup.</summary>
        public static IEnumerable<string> AllFor(GlamSlot slot)
        {
            yield return For(slot, GlamTextureKind.Icon);
            yield return For(slot, GlamTextureKind.Rarity);
            yield return For(slot, GlamTextureKind.Dye1);
            yield return For(slot, GlamTextureKind.Dye2);
            yield return For(slot, GlamTextureKind.Dye1Name);
            yield return For(slot, GlamTextureKind.Dye2Name);
            yield return For(slot, GlamTextureKind.Category);
            yield return For(slot, GlamTextureKind.Tags);
            yield return For(slot, GlamTextureKind.Levels);
            for (var i = 0; i < NameFontKeys.Length; i++)
                yield return Name(slot, i);
        }

        public static bool IsIdentifierSafe(string name)
            => !string.IsNullOrEmpty(name) && name.Length <= 64 && IdentifierPattern.IsMatch(name);
    }
}
