using UnityEngine;
using UnityEngine.UI;

/// <summary>경작지 전용 삽화 확정 전, 길의 방향을 표시하는 UI 선도.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RouteSketchGraphic : MaskableGraphic
{
    public int column;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = rectTransform.rect;
        Quad(mesh, r, new Color(0.19f, 0.20f, 0.13f));
        for (int i = 0; i < 11; i++)
        {
            float y = r.yMin + r.height * (0.07f + i * 0.085f);
            Line(mesh, new Vector2(r.xMin, y), new Vector2(r.xMax, y + r.height * 0.16f),
                2f, new Color(0.33f, 0.34f, 0.22f));
        }
        Vector2 last = new Vector2(r.center.x, r.yMin);
        for (int i = 1; i <= 8; i++)
        {
            float t = i / 8f;
            float x = r.center.x + (column - 1) * r.width * 0.27f * t
                + Mathf.Sin(t * Mathf.PI * 1.4f + column) * r.width * 0.10f;
            var next = new Vector2(x, r.yMin + t * r.height);
            Line(mesh, last, next, Mathf.Lerp(48, 18, t), new Color(0.43f, 0.39f, 0.26f));
            Line(mesh, last, next, 2, new Color(0.62f, 0.54f, 0.35f));
            last = next;
        }
    }

    private static void Quad(VertexHelper mesh, Rect r, Color color)
    {
        Add(mesh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin),
            new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), color);
    }
    private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 d = b - a; Vector2 n = new Vector2(-d.y, d.x).normalized * width * 0.5f;
        Add(mesh, a - n, b - n, b + n, a + n, color);
    }
    private static void Add(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int i = mesh.currentVertCount;
        mesh.AddVert(a, color, Vector2.zero); mesh.AddVert(b, color, Vector2.zero);
        mesh.AddVert(c, color, Vector2.zero); mesh.AddVert(d, color, Vector2.zero);
        mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
    }
}
