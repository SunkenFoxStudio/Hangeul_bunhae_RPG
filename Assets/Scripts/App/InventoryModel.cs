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
        Rotating,   // 드래그 회전 중 자모가 바뀜 (다시 그리기만, 팝 연출 없음)
    }

    public readonly struct CardChange
    {
        public readonly ChangeType Type;
        public readonly Card Card;
        public CardChange(ChangeType type, Card card) { Type = type; Card = card; }
    }

    public enum TransformResult { None, Rotated, Split, Rejected }

    public readonly struct ShakeResult
    {
        public readonly int Rotated;   // 회전한 자모 카드 수
        public readonly int Merged;    // 다른 카드에 조립된(사라진) 자모 카드 수
        public ShakeResult(int rotated, int merged) { Rotated = rotated; Merged = merged; }
    }

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
        public MatchScanner Scanner { get; } = new MatchScanner();
        public event Action<CardChange> Changed;
        /// <summary>모드가 바뀌었을 때(조립/가방). 화면은 이것만 보고 표현을 바꾼다.</summary>
        public event Action<InventoryMode> ModeChanged;
        /// <summary>완성 단어 스캔이 끝날 때마다(알림 여부와 무관). 가방 모드 화면이 <see cref="MatchScanner.Shown"/>을 다시 읽는 시점.</summary>
        public event Action Scanned;
        public InventoryMode Mode { get; private set; }
        /// <summary>조작으로 단어가 새로 완성됐을 때(단어 추가/불러오기/격자 크기 변경으로 이미 완성돼 있던 것은 알리지 않는다). 본편이 구독해 아이템 지급 등을 처리한다.</summary>
        public event Action<WordMatch> WordCompleted;

        public InventoryModel(int cols = 10, int rows = 10) { Grid = new GridModel(cols, rows); }

        /// <summary>모드를 바꾼다. 카드 데이터는 건드리지 않는다.</summary>
        public void SetMode(InventoryMode mode)
        {
            if (Mode == mode) return;
            Mode = mode;
            ModeChanged?.Invoke(mode);
        }

        /// <summary>완성 판정에 쓸 단어 목록을 바꾼다. 이미 완성돼 있던 단어는 알리지 않는다.</summary>
        public void SetWords(ItemWordSet words)
        {
            Scanner.SetWords(words);
            Scan(notify: false);
        }

        /// <summary>
        /// 완성된 단어를 다시 찾는다. 카드를 조작한 뒤 호출하면(notify=true) 새로 완성된 단어마다 <see cref="WordCompleted"/>가 발생한다.
        /// 단어 추가/불러오기/크기 변경은 모델이 notify=false로 알아서 부른다.
        /// </summary>
        public IReadOnlyList<WordMatch> Scan(bool notify)
        {
            var fresh = Scanner.Scan(Grid, notify);
            Scanned?.Invoke();
            foreach (var m in fresh) WordCompleted?.Invoke(m);
            return fresh;
        }

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
            Scan(notify: false);          // 입력만으로 이미 완성돼 있는 단어는 알리지 않는다
            return new AddWordResult(syls.Count, hadBanned);
        }

        /// <summary>
        /// 이 단어의 글자 카드를 전부 빈 칸에 놓을 수 있는지(아이템 줍기 전에 확인한다). 한글 음절이 하나도 없거나 금지 음절이 섞여 있거나
        /// 남은 칸이 모자라면 false — 일부만 들어가는 일은 없도록 호출한 쪽이 이걸 먼저 확인한다.
        /// </summary>
        public bool CanAddWord(string word)
        {
            int total = 0;
            foreach (char ch in word ?? "")
            {
                if (!Hangul.IsSyllable(ch)) continue;
                if (ForbiddenSyllables.IsBanned(ch)) return false;
                total++;
            }
            return total > 0 && total <= Math.Max(0, Grid.Capacity - Grid.Cards.Count);
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

        // ───── 가방 흔들기 ─────
        /// <summary>
        /// 가방을 흔든다: 인벤토리 안의 <b>자모 카드</b>(완성된 음절 카드는 건드리지 않는다)를 무작위 순서로 하나씩 보면서
        /// mergeChance 확률로 합칠 수 있는 아무 카드에 조립하고, 아니면 rotateChance 확률로 모양이 바뀌는 각도로 회전한다.
        /// 나머지는 그대로 둔다 — 모든 카드가 조립될 필요는 없다. 금지 음절이 되는 조립은 후보에서 제외한다.
        /// <b>이미 완성된 단어에 속한 카드에는 조립하지 않는다</b>(흔들다가 새로 완성된 단어도 그 순간부터 보호한다).
        /// 합쳐진 카드는 계속 처리 대상이 되므로(자모로 남아 있으면) 한 번에 연쇄 조립이 일어날 수 있다.
        /// rng를 넘기면 결과를 재현할 수 있다(테스트용).
        /// </summary>
        public ShakeResult ShakeBag(Random rng, float mergeChance = 0.55f, float rotateChance = 0.3f)
        {
            rng = rng ?? new Random();
            var jamo = Grid.Cards.Where(c => c.Kind == CardKind.Jamo).ToList();
            for (int i = jamo.Count - 1; i > 0; i--)             // 처리 순서를 섞는다
            {
                int j = rng.Next(i + 1);
                (jamo[i], jamo[j]) = (jamo[j], jamo[i]);
            }

            var guarded = CardsInCompletedWords();   // 완성된 단어를 이루는 카드: 조립 대상에서 제외

            int rotated = 0, merged = 0;
            foreach (var c in jamo)
            {
                if (c.Kind != CardKind.Jamo || !Grid.Cards.Contains(c)) continue;   // 앞에서 다른 카드에 합쳐졌거나 음절이 됐다

                double roll = rng.NextDouble();
                bool didMerge = false;
                if (roll < mergeChance)
                {
                    var targets = Grid.Cards
                        .Where(o => o != c && !guarded.Contains(o.Id) && CardOps.TryCombine(c, o, false, out _) == CombineOutcome.Ok)
                        .ToList();
                    if (targets.Count > 0)
                        didMerge = TryMerge(c, targets[rng.Next(targets.Count)]) == CombineOutcome.Ok;
                    if (didMerge)
                    {
                        merged++;
                        guarded = CardsInCompletedWords();   // 이번 조립으로 단어가 완성됐을 수 있다
                    }
                }

                if (!didMerge && roll >= mergeChance && roll < mergeChance + rotateChance)   // 조립이 걸렸는데 대상이 없으면 그대로 둔다
                {
                    var turns = Enumerable.Range(1, 3).Where(k => RotationTable.Rotate(c.Jamo, k) != c.Jamo).ToList();
                    if (turns.Count > 0)
                    {
                        c.Jamo = RotationTable.Rotate(c.Jamo, turns[rng.Next(turns.Count)]);
                        Notify(ChangeType.Updated, c);
                        rotated++;
                    }
                }
            }
            return new ShakeResult(rotated, merged);
        }

        // 지금 완성돼 있는 단어(겹쳐서 숨겨진 것 포함)를 이루는 카드의 아이디
        HashSet<int> CardsInCompletedWords()
        {
            var ids = new HashSet<int>();
            foreach (var m in Scanner.FindAll(Grid))
                foreach (var (col, row) in m.Cells())
                {
                    var card = Grid.CardAt(col, row);
                    if (card != null) ids.Add(card.Id);
                }
            return ids;
        }

        // ───── 모서리 드래그 회전 ─────
        /// <summary>자모 카드의 드래그 회전을 시작한다. 자모 카드가 아니면 null.</summary>
        public RotationSession BeginRotation(Card c) => c.Kind == CardKind.Jamo ? new RotationSession(c.Jamo) : null;

        /// <summary>누적 90° 횟수를 반영한다. 90° 경계를 넘는 순간 자모가 바뀐다(손을 떼기 전에도). 바뀌었으면 true.</summary>
        public bool ApplyRotation(Card c, RotationSession s, int steps)
        {
            if (!s.Apply(steps)) return false;
            c.Jamo = s.Current;
            Notify(ChangeType.Rotating, c);
            return true;
        }

        /// <summary>손을 뗀다. 자모로 인정되는 각도면 true(확정), 아니면 false(원래 자모로 복귀한 상태).</summary>
        public bool FinishRotation(Card c, RotationSession s)
        {
            bool mapped = s.Finish();
            if (c.Jamo != s.Current)
            {
                c.Jamo = s.Current;
                Notify(ChangeType.Rotating, c);
            }
            return mapped;
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

        /// <summary>
        /// 가로로 이어 붙은 카드 묶음(왼쪽→오른쪽 순서)을 왼쪽 끝이 (startCol,row)가 되게 통째로 옮길 수 있는지.
        /// 목적지가 모두 격자 안이고, 비어 있거나 묶음 자신의 칸이어야 한다(자리 교환은 하지 않는다).
        /// </summary>
        public bool CanMoveGroup(IReadOnlyList<Card> cards, int startCol, int row)
        {
            if (cards == null || cards.Count == 0) return false;
            if (row < 0 || row >= Grid.Rows || startCol < 0 || startCol + cards.Count > Grid.Cols) return false;
            for (int i = 0; i < cards.Count; i++)
            {
                var occupant = Grid.CardAt(startCol + i, row);
                if (occupant != null && !cards.Contains(occupant)) return false;
            }
            return true;
        }

        /// <summary>
        /// 모양이 정해진 묶음(일렬이든 접두어를 위에 얹은 모양이든)을 모양 그대로 (dCol,dRow)만큼 옮길 수 있는지.
        /// 목적지가 모두 격자 안이고, 비어 있거나 묶음 자신의 칸이어야 한다(자리 교환은 하지 않는다).
        /// </summary>
        public bool CanShiftGroup(IReadOnlyList<Card> cards, int dCol, int dRow)
        {
            if (cards == null || cards.Count == 0) return false;
            foreach (var c in cards)
            {
                int col = c.Col + dCol, row = c.Row + dRow;
                if (!Grid.InGrid(col, row)) return false;
                var occupant = Grid.CardAt(col, row);
                if (occupant != null && !cards.Contains(occupant)) return false;
            }
            return true;
        }

        /// <summary>묶음을 모양 그대로 (dCol,dRow)만큼 옮긴다. 옮길 수 없으면 아무것도 바꾸지 않고 false.</summary>
        public bool ShiftGroup(IReadOnlyList<Card> cards, int dCol, int dRow)
        {
            if (!CanShiftGroup(cards, dCol, dRow)) return false;
            foreach (var c in cards) { c.Col += dCol; c.Row += dRow; }
            foreach (var c in cards) Notify(ChangeType.Moved, c);
            return true;
        }

        /// <summary>묶음을 통째로 옮긴다. 옮길 수 없으면 아무것도 바꾸지 않고 false.</summary>
        public bool MoveGroup(IReadOnlyList<Card> cards, int startCol, int row)
        {
            if (!CanMoveGroup(cards, startCol, row)) return false;
            for (int i = 0; i < cards.Count; i++) { cards[i].Col = startCol + i; cards[i].Row = row; }
            foreach (var c in cards) Notify(ChangeType.Moved, c);
            return true;
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
            Scan(notify: false);
            return new ResizeResult(true, Math.Max(0, over), Math.Max(0, over));
        }
        // ───── 저장 / 불러오기 ─────
        /// <summary>현재 격자 크기와 카드 배치를 세이브 데이터로 만든다.</summary>
        public InventorySaveData ToSave()
        {
            var d = new InventorySaveData
            {
                cols = Grid.Cols,
                rows = Grid.Rows,
                mode = Mode == InventoryMode.Bag ? InventorySaveData.ModeBag : InventorySaveData.ModeAssemble,
            };
            foreach (var c in Grid.Cards.OrderBy(c => c.Id)) d.cards.Add(CardSaveData.From(c));
            return d;
        }

        /// <summary>
        /// 세이브 데이터로 인벤토리를 통째로 교체한다. 한글 규칙에 맞지 않는 카드는 버리고,
        /// 범위 밖이거나 칸이 겹친 카드는 가장 가까운 빈 칸으로 옮기며(Resize와 같은 규칙), 자리가 모자라면 마지막 카드부터 버린다.
        /// 아이디가 비정상이거나 겹치면 새로 매긴다. 데이터 자체가 못 쓰는 것이면 아무것도 바꾸지 않고 실패를 돌려준다.
        /// </summary>
        public LoadResult Load(InventorySaveData data)
        {
            if (data == null) return LoadResult.Fail("저장 데이터가 없습니다.");
            if (data.schemaVersion != InventorySaveData.CurrentVersion)
                return LoadResult.Fail($"지원하지 않는 schemaVersion: {data.schemaVersion}");

            int cols = GridModel.Clamp(data.cols, GridModel.MinSize, GridModel.MaxSize);
            int rows = GridModel.Clamp(data.rows, GridModel.MinSize, GridModel.MaxSize);

            // 1) 검증
            var cards = new List<Card>();
            int skipped = 0;
            foreach (var cs in data.cards ?? new List<CardSaveData>())
            {
                if (cs != null && cs.TryToCard(out var card)) cards.Add(card);
                else skipped++;
            }

            // 2) 용량을 넘으면 (행→열 순) 마지막 카드부터 버린다
            cards = cards.OrderBy(c => c.Row).ThenBy(c => c.Col).ToList();
            int cap = cols * rows;
            if (cards.Count > cap)
            {
                skipped += cards.Count - cap;
                cards = cards.Take(cap).ToList();
            }

            // 3) 아이디 정리: 0 이하이거나 겹치면 새로 매긴다
            var seen = new HashSet<int>();
            int maxId = 0;
            foreach (var c in cards) if (c.Id > 0 && seen.Add(c.Id)) maxId = Math.Max(maxId, c.Id);
            seen.Clear();
            foreach (var c in cards)
            {
                if (c.Id <= 0 || !seen.Add(c.Id)) c.Id = ++maxId;
            }
            _nextId = Math.Max(_nextId, maxId + 1);

            // 4) 기존 카드를 지우고 새 격자에 배치
            Discard(Grid.Cards);
            Grid.SetSize(cols, rows);

            var deferred = new List<Card>();
            foreach (var c in cards)
            {
                if (Grid.InGrid(c.Col, c.Row) && Grid.CardAt(c.Col, c.Row) == null) Grid.Add(c);
                else deferred.Add(c);
            }
            foreach (var c in deferred)
            {
                int fromCol = GridModel.Clamp(c.Col, 0, cols - 1), fromRow = GridModel.Clamp(c.Row, 0, rows - 1);
                Grid.TryFindEmptyNear(fromCol, fromRow, out var cell);
                c.Col = cell.col; c.Row = cell.row;
                Grid.Add(c);
            }

            foreach (var c in Grid.Cards.ToList()) Notify(ChangeType.Added, c);
            SetMode(data.mode == InventorySaveData.ModeBag ? InventoryMode.Bag : InventoryMode.Assemble);
            Scan(notify: false);
            return new LoadResult(true, null, cards.Count, skipped, deferred.Count);
        }
    }
}
