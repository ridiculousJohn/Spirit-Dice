using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class ModifierConditionTests
    {
        [TestCaseSource(nameof(Cases))]
        public void ConditionsMatchIndependentBoundaryWitnesses(ModifierCondition condition, ModifierContext context, bool expected)
            => Assert.That(ModifierConditions.Evaluate(condition, in context), Is.EqualTo(expected));

        [Test]
        public void WitnessesCoverEveryConditionKind()
        {
            var covered = Cases().Select(value => ((ModifierCondition)value.Arguments[0]).Kind).Distinct();
            Assert.That(covered, Is.EquivalentTo(Enum.GetValues(typeof(ModifierConditionKind))));
        }

        private static IEnumerable<TestCaseData> Cases()
        {
            yield return Case(ModifierConditionKind.NoDiceRerolled, C(), true);
            yield return Case(ModifierConditionKind.NoDiceRerolled, C(ever: 1, spent: 1), false);
            yield return Case(ModifierConditionKind.ContainsFace, C(), true, 1);
            yield return Case(ModifierConditionKind.ContainsFace, C("23456"), false, 1);
            yield return Case(ModifierConditionKind.ExactlyOneSelectedFace, C(selected: 1), true, 1);
            yield return Case(ModifierConditionKind.ExactlyOneSelectedFace, C("11345", selected: 3), false, 1);
            yield return Case(ModifierConditionKind.OutOfRerolls, C(rerolls: 0), true);
            yield return Case(ModifierConditionKind.OutOfRerolls, C(rerolls: 1), false);
            yield return Case(ModifierConditionKind.RangeLessThan, C("22334"), true, 3);
            yield return Case(ModifierConditionKind.RangeLessThan, C("22335"), false, 3);
            yield return Case(ModifierConditionKind.SumLessThanInitial, C("11111", initial: "11112"), true);
            yield return Case(ModifierConditionKind.SumLessThanInitial, C("11112", initial: "11112"), false);
            yield return Case(ModifierConditionKind.LastHand, C(hands: 1), true);
            yield return Case(ModifierConditionKind.LastHand, C(hands: 2), false);
            yield return Case(ModifierConditionKind.SelectedFaceUnchanged, C(before: "12346", selected: 1), true);
            yield return Case(ModifierConditionKind.SelectedFaceUnchanged, C(before: "12346", selected: 16), false);
            yield return Case(ModifierConditionKind.SelectedFaceUnchanged, C(before: "12345", selected: 0), false);
            yield return Case(ModifierConditionKind.SelectedMatchingGroupAtLeast, C("22245", selected: 7), true, 3);
            yield return Case(ModifierConditionKind.SelectedMatchingGroupAtLeast, C("22245", selected: 3), false, 3);
            yield return Case(ModifierConditionKind.ContainsUpperFace, C("12346"), true, 6);
            yield return Case(ModifierConditionKind.ContainsUpperFace, C(), false, 6);
            yield return Case(ModifierConditionKind.RankBelowPrevious, C("12346", previous: "11234"), true);
            yield return Case(ModifierConditionKind.RankBelowPrevious, C("11234", previous: "12346"), false);
            yield return Case(ModifierConditionKind.RankAbovePrevious, C("11234", previous: "12346"), true);
            yield return Case(ModifierConditionKind.RankAbovePrevious, C("12346", previous: "11234"), false);
            yield return Case(ModifierConditionKind.RankEqualsPrevious, C("11234", previous: "22345"), true);
            yield return Case(ModifierConditionKind.RankEqualsPrevious, C("12346", previous: "22345"), false);
            yield return Case(ModifierConditionKind.RankEqualsPrevious, C("12346"), true);
            yield return Case(ModifierConditionKind.RankEqualsBeforeReroll, C("11234", before: "22345"), true);
            yield return Case(ModifierConditionKind.RankEqualsBeforeReroll, C("12346", before: "22345"), false);
            yield return Case(ModifierConditionKind.RankEqualsBeforeReroll, C(), false);
            yield return Case(ModifierConditionKind.SumDivisibleBy, C(), true, 5);
            yield return Case(ModifierConditionKind.SumDivisibleBy, C("12346"), false, 5);
            yield return Case(ModifierConditionKind.EdgePairSumsEqual, C("12312"), true);
            yield return Case(ModifierConditionKind.EdgePairSumsEqual, C("12313"), false);
            yield return Case(ModifierConditionKind.Ordered, C("11234"), true);
            yield return Case(ModifierConditionKind.Ordered, C("54321"), true);
            yield return Case(ModifierConditionKind.Ordered, C("33333"), true);
            yield return Case(ModifierConditionKind.Ordered, C("12134"), false);
            yield return Case(ModifierConditionKind.HighDie, C("12346"), true);
            yield return Case(ModifierConditionKind.HighDie, C("11234"), false);
            yield return Case(ModifierConditionKind.AdjacentDifferent, C("12121"), true);
            yield return Case(ModifierConditionKind.AdjacentDifferent, C("11234"), false);
            yield return Case(ModifierConditionKind.AdjacentMatchingTriple, C("22234"), true);
            yield return Case(ModifierConditionKind.AdjacentMatchingTriple, C("22123"), false);
            yield return Case(ModifierConditionKind.ExactlyOneFace, C(), true, 2);
            yield return Case(ModifierConditionKind.ExactlyOneFace, C("12245"), false, 2);
            yield return Case(ModifierConditionKind.ContainsRerollsRemaining, C(rerolls: 3), true);
            yield return Case(ModifierConditionKind.ContainsRerollsRemaining, C(rerolls: 6), false);
            yield return Case(ModifierConditionKind.SelectedMatchesHandsRemaining, C(selected: 4, hands: 3), true);
            yield return Case(ModifierConditionKind.SelectedMatchesHandsRemaining, C(selected: 1, hands: 3), false);
            yield return Case(ModifierConditionKind.AdjacentDifferenceAtLeast, C("13531"), true, 2);
            yield return Case(ModifierConditionKind.AdjacentDifferenceAtLeast, C("13532"), false, 2);
            yield return Case(ModifierConditionKind.PositiveRerollsDivisibleBy, C(rerolls: 3), true, 3);
            yield return Case(ModifierConditionKind.PositiveRerollsDivisibleBy, C(rerolls: 0), false, 3);
            yield return Case(ModifierConditionKind.PositiveRerollsDivisibleBy, C(rerolls: 4), false, 3);
            yield return Case(ModifierConditionKind.MiddleUnique, C(), true);
            yield return Case(ModifierConditionKind.MiddleUnique, C("13345"), false);
            yield return Case(ModifierConditionKind.HighDieOrStraight, C("12346"), true);
            yield return Case(ModifierConditionKind.HighDieOrStraight, C(), true);
            yield return Case(ModifierConditionKind.HighDieOrStraight, C("11234"), false);
            yield return Case(ModifierConditionKind.AlternatingParity, C("12121"), true);
            yield return Case(ModifierConditionKind.AlternatingParity, C("12221"), false);
            yield return Case(ModifierConditionKind.InteriorEqualsNeighborSum, C("13245"), true);
            yield return Case(ModifierConditionKind.InteriorEqualsNeighborSum, C(), false);
            yield return Case(ModifierConditionKind.SelectedInteriorEqualsNeighborSum, C("13245", selected: 2), true);
            yield return Case(ModifierConditionKind.SelectedInteriorEqualsNeighborSum, C("13245", selected: 1), false);
            yield return Case(ModifierConditionKind.EveryDieRerolled, C(ever: 31), true);
            yield return Case(ModifierConditionKind.EveryDieRerolled, C(ever: 30), false);
            yield return Case(ModifierConditionKind.ExactlyOneSelectedMatchesSelectedCount, C(selected: 3), true);
            yield return Case(ModifierConditionKind.ExactlyOneSelectedMatchesSelectedCount, C("22345", selected: 3), false);
            yield return Case(ModifierConditionKind.MinimumEqualsRerollsSpent, C("22345", spent: 2), true);
            yield return Case(ModifierConditionKind.MinimumEqualsRerollsSpent, C("22345", spent: 1), false);
            yield return Case(ModifierConditionKind.RerollsExceedHandsRemaining, C(hands: 2, rerolls: 4), true);
            yield return Case(ModifierConditionKind.RerollsExceedHandsRemaining, C(hands: 2, rerolls: 3), false);
            yield return Case(ModifierConditionKind.MaximumAtMostRerollsRemaining, C(rerolls: 5), true);
            yield return Case(ModifierConditionKind.MaximumAtMostRerollsRemaining, C(rerolls: 4), false);
            yield return Case(ModifierConditionKind.LargestGroupEqualsHandsOrRerolls, C("11234", hands: 1, rerolls: 4), true);
            yield return Case(ModifierConditionKind.LargestGroupEqualsHandsOrRerolls, C("11234", hands: 2, rerolls: 4), false);
            yield return Case(ModifierConditionKind.EdgeSumEqualsEither, C(), true, 6, 8);
            yield return Case(ModifierConditionKind.EdgeSumEqualsEither, C("22346"), true, 6, 8);
            yield return Case(ModifierConditionKind.EdgeSumEqualsEither, C("12346"), false, 6, 8);
            yield return Case(ModifierConditionKind.FaceEqualsPosition, C("22345"), true);
            yield return Case(ModifierConditionKind.FaceEqualsPosition, C("23456"), false);
            yield return Case(ModifierConditionKind.FirstHand, C(played: 0), true);
            yield return Case(ModifierConditionKind.FirstHand, C(played: 1), false);
            yield return Case(ModifierConditionKind.ExtremeEqualsHandsRemaining, C(hands: 0), true);
            yield return Case(ModifierConditionKind.ExtremeEqualsHandsRemaining, C("22346", hands: 2), false);
            yield return Case(ModifierConditionKind.SumLessThan, C("11116"), true, 15);
            yield return Case(ModifierConditionKind.SumLessThan, C(), false, 15);
            yield return Case(ModifierConditionKind.TwoPair, C("11223"), true);
            yield return Case(ModifierConditionKind.TwoPair, C("11234"), false);
            yield return Case(ModifierConditionKind.SelectedSumEqualsEither, C("23456", selected: 10), true, 8, 10);
            yield return Case(ModifierConditionKind.SelectedSumEqualsEither, C("23456", selected: 20), true, 8, 10);
            yield return Case(ModifierConditionKind.SelectedSumEqualsEither, C("23456", selected: 12), false, 8, 10);
            yield return Case(ModifierConditionKind.RerollsSpentAtLeast, C(spent: 3), true, 3);
            yield return Case(ModifierConditionKind.RerollsSpentAtLeast, C(spent: 2), false, 3);
            yield return Case(ModifierConditionKind.AdjacentDifferenceAtMost, C("11234"), true, 1);
            yield return Case(ModifierConditionKind.AdjacentDifferenceAtMost, C("11345"), false, 1);
            yield return Case(ModifierConditionKind.EdgeSumGreaterThanInterior, C("61116"), true);
            yield return Case(ModifierConditionKind.EdgeSumGreaterThanInterior, C("11112"), false);
            yield return Case(ModifierConditionKind.InitialPair, C(initial: "11234"), true);
            yield return Case(ModifierConditionKind.InitialPair, C(initial: "12345"), false);
            yield return Case(ModifierConditionKind.InitialRankAbovePrevious, C(initial: "11234", previous: "12346"), true);
            yield return Case(ModifierConditionKind.InitialRankAbovePrevious, C(initial: "12346", previous: "11234"), false);
            yield return Case(ModifierConditionKind.InitialRankAbovePrevious, C(initial: "11234"), false);
            yield return Case(ModifierConditionKind.RangeGreaterThan, C("22346"), true, 3);
            yield return Case(ModifierConditionKind.RangeGreaterThan, C("22345"), false, 3);
            yield return Case(ModifierConditionKind.PreviousStageExhaustedHands, C(priorStarting: 3, priorRemaining: 0), true);
            yield return Case(ModifierConditionKind.PreviousStageExhaustedHands, C(priorStarting: 3, priorRemaining: 1), false);
            yield return Case(ModifierConditionKind.PreviousStageExhaustedHands, C(), false);
            yield return Case(ModifierConditionKind.SameParity, C("13513"), true);
            yield return Case(ModifierConditionKind.SameParity, C("24624"), true);
            yield return Case(ModifierConditionKind.SameParity, C(), false);
            yield return Case(ModifierConditionKind.InteriorSumDivisibleBy, C("11231"), true, 3);
            yield return Case(ModifierConditionKind.InteriorSumDivisibleBy, C("11331"), false, 3);
            yield return Case(ModifierConditionKind.EdgeSumEqualsHandsOrRerolls, C(hands: 5, rerolls: 4), true);
            yield return Case(ModifierConditionKind.EdgeSumEqualsHandsOrRerolls, C(hands: 4, rerolls: 4), false);
            yield return Case(ModifierConditionKind.PreviousStageAtMostHalfHandsPlayed, C(priorStarting: 3, priorRemaining: 2), true);
            yield return Case(ModifierConditionKind.PreviousStageAtMostHalfHandsPlayed, C(priorStarting: 3, priorRemaining: 1), false);
            yield return Case(ModifierConditionKind.PreviousStageAtMostHalfHandsPlayed, C(priorStarting: 4, priorRemaining: 2), true);
            yield return Case(ModifierConditionKind.PreviousStageAtMostHalfHandsPlayed, C(), false);
            yield return Case(ModifierConditionKind.LastRerollOnLastHand, C(hands: 1, rerolls: 0), true);
            yield return Case(ModifierConditionKind.LastRerollOnLastHand, C(hands: 2, rerolls: 0), false);
            yield return Case(ModifierConditionKind.LastRerollOnLastHand, C(hands: 1, rerolls: 1), false);
            yield return Case(ModifierConditionKind.RankBelowBeforeReroll, C("12346", before: "11234"), true);
            yield return Case(ModifierConditionKind.RankBelowBeforeReroll, C("11234", before: "12346"), false);
            yield return Case(ModifierConditionKind.RankBelowBeforeReroll, C(), false);
            yield return Case(ModifierConditionKind.Never, C(), false);
            yield return Case(ModifierConditionKind.ThreeSmallestSumAtMostMaximum, C("11244"), true);
            yield return Case(ModifierConditionKind.ThreeSmallestSumAtMostMaximum, C("11233"), false);
            yield return Case(ModifierConditionKind.SecondFourthProductAtLeastSum, C("13131"), true);
            yield return Case(ModifierConditionKind.SecondFourthProductAtLeastSum, C("11331"), false);
            yield return Case(ModifierConditionKind.InteriorEqualsNeighborDifference, C("31245"), true);
            yield return Case(ModifierConditionKind.InteriorEqualsNeighborDifference, C("33333"), false);
            yield return Case(ModifierConditionKind.MiddleEqualsOddCount, C("22122"), true);
            yield return Case(ModifierConditionKind.MiddleEqualsOddCount, C("22222"), false);
            yield return Case(ModifierConditionKind.SumGreaterThan, C("66661"), true, 22);
            yield return Case(ModifierConditionKind.SumGreaterThan, C("66622"), false, 22);
            yield return Case(ModifierConditionKind.HandsRemainingEven, C(hands: 0), false);
            yield return Case(ModifierConditionKind.HandsRemainingEven, C(hands: 1), true);
            yield return Case(ModifierConditionKind.HandsRemainingEven, C(hands: 2), false);
            yield return Case(ModifierConditionKind.EdgePairDifferencesEqual, C("21345"), true);
            yield return Case(ModifierConditionKind.MiddleHand, C(hands: 2, starting: 4), true);
            yield return Case(ModifierConditionKind.MiddleHand, C(hands: 3, starting: 5), true);
            yield return Case(ModifierConditionKind.EdgePairDifferencesEqual, C(), true);
            yield return Case(ModifierConditionKind.EdgePairDifferencesEqual, C("12346"), false);
            yield return Case(ModifierConditionKind.InteriorContainsPair, C("12234"), true);
            yield return Case(ModifierConditionKind.InteriorContainsPair, C(), false);
            yield return Case(ModifierConditionKind.OddSumGreaterThanEvenSum, C("11345"), true);
            yield return Case(ModifierConditionKind.OddSumGreaterThanEvenSum, C("22466"), false);
            yield return Case(ModifierConditionKind.SumPrime, C("11113"), true);
            yield return Case(ModifierConditionKind.SumPrime, C("11112"), false);
            yield return Case(ModifierConditionKind.EdgesMatchInterior, C("11121"), true);
            yield return Case(ModifierConditionKind.EdgesMatchInterior, C("12341"), false);
            yield return Case(ModifierConditionKind.UniqueCountLessThanRerollsSpent, C("11223", spent: 4), true);
            yield return Case(ModifierConditionKind.UniqueCountLessThanRerollsSpent, C("11223", spent: 3), false);
            yield return Case(ModifierConditionKind.NoExtremeFaces, C("22345"), true);
            yield return Case(ModifierConditionKind.NoExtremeFaces, C(), false);
            yield return Case(ModifierConditionKind.MiddleFacesCountAtLeast, C("33441"), true, 4);
            yield return Case(ModifierConditionKind.MiddleFacesCountAtLeast, C("33411"), false, 4);
            yield return Case(ModifierConditionKind.MiddleHand, C(hands: 2, starting: 3), true);
            yield return Case(ModifierConditionKind.MiddleHand, C(hands: 3, starting: 3), false);
            yield return Case(ModifierConditionKind.InteriorPairMatchesEdge, C("22235"), true);
            yield return Case(ModifierConditionKind.InteriorPairMatchesEdge, C("11225"), false);
            yield return Case(ModifierConditionKind.InteriorPairMatchesEdge, C("11232"), false);
            yield return Case(ModifierConditionKind.Always, C(), true);
        }

        private static TestCaseData Case(ModifierConditionKind kind, ModifierContext context, bool expected,
            int parameter = 0, int secondary = 0)
            => new TestCaseData(new ModifierCondition(kind, parameter, secondary), context, expected)
                .SetName(kind + "_" + string.Join("", context.CurrentDice.ToArray()) + "_" + expected + "_" +
                    context.SelectedMask + "_" + context.HandsRemaining + "_" + context.RerollsRemaining + "_" +
                    context.RerollsSpentOnHand + "_" + context.EverRerolledMask + "_" +
                    string.Join("", context.InitialDice.ToArray()) + "_" + context.BeforeRerollDice + "_" +
                    context.PreviousPlayedDice + "_" + context.PreviousStageStartingHands + "_" + context.PreviousStageHandsRemaining);

        private static ModifierContext C(string dice = "12345", string initial = null, string before = null,
            string previous = null, int selected = 0, int ever = 0, int spent = 0, int hands = 3,
            int rerolls = 3, int played = 0, int starting = 3, int? priorStarting = null, int? priorRemaining = null)
            => new ModifierContext(D(dice), D(initial ?? dice), before == null ? (DiceValues?)null : D(before),
                previous == null ? (DiceValues?)null : D(previous), selected, ever, spent, hands, rerolls,
                played, starting, priorStarting, priorRemaining);

        private static DiceValues D(string values)
            => new DiceValues(values[0] - '0', values[1] - '0', values[2] - '0', values[3] - '0', values[4] - '0');
    }
}
