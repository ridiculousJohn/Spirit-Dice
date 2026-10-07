using System;

namespace Dice.Engine
{
    public sealed class ThreeSmallestSumAtMostMaximumCondition : IModifierCondition
    {
        public bool Evaluate(in ModifierContext context)
        {
            ModifierConditionHelpers.ValidateContext(in context);
            DiceValues dice = context.CurrentDice;
            int first = 7, second = 7, third = 7, maximum = 1;
            for (int i = 0; i < DiceValues.Count; ++i)
            {
                int face = dice[i];
                maximum = Math.Max(maximum, face);
                if (face < first) { third = second; second = first; first = face; }
                else if (face < second) { third = second; second = face; }
                else if (face < third) third = face;
            }
            return first + second + third <= maximum;
        }
    }
}
