using System;
using System.Linq;
using Hangeul.App;
using Hangeul.Domain;
using NUnit.Framework;

namespace Hangeul.Tests
{
    public class ShakeBagTests
    {
        [Test]
        public void MergeChanceOne_CombinesCombinablePair()
        {
            var m = new InventoryModel(5, 5);
            m.PlaceJamo('ㄱ', 0, 0);
            m.PlaceJamo('ㅏ', 3, 3);          // 떨어져 있어도 조립된다

            var r = m.ShakeBag(new Random(1), mergeChance: 1f, rotateChance: 0f);

            Assert.AreEqual(1, r.Merged);
            Assert.AreEqual(0, r.Rotated);
            var card = m.Grid.Cards.Single();
            Assert.AreEqual('가', card.Letter);
        }

        [Test]
        public void RotateChanceOne_RotatesRotatable_AndLeavesOthers()
        {
            var m = new InventoryModel(5, 5);
            var a = m.PlaceJamo('ㅏ', 0, 0);
            var mm = m.PlaceJamo('ㅁ', 1, 0);   // 회전할 모양이 없는 자모 (조립은 mergeChance가 0이라 일어나지 않는다)
            var r = m.ShakeBag(new Random(2), mergeChance: 0f, rotateChance: 1f);

            Assert.AreEqual(1, r.Rotated);
            Assert.AreEqual(0, r.Merged);
            Assert.AreNotEqual('ㅏ', a.Jamo);
            Assert.IsTrue("ㅜㅓㅗ".Contains(a.Jamo));
            Assert.AreEqual('ㅁ', mm.Jamo);
        }

        [Test]
        public void ZeroChances_ChangeNothing()
        {
            var m = new InventoryModel(5, 5);
            m.PlaceJamo('ㄱ', 0, 0);
            m.PlaceJamo('ㅏ', 1, 0);
            var r = m.ShakeBag(new Random(3), 0f, 0f);
            Assert.AreEqual((0, 0), (r.Rotated, r.Merged));
            Assert.AreEqual(2, m.Grid.Cards.Count);
        }

        [Test]
        public void SyllableCards_AreNeverTouched()
        {
            var m = new InventoryModel(6, 4);
            m.AddWord("가방");
            var before = m.Grid.Cards.Select(c => (c.Id, c.Letter)).ToArray();

            var r = m.ShakeBag(new Random(4), 1f, 1f);
            Assert.AreEqual((0, 0), (r.Rotated, r.Merged));            // 자모 카드가 없으니 아무 일도 없다
            CollectionAssert.AreEqual(before, m.Grid.Cards.Select(c => (c.Id, c.Letter)).ToArray());
        }

        [Test]
        public void JamoMergesIntoSyllableCard_ButSyllableStaysValid()
        {
            var m = new InventoryModel(5, 5);
            m.AddWord("가");
            m.PlaceJamo('ㄴ', 0, 0);

            m.ShakeBag(new Random(5), 1f, 0f);                         // ㄴ이 가에 받침으로 들어간다
            var card = m.Grid.Cards.Single();
            Assert.AreEqual('간', card.Letter);
        }

