using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Beveled bone faces keep history pieces visually related to the encounter dice. */
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpiritModifierTokenGraphic : MaskableGraphic
    {
        private Color accent = new Color32(57, 108, 66, 255);

        public Color Accent
        {
            get => accent;
            set { accent = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Quad(vertices, new Vector2(.10f, .06f), new Vector2(.94f, .06f),
                new Vector2(.98f, .19f), new Vector2(.14f, .23f), new Color32(0, 0, 0, 100));
            Quad(vertices, new Vector2(.08f, .20f), new Vector2(.80f, .12f),
                new Vector2(.80f, .37f), new Vector2(.08f, .43f), new Color32(172, 151, 113, 255));
            Quad(vertices, new Vector2(.80f, .12f), new Vector2(.94f, .59f),
                new Vector2(.94f, .84f), new Vector2(.80f, .37f), new Color32(119, 103, 77, 255));
            Quad(vertices, new Vector2(.08f, .43f), new Vector2(.80f, .37f),
                new Vector2(.94f, .84f), new Vector2(.21f, .91f), new Color32(244, 229, 197, 255));
            Quad(vertices, new Vector2(.14f, .46f), new Vector2(.76f, .41f),
                new Vector2(.87f, .80f), new Vector2(.25f, .86f), new Color32(231, 216, 180, 255));
            Quad(vertices, new Vector2(.12f, .41f), new Vector2(.77f, .35f),
                new Vector2(.77f, .38f), new Vector2(.12f, .44f), new Color32(255, 243, 214, 255));
            Quad(vertices, new Vector2(.15f, .24f), new Vector2(.70f, .19f),
                new Vector2(.70f, .23f), new Vector2(.15f, .28f), accent);
        }

        private void Quad(VertexHelper vertices, Vector2 first, Vector2 second, Vector2 third, Vector2 fourth, Color tint)
        {
            int index = vertices.currentVertCount;
            Rect area = rectTransform.rect;
            vertices.AddVert(Position(area, first), tint * color, Vector2.zero);
            vertices.AddVert(Position(area, second), tint * color, Vector2.zero);
            vertices.AddVert(Position(area, third), tint * color, Vector2.zero);
            vertices.AddVert(Position(area, fourth), tint * color, Vector2.zero);
            vertices.AddTriangle(index, index + 1, index + 2);
            vertices.AddTriangle(index, index + 2, index + 3);
        }

        private static Vector2 Position(Rect area, Vector2 point)
            => new Vector2(area.xMin + point.x * area.width, area.yMin + point.y * area.height);
    }

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
