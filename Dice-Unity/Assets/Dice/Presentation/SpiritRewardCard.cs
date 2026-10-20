using System;
using System.Text.RegularExpressions;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Binds one offer option to authored modifier and hand upgrade presentations. */
    public sealed class SpiritRewardCard : MonoBehaviour
    {
        [SerializeField] private GameObject modifierRoot;
        [SerializeField] private GameObject handRoot;
        [SerializeField] private Text title;
        [SerializeField] private Text condition;
        [SerializeField] private Text effects;
        [SerializeField] private Image charmIcon;
        [SerializeField] private SpiritModifierSigilGraphic charmSigil;
        [SerializeField] private SpiritModifierCategoryBadge charmCategory;
        [SerializeField] private Text handTitle;
        [SerializeField] private Text handLevelText;
        [SerializeField] private Text handValues;
        [SerializeField] private SpiritHandPatternGraphic handPattern;
        [SerializeField] private Graphic selectionMarker;
        [SerializeField] private Image modifierBackground;
        [SerializeField] private Image handBackground;
        [SerializeField] private Color modifierColor = Color.white;
        [SerializeField] private Color modifierSelectedColor = new Color(1.12f, 1.08f, .82f, 1);
        [SerializeField] private Color handColor = new Color32(26, 40, 32, 215);
        [SerializeField] private Color handSelectedColor = new Color32(43, 65, 49, 255);
        [SerializeField] private Color titleColor = new Color32(246, 231, 190, 255);
        [SerializeField] private Color selectedTitleColor = new Color32(210, 166, 88, 255);
        [SerializeField] private Color handLevelColor = new Color32(191, 187, 162, 255);
        [field: SerializeField] public Button Button { get; private set; }
        private Action<long, long> choose;
        private long offerId;
        private long optionId;
        private bool handUpgradeMode;
        private int handLevel;
        private long handBase;
        private long handMultiplier;
        private long handBaseIncrement;
        private long handMultiplierIncrement;

        public static SpiritRewardCard Create(SpiritRewardCard prefab, string name, Transform parent, Action<long, long> choose)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            var card = Instantiate(prefab, parent, false);
            card.name = name;
            card.choose = choose ?? throw new ArgumentNullException(nameof(choose));
            card.Button.onClick.AddListener(() => card.choose(card.offerId, card.optionId));
            return card;
        }

        public void RenderModifier(ModifierDefinition definition, long offerId, long optionId)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            this.offerId = offerId;
            this.optionId = optionId;
            handUpgradeMode = false;
            modifierRoot.SetActive(true);
            handRoot.SetActive(false);
            SpiritCharmVisuals.SetIcon(charmIcon, charmSigil, charmCategory, definition);
            title.text = SpiritModifierPresentation.GetName(definition);
            var theme = SpiritUi.GetTheme(transform);
            condition.text = GetModifierDescription(definition, theme.amber, theme.GetModifierTriggerColor(definition.Phase));
            effects.text = "<b>" + SpiritModifierPresentation.GetEffectText(definition) + "</b>";
            effects.color = SpiritModifierIcons.GetEffectColor(definition);
            SetModifierSelected(false);
        }

        public static string GetModifierDescription(ModifierDefinition definition, Color highlight, Color? triggerHighlight = null)
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
            string triggerColor = ColorUtility.ToHtmlStringRGB(triggerHighlight ?? highlight);
            string text = "<b><color=#" + triggerColor + ">" + timing + "</color></b>: " + description;
            if (definition.ActivationLimit == ModifierActivationLimit.OncePerStage)
                text += " <b>Once per stage.</b>";
            else if (definition.ActivationLimit == ModifierActivationLimit.OncePerHand && definition.Phase != ModifierPhase.PlayHand)
                text += " <b>Once per hand.</b>";
            return text;
        }

        public void SetModifierSelected(bool selected)
        {
            if (handUpgradeMode) return;
            title.color = selected ? selectedTitleColor : titleColor;
            modifierBackground.color = selected ? modifierSelectedColor : modifierColor;
            selectionMarker.gameObject.SetActive(selected);
        }

        public void RenderHandUpgrade(HandDefinition hand, int level, long offerId, long optionId)
        {
            if (hand == null) throw new ArgumentNullException(nameof(hand));
            if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
            this.offerId = offerId;
            this.optionId = optionId;
            handUpgradeMode = true;
            handLevel = level;
            handBase = checked(hand.BaseScore + hand.BaseScorePerUpgrade * level);
            handMultiplier = checked(hand.Multiplier + hand.MultiplierPerUpgrade * level);
            handBaseIncrement = hand.BaseScorePerUpgrade;
            handMultiplierIncrement = hand.MultiplierPerUpgrade;
            _ = checked(level + 1);
            _ = checked(handBase + handBaseIncrement);
            _ = checked(handMultiplier + handMultiplierIncrement);
            modifierRoot.SetActive(false);
            handRoot.SetActive(true);
            handTitle.text = DiagnosticText.Humanize(hand.Category.ToString());
            handPattern.Category = hand.Category;
            SetHandUpgradeSelected(false);
        }

        public void SetHandUpgradeSelected(bool selected)
        {
            if (!handUpgradeMode) return;
            handTitle.color = selected ? selectedTitleColor : titleColor;
            handLevelText.color = selected ? selectedTitleColor : handLevelColor;
            handBackground.color = selected ? handSelectedColor : handColor;
            handLevelText.text = "Level " + handLevel + (selected ? " +1" : "");
            handValues.text = "BASE " + DiagnosticText.Number(handBase) +
                (selected ? " +" + DiagnosticText.Number(handBaseIncrement) : "") +
                "\nMULT " + DiagnosticText.Number(handMultiplier) +
                (selected ? " +" + DiagnosticText.Number(handMultiplierIncrement) : "");
            selectionMarker.gameObject.SetActive(selected);
        }
    }
}
