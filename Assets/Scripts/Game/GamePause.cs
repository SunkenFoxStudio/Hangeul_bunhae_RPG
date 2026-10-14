using System;
using UnityEngine;

namespace Hangeul.Game
{
    /// <summary>게임을 멈추는 이유. 하나라도 켜져 있으면 게임 시간이 멈춘다.</summary>
    [Flags]
    public enum PauseReason
    {
        None = 0,
        Inventory = 1,   // 인벤토리가 열려 있다
        Dialogue = 2,    // 대화 중(후속)
        Menu = 4,        // 일시정지 메뉴(후속)
    }

    /// <summary>
    /// 게임 시간 정지. 이유(<see cref="PauseReason"/>)가 하나라도 켜져 있으면 <c>Time.timeScale = 0</c>이라
    /// 플레이어, 적, 투사체, 애니메이션, 게임 월드의 물리가 모두 멈춘다.
    /// 멈춘 동안에도 움직여야 하는 것(UI 연출, 카메라 이동, 가방 흔들기 물리)은 <c>Time.unscaledDeltaTime</c>을 쓴다.
    /// 새로 만드는 게임 코드는 <c>Time.deltaTime</c>/<c>Time.fixedDeltaTime</c>(멈추면 같이 멈춤)을 쓰면 별도 처리 없이 정지가 적용된다.
    /// </summary>
    public static class GamePause
    {
        static PauseReason _reasons;

        public static bool IsPaused => _reasons != PauseReason.None;
        public static PauseReason Reasons => _reasons;

        /// <summary>정지 상태가 바뀔 때(true = 멈춤).</summary>
        public static event Action<bool> PausedChanged;

        public static void Set(PauseReason reason, bool on)
        {
            var next = on ? _reasons | reason : _reasons & ~reason;
            if (next == _reasons) return;

            bool was = IsPaused;
            _reasons = next;
            Time.timeScale = IsPaused ? 0f : 1f;
            if (was != IsPaused) PausedChanged?.Invoke(IsPaused);
        }

        // 에디터에서 도메인 리로드 없이 플레이를 다시 시작해도 이전 상태가 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            _reasons = PauseReason.None;
            PausedChanged = null;
            Time.timeScale = 1f;
        }
    }
}
