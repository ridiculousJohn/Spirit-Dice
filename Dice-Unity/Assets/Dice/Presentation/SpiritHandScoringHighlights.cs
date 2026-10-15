using System;
using Dice.Engine;

namespace Dice.Presentation
{
    public static class SpiritHandScoringHighlights
    {
        public static int GetMask(DiceValues dice, HandCategory category)
        {
            if (!dice.IsValid) throw new ArgumentException("Five valid die faces are required.", nameof(dice));
            if ((uint)category >= HandDefinition.CategoryCount) throw new ArgumentOutOfRangeException(nameof(category));
            if (category == HandCategory.FullHouse || category == HandCategory.Straight
                || category == HandCategory.FiveOfAKind) return DiceValues.AllMask;

            if (category == HandCategory.HighDie)
            {
                int highestIndex = 0;
                for (int index = 1; index < DiceValues.Count; ++index)
                    if (dice[index] > dice[highestIndex]) highestIndex = index;
                return 1 << highestIndex;
            }

            var counts = new int[7];
            for (int index = 0; index < DiceValues.Count; ++index) ++counts[dice[index]];
            int matchingCount = category == HandCategory.ThreeOfAKind ? 3
                : category == HandCategory.FourOfAKind ? 4 : 2;
            int mask = 0;
            for (int index = 0; index < DiceValues.Count; ++index)
                if (counts[dice[index]] == matchingCount) mask |= 1 << index;
            return mask;
        }
    }
}
