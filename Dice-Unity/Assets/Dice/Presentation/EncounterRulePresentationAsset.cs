using System;
using System.Collections.Generic;
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
        [Tooltip("Optional longer description; a rule without one is described by its available text.")]
        [SerializeField, TextArea] private string description;
        [SerializeField] private string availableText;
        [SerializeField] private string usedText;
        [SerializeField] private Sprite icon;
        [Tooltip("The action the rule acts on: its HUD badge sits beside that action's button and its activation cue marks it.")]
        [SerializeField] private ModifierPhase activationPhase = ModifierPhase.PlayerReroll;
        [Tooltip("Whether the gameplay HUD shows the rule's badge beside the button of its activation phase.")]
        [SerializeField] private bool shownOnHud;
        [Tooltip("Optional presentation the rule brings to playback, such as its activation, marked and bound dice or a disturbance; empty for none.")]
        [SerializeField] private RulePresenterAsset presenter;

        public string RuleId => ruleId;
        public string DisplayName => displayName;
        public string Description => string.IsNullOrWhiteSpace(description) ? availableText : description;
        public Sprite Icon => icon;
        public ModifierPhase ActivationPhase => activationPhase;
        public bool ShownOnHud => shownOnHud;
        public RulePresenterAsset Presenter => presenter;

        /** The card text for the rule's availability, with each {name} the rule's snapshot supplies filled in. */
        public string TextFor(EncounterRuleSnapshot rule)
        {
            string text = rule.IsAvailable ? availableText : usedText;
            if (text == null) return null;
            foreach (var value in rule.TextValues)
                text = text.Replace("{" + value.Key + "}", value.Value.ToString(CultureInfo.InvariantCulture));
            return text;
        }

        /** The card among the cards that presents the rule, or null when none does. */
        public static EncounterRulePresentationAsset For(IReadOnlyList<EncounterRulePresentationAsset> cards, string ruleId)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            foreach (var card in cards)
                if (card != null && string.Equals(card.ruleId, ruleId, StringComparison.Ordinal)) return card;
            return null;
        }
    }
}
