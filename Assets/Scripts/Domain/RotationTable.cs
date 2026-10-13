using System.Collections.Generic;

namespace Hangeul.Domain
{
    /// <summary>자모의 시계방향 90° 단위 회전표(ROT4). 매핑이 없는 각도에서는 원래 자모를 유지한다.</summary>
    public static class RotationTable
    {
        const char NoMap = '\0';

        static readonly Dictionary<char, char[]> Rot4 = Build();

        static Dictionary<char, char[]> Build()
        {
            var d = new Dictionary<char, char[]>();
            foreach (var cyc in new[] { "ㅏㅜㅓㅗ", "ㅑㅠㅕㅛ" })
                for (int i = 0; i < 4; i++)
                {
                    var row = new char[4];
                    for (int k = 0; k < 4; k++) row[k] = cyc[(i + k) % 4];
                    d[cyc[i]] = row;
                }
            d['ㅣ'] = new[] { 'ㅣ', 'ㅡ', 'ㅣ', 'ㅡ' };
            d['ㅡ'] = new[] { 'ㅡ', 'ㅣ', 'ㅡ', 'ㅣ' };
            d['ㄱ'] = new[] { 'ㄱ', NoMap, 'ㄴ', NoMap };   // ┐ 를 180° 돌리면 └
            d['ㄴ'] = new[] { 'ㄴ', NoMap, 'ㄱ', NoMap };
            return d;
        }

        /// <summary>시계방향으로 quarterTurns×90° 돌린 자모. 음수/4 이상도 허용(모듈로 4). 매핑이 없으면 원래 자모.</summary>
        public static char Rotate(char jamo, int quarterTurns)
        {
            int n = ((quarterTurns % 4) + 4) % 4;
            if (Rot4.TryGetValue(jamo, out var row) && row[n] != NoMap) return row[n];
            return jamo;
        }

        /// <summary>모양이 바뀌는 가장 작은 시계방향 회전 횟수(1..3). 회전해도 안 바뀌면 0.</summary>
        public static int NextStep(char jamo)
        {
            for (int k = 1; k < 4; k++)
                if (Rotate(jamo, k) != jamo) return k;
            return 0;
        }

        public static bool CanRotate(char jamo) => NextStep(jamo) != 0;
    }
}
