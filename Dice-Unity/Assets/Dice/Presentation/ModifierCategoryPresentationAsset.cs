using UnityEngine;

namespace Dice.Presentation
{
    /** Shared category styling for charm ink, accents, and effect badges. */
    [CreateAssetMenu(menuName = "Dice/Modifier Category Presentation")]
    public sealed class ModifierCategoryPresentationAsset : ScriptableObject
    {
        [SerializeField] private Color accentColor = new Color32(239, 224, 190, 255);
        [SerializeField] private Color inkColor = new Color32(70, 52, 35, 255);
        /** The category's mark on charm badges and dice; a category without one shows no mark. */
        [SerializeField] private Sprite symbol;

        public Color AccentColor => accentColor;
        public Color InkColor => inkColor;
        public Sprite Symbol => symbol;
    }
}
