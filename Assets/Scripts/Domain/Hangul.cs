using System;

namespace Hangeul.Domain
{
    /// <summary>한글 음절 ↔ 초성/중성/종성 변환. 자모는 호환 자모(ㄱ, ㅏ …)로 다룬다.</summary>
    public static class Hangul
    {
        public const string Cho = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
        public const string Jung = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";
        /// <summary>종성 목록. 인덱스 0은 받침 없음('\0').</summary>
        public const string Jong = "\0ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";

        /// <summary>받침 없음을 나타내는 값.</summary>
        public const char None = '\0';

        const int Base = 0xAC00;
        const int Last = 0xD7A3;

        public static bool IsSyllable(char c) => c >= Base && c <= Last;

        public static bool IsCho(char c) => c != None && Cho.IndexOf(c) >= 0;
        public static bool IsJung(char c) => c != None && Jung.IndexOf(c) >= 0;
        /// <summary>받침으로 쓸 수 있는 자모인지(None 제외).</summary>
        public static bool IsJong(char c) => c != None && Jong.IndexOf(c) > 0;
        public static bool IsConsonant(char c) => IsCho(c) || IsJong(c);

        /// <summary>음절을 분해한다. 받침이 없으면 jong은 <see cref="None"/>.</summary>
        public static bool TryDecompose(char syllable, out char cho, out char jung, out char jong)
        {
            cho = jung = jong = None;
            if (!IsSyllable(syllable)) return false;
            int n = syllable - Base;
            cho = Cho[n / 588];
            jung = Jung[n % 588 / 28];
            jong = Jong[n % 28];
            return true;
        }

        /// <summary>초/중/종성을 합친다. 종성이 없으면 <see cref="None"/>.</summary>
        public static bool TryCompose(char cho, char jung, char jong, out char syllable)
        {
            int c = cho == None ? -1 : Cho.IndexOf(cho);
            int v = jung == None ? -1 : Jung.IndexOf(jung);
            int t = Jong.IndexOf(jong);
            if (c < 0 || v < 0 || t < 0) { syllable = None; return false; }
            syllable = (char)(Base + (c * 21 + v) * 28 + t);
            return true;
        }

        /// <summary>합칠 수 없는 자모면 예외.</summary>
        public static char Compose(char cho, char jung, char jong = None)
        {
            if (!TryCompose(cho, jung, jong, out var s))
                throw new ArgumentException($"합칠 수 없는 자모: cho={(int)cho}, jung={(int)jung}, jong={(int)jong}");
            return s;
        }
    }
}
