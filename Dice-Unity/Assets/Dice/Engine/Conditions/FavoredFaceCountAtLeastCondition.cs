using System;

namespace Dice.Engine
{
    public sealed class FavoredFaceCountAtLeastCondition : IModifierCondition
    {
        public int Parameter { get; }

        public FavoredFaceCountAtLeastCondition(int parameter = 2)
        {
            parameter = parameter == 0 ? 2 : parameter;
            if (parameter < 1 || parameter > DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(parameter));
            Parameter = parameter;
        }

        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            return context.FavoredFace.HasValue &&
                ModifierConditionHelpers.CountOf(context.CurrentDice, context.FavoredFace.Value) >= Parameter;
        }
    }
}
