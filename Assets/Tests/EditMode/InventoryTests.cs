using System.Collections.Generic;
using System.Linq;
using Hangeul.App;
using Hangeul.Domain;
using NUnit.Framework;

namespace Hangeul.Tests
{
    public class CardTests
    {
        [Test]
        public void IsComplete_Rules()
        {
            var full = Card.NewSyllable(1, 'ㄱ', 'ㅏ', 'ㄱ', 0, 0);
            Assert.IsTrue(full.IsComplete);
            Assert.AreEqual('각', full.Letter);

            var noJong = Card.NewSyllable(2, 'ㄱ', 'ㅏ', '\0', 0, 0);
            Assert.IsTrue(noJong.IsComplete);

            noJong.G.Jong = 'ㄱ';   // 받침 고스트가 남아 있으면 미완성
            Assert.IsFalse(noJong.IsComplete);
            Assert.IsNull(noJong.Letter);

            var noCho = Card.NewSyllable(3, '\0', 'ㅏ', '\0', 0, 0);
            Assert.IsFalse(noCho.IsComplete);
            Assert.IsFalse(Card.NewJamo(4, 'ㄱ', 0, 0).IsComplete);
        }

        [Test]
        public void Banned()
        {
            Assert.IsTrue(ForbiddenSyllables.IsBanned(new Parts { Cho = 'ㄷ', Jung = 'ㅚ', Jong = 'ㅆ' }));
            Assert.IsFalse(ForbiddenSyllables.IsBanned(new Parts { Cho = 'ㄷ', Jung = 'ㅙ', Jong = 'ㅆ' }));   // 됐
        }
    }

    public class GridModelTests
    {
        [Test]
        public void EmptyNear_PrefsFirst_ThenManhattan()
        {
            var g = new GridModel(5, 5);
            g.Add(Card.NewJamo(1, 'ㄱ', 2, 2));
            Assert.IsTrue(g.TryFindEmptyNear(2, 2, out var cell, DetachRules.Prefs(PullDir.R)));
            Assert.AreEqual((3, 2), cell);

            g.Add(Card.NewJamo(2, 'ㄱ', 3, 2));
            g.Add(Card.NewJamo(3, 'ㄱ', 2, 1));
            g.Add(Card.NewJamo(4, 'ㄱ', 2, 3));
            g.Add(Card.NewJamo(5, 'ㄱ', 1, 2));
            Assert.IsTrue(g.TryFindEmptyNear(2, 2, out cell, DetachRules.Prefs(PullDir.R)));
            Assert.AreEqual((2, 0), cell);   // 거리 1이 모두 막혀 거리 2: 행→열 순으로 먼저 만난 칸
        }

        [Test]
        public void EmptyNear_FullGrid()
        {
            var g = new GridModel(1, 1);
            g.Add(Card.NewJamo(1, 'ㄱ', 0, 0));
            Assert.IsFalse(g.TryFindEmptyNear(0, 0, out _));
        }

        [Test]
        public void FindRun_CenterRow_AndAnchor()
        {
            var g = new GridModel(10, 10);
            Assert.IsTrue(g.TryFindRun(4, null, out var s));
            Assert.AreEqual((3, 4), s);   // 가운데 줄((10-1)/2=4.5 → 4가 먼저), 가운데 정렬

            Assert.IsTrue(g.TryFindRun(3, (7, 0), out s));
            Assert.AreEqual((7, 0), s);
        }

        [Test]
        public void FindRun_SkipsBlocked_AndFailsWhenTooLong()
        {
            var g = new GridModel(4, 1);
            g.Add(Card.NewJamo(1, 'ㄱ', 1, 0));
            Assert.IsFalse(g.TryFindRun(3, null, out _));
            Assert.IsTrue(g.TryFindRun(2, null, out var s));
            Assert.AreEqual((2, 0), s);
            Assert.IsFalse(g.TryFindRun(5, null, out _));
        }
    }

    public class CombineTests
    {
        static Card J(char j) => Card.NewJamo(1, j, 0, 0);

