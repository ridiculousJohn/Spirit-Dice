using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Dice.Engine;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;

namespace Dice.Tests
{
    public sealed class ModifierCatalogTests
    {
        private static readonly string[] Roles = { "base_score", "multiplier", "combined", "rerolls" };

        [Test]
        public void ImportedCatalogContainsAllFamiliesAndVariantsWithUniqueMechanicalIdentities()
        {
            var catalog = Catalog();
            Assert.That(catalog.Modifiers.Count, Is.EqualTo(300));
            Assert.That(catalog.Modifiers.Select(value => value.Id).Distinct().Count(), Is.EqualTo(300));
            Assert.That(catalog.Modifiers.Select(value => value.FamilyId).Distinct().Count(), Is.EqualTo(75));
            foreach (var family in catalog.Modifiers.GroupBy(value => value.FamilyId))
            {
                Assert.That(family.Count(), Is.EqualTo(4), family.Key);
                Assert.That(family.Select(value => value.Id.Substring(value.FamilyId.Length + 1)), Is.EquivalentTo(Roles), family.Key);
            }
            Assert.That(catalog.Modifiers.Count(value => value.RewardEligible), Is.EqualTo(296));
            Assert.That(catalog.GetEncounter("encounter.standard").StartingModifiers, Is.Empty);
        }

        [TestCaseSource(nameof(Variants))]
        public void EveryImportedVariantMatchesItsExtractedMechanicalValues(int trigger, int phase, string role,
            long baseScore, long multiplier, long rerolls)
        {
            var catalog = Catalog();
            var kind = (ModifierConditionKind)trigger;
            string phaseName = phase == 0 ? "initial_roll" : phase == 1 ? "player_reroll" : "play_hand";
            string familyId = "modifier." + phaseName + "." + SnakeCase(kind.ToString());
            string id = familyId + "." + role;
            var definition = catalog.GetModifier(id);
            Assert.That(definition.FamilyId, Is.EqualTo(familyId));
            Assert.That(definition.Phase, Is.EqualTo((ModifierPhase)phase));
            using var source = new SerializedObject(TestContent.ModifierAssets().Single(asset => asset.Id == id));
            Assert.That(source.FindProperty("conditionKind").intValue, Is.EqualTo(trigger));
            Assert.That(definition.Condition, Is.InstanceOf<IModifierCondition>());
            Assert.That(definition.Condition.GetType().Name, Is.EqualTo(kind + "Condition"));
            Assert.That(definition.ActivationLimit, Is.EqualTo(ModifierActivationLimit.OncePerHand));
            Assert.That(definition.RewardEligible, Is.EqualTo(trigger != 56));
            long[] expected = { baseScore, multiplier, rerolls };
            Assert.That(definition.Effects.Count, Is.EqualTo(expected.Count(value => value != 0)));
            for (int effect = 0; effect < expected.Length; ++effect)
            {
                var effects = definition.Effects.Where(value => (int)value.Kind == effect).ToArray();
                if (expected[effect] == 0) Assert.That(effects, Is.Empty);
                else
                {
                    Assert.That(effects.Length, Is.EqualTo(1));
                    Assert.That(effects[0].Amount, Is.EqualTo(expected[effect]));
                }
            }
        }

