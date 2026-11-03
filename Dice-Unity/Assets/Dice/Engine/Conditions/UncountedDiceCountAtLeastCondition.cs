using System;

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

        /** Counts the dice of the hand that do not count toward it, whichever rule reports them. */
        public bool Evaluate(in ModifierContext context)
            => DiceValues.Count - context.CurrentHand.CountedCount >= Parameter;
    }
}
