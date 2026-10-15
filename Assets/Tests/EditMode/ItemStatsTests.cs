using System.Collections.Generic;
using Hangeul.App;
using NUnit.Framework;
using UnityEngine;

namespace Hangeul.Tests
{
    public class ItemStatsTests
    {
        static ItemWordsFile File(params WordStatsEntry[] stats)
        {
            var file = new ItemWordsFile
            {
                schemaVersion = 1,
                categories = new List<string> { "무기", "음식", "속성" },
                items = new List<ItemWordEntry>
                {
                    new ItemWordEntry { word = "검", category = "무기" },
                    new ItemWordEntry { word = "화살", category = "무기", acceptsPrefix = true, prefixes = new List<string> { "불" } },
                    new ItemWordEntry { word = "사과", category = "음식" },
                    new ItemWordEntry { word = "불", category = "속성" },
                },
            };
            file.wordStats.AddRange(stats);
            return file;
        }

        static WordStatsEntry Entry(string word, ItemStats stats) => new WordStatsEntry { word = word, stats = stats };

        // ───── 기본값 / 범위 ─────
        [Test]
        public void Defaults_MatchTheCurrentPlayerMeleeSettings()
        {
            var d = new ItemStats();
            Assert.AreEqual(1, d.damage);
            Assert.AreEqual(4f, d.knockback);
            Assert.AreEqual(0.3f, d.stun);
            Assert.AreEqual(1.27f, d.reach);
            Assert.AreEqual(1.2f, d.width);
            Assert.AreEqual(0.08f, d.windup);
            Assert.AreEqual(0.12f, d.active);
            Assert.AreEqual(0.18f, d.recovery);
            Assert.AreEqual(2, d.heal);
        }

        [Test]
        public void Clamped_PullsBadValuesIntoARange_AndKeepsGoodOnes()
        {
            var c = new ItemStats { damage = 0, knockback = -3f, stun = 99f, reach = 0f, width = 1000f, windup = -1f, active = 0f, recovery = 5f, heal = -4 }.Clamped();
            Assert.AreEqual(1, c.damage);
            Assert.AreEqual(0f, c.knockback);
            Assert.AreEqual(10f, c.stun);
            Assert.AreEqual(0.01f, c.reach);
            Assert.AreEqual(30f, c.width);
            Assert.AreEqual(0f, c.windup);
            Assert.AreEqual(0.01f, c.active);   // 판정 시간 0은 아무도 못 맞히므로 막는다
            Assert.AreEqual(5f, c.recovery);
            Assert.AreEqual(1, c.heal);

            Assert.IsTrue(new ItemStats().SameAs(new ItemStats().Clamped()));   // 기본값은 이미 범위 안
        }

        [Test]
        public void Clamped_NaNDoesNotLeakThrough()
        {
            var c = new ItemStats { knockback = float.NaN, reach = float.NaN }.Clamped();
            Assert.IsFalse(float.IsNaN(c.knockback));
            Assert.IsFalse(float.IsNaN(c.reach));
            Assert.AreEqual(0.01f, c.reach);
        }

        [Test]
        public void SameAs_And_CopyFrom()
        {
            var a = new ItemStats { damage = 3, heal = 6, reach = 2f };
            var b = new ItemStats();
            Assert.IsFalse(a.SameAs(b));
            b.CopyFrom(a);
            Assert.IsTrue(a.SameAs(b));
            b.reach = 2.5f;
            Assert.IsFalse(a.SameAs(b));
            Assert.AreEqual(2f, a.reach);   // 복사는 서로 독립
            Assert.IsFalse(a.SameAs(null));
        }

        // ───── 파일에서 읽기 ─────
        [Test]
        public void StatsOf_IsNullForWordsWithoutCustomStats()
        {
            var set = ItemWordSet.FromFile(File(), out _);
            Assert.IsNull(set.StatsOf("검"));
            Assert.IsNull(set.StatsOf("없는단어"));
            Assert.IsNull(set.StatsOf(null));
        }

