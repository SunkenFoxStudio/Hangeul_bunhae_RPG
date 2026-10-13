namespace Hangeul.Domain
{
    public enum VowelLayout { V, H, C }   // V 세로(ㅏ), H 가로(ㅗ), C 복합(ㅘ)
    public enum Slot { Cho, Jung, Jong }
    public enum PullDir { L, R, U, D }

    /// <summary>음절 카드 안에서 각 자모가 차지하는 영역(0~100 좌표계)과 드래그 방향별 분리 슬롯.</summary>
    public static class SyllableLayout
    {
        /// <summary>영역 사각형. 좌표계는 0~100(왼쪽 위 원점).</summary>
        public readonly struct Rect
        {
            public readonly int X, Y, W, H;
            public Rect(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
        }

        public static VowelLayout LayoutOf(char vowel)
        {
            if ("ㅏㅐㅑㅒㅓㅔㅕㅖㅣ".IndexOf(vowel) >= 0) return VowelLayout.V;
            if ("ㅗㅛㅜㅠㅡ".IndexOf(vowel) >= 0) return VowelLayout.H;
            return VowelLayout.C;
        }

        static Rect[] R(params int[] v)
        {
            var r = new Rect[v.Length / 4];
            for (int i = 0; i < r.Length; i++) r[i] = new Rect(v[i * 4], v[i * 4 + 1], v[i * 4 + 2], v[i * 4 + 3]);
            return r;
        }

        /// <summary>슬롯이 차지하는 영역들(중성은 복합 모음에서 2개). 받침이 없는 레이아웃의 Jong은 빈 배열.</summary>
        public static Rect[] Regions(VowelLayout layout, bool hasJong, Slot slot)
        {
            switch (layout)
            {
                case VowelLayout.V:
                    if (hasJong) return slot == Slot.Cho ? R(0, 0, 56, 62) : slot == Slot.Jung ? R(56, 0, 44, 62) : R(0, 62, 100, 38);
                    return slot == Slot.Cho ? R(0, 0, 56, 100) : slot == Slot.Jung ? R(56, 0, 44, 100) : R();
                case VowelLayout.H:
                    if (hasJong) return slot == Slot.Cho ? R(0, 0, 100, 42) : slot == Slot.Jung ? R(0, 42, 100, 22) : R(0, 64, 100, 36);
                    return slot == Slot.Cho ? R(0, 0, 100, 48) : slot == Slot.Jung ? R(0, 48, 100, 52) : R();
                default:
                    if (hasJong) return slot == Slot.Cho ? R(0, 0, 48, 38) : slot == Slot.Jung ? R(48, 0, 52, 62, 0, 38, 48, 24) : R(0, 62, 100, 38);
                    return slot == Slot.Cho ? R(0, 0, 48, 48) : slot == Slot.Jung ? R(48, 0, 52, 100, 0, 48, 48, 52) : R();
            }
        }

        static readonly Slot[] NoSlots = new Slot[0];

        /// <summary>드래그 방향별로 떨어져 나올 슬롯 후보(앞쪽 우선). 채워진 첫 슬롯이 선택된다.</summary>
        public static Slot[] Pull(VowelLayout layout, PullDir dir)
        {
            switch (layout)
            {
                case VowelLayout.V:
                    return dir == PullDir.L || dir == PullDir.U ? new[] { Slot.Cho }
                         : dir == PullDir.R ? new[] { Slot.Jung } : new[] { Slot.Jong };
                case VowelLayout.H:
                    return dir == PullDir.U ? new[] { Slot.Cho, Slot.Jung }
                         : dir == PullDir.D ? new[] { Slot.Jong, Slot.Jung } : NoSlots;
                default:
                    return dir == PullDir.L || dir == PullDir.U ? new[] { Slot.Cho }
                         : dir == PullDir.R ? new[] { Slot.Jung } : new[] { Slot.Jong, Slot.Jung };
            }
        }
    }
}
