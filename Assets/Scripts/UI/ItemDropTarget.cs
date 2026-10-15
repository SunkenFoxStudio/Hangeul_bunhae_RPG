using Hangeul.App;
using UnityEngine;

namespace Hangeul.UI
{
    /// <summary>플레이어에게 놓은 완성 단어 한 덩어리의 정보.</summary>
    public readonly struct ItemDrop
    {
        public readonly string Word;
        public readonly ItemKind Kind;
        /// <summary>그 단어가 지금 장착 중인지.</summary>
        public readonly bool Equipped;

        public ItemDrop(string word, ItemKind kind, bool equipped) { Word = word; Kind = kind; Equipped = equipped; }
    }

    /// <summary>
    /// 인벤토리 카드를 끌어다 놓을 수 있는 월드 쪽 대상(플레이어). UI 어셈블리는 게임 어셈블리를 모르므로,
    /// 게임 쪽이 이 인터페이스를 구현해 <see cref="InventoryView.DropTarget"/>에 등록한다.
    /// </summary>
    public interface IItemDropTarget
    {
        /// <summary>화면 좌표(픽셀)가 이 대상 위인지.</summary>
        bool Contains(Vector2 screenPosition);

        /// <summary>놓으면 어떻게 되는지(아무것도 바꾸지 않는다). 끌고 있는 동안 안내 문구를 보여주는 데 쓴다.</summary>
        ItemUseAction Preview(ItemDrop item);

        /// <summary>놓았다: 게임 쪽 효과(회복 등)를 적용하고, 인벤토리가 이어서 해야 할 일(장착/해제/카드 없애기)을 돌려준다.</summary>
        ItemUseAction Use(ItemDrop item);
    }
}
