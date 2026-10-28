using System;

namespace Dice.Engine
{
    public sealed class SumLessThanCondition : IModifierCondition
    {
        public int Parameter { get; }

        public SumLessThanCondition(int parameter)
        {
            if (parameter < 0) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return dice.Sum < Parameter;
        }
    }
}
