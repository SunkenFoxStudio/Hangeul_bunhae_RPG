using System.Collections.Generic;
using System.Linq;
using Hangeul.App;
using Hangeul.UI;
using NUnit.Framework;
using UnityEngine;

namespace Hangeul.Tests
{
    public class ItemDatabaseTests
    {
        // 시험용 스프라이트(아무 그림이나 한 장)
        static Sprite NewSprite() => Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);

        static ItemWordEntry Word(string word, string category, bool acceptsPrefix = false, params string[] prefixes)
            => new ItemWordEntry { word = word, category = category, acceptsPrefix = acceptsPrefix, prefixes = prefixes.ToList() };

        static WordStatsEntry Stats(string word, ItemStats stats) => new WordStatsEntry { word = word, stats = stats };

        // 옛 DB와 비슷한 JSON: 무기·음식·방어구·접두어·속성 + 일반 카테고리 + 새로 생긴 '물약'
        static ItemWordsFile SampleFile(params WordStatsEntry[] stats)
        {
            var file = new ItemWordsFile
            {
                schemaVersion = 1,
                categories = new List<string> { "추가한 단어", "무기", "방어구", "음식", "속성", "접두어", "물약" },
                items = new List<ItemWordEntry>
                {
                    Word("검", "무기", true, "화염", "강화"),
                    Word("활", "무기"),
                    Word("방패", "방어구"),
                    Word("사과", "음식"),
                    Word("화염", "속성"),
                    Word("강화", "접두어"),
                    Word("체력물약", "물약"),
                    Word("담배", "추가한 단어"),
                },
            };
            file.wordStats.AddRange(stats);
            return file;
        }

        static ItemDatabaseData Import(ItemWordsFile file, ImportMode mode = ImportMode.AddOnly)
        {
            var db = new ItemDatabaseData();
            ItemDatabaseImporter.Import(file, db, mode);
            return db;
        }

        // ───── 종류별 기본 칸 ─────
        [Test]
        public void TypedStats_Defaults_MatchTheFlatItemStats()
        {
            Assert.IsTrue(new WeaponStats().ToItemStats().SameAs(new ItemStats()));   // 무기 칸의 기본값 = ItemStats의 기본값
            Assert.AreEqual(new ItemStats().heal, new FoodStats().heal);
            Assert.IsTrue(new PrefixDamageEffect().ToItemStats().SameAs(new ItemStats()));   // 기본: 더하기 0 = 변화 없음
            Assert.AreEqual(new ItemStats().elementColor, new PrefixElementEffect().color);
        }

        [Test]
        public void TypedStats_RoundTripThroughItemStats()
        {
            var w = new WeaponStats { damage = 4, knockback = 6f, reach = 2f, active = 0.3f };
            Assert.IsTrue(w.SameAs(WeaponStats.From(w.ToItemStats())));

            var f = new FoodStats { heal = 6 };
            Assert.AreEqual(6, FoodStats.From(f.ToItemStats()).heal);

            var d = new PrefixDamageEffect { mode = DamageMode.Multiply, amount = 1.5f };
            Assert.IsTrue(d.SameAs(PrefixDamageEffect.From(d.ToItemStats())));

            var e = new PrefixElementEffect { chancePercent = 35f, color = "#5FD8FF" };
            Assert.IsTrue(e.SameAs(PrefixElementEffect.From(e.ToItemStats())));
        }

        // ───── 카테고리 종류 짐작 ─────
        [Test]
        public void InferType_KnowsTheExistingCategoryNames()
        {
            Assert.AreEqual(CategoryType.Weapon, ItemDatabaseImporter.InferType("무기", out var known)); Assert.IsTrue(known);
            Assert.AreEqual(CategoryType.Armor, ItemDatabaseImporter.InferType("방어구", out known)); Assert.IsTrue(known);
            Assert.AreEqual(CategoryType.Food, ItemDatabaseImporter.InferType("음식", out known)); Assert.IsTrue(known);
            Assert.AreEqual(CategoryType.Prefix, ItemDatabaseImporter.InferType("접두어", out known)); Assert.IsTrue(known);
            Assert.AreEqual(CategoryType.Prefix, ItemDatabaseImporter.InferType("속성", out known)); Assert.IsTrue(known);
            Assert.AreEqual(CategoryType.Plain, ItemDatabaseImporter.InferType("재료·잡화", out known)); Assert.IsTrue(known);   // 옛 일반 카테고리
            Assert.AreEqual(CategoryType.Plain, ItemDatabaseImporter.InferType("물약", out known)); Assert.IsFalse(known);       // 처음 보는 이름
        }

