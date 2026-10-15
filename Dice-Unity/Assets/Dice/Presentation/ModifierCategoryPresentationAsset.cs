using UnityEngine;

namespace Dice.Presentation
{
    public enum ModifierCategorySymbol { None = 0, Base = 1, Multiplier = 2, Combined = 3, Rerolls = 4 }

    /** Shared category styling for charm ink, accents, and effect badges. */
    [CreateAssetMenu(menuName = "Dice/Modifier Category Presentation")]
    public sealed class ModifierCategoryPresentationAsset : ScriptableObject
    {
        [SerializeField] private Color accentColor = new Color32(239, 224, 190, 255);
        [SerializeField] private Color inkColor = new Color32(70, 52, 35, 255);
        [SerializeField] private ModifierCategorySymbol symbol;

        public Color AccentColor => accentColor;
        public Color InkColor => inkColor;
        public ModifierCategorySymbol Symbol => symbol;
    }
}
