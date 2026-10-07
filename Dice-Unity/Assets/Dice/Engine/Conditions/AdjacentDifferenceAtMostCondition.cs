using System;

namespace Dice.Engine
{
    public sealed class AdjacentDifferenceAtMostCondition : IModifierCondition
    {
        public int Parameter { get; }

        public AdjacentDifferenceAtMostCondition(int parameter = 1)
        {
            parameter = parameter == 0 ? 1 : parameter;
            if (parameter < 0) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.AdjacentDifferences(dice, 0, Parameter);
        }
    }
}
