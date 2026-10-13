using UnityEngine;
using UnityEngine.UI;

namespace Hangeul.UI
{
    /// <summary>
    /// 인벤토리 창 왼쪽의 쓰레기통. 카드를 끌어다 이 영역 위에서 놓으면 삭제된다(InventoryView가 판정).
    /// 아이콘은 <see cref="icon"/> Image의 Sprite에 나중에 끌어다 넣으면 된다(비어 있으면 색 칸만 보인다).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TrashZone : MonoBehaviour
    {
        [SerializeField] Image background;
        [SerializeField] Image icon;
        [SerializeField] Color normalColor = new Color(0.15f, 0.1f, 0.07f, 0.85f);
        [SerializeField] Color hoverColor = new Color(0.65f, 0.18f, 0.12f, 0.95f);

        RectTransform _rect;

        void Awake()
        {
            _rect = (RectTransform)transform;
            SetHover(false);
        }

        /// <summary>화면 좌표가 이 영역 안인지.</summary>
        public bool Contains(Vector2 screenPoint, Camera cam)
        {
            if (_rect == null) _rect = (RectTransform)transform;
            return RectTransformUtility.RectangleContainsScreenPoint(_rect, screenPoint, cam);
        }

        /// <summary>영역 중심의 화면 좌표 (카드가 달라붙을 위치).</summary>
        public Vector2 ScreenCenter(Camera cam)
        {
            if (_rect == null) _rect = (RectTransform)transform;
            return RectTransformUtility.WorldToScreenPoint(cam, _rect.TransformPoint(_rect.rect.center));
        }

        /// <summary>카드를 들고 위에 올라와 있는 동안 강조한다.</summary>
        public void SetHover(bool on)
        {
            if (background != null) background.color = on ? hoverColor : normalColor;
            if (icon != null) icon.transform.localScale = Vector3.one * (on ? 1.15f : 1f);
        }
    }
}
