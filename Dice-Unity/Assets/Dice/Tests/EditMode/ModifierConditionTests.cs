using System;
using System.Collections.Generic;
using System.Reflection;
using Dice.Engine;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class ModifierConditionTests
    {
        /**
         * Checks each condition kind, built by the factory from its descriptor, holds for a fabricated witness and not for a
         * near miss / fails if a kind evaluates the wrong predicate or ignores its configured parameters.
         */
        [TestCaseSource(nameof(Conditions))]
        public void ConditionHoldsForItsWitnessAndNotForItsNearMiss(ModifierCondition condition, ModifierContext? holds,
            ModifierContext? fails)
        {
            var evaluator = ModifierConditions.Create(condition);
            if (holds.HasValue) Assert.That(evaluator.Evaluate(holds.Value), Is.True, "witness");
            if (fails.HasValue) Assert.That(evaluator.Evaluate(fails.Value), Is.False, "near miss");
        }

        /**
         * Checks every condition kind gets its own sealed, immutable evaluation class, since conditions are shared across
         * sessions and evaluated during previews / fails if two kinds share a class or a condition gains mutable state.
         */
        [Test]
        public void EveryConditionKindCreatesItsOwnSealedImmutableEvaluationClass()
        {
            var types = new HashSet<Type>();
            foreach (ModifierConditionKind kind in Enum.GetValues(typeof(ModifierConditionKind)))
            {
                Type type = ModifierConditions.Create(kind).GetType();
                Assert.That(type.IsSealed, Is.True, kind.ToString());
                Assert.That(types.Add(type), Is.True, kind + " shares its evaluation class with another kind.");
                for (Type level = type; level != null && level != typeof(object); level = level.BaseType)
                {
                    const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                        BindingFlags.DeclaredOnly;
                    foreach (var property in level.GetProperties(Declared))
                        Assert.That(property.SetMethod, Is.Null, kind + ": " + property.Name);
                    foreach (var field in level.GetFields(Declared))
                        Assert.That(field.IsInitOnly, Is.True, kind + ": " + field.Name);
                }
            }
        }

        /** Checks the factory rejects an undefined condition kind / fails if unknown content builds a condition. */
        [Test]
        public void FactoryRejectsAnUndefinedConditionKind()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => ModifierConditions.Create((ModifierConditionKind)(-1)));
            Assert.That(exception.ParamName, Is.EqualTo("kind"));
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
            Assert.That(condition.Evaluate(C("21333", rules: new[] { new DieBindingRuleSnapshot("rule.binding", true, 1, 16) })),
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
            Assert.That(ModifierConditions.Create(ModifierConditionKind.Never).CanEverActivate, Is.False);
            Assert.That(ModifierConditions.Create(ModifierConditionKind.Always).CanEverActivate, Is.True);
            Assert.That(ModifierConditions.Create(ModifierConditionKind.ContainsFace).CanEverActivate, Is.True);
        }

        /** Checks a descriptor accepts its kind's largest parameter and rejects the next one / fails if out-of-range content loads. */
        [TestCase(ModifierConditionKind.ContainsFace, 6, 0, 7, 0)]
        [TestCase(ModifierConditionKind.FavoredFaceCountAtLeast, 5, 0, 6, 0)]
        [TestCase(ModifierConditionKind.NoExtremeFaces, 1, 6, 1, 7)]
        [TestCase(ModifierConditionKind.SumLessThan, 1, 0, -1, 0)]
        public void DescriptorRejectsParametersOutsideItsKindsRange(ModifierConditionKind kind, int validParameter,
            int validSecondary, int invalidParameter, int invalidSecondary)
        {
            Assert.DoesNotThrow(() => new ModifierCondition(kind, validParameter, validSecondary));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ModifierCondition(kind, invalidParameter, invalidSecondary));
        }

        private static IEnumerable<TestCaseData> Conditions()
        {
            yield return Pair(ModifierConditionKind.NoDiceRerolled, C(), C(ever: 1, spent: 1));
            yield return Pair(ModifierConditionKind.ContainsFace, C("23456"), C(), 6);
            yield return Pair(ModifierConditionKind.ExactlyOneSelectedFace, C("12346", selected: 16), C("12346", selected: 1), 6);
            yield return Pair(ModifierConditionKind.OutOfRerolls, C(rerolls: 0), C(rerolls: 1));
            yield return Pair(ModifierConditionKind.RangeLessThan, C("11222"), C("11233"), 2);
            yield return Pair(ModifierConditionKind.SumLessThanInitial, C("11111", initial: "11112"), C("11112", initial: "11112"));
            yield return Pair(ModifierConditionKind.LastHand, C(hands: 1), C(hands: 2));
            yield return Pair(ModifierConditionKind.SelectedFaceUnchanged, C(before: "12346", selected: 1), C(before: "12346", selected: 16));
            yield return Pair(ModifierConditionKind.SelectedMatchingGroupAtLeast, C("22345", selected: 3), C("22345", selected: 1), 2);
            yield return Pair(ModifierConditionKind.ContainsUpperFace, C(), C("13456"), 2);
            yield return Pair(ModifierConditionKind.RankBelowPrevious, C("12346", previous: "11234"), C("11234", previous: "12346"));
            yield return Pair(ModifierConditionKind.RankAbovePrevious, C("11234", previous: "12346"), C("12346", previous: "11234"));
            yield return Pair(ModifierConditionKind.RankEqualsPrevious, C("11234", previous: "22345"), C("12346", previous: "22345"));
            yield return Pair(ModifierConditionKind.RankEqualsBeforeReroll, C("11234", before: "22345"), C("12346", before: "22345"));
            yield return Pair(ModifierConditionKind.SumDivisibleBy, C("11114"), C(), 4);
            yield return Pair(ModifierConditionKind.EdgePairSumsEqual, C("12312"), C("12313"));
            yield return Pair(ModifierConditionKind.Ordered, C("11234"), C("12134"));
            yield return Pair(ModifierConditionKind.HighDie, C("12346"), C("11234"));
            yield return Pair(ModifierConditionKind.AdjacentDifferent, C("12121"), C("11234"));
            yield return Pair(ModifierConditionKind.AdjacentMatchingTriple, C("22234"), C("22123"));
            yield return Pair(ModifierConditionKind.ExactlyOneFace, C(), C("12346"), 5);
            yield return Pair(ModifierConditionKind.ContainsRerollsRemaining, C(rerolls: 3), C(rerolls: 6));
            yield return Pair(ModifierConditionKind.SelectedMatchesHandsRemaining, C(selected: 4, hands: 3), C(selected: 1, hands: 3));
            yield return Pair(ModifierConditionKind.AdjacentDifferenceAtLeast, C("14141"), C("13131"), 3);
            yield return Pair(ModifierConditionKind.PositiveRerollsDivisibleBy, C(rerolls: 2), C(rerolls: 3), 2);
            yield return Pair(ModifierConditionKind.MiddleUnique, C(), C("13345"));
            yield return Pair(ModifierConditionKind.HighDieOrStraight, C("12346"), C("11234"));
            yield return Pair(ModifierConditionKind.AlternatingParity, C("12121"), C("12221"));
            yield return Pair(ModifierConditionKind.InteriorEqualsNeighborSum, C("13245"), C());
            yield return Pair(ModifierConditionKind.SelectedInteriorEqualsNeighborSum, C("13245", selected: 2), C("13245", selected: 1));
            yield return Pair(ModifierConditionKind.EveryDieRerolled, C(ever: 31), C(ever: 30));
            yield return Pair(ModifierConditionKind.ExactlyOneSelectedMatchesSelectedCount, C(selected: 3), C("22345", selected: 3));
            yield return Pair(ModifierConditionKind.MinimumEqualsRerollsSpent, C("22345", spent: 2), C("22345", spent: 1));
            yield return Pair(ModifierConditionKind.RerollsExceedHandsRemaining, C(hands: 2, rerolls: 4), C(hands: 2, rerolls: 3));
            yield return Pair(ModifierConditionKind.MaximumAtMostRerollsRemaining, C(rerolls: 5), C(rerolls: 4));
            yield return Pair(ModifierConditionKind.LargestGroupEqualsHandsOrRerolls, C("11234", hands: 1, rerolls: 4),
                C("11234", hands: 2, rerolls: 4));
            yield return Pair(ModifierConditionKind.EdgeSumEqualsEither, C("12346"), C(), 7, 9);
            yield return Pair(ModifierConditionKind.FaceEqualsPosition, C("22345"), C("23456"));
            yield return Pair(ModifierConditionKind.FirstHand, C(played: 0), C(played: 1));
            yield return Pair(ModifierConditionKind.ExtremeEqualsHandsRemaining, C(hands: 0), C("22346", hands: 2));
            yield return Pair(ModifierConditionKind.SumLessThan, C("11115"), C("11116"), 10);
            yield return Pair(ModifierConditionKind.TwoPair, C("11223"), C("11234"));
            yield return Pair(ModifierConditionKind.SelectedSumEqualsEither, C(selected: 3), C("23456", selected: 10), 3, 5);
            yield return Pair(ModifierConditionKind.RerollsSpentAtLeast, C(spent: 2), C(spent: 1), 2);
            yield return Pair(ModifierConditionKind.AdjacentDifferenceAtMost, C("11345"), C("11456"), 2);
            yield return Pair(ModifierConditionKind.EdgeSumGreaterThanInterior, C("61116"), C("11112"));
            yield return Pair(ModifierConditionKind.InitialPair, C(initial: "11234"), C(initial: "12345"));
            yield return Pair(ModifierConditionKind.InitialRankAbovePrevious, C(initial: "11234", previous: "12346"),
                C(initial: "12346", previous: "11234"));
            yield return Pair(ModifierConditionKind.RangeGreaterThan, C("12344"), C("11223"), 2);
            yield return Pair(ModifierConditionKind.PreviousStageExhaustedHands, C(priorStarting: 3, priorRemaining: 0),
                C(priorStarting: 3, priorRemaining: 1));
            yield return Pair(ModifierConditionKind.SameParity, C("13513"), C());
            yield return Pair(ModifierConditionKind.InteriorSumDivisibleBy, C("11341"), C("11261"), 2);
            yield return Pair(ModifierConditionKind.EdgeSumEqualsHandsOrRerolls, C(hands: 5, rerolls: 4), C(hands: 4, rerolls: 4));
            yield return Pair(ModifierConditionKind.PreviousStageAtMostHalfHandsPlayed, C(priorStarting: 3, priorRemaining: 2),
                C(priorStarting: 3, priorRemaining: 1));
            yield return Pair(ModifierConditionKind.LastRerollOnLastHand, C(hands: 1, rerolls: 0), C(hands: 2, rerolls: 0));
            yield return Pair(ModifierConditionKind.RankBelowBeforeReroll, C("12346", before: "11234"), C("11234", before: "12346"));
            yield return Never(C());
            yield return Pair(ModifierConditionKind.ThreeSmallestSumAtMostMaximum, C("11244"), C("11233"));
            yield return Pair(ModifierConditionKind.SecondFourthProductAtLeastSum, C("13131"), C("11331"));
            yield return Pair(ModifierConditionKind.InteriorEqualsNeighborDifference, C("31245"), C("33333"));
            yield return Pair(ModifierConditionKind.MiddleEqualsOddCount, C("22122"), C("22222"));
            yield return Pair(ModifierConditionKind.SumGreaterThan, C("55551"), C("55541"), 20);
            yield return Pair(ModifierConditionKind.HandsRemainingEven, C(hands: 1), C(hands: 2));
            yield return Pair(ModifierConditionKind.EdgePairDifferencesEqual, C("21345"), C("12346"));
            yield return Pair(ModifierConditionKind.InteriorContainsPair, C("12234"), C());
            yield return Pair(ModifierConditionKind.OddSumGreaterThanEvenSum, C("11345"), C("22466"));
            yield return Pair(ModifierConditionKind.SumPrime, C("11113"), C("11112"));
            yield return Pair(ModifierConditionKind.EdgesMatchInterior, C("11121"), C("12341"));
            yield return Pair(ModifierConditionKind.UniqueCountLessThanRerollsSpent, C("11223", spent: 4), C("11223", spent: 3));
            yield return Pair(ModifierConditionKind.NoExtremeFaces, C("13466"), C("23344"), 2, 5);
            yield return Pair(ModifierConditionKind.MiddleFacesCountAtLeast, C("33111"), C("31111"), 2);
            yield return Pair(ModifierConditionKind.MiddleHand, C(hands: 2, starting: 3), C(hands: 3, starting: 3));
            yield return Pair(ModifierConditionKind.InteriorPairMatchesEdge, C("22235"), C("11225"));
            yield return Always(C());
            yield return Pair(ModifierConditionKind.FavoredFaceCountAtLeast, C("22223", rules: new[] { Favored(2) }),
                C("22234", rules: new[] { Favored(2) }), 4);
            yield return Pair(ModifierConditionKind.BoundFaceCountAtLeast, C("22134", rules: new[] { Bound(1) }),
                C("21345", rules: new[] { Bound(1) }), 2);
        }

        /** Each condition's witness and near miss as single expectations, for checks that compare imported conditions. */
        internal static IEnumerable<TestCaseData> Cases()
        {
            foreach (var pair in Conditions())
            {
                var condition = (ModifierCondition)pair.Arguments[0];
                var holds = (ModifierContext?)pair.Arguments[1];
                var fails = (ModifierContext?)pair.Arguments[2];
                if (holds.HasValue)
                    yield return new TestCaseData(condition, holds.Value, true).SetName(condition.Kind + "_Witness");
                if (fails.HasValue)
                    yield return new TestCaseData(condition, fails.Value, false).SetName(condition.Kind + "_NearMiss");
            }
        }

        private static TestCaseData Pair(ModifierConditionKind kind, ModifierContext holds, ModifierContext fails,
            int parameter = 0, int secondary = 0)
            => new TestCaseData(new ModifierCondition(kind, parameter, secondary), (ModifierContext?)holds, (ModifierContext?)fails)
                .SetName(kind.ToString());

        private static TestCaseData Always(ModifierContext holds)
            => new TestCaseData(new ModifierCondition(ModifierConditionKind.Always), (ModifierContext?)holds, null)
                .SetName(nameof(ModifierConditionKind.Always));

        private static TestCaseData Never(ModifierContext fails)
            => new TestCaseData(new ModifierCondition(ModifierConditionKind.Never), null, (ModifierContext?)fails)
                .SetName(nameof(ModifierConditionKind.Never));

        private static ModifierContext C(string dice = "12345", string initial = null, string before = null,
            string previous = null, int selected = 0, int ever = 0, int spent = 0, int hands = 3,
            int rerolls = 3, int played = 0, int starting = 3, int? priorStarting = null, int? priorRemaining = null,
            EncounterRuleSnapshot[] rules = null)
            => new ModifierContext(D(dice), D(initial ?? dice), before == null ? (DiceValues?)null : D(before),
                previous == null ? (DiceValues?)null : D(previous), selected, ever, spent, hands, rerolls,
                played, starting, priorStarting, priorRemaining, rules);

        private static EncounterRuleSnapshot Plain() => new EncounterRuleSnapshot("rule.plain", true);
        private static EncounterRuleSnapshot Favored(int face) => new FavoredFaceRuleSnapshot("rule.favored." + face, true, face);
        private static EncounterRuleSnapshot Bound(int boundMask)
            => new DieBindingRuleSnapshot("rule.binding." + boundMask, true, boundMask, 0);

        private static DiceValues D(string values)
            => new DiceValues(values[0] - '0', values[1] - '0', values[2] - '0', values[3] - '0', values[4] - '0');
    }
}
