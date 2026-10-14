using System;

namespace Hangeul.App
{
    /// <summary>스프라이트 프레임 애니메이션의 시간 계산(순수 규칙). 플레이어·적의 방향별 애니메이터가 같은 식을 쓴다.</summary>
    public static class ClipMath
    {
        /// <summary>
        /// 재생을 시작하고 time초 지났을 때 보여줄 프레임 번호.
        /// loop면 처음으로 돌아가고, 아니면 마지막 프레임에 머문다. 프레임이 없으면 0.
        /// </summary>
        public static int FrameIndex(float time, float fps, int count, bool loop)
        {
            if (count <= 0) return 0;
            int i = (int)Math.Floor(time * Math.Max(0.01f, fps));
            return loop ? ((i % count) + count) % count : Math.Max(0, Math.Min(i, count - 1));
        }

        /// <summary>프레임 count장을 정확히 seconds초 동안 한 번 재생하려면 필요한 초당 프레임 수. 맞출 수 없으면 0.</summary>
        public static float FpsToFit(int count, float seconds)
            => count > 0 && seconds > 0f ? count / seconds : 0f;
    }
}
