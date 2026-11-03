using System;

namespace Dice.Engine
{
    public sealed class SelectedSumEqualsEitherCondition : IModifierCondition
    {
        public int Parameter { get; }
        public int SecondaryParameter { get; }

        public SelectedSumEqualsEitherCondition(int parameter, int secondaryParameter)
        {
            if (parameter < 0 || secondaryParameter < 0) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
            SecondaryParameter = secondaryParameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            int selectedSum = context.CurrentHand.SumOf(context.SelectedMask);
            return context.SelectedMask != 0 &&
                (selectedSum == Parameter || selectedSum == SecondaryParameter);
        }
    }
}
