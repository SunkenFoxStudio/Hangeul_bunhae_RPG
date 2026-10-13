using Hangeul.Domain;
using NUnit.Framework;

namespace Hangeul.Tests
{
    public class HangulTests
    {
        [TestCase('가', 'ㄱ', 'ㅏ', '\0')]
        [TestCase('각', 'ㄱ', 'ㅏ', 'ㄱ')]
        [TestCase('힣', 'ㅎ', 'ㅣ', 'ㅎ')]
        [TestCase('닭', 'ㄷ', 'ㅏ', 'ㄺ')]
        [TestCase('왜', 'ㅇ', 'ㅙ', '\0')]
        public void Decompose_Compose_RoundTrip(char syl, char cho, char jung, char jong)
        {
            Assert.IsTrue(Hangul.TryDecompose(syl, out var c, out var v, out var t));
            Assert.AreEqual((cho, jung, jong), (c, v, t));
            Assert.AreEqual(syl, Hangul.Compose(c, v, t));
        }

        [Test]
        public void AllSyllables_RoundTrip()
        {
            for (char s = '가'; s <= '힣'; s++)
            {
                Hangul.TryDecompose(s, out var c, out var v, out var t);
                Assert.AreEqual(s, Hangul.Compose(c, v, t));
            }
        }

        [Test]
        public void NonSyllable_Fails()
        {
            Assert.IsFalse(Hangul.TryDecompose('ㄱ', out _, out _, out _));
            Assert.IsFalse(Hangul.TryCompose('ㅏ', 'ㅏ', '\0', out _));   // 초성이 아닌 자모
            Assert.IsFalse(Hangul.TryCompose('ㄱ', 'ㅏ', 'ㄸ', out _));   // 종성 불가(ㄸ)
        }

        [Test]
        public void Classification()
        {
            Assert.IsTrue(Hangul.IsCho('ㄸ')); Assert.IsFalse(Hangul.IsJong('ㄸ'));
            Assert.IsTrue(Hangul.IsJong('ㄳ')); Assert.IsFalse(Hangul.IsCho('ㄳ'));
            Assert.IsFalse(Hangul.IsJong('\0')); Assert.IsFalse(Hangul.IsCho('\0'));
            Assert.IsTrue(Hangul.IsJung('ㅢ'));
        }
    }

    public class JamoTablesTests
    {
        [TestCase('ㄱ', 'ㄱ', 'ㄲ')]
        [TestCase('ㄱ', 'ㅅ', 'ㄳ')]
        [TestCase('ㅅ', 'ㄱ', 'ㄳ')]   // 순서 무관
        [TestCase('ㄹ', 'ㅎ', 'ㅀ')]
        [TestCase('ㅗ', 'ㅏ', 'ㅘ')]
        [TestCase('ㅏ', 'ㅗ', 'ㅘ')]
        [TestCase('ㅡ', 'ㅣ', 'ㅢ')]
        [TestCase('ㅜ', 'ㅜ', 'ㅠ')]
        public void Combine(char a, char b, char expected)
        {
            Assert.IsTrue(JamoTables.TryCombine(a, b, out var r));
            Assert.AreEqual(expected, r);
        }

        [TestCase('ㄱ', 'ㅏ')]   // 자음+모음은 CC/VV가 아님
        [TestCase('ㄴ', 'ㄱ')]
        [TestCase('ㅗ', 'ㅇ')]   // ㅎ은 홀드 규칙 전용
        public void Combine_Rejected(char a, char b) => Assert.IsFalse(JamoTables.TryCombine(a, b, out _));

        [TestCase('ㄲ', 'ㄱ', 'ㄱ')]
        [TestCase('ㄳ', 'ㄱ', 'ㅅ')]
        [TestCase('ㅘ', 'ㅗ', 'ㅏ')]
        [TestCase('ㅎ', 'ㅗ', 'ㅇ')]
        public void Split(char c, char first, char second)
        {
            Assert.IsTrue(JamoTables.TrySplit(c, out var p));
            Assert.AreEqual((first, second), p);
        }

        [Test]
        public void Split_NotCombined() => Assert.IsFalse(JamoTables.CanSplit('ㄱ'));

