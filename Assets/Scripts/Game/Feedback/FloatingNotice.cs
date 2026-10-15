using UnityEngine;
using UnityEngine.UI;

namespace Hangeul.Game.Feedback
{
    /// <summary>
    /// 캐릭터 머리 위에 잠깐 떴다 사라지는 알림(예: 무기 없이 공격하려 할 때). 붙인 오브젝트를 따라다니며 천천히 떠오르다 흐려진다.
    /// 지금은 임시로 <b>글자 말풍선</b>을 보여주고, <see cref="Show"/>에 그림(Sprite)을 넘기면 말풍선 대신 <b>그림</b>을 보여준다 —
    /// 나중에 스프라이트로 바꿀 때 코드는 그대로 두고 그림만 넣으면 된다(그림 자리는 알림을 부르는 쪽의 인스펙터에 있다).
    /// 화면 UI(오버레이 캔버스)를 처음 쓸 때 자동으로 만든다. 시간은 멈추지 않는 시간으로 재서 히트스톱·일시정지 중에도 흐른다.
    /// </summary>
    [AddComponentMenu("Hangeul/Floating Notice")]
    public sealed class FloatingNotice : MonoBehaviour
    {
        [Header("글자 말풍선 (그림이 없을 때)")]
        [Tooltip("비워 두면 인벤토리와 같은 글꼴을 쓰고, 그것도 없으면 기본 글꼴을 쓴다.")]
        [SerializeField] Font font;
        [SerializeField, Min(8)] int fontSize = 28;
        [SerializeField] Color textColor = new Color(1f, 0.93f, 0.8f);
        [SerializeField] Color bubbleColor = new Color(0.1f, 0.07f, 0.04f, 0.85f);
        [SerializeField] Vector2 padding = new Vector2(18f, 10f);

        [Header("그림 (알림에 그림을 넘겼을 때)")]
        [Tooltip("그림이 가로·세로 중 긴 쪽이 이 크기(화면 단위, 1920×1080 기준)가 되도록 맞춘다.")]
        [SerializeField, Min(8f)] float iconMaxSize = 96f;

        [Header("위치 / 시간")]
        [Tooltip("붙은 오브젝트 위치에서의 오프셋(월드 단위). 보통 머리 위.")]
        [SerializeField] Vector2 worldOffset = new Vector2(0f, 1.3f);
        [SerializeField, Min(0.1f)] float seconds = 1.5f;
        [Tooltip("떠 있는 동안 위로 올라가는 거리(월드 단위). 처음엔 빠르게 올라가다 점점 느려진다.")]
        [SerializeField, Min(0f)] float riseUnits = 0.6f;
        [Tooltip("전체 시간 중 마지막 이 비율 동안 서서히 사라진다. 클수록 올라가는 동안 일찍부터 흐려진다.")]
        [SerializeField, Range(0.05f, 1f)] float fadeFraction = 0.65f;

        RectTransform _root;       // 캔버스 안에서 위치를 옮기는 알림 전체
        CanvasGroup _group;
        Image _bubble;
        Text _text;
        Image _icon;
        float _t = -1f;            // 보여 준 지 몇 초(음수면 숨김)

        /// <summary>글꼴이 비어 있을 때만 채운다(알림을 부르는 쪽이 프로젝트 글꼴을 넘겨 줄 때).</summary>
        public void UseFontIfNone(Font fallback)
        {
            if (font == null) font = fallback;
        }

        /// <summary>
        /// 알림을 띄운다(이미 떠 있으면 처음부터 다시). icon이 있으면 글자 대신 그림을, 없으면 message를 글자 말풍선으로 보여준다.
        /// </summary>
        public void Show(string message, Sprite icon = null)
        {
            EnsureBuilt();
            _root.gameObject.SetActive(true);   // 글자 크기를 재기 전에 켜 둔다

            bool useIcon = icon != null;
            _bubble.enabled = !useIcon;
            _text.gameObject.SetActive(!useIcon);
            _icon.gameObject.SetActive(useIcon);

            if (useIcon)
            {
                _icon.sprite = icon;
                var size = icon.rect.size;
                float k = iconMaxSize / Mathf.Max(size.x, size.y);
                _root.sizeDelta = size * k;
            }
            else
            {
                _text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _text.fontSize = fontSize;
                _text.color = textColor;
                _text.text = message;
                _root.sizeDelta = new Vector2(_text.preferredWidth + padding.x * 2f, _text.preferredHeight + padding.y * 2f);
                _bubble.color = bubbleColor;
            }

            _t = 0f;
            _group.alpha = 1f;
            _root.gameObject.SetActive(true);
            Place(0f);
        }

        /// <summary>떠 있는 알림을 바로 숨긴다.</summary>
        public void Hide()
        {
            _t = -1f;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_t < 0f) return;

            _t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(_t / seconds);
            if (u >= 1f) { Hide(); return; }

            float fadeStart = 1f - fadeFraction;
            _group.alpha = u <= fadeStart ? 1f : 1f - (u - fadeStart) / fadeFraction;
            Place(u);
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.root.gameObject);   // 따로 만든 캔버스도 함께 치운다
        }

        // 붙은 오브젝트의 머리 위 화면 좌표로 옮긴다(오버레이 캔버스에서 RectTransform.position은 화면 픽셀이다).
        void Place(float u)
        {
            var cam = Camera.main;
            if (cam == null) { _root.gameObject.SetActive(false); return; }
            _root.gameObject.SetActive(true);

            float rise = riseUnits * (1f - (1f - u) * (1f - u));   // 처음엔 빠르게, 끝으로 갈수록 느리게(ease-out)
            Vector3 world = transform.position + (Vector3)worldOffset + Vector3.up * rise;
            Vector3 screen = cam.WorldToScreenPoint(world);
            _root.position = new Vector3(screen.x, screen.y, 0f);
        }

        void EnsureBuilt()
        {
            if (_root != null) return;

            var canvasGo = new GameObject("FloatingNoticeCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;   // 인벤토리(10)보다 위, 화면 페이드(50)보다 아래
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var rootGo = new GameObject("Notice", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            rootGo.transform.SetParent(canvasGo.transform, false);
            _root = (RectTransform)rootGo.transform;
            _root.anchorMin = _root.anchorMax = Vector2.zero;   // 화면 왼쪽 아래 기준으로 position에 화면 픽셀을 넣는다
            _root.pivot = new Vector2(0.5f, 0f);                  // 아래쪽 가운데가 머리 위 기준점
            _group = rootGo.GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _bubble = rootGo.GetComponent<Image>();
            _bubble.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(rootGo.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            _text = textGo.GetComponent<Text>();
            _text.alignment = TextAnchor.MiddleCenter;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.raycastTarget = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(rootGo.transform, false);
            var irt = (RectTransform)iconGo.transform;
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = irt.offsetMax = Vector2.zero;
            _icon = iconGo.GetComponent<Image>();
            _icon.raycastTarget = false;
            _icon.preserveAspect = true;

            _root.gameObject.SetActive(false);
        }
    }
}
