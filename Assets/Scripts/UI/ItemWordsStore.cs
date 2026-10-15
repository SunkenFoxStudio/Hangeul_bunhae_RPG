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
    /// <item><b>빌드</b>: 게임 실행 파일(exe)이 있는 폴더의 <c>item_words.json</c>이 기준이다. 빌드에 구워 둔 목록(Resources)은 그 파일이 없을 때의 기본값일 뿐이다.
    /// DB 관리 씬이 처음 열릴 때 기본값을 그 자리에 복사해 두고(<see cref="EnsureFile"/>), 이후 바꿀 때마다 그 파일을 갱신한다.
    /// 게임 씬(인벤토리)도 같은 파일을 읽어서 빌드 안에서 고친 단어가 바로 반영된다.
    /// 이 파일을 프로젝트의 <c>Assets/Resources/item_words.json</c>에 덮어쓰면 정식 목록이 된다.</item>
    /// </list>
    /// </summary>
    public static class ItemWordsStore
    {
        public const string ResourceName = "item_words";
        public const string FileName = "item_words.json";

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
        /// 지금 쓸 목록의 내용. 빌드에서는 실행 파일 옆 파일이 있으면 그것을(없거나 읽을 수 없으면 기본 목록을),
        /// 에디터에서는 assigned(인스펙터에 직접 넣은 파일)가 있으면 그것을, 없으면 Resources의 것을 쓴다. 아무것도 없으면 null.
        /// </summary>
        public static string ReadCurrent(TextAsset assigned = null)
        {
            if (!Application.isEditor)
            {
                string path = FilePath;
                try
                {
                    if (File.Exists(path)) return File.ReadAllText(path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ItemWordsStore] {path} 파일을 읽을 수 없어 빌드에 들어 있는 기본 목록을 씁니다: {e.Message}");
                }
            }
            return assigned != null ? assigned.text : ReadBuiltIn();
        }

        /// <summary>
        /// 빌드에서 목록 파일이 아직 없으면 기본 목록을 그 자리에 복사해 둔다(에디터에서는 아무것도 하지 않는다).
        /// 기본 목록도 없으면 만들지 않는다. 실패해도 게임을 멈추지 않고 이유를 error로 돌려준다.
        /// </summary>
        public static bool EnsureFile(out string error)
        {
            error = null;
            if (Application.isEditor) return true;
            string path = FilePath;
            if (File.Exists(path)) return true;
            string builtIn = ReadBuiltIn();
            if (builtIn == null) return true;   // 복사할 기본값이 없다 — 첫 저장 때 만들어진다
            return TryWrite(builtIn, out error);
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
