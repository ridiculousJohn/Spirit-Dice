using System;

namespace Dice.Engine
{
    public sealed class EdgeSumEqualsEitherCondition : IModifierCondition
    {
        public int Parameter { get; }
        public int SecondaryParameter { get; }

        public EdgeSumEqualsEitherCondition(int parameter, int secondaryParameter)
        {
            if (parameter < 0 || secondaryParameter < 0) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
            SecondaryParameter = secondaryParameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            if (!context.CurrentHand.Counts(ModifierConditionHelpers.EdgeDice)) return false;
            DiceValues dice = context.CurrentHand.Faces;
            return (dice[0] + dice[4]) == Parameter || (dice[0] + dice[4]) == SecondaryParameter;
        }
    }
}
