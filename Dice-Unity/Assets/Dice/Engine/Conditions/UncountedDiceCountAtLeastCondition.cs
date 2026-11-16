using System;
using System.Collections.Generic;

namespace Dice.Engine
{
    public sealed class UncountedDiceCountAtLeastCondition : IModifierCondition
    {
        public int Parameter { get; }

        /** A hand counts at least one die, so at most all but one can be uncounted. */
        public UncountedDiceCountAtLeastCondition(int parameter)
        {
            if (parameter < 1 || parameter > DiceValues.Count - 1) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        /** Every die counts unless a rule in play can leave dice uncounted. */
        public bool CanEverActivateWith(IReadOnlyList<EncounterRuleDefinition> rulesInPlay)
        {
            foreach (var rule in rulesInPlay)
                if (rule.CanLeaveDiceUncounted) return true;
            return false;
        }

        /** Counts the dice of the hand that do not count toward it, whichever rule reports them. */
        public bool Evaluate(in ModifierContext context)
            => DiceValues.Count - context.CurrentHand.CountedCount >= Parameter;
    }
}
