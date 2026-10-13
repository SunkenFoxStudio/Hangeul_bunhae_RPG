using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hangeul.Domain;

namespace Hangeul.App
{
    /// <summary>격자에서 찾은 완성 단어 하나. 같은 행에서 Col부터 Length칸을 차지한다.</summary>
    public readonly struct WordMatch
    {
        public readonly string Word;
        public readonly int Row, Col, Length;
        /// <summary>같은 단어가 같은 카드들로 같은 자리에 있는지 가려내는 키(행:열:단어:카드아이디들).</summary>
        public readonly string Key;

        public WordMatch(string word, int row, int col, int length, string key)
        {
            Word = word; Row = row; Col = col; Length = length; Key = key;
        }
    }

    /// <summary>
    /// 단어 완성 감지 (HTML의 scanMatches).
    /// 같은 행에서 왼쪽→오른쪽으로 이어진 <see cref="Card.IsComplete"/> 음절 카드의 글자를 이어 붙여 단어 목록과 대조한다.
    /// 세로/대각선은 인정하지 않고, 겹치는 매치는 글자 수가 가장 많은 것만 보인다(술병 → '술','병'은 숨김).
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
        /// 지금 격자에서 완성돼 있는 단어를 전부 찾는다(겹쳐서 숨겨질 것까지 포함). 스캐너의 상태(알림 기준·Shown)는 바꾸지 않는다.
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

            // 겹치는 매치는 긴 것 우선(같은 길이면 위쪽·왼쪽)
            var used = new HashSet<(int, int)>();
            var shown = new List<WordMatch>();
            foreach (var m in found.OrderByDescending(m => m.Length).ThenBy(m => m.Row).ThenBy(m => m.Col))
            {
                bool overlap = false;
                for (int k = 0; k < m.Length && !overlap; k++) overlap = used.Contains((m.Row, m.Col + k));
                if (overlap) continue;
                for (int k = 0; k < m.Length; k++) used.Add((m.Row, m.Col + k));
                shown.Add(m);
            }

            var fresh = notify ? shown.Where(m => !_previous.Contains(m.Key)).ToList() : new List<WordMatch>();
            _previous = current;
            _shown = shown;
            return fresh;
        }
    }
}