        // ───── 가져오기: 종류별 목록에 나눠 담기 ─────
        [Test]
        public void Import_SplitsWordsIntoTheListOfTheirCategoryType()
        {
            var db = Import(SampleFile());

            Assert.AreEqual(new[] { "검", "활" }, db.weapons.Select(w => w.word).ToArray());
            Assert.AreEqual(new[] { "사과" }, db.foods.Select(w => w.word).ToArray());
            Assert.AreEqual(new[] { "방패" }, db.armors.Select(w => w.word).ToArray());
            Assert.AreEqual(new[] { "화염" }, db.prefixElements.Select(w => w.word).ToArray());   // 속성 카테고리의 기본: 속성 묻히기
            Assert.AreEqual(new[] { "강화" }, db.prefixDamages.Select(w => w.word).ToArray());    // 접두어 카테고리의 기본: 대미지 더하기
            CollectionAssert.AreEquivalent(new[] { "체력물약", "담배" }, db.plains.Select(w => w.word).ToArray());   // 모르는 카테고리는 일반
            Assert.AreEqual(8, db.WordCount);
        }

        [Test]
        public void Import_KeepsCategoryOrder_AndInfersTheirTypes()
        {
            var db = Import(SampleFile());
            Assert.AreEqual(new[] { "추가한 단어", "무기", "방어구", "음식", "속성", "접두어", "물약" }, db.categories.Select(c => c.name).ToArray());
            Assert.AreEqual(CategoryType.Weapon, db.TypeOfCategory("무기"));
            Assert.AreEqual(CategoryType.Prefix, db.TypeOfCategory("속성"));
            Assert.AreEqual(CategoryType.Plain, db.TypeOfCategory("물약"));
        }

        [Test]
        public void Import_ReportsNewAndUnknownCategories()
        {
            var db = new ItemDatabaseData();
            var report = ItemDatabaseImporter.Import(SampleFile(), db, ImportMode.AddOnly);
            Assert.AreEqual(7, report.NewCategories.Count);
            CollectionAssert.AreEqual(new[] { "물약" }, report.UnknownCategories);   // 짐작하지 못한 것만(옛 일반 카테고리는 알림 없이 일반)
            Assert.AreEqual(8, report.Added);
        }

        [Test]
        public void Import_FileCategoryTypes_BeatNameGuessing()
        {
            var file = SampleFile();
            file.categoryTypes.Add(new CategoryTypeEntry { name = "물약", type = CategoryType.Food });
            var report = new ImportReport();
            var db = new ItemDatabaseData();
            report = ItemDatabaseImporter.Import(file, db, ImportMode.AddOnly);

            Assert.AreEqual(CategoryType.Food, db.TypeOfCategory("물약"));
            Assert.AreEqual(new[] { "사과", "체력물약" }, db.foods.Select(f => f.word).OrderBy(w => w).ToArray());
            Assert.IsEmpty(report.UnknownCategories);
        }

        [Test]
        public void Import_ExistingCategoryDefinitions_AreNeverChanged()
        {
            var db = new ItemDatabaseData();
            db.categories.Add(new CategoryDef { name = "무기", type = CategoryType.Plain });   // 인스펙터에서 일부러 바꿔 둔 종류
            ItemDatabaseImporter.Import(SampleFile(), db, ImportMode.Overwrite);
            Assert.AreEqual(CategoryType.Plain, db.TypeOfCategory("무기"));
            Assert.IsNull(db.weapons.FirstOrDefault());   // 그래서 무기 카테고리 단어도 일반 목록으로
            Assert.IsNotNull(db.Find("검") as PlainWordRecord);
        }

        // ───── 수치: 따로 정했는지 / 기본값을 따르는지 ─────
        [Test]
        public void Import_WordsWithoutStats_FollowTheDefaults()
        {
            var db = Import(SampleFile());
            Assert.IsTrue(((WeaponWordRecord)db.Find("검")).useDefaults);
            Assert.IsTrue(((FoodWordRecord)db.Find("사과")).useDefaults);
            Assert.IsTrue(((PrefixElementWordRecord)db.Find("화염")).useDefaults);
            Assert.IsTrue(((PrefixDamageWordRecord)db.Find("강화")).useDefaults);
        }

