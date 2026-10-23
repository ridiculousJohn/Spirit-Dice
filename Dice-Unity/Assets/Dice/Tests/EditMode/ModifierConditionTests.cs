using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dice.Engine;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class ModifierConditionTests
    {
        [TestCaseSource(nameof(Cases))]
        public void ConditionsMatchIndependentBoundaryWitnesses(ModifierCondition condition, ModifierContext context, bool expected)
        {
            Assert.That(ModifierConditions.Create(condition).Evaluate(in context), Is.EqualTo(expected));
            Assert.That(ModifierConditions.Create(condition.Kind, condition.Parameter, condition.SecondaryParameter)
                .Evaluate(in context), Is.EqualTo(expected));
        }

        [Test]
        public void WitnessesCoverEveryConditionKind()
        {
            var covered = Cases().Select(value => ((ModifierCondition)value.Arguments[0]).Kind).Distinct();
            Assert.That(covered, Is.EquivalentTo(Enum.GetValues(typeof(ModifierConditionKind))));
        }

        [Test]
        public void EveryConditionKindCreatesItsOwnSealedEvaluationImplementation()
        {
            var types = new HashSet<Type>();
            foreach (ModifierConditionKind kind in Enum.GetValues(typeof(ModifierConditionKind)))
            {
                var condition = ModifierConditions.Create(kind);
                Type type = condition.GetType();
                Assert.That(type.Name, Is.EqualTo(kind + "Condition"));
                Assert.That(type.IsSealed, Is.True, kind.ToString());
                Assert.That(type.BaseType, Is.EqualTo(typeof(object)), kind.ToString());
                Assert.That(type.GetInterfaces(), Does.Contain(typeof(IModifierCondition)), kind.ToString());
                Assert.That(type.GetMethod(nameof(IModifierCondition.Evaluate)).DeclaringType,
                    Is.EqualTo(type), kind.ToString());
                Assert.That(types.Add(type), Is.True, kind.ToString());
                var defaults = new ModifierCondition(kind);
                string[] parameters = defaults.Parameter == 0 ? Array.Empty<string>() :
                    defaults.SecondaryParameter == 0 ? new[] { "Parameter" } :
                    new[] { "Parameter", "SecondaryParameter" };
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                Assert.That(properties.Select(value => value.Name), Is.EquivalentTo(parameters), kind.ToString());
                foreach (var property in properties)
                {
                    Assert.That(property.DeclaringType, Is.EqualTo(type), kind.ToString());
                    Assert.That(property.PropertyType, Is.EqualTo(typeof(int)), kind.ToString());
                    Assert.That(property.CanWrite, Is.False, kind.ToString());
                }
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(fields.Length, Is.EqualTo(parameters.Length), kind.ToString());
                Assert.That(fields.All(value => value.FieldType == typeof(int) && value.IsInitOnly),
                    Is.True, kind.ToString());
            }
        }

        [TestCase(-1)]
        [TestCase(int.MaxValue)]
        public void FactoryRejectsUnknownConditionKinds(int value)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                ModifierConditions.Create((ModifierConditionKind)value));
            Assert.That(exception.ParamName, Is.EqualTo("kind"));
        }

        [Test]
        public void EveryBuiltInConditionRejectsTheDefaultInvalidContext()
        {
            var context = default(ModifierContext);
            foreach (ModifierConditionKind kind in Enum.GetValues(typeof(ModifierConditionKind)))
            {
                var condition = ModifierConditions.Create(kind);
                var exception = Assert.Throws<ArgumentException>(() => condition.Evaluate(in context), kind.ToString());
                Assert.That(exception.ParamName, Is.EqualTo("context"), kind.ToString());
            }
        }

        [TestCaseSource(nameof(ParameterizedConstructors))]
        public void ParameterizedConstructorsAndFactoriesPreserveDefaultsAndNormalizeZero(
            ModifierConditionKind kind, Func<int, int, IModifierCondition> construct,
            Func<IModifierCondition> constructDefault, int parameter, int secondaryParameter, int maximum)
        {
            foreach (var condition in new[]
            {
                constructDefault(), construct(0, 0), ModifierConditions.Create(kind),
                construct(0, secondaryParameter), construct(parameter, 0),
                ModifierConditions.Create(kind, 0, 0), ModifierConditions.Create(new ModifierCondition(kind))
            })
            {
                var type = condition.GetType();
                Assert.That(type.GetProperty("Parameter").GetValue(condition), Is.EqualTo(parameter));
                if (secondaryParameter != 0)
                    Assert.That(type.GetProperty("SecondaryParameter").GetValue(condition),
                        Is.EqualTo(secondaryParameter));
            }
        }

        [TestCaseSource(nameof(ParameterizedConstructors))]
        public void ParameterizedConstructorsAndFactoriesRejectInvalidConsumedParameters(
            ModifierConditionKind kind, Func<int, int, IModifierCondition> construct,
            Func<IModifierCondition> constructDefault, int parameter, int secondaryParameter, int maximum)
        {
            var largest = construct(maximum, secondaryParameter);
            Assert.That(largest.GetType().GetProperty("Parameter").GetValue(largest), Is.EqualTo(maximum));
            Assert.Throws<ArgumentOutOfRangeException>(() => construct(-1, secondaryParameter));
            Assert.Throws<ArgumentOutOfRangeException>(() => ModifierConditions.Create(kind, -1, secondaryParameter));
            if (maximum != int.MaxValue)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => construct(maximum + 1, secondaryParameter));
                Assert.Throws<ArgumentOutOfRangeException>(() => ModifierConditions.Create(kind, maximum + 1, secondaryParameter));
            }
            if (secondaryParameter == 0) return;
            Assert.Throws<ArgumentOutOfRangeException>(() => construct(parameter, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ModifierConditions.Create(kind, parameter, -1));
            if (kind == ModifierConditionKind.NoExtremeFaces)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => construct(parameter, 7));
                Assert.Throws<ArgumentOutOfRangeException>(() => ModifierConditions.Create(kind, parameter, 7));
            }
        }

        [TestCaseSource(nameof(NonDefaultCases))]
        public void ConstructorsAndFactoriesUseNonDefaultParametersInEvaluation(ModifierCondition descriptor,
            ModifierContext context, bool expected, bool defaultExpected)
        {
            ConditionsMatchIndependentBoundaryWitnesses(descriptor, context, expected);
            var constructorCase = ParameterizedConstructors().Single(value =>
                (ModifierConditionKind)value.Arguments[0] == descriptor.Kind);
            var construct = (Func<int, int, IModifierCondition>)constructorCase.Arguments[1];
            Assert.That(construct(descriptor.Parameter, descriptor.SecondaryParameter).Evaluate(in context),
                Is.EqualTo(expected));
            Assert.That(ModifierConditions.Create(descriptor.Kind).Evaluate(in context), Is.EqualTo(defaultExpected));
        }

        [Test]
        public void NonDefaultBehaviorWitnessesCoverEveryParameterizedKind()
        {
            var parameterized = ParameterizedConstructors().Select(value => (ModifierConditionKind)value.Arguments[0]);
            var covered = NonDefaultCases().Where(value => (bool)value.Arguments[2] != (bool)value.Arguments[3])
                .Select(value => ((ModifierCondition)value.Arguments[0]).Kind).Distinct();
            Assert.That(parameterized.Count(), Is.EqualTo(21));
            Assert.That(covered, Is.EquivalentTo(parameterized));
        }

        private static IEnumerable<TestCaseData> ParameterizedConstructors()
        {
            yield return Constructor(ModifierConditionKind.FavoredFaceCountAtLeast, (p, q) => new FavoredFaceCountAtLeastCondition(p), () => new FavoredFaceCountAtLeastCondition(), 2, maximum: 5);
            yield return Constructor(ModifierConditionKind.BoundFaceCountAtLeast, (p, q) => new BoundFaceCountAtLeastCondition(p), () => new BoundFaceCountAtLeastCondition(), 3, maximum: 5);
            yield return Constructor(ModifierConditionKind.ContainsFace, (p, q) => new ContainsFaceCondition(p), () => new ContainsFaceCondition(), 1, maximum: 6);
            yield return Constructor(ModifierConditionKind.ContainsUpperFace, (p, q) => new ContainsUpperFaceCondition(p), () => new ContainsUpperFaceCondition(), 6, maximum: 6);
            yield return Constructor(ModifierConditionKind.ExactlyOneSelectedFace, (p, q) => new ExactlyOneSelectedFaceCondition(p), () => new ExactlyOneSelectedFaceCondition(), 1, maximum: 6);
            yield return Constructor(ModifierConditionKind.ExactlyOneFace, (p, q) => new ExactlyOneFaceCondition(p), () => new ExactlyOneFaceCondition(), 2, maximum: 6);
            yield return Constructor(ModifierConditionKind.RangeLessThan, (p, q) => new RangeLessThanCondition(p), () => new RangeLessThanCondition(), 3);
            yield return Constructor(ModifierConditionKind.RangeGreaterThan, (p, q) => new RangeGreaterThanCondition(p), () => new RangeGreaterThanCondition(), 3);
            yield return Constructor(ModifierConditionKind.SelectedMatchingGroupAtLeast, (p, q) => new SelectedMatchingGroupAtLeastCondition(p), () => new SelectedMatchingGroupAtLeastCondition(), 3, maximum: 5);
            yield return Constructor(ModifierConditionKind.MiddleFacesCountAtLeast, (p, q) => new MiddleFacesCountAtLeastCondition(p), () => new MiddleFacesCountAtLeastCondition(), 4, maximum: 5);
            yield return Constructor(ModifierConditionKind.SumDivisibleBy, (p, q) => new SumDivisibleByCondition(p), () => new SumDivisibleByCondition(), 5);
            yield return Constructor(ModifierConditionKind.PositiveRerollsDivisibleBy, (p, q) => new PositiveRerollsDivisibleByCondition(p), () => new PositiveRerollsDivisibleByCondition(), 3);
            yield return Constructor(ModifierConditionKind.InteriorSumDivisibleBy, (p, q) => new InteriorSumDivisibleByCondition(p), () => new InteriorSumDivisibleByCondition(), 3);
            yield return Constructor(ModifierConditionKind.AdjacentDifferenceAtLeast, (p, q) => new AdjacentDifferenceAtLeastCondition(p), () => new AdjacentDifferenceAtLeastCondition(), 2);
            yield return Constructor(ModifierConditionKind.AdjacentDifferenceAtMost, (p, q) => new AdjacentDifferenceAtMostCondition(p), () => new AdjacentDifferenceAtMostCondition(), 1);
            yield return Constructor(ModifierConditionKind.SumLessThan, (p, q) => new SumLessThanCondition(p), () => new SumLessThanCondition(), 15);
            yield return Constructor(ModifierConditionKind.SumGreaterThan, (p, q) => new SumGreaterThanCondition(p), () => new SumGreaterThanCondition(), 22);
            yield return Constructor(ModifierConditionKind.RerollsSpentAtLeast, (p, q) => new RerollsSpentAtLeastCondition(p), () => new RerollsSpentAtLeastCondition(), 3);
            yield return Constructor(ModifierConditionKind.EdgeSumEqualsEither, (p, q) => new EdgeSumEqualsEitherCondition(p, q), () => new EdgeSumEqualsEitherCondition(), 6, 8);
            yield return Constructor(ModifierConditionKind.SelectedSumEqualsEither, (p, q) => new SelectedSumEqualsEitherCondition(p, q), () => new SelectedSumEqualsEitherCondition(), 8, 10);
            yield return Constructor(ModifierConditionKind.NoExtremeFaces, (p, q) => new NoExtremeFacesCondition(p, q), () => new NoExtremeFacesCondition(), 1, 6, 6);
        }

        private static TestCaseData Constructor(ModifierConditionKind kind, Func<int, int, IModifierCondition> construct,
            Func<IModifierCondition> constructDefault, int parameter, int secondaryParameter = 0, int maximum = int.MaxValue)
            => new TestCaseData(kind, construct, constructDefault, parameter, secondaryParameter, maximum)
                .SetName("{m}_" + kind);

        private static IEnumerable<TestCaseData> NonDefaultCases()
        {
            yield return NonDefaultCase(ModifierConditionKind.FavoredFaceCountAtLeast, C("22234", favoredFace: 2), false, true, 4);
            yield return NonDefaultCase(ModifierConditionKind.BoundFaceCountAtLeast, C("22134", bound: 1), true, false, 2);
            yield return NonDefaultCase(ModifierConditionKind.ContainsFace, C("23456"), true, false, 6);
            yield return NonDefaultCase(ModifierConditionKind.ContainsFace, C(), false, true, 6);
            yield return NonDefaultCase(ModifierConditionKind.ContainsUpperFace, C(), true, false, 2);
            yield return NonDefaultCase(ModifierConditionKind.ContainsUpperFace, C("13456"), false, true, 2);
            yield return NonDefaultCase(ModifierConditionKind.ExactlyOneSelectedFace, C("12346", selected: 16), true, false, 6);
            yield return NonDefaultCase(ModifierConditionKind.ExactlyOneSelectedFace, C("12346", selected: 1), false, true, 6);
            yield return NonDefaultCase(ModifierConditionKind.ExactlyOneFace, C(), true, true, 5);
            yield return NonDefaultCase(ModifierConditionKind.ExactlyOneFace, C("12346"), false, true, 5);
            yield return NonDefaultCase(ModifierConditionKind.RangeLessThan, C("11222"), true, true, 2);
            yield return NonDefaultCase(ModifierConditionKind.RangeLessThan, C("11233"), false, true, 2);
            yield return NonDefaultCase(ModifierConditionKind.RangeGreaterThan, C("12344"), true, false, 2);
            yield return NonDefaultCase(ModifierConditionKind.RangeGreaterThan, C("11223"), false, false, 2);
            yield return NonDefaultCase(ModifierConditionKind.SelectedMatchingGroupAtLeast, C("22345", selected: 3), true, false, 2);
            yield return NonDefaultCase(ModifierConditionKind.SelectedMatchingGroupAtLeast, C("22345", selected: 1), false, false, 2);
            yield return NonDefaultCase(ModifierConditionKind.MiddleFacesCountAtLeast, C("33111"), true, false, 2);
            yield return NonDefaultCase(ModifierConditionKind.MiddleFacesCountAtLeast, C("31111"), false, false, 2);
            yield return NonDefaultCase(ModifierConditionKind.SumDivisibleBy, C("11114"), true, false, 4);
            yield return NonDefaultCase(ModifierConditionKind.SumDivisibleBy, C(), false, true, 4);
            yield return NonDefaultCase(ModifierConditionKind.PositiveRerollsDivisibleBy, C(rerolls: 2), true, false, 2);
            yield return NonDefaultCase(ModifierConditionKind.PositiveRerollsDivisibleBy, C(rerolls: 3), false, true, 2);
            yield return NonDefaultCase(ModifierConditionKind.PositiveRerollsDivisibleBy, C(rerolls: 0), false, false, 2);
            yield return NonDefaultCase(ModifierConditionKind.InteriorSumDivisibleBy, C("11341"), true, false, 2);
            yield return NonDefaultCase(ModifierConditionKind.InteriorSumDivisibleBy, C("11261"), false, true, 2);
            yield return NonDefaultCase(ModifierConditionKind.AdjacentDifferenceAtLeast, C("14141"), true, true, 3);
            yield return NonDefaultCase(ModifierConditionKind.AdjacentDifferenceAtLeast, C("13131"), false, true, 3);
            yield return NonDefaultCase(ModifierConditionKind.AdjacentDifferenceAtMost, C("11345"), true, false, 2);
            yield return NonDefaultCase(ModifierConditionKind.AdjacentDifferenceAtMost, C("11456"), false, false, 2);
            yield return NonDefaultCase(ModifierConditionKind.SumLessThan, C("11115"), true, true, 10);
            yield return NonDefaultCase(ModifierConditionKind.SumLessThan, C("11116"), false, true, 10);
            yield return NonDefaultCase(ModifierConditionKind.SumGreaterThan, C("55551"), true, false, 20);
            yield return NonDefaultCase(ModifierConditionKind.SumGreaterThan, C("55541"), false, false, 20);
            yield return NonDefaultCase(ModifierConditionKind.RerollsSpentAtLeast, C(spent: 2), true, false, 2);
            yield return NonDefaultCase(ModifierConditionKind.RerollsSpentAtLeast, C(spent: 1), false, false, 2);
            yield return NonDefaultCase(ModifierConditionKind.EdgeSumEqualsEither, C("12346"), true, false, 7, 9);
            yield return NonDefaultCase(ModifierConditionKind.EdgeSumEqualsEither, C("32346"), true, false, 7, 9);
            yield return NonDefaultCase(ModifierConditionKind.EdgeSumEqualsEither, C(), false, true, 7, 9);
            yield return NonDefaultCase(ModifierConditionKind.EdgeSumEqualsEither, C("22346"), false, true, 7, 9);
            yield return NonDefaultCase(ModifierConditionKind.SelectedSumEqualsEither, C(selected: 3), true, false, 3, 5);
            yield return NonDefaultCase(ModifierConditionKind.SelectedSumEqualsEither, C(selected: 6), true, false, 3, 5);
            yield return NonDefaultCase(ModifierConditionKind.SelectedSumEqualsEither, C("23456", selected: 10), false, true, 3, 5);
            yield return NonDefaultCase(ModifierConditionKind.SelectedSumEqualsEither, C("23456", selected: 20), false, true, 3, 5);
            yield return NonDefaultCase(ModifierConditionKind.NoExtremeFaces, C("13466"), true, false, 2, 5);
            yield return NonDefaultCase(ModifierConditionKind.NoExtremeFaces, C("23344"), false, true, 2, 5);
            yield return NonDefaultCase(ModifierConditionKind.NoExtremeFaces, C("33455"), false, true, 2, 5);
        }

        private static TestCaseData NonDefaultCase(ModifierConditionKind kind, ModifierContext context,
            bool expected, bool defaultExpected, int parameter, int secondary = 0)
            => new TestCaseData(new ModifierCondition(kind, parameter, secondary), context, expected, defaultExpected)
                .SetName(kind + "_NonDefault_" + string.Join("", context.CurrentDice.ToArray()) + "_" +
                    context.SelectedMask + "_" + context.RerollsRemaining + "_" + context.RerollsSpentOnHand);

        internal static IEnumerable<TestCaseData> Cases()
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
            yield return Case(ModifierConditionKind.FavoredFaceCountAtLeast, C("22345", favoredFace: 2), true);
            yield return Case(ModifierConditionKind.FavoredFaceCountAtLeast, C("23456", favoredFace: 2), false);
            yield return Case(ModifierConditionKind.FavoredFaceCountAtLeast, C("22345", favoredFace: 6), false);
            yield return Case(ModifierConditionKind.FavoredFaceCountAtLeast, C("22345"), false);
            yield return Case(ModifierConditionKind.BoundFaceCountAtLeast, C("22234", bound: 1), true);
            yield return Case(ModifierConditionKind.BoundFaceCountAtLeast, C("22234", bound: 8), false);
            yield return Case(ModifierConditionKind.BoundFaceCountAtLeast, C("22234", bound: 24), false);
            yield return Case(ModifierConditionKind.BoundFaceCountAtLeast, C("22234", bound: 9), true);
            yield return Case(ModifierConditionKind.BoundFaceCountAtLeast, C("22234"), false);
            yield return Case(ModifierConditionKind.BoundFaceCountAtLeast, C("44454", bound: 16), true);
        }

        private static TestCaseData Case(ModifierConditionKind kind, ModifierContext context, bool expected,
            int parameter = 0, int secondary = 0)
            => new TestCaseData(new ModifierCondition(kind, parameter, secondary), context, expected)
                .SetName(kind + "_" + string.Join("", context.CurrentDice.ToArray()) + "_" + expected + "_" +
                    context.SelectedMask + "_" + context.HandsRemaining + "_" + context.RerollsRemaining + "_" +
                    context.RerollsSpentOnHand + "_" + context.EverRerolledMask + "_" +
                    string.Join("", context.InitialDice.ToArray()) + "_" + context.BeforeRerollDice + "_" +
                    context.PreviousPlayedDice + "_" + context.PreviousStageStartingHands + "_" + context.PreviousStageHandsRemaining + "_" +
                    context.FavoredFace + "_" + context.BoundMask);

        private static ModifierContext C(string dice = "12345", string initial = null, string before = null,
            string previous = null, int selected = 0, int ever = 0, int spent = 0, int hands = 3,
            int rerolls = 3, int played = 0, int starting = 3, int? priorStarting = null, int? priorRemaining = null,
            int? favoredFace = null, int bound = 0)
            => new ModifierContext(D(dice), D(initial ?? dice), before == null ? (DiceValues?)null : D(before),
                previous == null ? (DiceValues?)null : D(previous), selected, ever, spent, hands, rerolls,
                played, starting, priorStarting, priorRemaining, favoredFace, bound);

        private static DiceValues D(string values)
            => new DiceValues(values[0] - '0', values[1] - '0', values[2] - '0', values[3] - '0', values[4] - '0');
    }
}
