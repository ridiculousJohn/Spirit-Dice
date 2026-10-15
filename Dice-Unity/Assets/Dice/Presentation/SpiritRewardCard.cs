using System;
using System.Text.RegularExpressions;
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
        private SpiritHandPatternGraphic handPattern;
        private Image upgradeDivider;
        private Image upgradeSelectionMarker;
        private Image background;
        private Sprite modifierSprite;
        private Material modifierMaterial;
        private Image.Type modifierImageType;
        private Color modifierColor;
        private float modifierPixelsPerUnit;
        private bool modifierOutlineEnabled;
        private bool handUpgradeMode;
        private int handLevel;
        private long handBase;
        private long handMultiplier;
        private long handBaseIncrement;
        private long handMultiplierIncrement;
        private Action<long, long> choose;
        private long offerId;
        private long optionId;

        public Button Button { get; private set; }

        public static SpiritRewardCard Create(string name, Transform parent, Action<long, long> choose)
        {
            if (choose == null) throw new ArgumentNullException(nameof(choose));
            var image = SpiritUi.Panel(name, parent, Vector2.zero, Vector2.one, SpiritUi.GetTheme(parent).panelColor, 0, true);
            var card = image.gameObject.AddComponent<SpiritRewardCard>();
            card.background = image;
            card.modifierSprite = image.sprite;
            card.modifierMaterial = image.material;
            card.modifierImageType = image.type;
            card.modifierColor = image.color;
            card.modifierPixelsPerUnit = image.pixelsPerUnitMultiplier;
            card.modifierOutlineEnabled = image.GetComponent<Outline>()?.enabled ?? false;
            card.choose = choose;
            var layout = image.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 140;
            layout.flexibleHeight = 0;
            var button = image.gameObject.AddComponent<SpiritUiButtonVisual>();
            button.targetGraphic = image;
            var theme = SpiritUi.GetTheme(parent);
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = colors.selectedColor = theme != null ? theme.highlightedTint : new Color(1.10f, 1.08f, 1.01f, 1);
            colors.pressedColor = theme != null ? theme.pressedTint : new Color(.76f, .71f, .58f, 1);
            colors.disabledColor = theme != null ? theme.disabledTint : new Color(.46f, .47f, .43f, .88f);
            colors.fadeDuration = .12f;
            button.colors = colors;
            card.Button = button;
            button.onClick.AddListener(() => card.choose(card.offerId, card.optionId));

            card.title = SpiritUi.Heading("Title", card.transform, "", 20, Vector2.zero, Vector2.one);
            card.title.resizeTextMinSize = 14;
            card.condition = SpiritUi.Label("Condition", card.transform, "", 20, Vector2.zero, Vector2.one);
            card.condition.resizeTextForBestFit = true;
            card.condition.resizeTextMinSize = 15;
            card.condition.resizeTextMaxSize = 20;
            card.condition.supportRichText = true;
            card.effects = SpiritUi.Label("Effects", card.transform, "", 18, Vector2.zero, Vector2.one,
                TextAnchor.MiddleLeft, SpiritUi.GetTheme(parent).amber);
            card.effects.resizeTextForBestFit = true;
            card.effects.resizeTextMinSize = 14;
            card.effects.resizeTextMaxSize = 18;
            card.status = SpiritUi.Label("Status", card.transform, "", 13, Vector2.zero, Vector2.one,
                TextAnchor.MiddleRight, SpiritUi.GetTheme(parent).muted);
            card.status.resizeTextForBestFit = true;
            card.status.resizeTextMinSize = 12;
            card.status.resizeTextMaxSize = 13;
            card.currentHeader = SpiritUi.Label("CurrentHeader", card.transform, "CURRENT", 12, Vector2.zero, Vector2.one,
                TextAnchor.MiddleLeft, SpiritUi.GetTheme(parent).muted);
            card.upgradedHeader = SpiritUi.Label("UpgradedHeader", card.transform, "UPGRADED", 12, Vector2.zero, Vector2.one,
                TextAnchor.MiddleLeft, SpiritUi.GetTheme(parent).amber);
            card.upgradedValues = SpiritUi.Label("UpgradedValues", card.transform, "", 16, Vector2.zero, Vector2.one,
                TextAnchor.MiddleLeft, SpiritUi.GetTheme(parent).amber);
            card.upgradedValues.resizeTextForBestFit = true;
            card.upgradedValues.resizeTextMinSize = 14;
            card.upgradedValues.resizeTextMaxSize = 16;

            var iconRect = SpiritUi.Rect("CharmIcon", card.transform, new Vector2(0, 1), new Vector2(0, 1));
            iconRect.pivot = new Vector2(0, 1);
            iconRect.anchoredPosition = new Vector2(16, -14);
            iconRect.sizeDelta = new Vector2(42, 42);
            card.charmIcon = iconRect.gameObject.AddComponent<Image>();
            card.charmIcon.preserveAspect = true;
            card.charmIcon.raycastTarget = false;
            card.charmSigil = SpiritUi.Full("CharmSigil", card.transform).gameObject.AddComponent<SpiritModifierSigilGraphic>();
            card.charmSigil.rectTransform.anchorMin = card.charmSigil.rectTransform.anchorMax = new Vector2(0, 1);
            card.charmSigil.rectTransform.pivot = new Vector2(0, 1);
            card.charmSigil.rectTransform.anchoredPosition = iconRect.anchoredPosition;
            card.charmSigil.rectTransform.sizeDelta = iconRect.sizeDelta;
            card.charmSigil.color = SpiritUi.GetTheme(parent).amber;
            card.charmSigil.raycastTarget = false;
            var patternRect = SpiritUi.Rect("HandPattern", card.transform, new Vector2(0, 1), new Vector2(0, 1));
            patternRect.pivot = new Vector2(0, 1);
            patternRect.anchoredPosition = new Vector2(14, -45);
            patternRect.sizeDelta = new Vector2(174, 34);
            card.handPattern = patternRect.gameObject.AddComponent<SpiritHandPatternGraphic>();
            card.handPattern.raycastTarget = false;
            card.upgradeDivider = SpiritUi.Rect("UpgradeDivider", card.transform, Vector2.zero, new Vector2(1, 0),
                14, 0, 14, -1).gameObject.AddComponent<Image>();
            card.upgradeDivider.color = new Color(theme.amber.r, theme.amber.g, theme.amber.b, .3f);
            card.upgradeDivider.raycastTarget = false;
            card.upgradeSelectionMarker = SpiritUi.Rect("UpgradeSelectionMarker", card.transform, Vector2.zero, new Vector2(0, 1),
                0, 12, -3, 12).gameObject.AddComponent<Image>();
            card.upgradeSelectionMarker.color = theme.amber;
            card.upgradeSelectionMarker.raycastTarget = false;
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
            handUpgradeMode = false;
            background.sprite = modifierSprite;
            background.material = modifierMaterial;
            background.type = modifierImageType;
            background.color = modifierColor;
            background.pixelsPerUnitMultiplier = modifierPixelsPerUnit;
            var outline = background.GetComponent<Outline>();
            if (outline != null) outline.enabled = modifierOutlineEnabled;
            GetComponent<LayoutElement>().preferredHeight = 140;
            SetUpgradeVisibility(false);
            condition.alignment = TextAnchor.MiddleLeft;
            title.resizeTextMaxSize = 20;
            condition.resizeTextMinSize = 15;
            condition.resizeTextMaxSize = 20;
            effects.resizeTextMaxSize = 18;
            condition.color = SpiritUi.GetTheme(transform).cream;
            title.color = SpiritUi.GetTheme(transform).cream;
            status.text = "";
            status.gameObject.SetActive(false);
            charmIcon.sprite = SpiritModifierIcons.GetSprite(definition);
            charmIcon.gameObject.SetActive(charmIcon.sprite != null);
            charmSigil.Seed = SpiritModifierIcons.GetSigilSeed(definition);
            charmSigil.gameObject.SetActive(charmIcon.sprite == null);
            title.text = SpiritModifierPresentation.GetName(definition);
            condition.text = GetModifierDescription(definition, SpiritUi.GetTheme(transform).amber);
            effects.text = "<b>" + SpiritModifierPresentation.GetEffectText(definition) + "</b>";
            effects.supportRichText = true;
            UpdateModifierLayout();
            SetModifierSelected(false);
        }

        public static string GetModifierDescription(ModifierDefinition definition, Color highlight)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            string timing;
            switch (definition.Phase)
            {
                case ModifierPhase.InitialRoll: timing = "On initial roll"; break;
                case ModifierPhase.PlayerReroll: timing = "When rerolling"; break;
                case ModifierPhase.PlayHand: timing = "When playing a hand"; break;
                default: timing = DiagnosticText.Humanize(definition.Phase.ToString()); break;
            }
            string color = ColorUtility.ToHtmlStringRGB(highlight);
            string description = SpiritModifierPresentation.GetConditionText(definition);
            description = Regex.Replace(description,
                @"\b(?:\d+|High Die|Two Pair|Pair|Straight|rerolled|rerolls?|hands?|initial roll|previous played hand)\b",
                match => "<b><color=#" + color + ">" + match.Value + "</color></b>", RegexOptions.IgnoreCase);
            string text = "<b><color=#" + color + ">" + timing + "</color></b>: " + description;
            if (definition.ActivationLimit == ModifierActivationLimit.OncePerStage)
                text += " <b>Once per stage.</b>";
            else if (definition.ActivationLimit == ModifierActivationLimit.OncePerHand && definition.Phase != ModifierPhase.PlayHand)
                text += " <b>Once per hand.</b>";
            return text;
        }

        public void SetModifierSelected(bool selected)
        {
            if (handUpgradeMode) return;
            var theme = SpiritUi.GetTheme(transform);
            title.color = selected ? theme.amber : theme.cream;
            background.color = selected ? modifierColor * new Color(1.12f, 1.08f, .82f, 1) : modifierColor;
            upgradeSelectionMarker.gameObject.SetActive(selected);
            ((SpiritUiButtonVisual)Button).CaptureContent();
        }

        public void RenderHandUpgrade(HandDefinition hand, int level, long offerId, long optionId)
        {
            if (hand == null) throw new ArgumentNullException(nameof(hand));
            if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
            // Show persistent category values, excluding dice and temporary charm contributions.
            long currentBase = checked(hand.BaseScore + hand.BaseScorePerUpgrade * level);
            long currentMultiplier = checked(hand.Multiplier + hand.MultiplierPerUpgrade * level);
            _ = checked(level + 1);
            _ = checked(currentBase + hand.BaseScorePerUpgrade);
            _ = checked(currentMultiplier + hand.MultiplierPerUpgrade);
            this.offerId = offerId;
            this.optionId = optionId;
            handUpgradeMode = true;
            handLevel = level;
            handBase = currentBase;
            handMultiplier = currentMultiplier;
            handBaseIncrement = hand.BaseScorePerUpgrade;
            handMultiplierIncrement = hand.MultiplierPerUpgrade;
            GetComponent<LayoutElement>().preferredHeight = 100;
            background.sprite = null;
            background.material = null;
            background.type = Image.Type.Simple;
            var outline = background.GetComponent<Outline>();
            if (outline != null) outline.enabled = false;
            SetUpgradeVisibility(true);
            charmIcon.gameObject.SetActive(false);
            charmSigil.gameObject.SetActive(false);
            handPattern.Category = hand.Category;
            title.text = DiagnosticText.Humanize(hand.Category.ToString());
            condition.alignment = TextAnchor.MiddleRight;
            title.resizeTextMaxSize = 18;
            condition.resizeTextMinSize = 13;
            condition.resizeTextMaxSize = 15;
            effects.resizeTextMaxSize = 16;
            status.text = "";
            status.gameObject.SetActive(false);
            Place(title, 14, 125, 10, 26);
            Place(condition, 0, 14, 12, 22, .64f, 1);
            Place(effects, 202, 14, 43, 42);
            SetHandUpgradeSelected(false);
        }

        public void SetHandUpgradeSelected(bool selected)
        {
            if (!handUpgradeMode) return;
            var theme = SpiritUi.GetTheme(transform);
            condition.text = "Level " + handLevel + (selected ? " +1" : "");
            effects.text = "BASE " + DiagnosticText.Number(handBase) +
                (selected ? " +" + DiagnosticText.Number(handBaseIncrement) : "") +
                "\nMULT " + DiagnosticText.Number(handMultiplier) +
                (selected ? " +" + DiagnosticText.Number(handMultiplierIncrement) : "");
            title.color = selected ? theme.amber : theme.cream;
            condition.color = selected ? theme.amber : theme.muted;
            background.color = selected ? new Color32(43, 65, 49, 255) : new Color32(26, 40, 32, 215);
            upgradeSelectionMarker.gameObject.SetActive(selected);
            ((SpiritUiButtonVisual)Button).CaptureContent();
        }

        private void SetUpgradeVisibility(bool visible)
        {
            currentHeader.gameObject.SetActive(false);
            upgradedHeader.gameObject.SetActive(false);
            upgradedValues.gameObject.SetActive(false);
            handPattern.gameObject.SetActive(visible);
            upgradeDivider.gameObject.SetActive(visible);
            upgradeSelectionMarker.gameObject.SetActive(false);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (condition == null || currentHeader == null || handUpgradeMode) return;
            UpdateModifierLayout();
        }

        private void UpdateModifierLayout()
        {
            float height = ((RectTransform)transform).rect.height;
            if (height <= 0) height = GetComponent<LayoutElement>().preferredHeight;
            Place(title, 68, 16, 10, 25);
            Place(effects, 68, 16, 35, 25);
            Place(condition, 16, 16, 64, Mathf.Max(36, height - 76));
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
