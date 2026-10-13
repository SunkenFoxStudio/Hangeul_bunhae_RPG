using System;
using System.Collections.Generic;
using System.Linq;
using Hangeul.Domain;

namespace Hangeul.App
{
    public enum ChangeType
    {
        Added,      // 새 카드 (pop 연출)
        Removed,    // 병합으로 사라진 카드
        Discarded,  // 폐기된 카드 (폐기 연출)
        Updated,    // 내용이 바뀐 카드 (pop 연출)
        Moved,      // 칸이 바뀐 카드
        Rejected,   // 할 수 없는 조작 (흔들기)
    }

    public readonly struct CardChange
    {
        public readonly ChangeType Type;
        public readonly Card Card;
        public CardChange(ChangeType type, Card card) { Type = type; Card = card; }
    }

    public enum TransformResult { None, Rotated, Split, Rejected }

    public readonly struct AddWordResult
    {
        public readonly int Added;
        public readonly bool HadBanned;   // 금지 음절이 섞여 있었는지
        public AddWordResult(int added, bool hadBanned) { Added = added; HadBanned = hadBanned; }
    }

    public readonly struct ResizeResult
    {
        public readonly bool Applied;
        public readonly int Overflow;     // 적용하려면 폐기해야 하는 카드 수 (Applied=false일 때 확인창에 쓴다)
        public readonly int Discarded;
        public ResizeResult(bool applied, int overflow, int discarded) { Applied = applied; Overflow = overflow; Discarded = discarded; }
    }

    /// <summary>
    /// 인벤토리 상태의 단일 진실 소스. 모든 변경은 Changed 이벤트로 통지되고, View는 그것만 보고 그린다.
    /// 규칙은 inventory_medieval.html의 함수와 1:1 대응 (addWord, detach, splitJamo, transform, apply, setGridSize).
    /// </summary>
    public sealed class InventoryModel
    {
        int _nextId = 1;

        public GridModel Grid { get; }
        public event Action<CardChange> Changed;

        public InventoryModel(int cols = 10, int rows = 10) { Grid = new GridModel(cols, rows); }

        void Notify(ChangeType type, Card c) => Changed?.Invoke(new CardChange(type, c));

        Card AddJamo(char jamo, int col, int row)
        {
            var c = Card.NewJamo(_nextId++, jamo, col, row);
            Grid.Add(c);
            Notify(ChangeType.Added, c);
            return c;
        }

        Card AddSyllable(char cho, char jung, char jong, int col, int row)
        {
            var c = Card.NewSyllable(_nextId++, cho, jung, jong, col, row);
            Grid.Add(c);
            Notify(ChangeType.Added, c);
            return c;
        }

        /// <summary>디버그/테스트용: 자모 카드를 지정한 칸에 놓는다. 칸이 막혔으면 null.</summary>
        public Card PlaceJamo(char jamo, int col, int row) => Grid.IsFree(col, row) ? AddJamo(jamo, col, row) : null;

        // ───── 단어 추가 (addWord) ─────
        /// <summary>
        /// 단어의 한글 음절만 카드로 만들어 빈 칸에 놓는다. 금지 음절은 건너뛰고, 남은 칸 수만큼만 놓는다.
        /// Cols글자씩 끊어 한 줄에 연속 배치하고, 한 줄이 안 나오면 흩어서 놓는다.
        /// </summary>
        public AddWordResult AddWord(string word, (int col, int row)? anchor = null)
        {
            var all = new List<(char cho, char jung, char jong, char syl)>();
            foreach (char ch in word ?? "")
                if (Hangul.TryDecompose(ch, out var c, out var v, out var t)) all.Add((c, v, t, ch));

            bool hadBanned = all.Any(d => ForbiddenSyllables.IsBanned(d.syl));
            int room = Math.Max(0, Grid.Capacity - Grid.Cards.Count);
            var syls = all.Where(d => !ForbiddenSyllables.IsBanned(d.syl)).Take(room).ToList();

            for (int i = 0; i < syls.Count; i += Grid.Cols)
            {
                var chunk = syls.Skip(i).Take(Grid.Cols).ToList();
                if (Grid.TryFindRun(chunk.Count, anchor, out var at))
                {
                    for (int k = 0; k < chunk.Count; k++)
                        AddSyllable(chunk[k].cho, chunk[k].jung, chunk[k].jong, at.col + k, at.row);
                }
                else
                {
                    foreach (var d in chunk)
                    {
                        int ac = anchor?.col ?? Grid.Cols >> 1, ar = anchor?.row ?? Grid.Rows >> 1;
                        if (!Grid.TryFindEmptyNear(ac, ar, out var cell)) break;
                        AddSyllable(d.cho, d.jung, d.jong, cell.col, cell.row);
                    }
                }
            }
            return new AddWordResult(syls.Count, hadBanned);
        }

