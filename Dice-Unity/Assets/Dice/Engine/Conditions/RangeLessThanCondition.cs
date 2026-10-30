using System;

namespace Dice.Engine
{
    public sealed class RangeLessThanCondition : IModifierCondition
    {
        public int Parameter { get; }

        public RangeLessThanCondition(int parameter)
        {
            if (parameter < 0) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.Range(dice) < Parameter;
        }
    }
}