        [Test]
        public void EverySplittable_RecombinesExceptHieut()
        {
            foreach (var c in "ㄲㄸㅃㅆㅉㄳㄵㄶㄺㄻㄼㄽㄾㄿㅀㅄㅘㅙㅚㅝㅞㅟㅢㅐㅔㅒㅖㅑㅕㅛㅠ")
            {
                Assert.IsTrue(JamoTables.TrySplit(c, out var p), c.ToString());
                Assert.IsTrue(JamoTables.TryCombine(p.first, p.second, out var back));
                Assert.AreEqual(c, back);
            }
        }
    }

    public class RotationTableTests
    {
        [TestCase('ㅏ', "ㅏㅜㅓㅗ")]
        [TestCase('ㅜ', "ㅜㅓㅗㅏ")]
        [TestCase('ㅓ', "ㅓㅗㅏㅜ")]
        [TestCase('ㅗ', "ㅗㅏㅜㅓ")]
        [TestCase('ㅑ', "ㅑㅠㅕㅛ")]
        [TestCase('ㅠ', "ㅠㅕㅛㅑ")]
        [TestCase('ㅣ', "ㅣㅡㅣㅡ")]
        [TestCase('ㅡ', "ㅡㅣㅡㅣ")]
        [TestCase('ㄱ', "ㄱㄱㄴㄱ")]   // 매핑 없는 각도는 원래 자모 유지
        [TestCase('ㄴ', "ㄴㄴㄱㄴ")]
        [TestCase('ㅁ', "ㅁㅁㅁㅁ")]   // 표에 없는 자모
        public void Rotate_Table(char jamo, string expected)
        {
            for (int k = 0; k < 4; k++) Assert.AreEqual(expected[k], RotationTable.Rotate(jamo, k), $"{jamo} k={k}");
        }

        [Test]
        public void Rotate_WrapsAndNegative()
        {
            Assert.AreEqual('ㅏ', RotationTable.Rotate('ㅏ', 4));
            Assert.AreEqual('ㅜ', RotationTable.Rotate('ㅏ', 5));
            Assert.AreEqual('ㅗ', RotationTable.Rotate('ㅏ', -1));   // 반시계 90° = 시계 270°
            Assert.AreEqual('ㅜ', RotationTable.Rotate('ㅏ', -3));
        }

        [TestCase('ㅏ', 1)]
        [TestCase('ㅣ', 1)]
        [TestCase('ㄱ', 2)]   // 90°는 그대로, 180°에서 ㄴ
        [TestCase('ㄴ', 2)]
        [TestCase('ㅁ', 0)]
        public void NextStep(char jamo, int expected) => Assert.AreEqual(expected, RotationTable.NextStep(jamo));
    }

    public class SyllableLayoutTests
    {
        [TestCase('ㅏ', VowelLayout.V)]
        [TestCase('ㅣ', VowelLayout.V)]
        [TestCase('ㅗ', VowelLayout.H)]
        [TestCase('ㅡ', VowelLayout.H)]
        [TestCase('ㅘ', VowelLayout.C)]
        [TestCase('ㅢ', VowelLayout.C)]
        public void LayoutOf(char vowel, VowelLayout expected) => Assert.AreEqual(expected, SyllableLayout.LayoutOf(vowel));

        [Test]
        public void Regions_ComplexJungHasTwoRects()
        {
            Assert.AreEqual(2, SyllableLayout.Regions(VowelLayout.C, true, Slot.Jung).Length);
            Assert.AreEqual(0, SyllableLayout.Regions(VowelLayout.V, false, Slot.Jong).Length);
        }

        [Test]
        public void Pull_MatchesHtml()
        {
            CollectionAssert.AreEqual(new[] { Slot.Jung }, SyllableLayout.Pull(VowelLayout.V, PullDir.R));
            CollectionAssert.IsEmpty(SyllableLayout.Pull(VowelLayout.H, PullDir.L));
            CollectionAssert.AreEqual(new[] { Slot.Jong, Slot.Jung }, SyllableLayout.Pull(VowelLayout.H, PullDir.D));
            CollectionAssert.AreEqual(new[] { Slot.Jong, Slot.Jung }, SyllableLayout.Pull(VowelLayout.C, PullDir.D));
        }
    }
}
