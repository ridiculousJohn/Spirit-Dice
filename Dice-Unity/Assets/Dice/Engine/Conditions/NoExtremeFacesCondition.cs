using System;

namespace Dice.Engine
{
    public sealed class NoExtremeFacesCondition : IModifierCondition
    {
        public int Parameter { get; }
        public int SecondaryParameter { get; }

        public NoExtremeFacesCondition(int parameter, int secondaryParameter)
        {
            if (parameter < 1 || parameter > 6 || secondaryParameter < 1 || secondaryParameter > 6) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
            SecondaryParameter = secondaryParameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            return ModifierConditionHelpers.CountOf(dice, Parameter) == 0 && ModifierConditionHelpers.CountOf(dice, SecondaryParameter) == 0;
        }
    }
}
