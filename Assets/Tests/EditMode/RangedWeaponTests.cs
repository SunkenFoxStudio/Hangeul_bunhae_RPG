using System.Collections.Generic;
using System.Linq;
using Hangeul.App;
using Hangeul.UI;
using NUnit.Framework;
using UnityEngine;

namespace Hangeul.Tests
{
    /// <summary>원거리 무기(활)와 탄(화살)의 순수 규칙: 수치, 종류, 화살 고르기, 효과 겹치기, 게임 데이터의 탄 목록.</summary>
    public class RangedWeaponTests
    {
        // 활(무기) + 화살(탄, 접두어 가능) + 불·얼음(속성) + 강화(접두어)
        static ItemWordsFile File()
        {
            var file = new ItemWordsFile
            {
                schemaVersion = 1,
                categories = new List<string> { "무기", "탄", "속성", "접두어" },
                items = new List<ItemWordEntry>
                {
                    new ItemWordEntry { word = "활", category = "무기" },
                    new ItemWordEntry { word = "검", category = "무기" },
                    new ItemWordEntry { word = "화살", category = "탄", acceptsPrefix = true, prefixes = new List<string> { "불", "얼음" } },
                    new ItemWordEntry { word = "불", category = "속성" },
                    new ItemWordEntry { word = "얼음", category = "속성" },
                    new ItemWordEntry { word = "강화", category = "접두어" },
                },
            };
            file.categoryTypes.Add(new CategoryTypeEntry { name = "무기", type = CategoryType.Weapon });
            file.categoryTypes.Add(new CategoryTypeEntry { name = "탄", type = CategoryType.Ammo });
            file.categoryTypes.Add(new CategoryTypeEntry { name = "속성", type = CategoryType.Prefix });
            file.categoryTypes.Add(new CategoryTypeEntry { name = "접두어", type = CategoryType.Prefix });
            return file;
        }

        static InventoryModel Model()
        {
            var m = new InventoryModel(8, 4);
            m.SetWords(ItemWordSet.FromFile(File(), out _));
            return m;
        }

        // ───── 수치 ─────
        [Test]
        public void Defaults_AreMelee()
        {
            Assert.AreEqual(AttackStyle.Melee, new ItemStats().attackStyle);
            Assert.AreEqual(AttackStyle.Melee, new WeaponStats().attackStyle);
        }

        [Test]
        public void Clamped_FixesUnknownStyle_AndKeepsRangedStyle()
        {
            Assert.AreEqual(AttackStyle.Melee, new ItemStats { attackStyle = (AttackStyle)99 }.Clamped().attackStyle);
            Assert.AreEqual(AttackStyle.Ranged, new ItemStats { attackStyle = AttackStyle.Ranged }.Clamped().attackStyle);
            Assert.IsTrue(new ItemStats().SameAs(new ItemStats().Clamped()));   // 기본값은 이미 범위 안
        }

        [Test]
        public void SameAsAndCopyFrom_CoverTheAttackStyle()
        {
            var bow = new ItemStats { attackStyle = AttackStyle.Ranged };
            Assert.IsFalse(bow.SameAs(new ItemStats()));

            var copy = new ItemStats();
            copy.CopyFrom(bow);
            Assert.IsTrue(copy.SameAs(bow));
            Assert.AreEqual(AttackStyle.Ranged, copy.attackStyle);
        }

        [Test]
        public void WeaponStats_RoundTripsTheNewFields()
        {
            var stats = new WeaponStats { damage = 3, attackStyle = AttackStyle.Ranged, windup = 0.3f };
            var item = stats.ToItemStats();
            Assert.AreEqual(AttackStyle.Ranged, item.attackStyle);
            Assert.IsTrue(stats.SameAs(WeaponStats.From(item)));
            Assert.AreEqual(0.3f, WeaponStats.From(item).windup);
        }

        [Test]
        public void OldStatsWithoutTheNewFields_ReadAsMelee()
        {
            var old = JsonUtility.FromJson<ItemStats>("{\"damage\":3}");
            Assert.AreEqual(3, old.damage);
            Assert.AreEqual(AttackStyle.Melee, old.attackStyle);
        }

