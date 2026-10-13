using Hangeul.Domain;

namespace Hangeul.App
{
    /// <summary>
    /// 모서리 드래그 회전 한 번의 상태 (HTML의 d.orig / d.steps / d.rn).
    /// 90° 경계를 넘을 때마다 <see cref="Apply"/>를 부르면 자모가 즉시 바뀌고, 화면 각도는 이어서 돌아가도록 계산된다.
    /// 항상 드래그 시작 때의 자모(<see cref="Orig"/>) 기준으로 다시 계산하므로 돌렸다 되돌리면 원래대로 돌아온다.
    /// </summary>
    public sealed class RotationSession
    {
        /// <summary>드래그를 시작할 때의 자모.</summary>
        public char Orig { get; }
        /// <summary>지금까지 돈 90° 횟수 (시계방향 +, 음수 가능).</summary>
        public int Steps { get; private set; }
        /// <summary>현재 화면의 자모가 Orig를 몇 번 돌린 모양인지 (Orig 그대로면 0).</summary>
        public int Rn { get; private set; }
        public char Current { get; private set; }

        /// <summary>Apply로 자모가 바뀐 직후 새 글자를 먼저 놓을 각도(시계방향 도). 이전 모습에서 끊김 없이 이어 붙이기 위한 값.</summary>
        public float JumpAngleDeg { get; private set; }
        /// <summary>그 다음 애니메이션으로 도달할 각도(시계방향 도) = (Steps − Rn)·90.</summary>
        public float TargetAngleDeg { get; private set; }

        public RotationSession(char orig)
        {
            Orig = orig;
            Current = orig;
        }

        static int Mod4(int v) => ((v % 4) + 4) % 4;

        /// <summary>누적 회전 횟수를 steps로 갱신한다. 자모가 바뀌었으면 true.</summary>
        public bool Apply(int steps)
        {
            int n = Mod4(steps);
            RotationTable.TryRotate(Orig, n, out char want);   // 매핑이 없으면 Orig 유지

            bool changed = want != Current;
            if (changed)
            {
                Rn = want == Orig ? 0 : n;
                JumpAngleDeg = (Steps - Rn) * 90f;
                Current = want;
            }
            Steps = steps;
            TargetAngleDeg = (steps - Rn) * 90f;
            return changed;
        }

        /// <summary>
        /// 손을 뗄 때. 자모로 인정되는 각도면 true(그 자모를 확정, 화면 각도는 0으로 초기화),
        /// 아니면 false(자모는 이미 Orig이며 화면 각도만 0으로 되돌린다).
        /// </summary>
        public bool Finish()
        {
            if (!RotationTable.TryRotate(Orig, Steps, out char j)) return false;
            Current = j;
            return true;
        }
    }
}
