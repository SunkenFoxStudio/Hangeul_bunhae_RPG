using System;
using UnityEngine;

namespace Hangeul.Game.Character
{
    /// <summary>
    /// 방향별 프레임 배열을 가진 애니메이션 한 종류(Idle, Walk 등).
    /// 인스펙터에서 방향별 스프라이트 시트(예: Idle_Down.png)를 해당 배열 칸에 끌어다 놓으면 모든 프레임이 채워진다.
    /// </summary>
    [Serializable]
    public sealed class DirectionalClip
    {
        public float fps = 8f;
        public bool loop = true;
        public Sprite[] down;
        public Sprite[] up;
        public Sprite[] left;
        public Sprite[] right;

        public bool HasFrames => Has(down) || Has(up) || Has(left) || Has(right);

        /// <summary>해당 방향 프레임. 비어 있으면 Down → 나머지 중 하나 순으로 대체한다. 아무것도 없으면 null.</summary>
        public Sprite[] Get(Facing facing)
        {
            var frames = Raw(facing);
            if (Has(frames)) return frames;
            if (Has(down)) return down;
            foreach (Facing f in Enum.GetValues(typeof(Facing)))
            {
                frames = Raw(f);
                if (Has(frames)) return frames;
            }
            return null;
        }

        Sprite[] Raw(Facing facing)
        {
            switch (facing)
            {
                case Facing.Up: return up;
                case Facing.Left: return left;
                case Facing.Right: return right;
                default: return down;
            }
        }

        static bool Has(Sprite[] a) => a != null && a.Length > 0;
    }
}