        [Test]
        public void Import_CustomStats_BecomeTheWordsOwnValues()
        {
            var db = Import(SampleFile(
                Stats("검", new ItemStats { damage = 5, reach = 2f }),
                Stats("사과", new ItemStats { heal = 6 }),
                Stats("화염", new ItemStats { prefixKind = PrefixEffectKind.Element, elementChance = 35f, elementColor = "#5fd8ff" }),
                Stats("강화", new ItemStats { prefixKind = PrefixEffectKind.Damage, damageMode = DamageMode.Multiply, damageAmount = 1.5f })));

            var sword = (WeaponWordRecord)db.Find("검");
            Assert.IsFalse(sword.useDefaults);
            Assert.AreEqual(5, sword.stats.damage);
            Assert.AreEqual(2f, sword.stats.reach);

            Assert.AreEqual(6, ((FoodWordRecord)db.Find("사과")).stats.heal);

            var fire = (PrefixElementWordRecord)db.Find("화염");
            Assert.IsFalse(fire.useDefaults);
            Assert.AreEqual(35f, fire.effect.chancePercent);
            Assert.AreEqual("#5FD8FF", fire.effect.color);   // 색은 바로잡혀 들어온다

            var strong = (PrefixDamageWordRecord)db.Find("강화");
            Assert.AreEqual(DamageMode.Multiply, strong.effect.mode);
            Assert.AreEqual(1.5f, strong.effect.amount);
        }

        [Test]
        public void Import_PrefixKindFromTheStats_BeatsTheCategoryDefault()
        {
            // 속성 카테고리의 단어를 대미지 더하기로 정해 둔 경우
            var db = Import(SampleFile(Stats("화염", new ItemStats { prefixKind = PrefixEffectKind.Damage, damageAmount = 2f })));
            Assert.IsNotNull(db.Find("화염") as PrefixDamageWordRecord);
        }

        [Test]
        public void Import_KeepsPrefixRules()
        {
            var sword = (WeaponWordRecord)Import(SampleFile()).Find("검");
            Assert.IsTrue(sword.acceptsPrefix);
            Assert.AreEqual(new[] { "화염", "강화" }, sword.prefixes.ToArray());
        }

        [Test]
        public void Import_SkipsDuplicatesAndBrokenEntries()
        {
            var file = SampleFile();
            file.items.Add(Word("검", "음식"));   // 중복: 처음 것만
            file.items.Add(null);
            file.items.Add(Word("", "무기"));
            var db = Import(file);
            Assert.AreEqual(8, db.WordCount);
            Assert.IsNotNull(db.Find("검") as WeaponWordRecord);
        }

        // ───── 가져오기: 새 단어만 / 덮어쓰기 ─────
        [Test]
        public void AddOnly_LeavesExistingWordsAlone_AndAddsNewOnes()
        {
            var db = Import(SampleFile());
            var sword = (WeaponWordRecord)db.Find("검");
            sword.useDefaults = false;
            sword.stats.damage = 9;       // 인스펙터에서 고친 값
            var sprite = NewSprite();
            sword.sprite = sprite;

            var file = SampleFile(Stats("검", new ItemStats { damage = 2 }));
            file.items.Add(Word("도끼", "무기"));
            var report = ItemDatabaseImporter.Import(file, db, ImportMode.AddOnly);

            Assert.AreEqual(9, ((WeaponWordRecord)db.Find("검")).stats.damage);   // 그대로
            Assert.AreSame(sprite, db.Find("검").sprite);
            Assert.IsNotNull(db.Find("도끼") as WeaponWordRecord);               // 새 단어는 추가
            CollectionAssert.AreEqual(new[] { "도끼" }, report.AddedWords);
            Assert.AreEqual(0, report.Updated);
        }

