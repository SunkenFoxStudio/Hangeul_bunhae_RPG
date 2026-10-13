using System.Collections.Generic;
using System.Linq;
using Hangeul.App;
using Hangeul.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Hangeul.Tests
{
    public class BagModeTests
    {
        static ItemWordSet Words(params string[] w)
        {
            var s = new ItemWordSet();
            foreach (var x in w) s.Add(x);
            return s;
        }

        static InventoryModel Make(int cols, int rows, params string[] words)
        {
            var m = new InventoryModel(cols, rows);
            m.SetWords(Words(words));
            return m;
        }

        [Test]
        public void SetMode_RaisesEventOnlyOnChange()
        {
            var m = new InventoryModel();
            var seen = new List<InventoryMode>();
            m.ModeChanged += seen.Add;

            Assert.AreEqual(InventoryMode.Assemble, m.Mode);
            m.SetMode(InventoryMode.Assemble);
            m.SetMode(InventoryMode.Bag);
            m.SetMode(InventoryMode.Bag);
            CollectionAssert.AreEqual(new[] { InventoryMode.Bag }, seen);
        }

        [Test]
        public void Mode_IsSavedAndRestored()
        {
            var a = new InventoryModel();
            a.SetMode(InventoryMode.Bag);

            string json = JsonUtility.ToJson(a.ToSave());
            var b = new InventoryModel();
            Assert.IsTrue(b.Load(JsonUtility.FromJson<InventorySaveData>(json)).Ok);
            Assert.AreEqual(InventoryMode.Bag, b.Mode);

            // 모드가 없는 옛 세이브/알 수 없는 값은 조립 모드
            var c = new InventoryModel();
            c.SetMode(InventoryMode.Bag);
            c.Load(new InventorySaveData { mode = "???" });
            Assert.AreEqual(InventoryMode.Assemble, c.Mode);
        }

        [Test]
        public void Scanned_FiresOnEveryScan_EvenWithoutNotify()
        {
            var m = Make(5, 5, "가방");
            int count = 0;
            m.Scanned += () => count++;
            m.AddWord("가방");        // 내부에서 Scan(false)
            m.Scan(true);
            Assert.AreEqual(2, count);
            Assert.AreEqual("가방", m.Scanner.Shown.Single().Word);
        }

        [Test]
        public void MoveGroup_MovesWholeWord_ToEmptyCells()
        {
            var m = Make(8, 4, "가방");
            m.AddWord("가방", (1, 0));
            var cards = m.Grid.Cards.OrderBy(c => c.Col).ToList();
            Assert.AreEqual(2, cards.Count);

            Assert.IsTrue(m.MoveGroup(cards, 4, 2));
            Assert.AreEqual(new[] { (4, 2), (5, 2) }, cards.Select(c => (c.Col, c.Row)).ToArray());
            m.Scan(true);
            Assert.AreEqual("가방", m.Scanner.Shown.Single().Word);   // 옮겨도 단어는 그대로
        }

        [Test]
        public void MoveGroup_AllowsOverlapWithItself_ButNotOtherCards()
        {
            var m = Make(8, 4, "가방");
            m.AddWord("가방", (0, 0));
            var cards = m.Grid.Cards.OrderBy(c => c.Col).ToList();
            Assert.IsTrue(m.CanMoveGroup(cards, 1, 0));               // 한 칸 오른쪽: 자기 칸과 겹쳐도 된다

            var blocker = m.PlaceJamo('ㄱ', 4, 2);
            Assert.NotNull(blocker);
            Assert.IsFalse(m.CanMoveGroup(cards, 3, 2));               // (4,2)가 막혀 있음
            Assert.IsFalse(m.MoveGroup(cards, 3, 2));
            Assert.AreEqual((0, 0), (cards[0].Col, cards[0].Row));      // 실패하면 아무것도 바뀌지 않는다
        }

        [Test]
        public void MoveGroup_RejectsOutOfGrid()
        {
            var m = Make(4, 4, "가방");
            m.AddWord("가방", (0, 0));
            var cards = m.Grid.Cards.OrderBy(c => c.Col).ToList();
            Assert.IsFalse(m.CanMoveGroup(cards, 3, 0));               // 오른쪽 끝을 넘음
            Assert.IsFalse(m.CanMoveGroup(cards, -1, 0));
            Assert.IsFalse(m.CanMoveGroup(cards, 0, 4));
            Assert.IsFalse(m.CanMoveGroup(new List<Card>(), 0, 0));
        }
    }
}