        static CombineOutcome Try(Card a, Card o, out CombineResult r, bool hold = false) => CardOps.TryCombine(a, o, hold, out r);

        [Test]
        public void JamoJamo_Rules()
        {
            Assert.AreEqual(CombineOutcome.Ok, Try(J('ㅅ'), J('ㄱ'), out var r));
            Assert.IsTrue(r.IsJamo); Assert.AreEqual('ㄳ', r.Jamo);

            Assert.AreEqual(CombineOutcome.Ok, Try(J('ㅏ'), J('ㅗ'), out r));
            Assert.AreEqual('ㅘ', r.Jamo);

            Assert.AreEqual(CombineOutcome.Ok, Try(J('ㄱ'), J('ㅏ'), out r));   // 초성+중성
            Assert.AreEqual(('ㄱ', 'ㅏ', '\0'), (r.S.Cho, r.S.Jung, r.S.Jong));

            Assert.AreEqual(CombineOutcome.Ok, Try(J('ㅏ'), J('ㄱ'), out r));   // 순서 무관
            Assert.AreEqual(('ㄱ', 'ㅏ'), (r.S.Cho, r.S.Jung));

            Assert.AreEqual(CombineOutcome.Rejected, Try(J('ㄴ'), J('ㄱ'), out _));
            Assert.AreEqual(CombineOutcome.Ok, Try(J('ㅏ'), J('ㅏ'), out r));   // ㅏ+ㅏ=ㅑ
            Assert.AreEqual('ㅑ', r.Jamo);
        }

        [Test]
        public void Ho_NeedsHold()
        {
            Assert.AreEqual(CombineOutcome.Ok, Try(J('ㅗ'), J('ㅇ'), out var r));
            Assert.AreEqual(('ㅇ', 'ㅗ'), (r.S.Cho, r.S.Jung));   // 오

            Assert.AreEqual(CombineOutcome.Ok, Try(J('ㅗ'), J('ㅇ'), out r, hold: true));
            Assert.AreEqual('ㅎ', r.Jamo);

            Assert.AreEqual(CombineOutcome.Ok, Try(J('ㅇ'), J('ㅗ'), out r, hold: true));
            Assert.AreEqual('ㅎ', r.Jamo);
        }

        [Test]
        public void IntoSyllable()
        {
            var ga = Card.NewSyllable(2, 'ㄱ', 'ㅏ', '\0', 0, 0);
            Assert.AreEqual(CombineOutcome.Ok, Try(J('ㄴ'), ga, out var r));   // 받침 채움
            Assert.AreEqual('ㄴ', r.S.Jong);

            var gak = Card.NewSyllable(3, 'ㄱ', 'ㅏ', 'ㄱ', 0, 0);
            Assert.AreEqual(CombineOutcome.Ok, Try(J('ㅅ'), gak, out r));      // 겹받침 ㄳ
            Assert.AreEqual('ㄳ', r.S.Jong);

            Assert.AreEqual(CombineOutcome.Ok, Try(J('ㅗ'), Card.NewSyllable(4, 'ㄱ', 'ㅗ', '\0', 0, 0), out r));   // ㅗ+ㅗ=ㅛ
            Assert.AreEqual('ㅛ', r.S.Jung);

            Assert.AreEqual(CombineOutcome.Ok, Try(J('ㅏ'), Card.NewSyllable(5, 'ㄱ', 'ㅏ', '\0', 0, 0), out r));   // 중성 ㅏ + ㅏ = ㅑ
            Assert.AreEqual('ㅑ', r.S.Jung);

            // 중성끼리 합칠 때 순서는 (기존 중성, 드래그한 모음) 한 방향만 본다: ㅏ 다음 ㅜ는 표에 없다
            Assert.AreEqual(CombineOutcome.Rejected, Try(J('ㅜ'), Card.NewSyllable(6, 'ㄱ', 'ㅏ', '\0', 0, 0), out _));
        }

