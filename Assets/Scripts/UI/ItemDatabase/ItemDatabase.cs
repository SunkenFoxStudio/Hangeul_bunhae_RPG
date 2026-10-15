using Hangeul.App;
using UnityEngine;

namespace Hangeul.UI
{
    /// <summary>
    /// 게임 데이터 에셋: 단어, 카테고리, 접두어 규칙, 수치, 아이템 스프라이트를 게임이 직접 들고 있는 곳(에셋 하나).
    /// 게임이 이 에셋을 쓰게 되면(후속 단계) 메인 게임은 JSON을 읽지 않는다. 지금은 JSON에서 옮겨 담아 인스펙터로 확인할 수 있게만 만들어 둔 단계다.
    /// JSON과는 메뉴 <c>Hangeul ▸ Setup ▸ Item DB</c>로 주고받는다(가져오기: 새 단어만 추가 / JSON 값으로 덮어쓰기, 내보내기).
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Hangeul/Item Database")]
    public sealed class ItemDatabase : ScriptableObject
    {
        public ItemDatabaseData data = new ItemDatabaseData();

        /// <summary>게임 규칙이 쓰는 단어 파일(기본값을 따르는 단어도 기본값이 채워져 있다). <see cref="ItemWordSet.FromFile"/>에 그대로 넘긴다.</summary>
        public ItemWordsFile ToGameFile() => data.ToFile(forGame: true);
    }
}
