using System;
using UnityEngine;
using UnityEngine.UI;

namespace Hangeul.Game.Feedback
{
    /// <summary>
    /// 화면을 까맣게 덮었다가 걷는 페이드(죽어서 체크포인트로 돌아갈 때 등). 시간은 멈추지 않는 시간으로 재서 게임이 멈춰 있어도 진행한다.
    /// 씬에 없으면 처음 쓸 때 자동으로 만든다(<see cref="Instance"/>). Hangeul ▸ Setup ▸ Combat HUD &amp; Feedback 메뉴가 미리 만들어 둔다.
    /// </summary>
    [AddComponentMenu("Hangeul/Screen Fader")]
    public sealed class ScreenFader : MonoBehaviour
    {
        static ScreenFader _instance;

        [SerializeField] Image image;

        float _from, _to, _t, _duration;
        Action _onDone;
        bool _running;

        public static ScreenFader Instance
        {
            get
            {
                if (_instance == null) _instance = FindFirstObjectByType<ScreenFader>();
                if (_instance == null) _instance = new GameObject("ScreenFader").AddComponent<ScreenFader>();
                return _instance;
            }
        }

        /// <summary>현재 화면이 덮인 정도(0 = 투명, 1 = 완전히 검정).</summary>
        public float Alpha => image != null ? image.color.a : 0f;

        void Awake()
        {
            _instance = this;
            EnsureBuilt();
        }

        /// <summary>전체 화면 검정 이미지를 만든다(없을 때만). 메뉴가 편집 중에 미리 만들 때도 쓴다.</summary>
        public void EnsureBuilt()
        {
            if (image != null) return;

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;   // 인벤토리(10)·안내 UI·하트 위에 그린다

            var go = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = false;   // 클릭을 가로채지 않는다
        }

        /// <summary>화면을 까맣게 덮는다. 다 덮이면 onDone을 부른다(그 사이에 몰래 위치를 옮기기 좋다).</summary>
        public void FadeOut(float seconds, Action onDone = null) => Begin(1f, seconds, onDone);

        /// <summary>검은 화면을 걷는다.</summary>
        public void FadeIn(float seconds, Action onDone = null) => Begin(0f, seconds, onDone);

        void Begin(float target, float seconds, Action onDone)
        {
            EnsureBuilt();
            _from = image.color.a;
            _to = target;
            _t = 0f;
            _duration = Mathf.Max(0.0001f, seconds);
            _onDone = onDone;
            _running = true;
        }

        void Update()
        {
            if (!_running) return;
            _t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(_t / _duration);
            var c = image.color;
            c.a = Mathf.Lerp(_from, _to, u);
            image.color = c;
            if (u < 1f) return;

            _running = false;
            var done = _onDone;
            _onDone = null;
            done?.Invoke();
        }
    }
}
