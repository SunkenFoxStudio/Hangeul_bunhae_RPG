using Unity.Cinemachine;
using UnityEngine;

namespace Hangeul.Game.Inventory
{
    /// <summary>
    /// 인벤토리를 열면 카메라를 오른쪽으로 부드럽게 밀어서, 플레이어가 화면 왼쪽(빈 공간)에 보이게 한다.
    /// Cinemachine 3의 <see cref="CinemachinePositionComposer.TargetOffset"/>을 SmoothDamp로 움직이는 방식이다.
    /// CinemachineCamera 오브젝트(Position Composer가 붙은 것)에 붙이거나 직접 지정한다.
    /// </summary>
    public sealed class InventoryCameraShift : MonoBehaviour
    {
        [SerializeField] CinemachineCamera virtualCamera;
        [Tooltip("열렸을 때 플레이어가 화면 가운데에서 왼쪽으로 이동할 거리 (화면 너비 비율). 0.25면 중앙에서 25% 왼쪽.")]
        [SerializeField, Range(0f, 0.5f)] float screenShift = 0.25f;
        [SerializeField, Min(0.01f)] float smoothTime = 0.35f;

        CinemachinePositionComposer _composer;
        Vector3 _baseOffset;
        float _shiftX;         // 현재 추가 오프셋(월드 단위)
        float _velocity;
        bool _shifted;

        void Awake()
        {
            if (virtualCamera == null) virtualCamera = GetComponent<CinemachineCamera>();
            if (virtualCamera != null) _composer = virtualCamera.GetComponent<CinemachinePositionComposer>();
            if (_composer == null)
            {
                Debug.LogError("InventoryCameraShift: CinemachineCamera에 Position Composer가 필요합니다.", this);
                enabled = false;
                return;
            }
            _baseOffset = _composer.TargetOffset;
        }

        public void SetShifted(bool shifted) => _shifted = shifted;

        void LateUpdate()
        {
            float target = _shifted ? screenShift * ViewWidth() : 0f;
            _shiftX = Mathf.SmoothDamp(_shiftX, target, ref _velocity, smoothTime);
            // 카메라가 기준점보다 오른쪽을 비추면 플레이어는 화면 왼쪽에 보인다
            _composer.TargetOffset = _baseOffset + new Vector3(_shiftX, 0f, 0f);
        }

        float ViewWidth()
        {
            var cam = Camera.main;
            float aspect = cam != null ? cam.aspect : 16f / 9f;
            return 2f * virtualCamera.Lens.OrthographicSize * aspect;
        }
    }
}
