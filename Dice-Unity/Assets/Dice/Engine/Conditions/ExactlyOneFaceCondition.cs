using System;

namespace Dice.Engine
{
    public sealed class ExactlyOneFaceCondition : IModifierCondition
    {
        public int Parameter { get; }

        public ExactlyOneFaceCondition(int parameter)
        {
            if (!DiceValues.IsFace(parameter)) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            return context.CurrentHand.CountOf(Parameter) == 1;
        }
    }
}
