using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class ProgressionRewardAssetTests
    {
        private const string FamilyId = "family.story";
        private const string ModifierId = "modifier.story.base";
        private static readonly EncounterDefinition.Id EncounterId = TestEncounters.IdOf("encounter.story");
        private static readonly ItemDefinition.Id ItemId = new ItemDefinition.Id(new Guid("c2e85a1f7b3d4096a4e1f8d2b6c07e39"));
        private static readonly ItemDefinition.Id MissingItemId = new ItemDefinition.Id(new Guid("5d9f0b3e8a2c47e1b6d4a0f7c3e19b82"));
        private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        private readonly SyntheticCharms charms = new SyntheticCharms();
        private MechanicalCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            var modifier = new ModifierDefinition(TestModifiers.IdOf(ModifierId), TestFamilies.Of(FamilyId), ModifierPhase.PlayHand,
                new AlwaysCondition(), new ModifierEffect[] { new AddBaseScoreEffect(1) },
                availability: new ModifierAvailabilityDefinition(requiresUnlock: true));
            catalog = new MechanicalCatalog("test.progression-reward-assets", TestContent.Hands(),
                new[] { new EncounterDefinition(EncounterId, new[] { new StageDefinition(100, 3, 2) }) },
                new[] { modifier },
                new[] { new ItemDefinition(ItemId, piece: new TablePieceDefinition(TestPieces.IdOf("piece.story"))) });
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

            var discovered = families.Select(TestFamilies.IdOf).ToArray();
            Assert.That(definition.DiscoveredFamilyIds, Is.EqualTo(discovered));
            Assert.That(definition.GrantedModifierIds, Is.EqualTo(modifiers.Select(TestModifiers.IdOf)));
            if (grants) modifiers[0] = "modifier.changed";
            Assert.That(definition.DiscoveredFamilyIds, Is.EqualTo(discovered));
            Assert.That(definition.GrantedModifierIds, Is.EqualTo(grants ? TestModifiers.IdsOf(ModifierId) : Array.Empty<ModifierDefinition.Id>()));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void BuildRejectsUnknownFamilyOrCharm(bool unknownFamily)
        {
            var reward = Reward(new[] { unknownFamily ? "family.missing" : FamilyId },
                new[] { unknownFamily ? ModifierId : "modifier.missing" });
            Assert.Throws<ArgumentException>(() => reward.Build(catalog));
        }

        [Test]
        public void BuildPassesTheGrantedItems()
        {
            var reward = Reward(Array.Empty<string>(), Array.Empty<string>());
            SyntheticUi.Set(reward, "grantedItems", new[] { Item("item.story", ItemId) });
            Assert.That(reward.Build(catalog).GrantedItemIds, Is.EqualTo(new[] { ItemId }));
        }

        [Test]
        public void BuildRejectsUnknownItem()
        {
            var reward = Reward(new[] { FamilyId }, new[] { ModifierId });
            SyntheticUi.Set(reward, "grantedItems", new[] { Item("item.missing", MissingItemId) });
            Assert.Throws<ArgumentException>(() => reward.Build(catalog));
        }

        /** Checks a reward states it grants a charm it references and not one it does not, and a missing reference fails / fails if the answer ignores the references or a missing one is skipped. */
        [Test]
        public void RewardStatesWhetherItGrantsACharmFromItsReferences()
        {
            var reward = Reward(Array.Empty<string>(), new[] { ModifierId });
            Assert.That(reward.GrantsModifier(TestModifiers.IdOf(ModifierId)), Is.True);
            Assert.That(reward.GrantsModifier(TestModifiers.IdOf("modifier.other")), Is.False);
            SyntheticUi.Set(reward, "grantedModifiers", new ModifierAsset[] { null });
            Assert.Throws<InvalidOperationException>(() => reward.GrantsModifier(TestModifiers.IdOf(ModifierId)));
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
            if (completion) SyntheticUi.Set(journey, "completionReward", reward);
            else TestJourneys.SetBeat(journey, 0, "progressionReward", reward);
            Assert.That(completion ? journey.CompletionReward : journey.Beats[0].ProgressionReward, Is.SameAs(reward));
            Assert.DoesNotThrow(() => journey.Validate(catalog));

            SyntheticUi.Set(reward, "grantedModifiers", new[] { charms.Charm("modifier.missing") });
            Assert.Throws<ArgumentException>(() => journey.Validate(catalog));
        }

        private ProgressionRewardAsset Reward(string[] families, string[] modifiers)
        {
            var reward = Create<ProgressionRewardAsset>();
            SyntheticUi.Set(reward, "discoveredFamilies", families.Select(charms.Family).ToArray());
            SyntheticUi.Set(reward, "grantedModifiers", modifiers.Select(id => charms.Charm(id)).ToArray());
            return reward;
        }

        private ItemAsset Item(string name, ItemDefinition.Id id)
        {
            var item = Create<ItemAsset>();
            item.name = name;
            SyntheticUi.Set(item, ContentAsset.AssetGuidField, id.Value.ToString("N"));
            return item;
        }

        private EncounterJourneyAsset Journey()
        {
            var journey = Create<EncounterJourneyAsset>();
            SyntheticUi.Set(journey, "locationTitle", "Story table");
            SyntheticUi.Set(journey, "spiritName", "Story keeper");
            SyntheticUi.Set(journey, "leadText", "A keeper waits at the table.");
            TestJourneys.SetEncounterBeats(journey, TestJourneys.WithTray(Create<EncounterTablePresentationAsset>(),
                Create<SpiritTrayPresentationAsset>()), charms.Encounter("encounter.story"));
            return journey;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }
    }
}
