using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Hangeul.App;
using Hangeul.UI;
using UnityEditor;
using UnityEngine;

namespace Hangeul.Game.Editor
{
    /// <summary>
    /// 게임 데이터 에셋(<see cref="ItemDatabase"/>)의 전용 인스펙터. 종류별 목록으로 흩어진 단어를 한 표처럼 보여 준다.
    /// 위에서부터: 요약과 JSON 주고받기 버튼 → 카테고리(이름, 종류) → 종류별 기본값 → 단어 목록(검색·카테고리 필터, 단어마다 접고 펼침).
    /// 단어를 펼치면 그 단어의 종류에 있는 칸만 보인다(무기는 피해·범위·시간 — 원거리 무기는 공격 방식과 시위·쉬는 시간, 음식은 회복량, 탄은 접두어 규칙만, 접두어는 효과 종류별 값과 글자 색 선택기).
    /// "기본값 사용"이 켜진 단어는 에셋의 기본값을 읽기 전용으로 보여 주고, 끄면 그 단어만의 값을 고친다.
    /// 목록 구조가 바뀌는 일(단어 추가·삭제, 카테고리 변경, 종류 바꾸기)은 <see cref="Edit"/>를 거쳐 Undo가 된다.
    /// </summary>
    [CustomEditor(typeof(ItemDatabase))]
    public sealed class ItemDatabaseEditor : UnityEditor.Editor
    {
        static readonly string[] CategoryTypeLabels = { "일반 (수치 없음)", "무기", "방어구", "음식", "접두어", "탄 (화살 등)" };
        static readonly string[] AttackStyleLabels = { "근접", "원거리 (활)" };
        static readonly string[] DamageModeLabels = { "더하기 (+)", "곱하기 (×)" };
        static readonly string[] PrefixKindLabels = { "대미지 더하기", "속성 묻히기" };
        static readonly string[] SortLabels = { "최근 추가순", "카테고리순" };

        // 화면 상태(인스펙터를 닫으면 사라진다)
        bool _raw;
        bool _showCategories = true, _showDefaults, _showWords = true;
        string _search = "", _categoryFilter = "";
        bool _onlyCustom, _onlyNoSprite;
        int _sortMode;   // 0 = 최근 추가한 단어가 맨 위, 1 = 카테고리 순서대로
        string _newWord = "", _newCategoryName = "";
        int _newWordCategory;
        CategoryType _newCategoryType = CategoryType.Plain;
        string _message;
        ValidationReport _report;   // 마지막 검증 결과(고치고 나면 다시 눌러야 새로 고쳐진다)
        readonly HashSet<string> _open = new HashSet<string>();   // 펼쳐진 단어

        ItemDatabase Db => (ItemDatabase)target;
        ItemDatabaseData Data => Db.data;

        public override void OnInspectorGUI()
        {
            _raw = EditorGUILayout.ToggleLeft("원본 보기 (기본 인스펙터)", _raw);
            if (_raw) { DrawDefaultInspector(); return; }

            serializedObject.Update();

            DrawSummary();
            DrawCategories();
            DrawDefaults();
            DrawWords();

            serializedObject.ApplyModifiedProperties();
        }

        // ───── 구조를 바꾸는 편집 ─────
        // 직렬화 속성으로 고친 것을 먼저 반영하고, Undo를 기록한 뒤 데이터를 직접 바꾼다. 목록이 바뀌었으니 이번 그리기는 여기서 멈춘다.
        void Edit(string undoName, Action change)
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(Db, undoName);
            _message = null;
            change();
            EditorUtility.SetDirty(Db);
            serializedObject.Update();
            GUIUtility.ExitGUI();
        }

        // 대화상자가 뜨는 작업(JSON 가져오기·내보내기)을 부른다. 끝나면 인스펙터를 새로 읽는다.
        void RunDialogAction(Action action)
        {
            serializedObject.ApplyModifiedProperties();
            action();
            serializedObject.Update();
            GUIUtility.ExitGUI();
        }