        [Test]
        public void IntoSyllable_GhostCleared_AndCho()
        {
            var s = new Card { Id = 9, Kind = CardKind.Syllable, S = new Parts { Jung = 'ㅏ' }, G = new Parts { Cho = 'ㄱ' } };
            Assert.AreEqual(CombineOutcome.Ok, Try(J('ㄱ'), s, out var r));
            Assert.AreEqual('ㄱ', r.S.Cho);
            Assert.AreEqual('\0', r.G.Cho);   // 채운 슬롯의 고스트는 지운다

            var chOnly = new Card { Id = 10, Kind = CardKind.Syllable, S = new Parts { Cho = 'ㄱ' }, G = new Parts { Jung = 'ㅏ' } };
            Assert.AreEqual(CombineOutcome.Rejected, Try(J('ㄴ'), chOnly, out _));   // 초성만 있고 중성이 없으면 자음 거절
        }

        [Test]
        public void BannedSyllable_IsRefused()
        {
            var dwe = Card.NewSyllable(1, 'ㄷ', 'ㅚ', '\0', 0, 0);
            Assert.AreEqual(CombineOutcome.Banned, Try(J('ㅆ'), dwe, out _));   // 되 + ㅆ = 됬
        }
    }

    public class InventoryModelTests
    {
        static (InventoryModel m, List<CardChange> log) Make(int cols = 10, int rows = 10)
        {
            var m = new InventoryModel(cols, rows);
            var log = new List<CardChange>();
            m.Changed += log.Add;
            return (m, log);
        }

        [Test]
        public void AddWord_PlacesRunOnCenterRow()
        {
            var (m, _) = Make();
            var r = m.AddWord("가방");
            Assert.AreEqual(2, r.Added);
            var cards = m.Grid.Cards;
            Assert.AreEqual(new[] { (4, 4), (5, 4) }, cards.Select(c => (c.Col, c.Row)).ToArray());
            Assert.AreEqual("가방", new string(cards.Select(c => c.Letter.Value).ToArray()));
        }

        [Test]
        public void AddWord_SkipsBannedAndNonHangul()
        {
            var (m, _) = Make();
            var r = m.AddWord("됬a가");
            Assert.IsTrue(r.HadBanned);
            Assert.AreEqual(1, r.Added);
        }

        [Test]
        public void AddWord_LimitedByCapacity()
        {
            var (m, _) = Make(2, 1);
            Assert.AreEqual(2, m.AddWord("가나다").Added);
            Assert.AreEqual(0, m.AddWord("라").Added);
        }

        [Test]
        public void AddWord_LongWord_SplitsIntoRows()
        {
            var (m, _) = Make(3, 3);
            Assert.AreEqual(5, m.AddWord("가나다라마").Added);
            Assert.AreEqual(5, m.Grid.Cards.Count);
            Assert.AreEqual(5, m.Grid.Cards.Select(c => (c.Col, c.Row)).Distinct().Count());
        }

        [Test]
        public void Detach_Jung_LeavesGhostAndJamoCard()
        {
            var (m, _) = Make();
            m.AddWord("가");
            var ga = m.Grid.Cards[0];
            var jung = m.Detach(ga, PullDir.R);
            Assert.NotNull(jung);
            Assert.AreEqual(CardKind.Jamo, ga.Kind);   // 남은 건 초성 하나 → 자모 카드
            Assert.AreEqual('ㄱ', ga.Jamo);
            Assert.AreEqual('ㅏ', jung.Jamo);
            Assert.AreEqual((ga.Col + 1, ga.Row), (jung.Col, jung.Row));   // 끌어당긴 방향 이웃 칸
        }

        [Test]
        public void Detach_Jong_NoGhost_StaysComplete()
        {
            var (m, _) = Make();
            m.AddWord("각");
            var c = m.Grid.Cards[0];
            var jong = m.Detach(c, Slot.Jong, PullDir.D);
            Assert.AreEqual('ㄱ', jong.Jamo);
            Assert.AreEqual(CardKind.Syllable, c.Kind);
            Assert.IsTrue(c.IsComplete);
            Assert.AreEqual('가', c.Letter);
        }

