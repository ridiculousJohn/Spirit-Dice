using System;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Compact centered inspection of one charm instance; any tap on the backdrop dismisses it. */
    public sealed class SpiritCharmPopup : MonoBehaviour
    {
        private RectTransform portrait;

        public RectTransform Root { get; private set; }
        public RectTransform Panel { get; private set; }
        public Text Title { get; private set; }
        public Image Icon { get; private set; }
        public SpiritModifierSigilGraphic Sigil { get; private set; }
        public SpiritModifierCategoryBadge Category { get; private set; }
        public Text Effect { get; private set; }
        public Text Description { get; private set; }
        public Text Status { get; private set; }
        public SpiritCharmStatusBadge Badge { get; private set; }
        public Button CloseButton { get; private set; }
        public long InstanceId { get; private set; } = -1;
        public bool IsOpen => Root != null && Root.gameObject.activeSelf;

        /** The overlay covers the parent canvas; the panel width follows the portrait. */
        public void Build(Transform parent, RectTransform portraitRect)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (portraitRect == null) throw new ArgumentNullException(nameof(portraitRect));
            if (Root != null) throw new InvalidOperationException("The charm popup has already been built.");

            portrait = portraitRect;
            var backdrop = SpiritUi.Panel("CharmPopupOverlay", parent, Vector2.zero, Vector2.one,
                new Color(0, 0, 0, .62f));
            Root = backdrop.rectTransform;
            var dismiss = backdrop.gameObject.AddComponent<Button>();
            dismiss.targetGraphic = backdrop;
            dismiss.transition = Selectable.Transition.None;
            dismiss.navigation = new Navigation { mode = Navigation.Mode.None };
            // Dismiss on release so the pointer's click remains owned by the overlay.
            dismiss.onClick.AddListener(Close);
            var theme = SpiritUi.GetTheme(transform);
            Panel = SpiritUi.Panel("CharmPopupPanel", Root,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), theme.panelColor, 0, true).rectTransform;
            Title = SpiritUi.Heading("CharmPopupTitle", Panel, "", 25, Vector2.zero, Vector2.one);
            Title.resizeTextForBestFit = false;
            Icon = SpiritCharmVisuals.MakeIcon("CharmPopupIcon", Panel, new Vector2(0, 1), 52);
            Icon.rectTransform.anchoredPosition = new Vector2(50, -50);
            Sigil = SpiritCharmVisuals.MakeSigil("CharmPopupSigil", Icon);
            Category = SpiritModifierCategoryBadge.Create("CharmPopupCategory", Icon);
            Effect = SpiritUi.Label("CharmPopupEffect", Panel, "", 22,
                Vector2.zero, Vector2.one);
            Description = SpiritUi.Label("CharmPopupDescription", Panel, "", 18,
                Vector2.zero, Vector2.one);
            Description.supportRichText = true;
            Status = SpiritUi.Label("CharmPopupStatus", Panel, "", 16,
                Vector2.zero, Vector2.one);
            Badge = SpiritCharmStatusBadge.Create(Panel);
            CloseButton = SpiritUi.Button("CloseCharmPopupButton", Panel, "Close",
                new Vector2(.3f, 0), new Vector2(.7f, 0), Close);
            var closeRect = (RectTransform)CloseButton.transform;
            closeRect.offsetMin = new Vector2(0, 18);
            closeRect.offsetMax = new Vector2(0, 62);
            Root.gameObject.SetActive(false);
        }

        public void Show(long instanceId, ModifierDefinition definition, SpiritCharmStatusPresentation status,
            EncounterPhase phase)
        {
            InstanceId = instanceId;
            SpiritCharmVisuals.SetIcon(Icon, Sigil, Category, definition);
            Title.text = SpiritModifierPresentation.GetName(definition);
            Effect.text = SpiritModifierPresentation.GetEffectText(definition);
            Effect.color = SpiritModifierIcons.GetEffectColor(definition);
            var theme = SpiritUi.GetTheme(transform);
            Description.text = SpiritRewardCard.GetModifierDescription(definition, theme.amber,
                theme.GetModifierTriggerColor(definition.Phase));
            Badge.Render(status);
            Status.text = status.GetDetailText(definition.Phase, phase);
            Status.color = Badge.Icon.color;
            Status.gameObject.SetActive(!string.IsNullOrEmpty(Status.text));
            Badge.gameObject.SetActive(Status.gameObject.activeSelf);
            Root.SetAsLastSibling();
            Root.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Layout();
        }

        public void Close()
        {
            if (Root != null) Root.gameObject.SetActive(false);
            InstanceId = -1;
        }

        public void Layout()
        {
            float width = portrait.rect.width * .84f;
            float titleHeight = SpiritCharmCardLayout.TextHeight(Title, width - 118);
            SpiritCharmCardLayout.Place(Title, 94, 24, 24, titleHeight);
            float top = 24 + Mathf.Max(52, titleHeight) + 16;
            foreach (var label in new[] { Effect, Description })
            {
                float height = SpiritCharmCardLayout.TextHeight(label, width - 48);
                SpiritCharmCardLayout.Place(label, 24, 24, top, height);
                top += height + 12;
            }
            if (Status.gameObject.activeSelf)
            {
                float height = Mathf.Max(24, SpiritCharmCardLayout.TextHeight(Status, width - 84));
                SpiritCharmCardLayout.Place(Status, 60, 24, top, height);
                var badgeRect = (RectTransform)Badge.transform;
                badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(0, 1);
                badgeRect.pivot = new Vector2(0, 1);
                badgeRect.anchoredPosition = new Vector2(24, -top);
                top += height + 12;
            }
            Panel.sizeDelta = new Vector2(width, top + 74);
        }
    }
}
