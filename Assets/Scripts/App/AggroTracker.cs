using System;

namespace Hangeul.App
{
    /// <summary>
    /// 적이 플레이어를 쫓기 시작하고 포기하는 기준(감지 거리 / 포기 거리). 감지 거리 안으로 들어오면 쫓기 시작하고,
    /// 포기 거리보다 멀어져야 놓친다. 두 거리가 다른 덕분에 경계에서 쫓았다 놓았다를 반복하지 않는다(히스테리시스).
    /// 거리 계산만 하는 순수 규칙이라 EditMode에서 검증한다.
    /// </summary>
    public sealed class AggroTracker
    {
        public float DetectRange { get; private set; }
        public float LoseRange { get; private set; }
        public bool Active { get; private set; }

        public AggroTracker(float detectRange, float loseRange) => SetRanges(detectRange, loseRange);

        /// <summary>
        /// 거리를 바꾼다(플레이 중에 인스펙터 값을 고쳐도 바로 적용되도록 적이 매 프레임 부른다). 쫓는 중인지는 그대로 둔다.
        /// 포기 거리는 감지 거리보다 작을 수 없다.
        /// </summary>
        public void SetRanges(float detectRange, float loseRange)
        {
            DetectRange = Math.Max(0f, detectRange);
            LoseRange = Math.Max(DetectRange, loseRange);
        }

        /// <summary>대상까지의 거리로 상태를 갱신하고, 쫓는 중인지 돌려준다. 대상이 없거나 죽었으면(targetAlive=false) 바로 놓는다.</summary>
        public bool Update(float distance, bool targetAlive = true)
        {
            if (!targetAlive) Active = false;
            else if (!Active && distance <= DetectRange) Active = true;
            else if (Active && distance > LoseRange) Active = false;
            return Active;
        }

        public void Reset() => Active = false;
    }
}
