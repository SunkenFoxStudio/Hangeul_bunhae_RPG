using System;
using System.Collections.Generic;
using System.Linq;
using Hangeul.App;
using Hangeul.Domain;
using NUnit.Framework;

namespace Hangeul.Tests
{
    /// <summary>
    /// 가방 흔들기의 규칙 쪽(<see cref="ShakeSession"/>) 테스트. 실제 물리(Unity Physics2D, <c>ShakeWorld</c>)는 EditMode에서 돌지 않으므로
    /// 물리가 알려 주는 것(부딪힘, 위치, 각도)을 직접 넣어서 모델에 어떻게 반영되는지 확인한다.
    /// </summary>
    public class ShakeSessionTests
    {
        static ItemWordSet Words(params string[] w)
        {
            var set = new ItemWordSet();
            foreach (var x in w) set.Add(x);
            return set;
        }

        static ShakeSession Session(InventoryModel m, float mergeChance = 1f, int seed = 1)
            => new ShakeSession(m, new ShakeSettings { MergeChance = mergeChance }, new Random(seed));

        static void AssertValidLayout(InventoryModel m)
        {
            var cells = m.Grid.Cards.Select(c => (c.Col, c.Row)).ToList();
            Assert.AreEqual(cells.Count, cells.Distinct().Count(), "칸이 겹친다");
            foreach (var (col, row) in cells) Assert.IsTrue(m.Grid.InGrid(col, row), $"격자 밖: {col},{row}");
        }

        [Test]
        public void Bodies_StartAtCellCenters()
        {
            var m = new InventoryModel(4, 4);
            m.PlaceJamo('ㄱ', 2, 3);
            var b = Session(m).Bodies.Single();
            Assert.AreEqual((2.5f, 3.5f), (b.X, b.Y));
            Assert.IsFalse(b.Fixed);
            Assert.IsNotNull(b.Rotation);          // 자모는 돌 수 있다
        }

        [Test]
        public void TryMerge_CombinesTwoCards_AndKillsOne()
        {
            var m = new InventoryModel(4, 4);
            m.PlaceJamo('ㄱ', 0, 0);
            m.PlaceJamo('ㅏ', 3, 3);
            var s = Session(m);

            Assert.IsTrue(s.TryMerge(s.Bodies[0], s.Bodies[1]));
            Assert.AreEqual(1, s.Merged);
            Assert.AreEqual(1, s.Bodies.Count(b => b.Dead));
            var card = m.Grid.Cards.Single();
            Assert.AreEqual('가', card.Letter);
        }

        [Test]
        public void TryMerge_RespectsTheChance()
        {
            var m = new InventoryModel(4, 4);
            m.PlaceJamo('ㄱ', 0, 0);
            m.PlaceJamo('ㅏ', 3, 3);
            var s = Session(m, mergeChance: 0f);
            Assert.IsFalse(s.TryMerge(s.Bodies[0], s.Bodies[1]));
            Assert.AreEqual(2, m.Grid.Cards.Count);

            int merged = 0;                                         // 0.5면 일부만 조립된다
            for (int seed = 0; seed < 200; seed++)
            {
                var mm = new InventoryModel(4, 4);
                mm.PlaceJamo('ㄱ', 0, 0);
                mm.PlaceJamo('ㅏ', 3, 3);
                var ss = Session(mm, 0.5f, seed);
                if (ss.TryMerge(ss.Bodies[0], ss.Bodies[1])) merged++;
            }
            Assert.Greater(merged, 50);
            Assert.Less(merged, 150);
        }

        [Test]
        public void TryMerge_RejectsCardsThatCannotCombine_OrBannedSyllables()
        {
            var m = new InventoryModel(4, 4);
            m.PlaceJamo('ㅁ', 0, 0);
            m.PlaceJamo('ㄴ', 1, 0);          // 자음끼리는 겹자음이 안 되는 조합
            var s = Session(m);
            Assert.IsFalse(s.TryMerge(s.Bodies[0], s.Bodies[1]));

            var banned = new InventoryModel(4, 4);
            banned.AddWord("되");
            banned.PlaceJamo('ㅆ', 3, 3);      // 되 + ㅆ = 됬(금지)
            var bs = Session(banned);
            Assert.IsFalse(bs.TryMerge(bs.Bodies[0], bs.Bodies[1]));
            Assert.AreEqual(2, banned.Grid.Cards.Count);
        }

