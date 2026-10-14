using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Five bone dice illustrate the constrained faces of a hand; question marks are wildcards. */
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpiritHandPatternGraphic : MaskableGraphic
    {
        private static readonly int[][] faces =
        {
            new[] { 6, 0, 0, 0, 0 },
            new[] { 3, 3, 0, 0, 0 },
            new[] { 3, 3, 5, 5, 0 },
            new[] { 3, 3, 3, 0, 0 },
            new[] { 3, 3, 3, 5, 5 },
            new[] { 3, 3, 3, 3, 0 },
            new[] { 1, 2, 3, 4, 5 },
            new[] { 3, 3, 3, 3, 3 }
        };

        private HandCategory category;

        public HandCategory Category
        {
            get => category;
            set
            {
                if ((uint)value >= HandDefinition.CategoryCount)
                    throw new System.ArgumentOutOfRangeException(nameof(value));
                category = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect area = rectTransform.rect;
            float gap = 6;
            float size = Mathf.Min(area.height, (area.width - gap * 4) / 5);
            if (size <= 0) return;
            float left = area.xMin + (area.width - size * 5 - gap * 4) * .5f;
            Color border = new Color32(121, 108, 80, 255);
            Color bone = new Color32(234, 222, 191, 255);
            Color ink = new Color32(42, 47, 36, 255);
            for (int index = 0; index < 5; ++index)
            {
                var die = new Rect(left + index * (size + gap), area.center.y - size * .5f, size, size);
                RoundedRectangle(vertices, die, size * .14f, border * color);
                var face = new Rect(die.x + 1, die.y + 1, die.width - 2, die.height - 2);
                RoundedRectangle(vertices, face, size * .12f, bone * color);
                int value = faces[(int)category][index];
                if (value == 0) QuestionMark(vertices, die.center, size, ink * color);
                else Pips(vertices, die.center, size, value, ink * color);
            }
        }

        private static void Pips(VertexHelper vertices, Vector2 center, float size, int value, Color tint)
        {
            float offset = size * .24f;
            float radius = size * .063f;
            if ((value & 1) != 0) Disc(vertices, center, radius, tint);
            if (value >= 2)
            {
                Disc(vertices, center + new Vector2(-offset, offset), radius, tint);
                Disc(vertices, center + new Vector2(offset, -offset), radius, tint);
            }
            if (value >= 4)
            {
                Disc(vertices, center + new Vector2(offset, offset), radius, tint);
                Disc(vertices, center + new Vector2(-offset, -offset), radius, tint);
            }
            if (value == 6)
            {
                Disc(vertices, center + new Vector2(-offset, 0), radius, tint);
                Disc(vertices, center + new Vector2(offset, 0), radius, tint);
            }
        }

        private static void QuestionMark(VertexHelper vertices, Vector2 center, float size, Color tint)
        {
            float width = size * .068f;
            Vector2 previous = center + new Vector2(-.15f, .16f) * size;
            for (int index = 1; index <= 8; ++index)
            {
                float angle = Mathf.Lerp(180, -65, index / 8f) * Mathf.Deg2Rad;
                Vector2 next = center + new Vector2(Mathf.Cos(angle) * .15f, .16f + Mathf.Sin(angle) * .12f) * size;
                Line(vertices, previous, next, width, tint);
                previous = next;
            }
            Vector2 stem = center + new Vector2(0, -.075f) * size;
            Line(vertices, previous, stem, width, tint);
            Disc(vertices, center + new Vector2(0, -.225f) * size, width * .62f, tint);
        }

        private static void RoundedRectangle(VertexHelper vertices, Rect rect, float radius, Color tint)
        {
            int start = vertices.currentVertCount;
            vertices.AddVert(rect.center, tint, Vector2.zero);
            for (int corner = 0; corner < 4; ++corner)
            {
                Vector2 center = new Vector2(corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius,
                    corner < 2 ? rect.yMax - radius : rect.yMin + radius);
                for (int step = 0; step <= 4; ++step)
                {
                    float angle = (corner * 90 + step * 22.5f) * Mathf.Deg2Rad;
                    vertices.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
                }
            }
            for (int index = 0; index < 20; ++index)
                vertices.AddTriangle(start, start + index + 1, start + (index + 1) % 20 + 1);
        }

        private static void Disc(VertexHelper vertices, Vector2 center, float radius, Color tint)
        {
            int start = vertices.currentVertCount;
            vertices.AddVert(center, tint, Vector2.zero);
            for (int index = 0; index < 12; ++index)
            {
                float angle = index * Mathf.PI / 6;
                vertices.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
            }
            for (int index = 0; index < 12; ++index)
                vertices.AddTriangle(start, start + index + 1, start + (index + 1) % 12 + 1);
        }

        private static void Line(VertexHelper vertices, Vector2 first, Vector2 second, float width, Color tint)
        {
            Vector2 delta = second - first;
            Vector2 offset = new Vector2(-delta.y, delta.x).normalized * width * .5f;
            int start = vertices.currentVertCount;
            vertices.AddVert(first - offset, tint, Vector2.zero);
            vertices.AddVert(first + offset, tint, Vector2.zero);
            vertices.AddVert(second + offset, tint, Vector2.zero);
            vertices.AddVert(second - offset, tint, Vector2.zero);
            vertices.AddTriangle(start, start + 1, start + 2);
            vertices.AddTriangle(start, start + 2, start + 3);
        }
    }
}
