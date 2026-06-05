using System;
using Sightseeingway.CharacterCard;
using Xunit;

namespace Sightseeingway.Tests
{
    public class CharCoreTests
    {
        // ---- naming ----

        [Theory]
        [InlineData(0, "CHAR_NAME0")]
        [InlineData(3, "CHAR_NAME3")]
        public void CharNaming_Name_BuildsFontVariant(int idx, string expected)
            => Assert.Equal(expected, CharNaming.Name(idx));

        [Fact]
        public void CharNaming_NumberLabel_AppendsNum()
            => Assert.Equal("CHAR_FACE_NUM", CharNaming.NumberLabel("CHAR_FACE"));

        [Theory]
        [InlineData("CHAR_WORLD")]
        [InlineData("CHAR_GC_NAME")]
        [InlineData("CHAR_JOB_ICON")]
        [InlineData("CHAR_NAME0")]
        public void CharNaming_Keys_AreIdentifierSafe(string key)
            => Assert.True(CharNaming.IsIdentifierSafe(key));

        // ---- option-name captions ----

        [Theory]
        [InlineData("CHAR_FACE", "Face")]
        [InlineData("CHAR_HAIRSTYLE", "Hairstyle")]
        [InlineData("CHAR_FACIALFEATURE7", "Facial Feature 7")]
        [InlineData("CHAR_GC_RANK", "GC Rank")]
        [InlineData("CHAR_RACE", "Race")]
        public void Captions_CoverKnownKeys(string key, string caption)
        {
            Assert.Equal(caption, CharCaptions.For(key));
            Assert.Equal($"{key}_LABEL", CharCaptions.LabelName(key));
        }

        [Fact]
        public void Captions_AllLabelNamesAreIdentifierSafe()
        {
            foreach (var key in CharCaptions.Names.Keys)
            {
                var label = CharCaptions.LabelName(key);
                Assert.True(CharNaming.IsIdentifierSafe(label), label);
                Assert.True(label.Length <= 64);
            }
        }

        [Fact]
        public void Captions_UnknownKey_IsNull()
            => Assert.Null(CharCaptions.For("CHAR_NOPE"));

        // ---- change signature ----

        [Fact]
        public void Signature_ChangesWhenCustomizeChanges()
        {
            var a = Make(new byte[26]);
            var changed = new byte[26];
            changed[6] = 5; // a different hairstyle byte
            var b = Make(changed);
            Assert.NotEqual(a.Signature(), b.Signature());
        }

        [Fact]
        public void Signature_StableForIdenticalData()
            => Assert.Equal(Make(new byte[26]).Signature(), Make(new byte[26]).Signature());

        [Fact]
        public void Signature_ChangesWhenIdentityChanges()
        {
            var a = Make(new byte[26]);
            var b = a with { CurrentWorld = "Faerie" };
            Assert.NotEqual(a.Signature(), b.Signature());
        }

        private static CharSnapshot Make(byte[] customize) => new(
            "Wol", "Sargatanas", "Sargatanas", "Aether",
            "Hyur", "Midlander", "Female",
            "White Mage", 0u, string.Empty, 0u, 0,
            customize,
            Array.Empty<CharNumber>(), Array.Empty<CharFlag>(), Array.Empty<CharColor>());
    }
}
