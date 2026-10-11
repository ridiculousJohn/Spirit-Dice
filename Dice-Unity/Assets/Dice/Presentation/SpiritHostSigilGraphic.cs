using System;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Authored endpoint pairs describe a host's sigil in normalized square coordinates. */
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpiritHostSigilGraphic : MaskableGraphic
    {
        private Vector2[] lines = Array.Empty<Vector2>();
        private float width = .035f;

        public void Configure(Vector2[] endpoints, float lineWidth)
        {
            if (endpoints == null || endpoints.Length % 2 != 0) throw new ArgumentException("Sigil endpoints must occur in pairs.", nameof(endpoints));
            if (!SpiritHostPresentationAsset.Finite(lineWidth) || lineWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(lineWidth));
            foreach (Vector2 endpoint in endpoints)
                if (!SpiritHostPresentationAsset.Finite(endpoint)) throw new ArgumentException("Sigil endpoints must be finite.", nameof(endpoints));
            lines = (Vector2[])endpoints.Clone();
            width = lineWidth;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect area = rectTransform.rect;
            float scale = Mathf.Min(area.width, area.height);
            for (int i = 0; i < lines.Length; i += 2)
            {
                Vector2 first = area.center + lines[i] * scale;
                Vector2 second = area.center + lines[i + 1] * scale;
                Vector2 direction = second - first;
                Vector2 offset = new Vector2(-direction.y, direction.x).normalized * Mathf.Max(1f, scale * width) * .5f;
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
}
