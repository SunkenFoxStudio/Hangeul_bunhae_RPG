using Hangeul.Game.Inventory;
using Hangeul.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Hangeul.Game.Editor
{
    /// <summary>
    /// GameObject ▸ Hangeul ▸ Inventory UI : 오른쪽 인벤토리 패널의 뼈대를 만든다.
    /// 구조: Canvas ▸ Panel(우측 40%) ▸ BagFrame ▸ GridArea ▸ CardLayer (+ WordDock, TrashZone)
    /// </summary>
    public static class CreateInventoryUIMenu
    {
        const float PanelWidthRatio = 0.4f;   // 화면 오른쪽 40% (카메라 이동량 InventoryCameraShift.screenShift와 함께 조절)

        [MenuItem("GameObject/Hangeul/Inventory UI", false, 11)]
        static void Create()
        {
            var canvasGo = new GameObject("InventoryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Inventory UI");

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // 오른쪽 패널
            var panel = NewRect("Panel", canvasGo.transform);
            panel.anchorMin = new Vector2(1f - PanelWidthRatio, 0f);
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(1f, 0.5f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            AddImage(panel, new Color(0.12f, 0.09f, 0.06f, 0.92f));
            panel.gameObject.AddComponent<CanvasGroup>();
            var view = panel.gameObject.AddComponent<InventoryPanelView>();

            // 가방 프레임 (나중에 9-slice 스프라이트로 교체)
            var bag = NewRect("BagFrame", panel);
            Stretch(bag, left: 40f, right: 40f, top: 40f, bottom: 40f);
            AddImage(bag, new Color(0.35f, 0.22f, 0.12f, 1f));

            // 카드 격자 영역: 가방 프레임 안에서 정사각형으로 맞춘다(격자 비율에 맞게 aspectRatio 조정)
            var grid = NewRect("GridArea", bag);
            Stretch(grid, 24f, 24f, 24f, 24f);
            AddImage(grid, new Color(0.8f, 0.7f, 0.5f, 0.9f));
            var fitter = grid.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;

            // 카드가 놓일 레이어 (드래그 중인 카드를 이쪽 최상단으로 올린다)
            var cardLayer = NewRect("CardLayer", grid);
            Stretch(cardLayer, 0f, 0f, 0f, 0f);

            // 패널 뷰에 토글 연결
            var so = new SerializedObject(view);
            so.FindProperty("toggle").objectReferenceValue = Object.FindFirstObjectByType<InventoryToggle>();
            so.ApplyModifiedPropertiesWithoutUndo();

            AddCardGrid(grid);
            AddWordDock(panel);
            AddTrashZone(panel);
            EnsureEventSystem();
            Selection.activeGameObject = panel.gameObject;
        }

        /// <summary>이미 만들어 둔 인벤토리 UI(직접 조절한 값 유지)에 카드 격자와 단어 입력창만 추가한다.</summary>
        [MenuItem("GameObject/Hangeul/Inventory Cards (기존 UI에 추가)", false, 12)]
        static void AddToExisting()
        {
            var panelView = Object.FindFirstObjectByType<InventoryPanelView>();
            if (panelView == null) { Debug.LogWarning("InventoryPanelView가 없습니다. 먼저 Hangeul ▸ Inventory UI를 만드세요."); return; }

            var panel = (RectTransform)panelView.transform;
            RectTransform grid = null;
            foreach (var rt in panel.GetComponentsInChildren<RectTransform>(true))
                if (rt.name == "GridArea") { grid = rt; break; }
            if (grid == null) { Debug.LogWarning("Panel 아래에 'GridArea'가 없습니다."); return; }

            AddCardGrid(grid);
            if (panel.GetComponentInChildren<WordInputView>(true) == null) AddWordDock(panel);
            AddTrashZone(panel);
            EnsureEventSystem();
            Selection.activeGameObject = grid.gameObject;
        }

        static Font LoadFont() => AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NanumMyeongjo-ExtraBold.ttf");

        // GridArea에 InventoryView를 붙이고 CardLayer/AspectRatioFitter/폰트를 연결한다
        static void AddCardGrid(RectTransform grid)
        {
            var view = grid.GetComponent<InventoryView>();
            if (view == null) view = Undo.AddComponent<InventoryView>(grid.gameObject);

            var layer = grid.Find("CardLayer") as RectTransform;
            if (layer == null)
            {
                layer = NewRect("CardLayer", grid);
                Stretch(layer, 0f, 0f, 0f, 0f);
            }

            var so = new SerializedObject(view);
            so.FindProperty("cardLayer").objectReferenceValue = layer;
            so.FindProperty("fitter").objectReferenceValue = grid.GetComponent<AspectRatioFitter>();
            so.FindProperty("font").objectReferenceValue = LoadFont();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 하단 디버그 단어 입력창 (HTML의 #dock). 패널 맨 아래에 놓는다.
        static void AddWordDock(RectTransform panel)
        {
            var font = LoadFont();
            var dock = NewRect("WordDock", panel);
            dock.anchorMin = new Vector2(0f, 0f);
            dock.anchorMax = new Vector2(1f, 0f);
            dock.pivot = new Vector2(0.5f, 0f);
            dock.offsetMin = new Vector2(40f, 3f);
            dock.offsetMax = new Vector2(-40f, 37f);

            var inputRt = NewRect("Input", dock);
            inputRt.anchorMin = Vector2.zero;
            inputRt.anchorMax = new Vector2(0.78f, 1f);
            inputRt.offsetMin = inputRt.offsetMax = Vector2.zero;
            AddImage(inputRt, new Color(0.95f, 0.9f, 0.78f, 1f));
            var field = inputRt.gameObject.AddComponent<InputField>();
            var text = NewText("Text", inputRt, font, 20, new Color(0.17f, 0.11f, 0.07f), "");
            var placeholder = NewText("Placeholder", inputRt, font, 20, new Color(0.17f, 0.11f, 0.07f, 0.45f), "단어 입력 후 Enter (예: 가방)");
            placeholder.fontStyle = FontStyle.Italic;
            foreach (var t in new[] { text, placeholder })
            {
                var r = t.rectTransform;
                r.offsetMin = new Vector2(10f, 2f);
                r.offsetMax = new Vector2(-10f, -2f);
                t.alignment = TextAnchor.MiddleLeft;
            }
            field.textComponent = text;
            field.placeholder = placeholder;
            field.targetGraphic = inputRt.GetComponent<Image>();

            var btnRt = NewRect("AddButton", dock);
            btnRt.anchorMin = new Vector2(0.8f, 0f);
            btnRt.anchorMax = Vector2.one;
            btnRt.offsetMin = btnRt.offsetMax = Vector2.zero;
            var btnImg = btnRt.gameObject.AddComponent<Image>();
            btnImg.color = new Color(0.55f, 0.39f, 0.19f, 1f);
            var button = btnRt.gameObject.AddComponent<Button>();
            button.targetGraphic = btnImg;
            var label = NewText("Label", btnRt, font, 20, new Color(0.97f, 0.9f, 0.76f), "추가");
            label.alignment = TextAnchor.MiddleCenter;

            var wordInput = Undo.AddComponent<WordInputView>(dock.gameObject);
            var so = new SerializedObject(wordInput);
            so.FindProperty("inventory").objectReferenceValue = panel.GetComponentInChildren<InventoryView>(true);
            so.FindProperty("input").objectReferenceValue = field;
            so.FindProperty("addButton").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 인벤토리 창 왼쪽 바깥에 쓰레기통 자리(아이콘은 TrashZone/Icon의 Image Sprite에 나중에 끌어 넣는다). 이미 있으면 연결만 한다.
        static void AddTrashZone(RectTransform panel)
        {
            var view = panel.GetComponentInChildren<InventoryView>(true);
            var zone = panel.GetComponentInChildren<TrashZone>(true);
            if (zone == null)
            {
                var rt = NewRect("TrashZone", panel);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-12f, 0f);
                rt.sizeDelta = new Vector2(120f, 120f);
                var bg = rt.gameObject.AddComponent<Image>();

                var iconRt = NewRect("Icon", rt);
                Stretch(iconRt, 20f, 20f, 20f, 20f);
                var icon = iconRt.gameObject.AddComponent<Image>();
                icon.raycastTarget = false;
                icon.color = new Color(1f, 1f, 1f, 0.35f);   // 스프라이트를 넣으면 흰색(1,1,1,1)으로 올린다

                zone = Undo.AddComponent<TrashZone>(rt.gameObject);
                var zso = new SerializedObject(zone);
                zso.FindProperty("background").objectReferenceValue = bg;
                zso.FindProperty("icon").objectReferenceValue = icon;
                zso.ApplyModifiedPropertiesWithoutUndo();
            }

            if (view != null)
            {
                var so = new SerializedObject(view);
                so.FindProperty("trashZone").objectReferenceValue = zone;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static Text NewText(string name, RectTransform parent, Font font, int size, Color color, string content)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.text = content;
            t.raycastTarget = false;
            return t;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        static void AddImage(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
        }

        // 버튼/드래그 입력을 받으려면 씬에 EventSystem(Input System UI 모듈)이 하나 있어야 한다
        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }
    }
}
