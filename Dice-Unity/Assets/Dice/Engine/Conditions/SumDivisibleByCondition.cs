using System;

namespace Dice.Engine
{
    public sealed class SumDivisibleByCondition : IModifierCondition
    {
        public int Parameter { get; }

        public SumDivisibleByCondition(int parameter = 5)
        {
            parameter = parameter == 0 ? 5 : parameter;
            if (parameter < 1) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return dice.Sum % Parameter == 0;
        }
    }
}
