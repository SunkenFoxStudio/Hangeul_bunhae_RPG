using System.Collections.Generic;
using System.IO;
using System.Text;
using Hangeul.App;
using Hangeul.Domain;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hangeul.Game.DevTools
{
    /// <summary>
    /// 아이템 단어 DB(Resources/item_words.json)를 보고 고치는 디버그 화면(러프). UI는 실행할 때 코드로 만든다.
    /// 왼쪽: 카테고리 목록(클릭하면 그 카테고리만 보기, 빈 카테고리는 ×로 삭제, 새 카테고리 추가).
    /// 오른쪽: 단어 추가 줄(단어 + 카테고리 선택) 아래에 단어 목록(카테고리 버튼을 누르면 다음 카테고리로 바뀜, ×로 삭제).
    /// 바꿀 때마다 에디터에서는 파일에 바로 저장한다(빌드에서는 저장하지 않는다).
    /// </summary>
    public sealed class ItemDbManagerView : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] string returnScene = "SampleScene";

        const string ResourceName = "item_words";

        static readonly Color Bg = new Color(0.10f, 0.08f, 0.06f, 1f);
        static readonly Color Panel = new Color(0.17f, 0.13f, 0.09f, 1f);
        static readonly Color Row = new Color(0.24f, 0.18f, 0.12f, 1f);
        static readonly Color Brown = new Color(0.55f, 0.39f, 0.19f, 1f);
        static readonly Color BrownSel = new Color(0.78f, 0.55f, 0.22f, 1f);
        static readonly Color Danger = new Color(0.55f, 0.22f, 0.18f, 1f);
        static readonly Color Ink = new Color(0.97f, 0.9f, 0.76f, 1f);
        static readonly Color Dim = new Color(0.85f, 0.7f, 0.45f, 1f);
        static readonly Color Warn = new Color(1f, 0.6f, 0.45f, 1f);

        ItemWordsFile _file;
        bool _canSave = true;
        string _filter;        // null = 전체
        string _addCategory;

        Text _status, _listHeader, _addCategoryLabel;
        InputField _wordInput, _categoryInput;
        RectTransform _catContent, _wordContent;

        void Awake()
        {
            Load();
            Build();
            Refresh();
        }

        // ───── 데이터 ─────
        void Load()
        {
            var asset = Resources.Load<TextAsset>(ResourceName);
            _file = asset != null ? JsonUtility.FromJson<ItemWordsFile>(asset.text) : null;
            if (_file == null)
            {
                _file = new ItemWordsFile { schemaVersion = 1 };
            }
            else if (_file.schemaVersion != 1)
            {
                _canSave = false;   // 모르는 형식을 덮어쓰지 않는다
            }
            _file.categories ??= new List<string>();
            _file.items ??= new List<ItemWordEntry>();
            _addCategory = _file.categories.Count > 0 ? _file.categories[0] : "";
        }

        void Save(string message)
        {
#if UNITY_EDITOR
            if (!_canSave) { SetStatus("파일 형식(schemaVersion)이 달라 저장하지 않았습니다.", Warn); return; }
            string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Assets", "Resources", "item_words.json");
            File.WriteAllText(path, JsonUtility.ToJson(_file, true), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            SetStatus(message + "  (저장됨)", Ink);
#else
            SetStatus(message + "  (에디터에서만 저장됩니다)", Warn);
#endif
        }

        string NextCategory(string current)
        {
            var list = _file.categories;
            if (list.Count == 0) return "";
            int i = list.IndexOf(current);
            return list[(i + 1) % list.Count];
        }

        // ───── 동작 ─────
        void AddWord()
        {
            string w = (_wordInput.text ?? "").Trim();
            if (w.Length == 0) return;
            if (_file.categories.Count == 0) { SetStatus("먼저 카테고리를 추가하세요.", Warn); return; }
            if (!ItemWordSet.IsValidWord(w)) { SetStatus($"한글 음절로만 쓸 수 있습니다: {w}", Warn); return; }
            foreach (char ch in w)
                if (ForbiddenSyllables.IsBanned(ch)) { SetStatus($"금지 음절('{ch}')이 들어 있습니다: {w}", Warn); return; }
            if (_file.items.Exists(i => i.word == w)) { SetStatus($"이미 있는 단어입니다: {w}", Warn); return; }

            _file.items.Insert(0, new ItemWordEntry { word = w, category = _addCategory });
            _wordInput.text = "";
            Save($"추가: {w} ({_addCategory})");
            Refresh();
            _wordInput.ActivateInputField();
        }

        void AddCategory()
        {
            string c = (_categoryInput.text ?? "").Trim();
            if (c.Length == 0) return;
            if (_file.categories.Contains(c)) { SetStatus($"이미 있는 카테고리입니다: {c}", Warn); return; }

            _file.categories.Add(c);
            if (string.IsNullOrEmpty(_addCategory)) _addCategory = c;
            _categoryInput.text = "";
            Save($"카테고리 추가: {c}");
            Refresh();
            _categoryInput.ActivateInputField();
        }

        void RemoveCategory(string c)
        {
            if (_file.items.Exists(i => i.category == c)) return;   // 비어 있을 때만
            _file.categories.Remove(c);
            if (_filter == c) _filter = null;
            if (_addCategory == c) _addCategory = _file.categories.Count > 0 ? _file.categories[0] : "";
            Save($"카테고리 삭제: {c}");
            Refresh();
        }

        void RemoveWord(ItemWordEntry e)
        {
            _file.items.Remove(e);
            Save($"삭제: {e.word}");
            Refresh();
        }

        void CycleWordCategory(ItemWordEntry e)
        {
            e.category = NextCategory(e.category);
            Save($"{e.word} → {e.category}");
            Refresh();
        }

        void SetStatus(string message, Color color)
        {
            if (_status == null) return;
            _status.text = message;
            _status.color = color;
        }

        static bool EnterPressed() =>
            Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);

        // ───── 화면 갱신 ─────
        void Refresh()
        {
            RefreshCategories();
            RefreshWords();
            _addCategoryLabel.text = string.IsNullOrEmpty(_addCategory) ? "(카테고리 없음)" : _addCategory;
        }

        void RefreshCategories()
        {
            Clear(_catContent);
            var counts = new Dictionary<string, int>();
            foreach (var it in _file.items)
            {
                counts.TryGetValue(it.category ?? "", out int n);
                counts[it.category ?? ""] = n + 1;
            }

            AddCategoryRow("전체", $"전체 ({_file.items.Count})", null, false);
            foreach (var c in _file.categories)
            {
                counts.TryGetValue(c, out int n);
                AddCategoryRow(c, $"{c} ({n})", c, n == 0);
            }
        }

        void AddCategoryRow(string key, string label, string filter, bool deletable)
        {
            var row = NewRow(_catContent, 52, Color.clear);
            bool selected = _filter == filter;
            AddRowButton(row, label, 26, selected ? BrownSel : Brown, 0f, 1f, () => { _filter = filter; Refresh(); });
            if (deletable) AddRowButton(row, "×", 26, Danger, 52f, 0f, () => RemoveCategory(key));
        }

        void RefreshWords()
        {
            Clear(_wordContent);
            int shown = 0;
            foreach (var it in _file.items)
            {
                if (_filter != null && it.category != _filter) continue;
                shown++;
                var entry = it;
                var row = NewRow(_wordContent, 56, Row);
                AddLabel(row, entry.word, 32, Ink, 0f, 1f);
                AddRowButton(row, string.IsNullOrEmpty(entry.category) ? "(없음)" : entry.category, 24, Brown, 300f, 0f, () => CycleWordCategory(entry));
                AddRowButton(row, "×", 26, Danger, 56f, 0f, () => RemoveWord(entry));
            }
            _listHeader.text = $"단어 {shown}개  ·  {(_filter ?? "전체")}";
        }

        static void Clear(RectTransform content)
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i);
                child.SetParent(null, false);   // 이번 프레임의 레이아웃에서 바로 빠지게 한다
                Destroy(child.gameObject);
            }
        }

        // ───── 화면 만들기 ─────
        void Build()
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("DbCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)canvasGo.transform;

            Img(Rect("Bg", root, 0, 0, 1, 1, 0, 0, 0, 0), Bg);

            // 위쪽 줄: 제목 / 상태 / 돌아가기
            var top = Rect("Top", root, 0, 1, 1, 1, 0, -80, 0, 0);
            Img(top, Panel);
            Txt(Rect("Title", top, 0, 0, 0.4f, 1, 30, 0, 0, 0), "아이템 DB 관리 (디버그)", 34, Ink, TextAnchor.MiddleLeft);
            _status = Txt(Rect("Status", top, 0.4f, 0, 0.8f, 1, 0, 0, 0, 0), "", 24, Ink, TextAnchor.MiddleLeft);
            Btn(Rect("Back", top, 1, 0.5f, 1, 0.5f, -290, -26, -20, 26), "게임으로 돌아가기", 24, Brown, () => SceneManager.LoadScene(returnScene));

            // 왼쪽: 카테고리
            var left = Rect("Left", root, 0, 0, 0, 1, 20, 20, 440, -100);
            Img(left, Panel);
            Txt(Rect("Header", left, 0, 1, 1, 1, 16, -50, 0, 0), "카테고리", 30, Dim, TextAnchor.MiddleLeft);
            _catContent = MakeScroll(Rect("CatScroll", left, 0, 0, 1, 1, 10, 80, -10, -52));
            var catAdd = Rect("CatAdd", left, 0, 0, 1, 0, 10, 10, -10, 70);
            _categoryInput = MakeInput(Rect("Input", catAdd, 0, 0, 0.68f, 1, 0, 0, -6, 0), "새 카테고리 (Enter)");
            _categoryInput.onEndEdit.AddListener(_ => { if (EnterPressed()) AddCategory(); });
            Btn(Rect("Add", catAdd, 0.68f, 0, 1, 1, 0, 0, 0, 0), "추가", 24, Brown, AddCategory);

            // 오른쪽: 단어 추가 + 목록
            var right = Rect("Right", root, 0, 0, 1, 1, 460, 20, -20, -100);
            Img(right, Panel);
            var addRow = Rect("AddRow", right, 0, 1, 1, 1, 10, -76, -10, -10);
            _wordInput = MakeInput(Rect("Word", addRow, 0, 0, 0.5f, 1, 0, 0, -6, 0), "추가할 단어 (예: 가방) Enter");
            _wordInput.onEndEdit.AddListener(_ => { if (EnterPressed()) AddWord(); });
            var catBtn = Btn(Rect("Cat", addRow, 0.5f, 0, 0.82f, 1, 0, 0, -6, 0), "", 24, Brown, () =>
            {
                _addCategory = NextCategory(_addCategory);
                Refresh();
            });
            _addCategoryLabel = catBtn.GetComponentInChildren<Text>();
            Btn(Rect("Add", addRow, 0.82f, 0, 1, 1, 0, 0, 0, 0), "단어 추가", 24, Brown, AddWord);

            _listHeader = Txt(Rect("ListHeader", right, 0, 1, 1, 1, 16, -124, 0, -80), "", 26, Dim, TextAnchor.MiddleLeft);
            _wordContent = MakeScroll(Rect("WordScroll", right, 0, 0, 1, 1, 10, 10, -10, -130));
        }

        static RectTransform Rect(string name, Transform parent, float ax0, float ay0, float ax1, float ay1, float l, float b, float r, float t)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(ax0, ay0);
            rt.anchorMax = new Vector2(ax1, ay1);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(r, t);
            return rt;
        }

        static Image Img(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        Text Txt(RectTransform rt, string s, int size, Color color, TextAnchor anchor)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = s;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        Button Btn(RectTransform rt, string label, int size, Color color, UnityAction onClick)
        {
            var img = Img(rt, color);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(onClick);
            var textRt = Rect("Label", rt, 0, 0, 1, 1, 6, 0, -6, 0);
            Txt(textRt, label, size, Ink, TextAnchor.MiddleCenter);
            return b;
        }

        InputField MakeInput(RectTransform rt, string placeholderText)
        {
            Img(rt, new Color(0.95f, 0.9f, 0.78f, 1f));
            var field = rt.gameObject.AddComponent<InputField>();
            var dark = new Color(0.17f, 0.11f, 0.07f);
            var text = Txt(Rect("Text", rt, 0, 0, 1, 1, 12, 2, -12, -2), "", 26, dark, TextAnchor.MiddleLeft);
            var ph = Txt(Rect("Placeholder", rt, 0, 0, 1, 1, 12, 2, -12, -2), placeholderText, 24, new Color(dark.r, dark.g, dark.b, 0.45f), TextAnchor.MiddleLeft);
            ph.fontStyle = FontStyle.Italic;
            text.horizontalOverflow = ph.horizontalOverflow = HorizontalWrapMode.Overflow;
            field.textComponent = text;
            field.placeholder = ph;
            field.targetGraphic = rt.GetComponent<Image>();
            return field;
        }

        // 스크롤 영역을 만들고 내용(Content)을 돌려준다. 안에 넣은 줄은 세로로 쌓인다.
        static RectTransform MakeScroll(RectTransform area)
        {
            Img(area, new Color(0f, 0f, 0f, 0.25f));
            var scroll = area.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect("Viewport", area, 0, 0, 1, 1, 0, 0, 0, 0);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, 0, 1, 1, 1, 0, 0, 0, 0);
            content.pivot = new Vector2(0.5f, 1f);

            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6f;
            v.padding = new RectOffset(8, 8, 8, 8);
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return content;
        }

        // 목록의 한 줄(가로로 배치). 높이는 고정.
        static RectTransform NewRow(RectTransform parent, float height, Color color)
        {
            var rt = Rect("Row", parent, 0, 0, 1, 1, 0, 0, 0, 0);
            if (color.a > 0f) Img(rt, color);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(12, 8, 6, 6);
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return rt;
        }

        static void SetWidth(GameObject go, float preferred, float flexible)
        {
            var le = go.AddComponent<LayoutElement>();
            if (preferred > 0f) le.preferredWidth = preferred;
            le.flexibleWidth = flexible;
        }

        void AddLabel(RectTransform row, string s, int size, Color color, float preferred, float flexible)
        {
            var rt = Rect("Label", row, 0, 0, 1, 1, 0, 0, 0, 0);
            Txt(rt, s, size, color, TextAnchor.MiddleLeft);
            SetWidth(rt.gameObject, preferred, flexible);
        }

        void AddRowButton(RectTransform row, string label, int size, Color color, float preferred, float flexible, UnityAction onClick)
        {
            var rt = Rect("Button", row, 0, 0, 1, 1, 0, 0, 0, 0);
            Btn(rt, label, size, color, onClick);
            SetWidth(rt.gameObject, preferred, flexible);
        }
    }
}
