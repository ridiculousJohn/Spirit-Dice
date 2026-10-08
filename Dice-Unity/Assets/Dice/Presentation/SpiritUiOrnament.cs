using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Small symbols use the canvas mesh so their edges remain crisp at every UI scale. */
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpiritUiOrnament : MaskableGraphic
    {
        internal string kind;
        internal bool mirrored;

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect area = rectTransform.rect;
            Vector2 center = area.center;
            float extent = Mathf.Min(area.width, area.height);
            if (kind == "divider")
            {
                float middle = area.center.y;
                Line(vertices, new Vector2(area.xMin, middle), new Vector2(center.x - 8, middle), 1);
                Line(vertices, new Vector2(center.x + 8, middle), new Vector2(area.xMax, middle), 1);
                Diamond(vertices, center, 4);
                Diamond(vertices, new Vector2(area.xMin + area.width * .2f, middle), 2.5f);
                Diamond(vertices, new Vector2(area.xMax - area.width * .2f, middle), 2.5f);
                return;
            }
            if (kind == "play")
            {
                Triangle(vertices, center + new Vector2(-.24f, -.4f) * extent,
                    center + new Vector2(-.24f, .4f) * extent, center + new Vector2(.4f, 0) * extent);
                return;
            }
            if (kind == "reroll")
            {
                float radius = extent * .34f;
                const int segments = 28;
                for (int index = 0; index < segments; ++index)
                {
                    float start = Mathf.Lerp(35, 330, (float)index / segments) * Mathf.Deg2Rad;
                    float end = Mathf.Lerp(35, 330, (float)(index + 1) / segments) * Mathf.Deg2Rad;
                    Line(vertices, center + new Vector2(Mathf.Cos(start), Mathf.Sin(start)) * radius,
                        center + new Vector2(Mathf.Cos(end), Mathf.Sin(end)) * radius, extent * .085f);
                }
                Vector2 tip = center + new Vector2(.82f, .56f) * radius;
                Triangle(vertices, tip + new Vector2(.07f, -.23f) * extent,
                    tip + new Vector2(.12f, .09f) * extent, tip + new Vector2(-.17f, .01f) * extent);
                return;
            }
            if (kind == "charm")
            {
                const int segments = 32;
                for (int index = 0; index < segments; ++index)
                {
                    float start = index * Mathf.PI * 2 / segments;
                    float end = (index + 1) * Mathf.PI * 2 / segments;
                    Line(vertices, center + new Vector2(Mathf.Cos(start), Mathf.Sin(start)) * extent * .46f,
                        center + new Vector2(Mathf.Cos(end), Mathf.Sin(end)) * extent * .46f, Mathf.Max(1, extent * .045f));
                }
                extent *= .7f;
            }
            float sign = mirrored ? -1 : 1;
            Vector2 bottom = center + new Vector2(-.25f * sign, -.42f) * extent;
            Vector2 top = center + new Vector2(.22f * sign, .42f) * extent;
            Line(vertices, bottom, top, Mathf.Max(1, extent * .022f));
            for (int index = 0; index < 5; ++index)
            {
                Vector2 stem = Vector2.Lerp(bottom, top, .12f + index * .16f);
                Vector2 branch = stem + new Vector2((index % 2 == 0 ? -.25f : .23f) * sign, .19f) * extent;
                Line(vertices, stem, branch, Mathf.Max(.8f, extent * .014f));
                Leaf(vertices, branch, branch + new Vector2((index % 2 == 0 ? -.07f : .08f) * sign, .14f) * extent,
                    extent * .06f);
            }
            Leaf(vertices, top, top + new Vector2(.06f * sign, .13f) * extent, extent * .052f);
        }

        private void Leaf(VertexHelper vertices, Vector2 start, Vector2 end, float width)
        {
            Vector2 perpendicular = new Vector2(-(end - start).y, (end - start).x).normalized * width;
            Vector2 middle = Vector2.Lerp(start, end, .48f);
            Triangle(vertices, start, middle + perpendicular, end);
            Triangle(vertices, start, end, middle - perpendicular * .7f);
        }

        private void Diamond(VertexHelper vertices, Vector2 center, float size)
        {
            Triangle(vertices, center + Vector2.left * size, center + Vector2.up * size, center + Vector2.right * size);
            Triangle(vertices, center + Vector2.left * size, center + Vector2.right * size, center + Vector2.down * size);
        }

        private void Line(VertexHelper vertices, Vector2 start, Vector2 end, float width)
        {
            Vector2 perpendicular = new Vector2(-(end - start).y, (end - start).x).normalized * width * .5f;
            Triangle(vertices, start - perpendicular, start + perpendicular, end + perpendicular);
            Triangle(vertices, start - perpendicular, end + perpendicular, end - perpendicular);
        }

        private void Triangle(VertexHelper vertices, Vector2 first, Vector2 second, Vector2 third)
        {
            int index = vertices.currentVertCount;
            vertices.AddVert(first, color, Vector2.zero);
            vertices.AddVert(second, color, Vector2.zero);
            vertices.AddVert(third, color, Vector2.zero);
            vertices.AddTriangle(index, index + 1, index + 2);
        }
    }
}
