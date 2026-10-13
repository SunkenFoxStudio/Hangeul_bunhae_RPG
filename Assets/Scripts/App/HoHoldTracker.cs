using Hangeul.Domain;

namespace Hangeul.App
{
    /// <summary>
    /// ㅗ·ㅇ 쌍 위에서 드래그 카드가 같은 대상 위에 <see cref="HoldSeconds"/> 넘게 머물면 <see cref="On"/>이 켜진다
    /// (켜진 채로 드롭해야 'ㅎ', 아니면 '오'). 시간은 Tick(dt)으로 주입한다.
    /// </summary>
    public sealed class HoHoldTracker
    {
        public const float HoldSeconds = 0.6f;

        Card _dragged, _target;
        float _time;

        public bool On { get; private set; }

        /// <summary>이번 프레임 드래그 카드와 그 아래 대상(없으면 null).</summary>
        public void Tick(Card dragged, Card target, float dt)
        {
            if (dragged == null || target == null || !IsHoPair(dragged, target)) { Reset(); return; }

            if (dragged != _dragged || target != _target)
            {
                Reset();
                _dragged = dragged;
                _target = target;
            }
            _time += dt;
            if (_time >= HoldSeconds) On = true;
        }

        public void Reset()
        {
            _dragged = _target = null;
            _time = 0f;
            On = false;
        }

        static bool IsHoPair(Card a, Card b)
            => a.Kind == CardKind.Jamo && b.Kind == CardKind.Jamo
               && ((a.Jamo == 'ㅗ' && b.Jamo == 'ㅇ') || (a.Jamo == 'ㅇ' && b.Jamo == 'ㅗ'));
    }
}
