using System.Collections.Generic;

namespace Hangeul.App
{
    /// <summary>아이템 단어의 종류. DB 카테고리 이름으로 정해진다(<see cref="ItemKinds.FromCategory"/>).</summary>
    public enum ItemKind
    {
        None,     // 쓰임이 없는 단어(재료, 속성, 소모품 등 — 아직 정해지지 않은 것 포함)
        Weapon,   // 무기: 플레이어에게 드래그해 장착한다
        Armor,    // 방어구: 지금은 라벨만 표시한다(장착은 후속)
        Food,     // 음식: 플레이어에게 드래그하면 먹고 사라진다
    }

    public static class ItemKinds
    {
        public const string WeaponCategory = "무기";
        public const string ArmorCategory = "방어구";
        public const string FoodCategory = "음식";

        /// <summary>카테고리 이름 → 아이템 종류. 모르는 카테고리는 None.</summary>
        public static ItemKind FromCategory(string category)
        {
            switch (category)
            {
                case WeaponCategory: return ItemKind.Weapon;
                case ArmorCategory: return ItemKind.Armor;
                case FoodCategory: return ItemKind.Food;
                default: return ItemKind.None;
            }
        }

        /// <summary>가방 모드에서 단어 테두리 위에 쓰는 종류 이름. None은 빈 문자열(표시하지 않는다).</summary>
        public static string Label(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Weapon: return WeaponCategory;
                case ItemKind.Armor: return ArmorCategory;
                case ItemKind.Food: return FoodCategory;
                default: return "";
            }
        }
    }

    /// <summary>완성된 단어 카드를 플레이어에게 놓았을 때 일어나는 일.</summary>
    public enum ItemUseAction
    {
        None,      // 아무 일도 없다(쓸 수 없는 단어, 아직 장착할 수 없는 방어구 등)
        Equip,     // 무기를 장착한다
        Unequip,   // 장착 중인 무기를 푼다
        Eat,       // 먹는다: 효과가 적용되고 단어 카드가 사라진다
        Blocked,   // 쓸 수 있는 아이템이지만 지금은 쓸 수 없다(체력이 가득 찬 채로 음식 등)
    }

    /// <summary>아이템 사용 규칙(게임 엔진과 무관한 순수 규칙).</summary>
    public static class ItemUseRules
    {
        /// <param name="kind">놓은 단어의 종류</param>
        /// <param name="equipped">그 단어가 지금 장착 중인지</param>
        /// <param name="hp">플레이어의 현재 체력</param>
        /// <param name="maxHp">플레이어의 최대 체력</param>
        public static ItemUseAction Decide(ItemKind kind, bool equipped, int hp, int maxHp)
        {
            switch (kind)
            {
                case ItemKind.Weapon: return equipped ? ItemUseAction.Unequip : ItemUseAction.Equip;
                case ItemKind.Food: return hp < maxHp ? ItemUseAction.Eat : ItemUseAction.Blocked;   // 가득 찼으면 먹지 않고 제자리로
                default: return ItemUseAction.None;                                                 // 방어구는 아직 장착하지 않는다
            }
        }

        /// <summary>카드를 플레이어 위로 끌고 왔을 때 보여 줄 안내 문구. 아무것도 보이지 않아야 하면 빈 문자열.</summary>
        public static string Hint(ItemUseAction action)
        {
            switch (action)
            {
                case ItemUseAction.Equip: return "장착";
                case ItemUseAction.Unequip: return "장착 해제";
                case ItemUseAction.Eat: return "먹기";
                case ItemUseAction.Blocked: return "체력이 가득 참";
                default: return "";
            }
        }
    }

    /// <summary>
    /// 장착 중인 무기 단어. 단어 이름과 그것을 이루는 카드 아이디(읽는 순서)로 가린다 —
    /// 덩어리를 통째로 옮겨도(칸이 바뀌어도) 같은 카드들이 같은 단어를 이루고 있으면 같은 장착이다.
    /// </summary>
    public sealed class EquippedItem
    {
        public string Word { get; }
        public IReadOnlyList<int> CardIds { get; }

        public EquippedItem(string word, IReadOnlyList<int> cardIds)
        {
            Word = word;
            CardIds = cardIds;
        }

        public bool Matches(string word, IReadOnlyList<int> cardIds)
        {
            if (word != Word || cardIds == null || cardIds.Count != CardIds.Count) return false;
            for (int i = 0; i < CardIds.Count; i++)
                if (CardIds[i] != cardIds[i]) return false;
            return true;
        }
    }
}
