using Dice.UnityContent;
using UnityEngine;

namespace Dice.Presentation
{
    /** The journey screen as journey scenes and the kinds of beat show themselves on it. */
    internal interface IJourneyStage
    {
        /** Shows a picture on the picture layer: its background, lit by its scene lighting when it has one, and its foreground. */
        void ShowPicture(Texture2D background, SceneLightingAsset lighting, Texture2D foreground, Rect foregroundRect,
            Color foregroundTint);

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
