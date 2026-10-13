namespace Hangeul.Domain
{
    public enum CardKind { Syllable, Jamo }

    /// <summary>초/중/종성 세 슬롯. 비어 있으면 '\0'.</summary>
    public struct Parts
    {
        public char Cho, Jung, Jong;

        public char this[Slot slot]
        {
            get => slot == Slot.Cho ? Cho : slot == Slot.Jung ? Jung : Jong;
            set
            {
                if (slot == Slot.Cho) Cho = value;
                else if (slot == Slot.Jung) Jung = value;
                else Jong = value;
            }
        }

        public int Count => (Cho != 0 ? 1 : 0) + (Jung != 0 ? 1 : 0) + (Jong != 0 ? 1 : 0);
    }

    /// <summary>
    /// 격자 위의 카드 한 장(순수 데이터). 자모 카드는 <see cref="Jamo"/>, 음절 카드는 슬롯 <see cref="S"/>와 고스트 <see cref="G"/>.
    /// 고스트 = 떼어낸 자모의 흔적(반투명 표시). 종성을 떼면 고스트를 남기지 않는다.
    /// </summary>
    public sealed class Card
    {
        public int Id;
        public CardKind Kind;
        public char Jamo;
        public Parts S;
        public Parts G;
        public int Col, Row;

        public static Card NewJamo(int id, char jamo, int col, int row)
            => new Card { Id = id, Kind = CardKind.Jamo, Jamo = jamo, Col = col, Row = row };

        public static Card NewSyllable(int id, char cho, char jung, char jong, int col, int row)
            => new Card { Id = id, Kind = CardKind.Syllable, S = new Parts { Cho = cho, Jung = jung, Jong = jong }, Col = col, Row = row };

        /// <summary>초성·중성이 있고 받침 고스트가 남아 있지 않은, 빈 슬롯 없는 음절 카드.</summary>
        public bool IsComplete => Kind == CardKind.Syllable && S.Cho != 0 && S.Jung != 0 && (S.Jong != 0 || G.Jong == 0);

        /// <summary>완성 카드의 글자. 완성이 아니면 null.</summary>
        public char? Letter => IsComplete ? Hangul.Compose(S.Cho, S.Jung, S.Jong) : (char?)null;

        /// <summary>슬롯이 비어 있으면 고스트로 채워서 그린 모양(화면 표시용). 음절 카드만 의미가 있다.</summary>
        public char DisplayJung => S.Jung != 0 ? S.Jung : G.Jung;
    }

    /// <summary>만들 수 없는 글자(오타). 결합·입력 어느 쪽으로도 만들지 못한다.</summary>
    public static class ForbiddenSyllables
    {
        const char Banned = '됬';

        public static bool IsBanned(Parts s)
            => s.Cho != 0 && s.Jung != 0 && Hangul.TryCompose(s.Cho, s.Jung, s.Jong, out var c) && c == Banned;

        public static bool IsBanned(char syllable) => syllable == Banned;
    }
}
