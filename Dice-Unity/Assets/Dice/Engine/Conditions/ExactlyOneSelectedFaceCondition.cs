using System;

namespace Dice.Engine
{
    public sealed class ExactlyOneSelectedFaceCondition : IModifierCondition
    {
        public int Parameter { get; }

        public ExactlyOneSelectedFaceCondition(int parameter)
        {
            if (!DiceValues.IsFace(parameter)) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            return context.CurrentHand.CountOf(Parameter, context.SelectedMask) == 1;
        }
    }
}
