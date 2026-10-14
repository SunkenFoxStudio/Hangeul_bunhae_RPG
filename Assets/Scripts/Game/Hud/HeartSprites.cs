using UnityEngine;

namespace Hangeul.Game.Hud
{
    /// <summary>하트 그림이 슬롯에 없을 때 쓰는 임시 하트(코드로 그린 작은 픽셀 하트). 그림을 넣으면 쓰이지 않는다.</summary>
    public static class HeartSprites
    {
        // 11×10, '#'이 하트 모양
        static readonly string[] Shape =
        {
            "..##...##..",
            ".####.####.",
            "###########",
            "###########",
            "###########",
            ".#########.",
            "..#######..",
            "...#####...",
            "....###....",
            ".....#.....",
        };

        static Sprite _full, _half, _empty;

        public static Sprite Full => _full != null ? _full : _full = Build(left: Red, right: Red);
        public static Sprite Half => _half != null ? _half : _half = Build(left: Red, right: Gray);
        public static Sprite Empty => _empty != null ? _empty : _empty = Build(left: Gray, right: Gray);

        static readonly Color32 Red = new Color32(230, 40, 55, 255);
        static readonly Color32 Gray = new Color32(70, 55, 60, 220);
        static readonly Color32 Outline = new Color32(40, 15, 20, 255);

        static Sprite Build(Color32 left, Color32 right)
        {
            int w = Shape[0].Length, h = Shape.Length;
            int tw = w + 2, th = h + 2;   // 바깥에 한 칸 테두리
            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[tw * th];

            bool Filled(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && Shape[h - 1 - y][x] == '#';   // y는 아래가 0

            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                {
                    int sx = x - 1, sy = y - 1;
                    if (Filled(sx, sy)) pixels[y * tw + x] = sx < w / 2f ? left : right;
                    else if (Filled(sx - 1, sy) || Filled(sx + 1, sy) || Filled(sx, sy - 1) || Filled(sx, sy + 1)) pixels[y * tw + x] = Outline;
                }
            tex.SetPixels32(pixels);
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), tw);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
