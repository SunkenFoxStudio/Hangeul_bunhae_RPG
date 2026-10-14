using Hangeul.Game.Character;
using Hangeul.Game.Interaction;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hangeul.Game.Editor
{
    /// <summary>GameObject ▸ Hangeul ▸ Interaction Prompt / World Item : 상호작용 안내 UI와 맵 아이템을 만든다.</summary>
    public static class CreateInteractionMenus
    {
        static Font LoadFont() => AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NanumMyeongjo-ExtraBold.ttf");

        [MenuItem("GameObject/Hangeul/Interaction Prompt", false, 13)]
        static void CreatePrompt()
        {
            if (Object.FindFirstObjectByType<InteractionPromptView>() != null)
            {
                Debug.LogWarning("이미 InteractionPromptView가 있습니다.");
                return;
            }

            var font = LoadFont();
            var canvasGo = new GameObject("InteractionCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Interaction Prompt");
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;   // 인벤토리 캔버스(10)보다 아래
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.GetComponent<GraphicRaycaster>().enabled = false;   // 클릭을 가로채지 않는다

            // 월드 대상을 따라다니는 안내 패널 (피벗은 아래 가운데)
            var panel = NewRect("Prompt", canvasGo.transform);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            // 패널은 이름 칸과 안내 칸을 세로로 쌓는 투명한 틀. 각 칸은 자기 글자 길이에 맞춰 폭이 정해진다.
            var stack = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.childAlignment = TextAnchor.LowerCenter;
            stack.spacing = 4f;
            stack.childControlWidth = stack.childControlHeight = false;
            stack.childForceExpandWidth = stack.childForceExpandHeight = false;
            var stackFit = panel.gameObject.AddComponent<ContentSizeFitter>();
            stackFit.horizontalFit = stackFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = NewBox("TitleBox", panel, font, 40, new Color(0.97f, 0.9f, 0.76f), 18, 8, out _);
            title.text = "검";
            var hint = NewBox("HintBox", panel, font, 22, new Color(0.85f, 0.7f, 0.45f), 14, 6, out _);
            hint.text = "[E] 줍기";

            // 결과 문구 (화면 아래쪽 가운데)
            var message = NewText("Message", canvasGo.transform, font, 30, new Color(1f, 0.85f, 0.55f), TextAnchor.MiddleCenter);
            var mrt = message.rectTransform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 0.2f);
            mrt.sizeDelta = new Vector2(700f, 60f);
            message.text = "";
            message.gameObject.SetActive(false);

            var view = Undo.AddComponent<InteractionPromptView>(canvasGo);
            var so = new SerializedObject(view);
            so.FindProperty("player").objectReferenceValue = Object.FindFirstObjectByType<PlayerController>();
            so.FindProperty("panel").objectReferenceValue = panel;
            so.FindProperty("panelGroup").objectReferenceValue = group;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("hintText").objectReferenceValue = hint;
            so.FindProperty("messageText").objectReferenceValue = message;
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = canvasGo;
        }

        [MenuItem("GameObject/Hangeul/World Item", false, 14)]
        static void CreateWorldItem()
        {
            var go = new GameObject("Item_검", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(WorldItem));
            Undo.RegisterCreatedObjectUndo(go, "Create World Item");

            var col = go.GetComponent<CircleCollider2D>();
            col.isTrigger = true;          // 걸어 들어갈 수 있고, 센서는 트리거도 감지한다
            col.radius = 0.12f;

            // 보고 있는 씬 뷰의 가운데에 놓는다
            var view = SceneView.lastActiveSceneView;
            if (view != null) go.transform.position = new Vector3(view.pivot.x, view.pivot.y, 0f);

            Selection.activeGameObject = go;
            Debug.Log("월드 아이템을 만들었습니다. SpriteRenderer에 아이템 이미지를, World Item의 Word에 단어를 넣으세요.");
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // 배경 이미지 + 글자 하나로 이뤄진 칸. 폭과 높이가 글자에 맞춰진다. 반환값은 안의 Text.
        static Text NewBox(string name, Transform parent, Font font, int size, Color color, int padX, int padY, out RectTransform box)
        {
            box = NewRect(name, parent);
            var bg = box.gameObject.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.09f, 0.06f, 0.85f);
            bg.raycastTarget = false;
            var row = box.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(padX, padX, padY, padY);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var fit = box.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return NewText("Text", box, font, size, color, TextAnchor.MiddleCenter);
        }

        static Text NewText(string name, Transform parent, Font font, int size, Color color, TextAnchor anchor)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }
    }
}
