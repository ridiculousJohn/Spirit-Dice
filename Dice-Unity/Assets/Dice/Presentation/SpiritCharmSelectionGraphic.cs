using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** An inset gold frame marks the inspected charm without changing its layout. */
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpiritCharmSelectionGraphic : MaskableGraphic
    {
        private float pulseStarted = -1;
        public bool IsPulsing => pulseStarted >= 0;

        public void Pulse()
        {
            pulseStarted = Time.unscaledTime;
        }

        private void Update()
        {
            if (!IsPulsing) return;
            float progress = (Time.unscaledTime - pulseStarted) / .85f;
            float brightness = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
            canvasRenderer.SetColor(Color.Lerp(Color.white, new Color(1.3f, 1.3f, 1.3f, 1), brightness));
            if (progress >= 1) pulseStarted = -1;
        }

        protected override void OnDisable()
        {
            pulseStarted = -1;
            canvasRenderer.SetColor(Color.white);
            base.OnDisable();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var rect = GetPixelAdjustedRect();
            const float inset = 5;
            const float thickness = 3;
            const float corner = 9;
            for (int ring = 0; ring < 2; ++ring)
            {
                float edge = inset + ring * thickness;
                float cut = corner - ring * thickness * .5f;
                float left = rect.xMin + edge;
                float right = rect.xMax - edge;
                float bottom = rect.yMin + edge;
                float top = rect.yMax - edge;
                vertices.AddVert(new Vector3(left + cut, top), color, Vector2.zero);
                vertices.AddVert(new Vector3(right - cut, top), color, Vector2.zero);
                vertices.AddVert(new Vector3(right, top - cut), color, Vector2.zero);
                vertices.AddVert(new Vector3(right, bottom + cut), color, Vector2.zero);
                vertices.AddVert(new Vector3(right - cut, bottom), color, Vector2.zero);
                vertices.AddVert(new Vector3(left + cut, bottom), color, Vector2.zero);
                vertices.AddVert(new Vector3(left, bottom + cut), color, Vector2.zero);
                vertices.AddVert(new Vector3(left, top - cut), color, Vector2.zero);
            }
            for (int index = 0; index < 8; ++index)
            {
                int next = (index + 1) % 8;
                vertices.AddTriangle(index, next, index + 8);
                vertices.AddTriangle(next, next + 8, index + 8);
            }
        }
    }
}
