using UnityEngine;

namespace Hangeul.Game.Feedback
{
    /// <summary>
    /// 히트스톱: 때리거나 맞는 순간 게임을 아주 짧게 멈춰 타격감을 준다. <see cref="GamePause"/>의 <c>HitStop</c> 이유를 잠깐 켰다 끄는 방식이라
    /// 인벤토리 정지와 서로 방해하지 않는다(둘 중 하나라도 켜져 있으면 멈춘 채다). 시간은 멈추지 않는 시간(unscaled)으로 잰다.
    /// </summary>
    public static class HitStop
    {
        static HitStopRunner _runner;

        /// <summary>게임을 seconds초 동안 멈춘다. 이미 멈춰 있으면 더 긴 쪽에 맞춘다.</summary>
        public static void Request(float seconds)
        {
            if (seconds <= 0f) return;
            if (_runner == null)
            {
                var go = new GameObject("HitStop");
                _runner = go.AddComponent<HitStopRunner>();
            }
            _runner.Extend(seconds);
        }
    }
}
