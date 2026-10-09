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
        private static ModifierDefinition Modifier(string id, int tier = 1,
            ModifierAvailabilityScope scope = ModifierAvailabilityScope.Global, string scopeId = null,
            ModifierAccess access = ModifierAccess.Starter, bool rewardEligible = true)
            => new ModifierDefinition(id, "family." + id, ModifierPhase.PlayHand,
                new ModifierCondition(ModifierConditionKind.Never), Array.Empty<ModifierEffect>(),
                rewardEligible: rewardEligible, availability: new ModifierAvailabilityDefinition(tier, scope, scopeId, access));

        private static RewardPolicyDefinition Policy(int slots = 1, IEnumerable<ModifierTierRule> rules = null,
            IEnumerable<ModifierTierMilestone> milestones = null)
            => new RewardPolicyDefinition("policy", new[] { new ModifierRewardLayout(Enumerable.Repeat("pool", slots)) },
                2, 1, 1, 2, new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) }, rules, milestones);

        private static MechanicalCatalog Catalog(ModifierDefinition[] modifiers, RewardPolicyDefinition policy = null,
            IEnumerable<ModifierUnlockMilestone> milestones = null, string fallback = null, int stages = 2,
            string encounterId = TestContent.EncounterId, string spiritId = "fox")
            => new MechanicalCatalog("progression.tests", TestContent.Hands(), new[]
            {
                new EncounterDefinition(encounterId, Enumerable.Range(0, stages)
                    .Select(_ => new StageDefinition(1, 2, 1, StageExitBehavior.AwaitRewards)),
                    rewardPolicy: policy ?? Policy(), spiritId: spiritId, unlockMilestones: milestones)
            }, modifiers, new[] { new ModifierRewardPoolDefinition("pool",
                modifiers.Where(modifier => modifier.Id != fallback).Select(modifier => modifier.Id), fallback) });

        private static ModifierRewardGeneration Generate(MechanicalCatalog catalog, ulong seed = 0,
            IEnumerable<string> unlocked = null, IEnumerable<string> owned = null, IEnumerable<string> history = null, int incomingStage = 2)
        {
            var encounter = catalog.GetEncounter(TestContent.EncounterId);
            var random = new DeterministicRandom(seed);
            return RewardGenerator.GenerateModifiers(catalog, encounter.RewardPolicy, incomingStage - 1,
                owned ?? Array.Empty<string>(), history ?? Array.Empty<string>(), ref random, encounter, unlocked, incomingStage);
        }

        [Test]
        public void ScopeAndAccessRemainIndependentAndExclusiveRewardsNeedNoUnlock()
        {
            var modifiers = new[]
            {
                Modifier("global"), Modifier("locked", access: ModifierAccess.PermanentUnlock),
                Modifier("spirit", scope: ModifierAvailabilityScope.Spirit, scopeId: "fox", access: ModifierAccess.EncounterExclusive),
                Modifier("encounter", scope: ModifierAvailabilityScope.Encounter, scopeId: TestContent.EncounterId, access: ModifierAccess.EncounterExclusive),
                Modifier("other.spirit", scope: ModifierAvailabilityScope.Spirit, scopeId: "owl", access: ModifierAccess.EncounterExclusive),
                Modifier("other.encounter", scope: ModifierAvailabilityScope.Encounter, scopeId: "elsewhere", access: ModifierAccess.PermanentUnlock)
            };
            var catalog = Catalog(modifiers, Policy(6));
            Assert.That(Generate(catalog).ModifierIds, Is.EquivalentTo(new[] { "global", "spirit", "encounter" }));
            Assert.That(Generate(catalog, unlocked: new[] { "locked", "other.encounter" }).ModifierIds,
                Is.EquivalentTo(new[] { "global", "locked", "spirit", "encounter" }));
            Assert.Throws<ArgumentException>(() => new ModifierAvailabilityDefinition(access: ModifierAccess.EncounterExclusive));
        }

        [Test]
        public void PermanentUnlocksRespectGlobalSpiritAndEncounterScopes()
        {
            var modifiers = new[]
            {
                Modifier("global", access: ModifierAccess.PermanentUnlock),
                Modifier("spirit", scope: ModifierAvailabilityScope.Spirit, scopeId: "fox", access: ModifierAccess.PermanentUnlock),
                Modifier("encounter", scope: ModifierAvailabilityScope.Encounter, scopeId: TestContent.EncounterId, access: ModifierAccess.PermanentUnlock)
            };
            var policy = Policy(3);
            var catalog = Catalog(modifiers, policy);
            Assert.That(Generate(catalog).ModifierIds, Is.Empty);
            Assert.That(Generate(catalog, unlocked: modifiers.Select(value => value.Id)).ModifierIds,
                Is.EquivalentTo(modifiers.Select(value => value.Id)));
            var otherEncounter = new EncounterDefinition("other", new[] { new StageDefinition(1, 1, 0) }, spiritId: "owl");
            var random = new DeterministicRandom(1);
            Assert.That(RewardGenerator.GenerateModifiers(catalog, policy, 1, Array.Empty<string>(), Array.Empty<string>(),
                ref random, otherEncounter, modifiers.Select(value => value.Id)).ModifierIds, Is.EqualTo(new[] { "global" }));
        }

        [Test]
        public void EarnedSpiritUnlocksCarryIntoFutureFoxEncountersWhileIntroductionRewardsStayScoped()
        {
            var modifiers = new[]
            {
                Modifier("global", access: ModifierAccess.PermanentUnlock),
                Modifier("fox.unlock", scope: ModifierAvailabilityScope.Spirit, scopeId: "fox", access: ModifierAccess.PermanentUnlock),
                Modifier("introduction.unlock", scope: ModifierAvailabilityScope.Encounter, scopeId: TestContent.EncounterId,
                    access: ModifierAccess.PermanentUnlock),
                Modifier("introduction.exclusive", scope: ModifierAvailabilityScope.Encounter, scopeId: TestContent.EncounterId,
                    access: ModifierAccess.EncounterExclusive)
            };
            var policy = Policy(4);
            var introduction = Catalog(modifiers, policy, new[]
            {
                new ModifierUnlockMilestone(1, new[] { "global", "fox.unlock", "introduction.unlock" })
            });
            var session = EncounterSession.CreateEncounter(introduction, TestContent.EncounterId, 10);
            var cleared = session.Execute(PlayHandAction.Instance, 0).Snapshot;
            Assert.That(cleared.PendingReward.Options.Select(option => option.DefinitionId),
                Is.EquivalentTo(modifiers.Select(modifier => modifier.Id)));

            var foxRematch = Catalog(modifiers, policy, encounterId: "encounter.fox.rematch", spiritId: "fox");
            var rematch = EncounterSession.CreateEncounter(foxRematch, "encounter.fox.rematch", 10, cleared.UnlockedModifierIds);
            var foxOffer = rematch.Execute(PlayHandAction.Instance, 0).Snapshot;
            Assert.That(foxOffer.PendingReward.Options.Select(option => option.DefinitionId),
                Is.EquivalentTo(new[] { "global", "fox.unlock" }));
            Assert.That(foxOffer.NewlyUnlockedModifierIds, Is.Empty);
            var restored = EncounterSession.RestoreEncounter(foxRematch, rematch.CaptureSave());
            Assert.That(restored.Success, Is.True, restored.Detail);
            Assert.That(restored.Session.Snapshot.PendingReward.Options.Select(option => option.DefinitionId),
                Is.EquivalentTo(new[] { "global", "fox.unlock" }));

            var owlEncounter = Catalog(modifiers, policy, encounterId: "encounter.owl", spiritId: "owl");
            var owl = EncounterSession.CreateEncounter(owlEncounter, "encounter.owl", 10, cleared.UnlockedModifierIds);
            Assert.That(owl.Execute(PlayHandAction.Instance, 0).Snapshot.PendingReward.Options.Select(option => option.DefinitionId),
                Is.EqualTo(new[] { "global" }));
        }

        [Test]
        public void SmallPoolHistoryBypassAndFallbackNeverExposeLockedOrForeignModifiers()
        {
            var modifiers = new[] { Modifier("allowed"), Modifier("locked", access: ModifierAccess.PermanentUnlock),
                Modifier("fallback", access: ModifierAccess.PermanentUnlock, rewardEligible: false) };
            var catalog = Catalog(modifiers, fallback: "fallback");
            Assert.That(Generate(catalog, history: new[] { "family.allowed" }).ModifierIds, Is.EqualTo(new[] { "allowed" }));
            Assert.That(Generate(catalog, owned: new[] { "allowed" }).ModifierIds, Is.Empty);
            Assert.That(Generate(catalog, owned: new[] { "allowed" }, unlocked: new[] { "fallback" }).ModifierIds,
                Is.EqualTo(new[] { "fallback" }));
            var foreign = Catalog(new[] { Modifier("allowed"), Modifier("foreign", scope: ModifierAvailabilityScope.Spirit,
                scopeId: "owl", access: ModifierAccess.EncounterExclusive, rewardEligible: false) }, fallback: "foreign");
            Assert.That(Generate(foreign, owned: new[] { "allowed" }).ModifierIds, Is.Empty);
        }

        [Test]
        public void TierWeightsSelectTiersWithoutPopulationBias()
        {
            var modifiers = Enumerable.Range(0, 9).Select(i => Modifier("weak." + i)).Concat(new[] { Modifier("strong", 2) }).ToArray();
            var catalog = Catalog(modifiers, Policy(rules: new[] { new ModifierTierRule(1, new[] { 1, 1 }) }));
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
            var policy = Policy(3, new[] { new ModifierTierRule(1, new[] { 100, 0, 0 }), new ModifierTierRule(4, new[] { 0, 0, 100 }) },
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
            var catalog = Catalog(new[] { Modifier("weak"), Modifier("strong", 2), Modifier("fallback", rewardEligible: false) },
                Policy(rules: new[] { new ModifierTierRule(1, new[] { 0, 1 }) }), fallback: "fallback");
            Assert.That(Generate(catalog, history: new[] { "family.strong" }).ModifierIds, Is.EqualTo(new[] { "strong" }));
            Assert.That(Generate(catalog, owned: new[] { "strong" }).ModifierIds, Is.Empty);
        }

        [Test]
        public void ClearedMilestoneUnlocksBeforeOffersAndFinalCompletionWithoutDuplicateEvents()
        {
            var modifiers = new[] { Modifier("first", 2, access: ModifierAccess.PermanentUnlock),
                Modifier("final", access: ModifierAccess.PermanentUnlock) };
            var catalog = Catalog(modifiers, milestones: new[] { new ModifierUnlockMilestone(1, new[] { "first" }),
                new ModifierUnlockMilestone(2, new[] { "final" }) });
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 3);
            var clear = session.Execute(PlayHandAction.Instance, 0);
            Assert.That(clear.Snapshot.PendingReward.Options.Select(option => option.DefinitionId), Is.EqualTo(new[] { "first" }));
            Assert.That(clear.Events.OfType<ModifierUnlockedEvent>().Single().ModifierId, Is.EqualTo("first"));
            Assert.That(clear.Events.ToList().FindIndex(value => value is ModifierUnlockedEvent),
                Is.LessThan(clear.Events.ToList().FindIndex(value => value is RewardOfferedEvent)));
            var offer = session.Snapshot.PendingReward;
            session.Execute(new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId), session.Snapshot.Revision);
            offer = session.Snapshot.PendingReward;
            session.Execute(new SelectHandUpgradeAction(offer.OfferId, offer.Options[0].OptionId), session.Snapshot.Revision);
            var final = session.Execute(PlayHandAction.Instance, session.Snapshot.Revision);
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
            var catalog = Catalog(modifiers, Policy(milestones: new[] { new ModifierTierMilestone(2, 2) }));
            var original = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 7);
            original.Execute(PlayHandAction.Instance, 0);
            var pending = original.Snapshot.PendingReward;
            original.AddUnlockedModifiers(new[] { "strong" });
            Assert.That(original.Snapshot.PendingReward, Is.SameAs(pending));
            Assert.That(original.Snapshot.NewlyUnlockedModifierIds, Is.Empty);
            var bytes = EncounterSaveCodec.Serialize(original.CaptureSave());
            var restored = EncounterSession.RestoreEncounter(catalog, EncounterSaveCodec.Deserialize(bytes));
            Assert.That(restored.Success, Is.True, restored.Detail);
            Assert.That(restored.Session.Snapshot.PendingReward.Options[0].DefinitionId, Is.EqualTo("weak"));
            var first = original.Execute(new RefreshModifierOfferAction(pending.OfferId), original.Snapshot.Revision);
            var second = restored.Session.Execute(new RefreshModifierOfferAction(pending.OfferId), restored.Session.Snapshot.Revision);
            Assert.That(first.Snapshot.PendingReward.Options.Select(option => option.DefinitionId), Is.EqualTo(new[] { "strong" }));
            Assert.That(EncounterSaveCodec.Serialize(restored.Session.CaptureSave()), Is.EqualTo(EncounterSaveCodec.Serialize(original.CaptureSave())));
            Assert.That(second.Accepted, Is.True);
        }

        [TestCase(1, 3)]
        [TestCase(2, 4)]
        public void LegacyPendingChoicesSurviveMigrationResaveAndSelection(int format, int rules)
        {
            var starter = Catalog(new[] { Modifier("charm") });
            var session = EncounterSession.CreateEncounter(starter, TestContent.EncounterId, 4);
            session.Execute(PlayHandAction.Instance, 0);
            var document = JObject.Parse(System.Text.Encoding.UTF8.GetString(EncounterSaveCodec.Serialize(session.CaptureSave())));
            document[nameof(EncounterSaveData.SaveFormatVersion)] = format;
            document[nameof(EncounterSaveData.RulesVersion)] = rules;
            document.Remove(nameof(EncounterSaveData.UnlockedModifierIds));
            document.Remove(nameof(EncounterSaveData.NewlyUnlockedModifierIds));
            document.Remove(nameof(EncounterSaveData.PendingModifierOfferUsesLegacyEligibility));
            document.Remove(nameof(EncounterSaveData.PendingModifierOfferUnlockedIds));
            var current = Catalog(new[] { Modifier("charm", access: ModifierAccess.PermanentUnlock) });
            var restored = EncounterSession.RestoreEncounter(current, EncounterSaveCodec.Deserialize(System.Text.Encoding.UTF8.GetBytes(document.ToString())));
            Assert.That(restored.Success, Is.True, restored.Detail);
            restored = EncounterSession.RestoreEncounter(current, EncounterSaveCodec.Deserialize(EncounterSaveCodec.Serialize(restored.Session.CaptureSave())));
            Assert.That(restored.Success, Is.True, restored.Detail);
            var offer = restored.Session.Snapshot.PendingReward;
            Assert.That(restored.Session.Execute(new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId), restored.Session.Snapshot.Revision).Accepted, Is.True);
            Assert.That(EncounterSession.RestoreEncounter(current, restored.Session.CaptureSave()).Success, Is.True);
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
            Assert.Throws<FormatException>(() => EncounterSaveCodec.Deserialize(System.Text.Encoding.UTF8.GetBytes(document.ToString())));
        }

        [Test]
        public void CatalogRejectsNonPermanentAndForeignMilestoneReferences()
        {
            var milestone = new[] { new ModifierUnlockMilestone(1, new[] { "charm" }) };
            Assert.Throws<ArgumentException>(() => Catalog(new[] { Modifier("charm") }, milestones: milestone));
            Assert.Throws<ArgumentException>(() => Catalog(new[] { Modifier("charm", scope: ModifierAvailabilityScope.Spirit,
                scopeId: "owl", access: ModifierAccess.PermanentUnlock) }, milestones: milestone));
            Assert.Throws<ArgumentException>(() => Catalog(new[] { Modifier("charm", access: ModifierAccess.PermanentUnlock) },
                milestones: new[] { new ModifierUnlockMilestone(3, new[] { "charm" }) }));
        }

        [Test]
        public void FailedRewardResolutionAndInvalidProfileMergeDoNotCommitUnlocks()
        {
            var modifiers = new[] { Modifier("locked", access: ModifierAccess.PermanentUnlock), Modifier("starter") };
            var policy = new RewardPolicyDefinition("overflow", new[] { new ModifierRewardLayout(new[] { "pool" }) },
                int.MaxValue, 1, 1, 2, new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) });
            var catalog = Catalog(modifiers, policy, new[] { new ModifierUnlockMilestone(1, new[] { "locked" }) });
            var session = EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 5);
            Assert.Throws<OverflowException>(() => session.Execute(PlayHandAction.Instance, 0));
            Assert.That(session.Snapshot.Revision, Is.Zero);
            Assert.That(session.Snapshot.UnlockedModifierIds, Is.Empty);
            Assert.That(session.Snapshot.NewlyUnlockedModifierIds, Is.Empty);
            Assert.Throws<ArgumentException>(() => session.AddUnlockedModifiers(new[] { "locked", "starter" }));
            Assert.That(session.Snapshot.UnlockedModifierIds, Is.Empty);
        }
    }
}