        [Test]
        public void Overwrite_UpdatesTheJsonOwnedFields_ButKeepsTheSprite()
        {
            var db = Import(SampleFile());
            var sprite = NewSprite();
            db.Find("검").sprite = sprite;
            ((WeaponWordRecord)db.Find("검")).stats.damage = 9;
            ((WeaponWordRecord)db.Find("검")).useDefaults = false;

            var file = SampleFile(Stats("검", new ItemStats { damage = 2 }));
            var report = ItemDatabaseImporter.Import(file, db, ImportMode.Overwrite);

            var sword = (WeaponWordRecord)db.Find("검");
            Assert.AreEqual(2, sword.stats.damage);              // JSON 값으로
            Assert.IsFalse(sword.useDefaults);
            Assert.AreSame(sprite, sword.sprite);                // 스프라이트는 그대로
            CollectionAssert.AreEqual(new[] { "검" }, report.UpdatedWords);
        }

        [Test]
        public void Overwrite_BackToNoStats_FollowsTheDefaultsAgain()
        {
            var db = Import(SampleFile(Stats("검", new ItemStats { damage = 5 })));
            Assert.IsFalse(((WeaponWordRecord)db.Find("검")).useDefaults);
            ItemDatabaseImporter.Import(SampleFile(), db, ImportMode.Overwrite);   // JSON에는 이제 수치가 없다
            Assert.IsTrue(((WeaponWordRecord)db.Find("검")).useDefaults);
        }

        [Test]
        public void Overwrite_DoesNotDeleteWordsMissingFromTheJson_AndCountsUnchanged()
        {
            var db = Import(SampleFile());
            db.Add(new PlainWordRecord { word = "에셋에만있는단어", category = "추가한 단어" });

            var report = ItemDatabaseImporter.Import(SampleFile(), db, ImportMode.Overwrite);

            Assert.IsNotNull(db.Find("에셋에만있는단어"));
            Assert.AreEqual(0, report.Updated);
            Assert.AreEqual(0, report.Added);
            Assert.AreEqual(8, report.Unchanged);
        }

        [Test]
        public void Overwrite_WhenTheCategoryTypeChanges_MovesTheWordToAnotherList_KeepingItsSprite()
        {
            var db = Import(SampleFile());
            var sprite = NewSprite();
            db.Find("활").sprite = sprite;

            var file = SampleFile();
            file.items.First(i => i.word == "활").category = "음식";   // 활이 음식 카테고리로 옮겨졌다
            var report = ItemDatabaseImporter.Import(file, db, ImportMode.Overwrite);

            var bow = db.Find("활");
            Assert.IsInstanceOf<FoodWordRecord>(bow);
            Assert.AreSame(sprite, bow.sprite);
            Assert.AreEqual(1, db.weapons.Count);   // 검만 남는다
            CollectionAssert.AreEqual(new[] { "활" }, report.UpdatedWords);
        }

        [Test]
        public void ImportingIntoAClone_DoesNotChangeTheOriginal()
        {
            var db = Import(SampleFile());
            var before = db.WordCount;
            var preview = ItemDatabaseImporter.Import(SampleFile(Stats("검", new ItemStats { damage = 7 })), db.DeepClone(), ImportMode.Overwrite);

            Assert.AreEqual(1, preview.Updated);
            Assert.AreEqual(before, db.WordCount);
            Assert.IsTrue(((WeaponWordRecord)db.Find("검")).useDefaults);   // 원본은 그대로
        }

        // ───── 파일로 바꾸기 (게임이 쓰는 모양 / JSON으로 내보내는 모양) ─────
        [Test]
        public void ToFile_ForGame_FillsTheDefaultsIntoWordsThatFollowThem()
        {
            var db = Import(SampleFile());
            db.defaultWeapon.damage = 3;
            db.defaultFood.heal = 4;

            var set = ItemWordSet.FromFile(db.ToFile(forGame: true), out _);
            Assert.AreEqual(3, set.StatsOf("검").damage);   // 기본값을 따르는 단어도 에셋의 기본값으로 동작한다
            Assert.AreEqual(3, set.StatsOf("활").damage);
            Assert.AreEqual(4, set.StatsOf("사과").heal);
        }

