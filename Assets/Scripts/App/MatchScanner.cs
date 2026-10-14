using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hangeul.Domain;

namespace Hangeul.App
{
    /// <summary>
    /// 격자에서 찾은 완성 단어 하나.
    /// 보통은 같은 행에서 Col부터 Length칸을 차지한다(일렬).
    /// <see cref="PrefixLength"/>가 0보다 크면 "위 배치"다: Row/Col/Length는 본체 단어(아래 줄)이고,
    /// 그 바로 윗줄(Row-1)의 Col부터 PrefixLength칸이 접두어다(접두어의 첫 글자가 본체의 첫 글자 바로 위, 왼쪽 정렬).
    /// </summary>
    public readonly struct WordMatch
    {
        /// <summary>단어 전체 이름. 위 배치면 접두어 + 본체(예: 불화살).</summary>
        public readonly string Word;
        public readonly int Row, Col, Length;
        /// <summary>0이면 일렬. 아니면 본체 윗줄에 얹힌 접두어의 글자 수.</summary>
        public readonly int PrefixLength;
        /// <summary>같은 단어가 같은 카드들로 같은 자리에 있는지 가려내는 키(행:열:단어:카드아이디들).</summary>
        public readonly string Key;

        public bool IsStacked => PrefixLength > 0;
        /// <summary>단어가 차지하는 카드 수(접두어 포함).</summary>
        public int TotalLength => Length + PrefixLength;

        public WordMatch(string word, int row, int col, int length, string key, int prefixLength = 0)
        {
            Word = word; Row = row; Col = col; Length = length; Key = key; PrefixLength = prefixLength;
        }

        /// <summary>단어를 이루는 칸들을 읽는 순서(접두어 → 본체)로.</summary>
        public IEnumerable<(int col, int row)> Cells()
        {
            for (int k = 0; k < PrefixLength; k++) yield return (Col + k, Row - 1);
            for (int k = 0; k < Length; k++) yield return (Col + k, Row);
        }
    }

    /// <summary>
    /// 단어 완성 감지 (HTML의 scanMatches).
    /// 기본: 같은 행에서 왼쪽→오른쪽으로 이어진 <see cref="Card.IsComplete"/> 음절 카드의 글자를 이어 붙여 단어 목록과 대조한다.
    /// 예외(접두어 위 배치): DB의 접두어 규칙(<see cref="ItemWordSet.PrefixesFor"/>)에 있는 접두어는 본체 단어의 <b>바로 윗줄</b>에
    /// 첫 글자를 맞춰(왼쪽 정렬) 얹어도 한 덩어리 단어로 인정한다. 그 밖의 세로/대각선은 인정하지 않는다.
    /// 겹치는 매치는 글자 수가 가장 많은 것만 보인다(술병 → '술','병'은 숨김).
    /// 일렬(접두어가 본체 왼쪽에 이어 붙음)과 위 배치가 겹치면 <b>일렬이 우선</b>한다.
    /// 새로 완성된 것만 알린다: 이전 스캔의 매치 키(숨겨진 것 포함)와 비교한다.
    /// </summary>
    public sealed class MatchScanner
    {
        ItemWordSet _words = new ItemWordSet();
        HashSet<string> _previous = new HashSet<string>();
        List<WordMatch> _shown = new List<WordMatch>();

        /// <summary>마지막 스캔에서 화면에 보이는(겹치지 않는 가장 긴) 매치들. 가방 모드의 스프라이트 대체에 쓴다.</summary>
        public IReadOnlyList<WordMatch> Shown => _shown;

        public void SetWords(ItemWordSet words) => _words = words ?? new ItemWordSet();

