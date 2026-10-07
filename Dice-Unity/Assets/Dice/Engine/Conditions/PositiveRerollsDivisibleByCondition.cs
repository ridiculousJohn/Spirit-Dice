using System;

namespace Dice.Engine
{
    public sealed class PositiveRerollsDivisibleByCondition : IModifierCondition
    {
        public int Parameter { get; }

        public PositiveRerollsDivisibleByCondition(int parameter = 3)
        {
            parameter = parameter == 0 ? 3 : parameter;
            if (parameter < 1) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            return context.RerollsRemaining > 0 && context.RerollsRemaining % Parameter == 0;
        }
    }
}
