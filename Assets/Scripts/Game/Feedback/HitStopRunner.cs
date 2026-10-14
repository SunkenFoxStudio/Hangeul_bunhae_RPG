using UnityEngine;

namespace Hangeul.Game.Feedback
{
    /// <summary><see cref="HitStop"/>이 필요할 때 만드는 작은 도우미. 정해 둔 시간이 지나면 정지를 푼다.</summary>
    public sealed class HitStopRunner : MonoBehaviour
    {
        float _until;

        public void Extend(float seconds)
        {
            _until = Mathf.Max(_until, Time.unscaledTime + seconds);
            GamePause.Set(PauseReason.HitStop, true);
        }

        void Update()
        {
            if (_until <= 0f || Time.unscaledTime < _until) return;
            _until = 0f;
            GamePause.Set(PauseReason.HitStop, false);
        }

        // 멈춘 채로 사라지는 일이 없게(장면 전환 등)
        void OnDestroy() => GamePause.Set(PauseReason.HitStop, false);
    }
}
