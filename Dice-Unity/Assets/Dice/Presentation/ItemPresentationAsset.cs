using System;
using System.Collections.Generic;
using Dice.Engine;
using Dice.UnityContent;
using UnityEngine;

namespace Dice.Presentation
{
    /** How an item shows: the rule cards that present exactly the rules the item provides. The item skin lists it. */
    [CreateAssetMenu(menuName = "Dice/Item Presentation")]
    public sealed class ItemPresentationAsset : ScriptableObject
    {
        [Tooltip("The item this presentation presents.")]
        [SerializeField] private ItemAsset item;
        [Tooltip("One card for each rule the item provides; they show wherever the item is in play, beside the table's own cards.")]
        [SerializeField] private EncounterRulePresentationAsset[] ruleCards = Array.Empty<EncounterRulePresentationAsset>();

        public ItemAsset Item => item;
        public IReadOnlyList<EncounterRulePresentationAsset> RuleCards => ruleCards;

        /**
         * Fails, naming the presentation, unless its cards present exactly the rules of its item's definition, each once,
         * and each card's text uses only the placeholders its rule supplies.
         */
        internal void Validate(ItemDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (ruleCards == null) throw new InvalidOperationException("The item presentation " + name + " has no rule card list.");
            var presented = new HashSet<EncounterRuleDefinition.Id>();
            foreach (var card in ruleCards)
            {
                if (card == null) throw new InvalidOperationException("The item presentation " + name + " has a missing rule card.");
                if (card.Rule == null)
                    throw new InvalidOperationException("The rule card " + card.name + " of the item presentation " + name + " presents no rule.");
                if (!presented.Add(card.RuleId))
                    throw new InvalidOperationException("The item presentation " + name + " has more than one card for the rule " +
                        card.Rule.name + ".");
                EncounterRuleDefinition rule = null;
                foreach (var candidate in definition.Rules)
                    if (candidate.Identity == card.RuleId) rule = candidate;
                if (rule == null)
                    throw new InvalidOperationException("The rule card " + card.name + " of the item presentation " + name +
                        " presents the rule " + card.Rule.name + ", which the item " + item.name + " does not provide.");
                try { card.ValidateText(rule); }
                catch (FormatException exception)
                {
                    throw new InvalidOperationException("The item presentation " + name + " is invalid: " + exception.Message, exception);
                }
            }
            if (presented.Count != definition.Rules.Count)
                throw new InvalidOperationException("The item presentation " + name + " has no card for some rule of the item " +
                    item.name + ".");
        }
    }
}