        [Test]
        public void ToFile_ForGame_ChangingTheDefaultChangesEveryWordThatFollowsIt()
        {
            var db = Import(SampleFile(Stats("활", new ItemStats { damage = 8 })));   // 활만 자기 값
            db.defaultWeapon.damage = 5;

            var set = ItemWordSet.FromFile(db.ToFile(forGame: true), out _);
            Assert.AreEqual(5, set.StatsOf("검").damage);
            Assert.AreEqual(8, set.StatsOf("활").damage);   // 자기 값은 기본값이 바뀌어도 그대로
        }

        [Test]
        public void ToFile_ForGame_TheGameBehavesLikeBefore_ForPrefixEffects()
        {
            var db = Import(SampleFile(
                Stats("화염", new ItemStats { prefixKind = PrefixEffectKind.Element, elementChance = 40f, elementColor = "#33AAFF" }),
                Stats("강화", new ItemStats { prefixKind = PrefixEffectKind.Damage, damageAmount = 2f })));
            var set = ItemWordSet.FromFile(db.ToFile(forGame: true), out _);

            Assert.IsTrue(set.TryGetCompound("화염검", out var prefix, out _));
            var fire = WeaponModifier.From(prefix, set.StatsOf(prefix));
            Assert.AreEqual("화염", fire.Element);
            Assert.AreEqual("#33AAFF", fire.ElementColor);

            set.TryGetCompound("강화검", out var p2, out _);
            Assert.AreEqual(5, WeaponModifier.From(p2, set.StatsOf(p2)).ApplyDamage(3));
        }

        [Test]
        public void ToFile_ForGame_PrefixWordsThatFollowTheDefaults_HaveNoEffect()
        {
            var set = ItemWordSet.FromFile(Import(SampleFile()).ToFile(forGame: true), out _);
            set.TryGetCompound("화염검", out var prefix, out _);
            Assert.IsNull(WeaponModifier.From(prefix, set.StatsOf(prefix)));   // 기본값은 확률 0% = 효과 없음
        }

        [Test]
        public void ToFile_ForExport_OnlyCustomStatsAreWritten()
        {
            var db = Import(SampleFile(Stats("검", new ItemStats { damage = 5 })));
            var file = db.ToFile(forGame: false);
            Assert.AreEqual(new[] { "검" }, file.wordStats.Select(w => w.word).ToArray());
            Assert.AreEqual(5, file.wordStats[0].stats.damage);
        }

        [Test]
        public void ToFile_WritesCategoryTypes_SoRenamingACategoryDoesNotChangeItsUse()
        {
            var db = Import(SampleFile());
            db.FindCategory("무기").name = "칼과 창";
            foreach (var w in db.weapons) w.category = "칼과 창";

            var set = ItemWordSet.FromFile(db.ToFile(forGame: true), out _);
            Assert.AreEqual(ItemKind.Weapon, set.KindOf("검"));   // 이름이 바뀌어도 종류(무기)가 쓰인다
        }

        [Test]
        public void ToFile_NewCategoryOfFoodType_MakesItsWordsEdible()
        {
            var db = Import(SampleFile());
            db.FindCategory("물약").type = CategoryType.Food;
            Assert.AreEqual(1, db.Reclassify());   // 체력물약이 음식 목록으로

            var set = ItemWordSet.FromFile(db.ToFile(forGame: true), out _);
            Assert.AreEqual(ItemKind.Food, set.KindOf("체력물약"));
            Assert.AreEqual(2, set.StatsOf("체력물약").heal);   // 음식 기본값(회복량 2)
        }

        [Test]
        public void ToFile_PrefixCategoryByType_MakesCompoundsEvenWithACustomName()
        {
            var db = Import(SampleFile());
            db.categories.Add(new CategoryDef { name = "마법 접두어", type = CategoryType.Prefix });
            db.Add(new PrefixDamageWordRecord { word = "마법", category = "마법 접두어" });
            ((NormalWordRecord)db.Find("활")).acceptsPrefix = true;
            ((NormalWordRecord)db.Find("활")).prefixes.Add("마법");

            var set = ItemWordSet.FromFile(db.ToFile(forGame: true), out _);
            Assert.IsTrue(set.IsCompound("마법활"));
            Assert.AreEqual(ItemKind.Weapon, set.KindOf("마법활"));
        }