        [Test]
        public void Detach_Cho_LeavesGhost_Incomplete()
        {
            var (m, _) = Make();
            m.AddWord("각");
            var c = m.Grid.Cards[0];
            m.Detach(c, Slot.Cho, PullDir.L);
            Assert.AreEqual(CardKind.Syllable, c.Kind);
            Assert.AreEqual('ㄱ', c.G.Cho);
            Assert.IsFalse(c.IsComplete);
        }

        [Test]
        public void Detach_Rejected_WhenGridFull_NothingChanges()
        {
            var (m, log) = Make(1, 1);
            m.AddWord("가");
            var c = m.Grid.Cards[0];
            log.Clear();
            Assert.IsNull(m.Detach(c, PullDir.R));
            Assert.AreEqual(CardKind.Syllable, c.Kind);
            Assert.AreEqual(ChangeType.Rejected, log.Single().Type);
        }

        [Test]
        public void Detach_HorizontalVowel_LeftRightRefused()
        {
            var (m, _) = Make();
            m.AddWord("고");
            var c = m.Grid.Cards[0];
            Assert.IsNull(m.Detach(c, PullDir.L));
            Assert.IsNull(m.Detach(c, PullDir.R));
            Assert.NotNull(m.Detach(c, PullDir.U));   // 위로 끌면 초성
        }

        [Test]
        public void Transform_Rotate_AndSplitPriority()
        {
            var (m, _) = Make();
            var a = m.PlaceJamo('ㅏ', 0, 0);
            Assert.AreEqual(TransformResult.Rotated, m.Transform(a, false, out int k));
            Assert.AreEqual(('ㅜ', 1), (a.Jamo, k));

            var gwa = m.PlaceJamo('ㅘ', 3, 3);
            Assert.AreEqual(TransformResult.Split, m.Transform(gwa, false, out _));   // ㅘ는 회전 불가 → 분리
            Assert.AreEqual('ㅗ', gwa.Jamo);
            Assert.AreEqual('ㅏ', m.Grid.CardAt(4, 3).Jamo);

            var mm = m.PlaceJamo('ㅁ', 6, 6);
            Assert.AreEqual(TransformResult.Rejected, m.Transform(mm, true, out _));
        }

        [Test]
        public void Split_Hieut_StacksVertically()
        {
            var (m, _) = Make();
            var h = m.PlaceJamo('ㅎ', 2, 2);
            m.Transform(h, true, out _);
            Assert.AreEqual('ㅗ', h.Jamo);
            Assert.AreEqual('ㅇ', m.Grid.CardAt(2, 3).Jamo);
        }

        [Test]
        public void Split_Hieut_FallsBackToNearest_WhenBothBlocked()
        {
            var (m, _) = Make();
            var h = m.PlaceJamo('ㅎ', 5, 0);   // 위는 격자 밖, 아래는 막힘
            m.PlaceJamo('ㄱ', 5, 1);
            Assert.AreEqual(TransformResult.Split, m.Transform(h, true, out _));
            Assert.AreEqual('ㅗ', h.Jamo);
            Assert.AreEqual('ㅇ', m.Grid.CardAt(4, 0).Jamo);   // 거리 1 중 행→열 순으로 먼저 만난 칸
        }

        [Test]
        public void Split_Hieut_UsesAboveWhenBelowBlocked()
        {
            var (m, _) = Make();
            var h = m.PlaceJamo('ㅎ', 2, 2);
            m.PlaceJamo('ㄱ', 2, 3);
            m.Transform(h, true, out _);
            Assert.AreEqual('ㅇ', h.Jamo);                       // 원본=ㅇ, 새=ㅗ(위)
            Assert.AreEqual('ㅗ', m.Grid.CardAt(2, 1).Jamo);
        }

        [Test]
        public void Merge_Success_RemovesDragged()
        {
            var (m, log) = Make();
            var a = m.PlaceJamo('ㄱ', 0, 0);
            var b = m.PlaceJamo('ㅏ', 1, 0);
            Assert.AreEqual(CombineOutcome.Ok, m.TryMerge(a, b));
            Assert.AreEqual(1, m.Grid.Cards.Count);
            Assert.AreEqual(CardKind.Syllable, b.Kind);
            Assert.IsTrue(log.Any(c => c.Type == ChangeType.Removed && c.Card == a));
        }

