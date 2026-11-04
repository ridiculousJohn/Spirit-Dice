using System;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /**
     * Draws one picture drift layer over the picture's plate: an image over the layer's rectangle drawing the drifting texture
     * through the layer's window with the Source Masked Image shader, whose source tiling and offset move the texture while
     * the window stays fixed on the plate. Values that do not change while the layer shows are worked out when it is shown;
     * each frame only the texture's offset and the swelling opacity are set.
     */
    public sealed class PictureDriftLayerImage : MonoBehaviour
    {
        private static readonly int SourceId = Shader.PropertyToID("_SourceTex");
        private static readonly int MaskId = Shader.PropertyToID("_LayerMask");
        private static readonly int InvertMaskId = Shader.PropertyToID("_InvertMask");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private RawImage image;
        [Tooltip("Draws the texture through the window: Dice/Source Masked Image.")]
        [SerializeField] private Shader shader;
        private Material material;
        // The texture's offset at the start, in tiles, and its drift, in tiles per second; the offset wraps at whole tiles.
        private Vector2 startOffset;
        private Vector2 tileVelocity;
        private float opacity;
        private float swell;
        private float swellFrequency;

        // Internal so EditMode tests read what the layer draws.
        internal RawImage Image => image;

        /** Shows the layer over its rectangle at the start of its drift. */
        public void Show(PictureDriftLayer layer)
        {
            if (shader == null) throw new InvalidOperationException(name + " has no shader to draw its drift layer with.");
            var rect = (RectTransform)transform;
            rect.anchorMin = layer.Rect.min;
            rect.anchorMax = layer.Rect.max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (material == null) material = new Material(shader) { name = "Picture Drift Layer", hideFlags = HideFlags.HideAndDontSave };
            material.SetTexture(SourceId, layer.Texture);
            material.SetTexture(MaskId, layer.Window != null ? layer.Window : Texture2D.whiteTexture);
            material.SetFloat(InvertMaskId, 0f);
            // The tile at a point of the plate is (point - velocity * time) / tile size, so it stays registered to the picture.
            material.SetTextureScale(SourceId, layer.Rect.size / layer.TileSize);
            startOffset = layer.Rect.min / layer.TileSize;
            tileVelocity = layer.Velocity / layer.TileSize;
            opacity = layer.Opacity;
            swell = layer.Swell;
            swellFrequency = swell > 0 ? 2 * Mathf.PI / layer.SwellPeriod : 0f;
            image.material = material;
            Drift(0);
            gameObject.SetActive(true);
        }

        /** Places the texture and opacity as they are the given seconds after the layer was shown. */
        public void Drift(double seconds)
        {
            material.SetTextureOffset(SourceId, new Vector2(Wrap(startOffset.x - tileVelocity.x * seconds),
                Wrap(startOffset.y - tileVelocity.y * seconds)));
            float alpha = swell > 0 ? opacity * (1 + swell * (float)Math.Sin(swellFrequency * seconds)) : opacity;
            material.SetColor(ColorId, new Color(1, 1, 1, Mathf.Clamp01(alpha)));
        }

        /** Hides the layer and frees its material until it is shown again. */
        public void Hide()
        {
            gameObject.SetActive(false);
            image.material = null;
            ReleaseMaterial();
        }

        private void OnDestroy() => ReleaseMaterial();

        private void ReleaseMaterial()
        {
            if (material != null) OwnedObject.Release(material);
            material = null;
        }

        // Whole tiles look the same, so the offset is kept within one tile, where single precision holds it exactly enough.
        private static float Wrap(double tiles) => (float)(tiles - Math.Floor(tiles));
    }
}
