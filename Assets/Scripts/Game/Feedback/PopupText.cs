using UnityEngine;
using UnityEngine.UI;

namespace Hangeul.Game.Feedback
{
    /// <summary>
    /// 월드의 무언가(적 등) 머리 위에 떴다가 위로 올라가면서 흐려지는 글자. 속성이 적에게 묻었을 때 "화염!"처럼 띄우는 데 쓴다.
    /// <see cref="Spawn"/>으로 하나 만들면 스스로 움직이다 사라진다. 대상이 사라져도(적이 죽어서 없어져도) 마지막 자리에서 끝까지 뜬다.
    /// 화면 UI(오버레이 캔버스)를 처음 쓸 때 자동으로 만들고, 시간은 멈추지 않는 시간으로 재서 히트스톱 중에도 흐른다.
    /// 알림 하나가 오브젝트에 붙어 캔버스를 따로 만드는 <see cref="FloatingNotice"/>와 달리, 캔버스 하나를 모두가 나눠 쓴다.
    /// </summary>
    [AddComponentMenu("Hangeul/Popup Text")]
    public sealed class PopupText : MonoBehaviour
    {
        /// <summary>글꼴. 비어 있으면 기본 글꼴. 인벤토리와 같은 글꼴을 쓰도록 <c>PlayerItemUser</c>가 채운다.</summary>
        public static Font DefaultFont;

        const float Seconds = 1.0f;       // 떠 있는 시간
        const float RiseUnits = 0.7f;     // 올라가는 거리(월드 단위)
        const float FadeFraction = 0.6f;  // 전체 시간 중 마지막 이 비율 동안 흐려진다
        const float HeadMargin = 0.15f;   // 대상 그림 꼭대기에서 띄우는 간격(월드 단위)

        static RectTransform s_canvasRoot;

        // 에디터에서 도메인 다시 불러오기를 끄고 실행해도 지난 실행의 캔버스를 붙잡지 않도록
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_canvasRoot = null;

        Transform _follow;
        Vector3 _worldPos;        // 대상의 마지막 위치 + 머리 위 간격
        Vector3 _offset;
        RectTransform _rect;
        Text _text;
        Outline _outline;
        float _t;

        /// <summary>대상의 머리 위에 글자를 띄운다. 대상이나 글자가 없으면 아무것도 하지 않는다.</summary>
        public static PopupText Spawn(Transform target, string message, Color color, int fontSize = 40)
        {
            if (target == null || string.IsNullOrEmpty(message)) return null;
            var root = EnsureCanvas();

            var go = new GameObject("PopupText", typeof(RectTransform), typeof(Text), typeof(Outline), typeof(PopupText));
            go.transform.SetParent(root, false);
            var popup = go.GetComponent<PopupText>();
            popup.Init(target, message, color, fontSize);
            return popup;
        }

        void Init(Transform target, string message, Color color, int fontSize)
        {
            _follow = target;
            _offset = new Vector3(0f, HeadHeight(target), 0f);
            _worldPos = target.position + _offset;

            _rect = (RectTransform)transform;
            _rect.anchorMin = _rect.anchorMax = Vector2.zero;   // 화면 왼쪽 아래 기준으로 position에 화면 픽셀을 넣는다
            _rect.pivot = new Vector2(0.5f, 0f);                // 아래쪽 가운데가 머리 위 기준점
            _rect.sizeDelta = new Vector2(400f, fontSize * 1.4f);

            _text = GetComponent<Text>();
            _text.font = DefaultFont != null ? DefaultFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = fontSize;
            _text.fontStyle = FontStyle.Bold;
            _text.alignment = TextAnchor.LowerCenter;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.raycastTarget = false;
            _text.color = color;
            _text.text = message;

            _outline = GetComponent<Outline>();   // 어두운 배경에서도 밝은 배경에서도 읽히도록 테두리
            _outline.effectColor = new Color(0.1f, 0.05f, 0.03f, 0.9f);
            _outline.effectDistance = new Vector2(2f, -2f);

            Place(0f);
        }

        // 대상 그림(첫 렌더러)의 꼭대기까지의 높이. 렌더러가 없으면 1유닛.
        static float HeadHeight(Transform target)
        {
            var r = target.GetComponentInChildren<Renderer>();
            float top = r != null ? r.bounds.max.y - target.position.y : 1f;
            return Mathf.Max(0.3f, top) + HeadMargin;
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(_t / Seconds);
            if (u >= 1f) { Destroy(gameObject); return; }

            float fadeStart = 1f - FadeFraction;
            float alpha = u <= fadeStart ? 1f : 1f - (u - fadeStart) / FadeFraction;
            var c = _text.color; c.a = alpha; _text.color = c;
            var o = _outline.effectColor; o.a = 0.9f * alpha; _outline.effectColor = o;

            Place(u);
        }

        void Place(float u)
        {
            var cam = Camera.main;
            if (cam == null) { _text.enabled = false; return; }
            _text.enabled = true;

            if (_follow != null) _worldPos = _follow.position + _offset;   // 대상이 사라지면 마지막 자리에 남는다
            float rise = RiseUnits * (1f - (1f - u) * (1f - u));            // 처음엔 빠르게, 끝으로 갈수록 느리게(ease-out)
            Vector3 screen = cam.WorldToScreenPoint(_worldPos + Vector3.up * rise);
            _rect.position = new Vector3(screen.x, screen.y, 0f);
        }

        static RectTransform EnsureCanvas()
        {
            if (s_canvasRoot != null) return s_canvasRoot;

            var canvasGo = new GameObject("PopupTextCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 41;   // 머리 위 알림(40) 바로 위, 화면 페이드(50) 아래
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            s_canvasRoot = (RectTransform)canvasGo.transform;
            return s_canvasRoot;
        }
    }
}
