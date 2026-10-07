using System;

namespace Dice.Engine
{
    public sealed class InteriorSumDivisibleByCondition : IModifierCondition
    {
        public int Parameter { get; }

        public InteriorSumDivisibleByCondition(int parameter = 3)
        {
            parameter = parameter == 0 ? 3 : parameter;
            if (parameter < 1) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return (dice[1] + dice[2] + dice[3]) % Parameter == 0;
        }
    }
}
