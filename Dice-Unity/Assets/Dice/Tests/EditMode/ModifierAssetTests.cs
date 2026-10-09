using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dice.Engine;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class ModifierAssetTests
    {
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();
        }

        [Test]
        public void BuildPreservesIdentityConditionEffectsAndActivationPolicy()
        {
            var asset = Modifier("modifier.example");
            using var serialized = new SerializedObject(asset);
            serialized.FindProperty("phase").intValue = (int)ModifierPhase.PlayerReroll;
            serialized.FindProperty("conditionKind").intValue = (int)ModifierConditionKind.SelectedSumEqualsEither;
            serialized.FindProperty("parameter").intValue = 7;
            serialized.FindProperty("secondaryParameter").intValue = 12;
            serialized.FindProperty("activationLimit").intValue = (int)ModifierActivationLimit.OncePerStage;
            serialized.FindProperty("rewardEligible").boolValue = false;
            var effects = serialized.FindProperty("effects");
            effects.arraySize = 2;
            Effect(effects.GetArrayElementAtIndex(0), ModifierEffectKind.AddBaseScore, 25);
            Effect(effects.GetArrayElementAtIndex(1), ModifierEffectKind.AddRerolls, 2);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var definition = asset.Build();
            Assert.That(asset.Id, Is.EqualTo("modifier.example"));
            Assert.That(asset.FamilyId, Is.EqualTo("family.example"));
            Assert.That(definition.Id, Is.EqualTo(asset.Id));
            Assert.That(definition.FamilyId, Is.EqualTo(asset.FamilyId));
            Assert.That(definition.Phase, Is.EqualTo(ModifierPhase.PlayerReroll));
            Assert.That(definition.Condition, Is.TypeOf<SelectedSumEqualsEitherCondition>());
            var condition = (SelectedSumEqualsEitherCondition)definition.Condition;
            Assert.That(condition.Parameter, Is.EqualTo(7));
            Assert.That(condition.SecondaryParameter, Is.EqualTo(12));
            Assert.That(definition.ActivationLimit, Is.EqualTo(ModifierActivationLimit.OncePerStage));
            Assert.That(definition.RewardEligible, Is.False);
            Assert.That(definition.Effects.Select(effect => effect.Kind),
                Is.EqualTo(new[] { ModifierEffectKind.AddBaseScore, ModifierEffectKind.AddRerolls }));
            Assert.That(definition.Effects.Select(effect => effect.Amount), Is.EqualTo(new long[] { 25, 2 }));

            Effect(effects.GetArrayElementAtIndex(0), ModifierEffectKind.AddMultiplier, 9);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(definition.Effects[0].Kind, Is.EqualTo(ModifierEffectKind.AddBaseScore));
            Assert.That(definition.Effects[0].Amount, Is.EqualTo(25));
        }

        [Test]
        public void BuildUsesEngineValidationForInvalidConditionAndEffect()
        {
            var asset = Modifier("modifier.invalid");
            using var serialized = new SerializedObject(asset);
            serialized.FindProperty("conditionKind").intValue = (int)ModifierConditionKind.ContainsFace;
            serialized.FindProperty("parameter").intValue = 7;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<ArgumentOutOfRangeException>(() => asset.Build());

            serialized.FindProperty("parameter").intValue = 1;
            var effects = serialized.FindProperty("effects");
            effects.arraySize = 1;
            Effect(effects.GetArrayElementAtIndex(0), ModifierEffectKind.AddRerolls, (long)int.MaxValue + 1);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<ArgumentOutOfRangeException>(() => asset.Build());
        }

        [Test]
        public void CatalogBuildOrdersModifiersByOrdinalStableIdIndependentOfInputOrder()
        {
            var catalog = Catalog();
            var modifiers = new[] { Modifier("modifier.z"), Modifier("modifier.a"), Modifier("modifier.A") };
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                var first = catalog.Build(modifiers);
                var second = catalog.Build(modifiers.Reverse());
                Assert.That(first.Modifiers.Select(modifier => modifier.Id),
                    Is.EqualTo(new[] { "modifier.A", "modifier.a", "modifier.z" }));
                Assert.That(second.Modifiers.Select(modifier => modifier.Id),
                    Is.EqualTo(first.Modifiers.Select(modifier => modifier.Id)));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        [Test]
        public void CatalogBuildRejectsNullCollectionsMissingAssetsAndDuplicateIds()
        {
            var catalog = Catalog();
            var first = Modifier("modifier.same");
            var second = Modifier("modifier.same");
            Assert.Throws<ArgumentNullException>(() => catalog.Build(null));
            Assert.Throws<ArgumentException>(() => catalog.Build(new[] { first, null }));
            Assert.Throws<ArgumentException>(() => catalog.Build(new[] { first, second }));
            Assert.Throws<ArgumentException>(() => catalog.Build(new[] { first, first }));
        }

        private ModifierAsset Modifier(string id)
        {
            var asset = Create<ModifierAsset>();
            using var serialized = new SerializedObject(asset);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("familyId").stringValue = "family.example";
            serialized.FindProperty("conditionKind").intValue = (int)ModifierConditionKind.Always;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private MechanicalCatalogAsset Catalog()
        {
            var asset = Create<MechanicalCatalogAsset>();
            using var serialized = new SerializedObject(asset);
            serialized.FindProperty("catalogVersion").stringValue = "test.assets";
            var hands = serialized.FindProperty("hands");
            hands.arraySize = HandDefinition.CategoryCount;
            for (int i = 0; i < hands.arraySize; ++i)
            {
                var hand = hands.GetArrayElementAtIndex(i);
                hand.FindPropertyRelative("id").stringValue = "hand." + i;
                hand.FindPropertyRelative("category").intValue = i;
                hand.FindPropertyRelative("multiplier").longValue = 1;
            }
            var encounters = serialized.FindProperty("encounters");
            encounters.arraySize = 1;
            var encounter = encounters.GetArrayElementAtIndex(0);
            encounter.FindPropertyRelative("id").stringValue = "encounter.test";
            encounter.FindPropertyRelative("startingUpgrades").arraySize = HandDefinition.CategoryCount;
            var stages = encounter.FindPropertyRelative("stages");
            stages.arraySize = 1;
            var stage = stages.GetArrayElementAtIndex(0);
            stage.FindPropertyRelative("threshold").longValue = 100;
            stage.FindPropertyRelative("startingHands").intValue = 3;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }

        private static void Effect(SerializedProperty effect, ModifierEffectKind kind, long amount)
        {
            effect.FindPropertyRelative("kind").intValue = (int)kind;
            effect.FindPropertyRelative("amount").longValue = amount;
        }
    }
}
