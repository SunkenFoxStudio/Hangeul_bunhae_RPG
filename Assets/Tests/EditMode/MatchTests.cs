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

        static ItemWordsFile PrefixFile()
        {
            var file = new ItemWordsFile { schemaVersion = 1 };
            file.items.Add(new ItemWordEntry { word = "불", category = "접두어" });
            file.items.Add(new ItemWordEntry { word = "얼음", category = "접두어" });
            file.items.Add(new ItemWordEntry { word = "화살", category = "무기", acceptsPrefix = true, prefixes = new List<string> { "불", "얼음" } });
            file.items.Add(new ItemWordEntry { word = "사과", category = "음식" });   // 접두어 불가
            return file;
        }

        [Test]
        public void Prefixes_BecomeCompoundWords()
        {
            var set = ItemWordSet.FromFile(PrefixFile(), out int skipped);
            Assert.IsTrue(set.Contains("불화살") && set.Contains("얼음화살"));
            Assert.IsTrue(set.IsCompound("불화살"));
            Assert.IsFalse(set.IsCompound("화살"));
            Assert.IsFalse(set.Contains("불사과"));
            Assert.AreEqual(0, skipped);
            Assert.AreEqual(4, set.MaxLength);   // 얼음화살
        }

        [Test]
        public void Prefixes_IgnoredWhenNotAllowedOrNotAPrefixWord()
        {
            var file = PrefixFile();
            file.items[2].acceptsPrefix = false;                                   // 체크를 끄면 목록이 남아 있어도 무시한다
            Assert.IsFalse(ItemWordSet.FromFile(file, out _).Contains("불화살"));

            file.items[2].acceptsPrefix = true;
            file.items[2].prefixes.Add("사과");                                    // 접두어 카테고리의 단어가 아니면 무시한다
            Assert.IsFalse(ItemWordSet.FromFile(file, out _).Contains("사과화살"));
        }

        [Test]
        public void AttributeCategory_IsAlsoAPrefixSource()
        {
            var file = new ItemWordsFile { schemaVersion = 1 };
            file.items.Add(new ItemWordEntry { word = "불", category = "속성" });
            file.items.Add(new ItemWordEntry { word = "레어", category = "접두어" });
            file.items.Add(new ItemWordEntry { word = "화살", category = "무기", acceptsPrefix = true, prefixes = new List<string> { "불", "레어" } });
            // 속성/접두어 단어 자신에게 붙은 규칙은 무시한다
            file.items.Add(new ItemWordEntry { word = "화염", category = "속성", acceptsPrefix = true, prefixes = new List<string> { "불" } });

            var set = ItemWordSet.FromFile(file, out _);
            Assert.IsTrue(set.Contains("불화살") && set.Contains("레어화살"));
            Assert.IsFalse(set.Contains("불화염"));
            Assert.IsTrue(ItemWordSet.IsPrefixCategory("속성") && ItemWordSet.IsPrefixCategory("접두어") && !ItemWordSet.IsPrefixCategory("무기"));
        }

        [Test]
        public void ExplicitWord_WinsOverCompound()
        {
            var file = PrefixFile();
            file.items.Add(new ItemWordEntry { word = "불화살", category = "무기" });
            var set = ItemWordSet.FromFile(file, out int skipped);
            Assert.IsTrue(set.Contains("불화살"));
            Assert.IsFalse(set.IsCompound("불화살"));
            Assert.AreEqual(0, skipped);
        }

        [Test]
        public void CompoundWord_IsCompletedAsOneItem()
        {
            var m = new InventoryModel(6, 3);
            m.SetWords(ItemWordSet.FromFile(PrefixFile(), out _));
            m.AddWord("불화살");
            Assert.AreEqual(new[] { "불화살" }, m.Scanner.Shown.Select(x => x.Word).ToArray());   // 불·화살로 쪼개지지 않고 하나로 보인다
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

        // ───── 접두어 위 배치 ─────
        static ItemWordSet PrefixSet()
        {
            var file = new ItemWordsFile { schemaVersion = 1 };
            file.items.Add(new ItemWordEntry { word = "불", category = "속성" });
            file.items.Add(new ItemWordEntry { word = "얼음", category = "접두어" });
            file.items.Add(new ItemWordEntry { word = "화살", category = "무기", acceptsPrefix = true, prefixes = new List<string> { "불", "얼음" } });
            file.items.Add(new ItemWordEntry { word = "사과", category = "음식" });          // 접두어 불가
            file.items.Add(new ItemWordEntry { word = "불꽃", category = "무기" });
            return ItemWordSet.FromFile(file, out _);
        }

        static (InventoryModel m, List<WordMatch> done) MakePrefix(int cols, int rows)
        {
            var m = new InventoryModel(cols, rows);
            m.SetWords(PrefixSet());
            var done = new List<WordMatch>();
            m.WordCompleted += done.Add;
            return (m, done);
        }

        [Test]
        public void PrefixAbove_LeftAligned_IsOneWord()
        {
            var (m, _) = MakePrefix(6, 4);   // 알림은 StackedWord_NotifiesOnce...에서 본다(카드를 놓는 AddWord는 알리지 않고 스캔한다)
            Put(m, '불', 0, 0);
            Put(m, '화', 0, 1);
            Put(m, '살', 1, 1);
            m.Scan(true);

            var w = m.Scanner.Shown.Single();
            Assert.AreEqual("불화살", w.Word);
            Assert.IsTrue(w.IsStacked);
            Assert.AreEqual((1, 0, 2, 1), (w.Row, w.Col, w.Length, w.PrefixLength));
            CollectionAssert.AreEqual(new[] { (0, 0), (0, 1), (1, 1) }, w.Cells().ToArray());     // 읽는 순서: 접두어 → 본체
        }

        [Test]
        public void PrefixAbove_MultiLetter_CanExtendPastTheBase()
        {
            var (m, _) = MakePrefix(6, 4);
            Put(m, '얼', 0, 0);
            Put(m, '음', 1, 0);
            Put(m, '화', 0, 1);
            Put(m, '살', 1, 1);
            m.Scan(true);
            Assert.AreEqual("얼음화살", m.Scanner.Shown.Single().Word);
        }

        [Test]
        public void PrefixAbove_NotLeftAligned_IsNotJoined()
        {
            var (m, _) = MakePrefix(6, 4);
            Put(m, '불', 1, 0);            // 화가 아니라 살 위
            Put(m, '화', 0, 1);
            Put(m, '살', 1, 1);
            m.Scan(true);
            CollectionAssert.AreEquivalent(new[] { "불", "화살" }, m.Scanner.Shown.Select(x => x.Word).ToArray());
            Assert.IsFalse(m.Scanner.Shown.Any(x => x.IsStacked));
        }

        [Test]
        public void PrefixAbove_OnlyForWordsThatAcceptIt()
        {
            var (m, _) = MakePrefix(6, 4);
            Put(m, '불', 0, 0);
            Put(m, '사', 0, 1);
            Put(m, '과', 1, 1);
            m.Scan(true);
            CollectionAssert.AreEquivalent(new[] { "불", "사과" }, m.Scanner.Shown.Select(x => x.Word).ToArray());
        }

        [Test]
        public void Inline_BeatsPrefixAbove()
        {
            var (m, _) = MakePrefix(6, 4);
            Put(m, '불', 0, 1); Put(m, '화', 1, 1); Put(m, '살', 2, 1);   // 일렬: 불화살
            Put(m, '얼', 1, 0); Put(m, '음', 2, 0);                       // 위 배치라면 얼음화살(더 긴 단어)
            m.Scan(true);

            var shown = m.Scanner.Shown;
            Assert.IsTrue(shown.Any(x => x.Word == "불화살" && !x.IsStacked));
            Assert.IsFalse(shown.Any(x => x.Word == "얼음화살"));
            Assert.IsTrue(shown.Any(x => x.Word == "얼음"));              // 얼음은 혼자 단어로 남는다
        }

        [Test]
        public void PrefixAbove_RejectedWhenItsCardsBelongToALongerWord()
        {
            var (m, _) = MakePrefix(6, 4);
            Put(m, '불', 0, 0); Put(m, '꽃', 1, 0);                       // 불꽃(일렬)
            Put(m, '화', 0, 1); Put(m, '살', 1, 1);
            m.Scan(true);
            CollectionAssert.AreEquivalent(new[] { "불꽃", "화살" }, m.Scanner.Shown.Select(x => x.Word).ToArray());
        }

        [Test]
        public void StackedWord_NotifiesOnce_AndAlsoWhenFormedByMoving()
        {
            var (m, done) = MakePrefix(6, 4);
            var bul = Put(m, '불', 4, 3);
            Put(m, '화', 0, 1);
            Put(m, '살', 1, 1);
            m.Scan(true);
            done.Clear();

            m.Move(bul, 0, 0);
            m.Scan(true);
            Assert.AreEqual(new[] { "불화살" }, done.Select(x => x.Word).ToArray());
            m.Scan(true);
            Assert.AreEqual(1, done.Count);
        }

        [Test]
        public void ShiftGroup_KeepsTheShape_AndRejectsOverlapOrOutOfGrid()
        {
            var (m, _) = MakePrefix(6, 4);
            var a = Put(m, '불', 0, 0);
            var b = Put(m, '화', 0, 1);
            var c = Put(m, '살', 1, 1);
            var group = new List<Card> { a, b, c };

            Assert.IsTrue(m.ShiftGroup(group, 2, 1));
            Assert.AreEqual(((2, 1), (2, 2), (3, 2)), ((a.Col, a.Row), (b.Col, b.Row), (c.Col, c.Row)));

            Assert.IsFalse(m.CanShiftGroup(group, 3, 0));      // 오른쪽 끝을 넘음
            Assert.IsFalse(m.CanShiftGroup(group, 0, -2));     // 윗줄이 격자 밖
            var blocker = Put(m, '사', 5, 3);
            Assert.IsFalse(m.CanShiftGroup(group, 2, 1));      // 살이 (5,3)에 닿음
            Assert.IsTrue(m.CanShiftGroup(group, 1, 0));
        }

        [Test]
        public void StackedWordCards_AreProtectedFromShake()
        {
            var (m, _) = MakePrefix(6, 4);
            Put(m, '불', 0, 0); Put(m, '화', 0, 1); Put(m, '살', 1, 1);
            m.PlaceJamo('ㄴ', 4, 3);                                  // 화 + ㄴ = 환이 되지만 완성된 단어라 막혀야 한다
            var r = m.ShakeBag(new System.Random(1), mergeChance: 1f, rotateChance: 0f);
            Assert.AreEqual(0, r.Merged);
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
