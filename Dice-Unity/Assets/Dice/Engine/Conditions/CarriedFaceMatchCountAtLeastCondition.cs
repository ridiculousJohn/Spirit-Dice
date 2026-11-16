using System;
using System.Collections.Generic;

namespace Dice.Engine
{
    public sealed class CarriedFaceMatchCountAtLeastCondition : IModifierCondition
    {
        public int Parameter { get; }

        /** A carried die has at most four other dice to match it. */
        public CarriedFaceMatchCountAtLeastCondition(int parameter)
        {
            if (parameter < 1 || parameter > DiceValues.Count - 1) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool CanEverActivateWith(IReadOnlyList<EncounterRuleDefinition> rulesInPlay) => CarriedDice.RuleInPlay(rulesInPlay);

        /**
         * Holds when, for some carried die, at least Parameter other counted dice show its face. A carried die that does not
         * count has no face, so it matches nothing.
         */
        public bool Evaluate(in ModifierContext context)
        {
            int carriedMask = CarriedDice.Mask(context);
            for (int bay = 0; bay < DiceValues.Count; ++bay)
                if ((carriedMask & (1 << bay)) != 0 && context.CurrentHand.Counts(1 << bay) &&
                    CarriedDice.OtherMatches(context.CurrentHand, bay) >= Parameter) return true;
            return false;
        }
    }
}
