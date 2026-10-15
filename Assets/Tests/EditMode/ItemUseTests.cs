using System.Collections.Generic;
using System.Linq;
using Hangeul.App;
using NUnit.Framework;
using UnityEngine;

namespace Hangeul.Tests
{
    public class ItemUseTests
    {
        // 카테고리가 있는 단어 집합: 무기(검·칼·도끼·화살), 방어구(방패), 음식(사과·빵), 재료(물), 속성(불)
        static ItemWordSet Set()
        {
            var file = new ItemWordsFile { schemaVersion = 1 };
            file.items.Add(new ItemWordEntry { word = "불", category = "속성" });
            file.items.Add(new ItemWordEntry { word = "검", category = "무기" });
            file.items.Add(new ItemWordEntry { word = "칼", category = "무기" });
            file.items.Add(new ItemWordEntry { word = "도끼", category = "무기" });
            file.items.Add(new ItemWordEntry { word = "화살", category = "무기", acceptsPrefix = true, prefixes = new List<string> { "불" } });
            file.items.Add(new ItemWordEntry { word = "방패", category = "방어구" });
            file.items.Add(new ItemWordEntry { word = "사과", category = "음식" });
            file.items.Add(new ItemWordEntry { word = "빵", category = "음식" });
            file.items.Add(new ItemWordEntry { word = "물", category = "재료·잡화" });
            return ItemWordSet.FromFile(file, out _);
        }

        static InventoryModel Make(int cols = 8, int rows = 4)
        {
            var m = new InventoryModel(cols, rows);
            m.SetWords(Set());
            return m;
        }

        static WordMatch Shown(InventoryModel m, string word) => m.Scanner.Shown.Single(x => x.Word == word);

        static void Put(InventoryModel m, char letter, int col, int row) => m.AddWord(letter.ToString(), (col, row));

        // ───── 종류와 사용 규칙 ─────
        [Test]
        public void Kinds_FollowCategoryNames()
        {
            Assert.AreEqual(ItemKind.Weapon, ItemKinds.FromCategory("무기"));
            Assert.AreEqual(ItemKind.Armor, ItemKinds.FromCategory("방어구"));
            Assert.AreEqual(ItemKind.Food, ItemKinds.FromCategory("음식"));
            Assert.AreEqual(ItemKind.None, ItemKinds.FromCategory("재료·잡화"));
            Assert.AreEqual(ItemKind.None, ItemKinds.FromCategory("추가한 단어"));
            Assert.AreEqual(ItemKind.None, ItemKinds.FromCategory(null));

            Assert.AreEqual("무기", ItemKinds.Label(ItemKind.Weapon));
            Assert.AreEqual("방어구", ItemKinds.Label(ItemKind.Armor));
            Assert.AreEqual("음식", ItemKinds.Label(ItemKind.Food));
            Assert.AreEqual("", ItemKinds.Label(ItemKind.None));
        }

        [Test]
        public void Rules_WeaponTogglesEquip()
        {
            Assert.AreEqual(ItemUseAction.Equip, ItemUseRules.Decide(ItemKind.Weapon, false, 6, 6));
            Assert.AreEqual(ItemUseAction.Unequip, ItemUseRules.Decide(ItemKind.Weapon, true, 6, 6));
        }

        [Test]
        public void Rules_FoodIsEatenOnlyWhenHurt()
        {
            Assert.AreEqual(ItemUseAction.Eat, ItemUseRules.Decide(ItemKind.Food, false, 5, 6));
            Assert.AreEqual(ItemUseAction.Blocked, ItemUseRules.Decide(ItemKind.Food, false, 6, 6));   // 가득 차면 먹지 않는다
        }

        [Test]
        public void Rules_ArmorAndOthersDoNothing()
        {
            Assert.AreEqual(ItemUseAction.None, ItemUseRules.Decide(ItemKind.Armor, false, 3, 6));
            Assert.AreEqual(ItemUseAction.None, ItemUseRules.Decide(ItemKind.None, false, 3, 6));
        }

        [Test]
        public void Hints_OnlyForUsableActions()
        {
            Assert.AreEqual("장착", ItemUseRules.Hint(ItemUseAction.Equip));
            Assert.AreEqual("장착 해제", ItemUseRules.Hint(ItemUseAction.Unequip));
            Assert.AreEqual("먹기", ItemUseRules.Hint(ItemUseAction.Eat));
            Assert.AreEqual("체력이 가득 참", ItemUseRules.Hint(ItemUseAction.Blocked));
            Assert.AreEqual("", ItemUseRules.Hint(ItemUseAction.None));
        }