        [Test]
        public void ExportThenImport_RoundTripsTheJsonOwnedData()
        {
            var db = Import(SampleFile(
                Stats("검", new ItemStats { damage = 5 }),
                Stats("화염", new ItemStats { prefixKind = PrefixEffectKind.Element, elementChance = 30f })));
            string json = JsonUtility.ToJson(db.ToFile(forGame: false), true);

            var again = new ItemDatabaseData();
            var report = ItemDatabaseImporter.Import(JsonUtility.FromJson<ItemWordsFile>(json), again, ImportMode.AddOnly);

            Assert.AreEqual(db.WordCount, again.WordCount);
            Assert.AreEqual(0, report.UnknownCategories.Count);   // 내보낸 파일에는 카테고리 종류가 적혀 있다 — 짐작할 일이 없다
            Assert.AreEqual(5, ((WeaponWordRecord)again.Find("검")).stats.damage);
            Assert.IsTrue(((WeaponWordRecord)again.Find("활")).useDefaults);
            Assert.AreEqual(30f, ((PrefixElementWordRecord)again.Find("화염")).effect.chancePercent);
            foreach (var r in db.AllWords()) Assert.AreEqual(r.Kind, again.Find(r.word).Kind, r.word);
        }

        // ───── 정리 / 복사 ─────
        [Test]
        public void Reclassify_MovesWordsToTheListOfTheirCategoryType_KeepingTheRest()
        {
            var db = Import(SampleFile());
            var sprite = NewSprite();
            var potion = db.Find("체력물약");
            potion.sprite = sprite;
            ((NormalWordRecord)potion).acceptsPrefix = true;
            ((NormalWordRecord)potion).prefixes.Add("강화");
            db.FindCategory("물약").type = CategoryType.Food;

            Assert.AreEqual(1, db.Reclassify());
            var moved = (FoodWordRecord)db.Find("체력물약");
            Assert.AreSame(sprite, moved.sprite);
            Assert.IsTrue(moved.acceptsPrefix);
            CollectionAssert.AreEqual(new[] { "강화" }, moved.prefixes);
            Assert.IsTrue(moved.useDefaults);
            Assert.AreEqual(0, db.Reclassify());   // 다시 해도 옮길 것이 없다
        }

        [Test]
        public void Reclassify_PrefixCategory_KeepsAnExistingPrefixKind()
        {
            var db = Import(SampleFile());
            db.FindCategory("추가한 단어").type = CategoryType.Prefix;
            Assert.AreEqual(1, db.Reclassify());                   // 담배 → 접두어(대미지 더하기로 시작)
            Assert.IsInstanceOf<PrefixDamageWordRecord>(db.Find("담배"));

            db.FindCategory("속성").type = CategoryType.Prefix;   // 이미 속성 묻히기인 '화염'은 그대로
            Assert.AreEqual(0, db.Reclassify());
            Assert.IsInstanceOf<PrefixElementWordRecord>(db.Find("화염"));
        }

        [Test]
        public void DeepClone_IsIndependentOfTheOriginal()
        {
            var db = Import(SampleFile());
            var sprite = NewSprite();
            db.Find("검").sprite = sprite;
            var clone = db.DeepClone();

            ((WeaponWordRecord)clone.Find("검")).stats.damage = 99;
            clone.Find("검").category = "바뀜";
            ((NormalWordRecord)clone.Find("검")).prefixes.Clear();
            clone.defaultWeapon.damage = 77;
            clone.categories[0].name = "바뀜";

            Assert.AreEqual(1, ((WeaponWordRecord)db.Find("검")).stats.damage);
            Assert.AreEqual("무기", db.Find("검").category);
            Assert.AreEqual(2, ((NormalWordRecord)db.Find("검")).prefixes.Count);
            Assert.AreEqual(1, db.defaultWeapon.damage);
            Assert.AreEqual("추가한 단어", db.categories[0].name);
            Assert.AreSame(sprite, clone.Find("검").sprite);   // 스프라이트는 같은 에셋
            Assert.AreEqual(db.WordCount, clone.WordCount);
        }

        [Test]
        public void Find_And_Remove_WorkAcrossLists()
        {
            var db = Import(SampleFile());
            Assert.IsNull(db.Find("없는단어"));
            Assert.IsNull(db.Find(null));
            var apple = db.Find("사과");
            Assert.IsTrue(db.Remove(apple));
            Assert.IsNull(db.Find("사과"));
            Assert.AreEqual(7, db.WordCount);
        }
    }
}
