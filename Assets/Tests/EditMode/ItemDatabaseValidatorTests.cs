using System.Collections.Generic;
using System.Linq;
using Hangeul.App;
using Hangeul.UI;
using NUnit.Framework;
using UnityEngine;

namespace Hangeul.Tests
{
    public class ItemDatabaseValidatorTests
    {
        static ItemWordEntry Word(string word, string category, bool acceptsPrefix = false, params string[] prefixes)
            => new ItemWordEntry { word = word, category = category, acceptsPrefix = acceptsPrefix, prefixes = prefixes.ToList() };

        // 문제가 없는 데이터: 무기(접두어 가능) + 속성 + 접두어 + 음식 + 일반
        static ItemDatabaseData Clean()
        {
            var file = new ItemWordsFile
            {
                schemaVersion = 1,
                categories = new List<string> { "무기", "속성", "접두어", "음식", "추가한 단어" },
                items = new List<ItemWordEntry>
                {
                    Word("검", "무기", true, "화염", "강화"),
                    Word("화염", "속성"),
                    Word("강화", "접두어"),
                    Word("사과", "음식"),
                    Word("담배", "추가한 단어"),
                },
            };
            var db = new ItemDatabaseData();
            ItemDatabaseImporter.Import(file, db, ImportMode.AddOnly);
            return db;
        }

        static ValidationReport Run(ItemDatabaseData db) => ItemDatabaseValidator.Validate(db);

        [Test]
        public void CleanData_HasNoErrorsOrWarnings()
        {
            var report = Run(Clean());
            Assert.AreEqual(0, report.Count(IssueLevel.Error), string.Join(" / ", report.Issues.Select(i => i.Message)));
            Assert.AreEqual(0, report.Count(IssueLevel.Warning), string.Join(" / ", report.Issues.Select(i => i.Message)));
            Assert.IsFalse(report.HasErrors);
        }

        [Test]
        public void NullDatabase_GivesAnEmptyReport() => Assert.AreEqual(0, Run(null).Issues.Count);

        // ───── 단어 ─────
        [Test]
        public void DuplicateWords_AreErrors_EvenAcrossLists()
        {
            var db = Clean();
            db.armors.Add(new ArmorWordRecord { word = "검", category = "무기" });   // 무기 목록의 '검'과 겹친다
            var issue = Run(db).Of("duplicate-word").Single();
            Assert.AreEqual(IssueLevel.Error, issue.Level);
            Assert.AreEqual("검", issue.Word);
        }

        [Test]
        public void EmptyAndInvalidWords_AreErrors()
        {
            var db = Clean();
            db.plains.Add(new PlainWordRecord { word = "", category = "추가한 단어" });
            db.plains.Add(new PlainWordRecord { word = "abc", category = "추가한 단어" });
            var report = Run(db);
            Assert.IsTrue(report.Has("empty-word"));
            Assert.AreEqual("abc", report.Of("invalid-word").Single().Word);
            Assert.IsTrue(report.HasErrors);
        }

        [Test]
        public void BannedSyllable_IsAWarning()
        {
            var db = Clean();
            db.plains.Add(new PlainWordRecord { word = "됬", category = "추가한 단어" });   // 됬 ✗ 됐 ○
            var issue = Run(db).Of("banned-syllable").Single();
            Assert.AreEqual(IssueLevel.Warning, issue.Level);
            Assert.AreEqual("됬", issue.Word);
        }

        // ───── 카테고리 ─────
        [Test]
        public void UnknownCategory_IsAnError()
        {
            var db = Clean();
            db.plains.Add(new PlainWordRecord { word = "고아", category = "없는카테고리" });
            var issue = Run(db).Of("unknown-category").Single();
            Assert.AreEqual(IssueLevel.Error, issue.Level);
            Assert.AreEqual("고아", issue.Word);
        }

        [Test]
        public void DuplicateAndEmptyCategoryNames_AreErrors()
        {
            var db = Clean();
            db.categories.Add(new CategoryDef { name = "무기", type = CategoryType.Food });
            db.categories.Add(new CategoryDef { name = "  ", type = CategoryType.Plain });
            var report = Run(db);
            Assert.IsTrue(report.Has("duplicate-category"));
            Assert.IsTrue(report.Has("empty-category-name"));
        }

        [Test]
        public void CategoryWithoutWords_IsInfo()
        {
            var db = Clean();
            db.categories.Add(new CategoryDef { name = "빈카테고리", type = CategoryType.Plain });
            var issue = Run(db).Of("empty-category").Single();
            Assert.AreEqual(IssueLevel.Info, issue.Level);
            StringAssert.Contains("빈카테고리", issue.Message);
        }

        [Test]
        public void WordInTheWrongList_IsAWarning_AndGoesAwayAfterReclassify()
        {
            var db = Clean();
            db.FindCategory("음식").type = CategoryType.Weapon;   // 종류만 바꾸고 단어는 아직 음식 목록에
            var issue = Run(db).Of("wrong-list").Single();
            Assert.AreEqual(IssueLevel.Warning, issue.Level);
            Assert.AreEqual("사과", issue.Word);

            db.Reclassify();
            Assert.IsFalse(Run(db).Has("wrong-list"));
        }

        [Test]
        public void WrongListIsNotReportedTwice_WhenTheCategoryIsUnknown()
        {
            var db = Clean();
            db.weapons.Add(new WeaponWordRecord { word = "고아검", category = "없는카테고리" });
            var report = Run(db);
            Assert.IsTrue(report.Has("unknown-category"));
            Assert.IsFalse(report.Of("wrong-list").Any(i => i.Word == "고아검"));
        }

