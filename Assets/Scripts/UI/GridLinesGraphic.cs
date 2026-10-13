using UnityEngine;
using UnityEngine.UI;

namespace Hangeul.UI
{
    /// <summary>
    /// 인벤토리 격자의 칸 구분선과 체커 무늬를 메시로 그린다(스프라이트 불필요). 카드 뒤에 깔리며 클릭을 받지 않는다.
    /// 선은 칸 경계에 걸쳐 그려지고(바깥 테두리는 안쪽으로 밀려 잘리지 않는다), 교차점이 겹쳐 진해지지 않도록
    /// 세로선은 통으로, 가로선은 세로선 사이 구간만 그린다.
    /// 선 색은 이 Graphic의 Color, 체커 색은 <see cref="Set"/>의 alternateColor.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GridLinesGraphic : MaskableGraphic
    {
        int _cols = 1, _rows = 1;
        float _thickness = 2f;
        Color _alternate = Color.clear;

        public void Set(int cols, int rows, float lineWidth, Color lineColor, Color alternateColor)
        {
            _cols = Mathf.Max(1, cols);
            _rows = Mathf.Max(1, rows);
            _thickness = Mathf.Max(0f, lineWidth);
            _alternate = alternateColor;
            color = lineColor;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float cw = r.width / _cols, ch = r.height / _rows, half = _thickness / 2f;

            // 체커: 한 칸 걸러 한 칸
            if (_alternate.a > 0f)
                for (int row = 0; row < _rows; row++)
                    for (int col = 0; col < _cols; col++)
                        if (((col + row) & 1) == 1)
                            Quad(vh, r.xMin + col * cw, r.yMax - (row + 1) * ch, r.xMin + (col + 1) * cw, r.yMax - row * ch, _alternate);

            if (_thickness <= 0f || color.a <= 0f) return;

            // 세로선 (통으로). 바깥 테두리는 안쪽으로 밀어 넣는다
            for (int i = 0; i <= _cols; i++)
            {
                float x = Mathf.Clamp(r.xMin + i * cw, r.xMin + half, r.xMax - half);
                Quad(vh, x - half, r.yMin, x + half, r.yMax, color);
            }

            // 가로선: 세로선 두께만큼 비운 구간만
            for (int j = 0; j <= _rows; j++)
            {
                float y = Mathf.Clamp(r.yMax - j * ch, r.yMin + half, r.yMax - half);
                for (int i = 0; i < _cols; i++)
                {
                    float x0 = Mathf.Clamp(r.xMin + i * cw, r.xMin + half, r.xMax - half) + half;
                    float x1 = Mathf.Clamp(r.xMin + (i + 1) * cw, r.xMin + half, r.xMax - half) - half;
                    if (x1 > x0) Quad(vh, x0, y - half, x1, y + half, color);
                }
            }
        }

        static void Quad(VertexHelper vh, float x0, float y0, float x1, float y1, Color c)
        {
            int n = vh.currentVertCount;
            Color32 c32 = c;
            vh.AddVert(new Vector3(x0, y0), c32, Vector2.zero);
            vh.AddVert(new Vector3(x0, y1), c32, Vector2.zero);
            vh.AddVert(new Vector3(x1, y1), c32, Vector2.zero);
            vh.AddVert(new Vector3(x1, y0), c32, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2);
            vh.AddTriangle(n + 2, n + 3, n);
        }
    }
}
