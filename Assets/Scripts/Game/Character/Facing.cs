using UnityEngine;

namespace Hangeul.Game.Character
{
    /// <summary>스프라이트가 제공하는 4방향. 좌/우 방향은 Left / Right 시트를 쓴다.</summary>
    public enum Facing { Down, Up, Left, Right }

    public static class FacingUtil
    {
        // 위쪽(대각선 포함)은 모두 Up, 정확히 좌우(±5°)는 측면, 아래쪽은 60° 섹터로 나눈다.
        const float HorizontalSnapDeg = 5f;

        public static Facing FromVector(Vector2 v)
        {
            float a = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            if (a < 0f) a += 360f;

            float toHorizontal = Mathf.Min(a, Mathf.Abs(a - 180f), 360f - a);
            if (toHorizontal < HorizontalSnapDeg)
                return v.x >= 0f ? Facing.Right : Facing.Left;

            if (a < 180f) return Facing.Up;          // 위쪽 절반(좌상/우상 포함)
            if (a < 240f) return Facing.Left;    // 180°~240°
            if (a < 300f) return Facing.Down;        // 240°~300°
            return Facing.Right;                 // 300°~360°
        }

        public static Vector2 ToVector(Facing facing)
        {
            switch (facing)
            {
                case Facing.Up: return Vector2.up;
                case Facing.Left: return Vector2.left;
                case Facing.Right: return Vector2.right;
                default: return Vector2.down;
            }
        }
    }
}
