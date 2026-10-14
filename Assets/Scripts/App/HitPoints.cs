using System;

namespace Hangeul.App
{
    /// <summary>
    /// 체력 값(정수). 하트 방식처럼 정수에서 1, 2씩 깎는다. 순수 규칙만 담고(시간·무적·팀은 Unity 쪽 <c>Health</c>가 맡는다) EditMode에서 검증한다.
    /// </summary>
    public sealed class HitPoints
    {
        public int Max { get; private set; }
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;
        public bool IsFull => Current >= Max;

        public HitPoints(int max, int? current = null)
        {
            Max = Math.Max(1, max);
            Current = Math.Max(0, Math.Min(Max, current ?? Max));
        }

        /// <summary>피해를 입는다. 실제로 깎인 양을 돌려준다(0 이하의 피해나 이미 죽은 뒤에는 0, 남은 체력보다 크면 남은 만큼).</summary>
        public int Damage(int amount)
        {
            if (amount <= 0 || IsDead) return 0;
            int taken = Math.Min(amount, Current);
            Current -= taken;
            return taken;
        }

        /// <summary>회복한다. 실제로 늘어난 양을 돌려준다. 죽은 뒤에는 회복되지 않는다(<see cref="Revive"/>로만 되살린다).</summary>
        public int Heal(int amount)
        {
            if (amount <= 0 || IsDead) return 0;
            int healed = Math.Min(amount, Max - Current);
            Current += healed;
            return healed;
        }

        /// <summary>되살린다. hp를 주지 않으면 가득 채운다.</summary>
        public void Revive(int? hp = null) => Current = Math.Max(1, Math.Min(Max, hp ?? Max));

        /// <summary>최대 체력을 바꾼다. fill이면 가득 채우고, 아니면 현재 체력을 새 최대치 안으로 맞춘다(죽은 상태는 그대로).</summary>
        public void SetMax(int max, bool fill = false)
        {
            Max = Math.Max(1, max);
            Current = fill ? Max : Math.Min(Current, Max);
        }
    }
}
