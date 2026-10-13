using System;
using System.Collections.Generic;
using System.Linq;

namespace Hangeul.Domain
{
    /// <summary>카드가 놓이는 격자(1..20 × 1..20)와 빈 칸 탐색. 한 칸에 카드는 한 장.</summary>
    public sealed class GridModel
    {
        public const int MinSize = 1, MaxSize = 20;

        readonly List<Card> _cards = new List<Card>();

        public int Cols { get; private set; }
        public int Rows { get; private set; }
        public IReadOnlyList<Card> Cards => _cards;
        public int Capacity => Cols * Rows;

        public GridModel(int cols = 10, int rows = 10)
        {
            Cols = Clamp(cols, MinSize, MaxSize);
            Rows = Clamp(rows, MinSize, MaxSize);
        }

        public static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

        /// <summary>크기만 바꾼다. 카드 재배치/폐기는 호출한 쪽(InventoryModel.Resize) 책임.</summary>
        public void SetSize(int cols, int rows)
        {
            Cols = Clamp(cols, MinSize, MaxSize);
            Rows = Clamp(rows, MinSize, MaxSize);
        }

        public void Add(Card c) => _cards.Add(c);
        public bool Remove(Card c) => _cards.Remove(c);

        public bool InGrid(int col, int row) => col >= 0 && row >= 0 && col < Cols && row < Rows;

        public Card CardAt(int col, int row, Card except = null)
        {
            foreach (var o in _cards)
                if (o != except && o.Col == col && o.Row == row) return o;
            return null;
        }

        public bool IsFree(int col, int row) => InGrid(col, row) && CardAt(col, row) == null;

        /// <summary>
        /// (col,row) 주변 빈 칸. 우선순위 상대좌표 prefs를 먼저 보고, 없으면 맨해튼 거리가 가장 가까운 빈 칸
        /// (동률이면 행→열 순으로 먼저 만난 칸). 빈 칸이 없으면 false.
        /// </summary>
        public bool TryFindEmptyNear(int col, int row, out (int col, int row) cell, (int dc, int dr)[] prefs = null)
        {
            if (prefs != null)
                foreach (var (dc, dr) in prefs)
                    if (IsFree(col + dc, row + dr)) { cell = (col + dc, row + dr); return true; }

            bool found = false;
            int best = int.MaxValue;
            cell = default;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    if (CardAt(c, r) != null) continue;
                    int d = Math.Abs(c - col) + Math.Abs(r - row);
                    if (d < best) { best = d; cell = (c, r); found = true; }
                }
            return found;
        }

        /// <summary>
        /// 한 줄에 n칸 연속으로 빈 자리. anchor가 없으면 격자 가운데 줄부터, 있으면 anchor와 가까운 줄/위치부터.
        /// 반환은 시작 칸.
        /// </summary>
        public bool TryFindRun(int n, (int col, int row)? anchor, out (int col, int row) start)
        {
            start = default;
            if (n < 1 || n > Cols) return false;

            double anchorRow = anchor.HasValue ? anchor.Value.row : (Rows - 1) / 2.0;
            int anchorCol = anchor.HasValue ? anchor.Value.col : (Cols - n) / 2;   // (Cols-n)>=0 이므로 정수 나눗셈 = floor

            var rows = Enumerable.Range(0, Rows).OrderBy(r => Math.Abs(r - anchorRow)).ToList();   // OrderBy는 안정 정렬
            int ideal = Clamp(anchorCol, 0, Cols - n);
            var starts = Enumerable.Range(0, Cols - n + 1).OrderBy(x => Math.Abs(x - ideal)).ToList();

            foreach (int r in rows)
                foreach (int x in starts)
                {
                    bool ok = true;
                    for (int k = 0; k < n && ok; k++) ok = IsFree(x + k, r);
                    if (ok) { start = (x, r); return true; }
                }
            return false;
        }
    }
}
