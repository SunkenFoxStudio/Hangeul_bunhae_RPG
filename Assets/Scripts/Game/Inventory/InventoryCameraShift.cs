using Unity.Cinemachine;
using UnityEngine;

namespace Hangeul.Game.Inventory
{
    /// <summary>
    /// 인벤토리를 열면 카메라를 오른쪽으로 부드럽게 밀어서, 플레이어가 화면 왼쪽(빈 공간)에 보이게 한다.
    /// Cinemachine 설정(Position Composer)은 건드리지 않고, <see cref="InventoryCameraOffset"/> 확장에 SmoothDamp한 값을 넘긴다
    /// (설정값을 Play 중에 바꾸면 종료 때마다 저장 여부 확인창이 뜬다).
    /// CinemachineCamera 오브젝트에 붙이거나 직접 지정한다. 확장 컴포넌트가 없으면 시작할 때 자동으로 붙인다.
    /// </summary>
    public sealed class InventoryCameraShift : MonoBehaviour
    {
        [SerializeField] CinemachineCamera virtualCamera;
        [Tooltip("열렸을 때 플레이어가 화면 가운데에서 왼쪽으로 이동할 거리 (화면 너비 비율). 0.25면 중앙에서 25% 왼쪽.")]
        [SerializeField, Range(0f, 0.5f)] float screenShift = 0.25f;
        [SerializeField, Min(0.01f)] float smoothTime = 0.35f;

        InventoryCameraOffset _offset;
        float _shiftX;         // 현재 추가 오프셋(월드 단위)
        float _velocity;
        bool _shifted;

        void Awake()
        {
            if (virtualCamera == null) virtualCamera = GetComponent<CinemachineCamera>();
            if (virtualCamera == null)
            {
                Debug.LogError("InventoryCameraShift: CinemachineCamera를 찾을 수 없습니다.", this);
                enabled = false;
                return;
            }
            _offset = virtualCamera.GetComponent<InventoryCameraOffset>();
            if (_offset == null) _offset = virtualCamera.gameObject.AddComponent<InventoryCameraOffset>();
        }

        public void SetShifted(bool shifted) => _shifted = shifted;

        void LateUpdate()
        {
            float target = _shifted ? screenShift * ViewWidth() : 0f;
            _shiftX = Mathf.SmoothDamp(_shiftX, target, ref _velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);   // 인벤토리를 열면 게임 시간이 멈추므로 멈추지 않는 시간으로
            // 카메라가 기준점보다 오른쪽을 비추면 플레이어는 화면 왼쪽에 보인다
            _offset.ShiftX = _shiftX;
        }

        float ViewWidth()
        {
            var cam = Camera.main;
            float aspect = cam != null ? cam.aspect : 16f / 9f;
            return 2f * virtualCamera.Lens.OrthographicSize * aspect;
        }
    }
}
