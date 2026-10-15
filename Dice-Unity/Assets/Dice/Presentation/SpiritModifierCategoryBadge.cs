using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** A small category mark accompanies family artwork without occupying the charm's status corner. */
    public sealed class SpiritModifierCategoryBadge : MonoBehaviour
    {
        public Image Accent { get; private set; }
        public Image Glyph { get; private set; }
        private Outline border;

        internal static SpiritModifierCategoryBadge Create(string name, Image icon)
        {
            var rect = SpiritUi.Rect(name, icon.transform.parent, Vector2.zero, Vector2.one);
            var badge = rect.gameObject.AddComponent<SpiritModifierCategoryBadge>();
            badge.Accent = SpiritUi.Rect("CategoryAccent", rect, Vector2.zero, new Vector2(0, 1))
                .gameObject.AddComponent<Image>();
            badge.Accent.rectTransform.offsetMin = new Vector2(-5, 2);
            badge.Accent.rectTransform.offsetMax = new Vector2(-2, -2);
            badge.Accent.raycastTarget = false;
            var backing = SpiritUi.Rect("CategoryBacking", rect, new Vector2(1, 0), new Vector2(1, 0))
                .gameObject.AddComponent<Image>();
            backing.rectTransform.anchoredPosition = new Vector2(-4, 4);
            float size = Mathf.Clamp(icon.rectTransform.sizeDelta.y * .38f, 14, 18);
            backing.rectTransform.sizeDelta = new Vector2(size, size);
            backing.color = new Color32(18, 27, 24, 255);
            backing.raycastTarget = false;
            badge.border = backing.gameObject.AddComponent<Outline>();
            badge.border.effectDistance = new Vector2(1, -1);
            badge.Glyph = SpiritUi.Full("CategoryGlyph", backing.transform).gameObject.AddComponent<Image>();
            badge.Glyph.rectTransform.offsetMin = new Vector2(2, 2);
            badge.Glyph.rectTransform.offsetMax = new Vector2(-2, -2);
            badge.Glyph.preserveAspect = true;
            badge.Glyph.raycastTarget = false;
            badge.Align(icon.rectTransform);
            badge.Render(null);
            return badge;
        }

        internal void Render(ModifierDefinition definition)
        {
            Glyph.sprite = definition == null ? null : SpiritModifierIcons.GetCategorySprite(definition);
            Color color = definition == null ? Color.white : SpiritModifierIcons.GetEffectColor(definition);
            Accent.color = border.effectColor = color;
            Glyph.color = Color.white;
            gameObject.SetActive(Glyph.sprite != null);
        }

        internal void Align(RectTransform icon)
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = icon.anchorMin;
            rect.anchorMax = icon.anchorMax;
            rect.pivot = icon.pivot;
            rect.sizeDelta = icon.sizeDelta;
            rect.anchoredPosition = icon.anchoredPosition;
        }
    }
}
