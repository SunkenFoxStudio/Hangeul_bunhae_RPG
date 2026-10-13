using System;
using System.IO;
using Hangeul.App;
using UnityEngine;

namespace Hangeul.UI
{
    /// <summary>
    /// 인벤토리를 JSON 파일로 저장/불러오는 개발용 기본 저장소 (Application.persistentDataPath).
    /// 본편 게임 세이브가 생기면 <see cref="InventoryModel.ToSave"/>의 결과를 그 세이브 안에 넣고,
    /// 이 클래스는 단독 테스트용으로만 남긴다.
    /// </summary>
    public static class InventorySaveStore
    {
        public const string DefaultFileName = "inventory.json";

        public static string PathOf(string fileName) => Path.Combine(Application.persistentDataPath, fileName);

        public static bool Exists(string fileName) => File.Exists(PathOf(fileName));

        public static void Save(InventoryModel model, string fileName)
        {
            string path = PathOf(fileName), tmp = path + ".tmp";
            string json = JsonUtility.ToJson(model.ToSave(), true);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(tmp, json);                    // 쓰다가 끊겨도 기존 파일이 남도록 임시 파일에 먼저 쓴다
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        /// <summary>파일에서 불러와 모델에 적용한다. 파일이 없거나 깨졌으면 아무것도 바꾸지 않고 실패를 돌려준다.</summary>
        public static LoadResult Load(InventoryModel model, string fileName)
        {
            string path = PathOf(fileName);
            if (!File.Exists(path)) return LoadResult.Fail("저장 파일이 없습니다.");

            InventorySaveData data;
            try { data = JsonUtility.FromJson<InventorySaveData>(File.ReadAllText(path)); }
            catch (Exception e) { return LoadResult.Fail("저장 파일을 읽을 수 없습니다: " + e.Message); }

            return model.Load(data);
        }

        public static void Delete(string fileName)
        {
            string path = PathOf(fileName);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
