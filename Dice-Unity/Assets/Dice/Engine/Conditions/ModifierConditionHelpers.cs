using System;

namespace Dice.Engine
{
    /**
     * Reads the current hand for conditions. The quantities over the hand (counts, sums, extremes, groups, parity, rank)
     * see only its counted dice, and a condition that names positions asks HandDice.Counts first, since a die that does not
     * count has no face to read.
     */
    internal static class ModifierConditionHelpers
    {
        /** The first and last dice. */
        internal const int EdgeDice = 1 << 0 | 1 << 4;
        /** The three dice between the edges. */
        internal const int InteriorDice = 1 << 1 | 1 << 2 | 1 << 3;
        internal const int MiddleDie = 1 << 2;
        internal const int SecondAndFourthDice = 1 << 1 | 1 << 3;

        internal static int Range(in ModifierContext context) => context.CurrentHand.Maximum - context.CurrentHand.Minimum;

        internal static int LargestGroup(in ModifierContext context)
        {
            HandDice hand = context.CurrentHand;
            int largestGroup = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
                if (hand.Counts(1 << i)) largestGroup = Math.Max(largestGroup, hand.CountOf(hand.Faces[i]));
            return largestGroup;
        }

        internal static int OddCount(in ModifierContext context)
        {
            HandDice hand = context.CurrentHand;
            int oddCount = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
                if (hand.Counts(1 << i) && (hand.Faces[i] & 1) != 0) ++oddCount;
            return oddCount;
        }

        internal static int OddSum(in ModifierContext context)
        {
            HandDice hand = context.CurrentHand;
            int oddSum = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
                if (hand.Counts(1 << i) && (hand.Faces[i] & 1) != 0) oddSum += hand.Faces[i];
            return oddSum;
        }

        /** Compares two hands' ranks, each over the dice that counted when it stood. */
        internal static bool CompareRank(HandDice hand, HandDice? other, int comparison, bool initialRankWhenMissing = false)
        {
            if (!other.HasValue && !initialRankWhenMissing) return false;
            // The initial previous-hand comparison rank is HighDie.
            HandCategory otherRank = other.HasValue ? HandEvaluator.Evaluate(other.Value) : HandCategory.HighDie;
            return ((int)HandEvaluator.Evaluate(hand)).CompareTo((int)otherRank) == comparison;
        }

        /** Whether every pair of neighbouring dice differs by an amount in the range; false when any die does not count. */
        internal static bool AdjacentDifferences(in ModifierContext context, int minimum, int maximum)
        {
            if (!context.CurrentHand.Counts(DiceValues.AllMask)) return false;
            DiceValues dice = context.CurrentHand.Faces;
            for (int i = 1; i < DiceValues.Count; ++i)
            {
                int difference = Math.Abs(dice[i - 1] - dice[i]);
                if (difference < minimum || difference > maximum) return false;
            }
            return true;
        }
    }
}
