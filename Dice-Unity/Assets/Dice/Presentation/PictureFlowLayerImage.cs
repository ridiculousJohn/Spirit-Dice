using System;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /**
     * Draws one picture flow layer over the picture's plate: an image over the layer's rectangle drawing the picture itself,
     * at the same texture coordinates as the background, through the Picture Flow Image shader, which pulls the picture along
     * the layer's flow in two cross-faded phases and blends it over the plate by the window. Values that do not change while
     * the layer shows are worked out when it is shown; each frame only the phase is set.
     */
    public sealed class PictureFlowLayerImage : MonoBehaviour
    {
        private static readonly int WindowId = Shader.PropertyToID("_WindowTex");
        private static readonly int FlowId = Shader.PropertyToID("_FlowTex");
        private static readonly int FlowRectId = Shader.PropertyToID("_FlowRect");
        private static readonly int FlowSpanId = Shader.PropertyToID("_FlowSpan");
        private static readonly int FlowPhaseId = Shader.PropertyToID("_FlowPhase");
        private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");

        [SerializeField] private RawImage image;
        [Tooltip("Pulls the picture along the flow through the window: Dice/Picture Flow Image.")]
        [SerializeField] private Shader shader;
        private Material material;
        private double phasePeriod;

        /** Shows the layer over its rectangle, redrawing the picture there, at the start of its flow. */
        public void Show(PictureFlowLayer layer, Texture2D picture)
        {
            if (shader == null) throw new InvalidOperationException(name + " has no shader to draw its flow layer with.");
            var rect = (RectTransform)transform;
            rect.anchorMin = layer.Rect.min;
            rect.anchorMax = layer.Rect.max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            // The image samples the picture over its rectangle exactly where the background does, so with no displacement it
            // draws the plate unchanged.
            image.texture = picture;
            image.uvRect = layer.Rect;
            if (material == null) material = new Material(shader) { name = "Picture Flow Layer", hideFlags = HideFlags.HideAndDontSave };
            material.SetTexture(WindowId, layer.Window);
            material.SetTexture(FlowId, layer.Flow);
            material.SetVector(FlowRectId, new Vector4(layer.Rect.x, layer.Rect.y, layer.Rect.width, layer.Rect.height));
            // The displacement at the end of a phase, in the picture's texture coordinates, so it holds under any crop.
            float reach = layer.Speed * layer.PhasePeriod;
            material.SetVector(FlowSpanId, new Vector4(reach / picture.width, reach / picture.height, 0, 0));
            material.SetFloat(NoiseStrengthId, layer.NoiseStrength);
            phasePeriod = layer.PhasePeriod;
            image.material = material;
            Flow(0);
            gameObject.SetActive(true);
        }

        /** Sets the phase as it is the given seconds after the layer was shown. */
        public void Flow(double seconds)
        {
            // The phase is kept within one period, where single precision holds it exactly enough.
            double phases = seconds / phasePeriod;
            material.SetFloat(FlowPhaseId, (float)(phases - Math.Floor(phases)));
        }

        /** Hides the layer and frees its material until it is shown again. */
        public void Hide()
        {
            gameObject.SetActive(false);
            image.material = null;
            image.texture = null;
            ReleaseMaterial();
        }

        private void OnDestroy() => ReleaseMaterial();

        private void ReleaseMaterial()
        {
            if (material != null) OwnedObject.Release(material);
            material = null;
        }
    }
}
