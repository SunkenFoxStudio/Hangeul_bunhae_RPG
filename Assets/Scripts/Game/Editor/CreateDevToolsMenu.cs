using System.IO;
using System.Linq;
using Hangeul.Game.DevTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Hangeul.Game.Editor
{
    /// <summary>GameObject ▸ Hangeul ▸ DB Scene Button / Create Item DB Scene : 개발용 DB 관리 씬과 그리로 가는 버튼을 만든다.</summary>
    public static class CreateDevToolsMenu
    {
        const string DbScenePath = "Assets/Scenes/ItemDbScene.unity";

        static Font LoadFont() => AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NanumMyeongjo-ExtraBold.ttf");

        [MenuItem("GameObject/Hangeul/DB Scene Button", false, 15)]
        static void CreateButton()
        {
            if (Object.FindFirstObjectByType<DbSceneButton>() != null)
            {
                Debug.LogWarning("이미 DbSceneButton이 있습니다.");
                return;
            }

            var go = new GameObject("DbSceneButton", typeof(DbSceneButton));
            Undo.RegisterCreatedObjectUndo(go, "Create DB Scene Button");
            var so = new SerializedObject(go.GetComponent<DbSceneButton>());
            so.FindProperty("font").objectReferenceValue = LoadFont();
            so.ApplyModifiedPropertiesWithoutUndo();
            Selection.activeGameObject = go;

            if (!File.Exists(DbScenePath))
                Debug.LogWarning("DB 관리 씬이 아직 없습니다. GameObject ▸ Hangeul ▸ Create Item DB Scene 으로 만드세요.");
        }

        [MenuItem("GameObject/Hangeul/Create Item DB Scene", false, 16)]
        static void CreateScene()
        {
            // 하이어라키의 우클릭 메뉴로 실행하면 메뉴가 "지금 열린 씬"을 붙들고 있다. 그 상태에서 씬을 닫고 새로 만들면
            // 오브젝트가 닫힌 씬으로 들어가(저장된 씬이 비어 버림) 엔진이 죽는다. 메뉴가 끝난 뒤로 미룬다.
            EditorApplication.delayCall += CreateSceneNow;
        }

        static void CreateSceneNow()
        {
            if (File.Exists(DbScenePath) &&
                !EditorUtility.DisplayDialog("DB 관리 씬", "이미 있는 DB 관리 씬을 새로 만들어 덮어쓸까요?", "덮어쓰기", "취소"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string previous = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            var camGo = new GameObject("Main Camera", typeof(Camera));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.08f, 0.06f);
            cam.orthographic = true;

            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));
            WireUiActions(es.GetComponent<InputSystemUIInputModule>());

            var view = new GameObject("ItemDbManager", typeof(ItemDbManagerView));
            var so = new SerializedObject(view.GetComponent<ItemDbManagerView>());
            so.FindProperty("font").objectReferenceValue = LoadFont();
            so.ApplyModifiedPropertiesWithoutUndo();

            // 만든 오브젝트가 새 씬에 들어 있게 명시하고, 저장 전에 확인한다.
            foreach (var go in new[] { camGo, es, view }) SceneManager.MoveGameObjectToScene(go, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            int roots = scene.rootCount;
            bool saved = EditorSceneManager.SaveScene(scene, DbScenePath);

            EnsureInBuildSettings();
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
            if (saved && roots == 3) Debug.Log("DB 관리 씬을 만들었습니다: " + DbScenePath);
            else Debug.LogError($"DB 관리 씬 저장에 문제가 있습니다 (저장={saved}, 루트 오브젝트={roots}개, 기대값 3).");
        }

        // 게임 씬의 EventSystem과 같은 UI 입력 에셋(패키지 기본 UI 액션)을 가리키게 한다.
        // AssignDefaultActions()는 씬 안에 임시 액션을 만들어 저장하므로 실행할 때 클릭이 먹히지 않는다.
        static void WireUiActions(InputSystemUIInputModule module)
        {
            const string guid = "ca9f5fa95ffab41fb9a615ab714db018";
            var map = new (string property, string action)[]
            {
                ("m_PointAction", "UI/Point"), ("m_MoveAction", "UI/Navigate"), ("m_SubmitAction", "UI/Submit"),
                ("m_CancelAction", "UI/Cancel"), ("m_LeftClickAction", "UI/Click"), ("m_MiddleClickAction", "UI/MiddleClick"),
                ("m_RightClickAction", "UI/RightClick"), ("m_ScrollWheelAction", "UI/ScrollWheel"),
                ("m_TrackedDevicePositionAction", "UI/TrackedDevicePosition"), ("m_TrackedDeviceOrientationAction", "UI/TrackedDeviceOrientation"),
            };

            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(path);
            var refs = string.IsNullOrEmpty(path)
                ? new System.Collections.Generic.Dictionary<string, UnityEngine.InputSystem.InputActionReference>()
                : AssetDatabase.LoadAllAssetsAtPath(path).OfType<UnityEngine.InputSystem.InputActionReference>().GroupBy(r => r.name).ToDictionary(g => g.Key, g => g.First());

            if (asset == null || map.Any(m => !refs.ContainsKey(m.action)))
            {
                Debug.LogWarning("기본 UI 입력 에셋을 찾지 못했습니다. EventSystem의 Input System UI Input Module에 Actions Asset을 직접 지정하세요.");
                return;
            }

            var so = new SerializedObject(module);
            so.FindProperty("m_ActionsAsset").objectReferenceValue = asset;
            foreach (var (property, action) in map) so.FindProperty(property).objectReferenceValue = refs[action];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == DbScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(DbScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
