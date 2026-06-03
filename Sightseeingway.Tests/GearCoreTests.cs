using System.Linq;
using Sightseeingway.Gear;
using Xunit;

namespace Sightseeingway.Tests
{
    public class GearCoreTests
    {
        // ---- Slot registry ----

        [Fact]
        public void Slots_AreTwelveVisibleSlots_WithUniqueKeysAndIndices()
        {
            Assert.Equal(12, GlamSlots.All.Count);
            Assert.Equal(12, GlamSlots.All.Select(s => s.Key).Distinct().Count());
            Assert.Equal(12, GlamSlots.All.Select(s => s.EquipIndex).Distinct().Count());
        }

        [Fact]
        public void Slots_OmitWaistAndSoulCrystalIndices()
        {
            var indices = GlamSlots.All.Select(s => s.EquipIndex).ToHashSet();
            Assert.DoesNotContain(5, indices);   // waist (unused)
            Assert.DoesNotContain(13, indices);  // soul crystal (not visible)
        }

        // ---- Texture naming ----

        [Theory]
        [InlineData("HEAD", GlamTextureKind.Icon, "GLAM_HEAD_ICON")]
        [InlineData("BODY", GlamTextureKind.Name, "GLAM_BODY_NAME")]
        [InlineData("RINGR", GlamTextureKind.Rarity, "GLAM_RINGR_RARITY")]
        [InlineData("MAINHAND", GlamTextureKind.Dye1, "GLAM_MAINHAND_DYE1")]
        [InlineData("OFFHAND", GlamTextureKind.Dye2, "GLAM_OFFHAND_DYE2")]
        public void TextureNaming_BuildsExpectedSemantic(string key, GlamTextureKind kind, string expected)
        {
            var slot = new GlamSlot(0, key);
            Assert.Equal(expected, TextureNaming.For(slot, kind));
        }

        [Fact]
        public void TextureNaming_AllGeneratedNamesAreIdentifierSafe()
        {
            foreach (var slot in GlamSlots.All)
            {
                foreach (GlamTextureKind kind in System.Enum.GetValues<GlamTextureKind>())
                {
                    var name = TextureNaming.For(slot, kind);
                    Assert.True(TextureNaming.IsIdentifierSafe(name), $"unsafe: {name}");
                    Assert.True(name.Length <= 64);
                }
            }
        }

        [Theory]
        [InlineData("GLAM_HEAD_ICON", true)]
        [InlineData("", false)]
        [InlineData("9LEADING_DIGIT", false)]
        [InlineData("has space", false)]
        [InlineData("has-dash", false)]
        public void TextureNaming_IdentifierValidation(string name, bool expected)
            => Assert.Equal(expected, TextureNaming.IsIdentifierSafe(name));

        // ---- Swatch / colour math ----

        [Theory]
        [InlineData(0x0000FFu, 0, 0, 255)]   // SE stores BGR: low byte=blue → stays blue
        [InlineData(0xFF0000u, 255, 0, 0)]   // high byte=red → stays red
        [InlineData(0x00FF00u, 0, 255, 0)]   // mid byte=green
        [InlineData(0xE4E4F0u, 0xE4, 0xE4, 0xF0)]
        public void SeColorToRgb_SwapsBlueAndRed(uint seColor, int r, int g, int b)
        {
            var (rr, gg, bb) = SwatchFactory.SeColorToRgb(seColor);
            Assert.Equal((byte)r, rr);
            Assert.Equal((byte)g, gg);
            Assert.Equal((byte)b, bb);
        }

        [Fact]
        public void SolidRgba_FillsEveryPixelTightlyPacked()
        {
            var buf = SwatchFactory.SolidRgba(8, 8, 10, 20, 30);
            Assert.Equal(8 * 8 * 4, buf.Length);
            for (var i = 0; i < buf.Length; i += 4)
            {
                Assert.Equal(10, buf[i]);
                Assert.Equal(20, buf[i + 1]);
                Assert.Equal(30, buf[i + 2]);
                Assert.Equal(255, buf[i + 3]); // opaque
            }
        }

        [Fact]
        public void StainSwatch_Is8x8AndBgrCorrected()
        {
            var buf = SwatchFactory.StainSwatch(0x0000FF); // blue
            Assert.Equal(SwatchFactory.SwatchSize * SwatchFactory.SwatchSize * 4, buf.Length);
            Assert.Equal(0, buf[0]);     // R
            Assert.Equal(0, buf[1]);     // G
            Assert.Equal(255, buf[2]);   // B
            Assert.Equal(255, buf[3]);   // A
        }

        [Fact]
        public void RarityColor_KnownValues_AndUnknownFallsBackToWhite()
        {
            Assert.Equal(((byte)0xEE, (byte)0xEE, (byte)0xEE), SwatchFactory.RarityColor(1));
            Assert.Equal(SwatchFactory.RarityColor(1), SwatchFactory.RarityColor(99)); // unknown → white
            Assert.NotEqual(SwatchFactory.RarityColor(1), SwatchFactory.RarityColor(3)); // blue ≠ white
        }
    }
}
