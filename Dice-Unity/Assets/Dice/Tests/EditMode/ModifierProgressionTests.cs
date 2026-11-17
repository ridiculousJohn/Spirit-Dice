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
                requiresUnlock: true)).ToArray();
            var catalog = Catalog(modifiers, milestones: new[] { new ModifierUnlockMilestone(1) },
                unlockRewardModifierIds: modifiers.Select(value => value.Identity));
            var discoveries = modifiers.Take(discoveredCount).Select(value => value.Family.Identity).ToArray();
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 12,
                discoveredFamilyIds: discoveries);
            var result = session.Execute(PlayHandAction.Instance, 0);
            var offer = result.Snapshot.PendingReward;
            Assert.That(offer.Options.Count, Is.EqualTo(Math.Min(3, discoveredCount)));
            Assert.That(offer.Options.All(value => discoveries.Contains(catalog.GetModifier(value.DefinitionId.Value).Family.Identity)), Is.True);
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
            var modifiers = Enumerable.Range(0, 5).Select(i => new ModifierDefinition(TestModifiers.IdOf("unlock." + i), TestFamilies.Of("family.shared"),
                ModifierPhase.PlayHand, new NeverCondition(), Array.Empty<ModifierEffect>(),
                availability: new ModifierAvailabilityDefinition(requiresUnlock: true))).ToArray();
            var catalog = Catalog(modifiers, milestones: new[] { new ModifierUnlockMilestone(1) },
                unlockRewardModifierIds: modifiers.Select(value => value.Identity));
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 12,
                new[] { modifiers[0].Identity }, Array.Empty<ModifierFamilyDefinition.Id>());
            var snapshot = session.Execute(PlayHandAction.Instance, 0).Snapshot;
            Assert.That(snapshot.DiscoveredFamilyIds, Is.EqualTo(new[] { TestFamilies.IdOf("family.shared") }));
            Assert.That(snapshot.PendingReward.Options.Count, Is.EqualTo(3));
            Assert.That(snapshot.PendingReward.Options.Any(value => value.DefinitionId == modifiers[0].Identity), Is.False);
        }

        [Test]
        public void DiscoveryMergeRetainsPendingOfferAndInvalidMergeIsAtomic()
        {
            var modifiers = Enumerable.Range(0, 5).Select(i => Modifier("unlock." + i,
                requiresUnlock: true)).ToArray();
            var catalog = Catalog(modifiers, milestones: new[] { new ModifierUnlockMilestone(1) },
                unlockRewardModifierIds: modifiers.Select(value => value.Identity));
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
        public void CurrentSaveRejectsUnknownDiscovery()
        {
            var modifiers = new[] { Modifier("visible", requiresUnlock: true),
                Modifier("hidden", requiresUnlock: true) };
            var catalog = Catalog(modifiers, milestones: new[] { new ModifierUnlockMilestone(1) },
                unlockRewardModifierIds: modifiers.Select(value => value.Identity));
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 12,
                discoveredFamilyIds: new[] { modifiers[0].Family.Identity });
            session.Execute(PlayHandAction.Instance, 0);
            var save = session.CaptureSave();
            save.DiscoveredFamilyIds = new[] { TestFamilies.IdOf("unknown.family") };
            Assert.That(EncounterSession.RestoreEncounter(catalog, save).Error, Is.EqualTo(EncounterRestoreError.InvalidDefinition));
        }

        private static ModifierDefinition Modifier(string id, int tier = 1, bool requiresUnlock = false,
            bool rewardEligible = true)
            => new ModifierDefinition(TestModifiers.IdOf(id), TestFamilies.Of("family." + id), ModifierPhase.PlayHand,
                new NeverCondition(), Array.Empty<ModifierEffect>(),
                rewardEligible: rewardEligible, availability: new ModifierAvailabilityDefinition(tier, requiresUnlock));

        /** The pool of the charms other than the fallback, with that fallback. */
        private static ModifierRewardPoolDefinition Pool(ModifierDefinition[] modifiers, string fallback = null)
        {
            ModifierDefinition.Id? fallbackId = fallback == null ? (ModifierDefinition.Id?)null : TestModifiers.IdOf(fallback);
            return new ModifierRewardPoolDefinition("pool.ordinary",
                modifiers.Where(modifier => modifier.Identity != fallbackId).Select(modifier => modifier.Identity), fallbackId);
        }

        private static RewardPolicyDefinition Policy(ModifierRewardPoolDefinition pool, int slots = 1,
            IEnumerable<ModifierTierRule> rules = null, IEnumerable<ModifierTierMilestone> milestones = null)
            => new RewardPolicyDefinition(new[] { new ModifierRewardLayout(Enumerable.Repeat(pool, slots)) },
                2, 1, 1, 2, new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) }, rules, milestones);

        /** A catalog of the charms; without a policy, one slot draws from their pool with the fallback. */
        private static MechanicalCatalog Catalog(ModifierDefinition[] modifiers, RewardPolicyDefinition policy = null,
            IEnumerable<ModifierUnlockMilestone> milestones = null, string fallback = null, int stages = 2,
            IEnumerable<ModifierDefinition.Id> encounterModifierIds = null, IEnumerable<ModifierDefinition.Id> unlockRewardModifierIds = null)
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
            var inPlay = catalog.GetEncounterInPlay(TestContent.EncounterId, Array.Empty<ItemDefinition.Id>());
            var random = new DeterministicRandom(seed);
            return RewardGenerator.GenerateModifiers(catalog, inPlay.Encounter.RewardPolicy, incomingStage - 1,
                (owned ?? Array.Empty<string>()).Select(TestModifiers.IdOf), (history ?? Array.Empty<string>()).Select(TestFamilies.IdOf),
                ref random, inPlay, unlocked?.Select(TestModifiers.IdOf));
        }

        [Test]
        public void SmallPoolHistoryBypassAndFallbackNeverExposeLockedModifiers()
        {
            var modifiers = new[] { Modifier("allowed"), Modifier("locked", requiresUnlock: true),
                Modifier("fallback", requiresUnlock: true, rewardEligible: false) };
            var catalog = Catalog(modifiers, fallback: "fallback");
            Assert.That(Generate(catalog, history: new[] { "family.allowed" }).ModifierIds, Is.EqualTo(TestModifiers.IdsOf("allowed")));
            Assert.That(Generate(catalog, owned: new[] { "allowed" }).ModifierIds, Is.Empty);
            Assert.That(Generate(catalog, owned: new[] { "allowed" }, unlocked: new[] { "fallback" }).ModifierIds,
                Is.EqualTo(TestModifiers.IdsOf("fallback")));
        }

        /**
         * Checks the catalog derives as addition-only a charm only an encounter's charm additions hold, and not one a reward pool
         * holds as an entry or a fallback / fails if a pooled charm counts as addition-only or the listed one does not.
         */
        [Test]
        public void CatalogDerivesAdditionOnlyCharmsFromItsPools()
        {
            var pooled = Modifier("pooled");
            var fallback = Modifier("fallback");
            var addition = Modifier("addition");
            var pool = new ModifierRewardPoolDefinition("pool.ordinary", new[] { pooled.Identity }, fallback.Identity);
            var catalog = Catalog(new[] { pooled, fallback, addition }, Policy(pool), encounterModifierIds: new[] { addition.Identity });
            Assert.That(catalog.IsAdditionOnly(pooled.Identity), Is.False);
            Assert.That(catalog.IsAdditionOnly(fallback.Identity), Is.False);
            Assert.That(catalog.IsAdditionOnly(addition.Identity), Is.True);
        }

        /**
         * Checks a charm that can never activate with the encounter's rules in play fails loading on the encounter's charm
         * additions and in a pool its reward policy draws from, naming the charm, the place and the encounter / fails if
         * either loads or the message omits one of them.
         */
        [Test]
        public void CatalogRejectsACharmThatCannotActivateWithTheEncountersRules()
        {
            var pooled = Modifier("pooled");
            var favored = new ModifierDefinition(TestModifiers.IdOf("favored"), TestFamilies.Of("family.favored"), ModifierPhase.PlayHand,
                new FavoredFaceCountAtLeastCondition(2), new ModifierEffect[] { new AddBaseScoreEffect(1) });
            var added = Assert.Throws<ArgumentException>(() => Catalog(new[] { pooled, favored }, Policy(Pool(new[] { pooled })),
                encounterModifierIds: new[] { favored.Identity }));
            Assert.That(added.Message, Does.Contain(favored.Identity.ToString()).And.Contain("charm additions")
                .And.Contain(TestContent.EncounterId.ToString()));
            var drawn = Assert.Throws<ArgumentException>(() => Catalog(new[] { pooled, favored }));
            Assert.That(drawn.Message, Does.Contain(favored.Identity.ToString()).And.Contain("pool.ordinary")
                .And.Contain(TestContent.EncounterId.ToString()));
        }

        [Test]
        public void TierWeightsSelectTiersWithoutPopulationBias()
        {
            var modifiers = Enumerable.Range(0, 9).Select(i => Modifier("weak." + i)).Concat(new[] { Modifier("strong", 2) }).ToArray();
            var catalog = Catalog(modifiers, Policy(Pool(modifiers), rules: new[] { new ModifierTierRule(1, new[] { 1, 1 }) }));
            int strongCount = 0;
            for (ulong seed = 0; seed < 1000; ++seed)
                if (Generate(catalog, seed).ModifierIds[0] == TestModifiers.IdOf("strong")) ++strongCount;
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
            Assert.That(Generate(catalog, incomingStage: 2).ModifierIds.All(id => TestModifiers.NameOf(id).StartsWith("weak.")), Is.True);
            var offer = Generate(catalog, history: new[] { "family.strong.0", "family.strong.1" }, incomingStage: 3).ModifierIds;
            Assert.That(offer.Take(2).All(id => TestModifiers.NameOf(id).StartsWith("strong.")), Is.True);
            Assert.That(offer.Count, Is.EqualTo(3));
            Assert.That(TestModifiers.NameOf(offer[2]), Does.StartWith("weak."));
            Assert.That(Generate(catalog, incomingStage: 4).ModifierIds.All(id => TestModifiers.NameOf(id).StartsWith("strong.")), Is.True);
            Assert.That(Generate(catalog, owned: new[] { "strong.0", "strong.1" }, incomingStage: 4).ModifierIds, Is.Empty);
        }

        [Test]
        public void TierFilteringAppliesToFallbackAndHistoryBypass()
        {
            var modifiers = new[] { Modifier("weak"), Modifier("strong", 2), Modifier("fallback", rewardEligible: false) };
            var catalog = Catalog(modifiers, Policy(Pool(modifiers, "fallback"), rules: new[] { new ModifierTierRule(1, new[] { 0, 1 }) }));
            Assert.That(Generate(catalog, history: new[] { "family.strong" }).ModifierIds, Is.EqualTo(TestModifiers.IdsOf("strong")));
            Assert.That(Generate(catalog, owned: new[] { "strong" }).ModifierIds, Is.Empty);
        }

        [Test]
        public void ClearedMilestoneUnlocksBeforeOffersAndFinalCompletionWithoutDuplicateEvents()
        {
            var modifiers = new[] { Modifier("first", 2, requiresUnlock: true),
                Modifier("final", requiresUnlock: true) };
            var catalog = Catalog(modifiers, milestones: new[] { new ModifierUnlockMilestone(1), new ModifierUnlockMilestone(2) },
                unlockRewardModifierIds: TestModifiers.IdsOf("first", "final"));
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 3);
            var clear = session.Execute(PlayHandAction.Instance, 0);
            Assert.That(clear.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingUnlock));
            Assert.That(clear.Snapshot.PendingReward.Options.Select(option => option.DefinitionId), Is.EquivalentTo(TestModifiers.IdsOf("first", "final")));
            Assert.That(clear.Events.OfType<ModifierUnlockedEvent>(), Is.Empty);
            var unlock = clear.Snapshot.PendingReward;
            clear = session.Execute(new SelectModifierUnlockAction(unlock.OfferId,
                unlock.Options.Single(value => value.DefinitionId == TestModifiers.IdOf("first")).OptionId), session.Snapshot.Revision);
            Assert.That(clear.Events.OfType<ModifierUnlockedEvent>().Single().ModifierId, Is.EqualTo(TestModifiers.IdOf("first")));
            Assert.That(clear.Snapshot.Modifiers, Is.Empty);
            var offer = session.Snapshot.PendingReward;
            session.Execute(new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId), session.Snapshot.Revision);
            offer = session.Snapshot.PendingReward;
            session.Execute(new SelectHandUpgradeAction(offer.OfferId, offer.Options[0].OptionId), session.Snapshot.Revision);
            var final = session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
            Assert.That(final.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.None));
            unlock = final.Snapshot.PendingReward;
            Assert.That(unlock.Options.Single().DefinitionId, Is.EqualTo(TestModifiers.IdOf("final")));
            var pendingFinal = EncounterSession.RestoreEncounter(catalog, session.CaptureSave());
            Assert.That(pendingFinal.Success, Is.True, pendingFinal.Detail);
            Assert.That(pendingFinal.Session.Snapshot.PendingReward.Equals(unlock), Is.True);
            final = session.Execute(new SelectModifierUnlockAction(unlock.OfferId, unlock.Options[0].OptionId), session.Snapshot.Revision);
            Assert.That(final.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(final.Snapshot.NewlyUnlockedModifierIds, Is.EqualTo(TestModifiers.IdsOf("first", "final")));
            Assert.That(final.Events.OfType<ModifierUnlockedEvent>().Single().ModifierId, Is.EqualTo(TestModifiers.IdOf("final")));
            var restored = EncounterSession.RestoreEncounter(catalog, session.CaptureSave());
            Assert.That(restored.Success, Is.True, restored.Detail);
            Assert.That(restored.Session.Snapshot.NewlyUnlockedModifierIds, Is.EqualTo(TestModifiers.IdsOf("first", "final")));
            var replay = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 3, final.Snapshot.UnlockedModifierIds);
            Assert.That(replay.Execute(PlayHandAction.Instance, 0).Events.OfType<ModifierUnlockedEvent>(), Is.Empty);
        }

        [Test]
        public void PendingOffersSurviveProfileMergeAndResumeWithDeterministicRefreshes()
        {
            var modifiers = new[] { Modifier("weak"), Modifier("strong", 2, requiresUnlock: true) };
            var catalog = Catalog(modifiers, Policy(Pool(modifiers), milestones: new[] { new ModifierTierMilestone(2, 2) }));
            var original = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 7);
            original.Execute(PlayHandAction.Instance, 0);
            var pending = original.Snapshot.PendingReward;
            original.AddUnlockedModifiers(TestModifiers.IdsOf("strong"));
            Assert.That(original.Snapshot.PendingReward, Is.SameAs(pending));
            Assert.That(original.Snapshot.NewlyUnlockedModifierIds, Is.Empty);
            var bytes = EncounterSaveCodec.Serialize(original.CaptureSave());
            var restored = EncounterSession.RestoreEncounter(catalog, EncounterSaveCodec.Deserialize(bytes, catalog));
            Assert.That(restored.Success, Is.True, restored.Detail);
            Assert.That(restored.Session.Snapshot.PendingReward.Options[0].DefinitionId, Is.EqualTo(TestModifiers.IdOf("weak")));
            var first = original.Execute(new RefreshModifierOfferAction(pending.OfferId), original.Snapshot.Revision);
            var second = restored.Session.Execute(new RefreshModifierOfferAction(pending.OfferId), restored.Session.Snapshot.Revision);
            Assert.That(first.Snapshot.PendingReward.Options.Select(option => option.DefinitionId), Is.EqualTo(TestModifiers.IdsOf("strong")));
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
            var modifiers = Enumerable.Range(0, 8).Select(i => new ModifierDefinition(TestModifiers.IdOf("charm." + i), TestFamilies.Of("shared.family"),
                ModifierPhase.PlayHand, new NeverCondition(), Array.Empty<ModifierEffect>(),
                availability: new ModifierAvailabilityDefinition(requiresUnlock: true))).ToArray();
            var catalog = Catalog(modifiers, milestones: new[] { new ModifierUnlockMilestone(1) },
                unlockRewardModifierIds: modifiers.Select(value => value.Identity));
            var owned = modifiers.Take(8 - remaining).Select(value => value.Identity).ToArray();
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 14, owned);
            var clear = session.Execute(PlayHandAction.Instance, 0);
            var offer = clear.Snapshot.PendingReward;
            Assert.That(offer.Kind, Is.EqualTo(RewardKind.ModifierUnlock));
            Assert.That(offer.Options.Count, Is.EqualTo(Math.Min(3, remaining)));
            Assert.That(offer.Options.Select(value => value.DefinitionId).Distinct().Count(), Is.EqualTo(offer.Options.Count));
            Assert.That(offer.Options.All(value => !owned.Contains(value.DefinitionId.Value)), Is.True);
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
            var modifiers = Enumerable.Range(0, 4).Select(i => Modifier("unlock." + i, requiresUnlock: true)).ToArray();
            var catalog = Catalog(modifiers, milestones: new[] { new ModifierUnlockMilestone(1) },
                unlockRewardModifierIds: modifiers.Select(value => value.Identity));
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 8);
            session.Execute(PlayHandAction.Instance, 0);
            var offer = session.Snapshot.PendingReward;
            session.AddUnlockedModifiers(new[] { offer.Options[0].DefinitionId.Value });
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
            var addition = Modifier("addition", 2);
            var policy = Policy(Pool(new[] { starter }), rules: new[] { new ModifierTierRule(1, new[] { 0, 1 }) });
            var encounter = new EncounterDefinition(TestContent.EncounterId,
                new[] { new StageDefinition(1, 2, 1, StageExitBehavior.AwaitRewards), new StageDefinition(1, 2, 1) },
                rewardPolicy: policy, encounterModifierIds: new[] { addition.Identity });
            var catalog = new MechanicalCatalog("additions", TestContent.Hands(), new[] { encounter }, new[] { starter, addition });
            Assert.That(Generate(catalog).ModifierIds, Is.EqualTo(new[] { addition.Identity }));
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
            var modifier = Modifier("last", requiresUnlock: true);
            var encounter = new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1, 1, 0) },
                unlockMilestones: new[] { new ModifierUnlockMilestone(1) },
                unlockRewardModifierIds: new[] { modifier.Identity });
            var catalog = new MechanicalCatalog("final.unlock", TestContent.Hands(), new[] { encounter }, new[] { modifier });
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 4,
                collected ? new[] { modifier.Identity } : Array.Empty<ModifierDefinition.Id>());
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
            var modifiers = new[] { Modifier("unlock", requiresUnlock: true) };
            var policy = new RewardPolicyDefinition(new[] { new ModifierRewardLayout(new[] { Pool(modifiers) }) },
                0, 1, 2, 1, new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) });
            var catalog = Catalog(modifiers, policy,
                new[] { new ModifierUnlockMilestone(1) }, stages: stages, unlockRewardModifierIds: TestModifiers.IdsOf("unlock"));
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 2);
            session.Execute(PlayHandAction.Instance, 0);
            session.Execute(AbandonEncounterAction.Instance, session.Snapshot.Revision);
            var restored = EncounterSession.RestoreEncounter(catalog, session.CaptureSave());
            Assert.That(restored.Success, Is.True, restored.Detail);
            Assert.That(restored.Session.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Abandoned));
            Assert.That(restored.Session.Snapshot.UnlockedModifierIds, Is.Empty);
        }

        [Test]
        public void CurrentSaveRequiresTheUnlockedModifiers()
        {
            var modifiers = new[] { Modifier("starter"), Modifier("locked", requiresUnlock: true) };
            var catalog = Catalog(modifiers);
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 9);
            session.Execute(PlayHandAction.Instance, 0);
            var document = JObject.Parse(System.Text.Encoding.UTF8.GetString(EncounterSaveCodec.Serialize(session.CaptureSave())));
            document.Remove(nameof(EncounterSaveData.UnlockedModifierIds));
            Assert.Throws<FormatException>(() => EncounterSaveCodec.Deserialize(System.Text.Encoding.UTF8.GetBytes(document.ToString()), catalog));
        }

        [Test]
        public void CatalogRejectsNonPermanentUnlockRewardsAndMilestonesBeyondTheEncounter()
        {
            var milestone = new[] { new ModifierUnlockMilestone(1) };
            var unlocks = TestModifiers.IdsOf("charm");
            Assert.Throws<ArgumentException>(() => Catalog(new[] { Modifier("charm") }, milestones: milestone,
                unlockRewardModifierIds: unlocks));
            Assert.Throws<ArgumentException>(() => Catalog(new[] { Modifier("charm", requiresUnlock: true) },
                milestones: new[] { new ModifierUnlockMilestone(3) }, unlockRewardModifierIds: unlocks));
        }

        [Test]
        public void FailedRewardResolutionAndInvalidProfileMergeDoNotCommitUnlocks()
        {
            var modifiers = new[] { Modifier("locked", requiresUnlock: true), Modifier("starter") };
            var policy = new RewardPolicyDefinition(new[] { new ModifierRewardLayout(new[] { Pool(modifiers) }) },
                int.MaxValue, 1, 1, 2, new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) });
            var catalog = Catalog(modifiers, policy, new[] { new ModifierUnlockMilestone(1) }, unlockRewardModifierIds: TestModifiers.IdsOf("locked"));
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 5);
            session.Execute(PlayHandAction.Instance, 0);
            var offer = session.Snapshot.PendingReward;
            Assert.Throws<OverflowException>(() => session.Execute(new SelectModifierUnlockAction(offer.OfferId,
                offer.Options[0].OptionId), session.Snapshot.Revision));
            Assert.That(session.Snapshot.Revision, Is.EqualTo(1));
            Assert.That(session.Snapshot.UnlockedModifierIds, Is.Empty);
            Assert.That(session.Snapshot.NewlyUnlockedModifierIds, Is.Empty);
            Assert.Throws<ArgumentException>(() => session.AddUnlockedModifiers(TestModifiers.IdsOf("locked", "starter")));
            Assert.That(session.Snapshot.UnlockedModifierIds, Is.Empty);
        }
    }
}
