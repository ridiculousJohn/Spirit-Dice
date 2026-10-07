using System;

namespace Dice.Engine
{
    public sealed class SelectedSumEqualsEitherCondition : IModifierCondition
    {
        public int Parameter { get; }
        public int SecondaryParameter { get; }

        public SelectedSumEqualsEitherCondition(int parameter = 8, int secondaryParameter = 10)
        {
            parameter = parameter == 0 ? 8 : parameter;
            secondaryParameter = secondaryParameter == 0 ? 10 : secondaryParameter;
            if (parameter < 0 || secondaryParameter < 0) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
            SecondaryParameter = secondaryParameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            int selectedSum = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
                if ((context.SelectedMask & (1 << i)) != 0) selectedSum += dice[i];
            return context.SelectedMask != 0 &&
                (selectedSum == Parameter || selectedSum == SecondaryParameter);
        }
    }
}
