using System;

namespace Dice.Engine
{
    public sealed class SelectedMatchingGroupAtLeastCondition : IModifierCondition
    {
        public int Parameter { get; }

        public SelectedMatchingGroupAtLeastCondition(int parameter = 3)
        {
            parameter = parameter == 0 ? 3 : parameter;
            if (parameter < 1 || parameter > DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            for (int face = 1; face <= 6; ++face)
                if (ModifierConditionHelpers.SelectedCountOf(dice, context.SelectedMask, face) >= Parameter) return true;
            return false;
        }
    }
}
