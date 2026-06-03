using System.Text.RegularExpressions;

namespace Sightseeingway.Gear
{
    /// <summary>
    /// The texture "kinds" published per slot. Each becomes a
    /// <c>GLAM_&lt;SLOT&gt;_&lt;KIND&gt;</c> texture semantic on the Shadingway bus.
    /// </summary>
    public enum GlamTextureKind
    {
        Icon,
        Name,
        Rarity,
        Dye1,
        Dye2,
    }

    /// <summary>
    /// Builds the bus texture names and validates that they satisfy Shadingway's
    /// identifier rule (it emits the name verbatim as a ReShade semantic).
    /// </summary>
    public static class TextureNaming
    {
        public const string Prefix = "GLAM_";

        // Shadingway requires identifier-safe names: [A-Za-z_][A-Za-z0-9_]*, <= 64 chars.
        private static readonly Regex IdentifierPattern =
            new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        public static string For(GlamSlot slot, GlamTextureKind kind)
            => $"{Prefix}{slot.Key}_{Suffix(kind)}";

        public static string Suffix(GlamTextureKind kind) => kind switch
        {
            GlamTextureKind.Icon   => "ICON",
            GlamTextureKind.Name   => "NAME",
            GlamTextureKind.Rarity => "RARITY",
            GlamTextureKind.Dye1   => "DYE1",
            GlamTextureKind.Dye2   => "DYE2",
            _ => "UNKNOWN",
        };

        public static bool IsIdentifierSafe(string name)
            => !string.IsNullOrEmpty(name) && name.Length <= 64 && IdentifierPattern.IsMatch(name);
    }
}
