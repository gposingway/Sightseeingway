using System.Collections.Generic;

namespace Sightseeingway.CharacterCard
{
    /// <summary>
    /// Static option-name captions for the CHAR_* fields ("Face", "Race", …). A shader can't
    /// render arbitrary text, so the producer publishes these as <c>&lt;KEY&gt;_LABEL</c> text
    /// textures alongside each value, letting a card compose "caption: value". They never change
    /// per character (only with the client language), so they're published once and stay resident.
    ///
    /// English for now; the localized names live in the game's CharaMakeType → Lobby sheets
    /// (see Glamourer's CustomizeSetFactory.GetOptionNames) and can be swapped in here later
    /// without touching the publisher.
    /// </summary>
    public static class CharCaptions
    {
        private const string P = CharNaming.Prefix;

        public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
        {
            // identity
            [CharNaming.World]        = "Home World",
            [CharNaming.CurrentWorld] = "World",
            [CharNaming.DataCenter]   = "Data Center",
            [CharNaming.Race]         = "Race",
            [CharNaming.Clan]         = "Clan",
            [CharNaming.Gender]       = "Gender",
            [CharNaming.Job]          = "Job",
            [CharNaming.GcName]       = "Grand Company",

            // numeric options
            [P + "FACE"]      = "Face",
            [P + "HAIRSTYLE"] = "Hairstyle",
            [P + "EYEBROWS"]  = "Eyebrows",
            [P + "NOSE"]      = "Nose",
            [P + "JAW"]       = "Jaw",
            [P + "MOUTH"]     = "Mouth",
            [P + "EYESHAPE"]  = "Eye Shape",
            [P + "TAIL"]      = "Tail / Ears",
            [P + "BUST"]      = "Bust",
            [P + "MUSCLE"]    = "Muscle Tone",
            [P + "HEIGHT"]    = "Height",
            [P + "FACEPAINT"] = "Face Paint",
            [P + "GC_RANK"]   = "GC Rank",

            // toggles
            [P + "HIGHLIGHTS"]        = "Highlights",
            [P + "LIPSTICK"]          = "Lipstick",
            [P + "SMALLIRIS"]         = "Small Iris",
            [P + "FACEPAINTREVERSED"] = "Face Paint Reversed",
            [P + "LEGACYTATTOO"]      = "Legacy Tattoo",
            [P + "FACIALFEATURE1"]    = "Facial Feature 1",
            [P + "FACIALFEATURE2"]    = "Facial Feature 2",
            [P + "FACIALFEATURE3"]    = "Facial Feature 3",
            [P + "FACIALFEATURE4"]    = "Facial Feature 4",
            [P + "FACIALFEATURE5"]    = "Facial Feature 5",
            [P + "FACIALFEATURE6"]    = "Facial Feature 6",
            [P + "FACIALFEATURE7"]    = "Facial Feature 7",
        };

        /// <summary>The caption text for a field's bus key, or null if none.</summary>
        public static string? For(string key) => Names.TryGetValue(key, out var c) ? c : null;

        /// <summary>The caption texture name for a field's bus key (CHAR_FACE → CHAR_FACE_LABEL).</summary>
        public static string LabelName(string key) => $"{key}_LABEL";
    }
}
