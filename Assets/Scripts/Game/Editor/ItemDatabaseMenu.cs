using System;
using System.IO;
using System.Linq;
using System.Text;
using Hangeul.App;
using Hangeul.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hangeul.Game.Editor
{
    /// <summary>
    /// Hangeul ▸ Setup ▸ Item DB : 게임 데이터 에셋(<see cref="ItemDatabase"/>)과 JSON(<c>Assets/Resources/item_words.json</c>, 게임 안 DB 화면이 고치는 파일)을 주고받는다.
    /// 가져오기는 두 가지다 — 새 단어만 추가(이미 있는 단어는 그대로, 인스펙터에서 고친 값이 유지된다) / JSON 값으로 덮어쓰기(확인창에서 바뀔 단어를 보여 준다).
    /// 어느 쪽도 아이템 스프라이트와 JSON에 없는 단어는 건드리지 않는다.
    /// </summary>
    public static class ItemDatabaseMenu
    {
        const string AssetFolder = "Assets/Data";
        const string AssetPath = AssetFolder + "/ItemDatabase.asset";
        const string JsonPath = "Assets/Resources/item_words.json";
        const string Menu = "Hangeul/Setup/Item DB/";

        [MenuItem(Menu + "JSON → 게임 데이터 (새 단어만 추가)", false, 1)]
        static void ImportAddOnly() => Import(ImportMode.AddOnly);

        [MenuItem(Menu + "JSON → 게임 데이터 (JSON 값으로 덮어쓰기)", false, 2)]
        static void ImportOverwrite() => Import(ImportMode.Overwrite);

        [MenuItem(Menu + "게임 데이터 → JSON 내보내기...", false, 20)]
        internal static void Export()
        {
            var db = Load();
            if (db == null) { NoAsset(); return; }

            string folder = Path.GetDirectoryName(Application.dataPath);
            string path = EditorUtility.SaveFilePanel("게임 데이터를 JSON으로 내보내기", folder, "item_words_export.json", "json");
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                AtomicFile.Write(path, JsonUtility.ToJson(db.data.ToFile(forGame: false), true));
                Debug.Log($"[ItemDB] JSON으로 내보냈습니다: {path} (단어 {db.data.WordCount}개)");
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("내보내기 실패", e.Message, "확인");
            }
        }

        [MenuItem(Menu + "검증 (문제 찾기)", false, 22)]
        static void Validate()
        {
            var db = Load();
            if (db == null) { NoAsset(); return; }

            var report = ItemDatabaseValidator.Validate(db.data);
            LogReport(db, report);

            int errors = report.Count(IssueLevel.Error), warnings = report.Count(IssueLevel.Warning), infos = report.Count(IssueLevel.Info);
            var sb = new StringBuilder();
            sb.AppendLine($"오류 {errors} · 경고 {warnings} · 안내 {infos}");
            if (errors + warnings == 0) sb.AppendLine("고쳐야 할 문제가 없습니다.");
            else
            {
                sb.AppendLine();
                foreach (var issue in report.Issues.Where(i => i.Level != IssueLevel.Info).Take(10)) sb.AppendLine("· " + issue.Message);
                int rest = errors + warnings - 10;
                if (rest > 0) sb.AppendLine($"… 외 {rest}개");
            }
            sb.AppendLine();
            sb.Append("전체 목록은 Console에 있고, 에셋 인스펙터의 '검증' 버튼으로도 볼 수 있습니다.");
            EditorUtility.DisplayDialog("게임 데이터 검증", sb.ToString(), "확인");
        }

        // 검증 결과를 Console에 적는다(오류는 빨간색, 경고는 노란색). 항목을 누르면 에셋이 선택된다.
        internal static void LogReport(UnityEngine.Object context, ValidationReport report)
        {
            foreach (var issue in report.Issues)
            {
                string line = "[ItemDB 검증] " + issue.Message;
                switch (issue.Level)
                {
                    case IssueLevel.Error: Debug.LogError(line, context); break;
                    case IssueLevel.Warning: Debug.LogWarning(line, context); break;
                    default: Debug.Log(line, context); break;
                }
            }
            if (report.Issues.Count == 0) Debug.Log("[ItemDB 검증] 문제가 없습니다.", context);
        }

        [MenuItem(Menu + "카테고리 종류에 맞게 단어 정리", false, 21)]
        static void Reclassify()
        {
            var db = Load();
            if (db == null) { NoAsset(); return; }

            Undo.RecordObject(db, "Item DB: 카테고리 종류에 맞게 단어 정리");
            int moved = db.data.Reclassify();
            Save(db);
            string message = moved == 0 ? "옮길 단어가 없습니다. 모든 단어가 자기 카테고리의 종류에 맞는 목록에 있습니다."
                                        : $"{moved}개 단어를 카테고리 종류에 맞는 목록으로 옮겼습니다. 옮겨진 단어의 수치는 기본값을 따르는 상태로 시작합니다.";
            Debug.Log("[ItemDB] " + message);
            EditorUtility.DisplayDialog("카테고리 종류에 맞게 단어 정리", message, "확인");
        }

        // ───── 게임 연결 ─────
        const string ConnectMenu = Menu + "게임이 데이터 에셋을 읽게 연결";

        /// <summary>열려 있는 씬의 인벤토리(InventoryView)가 게임 데이터 에셋을 읽는지.</summary>
        internal enum Connection { NoInventory, NotConnected, Connected }

        internal static Connection GetConnection()
        {
            var views = FindViews();
            if (views.Length == 0) return Connection.NoInventory;
            var db = Load();
            return db != null && views.All(v => v.DataAsset == db) ? Connection.Connected : Connection.NotConnected;
        }

        [MenuItem(ConnectMenu, false, 30)]
        internal static void ToggleConnection()
        {
            var views = FindViews();
            if (views.Length == 0)
            {
                EditorUtility.DisplayDialog("연결", "열려 있는 씬에 InventoryView가 없습니다.\nSampleScene을 연 뒤 다시 실행하세요.", "확인");
                return;
            }
            var db = Load();
            if (db == null) { NoAsset(); return; }

            bool connected = GetConnection() == Connection.Connected;
            foreach (var view in views)
            {
                var so = new SerializedObject(view);
                so.FindProperty("itemDatabase").objectReferenceValue = connected ? null : db;
                so.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            }
            string message = connected
                ? "연결을 껐습니다. 게임이 JSON(Assets/Resources/item_words.json)에서 단어를 읽습니다."
                : "연결했습니다. 게임이 JSON 대신 게임 데이터 에셋에서 단어와 수치를 읽습니다.";
            Debug.Log($"[ItemDB] {message} 씬을 저장하세요.");
        }

        // 메뉴 항목 앞에 체크 표시: 지금 연결돼 있는지
        [MenuItem(ConnectMenu, true)]
        static bool ToggleConnectionValidate()
        {
            UnityEditor.Menu.SetChecked(ConnectMenu, GetConnection() == Connection.Connected);
            return true;
        }

        static InventoryView[] FindViews()
            => UnityEngine.Object.FindObjectsByType<InventoryView>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        [MenuItem(Menu + "게임 데이터 에셋 선택", false, 40)]
        static void Select()
        {
            var db = Load();
            if (db == null) { NoAsset(); return; }
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
        }

        // ───── 가져오기 ─────
        internal static void Import(ImportMode mode)
        {
            if (!File.Exists(JsonPath))
            {
                EditorUtility.DisplayDialog("가져오기", $"JSON 파일이 없습니다:\n{JsonPath}", "확인");
                return;
            }

            ItemWordsFile file;
            try { file = JsonUtility.FromJson<ItemWordsFile>(File.ReadAllText(JsonPath)); }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("가져오기 실패", "JSON을 읽을 수 없습니다:\n" + e.Message, "확인");
                return;
            }
            if (file == null || file.schemaVersion != 1)
            {
                EditorUtility.DisplayDialog("가져오기 실패", "모르는 JSON 형식입니다(schemaVersion 1이 필요합니다).", "확인");
                return;
            }

            var existing = Load();

            // 먼저 복사본에 해 보고 무엇이 바뀔지 확인한다(에셋은 아직 그대로, 없으면 만들지도 않는다)
            var preview = ItemDatabaseImporter.Import(file, (existing != null ? existing.data : new ItemDatabaseData()).DeepClone(), mode);
            if (mode == ImportMode.Overwrite && preview.Updated > 0)
            {
                string names = string.Join(", ", preview.UpdatedWords.Take(12)) + (preview.Updated > 12 ? $" 외 {preview.Updated - 12}개" : "");
                if (!EditorUtility.DisplayDialog("JSON 값으로 덮어쓰기",
                        $"{preview.Updated}개 단어의 카테고리·접두어 규칙·수치가 JSON 값으로 바뀝니다 (인스펙터에서 고친 값은 사라집니다).\n\n{names}\n\n새로 추가될 단어: {preview.Added}개\n계속할까요?",
                        "덮어쓰기", "취소"))
                    return;
            }

            var db = existing ?? Create();
            Undo.RecordObject(db, mode == ImportMode.Overwrite ? "Item DB: JSON 값으로 덮어쓰기" : "Item DB: 새 단어만 추가");
            var report = ItemDatabaseImporter.Import(file, db.data, mode);
            Save(db);
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);

            string summary = Summarize(mode, report, db.data.WordCount);
            var validation = ItemDatabaseValidator.Validate(db.data);
            int problems = validation.Count(IssueLevel.Error) + validation.Count(IssueLevel.Warning);
            if (problems > 0)
                summary += $"{Environment.NewLine}{Environment.NewLine}검증: 오류 {validation.Count(IssueLevel.Error)} · 경고 {validation.Count(IssueLevel.Warning)} — 메뉴 '검증 (문제 찾기)'로 자세히 볼 수 있습니다.";
            Debug.Log("[ItemDB] " + summary.Replace("\n", " / "));
            EditorUtility.DisplayDialog("가져오기 완료", summary, "확인");
        }

        static string Summarize(ImportMode mode, ImportReport r, int total)
        {
            var sb = new StringBuilder();
            sb.AppendLine(mode == ImportMode.AddOnly ? "새 단어만 추가했습니다." : "JSON 값으로 덮어썼습니다.");
            sb.AppendLine($"추가 {r.Added}개 · 바뀜 {r.Updated}개 · 그대로 {r.Unchanged}개 (게임 데이터의 단어 총 {total}개)");
            if (r.NewCategories.Count > 0) sb.AppendLine($"새 카테고리 {r.NewCategories.Count}개: {string.Join(", ", r.NewCategories)}");
            if (r.UnknownCategories.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"종류를 정해야 하는 카테고리: {string.Join(", ", r.UnknownCategories)}");
                sb.AppendLine("지금은 '일반'(수치 없음)으로 들어갔습니다. 에셋의 카테고리 목록에서 종류를 고른 뒤");
                sb.AppendLine("'카테고리 종류에 맞게 단어 정리' 메뉴를 실행하면 단어가 알맞은 목록으로 옮겨집니다.");
            }
            return sb.ToString().TrimEnd();
        }

        // ───── 에셋 ─────
        static ItemDatabase Load() => AssetDatabase.LoadAssetAtPath<ItemDatabase>(AssetPath);

        static ItemDatabase Create()
        {
            if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder("Assets", "Data");
            var db = ScriptableObject.CreateInstance<ItemDatabase>();
            AssetDatabase.CreateAsset(db, AssetPath);
            return db;
        }

        static void Save(ItemDatabase db)
        {
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }

        static void NoAsset()
            => EditorUtility.DisplayDialog("게임 데이터 에셋이 없습니다", $"{AssetPath}\n\n먼저 'JSON → 게임 데이터' 메뉴로 만드세요.", "확인");
    }
}
