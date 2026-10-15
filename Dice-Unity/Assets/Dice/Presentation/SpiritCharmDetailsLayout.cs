using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Inspection gives the complete rule room and leaves the remaining space to the scrollable inventory. */
    public sealed class SpiritCharmDetailsLayout : MonoBehaviour
    {
        private Text title;
        private Text body;
        private Image icon;
        private SpiritModifierSigilGraphic sigil;
        private SpiritModifierCategoryBadge category;
        private RectTransform scroll;
        private RectTransform statusRow;
        private Text status;
        private SpiritCharmStatusBadge badge;
        private Vector2 measuredSize = new Vector2(-1, -1);
        private bool arranging;

        internal void Initialize(Text title, Text body, Image icon, RectTransform scroll,
            RectTransform statusRow, Text status, SpiritCharmStatusBadge badge, SpiritModifierSigilGraphic sigil,
            SpiritModifierCategoryBadge category)
        {
            this.title = title;
            this.body = body;
            this.icon = icon;
            this.sigil = sigil;
            this.category = category;
            this.scroll = scroll;
            this.statusRow = statusRow;
            this.status = status;
            this.badge = badge;
            title.resizeTextForBestFit = body.resizeTextForBestFit = false;
            RefreshLayout();
        }

        internal void RefreshLayout()
        {
            measuredSize = new Vector2(-1, -1);
            Arrange();
        }

        private void OnEnable() => RefreshLayout();
        private void OnRectTransformDimensionsChange() => Arrange();

        private void Arrange()
        {
            if (arranging || title == null || body == null || scroll == null) return;
            var rect = (RectTransform)transform;
            var parent = (RectTransform)rect.parent;
            var size = new Vector2(rect.rect.width, parent.rect.height);
            if (size.x <= 0 || size.y <= 0 || size == measuredSize) return;
            arranging = true;
            measuredSize = size;
            bool hasIcon = icon.gameObject.activeSelf || sigil.gameObject.activeSelf;
            float titleLeft = hasIcon ? 76 : 16;
            float titleHeight = SpiritCharmCardLayout.TextHeight(title, size.x - titleLeft - 16);
            SpiritCharmCardLayout.Place(title, titleLeft, 16, 14, titleHeight);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, 1);
            icon.rectTransform.anchoredPosition = new Vector2(42, -36);
            sigil.rectTransform.anchorMin = sigil.rectTransform.anchorMax = icon.rectTransform.anchorMin;
            sigil.rectTransform.anchoredPosition = icon.rectTransform.anchoredPosition;
            category.Align(icon.rectTransform);
            float bodyTop = 14 + Mathf.Max(titleHeight, hasIcon ? 44 : 0) + 8;
            float bodyHeight = SpiritCharmCardLayout.TextHeight(body, size.x - 32);
            SpiritCharmCardLayout.Place(body, 16, 16, bodyTop, bodyHeight);
            float bottom = bodyTop + bodyHeight;
            if (statusRow.gameObject.activeSelf)
            {
                float badgeWidth = ((RectTransform)badge.transform).rect.width;
                float statusHeight = Mathf.Max(24, SpiritCharmCardLayout.TextHeight(status, size.x - 32 - badgeWidth - 12));
                float statusTop = bottom + 12;
                statusRow.anchorMin = new Vector2(0, 1);
                statusRow.anchorMax = Vector2.one;
                statusRow.offsetMin = new Vector2(16, -statusTop - statusHeight);
                statusRow.offsetMax = new Vector2(-16, -statusTop);
                SpiritCharmCardLayout.Place(status, 0, badgeWidth + 12, 0, statusHeight);
                bottom = statusTop + statusHeight;
            }
            float height = Mathf.Max(parent.rect.height * .29f - 12, bottom + 14);
            rect.anchorMin = new Vector2(.02f, .04f);
            rect.anchorMax = new Vector2(.98f, .04f);
            rect.offsetMin = new Vector2(6, 6);
            rect.offsetMax = new Vector2(-6, 6 + height);
            scroll.anchorMin = Vector2.zero;
            scroll.offsetMin = new Vector2(scroll.offsetMin.x,
                Mathf.Max(parent.rect.height * .34f + 8, parent.rect.height * .04f + 6 + height + 10));
            arranging = false;
        }
    }
}
