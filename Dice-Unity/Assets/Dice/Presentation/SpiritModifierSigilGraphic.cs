using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Family sigils remain readable without requiring a mechanical icon field. */
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpiritModifierSigilGraphic : MaskableGraphic
    {
        private uint seed;

        public uint Seed
        {
            get => seed;
            set { seed = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect area = rectTransform.rect;
            Vector2 center = area.center;
            float scale = Mathf.Min(area.width, area.height) * .42f;
            int kind = (int)(seed % 12);
            if (kind < 4)
            {
                int sides = kind + 3;
                for (int i = 0; i < sides; ++i)
                    Line(vertices, center + Point(i, sides) * scale, center + Point(i + 1, sides) * scale, 1.5f);
                if ((seed & 16) != 0) Diamond(vertices, center, scale * .22f);
                else Line(vertices, center + Vector2.down * scale * .42f, center + Vector2.up * scale * .42f, 1.5f);
            }
            else if (kind < 8)
            {
                int points = kind + 1;
                for (int i = 0; i < points; ++i)
                {
                    Vector2 tip = center + Point(i, points) * scale;
                    Line(vertices, center, tip, 1.5f);
                    Diamond(vertices, tip, scale * .13f);
                }
                Diamond(vertices, center, scale * .18f);
            }
            else
            {
                Line(vertices, center + Vector2.down * scale, center + Vector2.up * scale, 1.6f);
                for (int i = 0; i < kind - 6; ++i)
                {
                    float height = Mathf.Lerp(-.65f, .65f, (float)i / (kind - 7));
                    Vector2 start = center + Vector2.up * height * scale;
                    Line(vertices, start, start + new Vector2((i & 1) == 0 ? -.65f : .65f, .32f) * scale, 1.5f);
                }
            }
            // The outer marks give families with the same central shape distinct silhouettes.
            for (int i = 0; i < 4; ++i)
                if ((seed & (1u << (i + 7))) != 0)
                    Diamond(vertices, center + Point(i, 4) * scale * 1.3f, scale * .10f);
        }

        private static Vector2 Point(int index, int count)
        {
            float angle = Mathf.PI * .5f + index * Mathf.PI * 2 / count;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private void Diamond(VertexHelper vertices, Vector2 center, float size)
        {
            Triangle(vertices, center + Vector2.left * size, center + Vector2.up * size, center + Vector2.right * size);
            Triangle(vertices, center + Vector2.left * size, center + Vector2.right * size, center + Vector2.down * size);
        }

        private void Line(VertexHelper vertices, Vector2 start, Vector2 end, float width)
        {
            Vector2 offset = new Vector2(-(end - start).y, (end - start).x).normalized * width * .5f;
            Triangle(vertices, start - offset, start + offset, end + offset);
            Triangle(vertices, start - offset, end + offset, end - offset);
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
