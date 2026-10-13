using Hangeul.Game.Inventory;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Hangeul.Game.Editor
{
    /// <summary>
    /// GameObject ▸ Hangeul ▸ Inventory UI : 오른쪽 인벤토리 패널의 뼈대를 만든다.
    /// 구조: Canvas ▸ Panel(우측 40%) ▸ Header / BagFrame ▸ GridArea ▸ CardLayer
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

            // 위쪽 제목 영역 (텍스트는 TMP 폰트 준비 후 추가)
            var header = NewRect("Header", panel);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = Vector2.one;
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 120f);
            header.anchoredPosition = Vector2.zero;
            AddImage(header, new Color(0.22f, 0.15f, 0.09f, 1f));

            // 가방 프레임 (나중에 9-slice 스프라이트로 교체)
            var bag = NewRect("BagFrame", panel);
            Stretch(bag, left: 40f, right: 40f, top: 140f, bottom: 40f);
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

            EnsureEventSystem();
            Selection.activeGameObject = panel.gameObject;
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