        [Test]
        public void CompletedWordCards_AreFixed_AndNeverMerge()
        {
            var m = new InventoryModel(6, 4);
            m.SetWords(Words("가방"));
            m.AddWord("가방");                  // 맨 위 왼쪽에 놓인다
            m.PlaceJamo('ㄴ', 5, 3);
            var s = Session(m);

            var fixedBodies = s.Bodies.Where(b => b.Fixed).ToList();
            Assert.AreEqual(2, fixedBodies.Count);
            var jamo = s.Bodies.Single(b => b.Card.Kind == CardKind.Jamo);
            Assert.IsFalse(s.TryMerge(jamo, fixedBodies[0]));           // 가 + ㄴ = 간 이 되지만 막힌다
            Assert.IsFalse(s.TryMerge(fixedBodies[0], jamo));
            Assert.AreEqual(3, m.Grid.Cards.Count);
        }

        [Test]
        public void IncompleteAndNonWordSyllables_AreDynamic()
        {
            var m = new InventoryModel(6, 4);
            m.SetWords(Words("가방"));
            m.AddWord("나무");                  // 단어가 아니다
            var s = Session(m);
            Assert.IsTrue(s.Bodies.All(b => !b.Fixed));
            Assert.IsTrue(s.Bodies.All(b => b.Rotation == null));       // 음절 카드는 글자가 바뀌지는 않는다
        }

        [Test]
        public void Rotation_ChangesTheGlyphEveryQuarterTurn()
        {
            var m = new InventoryModel(3, 3);
            var card = m.PlaceJamo('ㅏ', 1, 1);
            var s = Session(m);
            var b = s.Bodies.Single();

            void Pose(float angle) => s.SetPose(b, 1.5f, 1.5f, angle);

            Pose(40f); Assert.AreEqual('ㅏ', card.Jamo);               // 45° 전까지는 그대로
            Pose(95f); Assert.AreEqual('ㅜ', card.Jamo);               // 시계방향 한 칸
            Pose(185f); Assert.AreEqual('ㅓ', card.Jamo);
            Pose(275f); Assert.AreEqual('ㅗ', card.Jamo);
            Pose(365f); Assert.AreEqual('ㅏ', card.Jamo);              // 한 바퀴
            Pose(-85f); Assert.AreEqual('ㅗ', card.Jamo);              // 반대로 돌면 반대 순서
        }

        [Test]
        public void DisplayAngle_IsContinuousAcrossGlyphChanges()
        {
            var m = new InventoryModel(3, 3);
            m.PlaceJamo('ㅏ', 1, 1);
            var s = Session(m);
            var b = s.Bodies.Single();

            s.SetPose(b, 1.5f, 1.5f, 44f);
            float before = b.DisplayAngleDeg;
            s.SetPose(b, 1.5f, 1.5f, 46f);                              // 글자가 ㅜ로 바뀌는 순간
            float after = b.DisplayAngleDeg;

            Assert.AreEqual('ㅜ', b.Card.Jamo);
            Assert.AreEqual(44f, before, 1e-3f);
            Assert.AreEqual(46f - 90f, after, 1e-3f);                   // 글자가 90°를 맡고 화면에는 나머지만 돌려 그린다
            Assert.AreEqual(46f, after + 90f, 1e-3f);                   // 글자 + 나머지 = 물리 각도 그대로
        }

        [Test]
        public void NonRotatableJamo_JustSpins()
        {
            var m = new InventoryModel(3, 3);
            var card = m.PlaceJamo('ㅁ', 1, 1);
            var s = Session(m);
            var b = s.Bodies.Single();
            s.SetPose(b, 1.5f, 1.5f, 130f);
            Assert.AreEqual('ㅁ', card.Jamo);
            Assert.AreEqual(130f, b.DisplayAngleDeg, 1e-3f);
        }