        [Test]
        public void WordSet_KnowsKinds_AndCompoundsFollowTheBaseWord()
        {
            var set = Set();
            Assert.AreEqual(ItemKind.Weapon, set.KindOf("검"));
            Assert.AreEqual(ItemKind.Armor, set.KindOf("방패"));
            Assert.AreEqual(ItemKind.Food, set.KindOf("사과"));
            Assert.AreEqual(ItemKind.None, set.KindOf("물"));
            Assert.AreEqual(ItemKind.None, set.KindOf("불"));       // 속성 단어는 쓰임이 없다
            Assert.AreEqual(ItemKind.None, set.KindOf("없는단어"));
            Assert.AreEqual(ItemKind.None, set.KindOf(null));

            Assert.IsTrue(set.IsCompound("불화살"));
            Assert.AreEqual("무기", set.CategoryOf("불화살"));       // 합성 단어는 본체('화살')의 카테고리
            Assert.AreEqual(ItemKind.Weapon, set.KindOf("불화살"));
        }

        // ───── 무기 장착 ─────
        [Test]
        public void Equip_Weapon_SetsEquippedAndRaisesEventOnce()
        {
            var m = Make();
            m.AddWord("검", (0, 0));
            var w = Shown(m, "검");
            int events = 0;
            m.EquipmentChanged += () => events++;

            Assert.AreEqual(ItemKind.Weapon, m.KindOf(w));
            Assert.IsNull(m.EquippedWeapon);
            Assert.IsFalse(m.IsEquipped(w));

            Assert.IsTrue(m.EquipWeapon(w));
            Assert.AreEqual("검", m.EquippedWeapon.Word);
            Assert.IsTrue(m.IsEquipped(w));
            Assert.AreEqual(1, events);

            Assert.IsTrue(m.EquipWeapon(w));    // 같은 무기를 또 장착해도 변화 없음
            Assert.AreEqual(1, events);
        }

        [Test]
        public void Equip_RejectsFoodArmorAndOthers()
        {
            var m = Make();
            m.AddWord("사과", (0, 0));
            m.AddWord("방패", (0, 1));
            m.AddWord("물", (0, 2));
            foreach (var word in new[] { "사과", "방패", "물" })
                Assert.IsFalse(m.EquipWeapon(Shown(m, word)), word);
            Assert.IsNull(m.EquippedWeapon);
        }

        [Test]
        public void Equip_SurvivesMovingTheWholeWord()
        {
            var m = Make();
            m.AddWord("도끼", (1, 0));
            var w = Shown(m, "도끼");
            Assert.IsTrue(m.EquipWeapon(w));

            Assert.IsTrue(m.ShiftGroup(m.CardsOf(w).ToList(), 3, 2));   // 가방 모드에서 덩어리를 통째로 옮김
            m.Scan(true);

            Assert.IsNotNull(m.EquippedWeapon);
            Assert.IsTrue(m.IsEquipped(Shown(m, "도끼")));
        }

        [Test]
        public void Equip_ReleasedWhenTheWordBreaks()
        {
            var m = Make();
            m.AddWord("도끼", (1, 0));
            var w = Shown(m, "도끼");
            m.EquipWeapon(w);
            int released = 0;
            m.EquipmentChanged += () => { if (m.EquippedWeapon == null) released++; };

            m.Move(m.CardsOf(w)[0], 7, 3);    // 한 글자를 떼어 놓으면 단어가 깨진다
            m.Scan(true);

            Assert.IsNull(m.EquippedWeapon);
            Assert.AreEqual(1, released);
        }

        [Test]
        public void Equip_ReleasedWhenAWordCardIsDiscarded()
        {
            var m = Make();
            m.AddWord("검", (0, 0));
            m.EquipWeapon(Shown(m, "검"));

            m.Discard(m.Grid.CardAt(0, 0));
            m.Scan(true);

            Assert.IsNull(m.EquippedWeapon);
        }

        [Test]
        public void Equip_ASecondWeaponReplacesTheFirst()
        {
            var m = Make();
            m.AddWord("검", (0, 0));
            m.AddWord("칼", (0, 1));
            m.EquipWeapon(Shown(m, "검"));
            m.EquipWeapon(Shown(m, "칼"));

            Assert.AreEqual("칼", m.EquippedWeapon.Word);
            Assert.IsFalse(m.IsEquipped(Shown(m, "검")));
            Assert.IsTrue(m.IsEquipped(Shown(m, "칼")));
        }

