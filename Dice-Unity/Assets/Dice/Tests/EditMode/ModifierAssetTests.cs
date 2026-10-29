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
        private const string FamilyGuid = "0f1e2d3c4b5a69788796a5b4c3d2e1f0";
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private readonly SyntheticCharmAssets content = new SyntheticCharmAssets();
        private ModifierFamilyAsset exampleFamily;

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();
            content.Dispose();
            exampleFamily = null;
        }

        [Test]
        public void BuildPreservesIdentityConditionEffectsAndActivationPolicy()
        {
            var asset = Modifier("modifier.example");
            using var serialized = new SerializedObject(asset);
            serialized.FindProperty("phase").intValue = (int)ModifierPhase.PlayerReroll;
            serialized.FindProperty("condition").managedReferenceValue = new SelectedSumEqualsEitherConditionConfig(7, 12);
            serialized.FindProperty("activationLimit").intValue = (int)ModifierActivationLimit.OncePerStage;
            serialized.FindProperty("rewardEligible").boolValue = false;
            serialized.FindProperty("powerTier").intValue = 3;
            serialized.FindProperty("access").intValue = (int)ModifierAccess.PermanentUnlock;
            serialized.FindProperty("availabilityScope").intValue = (int)ModifierAvailabilityScope.Spirit;
            serialized.FindProperty("availabilityScopeId").stringValue = "spirit.test";
            var effects = serialized.FindProperty("effects");
            effects.arraySize = 2;
            effects.GetArrayElementAtIndex(0).managedReferenceValue = new AddBaseScoreEffectConfig(25);
            effects.GetArrayElementAtIndex(1).managedReferenceValue = new AddRerollsEffectConfig(2);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var family = asset.Family.Build();
            var definition = asset.Build(family);
            Assert.That(asset.Id, Is.EqualTo("modifier.example"));
            Assert.That(family.Identity, Is.EqualTo(new ModifierFamilyDefinition.Id(Guid.ParseExact(FamilyGuid, "N"))));
            Assert.That(definition.Id, Is.EqualTo(asset.Id));
            Assert.That(definition.Family, Is.SameAs(family));
            Assert.That(definition.Phase, Is.EqualTo(ModifierPhase.PlayerReroll));
            Assert.That(definition.Condition, Is.TypeOf<SelectedSumEqualsEitherCondition>());
            var condition = (SelectedSumEqualsEitherCondition)definition.Condition;
            Assert.That(condition.Parameter, Is.EqualTo(7));
            Assert.That(condition.SecondaryParameter, Is.EqualTo(12));
            Assert.That(definition.ActivationLimit, Is.EqualTo(ModifierActivationLimit.OncePerStage));
            Assert.That(definition.RewardEligible, Is.False);
            Assert.That(definition.Availability.PowerTier, Is.EqualTo(3));
            Assert.That(definition.Availability.Access, Is.EqualTo(ModifierAccess.PermanentUnlock));
            Assert.That(definition.Availability.Scope, Is.EqualTo(ModifierAvailabilityScope.Spirit));
            Assert.That(definition.Availability.SpiritId, Is.EqualTo("spirit.test"));
            Assert.That(definition.Availability.EncounterId, Is.Null);
            Assert.That(definition.Effects.Select(effect => effect.GetType()),
                Is.EqualTo(new[] { typeof(AddBaseScoreEffect), typeof(AddRerollsEffect) }));
            Assert.That(definition.Effects.Select(effect => effect.Amount), Is.EqualTo(new long[] { 25, 2 }));

            effects.GetArrayElementAtIndex(0).managedReferenceValue = new AddMultiplierEffectConfig(9);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(definition.Effects[0], Is.TypeOf<AddBaseScoreEffect>());
            Assert.That(definition.Effects[0].Amount, Is.EqualTo(25));
        }

        /** Checks an Encounter scope builds the referenced encounter's id and a scope with the wrong target fails, naming the charm / fails if a scope keeps a target it does not use. */
        [Test]
        public void EncounterScopeBuildsItsEncounterIdAndAScopeWithTheWrongTargetFails()
        {
            var asset = Modifier("modifier.scoped");
            var encounter = content.Encounter("encounter.scope");
            using var serialized = new SerializedObject(asset);
            serialized.FindProperty("access").intValue = (int)ModifierAccess.EncounterExclusive;
            serialized.FindProperty("availabilityScope").intValue = (int)ModifierAvailabilityScope.Encounter;
            serialized.FindProperty("availabilityEncounter").objectReferenceValue = encounter;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(asset.AvailabilityProblem, Is.Null);
            Assert.That(asset.Build(asset.Family.Build()).Availability.EncounterId, Is.EqualTo(encounter.Identity));

            serialized.FindProperty("availabilityScopeId").stringValue = "spirit.test";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(asset.AvailabilityProblem, Is.Not.Null);
            Assert.That(Assert.Throws<InvalidOperationException>(() => asset.Build(asset.Family.Build())).Message,
                Does.Contain("modifier.scoped"));

            serialized.FindProperty("availabilityScope").intValue = (int)ModifierAvailabilityScope.Global;
            serialized.FindProperty("availabilityScopeId").stringValue = string.Empty;
            serialized.FindProperty("access").intValue = (int)ModifierAccess.Starter;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(asset.AvailabilityProblem, Is.Not.Null);
            Assert.Throws<InvalidOperationException>(() => asset.Build(asset.Family.Build()));
        }

        [Test]
        public void BuildUsesEngineValidationForInvalidConditionAndEffect()
        {
            var asset = Modifier("modifier.invalid");
            using var serialized = new SerializedObject(asset);
            serialized.FindProperty("condition").managedReferenceValue = new ContainsFaceConditionConfig(7);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<ArgumentOutOfRangeException>(() => asset.Build(asset.Family.Build()));

            serialized.FindProperty("condition.face").intValue = 1;
            var effects = serialized.FindProperty("effects");
            effects.arraySize = 1;
            effects.GetArrayElementAtIndex(0).managedReferenceValue = new AddRerollsEffectConfig((long)int.MaxValue + 1);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<ArgumentOutOfRangeException>(() => asset.Build(asset.Family.Build()));
        }

        /** Checks the catalog lists its referenced charms by ordinal id whatever their authored order and culture / fails if authored order or culture orders them. */
        [Test]
        public void CatalogBuildOrdersModifiersByOrdinalStableIdIndependentOfInputOrder()
        {
            var catalog = Catalog();
            var modifiers = new[] { Modifier("modifier.z"), Modifier("modifier.a"), Modifier("modifier.A") };
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                Pool(catalog, modifiers);
                var first = catalog.Build().Catalog;
                Pool(catalog, modifiers.Reverse().ToArray());
                var second = catalog.Build().Catalog;
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

        /** Checks the catalog holds every charm its encounters and reward pools reference, once each, and no other / fails if a referencing list is skipped, a shared charm repeats or an unreferenced charm enters. */
        [Test]
        public void CatalogIsBuiltFromExactlyTheReferencedCharms()
        {
            var starting = Modifier("modifier.starting");
            var addition = Modifier("modifier.addition");
            var unlock = Modifier("modifier.unlock", ModifierAccess.PermanentUnlock);
            var pooled = Modifier("modifier.pooled");
            var fallback = Modifier("modifier.fallback");
            Modifier("modifier.unreferenced");
            var catalog = Catalog(content.Encounter("encounter.test", startingModifiers: new[] { starting, pooled },
                encounterModifiers: new[] { addition }, unlockRewardModifiers: new[] { unlock }));
            Pool(catalog, new[] { pooled }, fallback);

            var built = catalog.Build().Catalog;

            Assert.That(built.Modifiers.Select(modifier => modifier.Id), Is.EquivalentTo(new[]
                { starting.Id, addition.Id, unlock.Id, pooled.Id, fallback.Id }));
        }

        /** Checks each encounter and pool keeps its charms' ids in authored order / fails if a list is reordered or loses a charm. */
        [Test]
        public void EncounterAndPoolListsKeepTheirReferencedCharmsInAuthoredOrder()
        {
            var second = Modifier("modifier.b");
            var first = Modifier("modifier.a");
            var catalog = Catalog(content.Encounter("encounter.test", encounterModifiers: new[] { second, first }));
            Pool(catalog, new[] { second, first });

            var built = catalog.Build().Catalog;

            Assert.That(built.GetEncounter(TestEncounters.IdOf("encounter.test")).EncounterModifierIds, Is.EqualTo(new[] { second.Id, first.Id }));
            Assert.That(built.GetRewardPool("pool.test").ModifierIds, Is.EqualTo(new[] { second.Id, first.Id }));
        }

        /** Checks the content pairs each referenced charm with the catalog's definition of it and refuses an unreferenced charm / fails if a charm maps to another definition or an unused charm resolves. */
        [Test]
        public void ContentPairsEachReferencedCharmWithItsCatalogDefinition()
        {
            var catalog = Catalog();
            var first = Modifier("modifier.first");
            var second = Modifier("modifier.second");
            var unreferenced = Modifier("modifier.unreferenced");
            Pool(catalog, new[] { first, second });

            var content = catalog.Build();

            Assert.That(content.GetDefinition(first), Is.SameAs(content.Catalog.GetModifier(first.Id)));
            Assert.That(content.GetDefinition(second), Is.SameAs(content.Catalog.GetModifier(second.Id)));
            Assert.Throws<InvalidOperationException>(() => content.GetDefinition(unreferenced));
        }

        /** Checks a missing charm reference fails the build / fails if a broken reference is skipped. */
        [Test]
        public void CatalogBuildRejectsAMissingCharmReference()
        {
            var catalog = Catalog();
            Pool(catalog, new[] { Modifier("modifier.present"), null });
            Assert.Throws<InvalidOperationException>(() => catalog.Build());
        }

        /** Checks two charm assets with one id fail the build / fails if either silently replaces the other. */
        [Test]
        public void CatalogBuildRejectsTwoCharmAssetsSharingAnId()
        {
            var catalog = Catalog();
            Pool(catalog, new[] { Modifier("modifier.same"), Modifier("modifier.same") });
            Assert.Throws<InvalidOperationException>(() => catalog.Build());
        }

        /** Checks the charms of one family asset share its definition and two family assets with one id fail the build, naming the asset / fails if a family is built per charm or a duplicate id loads. */
        [Test]
        public void CatalogBuildSharesEachFamilyAndRejectsTwoFamilyAssetsSharingAnId()
        {
            var catalog = Catalog();
            var first = Modifier("modifier.first");
            var second = Modifier("modifier.second");
            Pool(catalog, new[] { first, second });
            var built = catalog.Build().Catalog;
            Assert.That(built.GetModifier(second.Id).Family, Is.SameAs(built.GetModifier(first.Id).Family));
            Assert.That(built.Families, Is.EqualTo(new[] { built.GetModifier(first.Id).Family }));

            Pool(catalog, new[] { first, Modifier("modifier.copy", family: Family(FamilyGuid, "family.copy")) });
            var failure = Assert.Throws<InvalidOperationException>(() => catalog.Build());
            Assert.That(failure.Message, Does.Contain("family.copy"));
        }

        /** Checks the catalog holds its listed encounters plus a played encounter it does not list, once each, and two encounter assets with one id fail the build, naming both / fails if a played encounter is missing or repeated or a duplicate id loads. */
        [Test]
        public void CatalogBuildAddsAnUnlistedPlayedEncounterAndRejectsTwoEncounterAssetsSharingAnId()
        {
            var listed = content.Encounter("encounter.listed");
            var unlisted = content.Encounter("encounter.unlisted");
            var catalog = Catalog(listed);
            Assert.That(catalog.Build(listed).Catalog.Encounters.Select(encounter => encounter.Identity), Is.EqualTo(new[] { listed.Identity }));
            Assert.That(catalog.Build(unlisted).Catalog.Encounters.Select(encounter => encounter.Identity),
                Is.EqualTo(new[] { listed.Identity, unlisted.Identity }));

            var copy = content.Encounter("encounter.listed");
            copy.name = "encounter.copy";
            var failure = Assert.Throws<InvalidOperationException>(() => Catalog(listed, copy).Build());
            Assert.That(failure.Message, Does.Contain("encounter.listed").And.Contain("encounter.copy"));
        }

        private static void Pool(MechanicalCatalogAsset catalog, ModifierAsset[] modifiers, ModifierAsset fallback = null)
        {
            using var serialized = new SerializedObject(catalog);
            var pools = serialized.FindProperty("rewardPools");
            pools.arraySize = 1;
            var pool = pools.GetArrayElementAtIndex(0);
            pool.FindPropertyRelative("id").stringValue = "pool.test";
            References(pool.FindPropertyRelative("modifiers"), modifiers);
            pool.FindPropertyRelative("fallbackModifier").objectReferenceValue = fallback;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void References(SerializedProperty list, params ModifierAsset[] modifiers)
        {
            list.arraySize = modifiers.Length;
            for (int i = 0; i < modifiers.Length; ++i) list.GetArrayElementAtIndex(i).objectReferenceValue = modifiers[i];
        }

        /** A charm; without a family it belongs to the one example family of the test. */
        private ModifierAsset Modifier(string id, ModifierAccess access = ModifierAccess.Starter, ModifierFamilyAsset family = null)
        {
            var asset = Create<ModifierAsset>();
            using var serialized = new SerializedObject(asset);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("family").objectReferenceValue = family != null ? family
                : exampleFamily != null ? exampleFamily : exampleFamily = Family(FamilyGuid, "family.example");
            serialized.FindProperty("condition").managedReferenceValue = new AlwaysConditionConfig();
            serialized.FindProperty("access").intValue = (int)access;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private ModifierFamilyAsset Family(string guid, string name)
        {
            var asset = Create<ModifierFamilyAsset>();
            asset.name = name;
            using var serialized = new SerializedObject(asset);
            serialized.FindProperty(ContentAsset.AssetGuidField).stringValue = guid;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        /** A catalog listing the encounters; without any it lists one test encounter. */
        private MechanicalCatalogAsset Catalog(params EncounterAsset[] encounters)
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
            if (encounters.Length == 0) encounters = new[] { content.Encounter("encounter.test") };
            var list = serialized.FindProperty("encounters");
            list.arraySize = encounters.Length;
            for (int i = 0; i < encounters.Length; ++i) list.GetArrayElementAtIndex(i).objectReferenceValue = encounters[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }
    }
}
