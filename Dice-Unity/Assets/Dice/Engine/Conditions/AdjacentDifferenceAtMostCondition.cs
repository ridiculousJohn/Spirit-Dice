using System;

namespace Dice.Engine
{
    public sealed class AdjacentDifferenceAtMostCondition : IModifierCondition
    {
        public int Parameter { get; }

        public AdjacentDifferenceAtMostCondition(int parameter)
        {
            if (parameter < 0) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            return ModifierConditionHelpers.AdjacentDifferences(context, 0, Parameter);
        }
    }
}
