using System;

namespace Hangeul.App
{
    /// <summary>
    /// 색을 "#RRGGBB" 글자로 다루는 도우미(순수 규칙). 데이터 파일에는 유니티 <c>Color</c> 대신 이 글자로 색을 적는다
    /// (사람이 읽고 고치기 쉽고, 순수 어셈블리가 유니티를 몰라도 된다). 게임 쪽에서 <c>ColorUtility.TryParseHtmlString</c>으로 색이 된다.
    /// </summary>
    public static class ColorHex
    {
        /// <summary>속성 글자의 기본 색(주황).</summary>
        public const string DefaultElement = "#FF9E33";

        /// <summary>속성 글자 색의 추천 색(첫 번째는 기본 색). DB 화면과 인스펙터가 같은 버튼을 보여 준다.</summary>
        public static readonly string[] ElementPresets =
            { DefaultElement, "#FF5A4D", "#FFE04D", "#7CE35B", "#5FD8FF", "#5C8DFF", "#B678FF", "#FFFFFF" };

        /// <summary>
        /// '#' 유무와 대소문자, 짧은 3자리("#F80" = "#FF8800")를 받아 "#RRGGBB"(대문자)로 바로잡는다.
        /// 색이 아니면 false(hex는 null).
        /// </summary>
        public static bool TryNormalize(string text, out string hex)
        {
            hex = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string t = text.Trim();
            if (t.StartsWith("#", StringComparison.Ordinal)) t = t.Substring(1);
            if (t.Length == 3) t = new string(new[] { t[0], t[0], t[1], t[1], t[2], t[2] });
            if (t.Length != 6) return false;
            foreach (char c in t)
                if (!Uri.IsHexDigit(c)) return false;

            hex = "#" + t.ToUpperInvariant();
            return true;
        }

        /// <summary>바로잡은 색, 색이 아니면 fallback.</summary>
        public static string NormalizeOr(string text, string fallback) => TryNormalize(text, out string hex) ? hex : fallback;
    }
}
