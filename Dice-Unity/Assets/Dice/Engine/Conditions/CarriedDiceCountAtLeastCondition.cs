using System;
using System.Collections.Generic;

namespace Dice.Engine
{
    public sealed class CarriedDiceCountAtLeastCondition : IModifierCondition
    {
        public int Parameter { get; }

        public CarriedDiceCountAtLeastCondition(int parameter)
        {
            if (parameter < 1 || parameter > DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool CanEverActivateWith(IReadOnlyList<EncounterRuleDefinition> rulesInPlay) => CarriedDice.RuleInPlay(rulesInPlay);

        /** Counts the counted dice among those every carry-over rule has carried; a carried die that does not count is not in the hand. */
        public bool Evaluate(in ModifierContext context)
            => DiceValues.CountBays(CarriedDice.Mask(context) & context.CurrentHand.CountedMask) >= Parameter;
    }
}
