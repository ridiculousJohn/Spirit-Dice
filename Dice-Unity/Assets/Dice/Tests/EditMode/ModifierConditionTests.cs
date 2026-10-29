using System;
using System.Collections.Generic;
using Dice.Engine;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class ModifierConditionTests
    {
        /**
         * Checks each condition holds for a fabricated witness and not for a near miss / fails if a condition evaluates the
         * wrong predicate or ignores its parameters.
         */
        [TestCaseSource(nameof(Conditions))]
        public void ConditionHoldsForItsWitnessAndNotForItsNearMiss(IModifierCondition condition, ModifierContext? holds,
            ModifierContext? fails)
        {
            if (holds.HasValue) Assert.That(condition.Evaluate(holds.Value), Is.True, "witness");
            if (fails.HasValue) Assert.That(condition.Evaluate(fails.Value), Is.False, "near miss");
        }

        /**
         * Checks the favored face count reads the first favored-face rule among the encounter's rules / fails if it counts a
         * face without such a rule, reads a later rule's face or ignores the snapshot's face.
         */
        [Test]
        public void FavoredFaceCountReadsTheFirstFavoredFaceRule()
        {
            var condition = new FavoredFaceCountAtLeastCondition(3);
            Assert.That(condition.Evaluate(C("22234", rules: new[] { Plain(), Favored(2), Favored(4) })), Is.True);
            Assert.That(condition.Evaluate(C("22234", rules: new[] { Favored(4), Favored(2) })), Is.False);
            Assert.That(condition.Evaluate(C("22234", rules: new[] { Plain() })), Is.False);
            Assert.That(condition.Evaluate(C("22234")), Is.False);
        }

        /**
         * Checks the bound face count reads the dice every binding rule has bound, and nothing without a binding rule / fails
         * if it reads only one binding rule, counts marked dice or counts dice no rule has bound.
         */
        [Test]
        public void BoundFaceCountReadsTheDiceEveryBindingRuleHasBound()
        {
            var condition = new BoundFaceCountAtLeastCondition(3);
            Assert.That(condition.Evaluate(C("21333", rules: new[] { Bound(1), Plain(), Bound(16) })), Is.True);
            Assert.That(condition.Evaluate(C("21333", rules: new[] { Bound(1) })), Is.False);
            Assert.That(condition.Evaluate(C("21333", rules: new[] { new DieBindingRuleSnapshot(TestRules.IdOf("binding"), true, 1, 16) })),
                Is.False);
            Assert.That(condition.Evaluate(C("21333", rules: new[] { Plain() })), Is.False);
        }

        /**
         * Checks two contexts built separately from equal values and equal rule snapshots are equal, and differ when a rule's
         * state or a resource differs / fails if equality compares rule list references or ignores a rule's state.
         */
        [Test]
        public void ContextsWithEqualValuesAndRuleSnapshotsAreEqual()
        {
            var context = C("22234", hands: 2, rules: new[] { Plain(), Favored(2) });
            var same = C("22234", hands: 2, rules: new[] { Plain(), Favored(2) });
            Assert.That(same, Is.EqualTo(context));
            Assert.That(same == context, Is.True);
            Assert.That(same.GetHashCode(), Is.EqualTo(context.GetHashCode()));
            Assert.That(C("22234", hands: 2, rules: new[] { Plain(), Favored(3) }), Is.Not.EqualTo(context));
            Assert.That(C("22234", hands: 2, rules: new[] { Plain() }), Is.Not.EqualTo(context));
            Assert.That(C("22234", hands: 1, rules: new[] { Plain(), Favored(2) }) != context, Is.True);
        }

        /** Checks only the never condition reports it can never activate / fails if another condition is treated as inert. */
        [Test]
        public void OnlyTheNeverConditionCanNeverActivate()
        {
            IModifierCondition never = new NeverCondition();
            IModifierCondition always = new AlwaysCondition();
            IModifierCondition containsFace = new ContainsFaceCondition(1);
            Assert.That(never.CanEverActivate, Is.False);
            Assert.That(always.CanEverActivate, Is.True);
            Assert.That(containsFace.CanEverActivate, Is.True);
        }

        /** Checks a condition accepts its largest parameter and rejects the next one / fails if out-of-range content loads. */
        [Test]
        public void ConditionRejectsParametersOutsideItsRange()
        {
            Assert.DoesNotThrow(() => new ContainsFaceCondition(6));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContainsFaceCondition(7));
            Assert.DoesNotThrow(() => new FavoredFaceCountAtLeastCondition(5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FavoredFaceCountAtLeastCondition(6));
            Assert.DoesNotThrow(() => new NoExtremeFacesCondition(1, 6));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NoExtremeFacesCondition(1, 7));
            Assert.DoesNotThrow(() => new SumLessThanCondition(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SumLessThanCondition(-1));
        }

        private static IEnumerable<TestCaseData> Conditions()
        {
            yield return Pair(new NoDiceRerolledCondition(), C(), C(ever: 1, spent: 1));
            yield return Pair(new ContainsFaceCondition(6), C("23456"), C());
            yield return Pair(new ExactlyOneSelectedFaceCondition(6), C("12346", selected: 16), C("12346", selected: 1));
            yield return Pair(new OutOfRerollsCondition(), C(rerolls: 0), C(rerolls: 1));
            yield return Pair(new RangeLessThanCondition(2), C("11222"), C("11233"));
            yield return Pair(new SumLessThanInitialCondition(), C("11111", initial: "11112"), C("11112", initial: "11112"));
            yield return Pair(new LastHandCondition(), C(hands: 1), C(hands: 2));
            yield return Pair(new SelectedFaceUnchangedCondition(), C(before: "12346", selected: 1), C(before: "12346", selected: 16));
            yield return Pair(new SelectedMatchingGroupAtLeastCondition(2), C("22345", selected: 3), C("22345", selected: 1));
            yield return Pair(new RankBelowPreviousCondition(), C("12346", previous: "11234"), C("11234", previous: "12346"));
            yield return Pair(new RankAbovePreviousCondition(), C("11234", previous: "12346"), C("12346", previous: "11234"));
            yield return Pair(new RankEqualsPreviousCondition(), C("11234", previous: "22345"), C("12346", previous: "22345"));
            yield return Pair(new RankEqualsBeforeRerollCondition(), C("11234", before: "22345"), C("12346", before: "22345"));
            yield return Pair(new SumDivisibleByCondition(4), C("11114"), C());
            yield return Pair(new EdgePairSumsEqualCondition(), C("12312"), C("12313"));
            yield return Pair(new OrderedCondition(), C("11234"), C("12134"));
            yield return Pair(new HighDieCondition(), C("12346"), C("11234"));
            yield return Pair(new AdjacentDifferentCondition(), C("12121"), C("11234"));
            yield return Pair(new AdjacentMatchingTripleCondition(), C("22234"), C("22123"));
            yield return Pair(new ExactlyOneFaceCondition(5), C(), C("12346"));
            yield return Pair(new ContainsRerollsRemainingCondition(), C(rerolls: 3), C(rerolls: 6));
            yield return Pair(new SelectedMatchesHandsRemainingCondition(), C(selected: 4, hands: 3), C(selected: 1, hands: 3));
            yield return Pair(new AdjacentDifferenceAtLeastCondition(3), C("14141"), C("13131"));
            yield return Pair(new PositiveRerollsDivisibleByCondition(2), C(rerolls: 2), C(rerolls: 3));
            yield return Pair(new MiddleUniqueCondition(), C(), C("13345"));
            yield return Pair(new HighDieOrStraightCondition(), C("12346"), C("11234"));
            yield return Pair(new AlternatingParityCondition(), C("12121"), C("12221"));
            yield return Pair(new InteriorEqualsNeighborSumCondition(), C("13245"), C());
            yield return Pair(new SelectedInteriorEqualsNeighborSumCondition(), C("13245", selected: 2), C("13245", selected: 1));
            yield return Pair(new EveryDieRerolledCondition(), C(ever: 31), C(ever: 30));
            yield return Pair(new ExactlyOneSelectedMatchesSelectedCountCondition(), C(selected: 3), C("22345", selected: 3));
            yield return Pair(new MinimumEqualsRerollsSpentCondition(), C("22345", spent: 2), C("22345", spent: 1));
            yield return Pair(new RerollsExceedHandsRemainingCondition(), C(hands: 2, rerolls: 4), C(hands: 2, rerolls: 3));
            yield return Pair(new MaximumAtMostRerollsRemainingCondition(), C(rerolls: 5), C(rerolls: 4));
            yield return Pair(new LargestGroupEqualsHandsOrRerollsCondition(), C("11234", hands: 1, rerolls: 4),
                C("11234", hands: 2, rerolls: 4));
            yield return Pair(new EdgeSumEqualsEitherCondition(7, 9), C("12346"), C());
            yield return Pair(new FaceEqualsPositionCondition(), C("22345"), C("23456"));
            yield return Pair(new FirstHandCondition(), C(played: 0), C(played: 1));
            yield return Pair(new ExtremeEqualsHandsRemainingCondition(), C(hands: 0), C("22346", hands: 2));
            yield return Pair(new SumLessThanCondition(10), C("11115"), C("11116"));
            yield return Pair(new TwoPairCondition(), C("11223"), C("11234"));
            yield return Pair(new SelectedSumEqualsEitherCondition(3, 5), C(selected: 3), C("23456", selected: 10));
            yield return Pair(new RerollsSpentAtLeastCondition(2), C(spent: 2), C(spent: 1));
            yield return Pair(new AdjacentDifferenceAtMostCondition(2), C("11345"), C("11456"));
            yield return Pair(new EdgeSumGreaterThanInteriorCondition(), C("61116"), C("11112"));
            yield return Pair(new InitialPairCondition(), C(initial: "11234"), C(initial: "12345"));
            yield return Pair(new InitialRankAbovePreviousCondition(), C(initial: "11234", previous: "12346"),
                C(initial: "12346", previous: "11234"));
            yield return Pair(new RangeGreaterThanCondition(2), C("12344"), C("11223"));
            yield return Pair(new PreviousStageExhaustedHandsCondition(), C(priorStarting: 3, priorRemaining: 0),
                C(priorStarting: 3, priorRemaining: 1));
            yield return Pair(new SameParityCondition(), C("13513"), C());
            yield return Pair(new InteriorSumDivisibleByCondition(2), C("11341"), C("11261"));
            yield return Pair(new EdgeSumEqualsHandsOrRerollsCondition(), C(hands: 5, rerolls: 4), C(hands: 4, rerolls: 4));
            yield return Pair(new PreviousStageAtMostHalfHandsPlayedCondition(), C(priorStarting: 3, priorRemaining: 2),
                C(priorStarting: 3, priorRemaining: 1));
            yield return Pair(new LastRerollOnLastHandCondition(), C(hands: 1, rerolls: 0), C(hands: 2, rerolls: 0));
            yield return Pair(new RankBelowBeforeRerollCondition(), C("12346", before: "11234"), C("11234", before: "12346"));
            yield return Never(C());
            yield return Pair(new ThreeSmallestSumAtMostMaximumCondition(), C("11244"), C("11233"));
            yield return Pair(new SecondFourthProductAtLeastSumCondition(), C("13131"), C("11331"));
            yield return Pair(new InteriorEqualsNeighborDifferenceCondition(), C("31245"), C("33333"));
            yield return Pair(new MiddleEqualsOddCountCondition(), C("22122"), C("22222"));
            yield return Pair(new SumGreaterThanCondition(20), C("55551"), C("55541"));
            yield return Pair(new HandsRemainingEvenCondition(), C(hands: 1), C(hands: 2));
            yield return Pair(new EdgePairDifferencesEqualCondition(), C("21345"), C("12346"));
            yield return Pair(new InteriorContainsPairCondition(), C("12234"), C());
            yield return Pair(new OddSumGreaterThanEvenSumCondition(), C("11345"), C("22466"));
            yield return Pair(new SumPrimeCondition(), C("11113"), C("11112"));
            yield return Pair(new EdgesMatchInteriorCondition(), C("11121"), C("12341"));
            yield return Pair(new UniqueCountLessThanRerollsSpentCondition(), C("11223", spent: 4), C("11223", spent: 3));
            yield return Pair(new NoExtremeFacesCondition(2, 5), C("13466"), C("23344"));
            yield return Pair(new MiddleFacesCountAtLeastCondition(2), C("33111"), C("31111"));
            yield return Pair(new MiddleHandCondition(), C(hands: 2, starting: 3), C(hands: 3, starting: 3));
            yield return Pair(new InteriorPairMatchesEdgeCondition(), C("22235"), C("11225"));
            yield return Always(C());
            yield return Pair(new FavoredFaceCountAtLeastCondition(4), C("22223", rules: new[] { Favored(2) }),
                C("22234", rules: new[] { Favored(2) }));
            yield return Pair(new BoundFaceCountAtLeastCondition(2), C("22134", rules: new[] { Bound(1) }),
                C("21345", rules: new[] { Bound(1) }));
        }

        /** Every condition of the witness cases, for checks that hold for any condition. */
        internal static IEnumerable<TestCaseData> EveryCondition()
        {
            foreach (TestCaseData pair in Conditions())
                yield return new TestCaseData(pair.Arguments[0]).SetName(pair.TestName);
        }

        private static TestCaseData Pair(IModifierCondition condition, ModifierContext holds, ModifierContext fails)
            => new TestCaseData(condition, (ModifierContext?)holds, (ModifierContext?)fails).SetName(condition.GetType().Name);

        private static TestCaseData Always(ModifierContext holds)
            => new TestCaseData(new AlwaysCondition(), (ModifierContext?)holds, null).SetName(nameof(AlwaysCondition));

        private static TestCaseData Never(ModifierContext fails)
            => new TestCaseData(new NeverCondition(), null, (ModifierContext?)fails).SetName(nameof(NeverCondition));

        private static ModifierContext C(string dice = "12345", string initial = null, string before = null,
            string previous = null, int selected = 0, int ever = 0, int spent = 0, int hands = 3,
            int rerolls = 3, int played = 0, int starting = 3, int? priorStarting = null, int? priorRemaining = null,
            EncounterRuleSnapshot[] rules = null)
            => new ModifierContext(D(dice), D(initial ?? dice), before == null ? (DiceValues?)null : D(before),
                previous == null ? (DiceValues?)null : D(previous), selected, ever, spent, hands, rerolls,
                played, starting, priorStarting, priorRemaining, rules);

        private static EncounterRuleSnapshot Plain() => new EncounterRuleSnapshot(TestRules.IdOf("plain"), true);
        private static EncounterRuleSnapshot Favored(int face) => new FavoredFaceRuleSnapshot(TestRules.IdOf("favored." + face), true, face);
        private static EncounterRuleSnapshot Bound(int boundMask)
            => new DieBindingRuleSnapshot(TestRules.IdOf("binding." + boundMask), true, boundMask, 0);

        private static DiceValues D(string values)
            => new DiceValues(values[0] - '0', values[1] - '0', values[2] - '0', values[3] - '0', values[4] - '0');
    }
}
