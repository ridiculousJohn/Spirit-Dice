using System;

namespace Dice.Engine
{
    public static class ModifierConditions
    {
        public static IModifierCondition Create(ModifierConditionKind kind, int parameter = 0, int secondaryParameter = 0)
            => Create(new ModifierCondition(kind, parameter, secondaryParameter));

        public static IModifierCondition Create(ModifierCondition configuration)
        {
            switch (configuration.Kind)
            {
                case ModifierConditionKind.Always: return new AlwaysCondition();
                case ModifierConditionKind.Never: return new NeverCondition();
                case ModifierConditionKind.NoDiceRerolled: return new NoDiceRerolledCondition();
                case ModifierConditionKind.ContainsFace: return new ContainsFaceCondition(configuration.Parameter);
                case ModifierConditionKind.ContainsUpperFace: return new ContainsUpperFaceCondition(configuration.Parameter);
                case ModifierConditionKind.ExactlyOneSelectedFace: return new ExactlyOneSelectedFaceCondition(configuration.Parameter);
                case ModifierConditionKind.OutOfRerolls: return new OutOfRerollsCondition();
                case ModifierConditionKind.RangeLessThan: return new RangeLessThanCondition(configuration.Parameter);
                case ModifierConditionKind.SumLessThanInitial: return new SumLessThanInitialCondition();
                case ModifierConditionKind.LastHand: return new LastHandCondition();
                case ModifierConditionKind.SelectedFaceUnchanged: return new SelectedFaceUnchangedCondition();
                case ModifierConditionKind.SelectedMatchingGroupAtLeast: return new SelectedMatchingGroupAtLeastCondition(configuration.Parameter);
                case ModifierConditionKind.RankBelowPrevious: return new RankBelowPreviousCondition();
                case ModifierConditionKind.RankAbovePrevious: return new RankAbovePreviousCondition();
                case ModifierConditionKind.RankEqualsPrevious: return new RankEqualsPreviousCondition();
                case ModifierConditionKind.RankEqualsBeforeReroll: return new RankEqualsBeforeRerollCondition();
                case ModifierConditionKind.SumDivisibleBy: return new SumDivisibleByCondition(configuration.Parameter);
                case ModifierConditionKind.EdgePairSumsEqual: return new EdgePairSumsEqualCondition();
                case ModifierConditionKind.Ordered: return new OrderedCondition();
                case ModifierConditionKind.HighDie: return new HighDieCondition();
                case ModifierConditionKind.AdjacentDifferent: return new AdjacentDifferentCondition();
                case ModifierConditionKind.AdjacentMatchingTriple: return new AdjacentMatchingTripleCondition();
                case ModifierConditionKind.ExactlyOneFace: return new ExactlyOneFaceCondition(configuration.Parameter);
                case ModifierConditionKind.ContainsRerollsRemaining: return new ContainsRerollsRemainingCondition();
                case ModifierConditionKind.SelectedMatchesHandsRemaining: return new SelectedMatchesHandsRemainingCondition();
                case ModifierConditionKind.AdjacentDifferenceAtLeast: return new AdjacentDifferenceAtLeastCondition(configuration.Parameter);
                case ModifierConditionKind.PositiveRerollsDivisibleBy: return new PositiveRerollsDivisibleByCondition(configuration.Parameter);
                case ModifierConditionKind.MiddleUnique: return new MiddleUniqueCondition();
                case ModifierConditionKind.HighDieOrStraight: return new HighDieOrStraightCondition();
                case ModifierConditionKind.AlternatingParity: return new AlternatingParityCondition();
                case ModifierConditionKind.InteriorEqualsNeighborSum: return new InteriorEqualsNeighborSumCondition();
                case ModifierConditionKind.SelectedInteriorEqualsNeighborSum: return new SelectedInteriorEqualsNeighborSumCondition();
                case ModifierConditionKind.EveryDieRerolled: return new EveryDieRerolledCondition();
                case ModifierConditionKind.ExactlyOneSelectedMatchesSelectedCount: return new ExactlyOneSelectedMatchesSelectedCountCondition();
                case ModifierConditionKind.MinimumEqualsRerollsSpent: return new MinimumEqualsRerollsSpentCondition();
                case ModifierConditionKind.RerollsExceedHandsRemaining: return new RerollsExceedHandsRemainingCondition();
                case ModifierConditionKind.MaximumAtMostRerollsRemaining: return new MaximumAtMostRerollsRemainingCondition();
                case ModifierConditionKind.LargestGroupEqualsHandsOrRerolls: return new LargestGroupEqualsHandsOrRerollsCondition();
                case ModifierConditionKind.EdgeSumEqualsEither: return new EdgeSumEqualsEitherCondition(configuration.Parameter, configuration.SecondaryParameter);
                case ModifierConditionKind.FaceEqualsPosition: return new FaceEqualsPositionCondition();
                case ModifierConditionKind.FirstHand: return new FirstHandCondition();
                case ModifierConditionKind.ExtremeEqualsHandsRemaining: return new ExtremeEqualsHandsRemainingCondition();
                case ModifierConditionKind.SumLessThan: return new SumLessThanCondition(configuration.Parameter);
                case ModifierConditionKind.TwoPair: return new TwoPairCondition();
                case ModifierConditionKind.SelectedSumEqualsEither: return new SelectedSumEqualsEitherCondition(configuration.Parameter, configuration.SecondaryParameter);
                case ModifierConditionKind.RerollsSpentAtLeast: return new RerollsSpentAtLeastCondition(configuration.Parameter);
                case ModifierConditionKind.AdjacentDifferenceAtMost: return new AdjacentDifferenceAtMostCondition(configuration.Parameter);
                case ModifierConditionKind.EdgeSumGreaterThanInterior: return new EdgeSumGreaterThanInteriorCondition();
                case ModifierConditionKind.InitialPair: return new InitialPairCondition();
                case ModifierConditionKind.InitialRankAbovePrevious: return new InitialRankAbovePreviousCondition();
                case ModifierConditionKind.RangeGreaterThan: return new RangeGreaterThanCondition(configuration.Parameter);
                case ModifierConditionKind.PreviousStageExhaustedHands: return new PreviousStageExhaustedHandsCondition();
                case ModifierConditionKind.SameParity: return new SameParityCondition();
                case ModifierConditionKind.InteriorSumDivisibleBy: return new InteriorSumDivisibleByCondition(configuration.Parameter);
                case ModifierConditionKind.EdgeSumEqualsHandsOrRerolls: return new EdgeSumEqualsHandsOrRerollsCondition();
                case ModifierConditionKind.PreviousStageAtMostHalfHandsPlayed: return new PreviousStageAtMostHalfHandsPlayedCondition();
                case ModifierConditionKind.LastRerollOnLastHand: return new LastRerollOnLastHandCondition();
                case ModifierConditionKind.RankBelowBeforeReroll: return new RankBelowBeforeRerollCondition();
                case ModifierConditionKind.ThreeSmallestSumAtMostMaximum: return new ThreeSmallestSumAtMostMaximumCondition();
                case ModifierConditionKind.SecondFourthProductAtLeastSum: return new SecondFourthProductAtLeastSumCondition();
                case ModifierConditionKind.InteriorEqualsNeighborDifference: return new InteriorEqualsNeighborDifferenceCondition();
                case ModifierConditionKind.MiddleEqualsOddCount: return new MiddleEqualsOddCountCondition();
                case ModifierConditionKind.SumGreaterThan: return new SumGreaterThanCondition(configuration.Parameter);
                case ModifierConditionKind.HandsRemainingEven: return new HandsRemainingEvenCondition();
                case ModifierConditionKind.EdgePairDifferencesEqual: return new EdgePairDifferencesEqualCondition();
                case ModifierConditionKind.InteriorContainsPair: return new InteriorContainsPairCondition();
                case ModifierConditionKind.OddSumGreaterThanEvenSum: return new OddSumGreaterThanEvenSumCondition();
                case ModifierConditionKind.SumPrime: return new SumPrimeCondition();
                case ModifierConditionKind.EdgesMatchInterior: return new EdgesMatchInteriorCondition();
                case ModifierConditionKind.UniqueCountLessThanRerollsSpent: return new UniqueCountLessThanRerollsSpentCondition();
                case ModifierConditionKind.NoExtremeFaces: return new NoExtremeFacesCondition(configuration.Parameter, configuration.SecondaryParameter);
                case ModifierConditionKind.MiddleFacesCountAtLeast: return new MiddleFacesCountAtLeastCondition(configuration.Parameter);
                case ModifierConditionKind.MiddleHand: return new MiddleHandCondition();
                case ModifierConditionKind.InteriorPairMatchesEdge: return new InteriorPairMatchesEdgeCondition();
                case ModifierConditionKind.FavoredFaceCountAtLeast: return new FavoredFaceCountAtLeastCondition(configuration.Parameter);
                default: throw new ArgumentOutOfRangeException(nameof(configuration));
            }
        }
    }
}