        [Test]
        public void CatalogParametersPreservePredicateConstants()
        {
            var expected = new Dictionary<ModifierConditionKind, int[]>
            {
                { ModifierConditionKind.ContainsFace, new[] { 1, 0 } },
                { ModifierConditionKind.ExactlyOneSelectedFace, new[] { 1, 0 } },
                { ModifierConditionKind.RangeLessThan, new[] { 3, 0 } },
                { ModifierConditionKind.SelectedMatchingGroupAtLeast, new[] { 3, 0 } },
                { ModifierConditionKind.ContainsUpperFace, new[] { 6, 0 } },
                { ModifierConditionKind.SumDivisibleBy, new[] { 5, 0 } },
                { ModifierConditionKind.ExactlyOneFace, new[] { 2, 0 } },
                { ModifierConditionKind.AdjacentDifferenceAtLeast, new[] { 2, 0 } },
                { ModifierConditionKind.PositiveRerollsDivisibleBy, new[] { 3, 0 } },
                { ModifierConditionKind.EdgeSumEqualsEither, new[] { 6, 8 } },
                { ModifierConditionKind.SumLessThan, new[] { 15, 0 } },
                { ModifierConditionKind.SelectedSumEqualsEither, new[] { 8, 10 } },
                { ModifierConditionKind.RerollsSpentAtLeast, new[] { 3, 0 } },
                { ModifierConditionKind.AdjacentDifferenceAtMost, new[] { 1, 0 } },
                { ModifierConditionKind.RangeGreaterThan, new[] { 3, 0 } },
                { ModifierConditionKind.InteriorSumDivisibleBy, new[] { 3, 0 } },
                { ModifierConditionKind.SumGreaterThan, new[] { 22, 0 } },
                { ModifierConditionKind.NoExtremeFaces, new[] { 1, 6 } },
                { ModifierConditionKind.MiddleFacesCountAtLeast, new[] { 4, 0 } },
                { ModifierConditionKind.FavoredFaceCountAtLeast, new[] { 2, 0 } }
            };
            var catalog = Catalog();
            var sources = TestContent.ModifierAssets();
            Assert.That(sources.Length, Is.EqualTo(catalog.Modifiers.Count));
            for (int i = 0; i < sources.Length; ++i)
            {
                using var source = new SerializedObject(sources[i]);
                string id = source.FindProperty("id").stringValue;
                var kind = (ModifierConditionKind)source.FindProperty("conditionKind").intValue;
                var parameters = expected.TryGetValue(kind, out var values) ? values : new[] { 0, 0 };
                Assert.That(source.FindProperty("parameter").intValue, Is.EqualTo(parameters[0]), id);
                Assert.That(source.FindProperty("secondaryParameter").intValue, Is.EqualTo(parameters[1]), id);
                var condition = catalog.GetModifier(id).Condition;
                var type = condition.GetType();
                if (parameters[0] != 0)
                    Assert.That(type.GetProperty("Parameter").GetValue(condition), Is.EqualTo(parameters[0]), id);
                if (parameters[1] != 0)
                    Assert.That(type.GetProperty("SecondaryParameter").GetValue(condition), Is.EqualTo(parameters[1]), id);
            }
        }

        [Test]
        public void ImportedConditionsMatchIndependentBoundaryWitnesses()
        {
            var catalog = Catalog();
            var sources = TestContent.ModifierAssets();
            var cases = ModifierConditionTests.Cases().ToArray();
            for (int i = 0; i < sources.Length; ++i)
            {
                using var source = new SerializedObject(sources[i]);
                string id = source.FindProperty("id").stringValue;
                var descriptor = new ModifierCondition(
                    (ModifierConditionKind)source.FindProperty("conditionKind").intValue,
                    source.FindProperty("parameter").intValue,
                    source.FindProperty("secondaryParameter").intValue);
                var witnesses = cases.Where(value =>
                {
                    var witness = (ModifierCondition)value.Arguments[0];
                    return witness.Kind == descriptor.Kind && witness.Parameter == descriptor.Parameter &&
                        witness.SecondaryParameter == descriptor.SecondaryParameter;
                }).ToArray();
                Assert.That(witnesses, Is.Not.Empty, id);
                foreach (var witness in witnesses)
                {
                    var context = (ModifierContext)witness.Arguments[1];
                    Assert.That(catalog.GetModifier(id).Condition.Evaluate(in context),
                        Is.EqualTo((bool)witness.Arguments[2]), id + ": " + witness.TestName);
                }
            }
        }

        private static MechanicalCatalog Catalog() => CatalogAsset().Build(TestContent.ModifierAssets());

        private static MechanicalCatalogAsset CatalogAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<MechanicalCatalogAsset>("Assets/Dice/Content/Mechanical/Standard.asset");
            Assert.That(asset, Is.Not.Null);
            return asset;
        }

        private static IEnumerable<TestCaseData> Variants()
        {
            foreach (string row in GoldenValues.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                long[] values = row.Split(',').Select(value => long.Parse(value, CultureInfo.InvariantCulture)).ToArray();
                for (int role = 0; role < Roles.Length; ++role)
                    yield return new TestCaseData((int)values[0], (int)values[1], Roles[role], values[2 + role * 3],
                        values[3 + role * 3], values[4 + role * 3]).SetName("Variant_" + values[0] + "_" + values[1] + "_" + Roles[role]);
            }
        }

        private static string SnakeCase(string name)
        {
            var value = new StringBuilder();
            for (int i = 0; i < name.Length; ++i)
            {
                if (i != 0 && char.IsUpper(name[i])) value.Append('_');
                value.Append(char.ToLowerInvariant(name[i]));
            }
            return value.ToString();
        }

