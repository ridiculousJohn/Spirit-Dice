using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** A fox mask identifies the host's favor when no authored sprite is assigned. */
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpiritFoxSigilGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Line(vertices, new Vector2(-.42f, .42f), new Vector2(-.33f, -.05f));
            Line(vertices, new Vector2(-.42f, .42f), new Vector2(-.12f, .21f));
            Line(vertices, new Vector2(-.12f, .21f), new Vector2(.12f, .21f));
            Line(vertices, new Vector2(.12f, .21f), new Vector2(.42f, .42f));
            Line(vertices, new Vector2(.42f, .42f), new Vector2(.33f, -.05f));
            Line(vertices, new Vector2(-.33f, -.05f), new Vector2(0, -.42f));
            Line(vertices, new Vector2(.33f, -.05f), new Vector2(0, -.42f));
            Line(vertices, new Vector2(-.28f, .04f), new Vector2(-.08f, -.04f));
            Line(vertices, new Vector2(.28f, .04f), new Vector2(.08f, -.04f));
            Line(vertices, new Vector2(-.05f, -.24f), new Vector2(.05f, -.24f));
        }

        private void Line(VertexHelper vertices, Vector2 first, Vector2 second)
        {
            Rect area = rectTransform.rect;
            float scale = Mathf.Min(area.width, area.height);
            first = area.center + first * scale;
            second = area.center + second * scale;
            Vector2 offset = new Vector2(-(second - first).y, (second - first).x).normalized * Mathf.Max(1, scale * .035f) * .5f;
            int index = vertices.currentVertCount;
            vertices.AddVert(first - offset, color, Vector2.zero);
            vertices.AddVert(first + offset, color, Vector2.zero);
            vertices.AddVert(second + offset, color, Vector2.zero);
            vertices.AddVert(second - offset, color, Vector2.zero);
            vertices.AddTriangle(index, index + 1, index + 2);
            vertices.AddTriangle(index, index + 2, index + 3);
        }
    }
}
