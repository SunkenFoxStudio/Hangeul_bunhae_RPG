using System;
using System.Collections.Generic;
using Hangeul.Domain;

namespace Hangeul.App
{
    /// <summary>
    /// 인벤토리 세이브 데이터. JsonUtility로 그대로 직렬화할 수 있도록 public 필드와 문자열만 쓴다
    /// (자모는 한 글자 문자열, 비어 있으면 ""). 게임 세이브 안에 통째로 넣어 쓴다.
    /// </summary>
    [Serializable]
    public sealed class InventorySaveData
    {
        public const int CurrentVersion = 1;
        public const string ModeAssemble = "assemble";
        public const string ModeBag = "bag";

        public int schemaVersion = CurrentVersion;
        public int cols = 10;
        public int rows = 10;
        /// <summary>"assemble"(조립) 또는 "bag"(가방). 모르는 값은 조립으로 본다.</summary>
        public string mode = ModeAssemble;
        public List<CardSaveData> cards = new List<CardSaveData>();
    }

    [Serializable]
    public sealed class SlotsSaveData
    {
        public string cho = "";
        public string jung = "";
        public string jong = "";
    }

    [Serializable]
    public sealed class CardSaveData
    {
        public const string KindSyllable = "syl";
        public const string KindJamo = "jamo";

        public int id;
        public string kind = KindJamo;
        public string jamo = "";
        public SlotsSaveData s = new SlotsSaveData();   // 채워진 슬롯
        public SlotsSaveData g = new SlotsSaveData();   // 고스트 (떼어낸 흔적)
        public int col;
        public int row;

        public static CardSaveData From(Card c)
        {
            var d = new CardSaveData { id = c.Id, col = c.Col, row = c.Row };
            if (c.Kind == CardKind.Jamo)
            {
                d.kind = KindJamo;
                d.jamo = Str(c.Jamo);
            }
            else
            {
                d.kind = KindSyllable;
                d.s = Slots(c.S);
                d.g = Slots(c.G);
            }
            return d;
        }

        /// <summary>검증하고 카드로 바꾼다. 한글 규칙에 맞지 않는 데이터면 false (id/좌표는 호출한 쪽이 보정).</summary>
        public bool TryToCard(out Card card)
        {
            card = null;
            if (kind == KindJamo)
            {
                if (!One(jamo, out char j) || j == 0 || !(Hangul.IsCho(j) || Hangul.IsJung(j) || Hangul.IsJong(j))) return false;
                card = Card.NewJamo(id, j, col, row);
                return true;
            }
            if (kind != KindSyllable) return false;

            if (!Parse(s, out Parts sp) || !Parse(g, out Parts gp)) return false;
            // 고스트는 비어 있는 슬롯에만 의미가 있다
            if (sp.Cho != 0) gp.Cho = Hangul.None;
            if (sp.Jung != 0) gp.Jung = Hangul.None;
            if (sp.Jong != 0) gp.Jong = Hangul.None;

            if (sp.Count < 2) return false;                         // 슬롯이 하나 이하면 음절 카드가 아니다
            char cho = sp.Cho != 0 ? sp.Cho : gp.Cho;
            char jung = sp.Jung != 0 ? sp.Jung : gp.Jung;
            char jong = sp.Jong != 0 ? sp.Jong : gp.Jong;
            if (!Hangul.TryCompose(cho, jung, jong, out _)) return false;   // 화면에 그릴 글자가 있어야 한다

            card = new Card { Id = id, Kind = CardKind.Syllable, S = sp, G = gp, Col = col, Row = row };
            return true;
        }

        static string Str(char c) => c == 0 ? "" : c.ToString();

        static SlotsSaveData Slots(Parts p) => new SlotsSaveData { cho = Str(p.Cho), jung = Str(p.Jung), jong = Str(p.Jong) };

        static bool One(string s, out char c)
        {
            c = Hangul.None;
            if (string.IsNullOrEmpty(s)) return true;
            if (s.Length != 1) return false;
            c = s[0];
            return true;
        }

        static bool Parse(SlotsSaveData d, out Parts p)
        {
            p = default;
            if (d == null) return true;
            if (!One(d.cho, out p.Cho) || !One(d.jung, out p.Jung) || !One(d.jong, out p.Jong)) return false;
            return (p.Cho == 0 || Hangul.IsCho(p.Cho))
                && (p.Jung == 0 || Hangul.IsJung(p.Jung))
                && (p.Jong == 0 || Hangul.IsJong(p.Jong));
        }
    }

    public readonly struct LoadResult
    {
        public readonly bool Ok;
        public readonly string Error;
        public readonly int Loaded;
        public readonly int Skipped;     // 규칙에 맞지 않거나 자리가 없어 버린 카드
        public readonly int Relocated;   // 범위 밖/겹침 때문에 옮긴 카드

        public LoadResult(bool ok, string error, int loaded, int skipped, int relocated)
        {
            Ok = ok; Error = error; Loaded = loaded; Skipped = skipped; Relocated = relocated;
        }

        public static LoadResult Fail(string error) => new LoadResult(false, error, 0, 0, 0);
    }
}
