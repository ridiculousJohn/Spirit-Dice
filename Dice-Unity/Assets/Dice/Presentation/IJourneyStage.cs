using System.Collections.Generic;
using Dice.UnityContent;
using UnityEngine;

namespace Dice.Presentation
{
    /** The journey screen as journey scenes and the kinds of beat show themselves on it. */
    internal interface IJourneyStage
    {
        /**
         * Shows a picture on the picture layer: its background, lit by its scene lighting when it has one, its drift layers
         * over the background, drifting until the picture leaves, and its foreground. The picture keeps the portrait's
         * 540:960 shape; on a screen narrower than that, horizontalCropAnchor is the part that stays in view (0 the left
         * edge, 0.5 the centre, 1 the right edge), and on a shorter one the top is cropped.
         */
        void ShowPicture(Texture2D background, SceneLightingAsset lighting, IReadOnlyList<PictureDriftLayer> driftLayers,
            Texture2D foreground, Rect foregroundRect, Color foregroundTint, float horizontalCropAnchor);

        /** Empties the picture layer, so the table scene shows. */
        void ClearPicture();

        void ShowHost(bool visible);

        /** Shows the charm the narration names, filling its {charm} placeholder with the charm's family. */
        void ShowCharm(ModifierAsset charm);

        /** Shows the table's rule cards and Accept in place of Continue. */
        void ShowInvitation(EncounterTablePresentationAsset table);

        /** Places the picture's background and foreground at offsets, in picture widths and heights, and scales. */
        void MovePicture(Vector2 backgroundOffset, float backgroundScale, Vector2 foregroundOffset, float foregroundScale);
    }
}
