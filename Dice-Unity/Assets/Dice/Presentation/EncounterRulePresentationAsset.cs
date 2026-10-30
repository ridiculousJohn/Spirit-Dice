using System;
using System.Collections.Generic;
using UnityEngine;
using Dice.Engine;
using Dice.UnityContent;

namespace Dice.Presentation
{
    /** The host's wording and artwork remain separate from the encounter's mechanical rule. */
    [CreateAssetMenu(menuName = "Dice/Encounter Rule Presentation")]
    public sealed class EncounterRulePresentationAsset : ScriptableObject
    {
        [Tooltip("The encounter rule this card presents; a table's cards present different rules.")]
        [SerializeField] private EncounterRuleAsset rule;
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

        /** The rule asset the card presents; null only on an invalid card, which table validation rejects. */
        public EncounterRuleAsset Rule => rule;
        /** The id of the rule the card presents; a card without a rule fails, naming the card. */
        public EncounterRuleDefinition.Id RuleId
            => rule != null ? rule.Identity : throw new InvalidOperationException("The rule card " + name + " presents no rule.");
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
            foreach (var value in rule.TextValues) text = SpiritWordingTemplate.Fill(text, value.Key, value.Value);
            return text;
        }

        /**
         * Fails, naming the card and its rule, when its available or used text has a placeholder the rule's snapshots do
         * not supply, which would show on screen as written, or when it has no description and its available text has any
         * placeholder, since the invitation describes the rule by that text with no rule snapshot to fill it.
         */
        internal void ValidateText(EncounterRuleDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            RequireSuppliedPlaceholders(availableText, "available", definition);
            RequireSuppliedPlaceholders(usedText, "used", definition);
            if (string.IsNullOrWhiteSpace(description) && !SpiritWordingTemplate.UsesOnly(availableText, Array.Empty<string>()))
                throw new FormatException("The rule card " + name + " of the rule " + (rule != null ? rule.name : "(none)") +
                    " has no description, so the invitation describes it by its available text, which has a placeholder the" +
                    " invitation cannot fill.");
        }

        private void RequireSuppliedPlaceholders(string text, string field, EncounterRuleDefinition definition)
        {
            if (SpiritWordingTemplate.UsesOnly(text, definition.TextValueNames)) return;
            string supplied = definition.TextValueNames.Count == 0 ? "none"
                : "{" + string.Join("}, {", definition.TextValueNames) + "}";
            throw new FormatException("The " + field + " text of the rule card " + name + " has a placeholder its rule " +
                (rule != null ? rule.name : "(none)") + " does not supply, or a stray brace; the rule supplies " + supplied + ".");
        }

        /** The card among the cards that presents the rule, or null when none does. */
        public static EncounterRulePresentationAsset For(IReadOnlyList<EncounterRulePresentationAsset> cards,
            EncounterRuleDefinition.Id ruleId)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            foreach (var card in cards)
                if (card != null && card.RuleId == ruleId) return card;
            return null;
        }
    }
}