        // Captured from the extracted reference: trigger, phase, then base/multiplier/rerolls for each variant role.
        private const string GoldenValues = @"57,2,15,0,0,0,3,0,10,1,0,0,0,2
4,2,15,0,0,0,3,0,10,1,0,0,0,2
53,0,30,0,0,0,6,0,15,3,0,0,0,3
22,1,10,0,0,0,2,0,5,1,0,0,0,1
58,2,15,0,0,0,3,0,5,2,0,0,0,2
5,2,10,0,0,0,2,0,5,1,0,0,0,1
13,1,10,0,0,0,2,0,5,1,0,0,0,1
56,2,0,0,0,0,0,0,0,0,0,0,0,0
0,2,10,0,0,0,2,0,5,1,0,0,0,1
19,2,15,0,0,0,3,0,10,1,0,0,0,2
31,1,10,0,0,0,2,0,5,1,0,0,0,1
3,2,10,0,0,0,2,0,5,1,0,0,0,1
2,1,10,0,0,0,2,0,5,1,0,0,0,1
23,1,20,0,0,0,4,0,10,2,0,0,0,2
40,2,15,0,0,0,3,0,10,1,0,0,0,2
64,2,10,0,0,0,2,0,5,1,0,0,0,1
35,2,10,0,0,0,2,0,5,1,0,0,0,1
8,1,20,0,0,0,4,0,10,2,0,0,0,2
36,2,15,0,0,0,3,0,10,1,0,0,0,2
11,2,10,0,0,0,2,0,5,1,0,0,0,2
55,1,10,0,0,0,2,0,5,1,0,0,0,1
47,0,15,0,0,0,3,0,5,2,0,0,0,2
38,0,10,0,0,0,2,0,5,1,0,0,0,1
72,2,15,0,0,0,3,0,10,1,0,0,0,2
39,2,10,0,0,0,2,0,5,1,0,0,0,1
18,2,10,0,0,0,2,0,5,1,0,0,0,1
46,0,10,0,0,0,2,0,5,1,0,0,0,1
44,2,15,0,0,0,3,0,10,1,0,0,0,2
17,2,30,0,0,0,6,0,15,3,0,0,0,3
26,1,10,0,0,0,2,0,5,1,0,0,0,1
10,2,10,0,0,0,2,0,5,1,0,0,0,2
52,2,15,0,0,0,3,0,5,2,0,0,0,2
29,1,15,0,0,0,3,0,5,2,0,0,0,2
6,0,15,0,0,0,3,0,5,2,0,0,0,2
66,2,15,0,0,0,3,0,5,2,0,0,0,2
63,2,10,0,0,0,2,0,5,1,0,0,0,1
59,2,10,0,0,0,2,0,5,1,0,0,0,1
7,1,10,0,0,0,2,0,5,1,0,0,0,1
32,2,15,0,0,0,3,0,10,1,0,0,0,2
69,2,15,0,0,0,3,0,5,2,0,0,0,2
62,2,10,0,0,0,2,0,5,1,0,0,0,1
21,2,10,0,0,0,2,0,5,1,0,0,0,1
33,2,10,0,0,0,2,0,5,1,0,0,0,1
16,1,15,0,0,0,3,0,5,2,0,0,0,2
54,1,20,0,0,0,4,0,10,2,0,0,0,2
50,2,15,0,0,0,3,0,10,1,0,0,0,2
71,0,15,0,0,0,3,0,5,2,0,0,0,2
67,2,15,0,0,0,3,0,10,1,0,0,0,2
28,2,15,0,0,0,3,0,5,2,0,0,0,2
14,2,20,0,0,0,4,0,10,2,0,0,0,2
37,2,10,0,0,0,2,0,5,1,0,0,0,1
41,2,15,0,0,0,3,0,10,1,0,0,0,2
27,2,25,0,0,0,5,0,15,2,0,0,0,2
45,2,20,0,0,0,4,0,10,2,0,0,0,2
1,2,10,0,0,0,2,0,5,1,0,0,0,1
61,2,10,0,0,0,2,0,5,1,0,0,0,1
43,2,10,0,0,0,2,0,5,1,0,0,0,1
68,2,15,0,0,0,3,0,5,2,0,0,0,2
51,2,10,0,0,0,2,0,5,1,0,0,0,1
9,2,10,0,0,0,2,0,5,1,0,0,0,1
16,2,20,0,0,0,4,0,10,2,0,0,0,2
65,2,10,0,0,0,2,0,5,1,0,0,0,1
25,2,10,0,0,0,2,0,5,1,0,0,0,1
60,2,15,0,0,0,3,0,5,2,0,0,0,2
15,2,20,0,0,0,4,0,10,2,0,0,0,2
12,2,15,0,0,0,3,0,5,2,0,0,0,2
48,2,10,0,0,0,2,0,5,1,0,0,0,1
34,2,15,0,0,0,3,0,5,2,0,0,0,2
24,2,10,0,0,0,2,0,5,1,0,0,0,1
20,2,10,0,0,0,2,0,5,1,0,0,0,2
42,1,10,0,0,0,2,0,5,1,0,0,0,1
30,2,10,0,0,0,2,0,5,1,0,0,0,1
70,2,15,0,0,0,3,0,10,1,0,0,0,2
49,0,20,0,0,0,4,0,10,2,0,0,0,2";
    }
}
