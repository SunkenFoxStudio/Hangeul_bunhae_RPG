using System.Collections.Generic;
using System.Linq;
using Hangeul.App;
using Hangeul.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Hangeul.Tests
{
    public class ItemWordSetTests
    {
        [Test]
        public void FromFile_FiltersInvalidAndDuplicates()
        {
            var file = new ItemWordsFile { schemaVersion = 1 };
            foreach (var w in new[] { "검", "방패", "검", "abc", "ㄱ", "", null, "가a" })
                file.items.Add(new ItemWordEntry { word = w });
            file.items.Add(null);

            var set = ItemWordSet.FromFile(file, out int skipped);
            Assert.AreEqual(2, set.Count);
            Assert.IsTrue(set.Contains("검") && set.Contains("방패"));
            Assert.AreEqual(7, skipped);
            Assert.AreEqual(2, set.MaxLength);
        }

        [Test]
        public void FromFile_RejectsWrongVersion() => Assert.IsNull(ItemWordSet.FromFile(new ItemWordsFile { schemaVersion = 2 }, out _));

        [Test]
        public void ParsesHtmlExportFormat()
        {
            const string json = @"{ ""schemaVersion"": 1, ""savedAt"": ""x"", ""categories"": [""무기""],
              ""items"": [ { ""word"": ""검"", ""category"": ""무기"",
                ""syllables"": [ { ""letter"": ""검"", ""cho"": ""ㄱ"", ""jung"": ""ㅓ"", ""jong"": ""ㅁ"" } ] } ] }";
            var set = ItemWordSet.FromFile(JsonUtility.FromJson<ItemWordsFile>(json), out _);
            Assert.IsTrue(set.Contains("검"));
        }
    }

    public class MatchScannerTests
    {
        static ItemWordSet Words(params string[] w)
        {
            var s = new ItemWordSet();
            foreach (var x in w) s.Add(x);
            return s;
        }

        static (InventoryModel m, List<WordMatch> done) Make(int cols, int rows, params string[] words)
        {
            var m = new InventoryModel(cols, rows);
            m.SetWords(Words(words));
            var done = new List<WordMatch>();
            m.WordCompleted += done.Add;
            return (m, done);
        }

        // 지정한 칸에 글자 카드 한 장을 놓는다 (AddWord는 notify 없이 스캔한다)
        static Card Put(InventoryModel m, char letter, int col, int row)
        {
            m.AddWord(letter.ToString(), (col, row));
            return m.Grid.Cards.First(c => c.Letter == letter);
        }

        [Test]
        public void AddWord_DoesNotNotify_ForAlreadyCompleteWord()
        {
            var (m, done) = Make(10, 5, "가방");
            m.AddWord("가방");
            m.Scan(notify: true);
            Assert.IsEmpty(done);
            Assert.AreEqual("가방", m.Scanner.Shown.Single().Word);   // 완성돼 있기는 하다
        }

        [Test]
        public void Completing_ByMove_NotifiesOnce()
        {
            var (m, done) = Make(10, 5, "가방");
            Put(m, '가', 0, 0);
            var bang = Put(m, '방', 3, 0);
            m.Scan(true);
            Assert.IsEmpty(done);

            m.Move(bang, 1, 0);
            m.Scan(true);
            Assert.AreEqual(1, done.Count);
            Assert.AreEqual(("가방", 0, 0, 2), (done[0].Word, done[0].Row, done[0].Col, done[0].Length));

            m.Scan(true);                                   // 다시 스캔해도 중복 알림 없음
            Assert.AreEqual(1, done.Count);
        }

        [Test]
        public void ReverseOrder_AndVertical_AreNotWords()
        {
            var (m, done) = Make(10, 5, "가방");
            Put(m, '방', 0, 0);
            Put(m, '가', 1, 0);                            // 순서가 반대
            m.Scan(true);
            Assert.IsEmpty(done);

            var (m2, done2) = Make(10, 5, "가방");
            Put(m2, '가', 0, 0);
            Put(m2, '방', 0, 1);                           // 세로
            m2.Scan(true);
            Assert.IsEmpty(done2);
        }

        [Test]
        public void OnlyLongestOverlappingMatch_IsShown_AndHiddenOnesDoNotRenotify()
        {
            var (m, done) = Make(10, 5, "술병", "술", "병");
            Put(m, '술', 0, 0);
            var byung = Put(m, '병', 4, 0);
            m.Scan(true);
            Assert.AreEqual(2, m.Scanner.Shown.Count);       // 술, 병 따로
            Assert.IsEmpty(done);                            // 목록 설정 직후/AddWord 직후엔 알리지 않음

            m.Move(byung, 1, 0);                             // 붙이면 술병만 보인다
            m.Scan(true);
            Assert.AreEqual("술병", m.Scanner.Shown.Single().Word);
            Assert.AreEqual(new[] { "술병" }, done.Select(d => d.Word).ToArray());   // 숨겨진 술/병은 다시 알리지 않는다
        }

        [Test]
        public void IncompleteCard_BreaksTheRun()
        {
            var (m, done) = Make(10, 5, "가방");
            Put(m, '가', 0, 0);
            var bang = Put(m, '방', 1, 0);
            m.Scan(true);
            done.Clear();

            m.Detach(bang, Slot.Cho, PullDir.D);             // 초성을 떼면 방 카드가 미완성(고스트)이 된다
            m.Scan(true);
            Assert.IsEmpty(m.Scanner.Shown);
        }

        [Test]
        public void NoWords_NothingHappens()
        {
            var m = new InventoryModel(5, 5);
            m.AddWord("가방");
            Assert.IsEmpty(m.Scan(true));
            Assert.IsEmpty(m.Scanner.Shown);
        }

        [Test]
        public void Load_And_Resize_DoNotNotify()
        {
            var (m, done) = Make(10, 5, "가방");
            m.AddWord("가방");
            var data = m.ToSave();

            var (m2, done2) = Make(10, 5, "가방");
            m2.Load(data);
            m2.Scan(true);
            Assert.IsEmpty(done2);

            m.Resize(8, 4);
            m.Scan(true);
            Assert.IsEmpty(done);
        }

        [Test]
        public void Discard_ThenRebuild_NotifiesAgain()
        {
            var (m, done) = Make(10, 5, "가방");
            m.AddWord("가방");
            var bang = m.Grid.Cards.First(c => c.Letter == '방');
            m.Discard(bang);                                 // 쓰레기통으로 지움
            m.Scan(true);
            Assert.IsEmpty(m.Scanner.Shown);

            Put(m, '방', 5, 3);
            m.Scan(true);
            var moved = m.Grid.Cards.First(c => c.Letter == '방');
            m.Move(moved, m.Grid.Cards.First(c => c.Letter == '가').Col + 1, m.Grid.Cards.First(c => c.Letter == '가').Row);
            m.Scan(true);
            Assert.AreEqual(1, done.Count);
        }
    }
}
