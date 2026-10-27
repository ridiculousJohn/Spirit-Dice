using System.Globalization;
using UnityEngine;
using Dice.Engine;

namespace Dice.Presentation
{
    /** The host's wording and artwork remain separate from the encounter's mechanical rule. */
    [CreateAssetMenu(menuName = "Dice/Encounter Rule Presentation")]
    public sealed class EncounterRulePresentationAsset : ScriptableObject
    {
        [SerializeField] private string ruleId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private string availableText;
        [SerializeField] private string usedText;
        [SerializeField] private Sprite icon;
        [SerializeField] private ModifierPhase activationPhase = ModifierPhase.PlayerReroll;
        [Tooltip("Optional visuals the rule shows on the tray's dice, such as marked and bound dice; empty for none.")]
        [SerializeField] private RuleDiceVisualAsset diceVisual;

        public string RuleId => ruleId;
        public string DisplayName => displayName;
        public string Description => string.IsNullOrWhiteSpace(description) ? availableText : description;
        public string AvailableText => availableText;
        public string UsedText => usedText;
        public Sprite Icon => icon;
        public ModifierPhase ActivationPhase => activationPhase;
        public RuleDiceVisualAsset DiceVisual => diceVisual;

        /** The card text for the rule's availability, with each {name} the rule's snapshot supplies filled in. */
        public string TextFor(EncounterRuleSnapshot rule)
        {
            string text = rule.IsAvailable ? availableText : usedText;
            if (text == null) return null;
            foreach (var value in rule.TextValues)
                text = text.Replace("{" + value.Key + "}", value.Value.ToString(CultureInfo.InvariantCulture));
            return text;
        }
    }
}
