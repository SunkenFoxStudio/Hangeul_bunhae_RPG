using System.Collections.Generic;
using System.Linq;
using Hangeul.App;
using Hangeul.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Hangeul.Tests
{
    public class SaveTests
    {
        // 카드를 비교하기 쉬운 문자열로
        static string Sig(Card c)
            => c.Kind == CardKind.Jamo
                ? $"{c.Id}:J:{c.Jamo}@{c.Col},{c.Row}"
                : $"{c.Id}:S:{Z(c.S.Cho)}{Z(c.S.Jung)}{Z(c.S.Jong)}/{Z(c.G.Cho)}{Z(c.G.Jung)}{Z(c.G.Jong)}@{c.Col},{c.Row}";

        static string Z(char c) => c == 0 ? "_" : c.ToString();

        static string[] Dump(InventoryModel m) => m.Grid.Cards.OrderBy(c => c.Id).Select(Sig).ToArray();

        static InventoryModel Sample()
        {
            var m = new InventoryModel(6, 5);
            m.AddWord("가방");
            m.AddWord("각");
            var ga = m.Grid.Cards.First(c => c.Letter == '가');
            m.Detach(ga, PullDir.R);                      // 낱개 자모 카드 두 장이 된다
            var gak = m.Grid.Cards.First(c => c.Letter == '각');
            m.Detach(gak, Slot.Cho, PullDir.U);           // 초성을 떼어 고스트가 남는 음절 카드
            m.PlaceJamo('ㅘ', 5, 4);
            return m;
        }

        [Test]
        public void RoundTrip_ModelToModel()
        {
            var a = Sample();
            var b = new InventoryModel(3, 3);
            var r = b.Load(a.ToSave());

            Assert.IsTrue(r.Ok);
            Assert.AreEqual(0, r.Skipped);
            Assert.AreEqual(0, r.Relocated);
            Assert.AreEqual((6, 5), (b.Grid.Cols, b.Grid.Rows));
            CollectionAssert.AreEqual(Dump(a), Dump(b));
        }

        [Test]
        public void RoundTrip_ThroughJson()
        {
            var a = Sample();
            string json = JsonUtility.ToJson(a.ToSave(), true);

            var b = new InventoryModel();
            Assert.IsTrue(b.Load(JsonUtility.FromJson<InventorySaveData>(json)).Ok);
            CollectionAssert.AreEqual(Dump(a), Dump(b));
        }

        [Test]
        public void Load_ContinuesIdsAfterMax()
        {
            var a = Sample();
            int max = a.Grid.Cards.Max(c => c.Id);
            var b = new InventoryModel();
            Assert.IsTrue(b.Load(a.ToSave()).Ok);

            Assert.IsTrue(b.Grid.TryFindEmptyNear(0, 0, out var cell));
            var fresh = b.PlaceJamo('ㅁ', cell.col, cell.row);
            Assert.Greater(fresh.Id, max);                // 불러온 카드와 아이디가 겹치지 않는다
        }

        [Test]
        public void Load_FiresDiscardedThenAdded()
        {
            var m = new InventoryModel(4, 4);
            m.AddWord("가");
            var log = new List<CardChange>();
            m.Changed += log.Add;

            var src = new InventoryModel(4, 4);
            src.AddWord("나다");
            m.Load(src.ToSave());

            Assert.AreEqual(1, log.Count(c => c.Type == ChangeType.Discarded));
            Assert.AreEqual(2, log.Count(c => c.Type == ChangeType.Added));
            Assert.AreEqual(ChangeType.Discarded, log[0].Type);
        }

        [Test]
        public void Load_RejectsBadVersion_AndNull_WithoutChangingAnything()
        {
            var m = new InventoryModel(4, 4);
            m.AddWord("가");

            Assert.IsFalse(m.Load(null).Ok);
            Assert.IsFalse(m.Load(new InventorySaveData { schemaVersion = 99 }).Ok);
            Assert.AreEqual(1, m.Grid.Cards.Count);
        }

        [Test]
        public void Load_SkipsInvalidCards()
        {
            var d = new InventorySaveData { cols = 5, rows = 5 };
            d.cards.Add(new CardSaveData { id = 1, kind = "jamo", jamo = "ㄱ", col = 0, row = 0 });   // 정상
            d.cards.Add(new CardSaveData { id = 2, kind = "jamo", jamo = "a", col = 1, row = 0 });    // 자모 아님
            d.cards.Add(new CardSaveData { id = 3, kind = "jamo", jamo = "ㄱㄴ", col = 2, row = 0 }); // 두 글자
            d.cards.Add(new CardSaveData { id = 4, kind = "item", col = 3, row = 0 });                // 모르는 종류
            d.cards.Add(new CardSaveData
            {
                id = 5, kind = "syl", col = 4, row = 0,
                s = new SlotsSaveData { cho = "ㅏ", jung = "ㅏ" },                                     // 초성 자리에 모음
            });
            d.cards.Add(new CardSaveData
            {
                id = 6, kind = "syl", col = 0, row = 1,
                s = new SlotsSaveData { cho = "ㄱ" },                                                  // 슬롯 하나뿐
            });
            d.cards.Add(null);

            var m = new InventoryModel();
            var r = m.Load(d);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(1, r.Loaded);
            Assert.AreEqual(6, r.Skipped);
            Assert.AreEqual("1:J:ㄱ@0,0", Sig(m.Grid.Cards.Single()));
        }

        [Test]
        public void Load_ValidSyllableWithGhost()
        {
            var d = new InventorySaveData { cols = 5, rows = 5 };
            d.cards.Add(new CardSaveData
            {
                id = 7, kind = "syl", col = 2, row = 2,
                s = new SlotsSaveData { jung = "ㅏ", jong = "ㄱ" },
                g = new SlotsSaveData { cho = "ㄱ", jung = "ㅏ" },   // 중성 고스트는 채워진 슬롯과 겹쳐서 버려진다
            });
            var m = new InventoryModel();
            Assert.IsTrue(m.Load(d).Ok);
            var c = m.Grid.Cards.Single();
            Assert.AreEqual('ㄱ', c.G.Cho);
            Assert.AreEqual('\0', c.G.Jung);
            Assert.IsFalse(c.IsComplete);
        }

        [Test]
        public void Load_RelocatesOutOfRangeAndOverlapping()
        {
            var d = new InventorySaveData { cols = 3, rows = 3 };
            d.cards.Add(new CardSaveData { id = 1, kind = "jamo", jamo = "ㄱ", col = 1, row = 1 });
            d.cards.Add(new CardSaveData { id = 2, kind = "jamo", jamo = "ㄴ", col = 1, row = 1 });   // 겹침
            d.cards.Add(new CardSaveData { id = 3, kind = "jamo", jamo = "ㄷ", col = 9, row = 9 });   // 범위 밖
            var m = new InventoryModel();
            var r = m.Load(d);

            Assert.AreEqual(3, r.Loaded);
            Assert.AreEqual(2, r.Relocated);
            Assert.AreEqual(3, m.Grid.Cards.Select(c => (c.Col, c.Row)).Distinct().Count());
            Assert.IsTrue(m.Grid.Cards.All(c => m.Grid.InGrid(c.Col, c.Row)));
            Assert.AreEqual((2, 2), m.Grid.Cards.Where(c => c.Id == 3).Select(c => (c.Col, c.Row)).Single());   // 가장 가까운 빈 칸
        }

        [Test]
        public void Load_OverCapacity_DropsLastCards()
        {
            var d = new InventorySaveData { cols = 1, rows = 2 };
            for (int i = 0; i < 4; i++)
                d.cards.Add(new CardSaveData { id = i + 1, kind = "jamo", jamo = "ㄱ", col = 0, row = i });
            var m = new InventoryModel();
            var r = m.Load(d);
            Assert.AreEqual(2, r.Loaded);
            Assert.AreEqual(2, r.Skipped);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, m.Grid.Cards.Select(c => c.Id).ToArray());
        }

        [Test]
        public void Load_FixesDuplicateAndInvalidIds()
        {
            var d = new InventorySaveData { cols = 4, rows = 1 };
            d.cards.Add(new CardSaveData { id = 5, kind = "jamo", jamo = "ㄱ", col = 0, row = 0 });
            d.cards.Add(new CardSaveData { id = 5, kind = "jamo", jamo = "ㄴ", col = 1, row = 0 });
            d.cards.Add(new CardSaveData { id = 0, kind = "jamo", jamo = "ㄷ", col = 2, row = 0 });
            var m = new InventoryModel();
            Assert.IsTrue(m.Load(d).Ok);
            Assert.AreEqual(3, m.Grid.Cards.Select(c => c.Id).Distinct().Count());
            Assert.IsTrue(m.Grid.Cards.All(c => c.Id > 0));
        }

        [Test]
        public void Load_ClampsGridSize()
        {
            var m = new InventoryModel();
            Assert.IsTrue(m.Load(new InventorySaveData { cols = 0, rows = 99 }).Ok);
            Assert.AreEqual((1, 20), (m.Grid.Cols, m.Grid.Rows));
        }
    }
}
