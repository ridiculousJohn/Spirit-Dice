using System;

namespace Dice.Engine
{
    public static class ModifierConditions
    {
        public static bool Evaluate(ModifierCondition condition, in ModifierContext context)
        {
            if (!context.CurrentDice.IsValid || !context.InitialDice.IsValid)
                throw new ArgumentException("A valid modifier context is required.", nameof(context));
            DiceValues dice = context.CurrentDice;
            int p = condition.Parameter;
            int q = condition.SecondaryParameter;
            int min = 6, max = 1, oddCount = 0, oddSum = 0, selectedCount = 0, selectedSum = 0, uniqueCount = 0, largestGroup = 0;
            var counts = new int[7];
            for (int i = 0; i < DiceValues.Count; ++i)
            {
                int face = dice[i];
                min = Math.Min(min, face);
                max = Math.Max(max, face);
                if ((face & 1) != 0) { ++oddCount; oddSum += face; }
                if (++counts[face] == 1) ++uniqueCount;
                largestGroup = Math.Max(largestGroup, counts[face]);
                if ((context.SelectedMask & (1 << i)) != 0) { ++selectedCount; selectedSum += face; }
            }
            int edgeSum = dice[0] + dice[4];
            long adjustedHands = (long)context.HandsRemaining + 1;
            switch (condition.Kind)
            {
                case ModifierConditionKind.Always: return true;
                case ModifierConditionKind.Never: return false;
                case ModifierConditionKind.NoDiceRerolled: return context.RerollsSpentOnHand == 0;
                case ModifierConditionKind.ContainsFace:
                case ModifierConditionKind.ContainsUpperFace: return counts[p] > 0;
                case ModifierConditionKind.ExactlyOneSelectedFace: return SelectedCountOf(dice, context.SelectedMask, p) == 1;
                case ModifierConditionKind.OutOfRerolls: return context.RerollsRemaining == 0;
                case ModifierConditionKind.RangeLessThan: return max - min < p;
                case ModifierConditionKind.SumLessThanInitial: return dice.Sum < context.InitialDice.Sum;
                case ModifierConditionKind.LastHand: return context.HandsRemaining == 1;
                case ModifierConditionKind.SelectedFaceUnchanged:
                    if (!context.BeforeRerollDice.HasValue) return false;
                    for (int i = 0; i < DiceValues.Count; ++i)
                        if ((context.SelectedMask & (1 << i)) != 0 && dice[i] == context.BeforeRerollDice.Value[i]) return true;
                    return false;
                case ModifierConditionKind.SelectedMatchingGroupAtLeast:
                    for (int face = 1; face <= 6; ++face)
                        if (SelectedCountOf(dice, context.SelectedMask, face) >= p) return true;
                    return false;
                case ModifierConditionKind.RankBelowPrevious: return CompareRank(dice, context.PreviousPlayedDice, -1, true);
                case ModifierConditionKind.RankAbovePrevious: return CompareRank(dice, context.PreviousPlayedDice, 1, true);
                case ModifierConditionKind.RankEqualsPrevious: return CompareRank(dice, context.PreviousPlayedDice, 0, true);
                case ModifierConditionKind.RankEqualsBeforeReroll: return CompareRank(dice, context.BeforeRerollDice, 0);
                case ModifierConditionKind.SumDivisibleBy: return dice.Sum % p == 0;
                case ModifierConditionKind.EdgePairSumsEqual: return dice[0] + dice[1] == dice[3] + dice[4];
                case ModifierConditionKind.Ordered:
                    bool ascending = true, descending = true;
                    for (int i = 1; i < DiceValues.Count; ++i)
                    { ascending &= dice[i - 1] <= dice[i]; descending &= dice[i - 1] >= dice[i]; }
                    return ascending || descending;
                case ModifierConditionKind.HighDie: return HandEvaluator.Evaluate(dice) == HandCategory.HighDie;
                case ModifierConditionKind.AdjacentDifferent: return AdjacentDifferences(dice, 1, 5);
                case ModifierConditionKind.AdjacentMatchingTriple:
                    for (int i = 0; i < DiceValues.Count - 2; ++i)
                        if (dice[i] == dice[i + 1] && dice[i] == dice[i + 2]) return true;
                    return false;
                case ModifierConditionKind.ExactlyOneFace: return counts[p] == 1;
                case ModifierConditionKind.ContainsRerollsRemaining:
                    return context.RerollsRemaining >= 1 && context.RerollsRemaining <= 6 && counts[context.RerollsRemaining] > 0;
                case ModifierConditionKind.SelectedMatchesHandsRemaining:
                    return SelectedCountOf(dice, context.SelectedMask, context.HandsRemaining) > 0;
                case ModifierConditionKind.AdjacentDifferenceAtLeast: return AdjacentDifferences(dice, p, 5);
                case ModifierConditionKind.PositiveRerollsDivisibleBy: return context.RerollsRemaining > 0 && context.RerollsRemaining % p == 0;
                case ModifierConditionKind.MiddleUnique: return counts[dice[2]] == 1;
                case ModifierConditionKind.HighDieOrStraight:
                    var category = HandEvaluator.Evaluate(dice);
                    return category == HandCategory.HighDie || category == HandCategory.Straight;
                case ModifierConditionKind.AlternatingParity:
                    for (int i = 1; i < DiceValues.Count; ++i) if ((dice[i - 1] & 1) == (dice[i] & 1)) return false;
                    return true;
                case ModifierConditionKind.InteriorEqualsNeighborSum:
                case ModifierConditionKind.SelectedInteriorEqualsNeighborSum:
                    for (int i = 1; i < DiceValues.Count - 1; ++i)
                        if ((condition.Kind != ModifierConditionKind.SelectedInteriorEqualsNeighborSum || (context.SelectedMask & (1 << i)) != 0) &&
                            dice[i] == dice[i - 1] + dice[i + 1]) return true;
                    return false;
                case ModifierConditionKind.EveryDieRerolled: return context.EverRerolledMask == DiceValues.AllMask;
                case ModifierConditionKind.ExactlyOneSelectedMatchesSelectedCount:
                    return SelectedCountOf(dice, context.SelectedMask, selectedCount) == 1;
                case ModifierConditionKind.MinimumEqualsRerollsSpent: return min == context.RerollsSpentOnHand;
                case ModifierConditionKind.RerollsExceedHandsRemaining: return context.RerollsRemaining > adjustedHands;
                case ModifierConditionKind.MaximumAtMostRerollsRemaining: return max <= context.RerollsRemaining;
                case ModifierConditionKind.LargestGroupEqualsHandsOrRerolls:
                    return largestGroup == adjustedHands || largestGroup == context.RerollsRemaining;
                case ModifierConditionKind.EdgeSumEqualsEither: return edgeSum == p || edgeSum == q;
                case ModifierConditionKind.FaceEqualsPosition:
                    for (int i = 0; i < DiceValues.Count; ++i) if (dice[i] == i + 1) return true;
                    return false;
                case ModifierConditionKind.FirstHand: return context.HandsPlayedInStage == 0;
                case ModifierConditionKind.ExtremeEqualsHandsRemaining: return min == adjustedHands || max == adjustedHands;
                case ModifierConditionKind.SumLessThan: return dice.Sum < p;
                case ModifierConditionKind.TwoPair: return HandEvaluator.Evaluate(dice) == HandCategory.TwoPair;
                case ModifierConditionKind.SelectedSumEqualsEither: return selectedCount > 0 && (selectedSum == p || selectedSum == q);
                case ModifierConditionKind.RerollsSpentAtLeast: return context.RerollsSpentOnHand >= p;
                case ModifierConditionKind.AdjacentDifferenceAtMost: return AdjacentDifferences(dice, 0, p);
                case ModifierConditionKind.EdgeSumGreaterThanInterior: return edgeSum > dice[1] + dice[2] + dice[3];
                case ModifierConditionKind.InitialPair: return HandEvaluator.Evaluate(context.InitialDice) == HandCategory.Pair;
                case ModifierConditionKind.InitialRankAbovePrevious: return CompareRank(context.InitialDice, context.PreviousPlayedDice, 1);
                case ModifierConditionKind.RangeGreaterThan: return max - min > p;
                case ModifierConditionKind.PreviousStageExhaustedHands:
                    return context.HandsPlayedInStage == 0 && context.PreviousStageHandsRemaining == 0;
                case ModifierConditionKind.SameParity: return oddCount == 0 || oddCount == DiceValues.Count;
                case ModifierConditionKind.InteriorSumDivisibleBy: return (dice[1] + dice[2] + dice[3]) % p == 0;
                case ModifierConditionKind.EdgeSumEqualsHandsOrRerolls: return edgeSum == adjustedHands || edgeSum == context.RerollsRemaining;
                case ModifierConditionKind.PreviousStageAtMostHalfHandsPlayed:
                    return context.HandsPlayedInStage == 0 && context.PreviousStageStartingHands.HasValue &&
                        context.PreviousStageHandsRemaining.HasValue &&
                        (long)context.PreviousStageHandsRemaining.Value * 2 >= context.PreviousStageStartingHands.Value;
                case ModifierConditionKind.LastRerollOnLastHand: return context.HandsRemaining == 1 && context.RerollsRemaining == 0;
                case ModifierConditionKind.RankBelowBeforeReroll: return CompareRank(dice, context.BeforeRerollDice, -1);
                case ModifierConditionKind.ThreeSmallestSumAtMostMaximum:
                    var sorted = dice.ToArray();
                    Array.Sort(sorted);
                    return sorted[0] + sorted[1] + sorted[2] <= sorted[4];
                case ModifierConditionKind.SecondFourthProductAtLeastSum: return dice[1] * dice[3] >= dice.Sum;
                case ModifierConditionKind.InteriorEqualsNeighborDifference:
                    for (int i = 1; i < DiceValues.Count - 1; ++i)
                        if (dice[i] == Math.Abs(dice[i - 1] - dice[i + 1])) return true;
                    return false;
                case ModifierConditionKind.MiddleEqualsOddCount: return dice[2] == oddCount;
                case ModifierConditionKind.SumGreaterThan: return dice.Sum > p;
                case ModifierConditionKind.HandsRemainingEven: return (adjustedHands & 1) == 0;
                case ModifierConditionKind.EdgePairDifferencesEqual: return Math.Abs(dice[0] - dice[1]) == Math.Abs(dice[3] - dice[4]);
                case ModifierConditionKind.InteriorContainsPair: return dice[1] == dice[2] || dice[1] == dice[3] || dice[2] == dice[3];
                case ModifierConditionKind.OddSumGreaterThanEvenSum: return oddSum > dice.Sum - oddSum;
                case ModifierConditionKind.SumPrime:
                    for (int divisor = 2; divisor * divisor <= dice.Sum; ++divisor) if (dice.Sum % divisor == 0) return false;
                    return true;
                case ModifierConditionKind.EdgesMatchInterior:
                    return dice[0] == dice[4] && (dice[0] == dice[1] || dice[0] == dice[2] || dice[0] == dice[3]);
                case ModifierConditionKind.UniqueCountLessThanRerollsSpent: return uniqueCount < context.RerollsSpentOnHand;
                case ModifierConditionKind.NoExtremeFaces: return counts[p] == 0 && counts[q] == 0;
                case ModifierConditionKind.MiddleFacesCountAtLeast: return counts[3] + counts[4] >= p;
                case ModifierConditionKind.MiddleHand: return context.HandsRemaining == (context.StartingHands + 1L) / 2;
                case ModifierConditionKind.InteriorPairMatchesEdge:
                    int firstEdgeCount = 0, lastEdgeCount = 0;
                    for (int i = 1; i < DiceValues.Count - 1; ++i)
                    { if (dice[i] == dice[0]) ++firstEdgeCount; if (dice[i] == dice[4]) ++lastEdgeCount; }
                    return firstEdgeCount >= 2 || lastEdgeCount >= 2;
                default: throw new ArgumentOutOfRangeException(nameof(condition));
            }
        }

        private static int SelectedCountOf(DiceValues dice, int selectedMask, int face)
        {
            int count = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
                if ((selectedMask & (1 << i)) != 0 && dice[i] == face) ++count;
            return count;
        }

        private static bool CompareRank(DiceValues dice, DiceValues? other, int comparison, bool initialRankWhenMissing = false)
        {
            // The initial previous-hand comparison rank is HighDie.
            if (!other.HasValue && initialRankWhenMissing) other = new DiceValues(1, 2, 3, 4, 6);
            if (!other.HasValue) return false;
            int difference = ((int)HandEvaluator.Evaluate(dice)).CompareTo((int)HandEvaluator.Evaluate(other.Value));
            return difference == comparison;
        }

        private static bool AdjacentDifferences(DiceValues dice, int minimum, int maximum)
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
