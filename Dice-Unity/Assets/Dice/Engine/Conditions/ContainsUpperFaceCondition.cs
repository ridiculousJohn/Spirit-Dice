using System;

namespace Dice.Engine
{
    public sealed class ContainsUpperFaceCondition : IModifierCondition
    {
        public int Parameter { get; }

        public ContainsUpperFaceCondition(int parameter = 6)
        {
            parameter = parameter == 0 ? 6 : parameter;
            if (parameter < 1 || parameter > 6) throw new ArgumentOutOfRangeException(nameof(parameter));
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