        [Test]
        public void SyllableCards_SpinWithoutChangingTheirLetter()
        {
            var m = new InventoryModel(3, 3);
            m.AddWord("가");
            var s = Session(m);
            var b = s.Bodies.Single();
            s.SetPose(b, 1.5f, 1.5f, 200f);
            Assert.AreEqual('가', b.Card.Letter);
            Assert.AreEqual(200f, b.DisplayAngleDeg, 1e-3f);
        }

        [Test]
        public void Merge_RestartsRotationFromTheCurrentAngle()
        {
            var m = new InventoryModel(4, 4);
            m.PlaceJamo('ㅏ', 0, 0);
            m.PlaceJamo('ㅣ', 3, 3);          // ㅏ + ㅣ = ㅐ (자모로 남는다)
            var s = Session(m);
            s.SetPose(s.Bodies[0], 0.5f, 0.5f, 10f);
            s.SetPose(s.Bodies[1], 3.5f, 3.5f, 40f);         // 둘 다 45° 안이라 글자는 그대로(ㅏ, ㅣ)

            // 어느 쪽이 남든 합쳐진 카드는 합치기 직전 각도에서 이어서 돈다
            Assert.IsTrue(s.TryMerge(s.Bodies[0], s.Bodies[1]));
            var alive = s.Bodies.Single(b => !b.Dead);
            Assert.AreEqual(alive.Card.Kind, CardKind.Jamo);
            Assert.AreEqual(0, alive.Rotation.Steps);                         // 새 자모는 여기서부터 다시 센다
            Assert.AreEqual(alive.Angle, alive.AngleBase, 1e-3f);
            Assert.AreEqual(alive.Angle, alive.DisplayAngleDeg, 1e-3f);       // 합쳐진 직후에도 화면 각도가 튀지 않는다
        }

        [Test]
        public void Finish_SnapsToNearestFreeCells_WithoutOverlap()
        {
            var m = new InventoryModel(5, 5);
            foreach (var (j, c, r) in new[] { ('ㄱ', 0, 0), ('ㅏ', 1, 0), ('ㄴ', 2, 0), ('ㅜ', 3, 0) }) m.PlaceJamo(j, c, r);
            var s = Session(m);

            foreach (var b in s.Bodies) s.SetPose(b, 2.5f, 4.6f, 0f);   // 전부 아래쪽 한가운데에 쌓였다
            s.Finish();

            Assert.IsTrue(s.Finished);
            AssertValidLayout(m);
            Assert.IsTrue(m.Grid.Cards.All(c => c.Row >= 3));           // 아래쪽 줄에 모인다
            Assert.IsTrue(m.Grid.Cards.Any(c => c.Col == 2 && c.Row == 4));   // 가장 가까운 칸은 (2,4)
        }

        [Test]
        public void Finish_KeepsFixedCardsInPlace_AndAvoidsTheirCells()
        {
            var m = new InventoryModel(6, 4);
            m.SetWords(Words("가방"));
            m.AddWord("가방");                  // 맨 위 왼쪽에 놓인다
            var jamo = m.PlaceJamo('ㄴ', 5, 3);
            var ga = m.Grid.Cards.First(c => c.Letter == '가');
            var bang = m.Grid.Cards.First(c => c.Letter == '방');
            var start = ((ga.Col, ga.Row), (bang.Col, bang.Row));

            var s = Session(m);
            var body = s.Bodies.Single(b => b.Card == jamo);
            s.SetPose(body, ga.Col + 0.5f, ga.Row + 0.5f, 0f);          // 완성된 카드 바로 위에 올라타 멈췄다
            s.Finish();

            Assert.AreEqual(start, ((ga.Col, ga.Row), (bang.Col, bang.Row)));
            Assert.AreNotEqual((ga.Col, ga.Row), (jamo.Col, jamo.Row));
            AssertValidLayout(m);
        }

