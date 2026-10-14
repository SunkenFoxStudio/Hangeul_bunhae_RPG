using System;
using Hangeul.Game.Character;
using UnityEngine;
using UnityEngine.Events;

namespace Hangeul.Game.Inventory
{
    /// <summary>
    /// Tab 키로 인벤토리를 열고 닫는다. 열려 있는 동안 플레이어 조작을 잠그고(Locked 상태),
    /// 카메라를 오른쪽으로 밀어 플레이어가 화면 왼쪽에 보이게 한다. 열려 있는 동안 게임 시간도 멈춘다(<see cref="GamePause"/>). 인벤토리 UI는 <see cref="onToggled"/>에 연결한다.
    /// (오른쪽에 표시할 패널을 켜고 끄는 일은 UI 쪽 담당)
    /// </summary>
    public sealed class InventoryToggle : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] InventoryCameraShift cameraShift;
        [Tooltip("열기/닫기 때 호출. 인자: 열렸으면 true. 인벤토리 패널 GameObject.SetActive 등을 연결한다.")]
        [SerializeField] UnityEvent<bool> onToggled;

        public bool IsOpen { get; private set; }

        /// <summary>열림 상태가 바뀔 때 발생(true = 열림). 패널 뷰 등 코드에서 구독하는 용도.</summary>
        public event Action<bool> Toggled;

        void Update()
        {
            if (player == null || !player.Input.InventoryPressed) return;

            if (IsOpen) Close();
            else if (player.IsFree) Open();   // 상호작용/대화 등 다른 잠금 중에는 열지 않는다
        }

        void OnDestroy() => GamePause.Set(PauseReason.Inventory, false);   // 열린 채로 사라져도 게임이 멈춘 채 남지 않게

        public void Open() => Set(true);
        public void Close() => Set(false);

        void Set(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            player.SetControlLocked(open);
            GamePause.Set(PauseReason.Inventory, open);   // 열려 있는 동안 게임 시간이 멈춘다(UI, 카메라 이동, 가방 흔들기 물리는 멈추지 않는 시간으로 돈다)
            if (cameraShift != null) cameraShift.SetShifted(open);
            Toggled?.Invoke(open);
            onToggled.Invoke(open);
        }
    }
}
