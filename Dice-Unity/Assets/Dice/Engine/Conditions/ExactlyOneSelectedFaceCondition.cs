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
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.SelectedCountOf(dice, context.SelectedMask, Parameter) == 1;
        }
    }
}
