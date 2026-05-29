using System;
using System.Collections.Generic;
using Sightseeingway;
using Xunit;

namespace Sightseeingway.Tests
{
    /// <summary>
    /// Covers the "Landmark" (SubLocation) filename field and its de-dup rule:
    /// the landmark resolves to the most specific place name available and is
    /// suppressed only when the Zone (MapName) field is also active AND would
    /// emit the same text. The decision is order-independent.
    /// </summary>
    public class FilenameGeneratorSubLocationTests
    {
        private static readonly DateTime Ts = new(2026, 5, 28, 13, 45, 6, 789);

        private const string Zone = "Middle La Noscea";
        private const string Landmark = "Summerford Farms";

        private static string Generate(List<FilenameField> fields, string map, string subLocation) =>
            FilenameGenerator.GenerateFilename(
                Ts,
                TimestampFormat.Compact,
                character: "",
                map: map,
                subLocation: subLocation,
                position: "",
                eorzeaTime: "",
                weather: "",
                shaderPreset: "",
                effectsEnabled: false,
                activeFieldsInOrder: fields,
                fileExtension: ".png");

        private static List<FilenameField> Fields(params FilenameField[] f) => new(f);

        // Row 1: at a landmark, Zone ON + Landmark ON -> both, zone then landmark.
        [Fact]
        public void AtLandmark_ZoneAndLandmarkOn_ShowsBoth()
        {
            var name = Generate(
                Fields(FilenameField.Timestamp, FilenameField.MapName, FilenameField.SubLocation),
                map: Zone, subLocation: Landmark);

            Assert.EndsWith($"-{Zone}-{Landmark}.png", name);
        }

        // Row 2: open field (landmark falls back to zone), Zone ON + Landmark ON -> de-dup to one zone token.
        [Fact]
        public void OpenField_ZoneAndLandmarkOn_DeDupsToSingleZoneToken()
        {
            var name = Generate(
                Fields(FilenameField.Timestamp, FilenameField.MapName, FilenameField.SubLocation),
                map: Zone, subLocation: Zone);

            Assert.EndsWith($"-{Zone}.png", name);
            // The zone string must appear exactly once.
            Assert.Equal(1, CountOccurrences(name, Zone));
        }

        // Row 3: at a landmark, Zone OFF + Landmark ON -> landmark only, no zone.
        [Fact]
        public void AtLandmark_ZoneOff_LandmarkOnly()
        {
            var name = Generate(
                Fields(FilenameField.Timestamp, FilenameField.SubLocation),
                map: Zone, subLocation: Landmark);

            Assert.EndsWith($"-{Landmark}.png", name);
            Assert.DoesNotContain(Zone, name);
        }

        // Row 4: open field, Zone OFF + Landmark ON -> landmark resolves to zone fallback and is shown.
        [Fact]
        public void OpenField_ZoneOff_LandmarkShowsZoneFallback()
        {
            var name = Generate(
                Fields(FilenameField.Timestamp, FilenameField.SubLocation),
                map: Zone, subLocation: Zone);

            Assert.EndsWith($"-{Zone}.png", name);
            Assert.Equal(1, CountOccurrences(name, Zone));
        }

        // De-dup is order-independent: Landmark BEFORE Zone, equal values -> still one token.
        [Fact]
        public void LandmarkBeforeZone_EqualValues_DeDups()
        {
            var name = Generate(
                Fields(FilenameField.Timestamp, FilenameField.SubLocation, FilenameField.MapName),
                map: Zone, subLocation: Zone);

            Assert.EndsWith($"-{Zone}.png", name);
            Assert.Equal(1, CountOccurrences(name, Zone));
        }

        // Order is otherwise respected: Landmark BEFORE Zone, distinct values -> landmark then zone.
        [Fact]
        public void LandmarkBeforeZone_DistinctValues_RespectsOrder()
        {
            var name = Generate(
                Fields(FilenameField.Timestamp, FilenameField.SubLocation, FilenameField.MapName),
                map: Zone, subLocation: Landmark);

            Assert.EndsWith($"-{Landmark}-{Zone}.png", name);
        }

        // Empty landmark contributes nothing (no stray separator).
        [Fact]
        public void EmptyLandmark_AddsNothing()
        {
            var name = Generate(
                Fields(FilenameField.Timestamp, FilenameField.SubLocation),
                map: "", subLocation: "");

            Assert.Equal($"{Ts.ToString(Constants.Formats.CompactTimestamp)}.png", name);
        }

        // "Unknown" landmark is treated as absent (matches FormatNamePart convention).
        [Fact]
        public void UnknownLandmark_AddsNothing()
        {
            var name = Generate(
                Fields(FilenameField.Timestamp, FilenameField.SubLocation),
                map: "Unknown", subLocation: "Unknown");

            Assert.Equal($"{Ts.ToString(Constants.Formats.CompactTimestamp)}.png", name);
        }

        // "Unknown" landmark alongside a real zone: landmark contributes nothing
        // (not suppressed by de-dup since it differs from the zone, but dropped by
        // FormatNamePart), and the zone still shows exactly once with no stray dash.
        [Fact]
        public void UnknownLandmark_WithRealZone_ShowsZoneOnly()
        {
            var name = Generate(
                Fields(FilenameField.Timestamp, FilenameField.MapName, FilenameField.SubLocation),
                map: Zone, subLocation: "Unknown");

            Assert.EndsWith($"-{Zone}.png", name);
            Assert.Equal(1, CountOccurrences(name, Zone));
            Assert.DoesNotContain("Unknown", name);
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            var count = 0;
            var idx = 0;
            while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) != -1)
            {
                count++;
                idx += needle.Length;
            }
            return count;
        }
    }
}
