using System;
using UnityEngine;

namespace Dice.Presentation
{
    /** Replaceable artwork and typography for the encounter's live controls. */
    [CreateAssetMenu(menuName = "Dice/Spirit UI Theme", fileName = "SpiritHouseTheme")]
    public sealed class SpiritUiTheme : ScriptableObject
    {
        [Serializable]
        public sealed class Frame
        {
            public Texture2D texture;
            public Sprite sprite;
            public Material surfaceMaterial;
            public Vector2 referenceTextureSize;
            public Rect spriteRect;
            public Vector4 border = new Vector4(120, 60, 120, 60);
            public float pixelsPerUnitMultiplier = 3;
            internal Sprite GetSprite() => sprite;
        }

        public Font headingFont;
        public Font bodyFont;
        public Frame greenFrame = new Frame();
        public Frame amberFrame = new Frame();
        public Frame darkFrame = new Frame();
        public Frame woodFrame = new Frame();
        public Color cream = new Color32(246, 231, 190, 255);
        public Color muted = new Color32(191, 187, 162, 255);
        public Color amber = new Color32(210, 166, 88, 255);
        public Color playHandTrigger = new Color32(164, 203, 139, 255);
        public Color rerollTrigger = new Color32(235, 191, 93, 255);
        public Color initialRollTrigger = new Color32(143, 192, 220, 255);
        public Color panelColor = new Color32(25, 36, 29, 255);
        public Color highlightedTint = new Color(1.10f, 1.08f, 1.01f, 1);
        public Color pressedTint = new Color(.76f, .71f, .58f, 1);
        public Color disabledTint = new Color(.46f, .47f, .43f, .88f);

        public Color GetModifierTriggerColor(Dice.Engine.ModifierPhase phase)
        {
            switch (phase)
            {
                case Dice.Engine.ModifierPhase.PlayHand: return playHandTrigger;
                case Dice.Engine.ModifierPhase.PlayerReroll: return rerollTrigger;
                case Dice.Engine.ModifierPhase.InitialRoll: return initialRollTrigger;
                default: return amber;
            }
        }

    }
}