        [Test]
        public void Finish_ConfirmsTheNearestQuarterTurn()
        {
            var m = new InventoryModel(3, 3);
            var card = m.PlaceJamo('ㅏ', 1, 1);
            var s = Session(m);
            var b = s.Bodies.Single();
            s.SetPose(b, 1.5f, 1.5f, 172f);                              // 180° 근처에서 멈췄다
            s.Finish();
            Assert.AreEqual('ㅓ', card.Jamo);
        }

        [Test]
        public void Finish_IsIdempotent_AndStopsFurtherChanges()
        {
            var m = new InventoryModel(3, 3);
            var card = m.PlaceJamo('ㅏ', 1, 1);
            var s = Session(m);
            var b = s.Bodies.Single();
            s.Finish();
            s.Finish();
            s.SetPose(b, 0.5f, 0.5f, 200f);                              // 끝난 뒤에는 무시
            Assert.AreEqual('ㅏ', card.Jamo);
            Assert.IsFalse(s.TryMerge(b, b));
        }

        [Test]
        public void NothingToShake_IsFinishedImmediately()
        {
            Assert.IsTrue(new ShakeSession(new InventoryModel(3, 3)).Finished);

            var m = new InventoryModel(6, 4);
            m.SetWords(Words("가방"));
            m.AddWord("가방");
            Assert.IsTrue(new ShakeSession(m).Finished);                 // 전부 완성된 단어면 흔들 카드가 없다
        }

        [Test]
        public void ManyRandomPoses_AlwaysGiveAValidLayout()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var rng = new Random(seed);
                var m = new InventoryModel(6, 6);
                int i = 0;
                foreach (char j in "ㄱㅏㄴㅗㅅㅜㅓㅡㅣㅁㅇㅂㅈ") { m.PlaceJamo(j, i % 6, i / 6); i++; }
                m.AddWord("가");
                int before = m.Grid.Cards.Count;
                var s = Session(m, 1f, seed);

                foreach (var b in s.Bodies)
                    s.SetPose(b, (float)rng.NextDouble() * 6f, (float)rng.NextDouble() * 6f, (float)rng.NextDouble() * 720f - 360f);
                for (int k = 0; k < 10; k++)                             // 아무 카드끼리 부딪힌 걸로 친다
                    s.TryMerge(s.Bodies[rng.Next(s.Bodies.Count)], s.Bodies[rng.Next(s.Bodies.Count)]);
                s.Finish();

                AssertValidLayout(m);
                Assert.AreEqual(before - s.Merged, m.Grid.Cards.Count, $"seed {seed}");
            }
        }

        [Test]
        public void ApplyLayout_RejectsConflicts_AndNotifiesMoves()
        {
            var m = new InventoryModel(4, 4);
            var a = m.PlaceJamo('ㄱ', 0, 0);
            var b = m.PlaceJamo('ㅏ', 1, 0);
            var c = m.PlaceJamo('ㄴ', 3, 3);

            Assert.IsFalse(m.ApplyLayout(new List<(Card, int, int)> { (a, 2, 2), (b, 2, 2) }));   // 서로 겹침
            Assert.IsFalse(m.ApplyLayout(new List<(Card, int, int)> { (a, 3, 3) }));               // 목록에 없는 카드의 칸
            Assert.IsFalse(m.ApplyLayout(new List<(Card, int, int)> { (a, 4, 0) }));               // 격자 밖
            Assert.AreEqual(((0, 0), (1, 0)), ((a.Col, a.Row), (b.Col, b.Row)));                  // 아무것도 바뀌지 않음

            var moved = new List<int>();
            m.Changed += ch => { if (ch.Type == ChangeType.Moved) moved.Add(ch.Card.Id); };
            Assert.IsTrue(m.ApplyLayout(new List<(Card, int, int)> { (a, 1, 0), (b, 0, 0), (c, 3, 3) }));   // 자리 바꿈은 허용(전체가 겹치지 않으면)
            Assert.AreEqual(((1, 0), (0, 0)), ((a.Col, a.Row), (b.Col, b.Row)));
            CollectionAssert.AreEquivalent(new[] { a.Id, b.Id }, moved);                          // 안 움직인 카드는 알리지 않는다
        }
    }
}