        [Test]
        public void Unequip_KeepsTheCards()
        {
            var m = Make();
            m.AddWord("검", (0, 0));
            m.EquipWeapon(Shown(m, "검"));

            m.UnequipWeapon();

            Assert.IsNull(m.EquippedWeapon);
            Assert.AreEqual(1, m.Grid.Cards.Count);    // 카드는 인벤토리에 그대로
            m.Scan(true);
            Assert.AreEqual("검", Shown(m, "검").Word);
        }

        [Test]
        public void Equip_PrefixStackedWord_ReleasedWhenThePrefixIsRemoved()
        {
            var m = Make(6, 4);
            Put(m, '불', 0, 0);
            Put(m, '화', 0, 1);
            Put(m, '살', 1, 1);
            m.Scan(true);

            var w = Shown(m, "불화살");
            Assert.IsTrue(w.IsStacked);
            Assert.AreEqual(ItemKind.Weapon, m.KindOf(w));
            Assert.IsTrue(m.EquipWeapon(w));
            Assert.AreEqual(3, m.EquippedWeapon.CardIds.Count);   // 접두어 카드까지 한 덩어리

            m.Discard(m.Grid.CardAt(0, 0));                       // '불'을 떼면 '화살'만 남는다 — 다른 단어(다른 카드 구성)
            m.Scan(true);

            Assert.IsNull(m.EquippedWeapon);
            Assert.AreEqual("화살", Shown(m, "화살").Word);
        }

        // ───── 음식 먹기 ─────
        [Test]
        public void ConsumeWord_RemovesOnlyThatWordsCards()
        {
            var m = Make();
            m.AddWord("사과", (0, 0));
            m.AddWord("빵", (0, 2));
            var apple = Shown(m, "사과");

            Assert.IsTrue(m.ConsumeWord(apple));
            m.Scan(true);

            Assert.AreEqual(1, m.Grid.Cards.Count);
            Assert.AreEqual("빵", m.Scanner.Shown.Single().Word);
            Assert.IsFalse(m.ConsumeWord(apple));    // 이미 없는 단어
        }

        [Test]
        public void ConsumeWord_IgnoresAStaleMatch()
        {
            var m = Make();
            m.AddWord("사과", (0, 0));
            var apple = Shown(m, "사과");
            m.Move(m.Grid.CardAt(0, 0), 5, 3);
            m.Scan(true);                            // 사과가 깨져 apple은 더 이상 보이는 단어가 아니다

            Assert.IsFalse(m.IsShown(apple));
            Assert.IsFalse(m.ConsumeWord(apple));
            Assert.AreEqual(2, m.Grid.Cards.Count);
        }

        // ───── 저장 ─────
        [Test]
        public void Equipment_IsRestoredFromSaveData()
        {
            var a = Make();
            a.SetMode(InventoryMode.Bag);
            a.AddWord("도끼", (1, 0));
            a.EquipWeapon(Shown(a, "도끼"));

            var data = a.ToSave();
            Assert.AreEqual("도끼", data.equippedWord);
            Assert.AreEqual(2, data.equippedCardIds.Count);

            var b = Make();
            Assert.IsTrue(b.Load(data).Ok);

            Assert.AreEqual("도끼", b.EquippedWeapon.Word);
            Assert.IsTrue(b.IsEquipped(Shown(b, "도끼")));
        }

        [Test]
        public void Equipment_SurvivesTheJsonRoundTrip()
        {
            var a = Make();
            a.AddWord("검", (0, 0));
            a.EquipWeapon(Shown(a, "검"));

            string json = JsonUtility.ToJson(a.ToSave());
            var b = Make();
            Assert.IsTrue(b.Load(JsonUtility.FromJson<InventorySaveData>(json)).Ok);

            Assert.AreEqual("검", b.EquippedWeapon.Word);
        }

        [Test]
        public void Equipment_NotRestoredWhenTheSavedCardsNoLongerMatch()
        {
            var a = Make();
            a.AddWord("검", (0, 0));
            a.EquipWeapon(Shown(a, "검"));
            var data = a.ToSave();
            data.equippedCardIds[0] = 999;           // 저장된 카드 아이디가 맞지 않으면 장착은 없던 일

            var b = Make();
            Assert.IsTrue(b.Load(data).Ok);
            Assert.IsNull(b.EquippedWeapon);
        }

        [Test]
        public void Load_ClearsThePreviousEquipment_AndOldSavesHaveNone()
        {
            var m = Make();
            m.AddWord("검", (0, 0));
            m.EquipWeapon(Shown(m, "검"));
            int events = 0;
            m.EquipmentChanged += () => events++;

            Assert.IsTrue(m.Load(new InventorySaveData()).Ok);    // 장착 필드가 없는 (옛) 세이브
            Assert.IsNull(m.EquippedWeapon);
            Assert.AreEqual(1, events);
        }
    }
}
