using System;

namespace Dice.Engine
{
    public sealed class AdjacentDifferenceAtLeastCondition : IModifierCondition
    {
        public int Parameter { get; }

        public AdjacentDifferenceAtLeastCondition(int parameter = 2)
        {
            parameter = parameter == 0 ? 2 : parameter;
            if (parameter < 0) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.AdjacentDifferences(dice, Parameter, 5);
        }
    }
}
