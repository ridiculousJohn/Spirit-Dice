using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class ProgressionRewardAssetTests
    {
        private const string FamilyId = "family.story";
        private const string ModifierId = "modifier.story.base";
        private const string EncounterId = "encounter.story";
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private readonly SyntheticCharms charms = new SyntheticCharms();
        private MechanicalCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            var modifier = new ModifierDefinition(ModifierId, FamilyId, ModifierPhase.PlayHand,
                new AlwaysCondition(), new ModifierEffect[] { new AddBaseScoreEffect(1) },
                availability: new ModifierAvailabilityDefinition(access: ModifierAccess.PermanentUnlock));
            catalog = new MechanicalCatalog("test.progression-reward-assets", TestContent.Hands(),
                new[] { new EncounterDefinition(EncounterId, new[] { new StageDefinition(100, 3, 2) }) },
                new[] { modifier });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();
            charms.Dispose();
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void BuildKeepsDiscoveryAndConcreteGrantsIndependent(bool discovers, bool grants)
        {
            string[] families = discovers ? new[] { FamilyId } : Array.Empty<string>();
            string[] modifiers = grants ? new[] { ModifierId } : Array.Empty<string>();
            var reward = Reward(families, modifiers);
            var definition = reward.Build(catalog);

            Assert.That(definition.DiscoveredFamilyIds, Is.EqualTo(families));
            Assert.That(definition.GrantedModifierIds, Is.EqualTo(modifiers));
            if (discovers) families[0] = "family.changed";
            if (grants) modifiers[0] = "modifier.changed";
            Assert.That(definition.DiscoveredFamilyIds, Is.EqualTo(discovers ? new[] { FamilyId } : Array.Empty<string>()));
            Assert.That(definition.GrantedModifierIds, Is.EqualTo(grants ? new[] { ModifierId } : Array.Empty<string>()));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BuildRejectsUnknownFamilyOrCharm(bool unknownFamily)
        {
            var reward = Reward(new[] { unknownFamily ? "family.missing" : FamilyId },
                new[] { unknownFamily ? ModifierId : "modifier.missing" });
            Assert.Throws<ArgumentException>(() => reward.Build(catalog));
        }

        /** Checks a reward states it grants a charm it references and not one it does not, and a missing reference fails / fails if the answer ignores the references or a missing one is skipped. */
        [Test]
        public void RewardStatesWhetherItGrantsACharmFromItsReferences()
        {
            var reward = Reward(Array.Empty<string>(), new[] { ModifierId });
            Assert.That(reward.GrantsModifier(ModifierId), Is.True);
            Assert.That(reward.GrantsModifier("modifier.other"), Is.False);
            Set(reward, "grantedModifiers", new ModifierAsset[] { null });
            Assert.Throws<InvalidOperationException>(() => reward.GrantsModifier(ModifierId));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void JourneyValidatesBeatAndCompletionRewards(bool completion)
        {
            var journey = Journey();
            Assert.That(journey.Beats[0].ProgressionReward, Is.Null);
            Assert.That(journey.CompletionReward, Is.Null);
            Assert.DoesNotThrow(() => journey.Validate(catalog));
            var reward = Reward(new[] { FamilyId }, new[] { ModifierId });
            Set(completion ? (object)journey : journey.Beats[0], completion ? "completionReward" : "progressionReward", reward);
            Assert.That(completion ? journey.CompletionReward : journey.Beats[0].ProgressionReward, Is.SameAs(reward));
            Assert.DoesNotThrow(() => journey.Validate(catalog));

            Set(reward, "grantedModifiers", new[] { charms.Charm("modifier.missing") });
            Assert.Throws<ArgumentException>(() => journey.Validate(catalog));
        }

        private ProgressionRewardAsset Reward(string[] families, string[] modifiers)
        {
            var reward = Create<ProgressionRewardAsset>();
            Set(reward, "discoveredFamilyIds", families);
            Set(reward, "grantedModifiers", modifiers.Select(id => charms.Charm(id)).ToArray());
            return reward;
        }

        private EncounterJourneyAsset Journey()
        {
            var journey = Create<EncounterJourneyAsset>();
            Set(journey, "encounterId", EncounterId);
            Set(journey, "locationTitle", "Story table");
            Set(journey, "spiritName", "Story keeper");
            Set(journey, "leadText", "A keeper waits at the table.");
            Set(journey, "tablePresentation", Create<EncounterTablePresentationAsset>());
            var beat = new EncounterJourneyBeat();
            Set(beat, "id", "invitation");
            Set(beat, "kind", EncounterJourneyBeatKind.Invitation);
            Set(journey, "beats", new[] { beat });
            return journey;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }

        private static void Set(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
