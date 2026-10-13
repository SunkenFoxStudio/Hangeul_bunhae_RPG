namespace Hangeul.Domain
{
    public enum CombineOutcome { Rejected, Ok, Banned }

    /// <summary>결합 결과. IsJamo면 Jamo가 새 자모 카드, 아니면 S/G가 새 음절 카드.</summary>
    public struct CombineResult
    {
        public bool IsJamo;
        public char Jamo;
        public Parts S, G;
    }

    /// <summary>드래그 방향 우선순위(HTML의 PREF)와 분리할 슬롯 선택.</summary>
    public static class DetachRules
    {
        static readonly (int, int)[] L = { (-1, 0), (0, -1), (0, 1), (1, 0) };
        static readonly (int, int)[] R = { (1, 0), (0, -1), (0, 1), (-1, 0) };
        static readonly (int, int)[] U = { (0, -1), (-1, 0), (1, 0), (0, 1) };
        static readonly (int, int)[] D = { (0, 1), (-1, 0), (1, 0), (0, -1) };

        /// <summary>끌어당긴 방향 쪽 이웃 칸부터 보는 우선순위.</summary>
        public static (int dc, int dr)[] Prefs(PullDir dir)
            => dir == PullDir.L ? L : dir == PullDir.R ? R : dir == PullDir.U ? U : D;

        /// <summary>방향에 대해 떨어져 나올 슬롯(앞에서부터 채워진 첫 슬롯). 없으면 false.</summary>
        public static bool TryPickSlot(Card c, PullDir dir, out Slot slot)
        {
            slot = Slot.Cho;
            if (c.Kind != CardKind.Syllable) return false;
            var layout = SyllableLayout.LayoutOf(c.DisplayJung);
            foreach (var s in SyllableLayout.Pull(layout, dir))
                if (c.S[s] != 0) { slot = s; return true; }
            return false;
        }
    }

    /// <summary>카드 결합 규칙 (HTML의 combine). 상태를 바꾸지 않고 결과만 계산한다.</summary>
    public static class CardOps
    {
        static bool IsHoPair(char a, char b) => (a == 'ㅗ' && b == 'ㅇ') || (a == 'ㅇ' && b == 'ㅗ');

        static CombineResult Jam(char j) => new CombineResult { IsJamo = true, Jamo = j };

        static CombineResult Syl(Parts s, Parts g) => new CombineResult { S = s, G = g };

        /// <summary>
        /// 드래그한 자모 카드 <paramref name="dragged"/>를 <paramref name="target"/>에 합친다.
        /// <paramref name="hoHold"/>: ㅗ·ㅇ 쌍 위에서 0.6초 홀드가 성립했는지(성립해야 ㅎ, 아니면 '오').
        /// </summary>
        public static CombineOutcome TryCombine(Card dragged, Card target, bool hoHold, out CombineResult result)
        {
            result = default;
            if (dragged.Kind != CardKind.Jamo) return CombineOutcome.Rejected;
            char j = dragged.Jamo;

            if (target.Kind == CardKind.Jamo)
            {
                char k = target.Jamo;
                if (hoHold && IsHoPair(j, k)) { result = Jam('ㅎ'); return CombineOutcome.Ok; }

                if ((Hangul.IsJung(j) && Hangul.IsJung(k)) || (Hangul.IsConsonant(j) && Hangul.IsConsonant(k)))
                {
                    if (!JamoTables.TryCombine(j, k, out var m)) return CombineOutcome.Rejected;
                    result = Jam(m);
                    return CombineOutcome.Ok;
                }

                Parts p = default;
                if (Hangul.IsCho(j) && Hangul.IsJung(k)) { p.Cho = j; p.Jung = k; }
                else if (Hangul.IsJung(j) && Hangul.IsCho(k)) { p.Cho = k; p.Jung = j; }
                else return CombineOutcome.Rejected;
                return Finish(Syl(p, default), out result);
            }

            Parts s = target.S, g = target.G;
            if (Hangul.IsJung(j))
            {
                if (s.Jung == 0) s.Jung = j;
                else if (JamoTables.TryCombineOrdered(s.Jung, j, out var vv)) s.Jung = vv;
                else return CombineOutcome.Rejected;
                g.Jung = Hangul.None;
            }
            else
            {
                if (s.Cho == 0)
                {
                    if (!Hangul.IsCho(j)) return CombineOutcome.Rejected;
                    s.Cho = j; g.Cho = Hangul.None;
                }
                else if (s.Jung == 0) return CombineOutcome.Rejected;
                else if (s.Jong == 0 && Hangul.IsJong(j)) { s.Jong = j; g.Jong = Hangul.None; }
                else if (s.Jong != 0 && JamoTables.TryCombineOrdered(s.Jong, j, out var cj) && Hangul.IsJong(cj)) s.Jong = cj;
                else if (JamoTables.TryCombineOrdered(s.Cho, j, out var cc) && Hangul.IsCho(cc)) s.Cho = cc;
                else return CombineOutcome.Rejected;
            }
            return Finish(Syl(s, g), out result);
        }

        static CombineOutcome Finish(CombineResult r, out CombineResult result)
        {
            result = r;
            return ForbiddenSyllables.IsBanned(r.S) ? CombineOutcome.Banned : CombineOutcome.Ok;
        }
    }
}