        /// <summary>
        /// 지금 격자에서 완성돼 있는 단어를 전부 찾는다(겹쳐서 숨겨질 것까지 포함, 위 배치 포함). 스캐너의 상태(알림 기준·Shown)는 바꾸지 않는다.
        /// 가방 흔들기처럼 "이미 완성된 단어의 카드"를 보호해야 하는 곳에서 쓴다.
        /// </summary>
        public List<WordMatch> FindAll(GridModel grid)
        {
            var found = new List<WordMatch>();
            if (_words.Count == 0) return found;

            var at = new Dictionary<int, Card>();
            foreach (var c in grid.Cards)
                if (grid.InGrid(c.Col, c.Row)) at[c.Row * grid.Cols + c.Col] = c;

            var word = new StringBuilder();
            var ids = new StringBuilder();
            for (int r = 0; r < grid.Rows; r++)
                for (int c0 = 0; c0 < grid.Cols; c0++)
                {
                    word.Clear(); ids.Clear();
                    for (int c1 = c0; c1 < grid.Cols; c1++)
                    {
                        if (!at.TryGetValue(r * grid.Cols + c1, out var card) || !card.IsComplete) break;
                        word.Append(card.Letter.Value);
                        ids.Append(card.Id).Append(',');
                        if (word.Length > _words.MaxLength) break;

                        string w = word.ToString();
                        if (!_words.Contains(w)) continue;
                        found.Add(new WordMatch(w, r, c0, c1 - c0 + 1, $"{r}:{c0}:{w}:{ids}"));
                    }
                }

            // 위 배치: 본체 단어(일렬로 찾은 것) 바로 윗줄에 접두어 글자들이 첫 글자부터 맞춰 놓여 있는지
            var stacked = new List<WordMatch>();
            foreach (var b in found)
            {
                if (b.Row == 0) continue;
                foreach (var prefix in _words.PrefixesFor(b.Word))
                {
                    if (b.Col + prefix.Length > grid.Cols) continue;
                    string full = prefix + b.Word;
                    if (!_words.Contains(full)) continue;

                    var prefixIds = new StringBuilder();
                    bool ok = true;
                    for (int k = 0; k < prefix.Length && ok; k++)
                    {
                        ok = at.TryGetValue((b.Row - 1) * grid.Cols + b.Col + k, out var pc) && pc.IsComplete && pc.Letter == prefix[k];
                        if (ok) prefixIds.Append(pc.Id).Append(',');
                    }
                    if (ok) stacked.Add(new WordMatch(full, b.Row, b.Col, b.Length, $"{b.Key}^{prefixIds}", prefix.Length));
                }
            }
            found.AddRange(stacked);
            return found;
        }

        /// <summary>
        /// 격자를 스캔한다. notify=true면 이번에 새로 보이게 된 매치를 돌려주고, false면 빈 목록
        /// (단어 추가/격자 크기 변경/불러오기 직후처럼 이미 완성돼 있던 것을 알리지 않을 때).
        /// </summary>
        public List<WordMatch> Scan(GridModel grid, bool notify)
        {
            var found = FindAll(grid);
            var current = new HashSet<string>(found.Select(m => m.Key));

            // 1) 일렬 매치: 겹치면 긴 것 우선(같은 길이면 위쪽·왼쪽)
            var used = new HashSet<(int, int)>();
            var shown = new List<WordMatch>();
            foreach (var m in found.Where(m => !m.IsStacked).OrderByDescending(m => m.Length).ThenBy(m => m.Row).ThenBy(m => m.Col))
            {
                bool overlap = false;
                for (int k = 0; k < m.Length && !overlap; k++) overlap = used.Contains((m.Row, m.Col + k));
                if (overlap) continue;
                for (int k = 0; k < m.Length; k++) used.Add((m.Row, m.Col + k));
                shown.Add(m);
            }

            // 2) 위 배치: 본체가 "혼자 보이는 일렬 단어"일 때만 접두어를 얹어 하나로 합친다(일렬 우선).
            //    접두어 칸이 더 긴 일렬 단어에 쓰이고 있으면 거절한다. 접두어 칸을 혼자 차지하던 일렬 단어(예: '불')는 합쳐지며 사라진다.
            foreach (var s in found.Where(m => m.IsStacked).OrderByDescending(m => m.TotalLength).ThenBy(m => m.Row).ThenBy(m => m.Col))
            {
                string baseWord = s.Word.Substring(s.PrefixLength);
                int baseIdx = shown.FindIndex(m => !m.IsStacked && m.Row == s.Row && m.Col == s.Col && m.Length == s.Length && m.Word == baseWord);
                if (baseIdx < 0) continue;

                var absorbed = new List<int> { baseIdx };
                bool ok = true;
                for (int k = 0; k < s.PrefixLength && ok; k++)
                {
                    int idx = shown.FindIndex(m => CoversCell(m, s.Col + k, s.Row - 1));
                    if (idx < 0) continue;                                     // 빈 칸이 아니라 카드가 있으나 단어가 아닌 경우
                    var other = shown[idx];
                    // 접두어 글자들을 정확히 그대로 이루는 일렬 단어 하나만 흡수할 수 있다
                    bool exactlyPrefix = !other.IsStacked && other.Row == s.Row - 1 && other.Col == s.Col && other.Length == s.PrefixLength;
                    if (!exactlyPrefix) ok = false;
                    else if (!absorbed.Contains(idx)) absorbed.Add(idx);
                }
                if (!ok) continue;

                foreach (int idx in absorbed.OrderByDescending(i => i)) shown.RemoveAt(idx);
                shown.Add(s);
            }

            var fresh = notify ? shown.Where(m => !_previous.Contains(m.Key)).ToList() : new List<WordMatch>();
            _previous = current;
            _shown = shown;
            return fresh;
        }

        static bool CoversCell(WordMatch m, int col, int row)
        {
            foreach (var (c, r) in m.Cells())
                if (c == col && r == row) return true;
            return false;
        }
    }
}