        // ───── 종류 ─────
        [Test]
        public void AmmoKind_ComesFromTheCategoryType_AndIsNotUsable()
        {
            Assert.AreEqual(ItemKind.Ammo, ItemKinds.FromType(CategoryType.Ammo));
            Assert.AreEqual("탄", ItemKinds.Label(ItemKind.Ammo));
            Assert.AreEqual(ItemUseAction.None, ItemUseRules.Decide(ItemKind.Ammo, false, 1, 6));   // 장착도 먹기도 없다
            Assert.AreEqual("", ItemUseRules.Hint(ItemUseAction.None));

            var set = ItemWordSet.FromFile(File(), out _);
            Assert.AreEqual(ItemKind.Ammo, set.KindOf("화살"));
            Assert.AreEqual(ItemKind.Ammo, set.KindOf("불화살"));   // 합성 단어는 본체의 종류를 따른다
            Assert.AreEqual(ItemKind.Weapon, set.KindOf("활"));
        }

        // ───── 화살 고르기 ─────
        [Test]
        public void PickArrow_IsNullWithoutAnyArrow()
        {
            var m = Model();
            Assert.IsNull(m.PickArrow());
            m.AddWord("활", (0, 0));   // 무기만 있다
            Assert.IsNull(m.PickArrow());
        }

        [Test]
        public void PickArrow_FindsAnArrowWord_AndLeavesTheCardsAlone()
        {
            var m = Model();
            m.AddWord("화살", (0, 2));
            int before = m.Grid.Cards.Count;
            var picked = m.PickArrow();
            Assert.IsNotNull(picked);
            Assert.AreEqual("화살", picked.Value.Word);
            Assert.AreEqual(before, m.Grid.Cards.Count);   // 쏴도 카드는 줄지 않는다
        }

        [Test]
        public void PickArrow_TakesTheFirstOneInReadingOrder()
        {
            var m = Model();
            m.AddWord("화살", (0, 3));       // 아래
            m.AddWord("불화살", (0, 0));     // 위
            var shown = m.Scanner.Shown.Where(s => m.Words.KindOf(s.Word) == ItemKind.Ammo).ToList();
            Assert.AreEqual(2, shown.Count);
            Assert.AreNotEqual(shown[0].Row, shown[1].Row, "시험의 전제: 두 화살이 다른 줄에 놓였다");

            var upper = shown.OrderBy(s => s.Row).First();
            Assert.AreEqual(upper.Word, m.PickArrow().Value.Word);
        }

        [Test]
        public void PickArrow_SameRow_TakesTheLeftOne()
        {
            var m = Model();
            m.AddWord("화살", (4, 1));
            m.AddWord("불화살", (0, 1));
            var shown = m.Scanner.Shown.Where(s => m.Words.KindOf(s.Word) == ItemKind.Ammo).ToList();
            Assert.AreEqual(2, shown.Count);
            if (shown[0].Row != shown[1].Row) Assert.Inconclusive("두 화살이 같은 줄에 놓이지 않았다(배치 규칙이 바뀜)");

            var left = shown.OrderBy(s => s.Col).First();
            Assert.AreEqual(left.Word, m.PickArrow().Value.Word);
        }

        [Test]
        public void PickArrow_IgnoresNonAmmoWords()
        {
            var m = Model();
            m.AddWord("검", (0, 0));
            m.AddWord("불", (0, 1));
            Assert.IsNull(m.PickArrow());
        }

        // ───── 효과 겹치기 (활의 접두어 + 화살의 접두어) ─────
        static WeaponModifier Fire(float chance = 30f) => WeaponModifier.From("불", new ItemStats { prefixKind = PrefixEffectKind.Element, elementChance = chance });
        static WeaponModifier Ice(float chance = 50f) => WeaponModifier.From("얼음", new ItemStats { prefixKind = PrefixEffectKind.Element, elementChance = chance });
        static WeaponModifier Plus(float amount) => WeaponModifier.From("강화", new ItemStats { prefixKind = PrefixEffectKind.Damage, damageMode = DamageMode.Add, damageAmount = amount });
        static WeaponModifier Times(float amount) => WeaponModifier.From("강력", new ItemStats { prefixKind = PrefixEffectKind.Damage, damageMode = DamageMode.Multiply, damageAmount = amount });

        [Test]
        public void ApplyDamage_ChainsInOrder_AndSkipsNullAndElementEffects()
        {
            Assert.AreEqual(3, WeaponModifier.ApplyDamage(3, (IEnumerable<WeaponModifier>)null));
            Assert.AreEqual(3, WeaponModifier.ApplyDamage(3, new WeaponModifier[] { null, Fire(), null }));   // 속성은 피해량을 바꾸지 않는다
            Assert.AreEqual(1, WeaponModifier.ApplyDamage(0, new WeaponModifier[0]));                           // 최소 1
            Assert.AreEqual(5, WeaponModifier.ApplyDamage(3, new[] { Plus(2f) }));
            Assert.AreEqual(8, WeaponModifier.ApplyDamage(3, new[] { Plus(2f), Times(1.5f) }));   // (3+2)×1.5 = 7.5 → 8
            Assert.AreEqual(7, WeaponModifier.ApplyDamage(3, new[] { Times(1.5f), Plus(2f) }));   // 3×1.5 = 4.5 → 5, 그다음 +2 = 7
        }

