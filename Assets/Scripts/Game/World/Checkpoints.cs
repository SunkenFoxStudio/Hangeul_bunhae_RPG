using UnityEngine;

namespace Hangeul.Game.World
{
    /// <summary>
    /// 마지막으로 켠 체크포인트의 기록. 플레이어가 죽으면 이 위치에서 되살아난다.
    /// 체크포인트를 하나도 켜지 않았다면 게임을 시작한 자리(<see cref="SetDefault"/>)가 기본 체크포인트다.
    /// 저장 파일에 남기는 것은 세이브 단계에서 한다(지금은 플레이를 다시 시작하면 초기화된다).
    /// </summary>
    public static class Checkpoints
    {
        /// <summary>되살아날 위치. 아무것도 정해지지 않았으면 null.</summary>
        public static Vector2? Position { get; private set; }
        /// <summary>마지막으로 켠 체크포인트 오브젝트(없을 수 있음).</summary>
        public static Checkpoint Active { get; private set; }

        /// <summary>기본 체크포인트(시작 위치). 이미 정해져 있으면 바꾸지 않는다.</summary>
        public static void SetDefault(Vector2 position)
        {
            if (!Position.HasValue) Position = position;
        }

        /// <summary>이 체크포인트를 켜고 이전 것은 끈다. 되살아날 위치도 이 체크포인트로 바뀐다.</summary>
        public static void Activate(Checkpoint checkpoint)
        {
            if (checkpoint == null || Active == checkpoint) return;
            if (Active != null) Active.SetActive(false);
            Active = checkpoint;
            Position = checkpoint.RespawnPosition;
            checkpoint.SetActive(true);
        }

        // 에디터에서 도메인 리로드 없이 플레이를 다시 시작해도 이전 기록이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            Position = null;
            Active = null;
        }
    }
}