        // ───── 요약 ─────
        void DrawSummary()
        {
            var d = Data;
            int noSprite = d.AllWords().Count(r => r.sprite == null);
            EditorGUILayout.HelpBox(
                $"단어 {d.WordCount}개 — 무기 {d.weapons.Count} · 음식 {d.foods.Count} · 방어구 {d.armors.Count} · 탄 {d.ammos.Count} · 접두어 {d.prefixDamages.Count + d.prefixElements.Count} · 일반 {d.plains.Count}\n" +
                $"아이템 스프라이트가 없는 단어 {noSprite}개",
                MessageType.None);

            DrawConnection();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("JSON 가져오기 (새 단어만)")) RunDialogAction(() => ItemDatabaseMenu.Import(ImportMode.AddOnly));
                if (GUILayout.Button("JSON 가져오기 (덮어쓰기)")) RunDialogAction(() => ItemDatabaseMenu.Import(ImportMode.Overwrite));
                if (GUILayout.Button("JSON 내보내기...")) RunDialogAction(ItemDatabaseMenu.Export);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("검증 (문제 찾기)")) _report = ItemDatabaseValidator.Validate(Data);
                if (_report != null && GUILayout.Button("검증 결과 닫기", GUILayout.Width(110))) _report = null;
            }
            DrawReport();

            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Info);
            EditorGUILayout.Space(4);
        }

        // ───── 검증 결과 ─────
        const int MaxIssuesShown = 40;

        void DrawReport()
        {
            if (_report == null) return;

            int errors = _report.Count(IssueLevel.Error), warnings = _report.Count(IssueLevel.Warning), infos = _report.Count(IssueLevel.Info);
            if (_report.Issues.Count == 0)
            {
                EditorGUILayout.HelpBox("검증: 문제가 없습니다.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox($"검증 결과 — 오류 {errors} · 경고 {warnings} · 안내 {infos}  (고친 뒤에는 '검증'을 다시 누르세요)",
                errors > 0 ? MessageType.Error : warnings > 0 ? MessageType.Warning : MessageType.Info);

            int shown = 0;
            foreach (var issue in _report.Issues)
            {
                if (shown++ >= MaxIssuesShown)
                {
                    EditorGUILayout.LabelField($"… 외 {_report.Issues.Count - MaxIssuesShown}개 (메뉴 Hangeul ▸ Setup ▸ Item DB ▸ 검증 으로 Console에서 전체를 볼 수 있습니다)", EditorStyles.miniLabel);
                    break;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(issue.Message, issue.Level == IssueLevel.Error ? MessageType.Error : issue.Level == IssueLevel.Warning ? MessageType.Warning : MessageType.None);
                    if (issue.Word != null && GUILayout.Button(new GUIContent("보기", "단어 목록에서 이 단어를 찾아 펼친다"), GUILayout.Width(44), GUILayout.Height(38)))
                        ShowWord(issue.Word);
                }
            }
        }

        // 단어 목록에서 그 단어만 보이게 하고 펼친다(화면 상태만 바꾼다)
        void ShowWord(string word)
        {
            _showWords = true;
            _search = word;
            _categoryFilter = "";
            _onlyCustom = false;
            _onlyNoSprite = false;
            _open.Add(word);
        }

        // 게임 연결 상태(열려 있는 씬의 InventoryView가 이 에셋을 읽는지). 1초에 한 번만 확인한다.
        double _connectionCheckedAt = -10;
        ItemDatabaseMenu.Connection _connection;

        void DrawConnection()
        {
            if (EditorApplication.timeSinceStartup - _connectionCheckedAt > 1.0)
            {
                _connection = ItemDatabaseMenu.GetConnection();
                _connectionCheckedAt = EditorApplication.timeSinceStartup;
            }

            string text;
            MessageType type;
            switch (_connection)
            {
                case ItemDatabaseMenu.Connection.Connected:
                    text = "게임 연결: 켜짐 — 열린 씬의 게임이 JSON 대신 이 에셋에서 단어와 수치를 읽습니다.";
                    type = MessageType.Info;
                    break;
                case ItemDatabaseMenu.Connection.NotConnected:
                    text = "게임 연결: 꺼짐 — 게임은 아직 JSON에서 단어를 읽습니다. 연결하면 이 에셋의 값이 게임에 반영됩니다.";
                    type = MessageType.Warning;
                    break;
                default:
                    text = "게임 연결: 열려 있는 씬에 InventoryView가 없습니다. SampleScene을 열면 연결할 수 있습니다.";
                    type = MessageType.None;
                    break;
            }
            EditorGUILayout.HelpBox(text, type);

            if (_connection != ItemDatabaseMenu.Connection.NoInventory
                && GUILayout.Button(_connection == ItemDatabaseMenu.Connection.Connected ? "게임 연결 끄기 (JSON을 읽게)" : "게임이 이 에셋을 읽게 연결"))
            {
                RunDialogAction(ItemDatabaseMenu.ToggleConnection);
                _connectionCheckedAt = -10;
            }
        }

        // ───── 카테고리 ─────
        void DrawCategories()
        {
            _showCategories = EditorGUILayout.Foldout(_showCategories, $"카테고리 ({Data.categories.Count})", true, EditorStyles.foldoutHeader);
            if (!_showCategories) return;

            EditorGUI.indentLevel++;
            for (int i = 0; i < Data.categories.Count; i++)
            {
                var c = Data.categories[i];
                if (c == null) continue;

                string newName;
                CategoryType newType;
                bool remove;
                using (new EditorGUILayout.HorizontalScope())
                {
                    newName = EditorGUILayout.DelayedTextField(c.name, GUILayout.MinWidth(120));
                    newType = (CategoryType)EditorGUILayout.Popup((int)c.type, CategoryTypeLabels, GUILayout.Width(130));
                    GUILayout.Label($"단어 {Data.WordCountIn(c.name)}개", GUILayout.Width(64));
                    remove = GUILayout.Button(new GUIContent("×", "단어가 없는 카테고리만 지울 수 있다"), GUILayout.Width(24));
                }

                string oldName = c.name;
                if (newName != oldName)
                    Edit("카테고리 이름 바꾸기", () => { if (!Data.TryRenameCategory(oldName, newName, out string error)) _message = error; });
                if (newType != c.type)
                    Edit("카테고리 종류 바꾸기", () =>
                    {
                        c.type = newType;
                        int moved = Data.Reclassify();
                        _message = moved > 0 ? $"'{oldName}'의 종류를 바꿔서 단어 {moved}개를 알맞은 목록으로 옮겼습니다. 옮겨진 단어의 수치는 기본값을 따릅니다." : null;
                    });
                if (remove)
                    Edit("카테고리 지우기", () => { if (!Data.TryRemoveCategory(oldName)) _message = $"'{oldName}'에는 단어가 있어서 지울 수 없습니다."; });
            }

            // 종류와 안 맞는 목록에 있는 단어(예: 가져오기로 종류를 짐작하지 못해 일반으로 들어간 뒤 종류를 고친 경우)
            int pending = Data.AllWords().Count(r => ItemDatabaseData.KindFor(Data.TypeOfCategory(r.category), r.Kind) != r.Kind);
            if (pending > 0)
            {
                EditorGUILayout.HelpBox($"카테고리 종류와 맞지 않는 목록에 있는 단어가 {pending}개 있습니다.", MessageType.Warning);
                if (GUILayout.Button("카테고리 종류에 맞게 단어 정리"))
                    Edit("카테고리 종류에 맞게 단어 정리", () => _message = $"단어 {Data.Reclassify()}개를 옮겼습니다.");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _newCategoryName = EditorGUILayout.TextField(_newCategoryName, GUILayout.MinWidth(120));
                _newCategoryType = (CategoryType)EditorGUILayout.Popup((int)_newCategoryType, CategoryTypeLabels, GUILayout.Width(130));
                if (GUILayout.Button("카테고리 추가", GUILayout.Width(100)))
                    Edit("카테고리 추가", () =>
                    {
                        if (Data.TryAddCategory(_newCategoryName, _newCategoryType, out string error)) _newCategoryName = "";
                        else _message = error;
                    });
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // ───── 기본값 ─────
        void DrawDefaults()
        {
            _showDefaults = EditorGUILayout.Foldout(_showDefaults, "기본값 ('기본값 사용'인 단어가 따르는 값)", true, EditorStyles.foldoutHeader);
            if (!_showDefaults) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("무기", EditorStyles.boldLabel);
            WeaponFields("data.defaultWeapon");

            EditorGUILayout.LabelField("음식", EditorStyles.boldLabel);
            Field("data.defaultFood.heal", "회복량 (체력 2 = 하트 한 칸)");

            EditorGUILayout.LabelField("접두어 — 대미지 더하기", EditorStyles.boldLabel);
            DamageEffectFields("data.defaultPrefixDamage", "mode", "amount");

            EditorGUILayout.LabelField("접두어 — 속성 묻히기", EditorStyles.boldLabel);
            ElementEffectFields("data.defaultPrefixElement", "chancePercent", "color", "속성!");
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        // ───── 단어 목록 ─────
        struct Row
        {
            public string ListPath;   // 에셋 안에서 그 목록의 이름(weapons 등)
            public int Index;         // 그 목록 안의 번호
            public WordRecord Record;
        }

        void DrawWords()
        {
            var rows = CollectRows();
            _showWords = EditorGUILayout.Foldout(_showWords, $"단어 ({rows.Count})", true, EditorStyles.foldoutHeader);
            if (!_showWords) return;

            var categoryNames = Data.categories.Where(c => c != null).Select(c => c.name).ToArray();

            // 검색과 필터
            using (new EditorGUILayout.HorizontalScope())
            {
                _search = EditorGUILayout.TextField(new GUIContent("검색", "단어나 카테고리 이름의 일부"), _search);
                var filterOptions = new[] { "전체 카테고리" }.Concat(categoryNames).ToArray();
                int filterIndex = Mathf.Max(0, Array.IndexOf(categoryNames, _categoryFilter) + 1);
                int picked = EditorGUILayout.Popup(filterIndex, filterOptions, GUILayout.Width(130));
                _categoryFilter = picked == 0 ? "" : categoryNames[picked - 1];
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _sortMode = EditorGUILayout.Popup(_sortMode, SortLabels, GUILayout.Width(100));
                _onlyCustom = GUILayout.Toggle(_onlyCustom, "이 단어만의 수치만", EditorStyles.miniButtonLeft);
                _onlyNoSprite = GUILayout.Toggle(_onlyNoSprite, "스프라이트 없는 단어만", EditorStyles.miniButtonMid);
                if (GUILayout.Button("모두 접기", EditorStyles.miniButtonRight)) _open.Clear();
            }

            // 새 단어
            using (new EditorGUILayout.HorizontalScope())
            {
                _newWord = EditorGUILayout.TextField(new GUIContent("새 단어"), _newWord);
                _newWordCategory = Mathf.Clamp(_newWordCategory, 0, Mathf.Max(0, categoryNames.Length - 1));
                _newWordCategory = categoryNames.Length > 0 ? EditorGUILayout.Popup(_newWordCategory, categoryNames, GUILayout.Width(130)) : 0;
                if (GUILayout.Button("단어 추가", GUILayout.Width(80)))
                {
                    string category = categoryNames.Length > 0 ? categoryNames[_newWordCategory] : null;
                    Edit("단어 추가", () =>
                    {
                        if (Data.TryAddWord(_newWord, category, out var added, out string error))
                        {
                            _open.Add(added.word);
                            _newWord = "";
                            _search = ""; _categoryFilter = ""; _onlyCustom = false; _onlyNoSprite = false;   // 새 단어가 가려지지 않게 필터를 푼다
                            _sortMode = 0;                                                                   // 최근 추가순이면 맨 위에 보인다
                            _message = $"'{added.word}'를 {category}에 추가했습니다. 목록 맨 위에 있습니다.";
                        }
                        else _message = error;
                    });
                }
            }

            // 목록
            var shown = rows.Where(Matches).ToList();
            EditorGUILayout.LabelField($"표시 {shown.Count} / 전체 {rows.Count}", EditorStyles.miniLabel);
            foreach (var row in shown) DrawRow(row, categoryNames);
        }

        List<Row> CollectRows()
        {
            var rows = new List<Row>();
            Collect(rows, "weapons", Data.weapons);
            Collect(rows, "foods", Data.foods);
            Collect(rows, "armors", Data.armors);
            Collect(rows, "ammos", Data.ammos);
            Collect(rows, "plains", Data.plains);
            Collect(rows, "prefixDamages", Data.prefixDamages);
            Collect(rows, "prefixElements", Data.prefixElements);

            // 정렬(OrderBy 계열은 안정 정렬이라 같은 값끼리는 목록에 들어 있는 순서 그대로)
            //  - 최근 추가순: 새로 추가한 단어가 맨 위. 순서 값이 같은 옛 단어들은 카테고리 순서대로.
            //  - 카테고리순: 카테고리 순서대로, 같은 카테고리 안에서는 최근에 추가한 단어가 먼저.
            var names = Data.categories.Select(c => c?.name).ToList();
            int CategoryIndex(WordRecord r) { int i = names.IndexOf(r.category); return i < 0 ? int.MaxValue : i; }
            return _sortMode == 0
                ? rows.OrderByDescending(r => r.Record.order).ThenBy(r => CategoryIndex(r.Record)).ToList()
                : rows.OrderBy(r => CategoryIndex(r.Record)).ThenByDescending(r => r.Record.order).ToList();
        }

        static void Collect(List<Row> rows, string path, IList list)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] is WordRecord r) rows.Add(new Row { ListPath = path, Index = i, Record = r });
        }

        bool Matches(Row row)
        {
            var r = row.Record;
            if (_categoryFilter.Length > 0 && r.category != _categoryFilter) return false;
            if (_search.Length > 0
                && (r.word == null || r.word.IndexOf(_search, StringComparison.Ordinal) < 0)
                && (r.category == null || r.category.IndexOf(_search, StringComparison.Ordinal) < 0)) return false;
            if (_onlyCustom && !IsCustom(r)) return false;
            if (_onlyNoSprite && r.sprite != null) return false;
            return true;
        }

        static bool IsCustom(WordRecord r)
        {
            switch (r)
            {
                case WeaponWordRecord w: return !w.useDefaults;
                case FoodWordRecord f: return !f.useDefaults;
                case PrefixDamageWordRecord d: return !d.useDefaults;
                case PrefixElementWordRecord e: return !e.useDefaults;
                default: return false;
            }
        }

        static string KindLabel(WordKind kind)
        {
            switch (kind)
            {
                case WordKind.Weapon: return "무기";
                case WordKind.Food: return "음식";
                case WordKind.Armor: return "방어구";
                case WordKind.Ammo: return "탄";
                case WordKind.PrefixDamage: return "접두어·대미지";
                case WordKind.PrefixElement: return "접두어·속성";
                default: return "일반";
            }
        }

        // 단어 한 줄: 접힌 줄(펼침 표시, 단어, 카테고리, 종류, 스프라이트, 삭제) + 펼치면 그 단어의 칸들
        void DrawRow(Row row, string[] categoryNames)
        {
            var rec = row.Record;
            var prop = serializedObject.FindProperty($"data.{row.ListPath}").GetArrayElementAtIndex(row.Index);
            bool open = _open.Contains(rec.word ?? "");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // 카테고리 목록에 없는 카테고리를 가리키는 단어는 맨 앞에 그 이름을 괄호로 보여 준다(엉뚱한 카테고리로 보이지 않게)
                int categoryIndex = Array.IndexOf(categoryNames, rec.category);
                bool unknownCategory = categoryIndex < 0;
                var rowCategoryNames = unknownCategory ? new[] { $"({rec.category})" }.Concat(categoryNames).ToArray() : categoryNames;
                bool nowOpen, deleteClicked;
                int pickedCategory;
                using (new EditorGUILayout.HorizontalScope())
                {
                    nowOpen = GUILayout.Toggle(open, (IsCustom(rec) ? "● " : "") + rec.word, EditorStyles.foldout, GUILayout.Width(150));
                    pickedCategory = EditorGUILayout.Popup(unknownCategory ? 0 : categoryIndex, rowCategoryNames, GUILayout.MinWidth(90));
                    GUILayout.Label(KindLabel(rec.Kind), EditorStyles.miniLabel, GUILayout.Width(78));
                    EditorGUILayout.PropertyField(prop.FindPropertyRelative("sprite"), GUIContent.none, GUILayout.Width(64));
                    deleteClicked = GUILayout.Button(new GUIContent("×", "이 단어를 지운다"), GUILayout.Width(24));
                }

                if (nowOpen != open)
                {
                    if (nowOpen) _open.Add(rec.word ?? ""); else _open.Remove(rec.word ?? "");
                }

                if (pickedCategory != (unknownCategory ? 0 : categoryIndex) && pickedCategory < rowCategoryNames.Length)
                {
                    string newCategory = rowCategoryNames[pickedCategory];
                    Edit("단어의 카테고리 바꾸기", () =>
                    {
                        rec.category = newCategory;
                        var target = ItemDatabaseData.KindFor(Data.TypeOfCategory(newCategory), rec.Kind);
                        if (target != rec.Kind) Data.ConvertTo(rec, target);
                    });
                }

                if (deleteClicked && EditorUtility.DisplayDialog("단어 지우기", $"'{rec.word}'를 지울까요?", "지우기", "취소"))
                    Edit("단어 지우기", () => { Data.Remove(rec); _open.Remove(rec.word ?? ""); });

                if (nowOpen) DrawRecordBody(rec, prop);
            }
        }

        // ───── 펼친 단어의 칸들 (종류마다 다르다) ─────
        void DrawRecordBody(WordRecord rec, SerializedProperty prop)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("sprite"), new GUIContent("아이템 스프라이트"));

            if (rec is NormalWordRecord normal) DrawPrefixRules(prop, normal);

            switch (rec)
            {
                case WeaponWordRecord w:
                    DrawUseDefaults(w.word, w.useDefaults,
                        () => { w.stats = Data.defaultWeapon.Clone(); w.useDefaults = false; }, () => w.useDefaults = true);
                    if (w.useDefaults) { using (new EditorGUI.DisabledScope(true)) WeaponReadOnly(Data.defaultWeapon); }
                    else WeaponFields(prop.propertyPath + ".stats");
                    break;

                case FoodWordRecord f:
                    DrawUseDefaults(f.word, f.useDefaults,
                        () => { f.stats = Data.defaultFood.Clone(); f.useDefaults = false; }, () => f.useDefaults = true);
                    if (f.useDefaults) { using (new EditorGUI.DisabledScope(true)) EditorGUILayout.IntField("회복량 (체력 2 = 하트 한 칸)", Data.defaultFood.heal); }
                    else Field(prop.propertyPath + ".stats.heal", "회복량 (체력 2 = 하트 한 칸)");
                    break;

                case PrefixDamageWordRecord d:
                    DrawPrefixKind(rec);
                    DrawUseDefaults(d.word, d.useDefaults,
                        () => { d.effect = Data.defaultPrefixDamage.Clone(); d.useDefaults = false; }, () => d.useDefaults = true);
                    if (d.useDefaults) { using (new EditorGUI.DisabledScope(true)) DamageEffectReadOnly(Data.defaultPrefixDamage); }
                    else DamageEffectFields(prop.propertyPath + ".effect", "mode", "amount");
                    break;

                case PrefixElementWordRecord e:
                    DrawPrefixKind(rec);
                    DrawUseDefaults(e.word, e.useDefaults,
                        () => { e.effect = Data.defaultPrefixElement.Clone(); e.useDefaults = false; }, () => e.useDefaults = true);
                    if (e.useDefaults) { using (new EditorGUI.DisabledScope(true)) ElementEffectReadOnly(Data.defaultPrefixElement, e.word); }
                    else ElementEffectFields(prop.propertyPath + ".effect", "chancePercent", "color", e.word + "!");
                    break;
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
        }

        // 접두어 규칙: 접두어를 받을 수 있는지, 어떤 접두어가 붙는지(붙으면 만들어지는 새 단어를 같이 보여 준다)
        void DrawPrefixRules(SerializedProperty prop, NormalWordRecord rec)
        {
            var accepts = prop.FindPropertyRelative("acceptsPrefix");
            accepts.boolValue = EditorGUILayout.ToggleLeft("접두어 가능", accepts.boolValue);
            if (!accepts.boolValue) return;

            var list = prop.FindPropertyRelative("prefixes");
            EditorGUI.indentLevel++;
            for (int i = 0; i < list.arraySize; i++)
            {
                string prefix = list.GetArrayElementAtIndex(i).stringValue;
                bool exists = Data.Find(prefix) is PrefixDamageWordRecord || Data.Find(prefix) is PrefixElementWordRecord;
                bool remove;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(exists ? $"{prefix}  →  {prefix}{rec.word}" : $"{prefix}  (접두어 단어에 없는 단어 — 효과 없음)");
                    remove = GUILayout.Button("×", GUILayout.Width(24));
                }
                if (remove) { list.DeleteArrayElementAtIndex(i); break; }
            }

            var candidates = Data.prefixDamages.Select(p => p.word).Concat(Data.prefixElements.Select(p => p.word))
                .Where(w => !string.IsNullOrEmpty(w) && !rec.prefixes.Contains(w)).OrderBy(w => w).ToArray();
            var options = new[] { "접두어 추가..." }.Concat(candidates).ToArray();
            int pick = EditorGUILayout.Popup(0, options);
            if (pick > 0)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = options[pick];
            }
            EditorGUI.indentLevel--;
        }

        // 접두어 단어의 효과 종류 바꾸기(대미지 더하기 ↔ 속성 묻히기): 다른 목록으로 옮겨진다
        void DrawPrefixKind(WordRecord rec)
        {
            int index = rec.Kind == WordKind.PrefixElement ? 1 : 0;
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup("효과 종류", index, PrefixKindLabels);
            if (EditorGUI.EndChangeCheck() && picked != index)
                Edit("접두어 효과 종류 바꾸기", () => Data.ConvertTo(rec, picked == 1 ? WordKind.PrefixElement : WordKind.PrefixDamage));
        }

        // "기본값 사용" 체크. 끄면 그 순간의 기본값을 복사해 와서 거기서부터 고친다.
        void DrawUseDefaults(string word, bool current, Action turnOff, Action turnOn)
        {
            EditorGUI.BeginChangeCheck();
            bool value = EditorGUILayout.ToggleLeft("기본값 사용 (에셋의 기본값을 따른다)", current);
            if (EditorGUI.EndChangeCheck())
                Edit($"{word}: 기본값 사용 {(value ? "켜기" : "끄기")}", () => { if (value) turnOn(); else turnOff(); });
        }

        // ───── 칸 그리기 도우미 ─────
        void Field(string path, string label)
        {
            var p = serializedObject.FindProperty(path);
            if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label));
        }

        // 무기 칸. 공격 방식이 근접이면 길이·폭·판정 시간, 원거리(활)면 화살 속도·사거리와 시위·쉬는 시간이 보인다.
        void WeaponFields(string basePath)
        {
            var style = serializedObject.FindProperty(basePath + ".attackStyle");
            if (style != null)
                style.enumValueIndex = EditorGUILayout.Popup(new GUIContent("공격 방식", "원거리는 장착하면 가방에 있는 화살(탄 단어)을 쏜다"), style.enumValueIndex, AttackStyleLabels);
            bool ranged = style != null && style.enumValueIndex == (int)AttackStyle.Ranged;

            EditorGUILayout.LabelField("피해", EditorStyles.miniBoldLabel);
            Field(basePath + ".damage", "피해량");
            Field(basePath + ".knockback", "밀어내기 (속도)");
            Field(basePath + ".stun", "경직 (초)");
            if (ranged)
            {
                EditorGUILayout.LabelField("시간 (초)", EditorStyles.miniBoldLabel);
                Field(basePath + ".windup", "준비 (시위를 당기는 시간)");
                Field(basePath + ".recovery", "마무리 (쏜 뒤 쉬는 시간)");
                EditorGUILayout.LabelField("화살의 속도·사거리는 화살 프리팹(Projectile)에서 정한다", EditorStyles.miniLabel);
                return;
            }
            EditorGUILayout.LabelField("범위 (유니티 단위)", EditorStyles.miniBoldLabel);
            Field(basePath + ".reach", "길이");
            Field(basePath + ".width", "폭");
            EditorGUILayout.LabelField("시간 (초)", EditorStyles.miniBoldLabel);
            Field(basePath + ".windup", "준비");
            Field(basePath + ".active", "판정");
            Field(basePath + ".recovery", "마무리");
        }

        static void WeaponReadOnly(WeaponStats s)
        {
            EditorGUILayout.Popup("공격 방식", (int)s.attackStyle, AttackStyleLabels);
            EditorGUILayout.LabelField("피해", EditorStyles.miniBoldLabel);
            EditorGUILayout.IntField("피해량", s.damage);
            EditorGUILayout.FloatField("밀어내기 (속도)", s.knockback);
            EditorGUILayout.FloatField("경직 (초)", s.stun);
            if (s.attackStyle == AttackStyle.Ranged)
            {
                EditorGUILayout.LabelField("시간 (초)", EditorStyles.miniBoldLabel);
                EditorGUILayout.FloatField("준비 (시위를 당기는 시간)", s.windup);
                EditorGUILayout.FloatField("마무리 (쏜 뒤 쉬는 시간)", s.recovery);
                EditorGUILayout.LabelField("화살의 속도·사거리는 화살 프리팹(Projectile)에서 정한다", EditorStyles.miniLabel);
                return;
            }
            EditorGUILayout.LabelField("범위 (유니티 단위)", EditorStyles.miniBoldLabel);
            EditorGUILayout.FloatField("길이", s.reach);
            EditorGUILayout.FloatField("폭", s.width);
            EditorGUILayout.LabelField("시간 (초)", EditorStyles.miniBoldLabel);
            EditorGUILayout.FloatField("준비", s.windup);
            EditorGUILayout.FloatField("판정", s.active);
            EditorGUILayout.FloatField("마무리", s.recovery);
        }

        void DamageEffectFields(string basePath, string modeName, string amountName)
        {
            var mode = serializedObject.FindProperty($"{basePath}.{modeName}");
            var amount = serializedObject.FindProperty($"{basePath}.{amountName}");
            if (mode == null || amount == null) return;

            mode.enumValueIndex = EditorGUILayout.Popup("계산 방식", mode.enumValueIndex, DamageModeLabels);
            EditorGUILayout.PropertyField(amount, new GUIContent(mode.enumValueIndex == (int)DamageMode.Multiply ? "값 (곱하기: 1이면 변화 없음)" : "값 (더하기: 0이면 변화 없음)"));
            EditorGUILayout.LabelField($"예) 기본 피해량 3 → {WeaponModifier.ApplyDamage(3, (DamageMode)mode.enumValueIndex, amount.floatValue)}", EditorStyles.miniLabel);
        }

        static void DamageEffectReadOnly(PrefixDamageEffect e)
        {
            EditorGUILayout.Popup("계산 방식", (int)e.mode, DamageModeLabels);
            EditorGUILayout.FloatField("값", e.amount);
            EditorGUILayout.LabelField($"예) 기본 피해량 3 → {WeaponModifier.ApplyDamage(3, e.mode, e.amount)}", EditorStyles.miniLabel);
        }

        void ElementEffectFields(string basePath, string chanceName, string colorName, string previewText)
        {
            var chance = serializedObject.FindProperty($"{basePath}.{chanceName}");
            var color = serializedObject.FindProperty($"{basePath}.{colorName}");
            if (chance == null || color == null) return;

            chance.floatValue = EditorGUILayout.Slider("묻을 확률 (%)", chance.floatValue, 0f, 100f);
            ColorFields(color, previewText);
        }

        static void ElementEffectReadOnly(PrefixElementEffect e, string word)
        {
            EditorGUILayout.Slider("묻을 확률 (%)", e.chancePercent, 0f, 100f);
            DrawColorPreview(ColorHex.NormalizeOr(e.color, ColorHex.DefaultElement), word + "!", "글자 색");
        }

        // 속성 글자 색: 컬러 선택기 + #RRGGBB 입력 + 추천 색 버튼 + 적 머리 위에 뜨는 모양의 미리보기
        void ColorFields(SerializedProperty hexProp, string previewText)
        {
            string hex = ColorHex.NormalizeOr(hexProp.stringValue, ColorHex.DefaultElement);
            ColorUtility.TryParseHtmlString(hex, out var color);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("글자 색");

                EditorGUI.BeginChangeCheck();
                var picked = EditorGUILayout.ColorField(GUIContent.none, color, false, false, false, GUILayout.Width(52));
                if (EditorGUI.EndChangeCheck()) hexProp.stringValue = "#" + ColorUtility.ToHtmlStringRGB(picked);

                EditorGUI.BeginChangeCheck();
                string typed = EditorGUILayout.DelayedTextField(hex, GUILayout.Width(76));
                if (EditorGUI.EndChangeCheck() && ColorHex.TryNormalize(typed, out string normalized)) hexProp.stringValue = normalized;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("추천 색");
                var previous = GUI.backgroundColor;
                foreach (var preset in ColorHex.ElementPresets)
                {
                    ColorUtility.TryParseHtmlString(preset, out var presetColor);
                    GUI.backgroundColor = presetColor;
                    if (GUILayout.Button(GUIContent.none, GUILayout.Width(22), GUILayout.Height(18))) hexProp.stringValue = preset;
                }
                GUI.backgroundColor = previous;
            }

            DrawColorPreview(ColorHex.NormalizeOr(hexProp.stringValue, ColorHex.DefaultElement), previewText, "미리보기");
        }

        // 게임에서처럼 어두운 곳 위의 "화염!"
        static void DrawColorPreview(string hex, string text, string label)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                var rect = GUILayoutUtility.GetRect(110, 26, GUILayout.Width(110));
                EditorGUI.DrawRect(rect, new Color(0.17f, 0.12f, 0.08f));
                var style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
                style.normal.textColor = color;
                GUI.Label(rect, text, style);
            }
        }
    }
}
