using System;
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
            condition.text = SpiritCharmVisuals.GetModifierDescription(charms, definition, SpiritUi.GetWording(transform).charmText,
                theme.amber, theme.GetModifierTriggerColor(definition.Phase));
            effects.text = "<b>" + charms.GetEffectText(definition) + "</b>";
            effects.color = charms.GetEffectColor(definition);
            SetModifierSelected(false);
        }

        public void SetModifierSelected(bool selected)
        {
            if (handUpgradeMode) return;
            var theme = SpiritUi.GetTheme(transform);
            title.color = selected ? theme.amber : theme.cream;
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
            var theme = SpiritUi.GetTheme(transform);
            handTitle.color = selected ? theme.amber : theme.cream;
            handLevelText.color = selected ? theme.amber : theme.muted;
            handBackground.color = selected ? handSelectedColor : handColor;
            var wording = SpiritUi.GetWording(transform).rewardCard;
            handLevelText.text = wording.Level(handLevel, selected);
            handValues.text = wording.Values(handBase, handMultiplier, handBaseIncrement, handMultiplierIncrement, selected);
            selectionMarker.gameObject.SetActive(selected);
        }
    }
}
