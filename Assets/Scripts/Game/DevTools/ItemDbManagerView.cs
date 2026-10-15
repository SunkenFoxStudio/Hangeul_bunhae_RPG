using System.Collections.Generic;
using Hangeul.App;
using Hangeul.Domain;
using Hangeul.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hangeul.Game.DevTools
{
    /// <summary>
    /// 아이템 단어 DB(item_words.json)를 보고 고치는 디버그 화면(러프). UI는 실행할 때 코드로 만든다.
    /// 왼쪽: 카테고리 목록(클릭하면 그 카테고리만 보기, 빈 카테고리는 ×로 삭제, 새 카테고리 추가).
    /// 오른쪽: 단어 추가 줄(단어 + 카테고리 선택) 아래에 단어 목록(카테고리 버튼을 누르면 다음 카테고리로 바뀜, ×로 삭제).
    /// 바꿀 때마다 파일에 바로 저장한다. 파일 위치는 에디터에서는 Assets/Resources, 빌드에서는 게임 실행 파일 옆이다
    /// (<see cref="ItemWordsStore"/>) — 화면 아래에 위치가 보이고, 버튼으로 그 폴더를 열 수 있다.
    /// </summary>
    public sealed class ItemDbManagerView : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] string returnScene = "SampleScene";

        static readonly Color Bg = new Color(0.10f, 0.08f, 0.06f, 1f);
        static readonly Color Panel = new Color(0.17f, 0.13f, 0.09f, 1f);
        static readonly Color Row = new Color(0.24f, 0.18f, 0.12f, 1f);
        static readonly Color Brown = new Color(0.55f, 0.39f, 0.19f, 1f);
        static readonly Color BrownSel = new Color(0.78f, 0.55f, 0.22f, 1f);
        static readonly Color Danger = new Color(0.55f, 0.22f, 0.18f, 1f);
        static readonly Color Gold = new Color(1f, 0.80f, 0.40f, 1f);        // 목록 안 글자 버튼: 선택된 것
        static readonly Color SoftRed = new Color(1f, 0.50f, 0.45f, 1f);     // 목록 안 글자 버튼: 삭제
        static readonly Color Ink = new Color(0.97f, 0.9f, 0.76f, 1f);
        static readonly Color Dim = new Color(0.85f, 0.7f, 0.45f, 1f);
        static readonly Color Warn = new Color(1f, 0.6f, 0.45f, 1f);

        ItemWordsFile _file;
        bool _canSave = true;
        string _loadMessage;   // 불러오다 생긴 문제: 화면이 만들어진 뒤 상태줄에 보여준다
        string _filter;        // null = 전체

        Text _status, _listHeader, _addCategoryLabel;
        InputField _wordInput, _categoryInput;
        RectTransform _catContent, _wordContent;

        // 접두어 선택 창(여러 개 고르기)
        RectTransform _popup, _popupContent;
        Text _popupTitle;
        ItemWordEntry _popupTarget;
        bool _popupIsCategory;   // true면 카테고리 고르기, false면 접두어 고르기

        void Awake()
        {
            EnsureEventSystem();
            Load();
            Build();
            Refresh();
            if (_loadMessage != null) SetStatus(_loadMessage, Warn);
        }

        // 진단용: 마우스를 누르거나 뗄 때 그 자리에 있는 UI를 위에서부터 콘솔에 적는다(클릭이 안 먹는 원인을 찾을 때 켠다).
        [SerializeField] bool logClickTargets = false;

        void Update()
        {
            if (!logClickTargets || Mouse.current == null || EventSystem.current == null) return;
            bool down = Mouse.current.leftButton.wasPressedThisFrame, up = Mouse.current.leftButton.wasReleasedThisFrame;
            if (!down && !up) return;

            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() }, hits);
            var names = new List<string>();
            for (int i = 0; i < hits.Count && i < 4; i++)
            {
                var g = hits[i].gameObject;
                names.Add(g.transform.parent != null ? $"{g.transform.parent.name}/{g.name}" : g.name);
            }
            Debug.Log($"[ItemDb] {(down ? "누름" : "뗌")}: {(names.Count == 0 ? "(UI 없음)" : string.Join("  >  ", names))}");
        }

        // EventSystem이 없을 때만 만든다. 새 UI 입력 모듈은 켜질 때 기본 입력을 스스로 연결한다.
        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        // ───── 데이터 ─────
        void Load()
        {
            // 빌드에서 목록 파일이 아직 없으면 기본 목록을 실행 파일 옆에 복사해 둔다(저장할 파일이 눈에 보이게)
            if (!ItemWordsStore.EnsureFile(out string ensureError))
                _loadMessage = "저장 파일을 만들 수 없습니다: " + ensureError;

            string json = ItemWordsStore.ReadCurrent();
            if (json != null)
            {
                try { _file = JsonUtility.FromJson<ItemWordsFile>(json); }
                catch (System.Exception e)
                {
                    _file = null;
                    _canSave = false;   // 읽지 못한 파일을 빈 목록으로 덮어쓰지 않는다
                    _loadMessage = "목록 파일을 읽을 수 없어 저장하지 않습니다: " + e.Message;
                }
            }

            if (_file == null)
            {
                _file = new ItemWordsFile { schemaVersion = 1 };
            }
            else if (_file.schemaVersion != 1)
            {
                _canSave = false;   // 모르는 형식을 덮어쓰지 않는다
                _loadMessage = "파일 형식(schemaVersion)이 달라 저장하지 않습니다.";
            }
            _file.categories ??= new List<string>();
            _file.items ??= new List<ItemWordEntry>();
        }

        // 접두어 카테고리에서 빠졌거나 삭제된 단어를 각 단어의 접두어 목록에서 지운다.
        void PrunePrefixes()
        {
            var valid = new HashSet<string>();
            foreach (var it in _file.items)
                if (ItemWordSet.IsPrefixCategory(it.category)) valid.Add(it.word);
            foreach (var it in _file.items)
            {
                it.prefixes ??= new List<string>();
                it.prefixes.RemoveAll(p => !valid.Contains(p));
            }
        }

        void Save(string message)
        {
            PrunePrefixes();
            if (!_canSave) { SetStatus("목록 파일을 읽지 못했거나 형식(schemaVersion)이 달라 저장하지 않았습니다.", Warn); return; }
            if (ItemWordsStore.TryWrite(JsonUtility.ToJson(_file, true), out string error)) SetStatus(message + "  (저장됨)", Ink);
            else SetStatus($"{message}  (저장 실패: {error})", Warn);
        }

        // ───── 동작 ─────
        void AddWord()
        {
            string w = (_wordInput.text ?? "").Trim();
            if (w.Length == 0) return;
            if (_file.categories.Count == 0) { SetStatus("먼저 카테고리를 추가하세요.", Warn); return; }
            if (_filter == null) { SetStatus("왼쪽에서 단어를 넣을 카테고리를 먼저 선택하세요.", Warn); return; }   // 지금 보고 있는 카테고리에 들어간다
            if (!ItemWordSet.IsValidWord(w)) { SetStatus($"한글 음절로만 쓸 수 있습니다: {w}", Warn); return; }
            foreach (char ch in w)
                if (ForbiddenSyllables.IsBanned(ch)) { SetStatus($"금지 음절('{ch}')이 들어 있습니다: {w}", Warn); return; }
            if (_file.items.Exists(i => i.word == w)) { SetStatus($"이미 있는 단어입니다: {w}", Warn); return; }

            _file.items.Insert(0, new ItemWordEntry { word = w, category = _filter });
            _wordInput.text = "";
            Save($"추가: {w} ({_filter})");
            Refresh();
            _wordInput.ActivateInputField();
        }

        void AddCategory()
        {
            string c = (_categoryInput.text ?? "").Trim();
            if (c.Length == 0) return;
            if (_file.categories.Contains(c)) { SetStatus($"이미 있는 카테고리입니다: {c}", Warn); return; }

            _file.categories.Add(c);
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
            Save($"카테고리 삭제: {c}");
            Refresh();
        }

        void RemoveWord(ItemWordEntry e)
        {
            _file.items.Remove(e);
            Save($"삭제: {e.word}");
            Refresh();
        }

        void MoveWordCategory(ItemWordEntry e, string category)
        {
            _popup.gameObject.SetActive(false);
            if (e.category == category) return;
            e.category = category;
            if (ItemWordSet.IsPrefixCategory(e.category)) { e.acceptsPrefix = false; e.prefixes.Clear(); }   // 접두어/속성 단어 자신은 접두어를 받지 않는다
            Save($"{e.word} → {e.category}");
            Refresh();
        }

        void SetAcceptsPrefix(ItemWordEntry e, bool on)
        {
            e.acceptsPrefix = on;
            Save($"{e.word}: 접두어 {(on ? "가능" : "불가")}");
            Refresh();
        }

        void TogglePrefix(ItemWordEntry e, string prefix)
        {
            if (!e.prefixes.Remove(prefix)) e.prefixes.Add(prefix);
            Save($"{e.word}: 접두어 {string.Join(", ", e.prefixes)}");
            Refresh();
        }

        void OpenPrefixPopup(ItemWordEntry e)
        {
            _popupTarget = e;
            _popupIsCategory = false;
            _popup.gameObject.SetActive(true);
            RefreshPopup();
        }

        void OpenCategoryPopup(ItemWordEntry e)
        {
            _popupTarget = e;
            _popupIsCategory = true;
            _popup.gameObject.SetActive(true);
            RefreshPopup();
        }

        void RefreshPopup()
        {
            if (_popup == null || !_popup.gameObject.activeSelf) return;
            if (_popupTarget == null || !_file.items.Contains(_popupTarget)) { _popup.gameObject.SetActive(false); return; }
            if (_popupIsCategory) { RefreshCategoryPopup(); return; }

            // 고른 접두어로 만들어지는 새 단어를 같이 보여준다
            string made = string.Join(", ", _popupTarget.prefixes.ConvertAll(p => p + _popupTarget.word));
            _popupTitle.text = $"'{_popupTarget.word}' 앞에 붙을 수 있는 접두어\n<size=20>{(made.Length == 0 ? "새 단어 없음" : "→ " + made)}</size>";
            Clear(_popupContent);
            int n = 0;
            foreach (var category in ItemWordSet.PrefixCategories)   // 접두어, 속성 순으로 묶어서 보여준다
            {
                foreach (var it in _file.items)
                {
                    if (it.category != category) continue;
                    n++;
                    string prefix = it.word;
                    bool on = _popupTarget.prefixes.Contains(prefix);
                    var row = NewRow(_popupContent, 52, Color.clear);
                    AddRowButton(row, $"{(on ? "✔ " : "")}{prefix}  <size=20>{category}</size>", 28, on ? Gold : Ink, 0f, 1f, () => TogglePrefix(_popupTarget, prefix));
                }
            }
            if (n == 0)
            {
                var row = NewRow(_popupContent, 52, Color.clear);
                AddLabel(row, $"'{string.Join("', '", ItemWordSet.PrefixCategories)}' 카테고리에 단어가 없습니다.", 24, Dim, 0f, 1f);
            }
        }

        // 단어를 옮길 카테고리를 고르는 창: 하나를 누르면 그 카테고리로 옮기고 닫는다
        void RefreshCategoryPopup()
        {
            _popupTitle.text = $"'{_popupTarget.word}'을(를) 옮길 카테고리{"\n"}<size=20>현재: {(string.IsNullOrEmpty(_popupTarget.category) ? "(없음)" : _popupTarget.category)}</size>";
            Clear(_popupContent);
            var target = _popupTarget;
            foreach (var category in _file.categories)
            {
                string c = category;
                bool current = target.category == c;
                var row = NewRow(_popupContent, 52, Color.clear);
                AddRowButton(row, (current ? "● " : "") + c, 28, current ? Gold : Ink, 0f, 1f, () => MoveWordCategory(target, c));
            }
        }

        void SetStatus(string message, Color color)
        {
            if (_status == null) return;
            _status.text = message;
            _status.color = color;
        }

        // ───── 화면 갱신 ─────
        void Refresh()
        {
            RefreshCategories();
            RefreshWords();
            RefreshPopup();
            // 단어는 지금 선택한 카테고리에 들어간다. 어디에 들어갈지 입력창 옆에 보여준다.
            _addCategoryLabel.text = _filter == null ? "← 카테고리를 선택하세요" : $"→ {_filter}";
            _addCategoryLabel.color = _filter == null ? Warn : Dim;
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
            AddRowButton(row, label, 26, selected ? Gold : Ink, 0f, 1f, () => { _filter = filter; Refresh(); });
            if (deletable) AddRowButton(row, "×", 26, SoftRed, 52f, 0f, () => RemoveCategory(key));
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
                AddRowButton(row, string.IsNullOrEmpty(entry.category) ? "(없음)" : entry.category, 24, Ink, 220f, 0f, () => OpenCategoryPopup(entry));
                AddPrefixControls(row, entry);
                AddRowButton(row, "×", 26, SoftRed, 56f, 0f, () => RemoveWord(entry));
            }
            _listHeader.text = $"단어 {shown}개  ·  {(_filter ?? "전체")}";
        }

        // 한 줄의 접두어 칸: [체크박스 접두어 가능] [붙을 수 있는 접두어 선택(창)]. 접두어 단어 자신에게는 쓰지 않는다.
        void AddPrefixControls(RectTransform row, ItemWordEntry e)
        {
            if (ItemWordSet.IsPrefixCategory(e.category))
            {
                AddLabel(row, "(앞에 붙는 단어)", 22, Dim, 500f, 0f);
                return;
            }

            // 체크박스
            var box = Rect("PrefixCheck", row, 0, 0, 1, 1, 0, 0, 0, 0);
            TextButton(box, "", 22, Ink, () => SetAcceptsPrefix(e, !e.acceptsPrefix));
            SetWidth(box.gameObject, 190f, 0f);
            var frame = Rect("Box", box, 0, 0.5f, 0, 0.5f, 10, -16, 42, 16);
            Img(frame, new Color(0.10f, 0.08f, 0.06f, 1f)).raycastTarget = false;
            var mark = Rect("Mark", frame, 0, 0, 1, 1, 5, 5, -5, -5);
            Img(mark, e.acceptsPrefix ? Ink : Color.clear).raycastTarget = false;
            Txt(Rect("Text", box, 0, 0, 1, 1, 52, 0, -6, 0), "접두어 가능", 22, e.acceptsPrefix ? Ink : Dim, TextAnchor.MiddleLeft);

            // 붙을 수 있는 접두어 선택
            if (!e.acceptsPrefix)
            {
                AddLabel(row, "—", 24, Dim, 300f, 0f);
                return;
            }
            string summary = e.prefixes.Count == 0 ? "접두어 선택…" : string.Join(", ", e.prefixes.ConvertAll(p => p + e.word));   // 만들어지는 새 단어 이름
            if (summary.Length > 14) summary = summary.Substring(0, 13) + "…";
            AddRowButton(row, summary, 22, e.prefixes.Count == 0 ? Dim : Gold, 300f, 0f, () => OpenPrefixPopup(e));
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
            var left = Rect("Left", root, 0, 0, 0, 1, 20, 70, 440, -100);
            Img(left, Panel);
            Txt(Rect("Header", left, 0, 1, 1, 1, 16, -50, 0, 0), "카테고리", 30, Dim, TextAnchor.MiddleLeft);
            _catContent = MakeScroll(Rect("CatScroll", left, 0, 0, 1, 1, 10, 80, -10, -52));
            var catAdd = Rect("CatAdd", left, 0, 0, 1, 0, 10, 10, -10, 70);
            _categoryInput = MakeInput(Rect("Input", catAdd, 0, 0, 0.68f, 1, 0, 0, -6, 0), "새 카테고리 (Enter)");
            _categoryInput.onSubmit.AddListener(_ => AddCategory());   // onSubmit = 입력칸에서 Enter를 눌렀을 때만(포커스를 잃은 경우는 오지 않는다)
            Btn(Rect("Add", catAdd, 0.68f, 0, 1, 1, 0, 0, 0, 0), "추가", 24, Brown, AddCategory);

            // 오른쪽: 단어 추가 + 목록
            var right = Rect("Right", root, 0, 0, 1, 1, 460, 70, -20, -100);
            Img(right, Panel);
            var addRow = Rect("AddRow", right, 0, 1, 1, 1, 10, -76, -10, -10);
            _wordInput = MakeInput(Rect("Word", addRow, 0, 0, 0.6f, 1, 0, 0, -6, 0), "추가할 단어 (예: 가방) Enter");
            _wordInput.onSubmit.AddListener(_ => AddWord());
            _addCategoryLabel = Txt(Rect("Target", addRow, 0.6f, 0, 0.82f, 1, 6, 0, -6, 0), "", 24, Dim, TextAnchor.MiddleLeft);
            Btn(Rect("Add", addRow, 0.82f, 0, 1, 1, 0, 0, 0, 0), "단어 추가", 24, Brown, AddWord);

            _listHeader = Txt(Rect("ListHeader", right, 0, 1, 1, 1, 16, -124, 0, -80), "", 26, Dim, TextAnchor.MiddleLeft);
            _wordContent = MakeScroll(Rect("WordScroll", right, 0, 0, 1, 1, 10, 10, -10, -130));

            // 아래 줄: 목록 파일이 어디에 저장되는지와, 그 폴더를 여는 버튼(빌드에서는 실행 파일 옆 — 동료에게 보낼 파일)
            var footer = Rect("Footer", root, 0, 0, 1, 0, 20, 10, -20, 60);
            Txt(Rect("FilePath", footer, 0, 0, 0.8f, 1, 4, 0, -10, 0), "저장 위치: " + ItemWordsStore.FilePath, 20, Dim, TextAnchor.MiddleLeft);
            Btn(Rect("OpenFolder", footer, 0.8f, 0, 1, 1, 0, 0, 0, 0), "저장 폴더 열기", 24, Brown, ItemWordsStore.OpenFolder);

            // 접두어 선택 창: 화면 전체를 덮는 어두운 막 위에 가운데 패널. 평소에는 꺼 둔다.
            _popup = Rect("PrefixPopup", root, 0, 0, 1, 1, 0, 0, 0, 0);
            Img(_popup, new Color(0f, 0f, 0f, 0.65f));   // 뒤쪽 클릭을 막는다
            var win = Rect("Window", _popup, 0.5f, 0.5f, 0.5f, 0.5f, -360, -380, 360, 380);
            Img(win, Panel);
            _popupTitle = Txt(Rect("Title", win, 0, 1, 1, 1, 20, -70, -20, 0), "", 28, Ink, TextAnchor.MiddleLeft);
            _popupContent = MakeScroll(Rect("Scroll", win, 0, 0, 1, 1, 16, 90, -16, -76));
            Btn(Rect("Close", win, 0.5f, 0, 0.5f, 0, -100, 16, 100, 70), "닫기", 26, Brown, () => _popup.gameObject.SetActive(false));
            _popup.gameObject.SetActive(false);
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

        // 배경 없이 글자만 보이는 버튼(목록 안 줄에서 쓴다). 투명한 이미지가 클릭을 받는다.
        // 투명 메시는 기본적으로 그려지지도, 클릭을 받지도 않으므로 cullTransparentMesh를 끈다.
        Button TextButton(RectTransform rt, string label, int size, Color textColor, UnityAction onClick)
        {
            var img = Img(rt, Color.clear);
            img.canvasRenderer.cullTransparentMesh = false;
            var b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = img;
            b.onClick.AddListener(onClick);
            Txt(Rect("Label", rt, 0, 0, 1, 1, 6, 0, -6, 0), label, size, textColor, TextAnchor.MiddleCenter);
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
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;   // 버튼은 자체 높이를 갖지 않는다. 줄 높이만큼 늘리지 않으면 높이 0이 되어 보이지도, 눌리지도 않는다.
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
            TextButton(rt, label, size, color, onClick);   // color는 글자색
            SetWidth(rt.gameObject, preferred, flexible);
        }
    }
}
