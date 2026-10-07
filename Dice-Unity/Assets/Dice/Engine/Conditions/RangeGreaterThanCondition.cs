using System;

namespace Dice.Engine
{
    public sealed class RangeGreaterThanCondition : IModifierCondition
    {
        public int Parameter { get; }

        public RangeGreaterThanCondition(int parameter = 3)
        {
            parameter = parameter == 0 ? 3 : parameter;
            if (parameter < 0) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.Range(dice) > Parameter;
        }
    }
}
