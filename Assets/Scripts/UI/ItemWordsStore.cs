using System;
using System.IO;
using Hangeul.App;
using UnityEngine;

namespace Hangeul.UI
{
    /// <summary>
    /// 아이템 단어 목록(item_words.json)을 읽고 쓰는 곳. 어디에 두느냐가 실행 환경마다 다르다.
    /// <list type="bullet">
    /// <item><b>에디터</b>: 프로젝트 안의 <c>Assets/Resources/item_words.json</c>이 원본이다(DB 관리 씬이 바로 고친다).</item>
    /// <item><b>개발 빌드(Development Build)</b>: 게임 실행 파일(exe)이 있는 폴더의 <c>item_words.json</c>이 기준이다. 빌드에 구워 둔 목록(Resources)은 그 파일이 없을 때의 기본값일 뿐이다.
    /// DB 관리 씬이 처음 열릴 때 기본값을 그 자리에 복사해 두고(<see cref="EnsureFile"/>), 이후 바꿀 때마다 그 파일을 갱신한다.
    /// 게임 씬(인벤토리)도 같은 파일을 읽어서 빌드 안에서 고친 단어가 바로 반영된다.
    /// 이 파일을 프로젝트의 <c>Assets/Resources/item_words.json</c>에 덮어쓰면 정식 목록이 된다.</item>
    /// <item><b>출시 빌드</b>: exe 옆 파일을 읽거나 만들지 않는다(플레이어가 파일을 고쳐 수치를 바꾸는 것을 막는다). 빌드에 구워 둔 목록만 쓴다.</item>
    /// </list>
    /// </summary>
    public static class ItemWordsStore
    {
        public const string ResourceName = "item_words";
        public const string FileName = "item_words.json";

        /// <summary>
        /// 게임이 읽고 있는 게임 데이터 에셋(없으면 null). 인벤토리(<see cref="InventoryView"/>)가 단어를 읽을 때 알려 주고, 개발 빌드에서
        /// DB 관리 씬이 처음 만드는 exe 옆 파일의 시작 내용을 이 에셋에서 만드는 데 쓴다(씬이 바뀌어도 유지된다).
        /// </summary>
        public static ItemDatabase RegisteredDatabase;

        // 에디터에서 도메인 다시 불러오기를 끄고 실행해도 지난 실행의 에셋을 붙잡지 않도록
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => RegisteredDatabase = null;

        /// <summary>exe 옆의 목록 파일을 읽고 쓰는 곳인지: 개발 빌드만. 에디터는 프로젝트의 Resources 파일을 쓰고, 출시 빌드는 파일을 쓰지 않는다.</summary>
        public static bool UsesExternalFile => !Application.isEditor && Debug.isDebugBuild;

        /// <summary>목록 파일의 위치. 에디터는 Assets/Resources 안, 빌드는 실행 파일 옆.</summary>
        public static string FilePath
        {
            get
            {
                string projectOrBuildFolder = Path.GetDirectoryName(Application.dataPath);   // 에디터: 프로젝트 폴더, 빌드: exe가 있는 폴더
                return Application.isEditor
                    ? Path.Combine(projectOrBuildFolder, "Assets", "Resources", FileName)
                    : Path.Combine(projectOrBuildFolder, FileName);
            }
        }

        /// <summary>빌드에 구워 둔 기본 목록(Resources)의 내용. 없으면 null.</summary>
        public static string ReadBuiltIn()
        {
            var asset = Resources.Load<TextAsset>(ResourceName);
            return asset != null ? asset.text : null;
        }

        /// <summary>
        /// 지금 쓸 목록의 내용. 개발 빌드에서는 실행 파일 옆 파일이 있으면 그것을(없거나 읽을 수 없으면 기본 목록을),
        /// 에디터와 출시 빌드에서는 assigned(인스펙터에 직접 넣은 파일)가 있으면 그것을, 없으면 Resources의 것을 쓴다. 아무것도 없으면 null.
        /// </summary>
        public static string ReadCurrent(TextAsset assigned = null)
            => ReadExternal() ?? ReadBundled(assigned);

        /// <summary>exe 옆 파일을 보지 않고, 직접 넣은 파일(assigned)이나 빌드에 구워 둔 기본 목록의 내용. 없으면 null.</summary>
        public static string ReadBundled(TextAsset assigned = null) => assigned != null ? assigned.text : ReadBuiltIn();

        /// <summary>
        /// 개발 빌드에서 exe 옆 목록 파일이 있으면 그 내용(동료가 빌드 안에서 고친 단어). 에디터, 출시 빌드, 파일이 없거나 읽을 수 없을 때는 null.
        /// 게임 데이터 에셋이 연결돼 있어도 이 파일이 있으면 개발 빌드에서는 이것이 우선한다.
        /// </summary>
        public static string ReadExternal()
        {
            if (!UsesExternalFile) return null;
            string path = FilePath;
            try
            {
                if (File.Exists(path)) return File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ItemWordsStore] {path} 파일을 읽을 수 없어 다른 단어 목록을 씁니다: {e.Message}");
            }
            return null;
        }

        /// <summary>
        /// 개발 빌드에서 목록 파일이 아직 없으면 시작 내용을 그 자리에 만들어 둔다(에디터와 출시 빌드에서는 아무것도 하지 않는다).
        /// 시작 내용은 게임이 읽고 있는 게임 데이터 에셋(<see cref="RegisteredDatabase"/>)을 JSON으로 내보낸 것이고, 에셋이 없으면 빌드에 구워 둔 기본 목록이다.
        /// 시작 내용이 없으면 만들지 않는다. 실패해도 게임을 멈추지 않고 이유를 error로 돌려준다.
        /// </summary>
        public static bool EnsureFile(out string error)
        {
            error = null;
            if (!UsesExternalFile) return true;
            string path = FilePath;
            if (File.Exists(path)) return true;
            string seed = RegisteredDatabase != null
                ? JsonUtility.ToJson(RegisteredDatabase.data.ToFile(forGame: false), true)   // 에셋에서만 만든 단어와 수치도 DB 화면에 보이도록
                : ReadBuiltIn();
            if (seed == null) return true;   // 시작 내용이 없다 — 첫 저장 때 만들어진다
            return TryWrite(seed, out error);
        }

        /// <summary>목록 파일에 저장한다(쓰다가 끊겨도 기존 파일이 남도록 임시 파일을 거친다). 실패하면 이유를 error로 돌려준다.</summary>
        public static bool TryWrite(string json, out string error)
        {
            error = null;
            try
            {
                AtomicFile.Write(FilePath, json);
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();   // 에디터: 바뀐 에셋을 바로 다시 가져온다
#endif
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>목록 파일이 들어 있는 폴더를 파일 탐색기로 연다.</summary>
        public static void OpenFolder()
        {
            string folder = Path.GetDirectoryName(FilePath).Replace('\\', '/');
            Application.OpenURL("file://" + (folder.StartsWith("/") ? folder : "/" + folder));   // 윈도: file:///C:/...
        }
    }
}
