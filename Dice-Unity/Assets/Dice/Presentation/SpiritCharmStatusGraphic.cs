using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** State symbols use canvas geometry rather than relying on the body font's symbol coverage. */
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpiritCharmStatusGraphic : MaskableGraphic
    {
        private SpiritCharmStatusState state;
        public SpiritCharmStatusState State
        {
            get => state;
            set { if (state == value) return; state = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect rect = rectTransform.rect;
            Vector2 center = rect.center;
            float size = Mathf.Min(rect.width, rect.height);
            float width = Mathf.Max(1, size * .065f);
            const int segments = 32;
            for (int i = 0; i < segments; ++i)
            {
                float start = i * Mathf.PI * 2 / segments;
                float end = (i + 1) * Mathf.PI * 2 / segments;
                Line(vertices, center + new Vector2(Mathf.Cos(start), Mathf.Sin(start)) * size * .43f,
                    center + new Vector2(Mathf.Cos(end), Mathf.Sin(end)) * size * .43f, width);
            }
            switch (state)
            {
                case SpiritCharmStatusState.Ready:
                    Stroke(vertices, center, size, width, new Vector2(-.22f, 0), new Vector2(-.06f, -.17f));
                    Stroke(vertices, center, size, width, new Vector2(-.06f, -.17f), new Vector2(.24f, .19f));
                    break;
                case SpiritCharmStatusState.WaitingCondition:
                    Stroke(vertices, center, size, width, new Vector2(-.18f, .23f), new Vector2(.18f, .23f));
                    Stroke(vertices, center, size, width, new Vector2(-.18f, -.23f), new Vector2(.18f, -.23f));
                    Stroke(vertices, center, size, width, new Vector2(-.15f, .21f), new Vector2(.15f, -.21f));
                    Stroke(vertices, center, size, width, new Vector2(.15f, .21f), new Vector2(-.15f, -.21f));
                    break;
                case SpiritCharmStatusState.Used:
                    Stroke(vertices, center, size, width, new Vector2(-.19f, -.19f), new Vector2(.19f, .19f));
                    Stroke(vertices, center, size, width, new Vector2(-.19f, .19f), new Vector2(.19f, -.19f));
                    break;
                case SpiritCharmStatusState.OnRoll:
                    for (int i = 0; i < 20; ++i)
                    {
                        float start = Mathf.Lerp(55, 325, i / 20f) * Mathf.Deg2Rad;
                        float end = Mathf.Lerp(55, 325, (i + 1) / 20f) * Mathf.Deg2Rad;
                        Line(vertices, center + new Vector2(Mathf.Cos(start), Mathf.Sin(start)) * size * .23f,
                            center + new Vector2(Mathf.Cos(end), Mathf.Sin(end)) * size * .23f, width);
                    }
                    Stroke(vertices, center, size, width, new Vector2(.19f, -.13f), new Vector2(.04f, -.12f));
                    Stroke(vertices, center, size, width, new Vector2(.19f, -.13f), new Vector2(.16f, -.28f));
                    break;
                case SpiritCharmStatusState.Inactive:
                    Stroke(vertices, center, size, width, new Vector2(-.1f, -.19f), new Vector2(-.1f, .19f));
                    Stroke(vertices, center, size, width, new Vector2(.1f, -.19f), new Vector2(.1f, .19f));
                    break;
                default:
                    Stroke(vertices, center, size, width, new Vector2(0, .22f), new Vector2(.16f, 0));
                    Stroke(vertices, center, size, width, new Vector2(.16f, 0), new Vector2(0, -.22f));
                    Stroke(vertices, center, size, width, new Vector2(0, -.22f), new Vector2(-.16f, 0));
                    Stroke(vertices, center, size, width, new Vector2(-.16f, 0), new Vector2(0, .22f));
                    break;
            }
        }

        private void Stroke(VertexHelper vertices, Vector2 center, float size, float width, Vector2 start, Vector2 end)
            => Line(vertices, center + start * size, center + end * size, width);

        private void Line(VertexHelper vertices, Vector2 start, Vector2 end, float width)
        {
            Vector2 perpendicular = new Vector2(-(end - start).y, (end - start).x).normalized * width * .5f;
            int index = vertices.currentVertCount;
            vertices.AddVert(start - perpendicular, color, Vector2.zero);
            vertices.AddVert(start + perpendicular, color, Vector2.zero);
            vertices.AddVert(end + perpendicular, color, Vector2.zero);
            vertices.AddVert(end - perpendicular, color, Vector2.zero);
            vertices.AddTriangle(index, index + 1, index + 2);
            vertices.AddTriangle(index, index + 2, index + 3);
        }
    }
}
