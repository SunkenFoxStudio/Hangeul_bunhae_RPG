using System;

namespace Hangeul.App
{
    public enum HeartState { Empty, Half, Full }

    /// <summary>
    /// 정수 체력을 하트 표시로 바꾸는 규칙. 하트 한 칸 = 체력 2(반 칸 표시, 젤다 방식).
    /// 예: 최대 6이면 하트 3칸, 현재 3이면 꽉 참·반 칸·빈 칸. 최대 체력이 홀수면 마지막 하트는 체력 1칸짜리라 1이 있으면 꽉 찬 것으로 본다.
    /// </summary>
    public static class HeartDisplay
    {
        public const int HpPerHeart = 2;

        /// <summary>최대 체력으로 그릴 하트 칸 수.</summary>
        public static int Count(int max, int hpPerHeart = HpPerHeart)
        {
            hpPerHeart = Math.Max(1, hpPerHeart);
            return max <= 0 ? 0 : (max + hpPerHeart - 1) / hpPerHeart;
        }

        /// <summary>왼쪽 하트부터 차례로 꽉 참/반 칸/빈 칸.</summary>
        public static HeartState[] Compute(int current, int max, int hpPerHeart = HpPerHeart)
        {
            hpPerHeart = Math.Max(1, hpPerHeart);
            var hearts = new HeartState[Count(max, hpPerHeart)];
            current = Math.Max(0, Math.Min(current, max));

            for (int i = 0; i < hearts.Length; i++)
            {
                int capacity = Math.Min(hpPerHeart, max - i * hpPerHeart);   // 이 하트가 담는 체력(마지막 하트는 더 적을 수 있다)
                int value = current - i * hpPerHeart;                        // 이 하트에 들어 있는 체력
                hearts[i] = value >= capacity ? HeartState.Full : value > 0 ? HeartState.Half : HeartState.Empty;
            }
            return hearts;
        }
    }
}