        [Test]
        public void ApplyDamage_OrderMatters()
        {
            int a = WeaponModifier.ApplyDamage(3, new[] { Plus(2f), Times(2f) });    // (3+2)×2 = 10
            int b = WeaponModifier.ApplyDamage(3, new[] { Times(2f), Plus(2f) });    // 3×2+2 = 8
            Assert.AreEqual(10, a);
            Assert.AreEqual(8, b);
        }

        [Test]
        public void RollElement_ReturnsTheFirstEffectThatLands_AndRollsEachOnce()
        {
            var rolls = new Queue<double>(new[] { 0.5, 0.4 });   // 불 30%에는 0.5(실패), 얼음 50%에는 0.4(성공)
            int calls = 0;
            var hit = WeaponModifier.RollElement(new[] { Fire(30f), Ice(50f) }, () => { calls++; return rolls.Dequeue(); });
            Assert.AreEqual("얼음", hit.Element);
            Assert.AreEqual(2, calls);

            var stopsEarly = new Queue<double>(new[] { 0.1 });
            calls = 0;
            hit = WeaponModifier.RollElement(new[] { Fire(30f), Ice(50f) }, () => { calls++; return stopsEarly.Dequeue(); });
            Assert.AreEqual("불", hit.Element);
            Assert.AreEqual(1, calls);   // 처음 효과가 묻으면 더 굴리지 않는다
        }

        [Test]
        public void RollElement_IgnoresNullAndDamageEffects_AndNoMatchGivesNull()
        {
            int calls = 0;
            Assert.IsNull(WeaponModifier.RollElement(new WeaponModifier[] { null, Plus(1f) }, () => { calls++; return 0.0; }));
            Assert.AreEqual(0, calls);   // 굴릴 것이 없으면 난수도 부르지 않는다
            Assert.IsNull(WeaponModifier.RollElement(null, () => 0.0));
            Assert.IsNull(WeaponModifier.RollElement(new[] { Fire() }, null));
            Assert.IsNull(WeaponModifier.RollElement(new[] { Fire(30f) }, () => 0.99));
        }

        // ───── 게임 데이터(에셋)의 탄 목록 ─────
        [Test]
        public void Database_PutsAmmoWordsInTheirOwnList_AndRoundTripsThroughTheFile()
        {
            var db = new ItemDatabaseData();
            var report = ItemDatabaseImporter.Import(File(), db, ImportMode.AddOnly);
            Assert.AreEqual(0, report.UnknownCategories.Count);

            Assert.AreEqual(1, db.ammos.Count);
            Assert.AreEqual("화살", db.ammos[0].word);
            Assert.IsTrue(db.ammos[0].acceptsPrefix);
            CollectionAssert.AreEqual(new[] { "불", "얼음" }, db.ammos[0].prefixes);
            Assert.AreEqual(WordKind.Ammo, db.Find("화살").Kind);
            Assert.AreEqual(CategoryType.Ammo, db.TypeOfCategory("탄"));

            // 게임이 쓰는 파일로 바꿔도 탄 종류가 이어진다(합성 단어 포함)
            var set = ItemWordSet.FromFile(db.ToFile(forGame: true), out _);
            Assert.AreEqual(ItemKind.Ammo, set.KindOf("화살"));
            Assert.AreEqual(ItemKind.Ammo, set.KindOf("불화살"));
            Assert.IsNull(set.StatsOf("화살"), "탄은 수치가 없다");
        }

        [Test]
        public void Database_AmmoListIsPartOfTheRulesEverywhere()
        {
            Assert.AreEqual(WordKind.Ammo, ItemDatabaseData.KindFor(CategoryType.Ammo, WordKind.Plain));
            Assert.IsInstanceOf<AmmoWordRecord>(ItemDatabaseData.Create(WordKind.Ammo));

            var db = new ItemDatabaseData();
            db.TryAddCategory("탄", CategoryType.Ammo, out _);
            Assert.IsTrue(db.TryAddWord("화살", "탄", out var rec, out _));
            Assert.IsInstanceOf<AmmoWordRecord>(rec);
            Assert.AreEqual(1, db.ammos.Count);
            Assert.AreEqual(1, db.WordCount);

            var clone = db.DeepClone();
            Assert.AreEqual(1, clone.ammos.Count);
            Assert.AreNotSame(db.ammos[0], clone.ammos[0]);

            Assert.IsTrue(db.Remove(rec));
            Assert.AreEqual(0, db.ammos.Count);
        }