        // ───── 접두어 규칙 ─────
        [Test]
        public void PrefixThatIsNotAPrefixWord_IsAnError()
        {
            var db = Clean();
            ((NormalWordRecord)db.Find("검")).prefixes.Add("사과");   // 사과는 접두어 단어가 아니다
            var issue = Run(db).Of("missing-prefix-word").Single();
            Assert.AreEqual(IssueLevel.Error, issue.Level);
            Assert.AreEqual("검", issue.Word);
            StringAssert.Contains("사과", issue.Message);
        }

        [Test]
        public void PrefixRules_EmptyOffAndDuplicates()
        {
            var db = Clean();
            db.plains.Add(new PlainWordRecord { word = "물", category = "추가한 단어", acceptsPrefix = true });                         // 가능인데 목록이 없다
            db.plains.Add(new PlainWordRecord { word = "돌", category = "추가한 단어", acceptsPrefix = false, prefixes = new List<string> { "화염" } });   // 꺼져 있는데 목록이 있다
            ((NormalWordRecord)db.Find("검")).prefixes.Add("화염");                                                                         // 같은 접두어가 두 번

            var report = Run(db);
            Assert.AreEqual("물", report.Of("prefix-rules-empty").Single().Word);
            Assert.AreEqual(IssueLevel.Info, report.Of("prefix-rules-off").Single().Level);
            Assert.AreEqual("검", report.Of("prefix-duplicate").Single().Word);
        }

        [Test]
        public void CompoundThatAlreadyExistsAsAWord_IsAWarning()
        {
            var db = Clean();
            db.plains.Add(new PlainWordRecord { word = "화염검", category = "추가한 단어" });   // 옛날에 직접 만들어 둔 합성 단어
            var issue = Run(db).Of("compound-exists").Single();
            Assert.AreEqual(IssueLevel.Warning, issue.Level);
            StringAssert.Contains("화염검", issue.Message);
        }

        // ───── 수치 / 색 ─────
        [Test]
        public void OutOfRangeOwnStats_AreAWarning_ButDefaultFollowingWordsAreNot()
        {
            var db = Clean();
            var sword = (WeaponWordRecord)db.Find("검");
            Assert.IsFalse(Run(db).Has("stats-out-of-range"));   // 기본값을 따르는 동안은 단어의 수치를 보지 않는다

            sword.useDefaults = false;
            sword.stats.damage = 0;   // 피해량은 1 이상
            var issue = Run(db).Of("stats-out-of-range").Single();
            Assert.AreEqual("검", issue.Word);
        }

        [Test]
        public void OutOfRangeDefaults_AreAWarning_WithoutAWord()
        {
            var db = Clean();
            db.defaultWeapon.reach = 0f;
            db.defaultPrefixElement.chancePercent = 250f;
            var issues = Run(db).Of("stats-out-of-range").ToList();
            Assert.AreEqual(2, issues.Count);
            Assert.IsTrue(issues.All(i => i.Word == null));
        }

        [Test]
        public void BadElementColor_IsAWarning_ButLowercaseAndShortFormsAreFine()
        {
            var db = Clean();
            var fire = (PrefixElementWordRecord)db.Find("화염");
            fire.useDefaults = false;

            fire.effect.color = "ff9e33";   // # 없는 소문자도 바로잡혀 쓰인다
            Assert.IsFalse(Run(db).Has("bad-color"));
            Assert.IsFalse(Run(db).Has("stats-out-of-range"));
            fire.effect.color = "#F80";
            Assert.IsFalse(Run(db).Has("bad-color"));

            fire.effect.color = "주황";
            var issue = Run(db).Of("bad-color").Single();
            Assert.AreEqual("화염", issue.Word);
            Assert.AreEqual(IssueLevel.Warning, issue.Level);
        }

        [Test]
        public void MultiplyBelowTheMinimum_IsOutOfRange()
        {
            var db = Clean();
            var strong = (PrefixDamageWordRecord)db.Find("강화");
            strong.useDefaults = false;
            strong.effect.mode = DamageMode.Multiply;
            strong.effect.amount = 0f;   // 곱하기는 0.1배부터
            Assert.AreEqual("강화", Run(db).Of("stats-out-of-range").Single().Word);
        }

        // ───── 스프라이트 ─────
        [Test]
        public void MissingSprites_AreOneInfoLine_WithExamples()
        {
            var db = Clean();
            var info = Run(db).Of("no-sprite").Single();
            Assert.AreEqual(IssueLevel.Info, info.Level);
            StringAssert.Contains("5개", info.Message);
            Assert.IsNull(info.Word);

            for (int i = 0; i < 10; i++) db.plains.Add(new PlainWordRecord { word = "단어" + "가나다라마바사아자차"[i], category = "추가한 단어" });
            StringAssert.Contains("15개", Run(db).Of("no-sprite").Single().Message);
            StringAssert.Contains("…", Run(db).Of("no-sprite").Single().Message);   // 예시는 몇 개만
        }

        [Test]
        public void NullEntriesInTheLists_AreAWarning()
        {
            var db = Clean();
            db.weapons.Add(null);
            db.categories.Add(null);
            Assert.AreEqual(2, Run(db).Of("empty-entry").Count());
        }

        // ───── 정렬 ─────
        [Test]
        public void Issues_AreOrderedErrorsFirst_ThenWarnings_ThenInfo()
        {
            var db = Clean();
            db.plains.Add(new PlainWordRecord { word = "고아", category = "없는카테고리" });   // 오류
            db.plains.Add(new PlainWordRecord { word = "됬", category = "추가한 단어" });         // 경고
            var levels = Run(db).Issues.Select(i => i.Level).ToList();
            CollectionAssert.AreEqual(levels.OrderBy(l => l).ToList(), levels);
            Assert.AreEqual(IssueLevel.Error, levels.First());
            Assert.AreEqual(IssueLevel.Info, levels.Last());
        }
    }
}
