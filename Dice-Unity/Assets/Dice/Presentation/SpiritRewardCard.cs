using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Binds one offer option to authored modifier and hand upgrade presentations. */
    public sealed class SpiritRewardCard : MonoBehaviour
    {
        [Serializable]
        private sealed class HandPatternSprite
        {
            public HandCategory Category;
            public Sprite Sprite;
        }

        [SerializeField] private GameObject modifierRoot;
        [SerializeField] private GameObject handRoot;
        [SerializeField] private Text title;
        [SerializeField] private Text condition;
        [SerializeField] private Text effects;
        [SerializeField] private Image charmIcon;
        [SerializeField] private SpiritModifierCategoryBadge charmCategory;
        [SerializeField] private Text handTitle;
        [SerializeField] private Text handLevelText;
        [SerializeField] private Text handValues;
        [SerializeField] private Image handPattern;
        [SerializeField] private HandPatternSprite[] handPatternSprites = Array.Empty<HandPatternSprite>();
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

        public void RenderModifier(ModifierPresentationCatalog charms, ModifierDefinition definition, long offerId, long optionId)
        {
            if (charms == null) throw new ArgumentNullException(nameof(charms));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            this.offerId = offerId;
            this.optionId = optionId;
            handUpgradeMode = false;
            modifierRoot.SetActive(true);
            handRoot.SetActive(false);
            SpiritCharmVisuals.SetIcon(charmIcon, charmCategory, charms, definition);
            title.text = charms.GetName(definition);
            var theme = SpiritUi.GetTheme(transform);
            condition.text = GetModifierDescription(charms, definition, SpiritUi.GetWording(transform).charmText, theme.amber,
                theme.GetModifierTriggerColor(definition.Phase));
            effects.text = "<b>" + charms.GetEffectText(definition) + "</b>";
            effects.color = charms.GetEffectColor(definition);
            SetModifierSelected(false);
        }

        /**
         * The charm's description in the wording's description template: its phase's lead in the trigger highlight, its
         * condition with numbers and the wording's highlighted terms in the highlight, and its activation limit's note.
         */
        public static string GetModifierDescription(ModifierPresentationCatalog charms, ModifierDefinition definition,
            SpiritUiWording.CharmText wording, Color highlight, Color? triggerHighlight = null)
        {
            if (charms == null) throw new ArgumentNullException(nameof(charms));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (wording == null) throw new ArgumentNullException(nameof(wording));
            string color = ColorUtility.ToHtmlStringRGB(highlight);
            var terms = new List<string> { @"\d+" };
            foreach (string term in wording.highlightedTerms) terms.Add(Regex.Escape(term));
            string condition = Regex.Replace(charms.GetConditionText(definition), @"\b(?:" + string.Join("|", terms) + @")\b",
                match => "<b><color=#" + color + ">" + match.Value + "</color></b>", RegexOptions.IgnoreCase);
            string triggerColor = ColorUtility.ToHtmlStringRGB(triggerHighlight ?? highlight);
            string lead = "<b><color=#" + triggerColor + ">" + charms.GetDescriptionLead(definition) + "</color></b>";
            string note = charms.GetDescriptionNote(definition);
            return wording.Description(lead, condition, note.Length > 0 ? "<b>" + note + "</b>" : "");
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
            Sprite pattern = GetHandPatternSprite(hand.Category);
            this.offerId = offerId;
            this.optionId = optionId;
            handUpgradeMode = true;
            handLevel = level;
            handBase = hand.BaseScoreAt(level);
            handMultiplier = hand.MultiplierAt(level);
            handBaseIncrement = hand.BaseScorePerUpgrade;
            handMultiplierIncrement = hand.MultiplierPerUpgrade;
            _ = checked(level + 1);
            _ = checked(handBase + handBaseIncrement);
            _ = checked(handMultiplier + handMultiplierIncrement);
            modifierRoot.SetActive(false);
            handRoot.SetActive(true);
            handTitle.text = SpiritUi.GetWording(transform).handNames.Name(hand.Category);
            handPattern.sprite = pattern;
            SetHandUpgradeSelected(false);
        }

        private Sprite GetHandPatternSprite(HandCategory category)
        {
            foreach (var pattern in handPatternSprites)
                if (pattern != null && pattern.Category == category && pattern.Sprite != null) return pattern.Sprite;
            throw new InvalidOperationException("The reward card has no hand pattern sprite for " + category + ".");
        }

        public void SetHandUpgradeSelected(bool selected)
        {
            if (!handUpgradeMode) return;
            handTitle.color = selected ? selectedTitleColor : titleColor;
            handLevelText.color = selected ? selectedTitleColor : handLevelColor;
            handBackground.color = selected ? handSelectedColor : handColor;
            var wording = SpiritUi.GetWording(transform).rewardCard;
            handLevelText.text = wording.Level(handLevel, selected);
            handValues.text = wording.Values(handBase, handMultiplier, handBaseIncrement, handMultiplierIncrement, selected);
            selectionMarker.gameObject.SetActive(selected);
        }
    }
}
