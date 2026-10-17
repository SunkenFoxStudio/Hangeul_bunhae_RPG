using Hangeul.App;
using UnityEngine;

namespace Hangeul.UI
{
    /// <summary>
    /// 가방 모드에서 쓸 수 있는 아이템 단어(무기/방어구/음식/탄)를 둘러싸는 테두리의 한 카드 몫.
    /// 덩어리를 이루는 카드마다 자기 바깥쪽 가장자리(이웃 카드가 같은 덩어리가 아닌 쪽)에만 선을 긋고,
    /// 덩어리의 첫 카드에는 종류 이름표를, 장착 중이면 마지막 카드의 오른쪽 아래에 체크 배지를 단다.
    /// </summary>
    public readonly struct ItemFrame
    {
        public static readonly ItemFrame None = default;

        public readonly bool Top, Right, Bottom, Left;
        public readonly Color Color;
        /// <summary>선 굵기(칸 대비). 0이면 테두리 없음.</summary>
        public readonly float Thickness;
        /// <summary>테두리 위에 걸치는 종류 이름표("무기" 등). 덩어리의 첫 카드에만 있다.</summary>
        public readonly string Label;
        /// <summary>오른쪽 아래 "장착 중" 체크 배지. 덩어리의 마지막 카드에만 있다.</summary>
        public readonly bool Badge;

        public ItemFrame(bool top, bool right, bool bottom, bool left, Color color, float thickness, string label, bool badge)
        {
            Top = top; Right = right; Bottom = bottom; Left = left;
            Color = color; Thickness = thickness; Label = label; Badge = badge;
        }

        public bool IsNone => Thickness <= 0f;

        public bool Same(ItemFrame o)
            => Top == o.Top && Right == o.Right && Bottom == o.Bottom && Left == o.Left
               && Color == o.Color && Mathf.Approximately(Thickness, o.Thickness) && Label == o.Label && Badge == o.Badge;

        // 가방 모드의 어두운 카드 위에서 잘 보이고, 조립 모드의 완성 글로우(초록)와 겹치지 않는 색
        public static Color KindColor(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Weapon: return new Color(0.92f, 0.36f, 0.30f);   // 붉은색
                case ItemKind.Armor: return new Color(0.36f, 0.58f, 0.95f);    // 푸른색
                case ItemKind.Food: return new Color(0.96f, 0.68f, 0.22f);     // 주황색
                case ItemKind.Ammo: return new Color(0.72f, 0.52f, 0.95f);     // 보라색
                default: return Color.clear;
            }
        }
    }
}