        [Test]
        public void StatsOf_ReturnsTheCustomStats_ClampedToTheRange()
        {
            var set = ItemWordSet.FromFile(File(
                Entry("검", new ItemStats { damage = 3, reach = 2f }),
                Entry("사과", new ItemStats { heal = 0 })), out _);

            Assert.AreEqual(3, set.StatsOf("검").damage);
            Assert.AreEqual(2f, set.StatsOf("검").reach);
            Assert.AreEqual(1, set.StatsOf("사과").heal);   // 0은 범위(1 이상)로 올려 읽는다
            Assert.IsNull(set.StatsOf("화살"));
        }

        [Test]
        public void StatsFor_UnknownWords_AreIgnored()
        {
            var set = ItemWordSet.FromFile(File(Entry("없는단어", new ItemStats { damage = 9 })), out _);
            Assert.IsNull(set.StatsOf("없는단어"));
        }

        [Test]
        public void DuplicateStatsEntries_FirstOneWins_AndBrokenEntriesAreSkipped()
        {
            var file = File(Entry("검", new ItemStats { damage = 2 }), Entry("검", new ItemStats { damage = 7 }), Entry("사과", null), null);
            var set = ItemWordSet.FromFile(file, out _);
            Assert.AreEqual(2, set.StatsOf("검").damage);
            Assert.IsNull(set.StatsOf("사과"));
        }

        [Test]
        public void CompoundWord_FollowsTheBaseWordStats()
        {
            var set = ItemWordSet.FromFile(File(Entry("화살", new ItemStats { damage = 4 })), out _);
            Assert.IsTrue(set.IsCompound("불화살"));
            Assert.AreEqual(4, set.StatsOf("불화살").damage);
        }

        [Test]
        public void CompoundWord_WithoutBaseStats_HasNone()
        {
            var set = ItemWordSet.FromFile(File(), out _);
            Assert.IsNull(set.StatsOf("불화살"));
        }

        [Test]
        public void OldFilesWithoutWordStats_StillLoad()
        {
            var file = File();
            file.wordStats = null;   // 옛 파일을 JsonUtility로 읽었을 때 목록이 비어 있거나 없는 경우
            var set = ItemWordSet.FromFile(file, out int skipped);
            Assert.IsNotNull(set);
            Assert.AreEqual(0, skipped);
            Assert.IsNull(set.StatsOf("검"));
        }

        [Test]
        public void Json_RoundTrip_KeepsOnlyTheCustomWords()
        {
            var file = File(Entry("검", new ItemStats { damage = 3, knockback = 6f, windup = 0.2f }));
            string json = JsonUtility.ToJson(file, true);
            var back = JsonUtility.FromJson<ItemWordsFile>(json);

            Assert.AreEqual(1, back.wordStats.Count);
            Assert.AreEqual("검", back.wordStats[0].word);
            var set = ItemWordSet.FromFile(back, out _);
            Assert.AreEqual(3, set.StatsOf("검").damage);
            Assert.AreEqual(6f, set.StatsOf("검").knockback, 1e-4f);
            Assert.AreEqual(0.2f, set.StatsOf("검").windup, 1e-4f);
            Assert.IsNull(set.StatsOf("사과"));
        }

        [Test]
        public void Json_WithPartialStats_FillsTheRestWithDefaults()
        {
            const string json = @"{ ""schemaVersion"": 1, ""categories"": [""무기""],
              ""items"": [ { ""word"": ""검"", ""category"": ""무기"" } ],
              ""wordStats"": [ { ""word"": ""검"", ""stats"": { ""damage"": 5 } } ] }";
            var set = ItemWordSet.FromFile(JsonUtility.FromJson<ItemWordsFile>(json), out _);
            Assert.AreEqual(5, set.StatsOf("검").damage);
            Assert.AreEqual(1.27f, set.StatsOf("검").reach, 1e-4f);   // 적지 않은 칸은 기본값
        }

        [Test]
        public void ExistingDatabaseFile_HasNoStats_AndStillLoads()
        {
            const string json = @"{ ""schemaVersion"": 1, ""categories"": [""무기""],
              ""items"": [ { ""word"": ""검"", ""category"": ""무기"", ""acceptsPrefix"": false, ""prefixes"": [] } ] }";
            var file = JsonUtility.FromJson<ItemWordsFile>(json);
            var set = ItemWordSet.FromFile(file, out _);
            Assert.IsTrue(set.Contains("검"));
            Assert.IsNull(set.StatsOf("검"));
        }
    }
}
