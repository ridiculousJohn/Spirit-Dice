using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Owned charm sections wrap at readable font sizes and share the grid's measured row height. */
    public sealed class SpiritCharmCardLayout : MonoBehaviour
    {
        private Text title;
        private Text condition;
        private Text effects;
        private Text status;
        private Image icon;

        internal void Initialize(Text title, Text condition, Text effects, Text status, Image icon)
        {
            this.title = title;
            this.condition = condition;
            this.effects = effects;
            this.status = status;
            this.icon = icon;
            foreach (var label in new[] { title, condition, effects, status }) label.resizeTextForBestFit = false;
        }

        internal float Arrange(float width)
        {
            bool hasIcon = icon.gameObject.activeSelf;
            float titleLeft = hasIcon ? 58 : 16;
            float titleHeight = TextHeight(title, width - titleLeft - 16);
            Place(title, titleLeft, 16, 14, titleHeight);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, 1);
            icon.rectTransform.anchoredPosition = new Vector2(34, -32);
            float top = 14 + Mathf.Max(titleHeight, hasIcon ? 36 : 0) + 8;
            foreach (var label in new[] { condition, effects })
            {
                float height = TextHeight(label, width - 32);
                Place(label, 16, 16, top, height);
                top += height + 6;
            }
            // Reserve a separate footer so the corner badge never covers wrapped condition or effect text.
            return Mathf.Max(156, top + 36);
        }

        internal static float TextHeight(Text label, float width)
        {
            var settings = label.GetGenerationSettings(new Vector2(Mathf.Max(1, width), 0));
            settings.resizeTextForBestFit = false;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            return Mathf.Ceil(label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text, settings) /
                label.pixelsPerUnit) + 2;
        }

        internal static void Place(Text label, float left, float right, float top, float height)
        {
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
