using System;

namespace Dice.Engine
{
    public sealed class SumGreaterThanCondition : IModifierCondition
    {
        public int Parameter { get; }

        public SumGreaterThanCondition(int parameter)
        {
            if (parameter < 0) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            DiceValues dice = context.CurrentDice;
            return dice.Sum > Parameter;
        }
    }
}
