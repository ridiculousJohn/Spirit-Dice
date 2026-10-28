using System;

namespace Dice.Engine
{
    public sealed class ContainsFaceCondition : IModifierCondition
    {
        public int Parameter { get; }

        public ContainsFaceCondition(int parameter)
        {
            if (!DiceValues.IsFace(parameter)) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.CountOf(dice, Parameter) > 0;
        }
    }
}
