using System.Collections.Generic;
using System.Globalization;
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
    /// 무기·음식 단어 줄에는 "상세 수치 열기" 버튼이 있어, 누르면 그 줄 아래에 밝은 배경의 수치 패널이 열리고 닫힌다
    /// (무기: 피해량·밀어내기·경직·범위·시간, 음식: 회복량). 값을 확정(Enter나 다른 곳 클릭)할 때마다 저장한다.
    /// 접두어·속성 단어의 패널에서는 효과의 종류를 고른다 — 대미지 더하기(피해량에 더하기/곱하기) 또는 속성 묻히기(때릴 때 묻을 확률과
    /// 적 머리 위에 뜨는 글자의 색).
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
        // 상세 수치 패널: 어두운 목록 위에서 눈에 띄는 밝은 배경
        static readonly Color StatsBg = new Color(0.93f, 0.88f, 0.74f, 1f);
        static readonly Color StatsInk = new Color(0.17f, 0.11f, 0.07f, 1f);
        static readonly Color StatsHint = new Color(0.17f, 0.11f, 0.07f, 0.65f);
        static readonly Color StatsInputBg = new Color(1f, 0.99f, 0.95f, 1f);

        ItemWordsFile _file;
        bool _canSave = true;
        string _loadMessage;   // 불러오다 생긴 문제: 화면이 만들어진 뒤 상태줄에 보여준다
        string _filter;        // null = 전체
        readonly HashSet<string> _openStats = new HashSet<string>();   // 상세 수치 패널이 열려 있는 단어(목록을 다시 그려도 유지)
        readonly Dictionary<string, Text> _toggleLabels = new Dictionary<string, Text>();   // 단어 줄의 "상세 수치 열기/닫기" 글자(●를 바로 붙이고 떼기 위해)

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
            _file.wordStats ??= new List<WordStatsEntry>();
            _file.wordStats.RemoveAll(w => w == null || string.IsNullOrEmpty(w.word));
            foreach (var w in _file.wordStats) w.stats ??= new ItemStats();
        }

        // 삭제된 단어의 수치와 같은 단어의 중복 수치를 지운다.
        void PruneStats()
        {
            var words = new HashSet<string>();
            foreach (var it in _file.items) words.Add(it.word);
            var seen = new HashSet<string>();
            _file.wordStats.RemoveAll(w => w == null || w.stats == null || !words.Contains(w.word) || !seen.Add(w.word));
            _openStats.RemoveWhere(w => !words.Contains(w));
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
            PruneStats();
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
            _toggleLabels.Clear();
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
                var kind = ItemKinds.FromCategory(entry.category);
                AddStatsToggle(row, entry, kind);
                AddRowButton(row, "×", 26, SoftRed, 56f, 0f, () => RemoveWord(entry));
                if ((kind != ItemKind.None || ItemWordSet.IsPrefixCategory(entry.category)) && _openStats.Contains(entry.word))
                    AddStatsPanel(entry, kind);   // 그 단어 줄 바로 아래
            }
            _listHeader.text = $"단어 {shown}개  ·  {(_filter ?? "전체")}";
        }

        // ───── 상세 수치 ─────
        // 수치를 읽고 고치는 방법(이름표, 정수 여부, 읽기, 쓰기). 값은 ItemStats가 범위에 맞춰 준다(Clamped).
        readonly struct StatDef
        {
            public readonly string Label;
            public readonly bool Integer;
            public readonly System.Func<ItemStats, float> Get;
            public readonly System.Action<ItemStats, float> Set;

            public StatDef(string label, bool integer, System.Func<ItemStats, float> get, System.Action<ItemStats, float> set)
            {
                Label = label; Integer = integer; Get = get; Set = set;
            }
        }

        static readonly StatDef DamageDef = new StatDef("피해량", true, s => s.damage, (s, v) => s.damage = Mathf.RoundToInt(v));
        static readonly StatDef KnockbackDef = new StatDef("밀어내기", false, s => s.knockback, (s, v) => s.knockback = v);
        static readonly StatDef StunDef = new StatDef("경직", false, s => s.stun, (s, v) => s.stun = v);
        static readonly StatDef ReachDef = new StatDef("길이", false, s => s.reach, (s, v) => s.reach = v);
        static readonly StatDef WidthDef = new StatDef("폭", false, s => s.width, (s, v) => s.width = v);
        static readonly StatDef WindupDef = new StatDef("준비", false, s => s.windup, (s, v) => s.windup = v);
        static readonly StatDef ActiveDef = new StatDef("판정", false, s => s.active, (s, v) => s.active = v);
        static readonly StatDef RecoveryDef = new StatDef("마무리", false, s => s.recovery, (s, v) => s.recovery = v);
        static readonly StatDef HealDef = new StatDef("회복량", true, s => s.heal, (s, v) => s.heal = Mathf.RoundToInt(v));
        static readonly StatDef DamageAmountDef = new StatDef("값", false, s => s.damageAmount, (s, v) => s.damageAmount = v);
        static readonly StatDef ElementChanceDef = new StatDef("확률(%)", false, s => s.elementChance, (s, v) => s.elementChance = v);

        static readonly ItemStats DefaultStats = new ItemStats();   // 읽기 전용: 수치를 따로 정하지 않은 단어가 보여주는 기본값
        static readonly ItemStats DefaultElementStats = new ItemStats { prefixKind = PrefixEffectKind.Element };   // 속성 카테고리는 '속성 묻히기'(확률 0%)가 기본
        const string ElementCategory = "속성";

        // 수치를 따로 정하지 않은 단어가 보여주는 기본값(읽기 전용). 접두어·속성 단어의 기본은 효과 없음(더하기 0 / 확률 0%)이라 보이는 값과 게임 동작이 같다.
        static ItemStats DefaultFor(ItemWordEntry e) => e.category == ElementCategory ? DefaultElementStats : DefaultStats;

        ItemStats StatsOrDefault(ItemWordEntry e) => FindStats(e.word) ?? DefaultFor(e);

        ItemStats FindStats(string word)
        {
            foreach (var w in _file.wordStats)
                if (w != null && w.word == word) return w.stats;
            return null;
        }

        // 이 단어의 수치를 고치려고 가져온다. 아직 따로 정한 적이 없으면 기본값으로 새로 만들어 목록에 넣는다.
        ItemStats StatsForEdit(ItemWordEntry e)
        {
            var s = FindStats(e.word);
            if (s != null) return s;
            s = DefaultFor(e).Clone();
            _file.wordStats.Add(new WordStatsEntry { word = e.word, stats = s });
            return s;
        }

        static string FormatNumber(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        static bool TryParseNumber(string text, out float value)
            => float.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsInfinity(value);

        // 무기·음식 줄 끝의 열기/닫기 버튼. 다른 종류의 단어는 칸만 비워 둬서 줄의 열이 맞는다.
        void AddStatsToggle(RectTransform row, ItemWordEntry e, ItemKind kind)
        {
            const float Width = 210f;
            if (kind == ItemKind.None && !ItemWordSet.IsPrefixCategory(e.category)) { AddLabel(row, "", 21, Dim, Width, 0f); return; }

            bool open = _openStats.Contains(e.word);
            var button = AddRowButton(row, ToggleText(e, open), 21, open ? Gold : Ink, Width, 0f, () =>
            {
                if (!_openStats.Remove(e.word)) _openStats.Add(e.word);
                RefreshWords();
            });
            _toggleLabels[e.word] = button.GetComponentInChildren<Text>();
        }

        // ●는 이 단어만의 수치를 정해 둔 단어
        string ToggleText(ItemWordEntry e, bool open)
            => (FindStats(e.word) != null ? "● " : "") + (open ? "상세 수치 닫기" : "상세 수치 열기");

        // 단어 줄 바로 아래에 열리는 밝은 배경의 수치 패널
        void AddStatsPanel(ItemWordEntry e, ItemKind kind)
        {
            var panel = Rect("Stats", _wordContent, 0, 0, 1, 1, 0, 0, 0, 0);
            Img(panel, StatsBg);
            var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(28, 28, 14, 16);
            v.spacing = 10f;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;

            // 맨 위 줄: 지금 수치가 기본값인지 이 단어만의 값인지 + 기본값으로 되돌리기
            var head = NewPlainRow(panel, 40f);
            var noteRt = Rect("Note", head, 0, 0, 1, 1, 0, 0, 0, 0);
            var note = Txt(noteRt, "", 22, StatsHint, TextAnchor.MiddleLeft);
            SetWidth(noteRt.gameObject, 0f, 1f);
            var resetRt = Rect("Reset", head, 0, 0, 1, 1, 0, 0, 0, 0);
            var reset = Btn(resetRt, "기본값으로", 22, Brown, () => ResetStats(e));
            SetWidth(resetRt.gameObject, 170f, 0f);

            void UpdateNote()
            {
                bool custom = FindStats(e.word) != null;
                note.text = custom ? "이 단어만의 수치입니다."
                    : ItemWordSet.IsPrefixCategory(e.category) ? "아직 효과를 정하지 않았습니다. 값을 고치면 이 단어만의 효과가 생깁니다."
                    : "기본 수치입니다. 값을 고치면 이 단어만의 수치가 됩니다.";
                reset.gameObject.SetActive(custom);
                // 줄의 토글 글자에도 ●를 바로 붙인다(패널이 열려 있는 동안만 불리므로 '닫기' 글자)
                if (_toggleLabels.TryGetValue(e.word, out var toggle) && toggle != null) toggle.text = ToggleText(e, true);
            }
            UpdateNote();

            if (ItemWordSet.IsPrefixCategory(e.category))
            {
                AddPrefixRows(panel, e, UpdateNote);
            }
            else if (kind == ItemKind.Weapon)
            {
                AddStatRow(panel, e, "피해", null, UpdateNote, DamageDef, KnockbackDef, StunDef);
                AddStatRow(panel, e, "범위 (유닛)", null, UpdateNote, ReachDef, WidthDef);
                AddStatRow(panel, e, "시간 (초)", null, UpdateNote, WindupDef, ActiveDef, RecoveryDef);
            }
            else if (kind == ItemKind.Food)
            {
                AddStatRow(panel, e, "회복", "체력 2 = 하트 한 칸", UpdateNote, HealDef);
            }
        }

        const float PanelRowHeight = 52f;   // 수치 패널의 한 줄 높이(정사각형 버튼의 한 변도 이 값)

        void AddGroupLabel(RectTransform row, string text)
        {
            var g = Rect("Group", row, 0, 0, 1, 1, 0, 0, 0, 0);
            Txt(g, text, 24, StatsInk, TextAnchor.MiddleLeft);
            SetWidth(g.gameObject, 150f, 0f);
        }

        // 둘 중 하나를 고르는 버튼. 지금 고른 것은 밝은 색에 ✔가 붙는다.
        void AddChoice(RectTransform row, string label, bool selected, UnityAction onClick)
        {
            var rt = Rect("Choice", row, 0, 0, 1, 1, 0, 0, 0, 0);
            Btn(rt, (selected ? "✔ " : "") + label, 24, selected ? BrownSel : Brown, onClick);
            SetWidth(rt.gameObject, 190f, 0f);
        }

        // 기호 하나만 적은 정사각형 선택 버튼(+, ×). 줄 높이와 같은 폭이라 정사각형이 된다. 지금 고른 것은 밝은 바탕에 흰 글자.
        void AddSquareChoice(RectTransform row, string symbol, bool selected, UnityAction onClick)
        {
            var rt = Rect("Choice", row, 0, 0, 1, 1, 0, 0, 0, 0);
            var button = Btn(rt, symbol, 34, selected ? BrownSel : Brown, onClick);
            button.GetComponentInChildren<Text>().color = selected ? Color.white : Ink;
            SetWidth(rt.gameObject, PanelRowHeight, 0f);
        }

        // 접두어·속성 단어의 패널: 효과의 종류(대미지 더하기 / 속성 묻히기)를 고르고, 종류에 맞는 값을 적는다.
        void AddPrefixRows(RectTransform panel, ItemWordEntry e, System.Action onChanged)
        {
            var stats = StatsOrDefault(e);

            var kindRow = NewPlainRow(panel, 52f);
            AddGroupLabel(kindRow, "종류");
            AddChoice(kindRow, "대미지 더하기", stats.prefixKind == PrefixEffectKind.Damage, () => SetPrefixKind(e, PrefixEffectKind.Damage));
            AddChoice(kindRow, "속성 묻히기", stats.prefixKind == PrefixEffectKind.Element, () => SetPrefixKind(e, PrefixEffectKind.Element));
            var kindSpacer = Rect("Spacer", kindRow, 0, 0, 1, 1, 0, 0, 0, 0);
            SetWidth(kindSpacer.gameObject, 0f, 1f);

            if (stats.prefixKind == PrefixEffectKind.Damage)
            {
                // 이 접두어가 붙은 무기('강화검')의 피해량을 더하거나 곱한다. 예시 줄로 결과를 바로 보여준다.
                var row = NewPlainRow(panel, 52f);
                AddGroupLabel(row, "대미지");
                AddSquareChoice(row, "+", stats.damageMode == DamageMode.Add, () => SetDamageMode(e, DamageMode.Add));        // 더하기
                AddSquareChoice(row, "×", stats.damageMode == DamageMode.Multiply, () => SetDamageMode(e, DamageMode.Multiply));   // 곱하기

                var exampleRt = Rect("Example", row, 0, 0, 1, 1, 0, 0, 0, 0);
                var example = Txt(exampleRt, "", 22, StatsHint, TextAnchor.MiddleLeft);
                SetWidth(exampleRt.gameObject, 0f, 1f);
                void UpdateExample()
                {
                    var now = StatsOrDefault(e);
                    example.text = $"예) 기본 피해량 3 → {WeaponModifier.ApplyDamage(3, now.damageMode, now.damageAmount)}";
                }
                UpdateExample();
                // 값 칸은 예시 글자보다 앞에 오도록 순서를 바꾼다
                AddStatCell(row, e, DamageAmountDef, () => { onChanged?.Invoke(); UpdateExample(); });
                exampleRt.SetAsLastSibling();
            }
            else
            {
                // 때릴 때마다 이 확률로 단어(속성)가 적에게 묻고, 적 머리 위에 "단어!"가 뜬다
                var row = NewPlainRow(panel, 52f);
                AddGroupLabel(row, "속성");
                AddStatCell(row, e, ElementChanceDef, onChanged);
                var hintRt = Rect("Hint", row, 0, 0, 1, 1, 0, 0, 0, 0);
                Txt(hintRt, $"때릴 때 이 확률로 묻으면 적 머리 위에 \"{e.word}!\"가 뜹니다", 22, StatsHint, TextAnchor.MiddleLeft);
                SetWidth(hintRt.gameObject, 0f, 2f);

                AddColorRow(panel, e, onChanged);
            }
        }

        static Color ColorOfHex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;

        // 속성 글자의 색: 적 머리 위에 뜨는 모양 그대로의 미리보기 + #RRGGBB 입력칸 + 추천 색 버튼
        void AddColorRow(RectTransform panel, ItemWordEntry e, System.Action onChanged)
        {
            var row = NewPlainRow(panel, PanelRowHeight);
            AddGroupLabel(row, "글자 색");

            // 미리보기: 게임처럼 어두운 곳 위의 "화염!"
            var chipRt = Rect("Preview", row, 0, 0, 1, 1, 0, 0, 0, 0);
            Img(chipRt, new Color(0.17f, 0.12f, 0.08f, 1f));
            var preview = Txt(Rect("Text", chipRt, 0, 0, 1, 1, 6, 0, -6, 0), e.word + "!", 28, Color.white, TextAnchor.MiddleCenter);
            preview.fontStyle = FontStyle.Bold;
            SetWidth(chipRt.gameObject, 150f, 0f);

            var hexRt = Rect("Hex", row, 0, 0, 1, 1, 0, 0, 0, 0);
            var field = MakeInput(hexRt, "#RRGGBB", StatsInputBg, SafeInputField.InputKind.Hex);
            field.characterLimit = 7;
            SetWidth(hexRt.gameObject, 150f, 0f);

            void Show(string hex)
            {
                field.text = hex;
                preview.color = ColorOfHex(hex);
            }
            Show(ColorHex.NormalizeOr(StatsOrDefault(e).elementColor, ColorHex.DefaultElement));

            void Choose(string hex)
            {
                var edited = StatsOrDefault(e).Clone();
                edited.elementColor = hex;
                bool changed = ApplyEdit(e, edited, $"{e.word}: 글자 색 {hex}");
                Show(hex);
                if (changed) onChanged?.Invoke();
            }

            field.onEndEdit.AddListener(_ =>
            {
                if (ColorHex.TryNormalize(field.text, out string hex)) Choose(hex);
                else
                {
                    Show(ColorHex.NormalizeOr(StatsOrDefault(e).elementColor, ColorHex.DefaultElement));   // 색이 아니면 원래 색으로
                    SetStatus("색은 #RRGGBB 형식으로 적어 주세요. 예) #FF9E33", Warn);
                }
            });

            // 추천 색: 정사각형 버튼들. 레이아웃 그룹 없이 왼쪽부터 직접 놓는다.
            const float Size = 44f, Step = 50f;
            var paletteRt = Rect("Palette", row, 0, 0, 1, 1, 0, 0, 0, 0);
            SetWidth(paletteRt.gameObject, ColorHex.ElementPresets.Length * Step, 0f);
            for (int i = 0; i < ColorHex.ElementPresets.Length; i++)
            {
                string hex = ColorHex.ElementPresets[i];
                var swatch = Rect("Swatch", paletteRt, 0, 0.5f, 0, 0.5f, i * Step, -Size / 2f, i * Step + Size, Size / 2f);
                Btn(swatch, "", 20, ColorOfHex(hex), () => Choose(hex));
            }

            var spacer = Rect("Spacer", row, 0, 0, 1, 1, 0, 0, 0, 0);
            SetWidth(spacer.gameObject, 0f, 1f);
        }

        // 효과의 종류를 바꾼다. 바로 효과가 보이도록 시작 값을 넣고(속성 묻히기 30%, 대미지 +1), 패널을 다시 그린다.
        void SetPrefixKind(ItemWordEntry e, PrefixEffectKind kind)
        {
            var current = StatsOrDefault(e);
            if (current.prefixKind == kind) return;
            var edited = current.Clone();
            edited.prefixKind = kind;
            if (kind == PrefixEffectKind.Element) edited.elementChance = 30f;
            else { edited.damageMode = DamageMode.Add; edited.damageAmount = 1f; }
            ApplyEdit(e, edited, $"{e.word}: {(kind == PrefixEffectKind.Element ? "속성 묻히기" : "대미지 더하기")}");
            RefreshWords();
        }

        // 더하기 ↔ 곱하기. 계산 방식이 바뀌면 같은 숫자의 뜻이 달라지므로 시작 값을 넣는다(더하기 +1, 곱하기 ×1.5).
        void SetDamageMode(ItemWordEntry e, DamageMode mode)
        {
            var current = StatsOrDefault(e);
            if (current.damageMode == mode) return;
            var edited = current.Clone();
            edited.damageMode = mode;
            edited.damageAmount = mode == DamageMode.Multiply ? 1.5f : 1f;
            ApplyEdit(e, edited, $"{e.word}: 대미지 {(mode == DamageMode.Multiply ? "곱하기" : "더하기")}");
            RefreshWords();
        }

        // 범위에 맞춘 값이 지금과 다르면 이 단어만의 수치로 저장한다. 바뀌었으면 true.
        bool ApplyEdit(ItemWordEntry e, ItemStats edited, string message)
        {
            edited = edited.Clamped();
            if (edited.SameAs(StatsOrDefault(e))) return false;   // 바뀐 게 없으면 '이 단어만의 수치'로 만들지 않는다
            StatsForEdit(e).CopyFrom(edited);
            Save(message);
            return true;
        }

        // 수치 패널의 한 줄: [묶음 이름] [이름표 + 입력칸] ×3 (모자라는 칸은 비워 둔다. 첫 빈칸에는 hint를 적는다)
        void AddStatRow(RectTransform panel, ItemWordEntry e, string group, string hint, System.Action onChanged, params StatDef[] defs)
        {
            const int Columns = 3;
            var row = NewPlainRow(panel, 52f);
            AddGroupLabel(row, group);

            foreach (var def in defs) AddStatCell(row, e, def, onChanged);
            for (int i = defs.Length; i < Columns; i++)
            {
                var sp = Rect("Spacer", row, 0, 0, 1, 1, 0, 0, 0, 0);
                if (i == defs.Length && !string.IsNullOrEmpty(hint)) Txt(sp, hint, 22, StatsHint, TextAnchor.MiddleLeft);
                SetWidth(sp.gameObject, 0f, 1f);
            }
        }

        void AddStatCell(RectTransform row, ItemWordEntry e, StatDef def, System.Action onChanged)
        {
            var cell = Rect("Stat", row, 0, 0, 1, 1, 0, 0, 0, 0);
            var h = cell.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            SetWidth(cell.gameObject, 0f, 1f);

            var labelRt = Rect("Label", cell, 0, 0, 1, 1, 0, 0, 0, 0);
            Txt(labelRt, def.Label, 24, StatsInk, TextAnchor.MiddleLeft);
            SetWidth(labelRt.gameObject, 112f, 0f);

            var inputRt = Rect("Input", cell, 0, 0, 1, 1, 0, 0, 0, 0);
            var field = MakeInput(inputRt, "", StatsInputBg, SafeInputField.InputKind.Number);
            field.contentType = def.Integer ? InputField.ContentType.IntegerNumber : InputField.ContentType.DecimalNumber;
            field.characterLimit = 8;
            field.text = FormatNumber(def.Get(StatsOrDefault(e)));
            SetWidth(inputRt.gameObject, 150f, 0f);

            // 칸을 벗어나거나 Enter를 눌러 입력이 끝날 때 확정한다. 목록을 다시 그리지 않아서 다음 칸을 바로 눌러 이어 고칠 수 있다.
            field.onEndEdit.AddListener(_ => CommitStat(e, def, field, onChanged));
        }

        // 입력한 값을 숫자로 읽어 범위에 맞추고, 바뀌었으면 이 단어만의 수치로 저장한다. 숫자가 아니면 원래 값으로 되돌린다.
        void CommitStat(ItemWordEntry e, StatDef def, InputField field, System.Action onChanged)
        {
            var current = StatsOrDefault(e);
            if (!TryParseNumber(field.text, out float value))
            {
                field.text = FormatNumber(def.Get(current));
                SetStatus("숫자를 입력하세요.", Warn);
                return;
            }

            var edited = current.Clone();
            def.Set(edited, value);
            edited = edited.Clamped();
            field.text = FormatNumber(def.Get(edited));   // 범위에 맞춰 바뀐 값을 그대로 보여준다
            if (ApplyEdit(e, edited, $"{e.word}: {def.Label} {FormatNumber(def.Get(edited))}")) onChanged?.Invoke();
        }

        void ResetStats(ItemWordEntry e)
        {
            _file.wordStats.RemoveAll(w => w != null && w.word == e.word);
            Save($"{e.word}: 기본 수치로 되돌림");
            RefreshWords();
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

        InputField MakeInput(RectTransform rt, string placeholderText, Color? background = null, SafeInputField.InputKind kind = SafeInputField.InputKind.Text)
        {
            Img(rt, background ?? new Color(0.95f, 0.9f, 0.78f, 1f));
            var field = rt.gameObject.AddComponent<SafeInputField>();   // 한글 입력기와 선택 영역이 겹칠 때 나는 유니티 예외를 피한다
            field.Kind = kind;
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

        // 배경 없는 가로 한 줄(수치 패널 안에서 쓴다). 높이는 고정, 안의 칸은 가로로 나란히.
        static RectTransform NewPlainRow(RectTransform parent, float height)
        {
            var rt = Rect("Row", parent, 0, 0, 1, 1, 0, 0, 0, 0);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 16f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return rt;
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

        Button AddRowButton(RectTransform row, string label, int size, Color color, float preferred, float flexible, UnityAction onClick)
        {
            var rt = Rect("Button", row, 0, 0, 1, 1, 0, 0, 0, 0);
            var button = TextButton(rt, label, size, color, onClick);   // color는 글자색
            SetWidth(rt.gameObject, preferred, flexible);
            return button;
        }
    }
}
