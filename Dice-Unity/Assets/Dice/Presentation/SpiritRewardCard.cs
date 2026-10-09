using System;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** A reusable reward choice keeps its identity and effective upgrade values visible. */
    public sealed class SpiritRewardCard : MonoBehaviour
    {
        private Text title;
        private Text condition;
        private Text effects;
        private Text status;
        private Text currentHeader;
        private Text upgradedHeader;
        private Text upgradedValues;
        private Image charmIcon;
        private SpiritModifierSigilGraphic charmSigil;
        private Action<long, long> choose;
        private long offerId;
        private long optionId;

        public Button Button { get; private set; }

        public static SpiritRewardCard Create(string name, Transform parent, Action<long, long> choose)
        {
            if (choose == null) throw new ArgumentNullException(nameof(choose));
            var image = SpiritUi.Panel(name, parent, Vector2.zero, Vector2.one, SpiritUi.PanelColor, 0, true);
            var card = image.gameObject.AddComponent<SpiritRewardCard>();
            card.choose = choose;
            var layout = image.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 140;
            layout.flexibleHeight = 0;
            var button = image.gameObject.AddComponent<SpiritUiButtonVisual>();
            button.targetGraphic = image;
            var theme = Resources.Load<SpiritUiTheme>("SpiritHouseTheme");
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = colors.selectedColor = theme != null ? theme.highlightedTint : new Color(1.10f, 1.08f, 1.01f, 1);
            colors.pressedColor = theme != null ? theme.pressedTint : new Color(.76f, .71f, .58f, 1);
            colors.disabledColor = theme != null ? theme.disabledTint : new Color(.46f, .47f, .43f, .88f);
            colors.fadeDuration = .12f;
            button.colors = colors;
            card.Button = button;
            button.onClick.AddListener(() => card.choose(card.offerId, card.optionId));

            card.title = SpiritUi.Heading("Title", card.transform, "", 18, Vector2.zero, Vector2.one);
            card.title.resizeTextMinSize = 14;
            card.condition = SpiritUi.Label("Condition", card.transform, "", 15, Vector2.zero, Vector2.one);
            card.condition.resizeTextForBestFit = true;
            card.condition.resizeTextMinSize = 13;
            card.condition.resizeTextMaxSize = 15;
            card.effects = SpiritUi.Label("Effects", card.transform, "", 16, Vector2.zero, Vector2.one,
                TextAnchor.MiddleLeft, SpiritUi.Amber);
            card.status = SpiritUi.Label("Status", card.transform, "", 13, Vector2.zero, Vector2.one,
                TextAnchor.MiddleRight, SpiritUi.Muted);
            card.status.resizeTextForBestFit = true;
            card.status.resizeTextMinSize = 12;
            card.status.resizeTextMaxSize = 13;
            card.currentHeader = SpiritUi.Label("CurrentHeader", card.transform, "CURRENT", 12, Vector2.zero, Vector2.one,
                TextAnchor.MiddleLeft, SpiritUi.Muted);
            card.upgradedHeader = SpiritUi.Label("UpgradedHeader", card.transform, "UPGRADED", 12, Vector2.zero, Vector2.one,
                TextAnchor.MiddleLeft, SpiritUi.Amber);
            card.upgradedValues = SpiritUi.Label("UpgradedValues", card.transform, "", 16, Vector2.zero, Vector2.one,
                TextAnchor.MiddleLeft, SpiritUi.Amber);

            var iconRect = SpiritUi.Rect("CharmIcon", card.transform, new Vector2(0, 1), new Vector2(0, 1));
            iconRect.pivot = new Vector2(0, 1);
            iconRect.anchoredPosition = new Vector2(26, -24);
            iconRect.sizeDelta = new Vector2(54, 54);
            card.charmIcon = iconRect.gameObject.AddComponent<Image>();
            card.charmIcon.preserveAspect = true;
            card.charmIcon.raycastTarget = false;
            card.charmSigil = SpiritUi.Full("CharmSigil", card.transform).gameObject.AddComponent<SpiritModifierSigilGraphic>();
            card.charmSigil.rectTransform.anchorMin = card.charmSigil.rectTransform.anchorMax = new Vector2(0, 1);
            card.charmSigil.rectTransform.pivot = new Vector2(0, 1);
            card.charmSigil.rectTransform.anchoredPosition = iconRect.anchoredPosition;
            card.charmSigil.rectTransform.sizeDelta = iconRect.sizeDelta;
            card.charmSigil.color = SpiritUi.Amber;
            card.charmSigil.raycastTarget = false;
            card.SetUpgradeVisibility(false);
            card.charmIcon.gameObject.SetActive(false);
            card.charmSigil.gameObject.SetActive(false);
            button.CaptureContent();
            return card;
        }

        public void RenderModifier(ModifierDefinition definition, long offerId, long optionId)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            this.offerId = offerId;
            this.optionId = optionId;
            SetUpgradeVisibility(false);
            charmIcon.sprite = SpiritModifierIcons.GetSprite(definition);
            charmIcon.gameObject.SetActive(charmIcon.sprite != null);
            charmSigil.Seed = SpiritModifierIcons.GetSigilSeed(definition);
            charmSigil.gameObject.SetActive(charmIcon.sprite == null);
            title.text = SpiritModifierPresentation.GetName(definition);
            condition.text = SpiritModifierPresentation.GetConditionText(definition);
            effects.text = SpiritModifierPresentation.GetEffectText(definition);
            status.text = SpiritModifierPresentation.GetTimingText(definition);
            Place(title, 90, 24, 14, 22);
            Place(condition, 90, 24, 37, 48);
            Place(effects, 24, 24, 87, 22);
            Place(status, 24, 24, 111, 17);
            ((SpiritUiButtonVisual)Button).CaptureContent();
        }

        public void RenderHandUpgrade(HandDefinition hand, int level, long offerId, long optionId)
        {
            if (hand == null) throw new ArgumentNullException(nameof(hand));
            if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
            // Compare the category's persistent values, excluding dice and temporary charm contributions.
            int upgradedLevel = checked(level + 1);
            long currentBase = checked(hand.BaseScore + hand.BaseScorePerUpgrade * level);
            long currentMultiplier = checked(hand.Multiplier + hand.MultiplierPerUpgrade * level);
            long upgradedBase = checked(hand.BaseScore + hand.BaseScorePerUpgrade * upgradedLevel);
            long upgradedMultiplier = checked(hand.Multiplier + hand.MultiplierPerUpgrade * upgradedLevel);
            this.offerId = offerId;
            this.optionId = optionId;
            SetUpgradeVisibility(true);
            charmIcon.gameObject.SetActive(false);
            charmSigil.gameObject.SetActive(false);
            title.text = DiagnosticText.Humanize(hand.Category.ToString());
            condition.text = "Level " + level + " to " + upgradedLevel;
            effects.text = "BASE " + DiagnosticText.Number(currentBase) + "\nMULT " + DiagnosticText.Number(currentMultiplier);
            upgradedValues.text = "BASE " + DiagnosticText.Number(upgradedBase) + "\nMULT " + DiagnosticText.Number(upgradedMultiplier);
            status.text = "Free hand upgrade";
            Place(title, 24, 24, 14, 22);
            Place(condition, 24, 24, 37, 19);
            Place(currentHeader, 24, 8, 58, 17, 0, .5f);
            Place(upgradedHeader, 8, 24, 58, 17, .5f, 1);
            Place(effects, 24, 8, 76, 36, 0, .5f);
            Place(upgradedValues, 8, 24, 76, 36, .5f, 1);
            Place(status, 24, 24, 114, 17);
            ((SpiritUiButtonVisual)Button).CaptureContent();
        }

        private void SetUpgradeVisibility(bool visible)
        {
            currentHeader.gameObject.SetActive(visible);
            upgradedHeader.gameObject.SetActive(visible);
            upgradedValues.gameObject.SetActive(visible);
        }

        private static void Place(Text label, float left, float right, float top, float height, float min = 0, float max = 1)
        {
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(min, 1);
            rect.anchorMax = new Vector2(max, 1);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
