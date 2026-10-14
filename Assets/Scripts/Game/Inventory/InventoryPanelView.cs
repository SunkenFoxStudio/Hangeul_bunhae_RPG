using UnityEngine;

namespace Hangeul.Game.Inventory
{
    /// <summary>
    /// 화면 오른쪽 인벤토리 패널의 표시/숨김 연출. <see cref="InventoryToggle"/>의 열림 상태를 받아
    /// 오른쪽에서 슬라이드하며 페이드 인/아웃한다(카메라 이동과 같은 길이로 맞춘다).
    /// 숨김 상태에서도 오브젝트는 켜져 있고, 알파 0 + 레이캐스트 차단 해제로 처리한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class InventoryPanelView : MonoBehaviour
    {
        [SerializeField] InventoryToggle toggle;
        [SerializeField, Min(0.01f)] float smoothTime = 0.35f;
        [Tooltip("숨겨졌을 때 패널 너비의 몇 배만큼 오른쪽으로 밀어 둘지.")]
        [SerializeField, Min(0f)] float hiddenOffset = 0.25f;

        RectTransform _rect;
        CanvasGroup _group;
        Vector2 _shownPos;
        float _t;              // 0 = 숨김, 1 = 표시
        float _velocity;
        bool _open;

        public bool IsOpen => _open;

        void Awake()
        {
            _rect = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            _shownPos = _rect.anchoredPosition;
            Apply();
        }

        void OnEnable()
        {
            if (toggle != null) toggle.Toggled += OnToggled;
        }

        void OnDisable()
        {
            if (toggle != null) toggle.Toggled -= OnToggled;
        }

        void OnToggled(bool open) => _open = open;

        void Update()
        {
            float target = _open ? 1f : 0f;
            if (Mathf.Approximately(_t, target) && Mathf.Approximately(_velocity, 0f)) return;

            _t = Mathf.SmoothDamp(_t, target, ref _velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);   // 게임 시간이 멈춰도 움직이도록
            if (Mathf.Abs(_t - target) < 0.001f) { _t = target; _velocity = 0f; }
            Apply();
        }

        void Apply()
        {
            _rect.anchoredPosition = _shownPos + Vector2.right * ((1f - _t) * _rect.rect.width * hiddenOffset);
            _group.alpha = _t;
            _group.blocksRaycasts = _group.interactable = _open && _t > 0.01f;
        }
    }
}