        // ───── 분리 (detach) ─────
        /// <summary>
        /// 음절 카드에서 슬롯 하나를 자모 카드로 떼어낸다. 끌어당긴 방향 쪽 이웃 빈 칸 우선.
        /// 빈 칸이 없거나 슬롯이 비었으면 아무것도 바꾸지 않고 Rejected 통지 후 null.
        /// </summary>
        public Card Detach(Card c, Slot slot, PullDir dir = PullDir.R)
        {
            if (c.Kind != CardKind.Syllable || c.S[slot] == 0
                || !Grid.TryFindEmptyNear(c.Col, c.Row, out var cell, DetachRules.Prefs(dir)))
            {
                Notify(ChangeType.Rejected, c);
                return null;
            }

            char val = c.S[slot];
            c.S[slot] = Hangul.None;
            c.G[slot] = slot == Slot.Jong ? Hangul.None : val;   // 받침은 고스트를 남기지 않는다

            if (c.S.Count == 1)                                   // 남은 게 하나면 자모 카드로
            {
                c.Kind = CardKind.Jamo;
                c.Jamo = c.S.Cho != 0 ? c.S.Cho : c.S.Jung != 0 ? c.S.Jung : c.S.Jong;
                c.S = default; c.G = default;
            }
            Notify(ChangeType.Updated, c);
            return AddJamo(val, cell.col, cell.row);
        }

        /// <summary>드래그 방향으로 분리를 시도한다(슬롯 선택 포함). 뽑을 슬롯이 없으면 Rejected.</summary>
        public Card Detach(Card c, PullDir dir)
        {
            if (!DetachRules.TryPickSlot(c, dir, out var slot)) { Notify(ChangeType.Rejected, c); return null; }
            return Detach(c, slot, dir);
        }

        // ───── 겹자모 분리 / 회전 (transform) ─────
        /// <summary>겹자모 분리. ㅎ은 세로로 쌓는다(아래 칸 → 위 칸 → 가장 가까운 빈 칸).</summary>
        public bool SplitJamo(Card c)
        {
            if (c.Kind != CardKind.Jamo || !JamoTables.TrySplit(c.Jamo, out var parts)) return false;

            char first = parts.first, second = parts.second;
            (int col, int row) cell;
            if (c.Jamo == 'ㅎ')
            {
                if (Grid.IsFree(c.Col, c.Row + 1)) cell = (c.Col, c.Row + 1);
                else if (Grid.IsFree(c.Col, c.Row - 1)) { cell = (c.Col, c.Row - 1); (first, second) = (second, first); }
                else if (!Grid.TryFindEmptyNear(c.Col, c.Row, out cell)) { Notify(ChangeType.Rejected, c); return false; }
            }
            else if (!Grid.TryFindEmptyNear(c.Col, c.Row, out cell, DetachRules.Prefs(PullDir.R)))
            {
                Notify(ChangeType.Rejected, c);
                return false;
            }

            c.Jamo = first;
            Notify(ChangeType.Updated, c);
            AddJamo(second, cell.col, cell.row);
            return true;
        }

        /// <summary>
        /// R키/우클릭(splitFirst=false: 회전 우선), 더블클릭(splitFirst=true: 분리 우선).
        /// 회전은 모양이 바뀌는 최소 시계 회전 횟수만큼 돌리고, 둘 다 안 되면 Rejected.
        /// </summary>
        public TransformResult Transform(Card c, bool splitFirst, out int quarterTurns)
        {
            quarterTurns = 0;
            if (c.Kind != CardKind.Jamo) return TransformResult.None;

            bool canSplit = JamoTables.CanSplit(c.Jamo);
            int k = RotationTable.NextStep(c.Jamo);
            if (canSplit && (splitFirst || k == 0))
                return SplitJamo(c) ? TransformResult.Split : TransformResult.Rejected;
            if (k == 0) { Notify(ChangeType.Rejected, c); return TransformResult.Rejected; }

            c.Jamo = RotationTable.Rotate(c.Jamo, k);
            quarterTurns = k;
            Notify(ChangeType.Updated, c);
            return TransformResult.Rotated;
        }

