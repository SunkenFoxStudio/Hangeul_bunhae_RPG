using UnityEngine;
using UnityEngine.UI;

namespace Hangeul.UI
{
    /// <summary>단어가 새로 완성됐을 때 그 칸 폭만큼 약 1초 보이는 초록 글로우 (HTML의 .done-ov).</summary>
    [RequireComponent(typeof(Image))]
    public sealed class CompletionOverlay : MonoBehaviour
    {
        const float Duration = 1f;
        static readonly Color Glow = new Color(0.3f, 0.9f, 0.45f, 0.6f);

        Image _image;
        float _t;

        public static void Spawn(RectTransform layer, Vector2 topLeft, Vector2 size)
        {
            var go = new GameObject("CompletionOverlay", typeof(RectTransform), typeof(Image), typeof(CompletionOverlay));
            go.transform.SetParent(layer, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            go.GetComponent<Image>().raycastTarget = false;
        }

        void Awake()
        {
            _image = GetComponent<Image>();
            Apply(0f);   // Image의 기본 색(흰색)이 첫 프레임에 그려지지 않도록 만들어지는 즉시 시작 색을 적용한다
        }

        void Update()
        {
            _t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(_t / Duration);
            Apply(u);
            if (u >= 1f) Destroy(gameObject);
        }

        void Apply(float u)
        {
            // 두 번 깜빡이며 사라진다
            float pulse = 0.5f + 0.5f * Mathf.Cos(u * Mathf.PI * 4f);
            var c = Glow;
            c.a *= (1f - u) * (0.45f + 0.55f * pulse);
            _image.color = c;
        }
    }
}
