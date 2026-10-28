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
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return (dice[0] + dice[4]) == Parameter || (dice[0] + dice[4]) == SecondaryParameter;
        }
    }
}
