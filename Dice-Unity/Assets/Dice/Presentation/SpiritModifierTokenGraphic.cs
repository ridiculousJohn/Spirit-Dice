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

}