        [Test]
        public void Merge_Banned_ChangesNothing()
        {
            var (m, _) = Make();
            m.AddWord("되");
            var dwe = m.Grid.Cards[0];
            var ss = m.PlaceJamo('ㅆ', 0, 0);
            Assert.AreEqual(CombineOutcome.Banned, m.TryMerge(ss, dwe));
            Assert.AreEqual(2, m.Grid.Cards.Count);
            Assert.AreEqual('\0', dwe.S.Jong);
        }

        [Test]
        public void Move_SwapsWithOccupant()
        {
            var (m, _) = Make();
            var a = m.PlaceJamo('ㄱ', 0, 0);
            var b = m.PlaceJamo('ㄴ', 3, 3);
            m.Move(a, 3, 3);
            Assert.AreEqual((3, 3), (a.Col, a.Row));
            Assert.AreEqual((0, 0), (b.Col, b.Row));
            m.Move(a, 99, -5);   // clamp
            Assert.AreEqual((9, 0), (a.Col, a.Row));
        }

        [Test]
        public void Resize_NeedsConfirmWhenOverflow_ThenDiscardsFromEnd()
        {
            var (m, _) = Make(3, 3);
            m.AddWord("가나다");   // 한 줄
            m.AddWord("라마바");
            m.AddWord("사아자");
            var res = m.Resize(2, 2);
            Assert.IsFalse(res.Applied);
            Assert.AreEqual(5, res.Overflow);
            Assert.AreEqual(9, m.Grid.Cards.Count);   // 아무것도 바뀌지 않음

            res = m.Resize(2, 2, allowDiscard: true);
            Assert.IsTrue(res.Applied);
            Assert.AreEqual(4, m.Grid.Cards.Count);
            Assert.IsTrue(m.Grid.Cards.All(c => m.Grid.InGrid(c.Col, c.Row)));
            Assert.AreEqual(4, m.Grid.Cards.Select(c => (c.Col, c.Row)).Distinct().Count());
        }

        [Test]
        public void Resize_Shrink_RelocatesOutsideCards()
        {
            var (m, _) = Make(10, 10);
            var a = m.PlaceJamo('ㄱ', 9, 9);
            m.Resize(3, 3);
            Assert.AreEqual((2, 2), (a.Col, a.Row));
            m.Resize(0, 99);   // clamp 1..20
            Assert.AreEqual((1, 20), (m.Grid.Cols, m.Grid.Rows));
        }
    }
    public class HoHoldTrackerTests
    {
        static Card J(int id, char j) => Card.NewJamo(id, j, 0, 0);

        [Test]
        public void TurnsOn_AfterHoldTime_OnSamePair()
        {
            var t = new HoHoldTracker();
            Card o = J(1, 'ㅗ'), ieung = J(2, 'ㅇ');
            t.Tick(o, ieung, 0.5f);
            Assert.IsFalse(t.On);
            t.Tick(o, ieung, 0.2f);
            Assert.IsTrue(t.On);
        }

        [Test]
        public void Resets_WhenTargetChangesOrLeaves()
        {
            var t = new HoHoldTracker();
            Card o = J(1, 'ㅗ'), a = J(2, 'ㅇ'), b = J(3, 'ㅇ');
            t.Tick(o, a, 0.7f);
            Assert.IsTrue(t.On);
            t.Tick(o, b, 0.1f);          // 대상이 바뀌면 처음부터
            Assert.IsFalse(t.On);
            t.Tick(o, b, 0.6f);
            Assert.IsTrue(t.On);
            t.Tick(o, null, 0.1f);       // 이탈
            Assert.IsFalse(t.On);
        }

        [Test]
        public void IgnoresOtherPairs()
        {
            var t = new HoHoldTracker();
            t.Tick(J(1, 'ㄱ'), J(2, 'ㅏ'), 5f);
            Assert.IsFalse(t.On);
        }
    }
}