        [Test]
        public void Database_ChangingACategoryToAmmo_ReclassifiesItsWords()
        {
            var db = new ItemDatabaseData();
            db.TryAddCategory("재료", CategoryType.Plain, out _);
            db.TryAddWord("화살", "재료", out _, out _);
            Assert.AreEqual(1, db.plains.Count);

            db.FindCategory("재료").type = CategoryType.Ammo;
            Assert.AreEqual(1, db.Reclassify());
            Assert.AreEqual(0, db.plains.Count);
            Assert.AreEqual(1, db.ammos.Count);
        }

        [Test]
        public void HasRangedWeapon_LooksAtOwnStatsOrTheDefault()
        {
            var db = new ItemDatabaseData();
            db.TryAddCategory("무기", CategoryType.Weapon, out _);
            db.TryAddWord("활", "무기", out var bow, out _);
            Assert.IsFalse(db.HasRangedWeapon());

            var w = (WeaponWordRecord)bow;
            w.useDefaults = false;
            w.stats.attackStyle = AttackStyle.Ranged;
            Assert.IsTrue(db.HasRangedWeapon());

            w.useDefaults = true;                                   // 기본값을 따르면 기본값 기준
            Assert.IsFalse(db.HasRangedWeapon());
            db.defaultWeapon.attackStyle = AttackStyle.Ranged;
            Assert.IsTrue(db.HasRangedWeapon());
        }

        [Test]
        public void Database_RangedStatsTravelThroughTheFile()
        {
            var db = new ItemDatabaseData();
            ItemDatabaseImporter.Import(File(), db, ImportMode.AddOnly);
            var bow = (WeaponWordRecord)db.Find("활");
            bow.useDefaults = false;
            bow.stats = new WeaponStats { damage = 2, attackStyle = AttackStyle.Ranged, windup = 0.3f };

            var back = new ItemDatabaseData();
            ItemDatabaseImporter.Import(db.ToFile(forGame: false), back, ImportMode.AddOnly);
            var roundTripped = (WeaponWordRecord)back.Find("활");
            Assert.IsFalse(roundTripped.useDefaults);
            Assert.AreEqual(AttackStyle.Ranged, roundTripped.stats.attackStyle);
            Assert.AreEqual(0.3f, roundTripped.stats.windup);

            var set = ItemWordSet.FromFile(db.ToFile(forGame: true), out _);
            Assert.AreEqual(AttackStyle.Ranged, set.StatsOf("활").attackStyle);
            Assert.AreEqual(AttackStyle.Melee, set.StatsOf("검").attackStyle);   // 기본값을 따르는 무기는 근접
        }

        // ───── 검증 ─────
        [Test]
        public void Validator_WarnsWhenThereIsABowButNoAmmoWord()
        {
            var file = File();
            file.items.RemoveAll(i => i.word == "화살");
            var db = new ItemDatabaseData();
            ItemDatabaseImporter.Import(file, db, ImportMode.AddOnly);

            Assert.IsFalse(ItemDatabaseValidator.Validate(db).Has("ranged-without-ammo"));   // 아직 활이 없다(모두 근접)

            var bow = (WeaponWordRecord)db.Find("활");
            bow.useDefaults = false;
            bow.stats.attackStyle = AttackStyle.Ranged;
            var issue = ItemDatabaseValidator.Validate(db).Of("ranged-without-ammo").Single();
            Assert.AreEqual(IssueLevel.Warning, issue.Level);

            db.TryAddWord("화살", "탄", out _, out _);   // 탄이 생기면 사라진다
            Assert.IsFalse(ItemDatabaseValidator.Validate(db).Has("ranged-without-ammo"));
        }

        [Test]
        public void Validator_ChecksRangedTimeRange()
        {
            var db = new ItemDatabaseData();
            ItemDatabaseImporter.Import(File(), db, ImportMode.AddOnly);
            var bow = (WeaponWordRecord)db.Find("활");
            bow.useDefaults = false;
            bow.stats.attackStyle = AttackStyle.Ranged;
            Assert.IsFalse(ItemDatabaseValidator.Validate(db).Has("stats-out-of-range"));

            bow.stats.windup = -1f;   // 시위 시간은 0 이상
            Assert.AreEqual("활", ItemDatabaseValidator.Validate(db).Of("stats-out-of-range").Single().Word);
        }
    }
}
