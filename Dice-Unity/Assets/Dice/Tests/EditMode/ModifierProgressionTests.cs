using System;
using System.Collections.Generic;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class ModifierProgressionTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        public void ExplicitFamilyDiscoveryGatesMilestoneIndividualsAndKeepsEmptyContinue(int discoveredCount)
        {
            var modifiers = Enumerable.Range(0, 5).Select(i => Modifier("unlock." + i,
                access: ModifierAccess.PermanentUnlock)).ToArray();
            var catalog = Catalog(modifiers, milestones: new[] { new ModifierUnlockMilestone(1) },
                unlockRewardModifierIds: modifiers.Select(value => value.Id));
            var discoveries = modifiers.Take(discoveredCount).Select(value => value.Family.Identity).ToArray();
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 12,
                discoveredFamilyIds: discoveries);
            var result = session.Execute(PlayHandAction.Instance, 0);
            var offer = result.Snapshot.PendingReward;
            Assert.That(offer.Options.Count, Is.EqualTo(Math.Min(3, discoveredCount)));
            Assert.That(offer.Options.All(value => discoveries.Contains(catalog.GetModifier(value.DefinitionId).Family.Identity)), Is.True);
            Assert.That(result.Snapshot.UnlockedModifierIds, Is.Empty);
            var restored = EncounterSession.RestoreEncounter(catalog,
                EncounterSaveCodec.Deserialize(EncounterSaveCodec.Serialize(session.CaptureSave()), catalog));
            Assert.That(restored.Success, Is.True, restored.Detail);
            Assert.That(restored.Session.Snapshot.DiscoveredFamilyIds, Is.EqualTo(discoveries));
            Assert.That(restored.Session.Snapshot.PendingReward.Equals(offer), Is.True);
            EncounterAction choice = discoveredCount == 0 ? (EncounterAction)new AcknowledgeModifierUnlockAction(offer.OfferId) :
                new SelectModifierUnlockAction(offer.OfferId, offer.Options[0].OptionId);
            Assert.That(restored.Session.Execute(choice, restored.Session.Snapshot.Revision).Accepted, Is.True);
            Assert.That(EncounterSession.RestoreEncounter(catalog, restored.Session.CaptureSave()).Success, Is.True);
        }

        [Test]
        public void OwnedIndividualDiscoversItsFamilyAndOnlyUnownedVariantsAreOffered()
        {
            var modifiers = Enumerable.Range(0, 5).Select(i => new ModifierDefinition("unlock." + i, TestFamilies.Of("family.shared"),
                ModifierPhase.PlayHand, new NeverCondition(), Array.Empty<ModifierEffect>(),
                availability: new ModifierAvailabilityDefinition(access: ModifierAccess.PermanentUnlock))).ToArray();
            var catalog = Catalog(modifiers, milestones: new[] { new ModifierUnlockMilestone(1) },
                unlockRewardModifierIds: modifiers.Select(value => value.Id));
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 12,
                new[] { modifiers[0].Id }, Array.Empty<ModifierFamilyDefinition.Id>());
            var snapshot = session.Execute(PlayHandAction.Instance, 0).Snapshot;
            Assert.That(snapshot.DiscoveredFamilyIds, Is.EqualTo(new[] { TestFamilies.IdOf("family.shared") }));
            Assert.That(snapshot.PendingReward.Options.Count, Is.EqualTo(3));
            Assert.That(snapshot.PendingReward.Options.Any(value => value.DefinitionId == modifiers[0].Id), Is.False);
        }

        [Test]
        public void DiscoveryMergeRetainsPendingOfferAndInvalidMergeIsAtomic()
        {
            var modifiers = Enumerable.Range(0, 5).Select(i => Modifier("unlock." + i,
                access: ModifierAccess.PermanentUnlock)).ToArray();
            var catalog = Catalog(modifiers, milestones: new[] { new ModifierUnlockMilestone(1) },
                unlockRewardModifierIds: modifiers.Select(value => value.Id));
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 12,
                discoveredFamilyIds: new[] { modifiers[0].Family.Identity });
            session.Execute(PlayHandAction.Instance, 0);
            var offer = session.Snapshot.PendingReward;
            Assert.Throws<ArgumentException>(() => session.AddDiscoveredFamilies(new[] { modifiers[1].Family.Identity, TestFamilies.IdOf("unknown") }));
            Assert.That(session.Snapshot.DiscoveredFamilyIds, Is.EqualTo(new[] { modifiers[0].Family.Identity }));
            session.AddDiscoveredFamilies(modifiers.Select(value => value.Family.Identity));
            var restored = EncounterSession.RestoreEncounter(catalog,
                EncounterSaveCodec.Deserialize(EncounterSaveCodec.Serialize(session.CaptureSave()), catalog));
            Assert.That(restored.Success, Is.True, restored.Detail);
            Assert.That(restored.Session.Snapshot.PendingReward.Equals(offer), Is.True);
            Assert.That(restored.Session.Snapshot.DiscoveredFamilyIds.Count, Is.EqualTo(5));
            var select = new SelectModifierUnlockAction(offer.OfferId, offer.Options[0].OptionId);
            Assert.That(restored.Session.Execute(select, restored.Session.Snapshot.Revision).Accepted, Is.True);
            Assert.That(EncounterSession.RestoreEncounter(catalog, restored.Session.CaptureSave()).Success, Is.True);
        }

        [Test]
        public void CurrentSaveRejectsUnknownDiscoveryAndUnlockFromAnUndiscoveredFamily()
        {
            var modifiers = new[] { Modifier("visible", access: ModifierAccess.PermanentUnlock),
                Modifier("hidden", access: ModifierAccess.PermanentUnlock) };
            var catalog = Catalog(modifiers, milestones: new[] { new ModifierUnlockMilestone(1) },
                unlockRewardModifierIds: modifiers.Select(value => value.Id));
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 12,
                discoveredFamilyIds: new[] { modifiers[0].Family.Identity });
            session.Execute(PlayHandAction.Instance, 0);
            var save = session.CaptureSave();
            save.DiscoveredFamilyIds = new[] { TestFamilies.IdOf("unknown.family") };
            Assert.That(EncounterSession.RestoreEncounter(catalog, save).Error, Is.EqualTo(EncounterRestoreError.InvalidDefinition));
            save = session.CaptureSave();
            save.PendingReward.Options[0].DefinitionId = modifiers[1].Id;
            Assert.That(EncounterSession.RestoreEncounter(catalog, save).Error, Is.EqualTo(EncounterRestoreError.InvalidState));
            save = session.CaptureSave();
            save.PendingUnlockOfferDiscoveredFamilyIds = null;
            Assert.That(EncounterSession.RestoreEncounter(catalog, save).Error, Is.EqualTo(EncounterRestoreError.InvalidState));
        }

        private static ModifierDefinition Modifier(string id, int tier = 1, ModifierAccess access = ModifierAccess.Starter,
            bool rewardEligible = true)
            => new ModifierDefinition(id, TestFamilies.Of("family." + id), ModifierPhase.PlayHand,
                new NeverCondition(), Array.Empty<ModifierEffect>(),
                rewardEligible: rewardEligible, availability: new ModifierAvailabilityDefinition(tier, access));

        /** The pool of the charms other than the fallback, with that fallback. */
        private static ModifierRewardPoolDefinition Pool(ModifierDefinition[] modifiers, string fallback = null)
            => new ModifierRewardPoolDefinition("pool.ordinary",
                modifiers.Where(modifier => modifier.Id != fallback).Select(modifier => modifier.Id), fallback);

        private static RewardPolicyDefinition Policy(ModifierRewardPoolDefinition pool, int slots = 1,
            IEnumerable<ModifierTierRule> rules = null, IEnumerable<ModifierTierMilestone> milestones = null)
            => new RewardPolicyDefinition(new[] { new ModifierRewardLayout(Enumerable.Repeat(pool, slots)) },
                2, 1, 1, 2, new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) }, rules, milestones);

        /** A catalog of the charms; without a policy, one slot draws from their pool with the fallback. */
        private static MechanicalCatalog Catalog(ModifierDefinition[] modifiers, RewardPolicyDefinition policy = null,
            IEnumerable<ModifierUnlockMilestone> milestones = null, string fallback = null, int stages = 2,
            IEnumerable<string> encounterModifierIds = null, IEnumerable<string> unlockRewardModifierIds = null)
            => new MechanicalCatalog("progression.tests", TestContent.Hands(), new[]
            {
                new EncounterDefinition(TestContent.EncounterId, Enumerable.Range(0, stages)
                    .Select(_ => new StageDefinition(1, 2, 1, StageExitBehavior.AwaitRewards)),
                    rewardPolicy: policy ?? Policy(Pool(modifiers, fallback)), unlockMilestones: milestones,
                    encounterModifierIds: encounterModifierIds, unlockRewardModifierIds: unlockRewardModifierIds)
            }, modifiers);

        private static ModifierRewardGeneration Generate(MechanicalCatalog catalog, ulong seed = 0,
            IEnumerable<string> unlocked = null, IEnumerable<string> owned = null, IEnumerable<string> history = null, int incomingStage = 2)
        {
            var encounter = catalog.GetEncounter(TestContent.EncounterId);
            var random = new DeterministicRandom(seed);
            return RewardGenerator.GenerateModifiers(catalog, encounter.RewardPolicy, incomingStage - 1,
                owned ?? Array.Empty<string>(), (history ?? Array.Empty<string>()).Select(TestFamilies.IdOf), ref random, encounter, unlocked);
        }

        [Test]
        public void SmallPoolHistoryBypassAndFallbackNeverExposeLockedModifiers()
        {
            var modifiers = new[] { Modifier("allowed"), Modifier("locked", access: ModifierAccess.PermanentUnlock),
                Modifier("fallback", access: ModifierAccess.PermanentUnlock, rewardEligible: false) };
            var catalog = Catalog(modifiers, fallback: "fallback");
            Assert.That(Generate(catalog, history: new[] { "family.allowed" }).ModifierIds, Is.EqualTo(new[] { "allowed" }));
            Assert.That(Generate(catalog, owned: new[] { "allowed" }).ModifierIds, Is.Empty);
            Assert.That(Generate(catalog, owned: new[] { "allowed" }, unlocked: new[] { "fallback" }).ModifierIds,
                Is.EqualTo(new[] { "fallback" }));
        }

        /** Checks an encounter-exclusive charm in a reward pool fails loading, naming the charm and the pool / fails if the catalog accepts it or the message omits either. */
        [Test]
        public void CatalogRejectsAnEncounterExclusiveCharmInAPoolNamingIt()
        {
            var exception = Assert.Throws<ArgumentException>(() => Catalog(new[] { Modifier("lantern", access: ModifierAccess.EncounterExclusive) }));
            Assert.That(exception.Message, Does.Contain("lantern").And.Contain("pool.ordinary"));
        }

        [Test]
        public void TierWeightsSelectTiersWithoutPopulationBias()
        {
            var modifiers = Enumerable.Range(0, 9).Select(i => Modifier("weak." + i)).Concat(new[] { Modifier("strong", 2) }).ToArray();
            var catalog = Catalog(modifiers, Policy(Pool(modifiers), rules: new[] { new ModifierTierRule(1, new[] { 1, 1 }) }));
            int strongCount = 0;
            for (ulong seed = 0; seed < 1000; ++seed)
                if (Generate(catalog, seed).ModifierIds[0] == "strong") ++strongCount;
            Assert.That(strongCount, Is.InRange(400, 600));
        }

        [Test]
        public void TierScheduleUsesIncomingStageAndMilestonesReserveStrongSlotsBeforeHistory()
        {
            var modifiers = new[] { Modifier("weak.0"), Modifier("weak.1"), Modifier("weak.2"),
                Modifier("strong.0", 3), Modifier("strong.1", 3) };
            var policy = Policy(Pool(modifiers), 3, new[] { new ModifierTierRule(1, new[] { 100, 0, 0 }), new ModifierTierRule(4, new[] { 0, 0, 100 }) },
                new[] { new ModifierTierMilestone(3, 3) });
            var catalog = Catalog(modifiers, policy);
            Assert.That(Generate(catalog, incomingStage: 2).ModifierIds.All(id => id.StartsWith("weak.")), Is.True);
            var offer = Generate(catalog, history: new[] { "family.strong.0", "family.strong.1" }, incomingStage: 3).ModifierIds;
            Assert.That(offer.Take(2).All(id => id.StartsWith("strong.")), Is.True);
            Assert.That(offer.Count, Is.EqualTo(3));
            Assert.That(offer[2], Does.StartWith("weak."));
            Assert.That(Generate(catalog, incomingStage: 4).ModifierIds.All(id => id.StartsWith("strong.")), Is.True);
            Assert.That(Generate(catalog, owned: new[] { "strong.0", "strong.1" }, incomingStage: 4).ModifierIds, Is.Empty);
        }

        [Test]
        public void TierFilteringAppliesToFallbackAndHistoryBypass()
        {
            var modifiers = new[] { Modifier("weak"), Modifier("strong", 2), Modifier("fallback", rewardEligible: false) };
            var catalog = Catalog(modifiers, Policy(Pool(modifiers, "fallback"), rules: new[] { new ModifierTierRule(1, new[] { 0, 1 }) }));
            Assert.That(Generate(catalog, history: new[] { "family.strong" }).ModifierIds, Is.EqualTo(new[] { "strong" }));
            Assert.That(Generate(catalog, owned: new[] { "strong" }).ModifierIds, Is.Empty);
        }

        [Test]
        public void ClearedMilestoneUnlocksBeforeOffersAndFinalCompletionWithoutDuplicateEvents()
        {
            var modifiers = new[] { Modifier("first", 2, access: ModifierAccess.PermanentUnlock),
                Modifier("final", access: ModifierAccess.PermanentUnlock) };
            var catalog = Catalog(modifiers, milestones: new[] { new ModifierUnlockMilestone(1), new ModifierUnlockMilestone(2) },
                unlockRewardModifierIds: new[] { "first", "final" });
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 3);
            var clear = session.Execute(PlayHandAction.Instance, 0);
            Assert.That(clear.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingUnlock));
            Assert.That(clear.Snapshot.PendingReward.Options.Select(option => option.DefinitionId), Is.EquivalentTo(new[] { "first", "final" }));
            Assert.That(clear.Events.OfType<ModifierUnlockedEvent>(), Is.Empty);
            var unlock = clear.Snapshot.PendingReward;
            clear = session.Execute(new SelectModifierUnlockAction(unlock.OfferId,
                unlock.Options.Single(value => value.DefinitionId == "first").OptionId), session.Snapshot.Revision);
            Assert.That(clear.Events.OfType<ModifierUnlockedEvent>().Single().ModifierId, Is.EqualTo("first"));
            Assert.That(clear.Snapshot.Modifiers, Is.Empty);
            var offer = session.Snapshot.PendingReward;
            session.Execute(new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId), session.Snapshot.Revision);
            offer = session.Snapshot.PendingReward;
            session.Execute(new SelectHandUpgradeAction(offer.OfferId, offer.Options[0].OptionId), session.Snapshot.Revision);
            var final = session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
            Assert.That(final.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.None));
            unlock = final.Snapshot.PendingReward;
            Assert.That(unlock.Options.Single().DefinitionId, Is.EqualTo("final"));
            var pendingFinal = EncounterSession.RestoreEncounter(catalog, session.CaptureSave());
            Assert.That(pendingFinal.Success, Is.True, pendingFinal.Detail);
            Assert.That(pendingFinal.Session.Snapshot.PendingReward.Equals(unlock), Is.True);
            final = session.Execute(new SelectModifierUnlockAction(unlock.OfferId, unlock.Options[0].OptionId), session.Snapshot.Revision);
            Assert.That(final.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(final.Snapshot.NewlyUnlockedModifierIds, Is.EqualTo(new[] { "first", "final" }));
            Assert.That(final.Events.OfType<ModifierUnlockedEvent>().Single().ModifierId, Is.EqualTo("final"));
            var restored = EncounterSession.RestoreEncounter(catalog, session.CaptureSave());
            Assert.That(restored.Success, Is.True, restored.Detail);
            Assert.That(restored.Session.Snapshot.NewlyUnlockedModifierIds, Is.EqualTo(new[] { "first", "final" }));
            var replay = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 3, final.Snapshot.UnlockedModifierIds);
            Assert.That(replay.Execute(PlayHandAction.Instance, 0).Events.OfType<ModifierUnlockedEvent>(), Is.Empty);
        }

        [Test]
        public void PendingOffersSurviveProfileMergeAndResumeWithDeterministicRefreshes()
        {
            var modifiers = new[] { Modifier("weak"), Modifier("strong", 2, access: ModifierAccess.PermanentUnlock) };
            var catalog = Catalog(modifiers, Policy(Pool(modifiers), milestones: new[] { new ModifierTierMilestone(2, 2) }));
            var original = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 7);
            original.Execute(PlayHandAction.Instance, 0);
            var pending = original.Snapshot.PendingReward;
            original.AddUnlockedModifiers(new[] { "strong" });
            Assert.That(original.Snapshot.PendingReward, Is.SameAs(pending));
            Assert.That(original.Snapshot.NewlyUnlockedModifierIds, Is.Empty);
            var bytes = EncounterSaveCodec.Serialize(original.CaptureSave());
            var restored = EncounterSession.RestoreEncounter(catalog, EncounterSaveCodec.Deserialize(bytes, catalog));
            Assert.That(restored.Success, Is.True, restored.Detail);
            Assert.That(restored.Session.Snapshot.PendingReward.Options[0].DefinitionId, Is.EqualTo("weak"));
            var first = original.Execute(new RefreshModifierOfferAction(pending.OfferId), original.Snapshot.Revision);
            var second = restored.Session.Execute(new RefreshModifierOfferAction(pending.OfferId), restored.Session.Snapshot.Revision);
            Assert.That(first.Snapshot.PendingReward.Options.Select(option => option.DefinitionId), Is.EqualTo(new[] { "strong" }));
            Assert.That(EncounterSaveCodec.Serialize(restored.Session.CaptureSave()), Is.EqualTo(EncounterSaveCodec.Serialize(original.CaptureSave())));
            Assert.That(second.Accepted, Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(7)]
        public void UnlockOfferUsesDistinctUnownedIndividualsAndRequiresExplicitChoice(int remaining)
        {
            var modifiers = Enumerable.Range(0, 8).Select(i => new ModifierDefinition("charm." + i, TestFamilies.Of("shared.family"),
                ModifierPhase.PlayHand, new NeverCondition(), Array.Empty<ModifierEffect>(),
                availability: new ModifierAvailabilityDefinition(access: ModifierAccess.PermanentUnlock))).ToArray();
            var catalog = Catalog(modifiers, milestones: new[] { new ModifierUnlockMilestone(1) },
                unlockRewardModifierIds: modifiers.Select(value => value.Id));
            var owned = modifiers.Take(8 - remaining).Select(value => value.Id).ToArray();
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 14, owned);
            var clear = session.Execute(PlayHandAction.Instance, 0);
            var offer = clear.Snapshot.PendingReward;
            Assert.That(offer.Kind, Is.EqualTo(RewardKind.ModifierUnlock));
            Assert.That(offer.Options.Count, Is.EqualTo(Math.Min(3, remaining)));
            Assert.That(offer.Options.Select(value => value.DefinitionId).Distinct().Count(), Is.EqualTo(offer.Options.Count));
            Assert.That(offer.Options.All(value => !owned.Contains(value.DefinitionId)), Is.True);
            Assert.That(clear.Snapshot.UnlockedModifierIds, Is.EqualTo(owned));
            Assert.That(session.Execute(new RefreshModifierOfferAction(offer.OfferId), session.Snapshot.Revision).Rejection,
                Is.EqualTo(CommandRejection.InvalidPhase));
            var bytes = EncounterSaveCodec.Serialize(session.CaptureSave());
            var restored = EncounterSession.RestoreEncounter(catalog, EncounterSaveCodec.Deserialize(bytes, catalog));
            Assert.That(restored.Success, Is.True, restored.Detail);
            Assert.That(EncounterSaveCodec.Serialize(restored.Session.CaptureSave()), Is.EqualTo(bytes));
            EncounterAction action = remaining == 0 ? (EncounterAction)new AcknowledgeModifierUnlockAction(offer.OfferId) :
                new SelectModifierUnlockAction(offer.OfferId, offer.Options[0].OptionId);
            var result = session.Execute(action, session.Snapshot.Revision);
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Snapshot.UnlockedModifierIds.Count, Is.EqualTo(owned.Length + (remaining == 0 ? 0 : 1)));
            Assert.That(result.Snapshot.Modifiers, Is.Empty);
        }

        [Test]
        public void UnlockOfferSurvivesProfileMergeWithoutRerollingOrDuplicatingProgress()
        {
            var modifiers = Enumerable.Range(0, 4).Select(i => Modifier("unlock." + i, access: ModifierAccess.PermanentUnlock)).ToArray();
            var catalog = Catalog(modifiers, milestones: new[] { new ModifierUnlockMilestone(1) },
                unlockRewardModifierIds: modifiers.Select(value => value.Id));
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 8);
            session.Execute(PlayHandAction.Instance, 0);
            var offer = session.Snapshot.PendingReward;
            session.AddUnlockedModifiers(new[] { offer.Options[0].DefinitionId });
            var restored = EncounterSession.RestoreEncounter(catalog, EncounterSaveCodec.Deserialize(EncounterSaveCodec.Serialize(session.CaptureSave()), catalog));
            Assert.That(restored.Success, Is.True, restored.Detail);
            Assert.That(restored.Session.Snapshot.PendingReward.Equals(offer), Is.True);
            var chosen = restored.Session.Execute(new SelectModifierUnlockAction(offer.OfferId, offer.Options[0].OptionId),
                restored.Session.Snapshot.Revision);
            Assert.That(chosen.Accepted, Is.True);
            Assert.That(chosen.Events.OfType<ModifierUnlockedEvent>(), Is.Empty);
            Assert.That(chosen.Snapshot.UnlockedModifierIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void EncounterAdditionsJoinOrdinaryPoolAndKeepTierRulesAndSavedEligibility()
        {
            var starter = Modifier("starter");
            var addition = Modifier("addition", 2, ModifierAccess.EncounterExclusive);
            var policy = Policy(Pool(new[] { starter }), rules: new[] { new ModifierTierRule(1, new[] { 0, 1 }) });
            var encounter = new EncounterDefinition(TestContent.EncounterId,
                new[] { new StageDefinition(1, 2, 1, StageExitBehavior.AwaitRewards), new StageDefinition(1, 2, 1) },
                rewardPolicy: policy, encounterModifierIds: new[] { addition.Id });
            var catalog = new MechanicalCatalog("additions", TestContent.Hands(), new[] { encounter }, new[] { starter, addition });
            Assert.That(Generate(catalog).ModifierIds, Is.EqualTo(new[] { addition.Id }));
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 3);
            session.Execute(PlayHandAction.Instance, 0);
            var restored = EncounterSession.RestoreEncounter(catalog, session.CaptureSave());
            Assert.That(restored.Success, Is.True, restored.Detail);
            var offer = restored.Session.Snapshot.PendingReward;
            restored.Session.Execute(new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId), restored.Session.Snapshot.Revision);
            Assert.That(EncounterSession.RestoreEncounter(catalog, restored.Session.CaptureSave()).Success, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FinalUnlockWithoutOrdinaryRewardsCanResumeBeforeCompleting(bool collected)
        {
            var modifier = Modifier("last", access: ModifierAccess.PermanentUnlock);
            var encounter = new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1, 1, 0) },
                unlockMilestones: new[] { new ModifierUnlockMilestone(1) },
                unlockRewardModifierIds: new[] { modifier.Id });
            var catalog = new MechanicalCatalog("final.unlock", TestContent.Hands(), new[] { encounter }, new[] { modifier });
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 4,
                collected ? new[] { modifier.Id } : Array.Empty<string>());
            session.Execute(PlayHandAction.Instance, 0);
            Assert.That(session.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.None));
            var restored = EncounterSession.RestoreEncounter(catalog,
                EncounterSaveCodec.Deserialize(EncounterSaveCodec.Serialize(session.CaptureSave()), catalog));
            Assert.That(restored.Success, Is.True, restored.Detail);
            var offer = restored.Session.Snapshot.PendingReward;
            EncounterAction action = collected ? (EncounterAction)new AcknowledgeModifierUnlockAction(offer.OfferId) :
                new SelectModifierUnlockAction(offer.OfferId, offer.Options[0].OptionId);
            var result = restored.Session.Execute(action, restored.Session.Snapshot.Revision);
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(EncounterSession.RestoreEncounter(catalog, restored.Session.CaptureSave()).Success, Is.True);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void AbandonPendingUnlockRetainsRestorableTerminalState(int stages)
        {
            var modifiers = new[] { Modifier("unlock", access: ModifierAccess.PermanentUnlock) };
            var policy = new RewardPolicyDefinition(new[] { new ModifierRewardLayout(new[] { Pool(modifiers) }) },
                0, 1, 2, 1, new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) });
            var catalog = Catalog(modifiers, policy,
                new[] { new ModifierUnlockMilestone(1) }, stages: stages, unlockRewardModifierIds: new[] { "unlock" });
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 2);
            session.Execute(PlayHandAction.Instance, 0);
            session.Execute(AbandonEncounterAction.Instance, session.Snapshot.Revision);
            var restored = EncounterSession.RestoreEncounter(catalog, session.CaptureSave());
            Assert.That(restored.Success, Is.True, restored.Detail);
            Assert.That(restored.Session.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Abandoned));
            Assert.That(restored.Session.Snapshot.UnlockedModifierIds, Is.Empty);
        }

        [Test]
        public void NewSaveRejectsIneligiblePendingChoiceAndInvalidEarnedUnlocks()
        {
            var modifiers = new[] { Modifier("starter"), Modifier("locked", access: ModifierAccess.PermanentUnlock) };
            var catalog = Catalog(modifiers);
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 9);
            session.Execute(PlayHandAction.Instance, 0);
            var save = session.CaptureSave();
            save.PendingReward.Options[0].DefinitionId = "locked";
            Assert.That(EncounterSession.RestoreEncounter(catalog, save).Error, Is.EqualTo(EncounterRestoreError.InvalidState));
            save = session.CaptureSave();
            save.UnlockedModifierIds = new[] { "locked" };
            save.NewlyUnlockedModifierIds = new[] { "locked" };
            Assert.That(EncounterSession.RestoreEncounter(catalog, save).Error, Is.EqualTo(EncounterRestoreError.InvalidState));
            var document = JObject.Parse(System.Text.Encoding.UTF8.GetString(EncounterSaveCodec.Serialize(session.CaptureSave())));
            document.Remove(nameof(EncounterSaveData.UnlockedModifierIds));
            Assert.Throws<FormatException>(() => EncounterSaveCodec.Deserialize(System.Text.Encoding.UTF8.GetBytes(document.ToString()), catalog));
        }

        [Test]
        public void CatalogRejectsNonPermanentUnlockRewardsAndMilestonesBeyondTheEncounter()
        {
            var milestone = new[] { new ModifierUnlockMilestone(1) };
            var unlocks = new[] { "charm" };
            Assert.Throws<ArgumentException>(() => Catalog(new[] { Modifier("charm") }, milestones: milestone,
                unlockRewardModifierIds: unlocks));
            Assert.Throws<ArgumentException>(() => Catalog(new[] { Modifier("charm", access: ModifierAccess.PermanentUnlock) },
                milestones: new[] { new ModifierUnlockMilestone(3) }, unlockRewardModifierIds: unlocks));
        }

        [Test]
        public void FailedRewardResolutionAndInvalidProfileMergeDoNotCommitUnlocks()
        {
            var modifiers = new[] { Modifier("locked", access: ModifierAccess.PermanentUnlock), Modifier("starter") };
            var policy = new RewardPolicyDefinition(new[] { new ModifierRewardLayout(new[] { Pool(modifiers) }) },
                int.MaxValue, 1, 1, 2, new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) });
            var catalog = Catalog(modifiers, policy, new[] { new ModifierUnlockMilestone(1) }, unlockRewardModifierIds: new[] { "locked" });
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 5);
            session.Execute(PlayHandAction.Instance, 0);
            var offer = session.Snapshot.PendingReward;
            Assert.Throws<OverflowException>(() => session.Execute(new SelectModifierUnlockAction(offer.OfferId,
                offer.Options[0].OptionId), session.Snapshot.Revision));
            Assert.That(session.Snapshot.Revision, Is.EqualTo(1));
            Assert.That(session.Snapshot.UnlockedModifierIds, Is.Empty);
            Assert.That(session.Snapshot.NewlyUnlockedModifierIds, Is.Empty);
            Assert.Throws<ArgumentException>(() => session.AddUnlockedModifiers(new[] { "locked", "starter" }));
            Assert.That(session.Snapshot.UnlockedModifierIds, Is.Empty);
        }
    }
}
