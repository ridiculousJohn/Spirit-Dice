using System;

namespace Dice.Engine
{
    internal static class ModifierConditionHelpers
    {
        internal static void ValidateContext(in ModifierContext context)
        {
            if (!context.CurrentDice.IsValid || !context.InitialDice.IsValid)
                throw new ArgumentException("A valid modifier context is required.", nameof(context));
        }

        internal static int CountOf(DiceValues dice, int face)
        {
            int count = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
                if (dice[i] == face) ++count;
            return count;
        }

        internal static int SelectedCountOf(DiceValues dice, int selectedMask, int face)
        {
            int count = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
                if ((selectedMask & (1 << i)) != 0 && dice[i] == face) ++count;
            return count;
        }

        internal static int Minimum(DiceValues dice)
        {
            int minimum = dice[0];
            for (int i = 1; i < DiceValues.Count; ++i) minimum = Math.Min(minimum, dice[i]);
            return minimum;
        }

        internal static int Maximum(DiceValues dice)
        {
            int maximum = dice[0];
            for (int i = 1; i < DiceValues.Count; ++i) maximum = Math.Max(maximum, dice[i]);
            return maximum;
        }

        internal static int Range(DiceValues dice)
        {
            int minimum = dice[0], maximum = dice[0];
            for (int i = 1; i < DiceValues.Count; ++i)
            {
                minimum = Math.Min(minimum, dice[i]);
                maximum = Math.Max(maximum, dice[i]);
            }
            return maximum - minimum;
        }

        internal static int LargestGroup(DiceValues dice)
        {
            int largestGroup = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
                largestGroup = Math.Max(largestGroup, CountOf(dice, dice[i]));
            return largestGroup;
        }

        internal static int OddCount(DiceValues dice)
        {
            int oddCount = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
                if ((dice[i] & 1) != 0) ++oddCount;
            return oddCount;
        }

        internal static int OddSum(DiceValues dice)
        {
            int oddSum = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
                if ((dice[i] & 1) != 0) oddSum += dice[i];
            return oddSum;
        }

        internal static bool CompareRank(DiceValues dice, DiceValues? other, int comparison, bool initialRankWhenMissing = false)
        {
            if (!other.HasValue && !initialRankWhenMissing) return false;
            // The initial previous-hand comparison rank is HighDie.
            HandCategory otherRank = other.HasValue ? HandEvaluator.Evaluate(other.Value) : HandCategory.HighDie;
            return ((int)HandEvaluator.Evaluate(dice)).CompareTo((int)otherRank) == comparison;
        }

        internal static bool AdjacentDifferences(DiceValues dice, int minimum, int maximum)
        {
            for (int i = 1; i < DiceValues.Count; ++i)
            {
                int difference = Math.Abs(dice[i - 1] - dice[i]);
                if (difference < minimum || difference > maximum) return false;
            }
            return true;
        }
    }
}
