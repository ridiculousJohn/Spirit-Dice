using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * A painted scene shown on the journey screen's picture layer: a wide plate, lit when it has scene lighting, layers
     * drifting over it, and an optional foreground layer. The host is hidden while it shows; the table scene the journey
     * reaches next stays loaded behind it.
     */
    [CreateAssetMenu(menuName = "Dice/Presentation/Picture Scene")]
    public sealed class PictureSceneAsset : JourneySceneAsset
    {
        [Tooltip("The wide plate; scene lighting shows its own base texture in its place.")]
        [SerializeField] private Texture2D background;
        [SerializeField] private SceneLightingAsset sceneLighting;
        [Tooltip("Layers drifting over the plate, such as fog or smoke, drawn in order above it and below the foreground.")]
        [SerializeField] private PictureDriftLayer[] driftLayers = Array.Empty<PictureDriftLayer>();
        [SerializeField] private Texture2D foreground;
        [Tooltip("The foreground's rectangle in normalized coordinates of the picture.")]
        [SerializeField] private Rect foregroundRect = new Rect(0, 0, 1, 1);
        [SerializeField] private Color foregroundTint = Color.white;

        public Texture2D Background => background;
        public SceneLightingAsset SceneLighting => sceneLighting;
        public IReadOnlyList<PictureDriftLayer> DriftLayers => driftLayers;
        public Texture2D Foreground => foreground;
        public Rect ForegroundRect => foregroundRect;
        public Color ForegroundTint => foregroundTint;
        public override EncounterTablePresentationAsset Table => null;

        internal override void Present(IJourneyStage stage)
        {
            stage.ShowPicture(background, sceneLighting, driftLayers, foreground, foregroundRect, foregroundTint);
            stage.ShowHost(false);
        }

        internal override void Validate(string beat)
        {
            if (background == null && sceneLighting == null)
                throw new FormatException("The picture scene " + name + " of " + beat + " requires a background or scene lighting.");
            sceneLighting?.Validate();
            for (int i = 0; i < driftLayers.Length; ++i)
            {
                string layer = "Drift layer " + i + " of the picture scene " + name + " of " + beat;
                if (driftLayers[i] == null) throw new FormatException(layer + " is empty.");
                driftLayers[i].Validate(layer);
            }
        }
    }
}
