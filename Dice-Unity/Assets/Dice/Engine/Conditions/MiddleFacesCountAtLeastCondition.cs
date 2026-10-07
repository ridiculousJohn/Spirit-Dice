using System;

namespace Dice.Engine
{
    public sealed class MiddleFacesCountAtLeastCondition : IModifierCondition
    {
        public int Parameter { get; }

        public MiddleFacesCountAtLeastCondition(int parameter = 4)
        {
            parameter = parameter == 0 ? 4 : parameter;
            if (parameter < 1 || parameter > DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.CountOf(dice, 3) + ModifierConditionHelpers.CountOf(dice, 4) >= Parameter;
        }
    }
}
