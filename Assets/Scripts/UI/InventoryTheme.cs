using UnityEngine;

namespace Hangeul.UI
{
    /// <summary>
    /// 인벤토리 화면 중 코드가 만들어 내는 요소(격자 칸, 카드 배경)의 스프라이트 모음.
    /// 비워 둔 항목은 코드로 그리는 기본 모양(색 칸/선)을 쓴다. 가방 프레임·쓰레기통 아이콘처럼 씬에 놓인 Image는
    /// 그 오브젝트의 Image에 스프라이트를 직접 끌어 넣으면 된다.
    /// 만들기: Project 창 우클릭 ▸ Create ▸ Hangeul ▸ Inventory Theme, 그 뒤 InventoryView의 Theme 칸에 연결.
    ///
    /// 픽셀 아트 팁: 스프라이트 임포트를 Filter Mode = Point, Compression = None 으로 두고,
    /// 카드·칸 스프라이트가 상하좌우로 늘어나도 안 깨지게 9-slice(Sprite Editor의 Border)를 잡아 두면 격자 크기가 바뀌어도 선명하다.
    /// </summary>
    [CreateAssetMenu(fileName = "InventoryTheme", menuName = "Hangeul/Inventory Theme")]
    public sealed class InventoryTheme : ScriptableObject
    {
        [Header("격자 칸 (하나라도 넣으면 선/체커를 코드로 그리지 않고 칸마다 이 스프라이트를 깐다)")]
        public Sprite cell;
        [Tooltip("한 칸 걸러 쓰는 스프라이트. 비우면 모든 칸이 Cell을 쓴다.")]
        public Sprite cellAlt;

        [Header("카드 배경 (비우면 색 칸)")]
        public Sprite cardSyllable;
        public Sprite cardConsonant;
        public Sprite cardVowel;
    }
}
