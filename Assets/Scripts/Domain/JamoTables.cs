using System.Collections.Generic;

namespace Hangeul.Domain
{
    /// <summary>자모 결합표(CC 자음+자음, VV 모음+모음)와 그 역(분리). 결합은 순서 무관(양방향).</summary>
    public static class JamoTables
    {
        static readonly Dictionary<string, char> Cc = new Dictionary<string, char>
        {
            ["ㄱㄱ"] = 'ㄲ', ["ㄷㄷ"] = 'ㄸ', ["ㅂㅂ"] = 'ㅃ', ["ㅅㅅ"] = 'ㅆ', ["ㅈㅈ"] = 'ㅉ',
            ["ㄱㅅ"] = 'ㄳ', ["ㄴㅈ"] = 'ㄵ', ["ㄴㅎ"] = 'ㄶ', ["ㄹㄱ"] = 'ㄺ', ["ㄹㅁ"] = 'ㄻ',
            ["ㄹㅂ"] = 'ㄼ', ["ㄹㅅ"] = 'ㄽ', ["ㄹㅌ"] = 'ㄾ', ["ㄹㅍ"] = 'ㄿ', ["ㄹㅎ"] = 'ㅀ', ["ㅂㅅ"] = 'ㅄ',
        };

        static readonly Dictionary<string, char> Vv = new Dictionary<string, char>
        {
            ["ㅗㅏ"] = 'ㅘ', ["ㅗㅐ"] = 'ㅙ', ["ㅗㅣ"] = 'ㅚ', ["ㅜㅓ"] = 'ㅝ', ["ㅜㅔ"] = 'ㅞ',
            ["ㅜㅣ"] = 'ㅟ', ["ㅡㅣ"] = 'ㅢ', ["ㅏㅣ"] = 'ㅐ', ["ㅓㅣ"] = 'ㅔ', ["ㅑㅣ"] = 'ㅒ',
            ["ㅕㅣ"] = 'ㅖ', ["ㅏㅏ"] = 'ㅑ', ["ㅓㅓ"] = 'ㅕ', ["ㅗㅗ"] = 'ㅛ', ["ㅜㅜ"] = 'ㅠ',
        };

        static readonly Dictionary<char, (char, char)> Split = BuildSplit();

        static Dictionary<char, (char, char)> BuildSplit()
        {
            var d = new Dictionary<char, (char, char)>();
            foreach (var t in new[] { Cc, Vv })
                foreach (var kv in t) d[kv.Value] = (kv.Key[0], kv.Key[1]);
            d['ㅎ'] = ('ㅗ', 'ㅇ');   // ㅎ = 위의 ㅗ + 아래의 ㅇ (first가 위쪽)
            return d;
        }

        /// <summary>두 자음 또는 두 모음을 합친다(a,b 순서 무관). 종류가 섞이면 실패. ㅎ은 여기서 만들지 않는다(홀드 규칙).</summary>
        public static bool TryCombine(char a, char b, out char result)
        {
            if (Hangul.IsJung(a) && Hangul.IsJung(b)) return Lookup(Vv, a, b, out result);
            if (Hangul.IsConsonant(a) && Hangul.IsConsonant(b)) return Lookup(Cc, a, b, out result);
            result = Hangul.None;
            return false;
        }

        /// <summary>순서를 지켜 합친다(first 다음 second). 겹받침/쌍자음 붙이기처럼 HTML이 한 방향만 보는 곳에서 쓴다.</summary>
        public static bool TryCombineOrdered(char first, char second, out char result)
        {
            string key = new string(new[] { first, second });
            return Vv.TryGetValue(key, out result) || Cc.TryGetValue(key, out result);
        }

        static bool Lookup(Dictionary<string, char> t, char a, char b, out char result)
            => t.TryGetValue(new string(new[] { a, b }), out result) || t.TryGetValue(new string(new[] { b, a }), out result);

        /// <summary>겹자모를 두 조각으로 나눈다(합친 순서 그대로). ㅎ은 (ㅗ, ㅇ).</summary>
        public static bool TrySplit(char combined, out (char first, char second) parts)
        {
            if (Split.TryGetValue(combined, out var p)) { parts = p; return true; }
            parts = default;
            return false;
        }

        public static bool CanSplit(char combined) => Split.ContainsKey(combined);
    }
}