        [Test]
        public void NeverCreatesBannedSyllable()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var m = new InventoryModel(6, 6);
                m.AddWord("되");
                m.PlaceJamo('ㅆ', 0, 0);                               // 되 + ㅆ = 됬(금지)
                m.ShakeBag(new Random(seed), 1f, 0f);
                Assert.IsFalse(m.Grid.Cards.Any(c => c.Kind == CardKind.Syllable && ForbiddenSyllables.IsBanned(c.S)), $"seed {seed}");
            }
        }

        [Test]
        public void Properties_HoldAcrossManySeeds()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                var m = new InventoryModel(7, 7);
                int i = 0;
                foreach (char j in "ㄱㅏㄴㅗㅇㅅㅣㅜㅓㄹ") { m.PlaceJamo(j, i % 7, i / 7); i++; }

                int before = m.Grid.Cards.Count;
                var r = m.ShakeBag(new Random(seed));

                Assert.AreEqual(before - r.Merged, m.Grid.Cards.Count, $"seed {seed}");                      // 조립한 만큼만 줄어든다
                Assert.AreEqual(m.Grid.Cards.Count, m.Grid.Cards.Select(c => (c.Col, c.Row)).Distinct().Count()); // 칸이 겹치지 않는다
                foreach (var c in m.Grid.Cards)
                {
                    if (c.Kind == CardKind.Jamo)
                        Assert.IsTrue(Hangul.IsCho(c.Jamo) || Hangul.IsJung(c.Jamo) || Hangul.IsJong(c.Jamo), $"seed {seed}: {c.Jamo}");
                    else
                        Assert.IsTrue(Hangul.TryCompose(c.S.Cho != 0 ? c.S.Cho : c.G.Cho, c.S.Jung != 0 ? c.S.Jung : c.G.Jung, c.S.Jong != 0 ? c.S.Jong : c.G.Jong, out _), $"seed {seed}");
                }
            }
        }

        [Test]
        public void Seeded_IsDeterministic()
        {
            string Run(int seed)
            {
                var m = new InventoryModel(5, 5);
                int i = 0;
                foreach (char j in "ㄱㅏㄴㅗㅇ") { m.PlaceJamo(j, i, 0); i++; }
                m.ShakeBag(new Random(seed));
                return string.Join("|", m.Grid.Cards.OrderBy(c => c.Id).Select(c => $"{c.Id}:{c.Kind}:{c.Jamo}:{c.Letter}"));
            }
            Assert.AreEqual(Run(42), Run(42));
        }

        static ItemWordSet Words(params string[] w)
        {
            var set = new ItemWordSet();
            foreach (var x in w) set.Add(x);
            return set;
        }

        [Test]
        public void CompletedWord_IsNotAMergeTarget()
        {
            var m = new InventoryModel(6, 4);
            m.SetWords(Words("가방"));
            m.AddWord("가방");                  // 이미 완성된 단어
            m.PlaceJamo('ㄴ', 0, 0);              // 가+ㄴ=간이 되지만 완성된 단어라 막혀야 한다

            var r = m.ShakeBag(new Random(1), mergeChance: 1f, rotateChance: 0f);

            Assert.AreEqual(0, r.Merged);
            Assert.AreEqual(3, m.Grid.Cards.Count);
            CollectionAssert.AreEquivalent(new char?[] { '가', '방' }, m.Grid.Cards.Where(c => c.Letter.HasValue).Select(c => c.Letter).ToArray());
        }

        [Test]
        public void WithoutThatWord_SameCardsDoMerge()
        {
            var m = new InventoryModel(6, 4);
            m.AddWord("가방");                  // 단어 목록이 없으면 완성된 단어가 아니다
            m.PlaceJamo('ㄴ', 0, 0);

            var r = m.ShakeBag(new Random(1), mergeChance: 1f, rotateChance: 0f);
            Assert.AreEqual(1, r.Merged);
            Assert.IsTrue(m.Grid.Cards.Any(c => c.Letter == '간'));
        }

        [Test]
        public void WordFormedDuringShake_IsProtectedFromThen()
        {
            bool sawWord = false;
            for (int seed = 0; seed < 150; seed++)
            {
                var m = new InventoryModel(6, 6);
                m.SetWords(Words("가"));
                m.PlaceJamo('ㄱ', 0, 0);
                m.PlaceJamo('ㅏ', 2, 0);
                m.PlaceJamo('ㄴ', 4, 4);          // 가가 만들어지면 여기에 받침으로 붙으면 안 된다

                m.ShakeBag(new Random(seed), mergeChance: 1f, rotateChance: 0f);

                Assert.IsFalse(m.Grid.Cards.Any(c => c.Letter == '간'), $"seed {seed}");
                if (m.Grid.Cards.Any(c => c.Letter == '가')) sawWord = true;
            }
            Assert.IsTrue(sawWord);              // 가가 만들어지는 경우는 실제로 있다
        }

        [Test]
        public void FindAll_DoesNotChangeScannerState()
        {
            var m = new InventoryModel(6, 4);
            m.SetWords(Words("가방"));
            m.AddWord("가방");
            int shownBefore = m.Scanner.Shown.Count;

            Assert.AreEqual(1, m.Scanner.FindAll(m.Grid).Count);
            Assert.AreEqual(shownBefore, m.Scanner.Shown.Count);
            Assert.IsEmpty(m.Scan(true));      // 알림 기준도 그대로
        }

        [Test]
        public void EmptyBag_NoCrash()
        {
            var m = new InventoryModel(3, 3);
            var r = m.ShakeBag(new Random(0));
            Assert.AreEqual((0, 0), (r.Rotated, r.Merged));
        }
    }
}
