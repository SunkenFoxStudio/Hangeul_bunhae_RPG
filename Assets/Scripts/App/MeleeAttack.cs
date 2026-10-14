using System;

namespace Hangeul.App
{
    public enum AttackPhase
    {
        Windup,     // 준비 동작: 아직 맞지 않는다
        Active,     // 공격 판정이 켜져 있다
        Recovery,   // 마무리 동작: 판정은 꺼졌지만 아직 움직이거나 다시 공격할 수 없다
        Done,
    }

    /// <summary>
    /// 근접 공격 한 번의 시간표(준비 → 판정 → 마무리). 애니메이션 프레임 수와 무관하게 시간(초)으로만 정해서
    /// 스프라이트가 한 장뿐이어도, 나중에 애니메이션이 들어와도 같은 타이밍으로 동작한다.
    /// </summary>
    public readonly struct AttackTimeline
    {
        public readonly float Windup, Active, Recovery;

        public AttackTimeline(float windup, float active, float recovery)
        {
            Windup = Math.Max(0f, windup);
            Active = Math.Max(0.01f, active);
            Recovery = Math.Max(0f, recovery);
        }

        public float Total => Windup + Active + Recovery;

        /// <summary>공격을 시작하고 t초가 지났을 때의 단계.</summary>
        public AttackPhase PhaseAt(float t)
        {
            if (t < Windup) return AttackPhase.Windup;
            if (t < Windup + Active) return AttackPhase.Active;
            if (t < Total) return AttackPhase.Recovery;
            return AttackPhase.Done;
        }
    }

    /// <summary>4방향 근접 공격의 판정 상자 계산(순수 기하). 상자는 공격 시작점 앞쪽으로 reach만큼 뻗는다.</summary>
    public static class MeleeBox
    {
        /// <summary>
        /// 시작점(originX, originY)에서 방향(dirX, dirY: 위/아래/왼쪽/오른쪽 중 하나)으로 reach만큼 뻗는 상자의 중심과 크기.
        /// 좌우 방향이면 가로가 reach, 상하 방향이면 세로가 reach이고 나머지 한 변은 width다.
        /// </summary>
        public static (float cx, float cy, float w, float h) Compute(float dirX, float dirY, float originX, float originY, float reach, float width)
        {
            bool horizontal = Math.Abs(dirX) > Math.Abs(dirY);
            float length = Math.Max(0f, reach), side = Math.Max(0f, width);
            float sx = horizontal ? Math.Sign(dirX) : 0f, sy = horizontal ? 0f : Math.Sign(dirY);
            return (originX + sx * length / 2f, originY + sy * length / 2f, horizontal ? length : side, horizontal ? side : length);
        }
    }
}