        // ───── 결합 / 이동 ─────
        /// <summary>
        /// 드래그한 자모 카드를 대상 카드에 합친다. 성공하면 드래그 카드는 제거되고 대상이 변환된다.
        /// 금지 음절이 되면 거절(Banned)이며 아무것도 바꾸지 않는다.
        /// </summary>
        public CombineOutcome TryMerge(Card dragged, Card target, bool hoHold = false)
        {
            var outcome = CardOps.TryCombine(dragged, target, hoHold, out var r);
            if (outcome != CombineOutcome.Ok) return outcome;

            if (r.IsJamo) { target.Kind = CardKind.Jamo; target.Jamo = r.Jamo; target.S = default; target.G = default; }
            else { target.Kind = CardKind.Syllable; target.Jamo = Hangul.None; target.S = r.S; target.G = r.G; }

            Grid.Remove(dragged);
            Notify(ChangeType.Removed, dragged);
            Notify(ChangeType.Updated, target);
            return CombineOutcome.Ok;
        }

        /// <summary>카드를 (col,row)로 옮긴다. 격자 밖은 clamp, 점유된 칸이면 자리 교환(상대는 내 원래 칸으로).</summary>
        public void Move(Card c, int col, int row)
        {
            col = GridModel.Clamp(col, 0, Grid.Cols - 1);
            row = GridModel.Clamp(row, 0, Grid.Rows - 1);
            var occ = Grid.CardAt(col, row, c);
            if (occ != null)
            {
                occ.Col = c.Col; occ.Row = c.Row;
                Notify(ChangeType.Moved, occ);
            }
            c.Col = col; c.Row = row;
            Notify(ChangeType.Moved, c);
        }

        // ───── 폐기 / 격자 크기 ─────
        public void Discard(IEnumerable<Card> cards)
        {
            foreach (var c in cards.ToList())
                if (Grid.Remove(c)) Notify(ChangeType.Discarded, c);
        }

        public void Discard(Card c) => Discard(new[] { c });

        public void Clear() => Discard(Grid.Cards);

        /// <summary>
        /// 격자 크기 변경(1..20으로 clamp). 카드가 새 용량을 넘으면 allowDiscard=true일 때만 (행→열 순) 마지막 카드부터 폐기하고,
        /// 아니면 아무것도 바꾸지 않고 Overflow를 돌려준다(UI가 확인창을 띄운 뒤 다시 호출).
        /// 새 격자 밖이거나 겹친 카드는 가장 가까운 빈 칸으로 옮긴다.
        /// </summary>
        public ResizeResult Resize(int cols, int rows, bool allowDiscard = false)
        {
            cols = GridModel.Clamp(cols, GridModel.MinSize, GridModel.MaxSize);
            rows = GridModel.Clamp(rows, GridModel.MinSize, GridModel.MaxSize);
            if (cols == Grid.Cols && rows == Grid.Rows) return new ResizeResult(true, 0, 0);

            int cap = cols * rows, over = Grid.Cards.Count - cap;
            if (over > 0 && !allowDiscard) return new ResizeResult(false, over, 0);

            var sorted = Grid.Cards.OrderBy(c => c.Row).ThenBy(c => c.Col).ToList();
            if (over > 0) Discard(sorted.Skip(cap));
            Grid.SetSize(cols, rows);

            foreach (var c in sorted)
            {
                if (!Grid.Cards.Contains(c)) continue;
                if (Grid.InGrid(c.Col, c.Row) && Grid.CardAt(c.Col, c.Row, c) == null) continue;
                int fromCol = GridModel.Clamp(c.Col, 0, cols - 1), fromRow = GridModel.Clamp(c.Row, 0, rows - 1);
                c.Col = c.Row = -1;                          // 자기 자리는 비운 것으로 보고 가장 가까운 빈 칸을 찾는다
                Grid.TryFindEmptyNear(fromCol, fromRow, out var cell);
                c.Col = cell.col; c.Row = cell.row;
                Notify(ChangeType.Moved, c);
            }
            return new ResizeResult(true, Math.Max(0, over), Math.Max(0, over));
        }
    }
}
