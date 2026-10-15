using Hangeul.App;
using NUnit.Framework;
using UnityEngine;

namespace Hangeul.Tests
{
    public class ColorHexTests
    {
        [TestCase("#FF9E33", "#FF9E33")]
        [TestCase("ff9e33", "#FF9E33")]        // '#'가 없어도, 소문자여도
        [TestCase("  #ff9e33  ", "#FF9E33")]   // 앞뒤 공백
        [TestCase("#F80", "#FF8800")]          // 3자리 줄임말
        [TestCase("abc", "#AABBCC")]
        [TestCase("#000000", "#000000")]
        public void TryNormalize_AcceptsColors(string input, string expected)
        {
            Assert.IsTrue(ColorHex.TryNormalize(input, out string hex));
            Assert.AreEqual(expected, hex);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("#")]
        [TestCase("#FF9E3")]       // 5자리
        [TestCase("#FF9E3300")]    // 8자리(투명도)는 받지 않는다
        [TestCase("#GG9E33")]      // 16진수가 아닌 글자
        [TestCase("주황")]
        public void TryNormalize_RejectsNonColors(string input)
        {
            Assert.IsFalse(ColorHex.TryNormalize(input, out string hex));
            Assert.IsNull(hex);
        }

        [Test]
        public void NormalizeOr_FallsBackWhenNotAColor()
        {
            Assert.AreEqual("#112233", ColorHex.NormalizeOr("123", "#FFFFFF"));
            Assert.AreEqual("#FFFFFF", ColorHex.NormalizeOr("zzz", "#FFFFFF"));
            Assert.AreEqual(ColorHex.DefaultElement, ColorHex.NormalizeOr(null, ColorHex.DefaultElement));
        }

        // ───── 속성 글자 색 (ItemStats / WeaponModifier) ─────
        [Test]
        public void DefaultElementColor_IsOrange()
        {
            Assert.AreEqual("#FF9E33", new ItemStats().elementColor);
        }

        [Test]
        public void Clamped_NormalizesTheElementColor_AndFixesBrokenOnes()
        {
            Assert.AreEqual("#33AAFF", new ItemStats { elementColor = "3af" }.Clamped().elementColor);
            Assert.AreEqual(ColorHex.DefaultElement, new ItemStats { elementColor = "주황" }.Clamped().elementColor);
            Assert.AreEqual(ColorHex.DefaultElement, new ItemStats { elementColor = null }.Clamped().elementColor);   // 옛 파일에는 이 칸이 없다
        }

        [Test]
        public void SameAs_And_CopyFrom_CoverTheElementColor()
        {
            var a = new ItemStats { elementColor = "#5FD8FF" };
            var b = new ItemStats();
            Assert.IsFalse(a.SameAs(b));
            b.CopyFrom(a);
            Assert.IsTrue(a.SameAs(b));
        }

        [Test]
        public void WeaponModifier_CarriesTheElementColor()
        {
            var m = WeaponModifier.From("얼음", new ItemStats { prefixKind = PrefixEffectKind.Element, elementChance = 50f, elementColor = "#5fd8ff" });
            Assert.AreEqual("#5FD8FF", m.ElementColor);
            Assert.AreEqual("얼음", m.Element);

            var damage = WeaponModifier.From("강화", new ItemStats { prefixKind = PrefixEffectKind.Damage, damageAmount = 2f });
            Assert.IsNull(damage.ElementColor);
        }

        [Test]
        public void Json_RoundTrip_KeepsTheElementColor_AndOldFilesGetTheDefault()
        {
            var stats = new ItemStats { prefixKind = PrefixEffectKind.Element, elementChance = 40f, elementColor = "#B678FF" };
            var back = JsonUtility.FromJson<ItemStats>(JsonUtility.ToJson(stats));
            Assert.AreEqual("#B678FF", back.elementColor);

            // 색 칸이 없는 옛 항목: 기본 색으로 읽힌다
            var old = JsonUtility.FromJson<ItemStats>(@"{ ""prefixKind"": 1, ""elementChance"": 25 }");
            Assert.AreEqual(ColorHex.DefaultElement, old.Clamped().elementColor);
        }
    }
}
