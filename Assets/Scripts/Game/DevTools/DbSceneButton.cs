using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hangeul.Game.DevTools
{
    /// <summary>
    /// 게임 화면 위에 항상 떠 있는 "DB 관리 씬으로 이동" 버튼(개발용). 실행할 때 자기 캔버스와 버튼을 코드로 만든다.
    /// 에디터와 Development Build에서만 보이고, 릴리스 빌드에서는 스스로 사라진다.
    /// </summary>
    public sealed class DbSceneButton : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] string targetScene = "ItemDbScene";
        [SerializeField] string label = "DB 관리";
        [Tooltip("화면 오른쪽 위 모서리에서 떨어진 거리(1920×1080 기준 픽셀).")]
        [SerializeField] Vector2 margin = new Vector2(16f, 16f);
        [Tooltip("인벤토리가 열려 있는 동안은 숨긴다(오른쪽 위에 있는 인벤토리 버튼들과 겹치고, 실수로 눌러 장면이 넘어가는 것을 막는다).")]
        [SerializeField] bool hideWhileInventoryOpen = true;

        CanvasGroup _group;

        void Awake()
        {
            if (!Application.isEditor && !Debug.isDebugBuild) { Destroy(gameObject); return; }

            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("DbSceneButtonCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            _group = canvasGo.GetComponent<CanvasGroup>();
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;   // 인벤토리(10)·안내 UI(5)보다 위
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);   // 오른쪽 위
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-margin.x, -margin.y);
            rt.sizeDelta = new Vector2(150f, 44f);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.12f, 0.09f, 0.06f, 0.8f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => SceneManager.LoadScene(targetScene));

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.font = font;
            text.text = label;
            text.fontSize = 22;
            text.color = new Color(0.97f, 0.9f, 0.76f);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
        }

        void Update()
        {
            if (_group == null) return;
            bool hidden = hideWhileInventoryOpen && (GamePause.Reasons & PauseReason.Inventory) != 0;
            _group.alpha = hidden ? 0f : 1f;
            _group.blocksRaycasts = !hidden;
            _group.interactable = !hidden;
        }
    }
}
