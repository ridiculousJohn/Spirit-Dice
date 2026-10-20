using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** A small category mark accompanies family artwork without occupying the charm's status corner. */
    public sealed class SpiritModifierCategoryBadge : MonoBehaviour
    {
        [field: SerializeField] public Image Accent { get; private set; }
        [field: SerializeField] public Image Glyph { get; private set; }
        [SerializeField] private Outline border;
        internal void Render(ModifierDefinition definition)
        {
            Glyph.sprite = definition == null ? null : SpiritModifierIcons.GetCategorySprite(definition);
            Color color = definition == null ? Color.white : SpiritModifierIcons.GetEffectColor(definition);
            Accent.color = border.effectColor = color;
            Glyph.color = Color.white;
            gameObject.SetActive(Glyph.sprite != null);
        }
    }
}
