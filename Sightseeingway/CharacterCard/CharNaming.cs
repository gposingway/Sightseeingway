using Sightseeingway.Gear;

namespace Sightseeingway.CharacterCard
{
    /// <summary>
    /// Builds the <c>CHAR_*</c> bus names (texture semantics + uniform keys) for the character
    /// provider. The character is one logical entity, so this is a flat set of names (no per-slot
    /// loop). The identifier rule and the bundled label fonts are shared with the gear provider.
    /// </summary>
    public static class CharNaming
    {
        public const string Prefix = "CHAR_";

        // ---- identity text labels ----
        public const string World        = Prefix + "WORLD";
        public const string CurrentWorld = Prefix + "CURRENTWORLD";
        public const string DataCenter   = Prefix + "DATACENTER";
        public const string Race         = Prefix + "RACE";
        public const string Clan         = Prefix + "CLAN";
        public const string Gender       = Prefix + "GENDER";
        public const string Job          = Prefix + "JOB";
        public const string GcName       = Prefix + "GC_NAME";

        // ---- icons (standard game icons via IconTexture) ----
        public const string JobIcon = Prefix + "JOB_ICON";
        public const string GcIcon  = Prefix + "GC_ICON";

        /// <summary>The character name rendered in bundled font <paramref name="fontIndex"/>
        /// (0..3 — see <see cref="TextureNaming.NameFontKeys"/>): CHAR_NAME0..3.</summary>
        public static string Name(int fontIndex) => $"{Prefix}NAME{fontIndex}";

        /// <summary>The number text-texture name for a numeric uniform key
        /// (e.g. <c>CHAR_FACE</c> → <c>CHAR_FACE_NUM</c>).</summary>
        public static string NumberLabel(string numberKey) => $"{numberKey}_NUM";

        /// <summary>Shadingway requires identifier-safe names; reuse the gear validator.</summary>
        public static bool IsIdentifierSafe(string name) => TextureNaming.IsIdentifierSafe(name);
    }
}
