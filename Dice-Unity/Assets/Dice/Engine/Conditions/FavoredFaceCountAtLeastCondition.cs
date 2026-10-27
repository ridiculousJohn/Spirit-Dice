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

        /** Reads the first favored-face rule; without one the condition is false. */
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            foreach (var rule in context.EncounterRules)
                if (rule is FavoredFaceRuleSnapshot favored)
                    return ModifierConditionHelpers.CountOf(context.CurrentDice, favored.FavoredFace) >= Parameter;
            return false;
        }
    }
}
