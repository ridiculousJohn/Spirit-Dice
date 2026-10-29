using System;
using System.Linq;
using Dice.Engine;
using NUnit.Framework;

namespace Dice.Tests
{
    /** Where the mechanical catalog states a charm can be obtained, over fabricated encounters, pools and charms. */
    public sealed class ModifierSourcesTests
    {
        private const string Locked = "locked";
        private const string Addition = "addition";
        private const string Pooled = "pooled";
        private const string Fallback = "fallback";
        private const string Ineligible = "ineligible";

        /** Checks the unlock sources are the encounters whose unlock list holds the charm and that make unlock offers, each with every milestone stage / fails if an encounter without the charm or without milestones is listed, or a milestone is dropped. */
        [Test]
        public void UnlockSourcesListEveryMilestoneOfEncountersWhoseUnlockListHoldsTheCharm()
        {
            var catalog = Catalog();
            var unlock = catalog.GetModifierSources(Locked).Unlocks.Single();
            Assert.That(unlock.Encounter.Identity, Is.EqualTo(TestEncounters.IdOf("encounter.unlocks")));
            Assert.That(unlock.StageNumbers, Is.EqualTo(new[] { 1, 3 }));
            Assert.That(catalog.GetModifierSources(Pooled).Unlocks, Is.Empty);
        }

        /** Checks the run reward sources are the encounters that add the charm or draw offers from a pool holding it as an entry or fallback, when the charm is reward-eligible / fails if a source is missed or an ineligible or unrelated charm is listed. */
        [TestCase(Addition, new[] { "encounter.unlocks" })]
        [TestCase(Pooled, new[] { "encounter.pooled", "encounter.scoped" })]
        [TestCase(Fallback, new[] { "encounter.pooled", "encounter.scoped" })]
        [TestCase(Ineligible, new string[0])]
        [TestCase(Locked, new[] { "encounter.scoped" })]
        public void RunRewardSourcesAreAdditionsPoolEntriesAndFallbacks(string modifierId, string[] encounterIds)
            => Assert.That(Catalog().GetModifierSources(modifierId).RunRewards.Select(encounter => encounter.Identity),
                Is.EqualTo(encounterIds.Select(TestEncounters.IdOf)));

        private static MechanicalCatalog Catalog()
        {
            var stages = new[] { new StageDefinition(1, 1, 0), new StageDefinition(1, 1, 0), new StageDefinition(1, 1, 0) };
            var pool = new ModifierRewardPoolDefinition("pool", new[] { Pooled }, Fallback);
            var second = new ModifierRewardPoolDefinition("second", new[] { Locked }, Ineligible);
            var policy = new RewardPolicyDefinition(new[] { new ModifierRewardLayout(new[] { pool }) },
                0, 0, 1, 2, new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) });
            var twoPools = new RewardPolicyDefinition(new[] { new ModifierRewardLayout(new[] { pool, second }) },
                0, 0, 1, 2, new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) });
            var encounters = new[]
            {
                new EncounterDefinition(TestEncounters.IdOf("encounter.unlocks"), stages, unlockMilestones: new[] { new ModifierUnlockMilestone(1),
                    new ModifierUnlockMilestone(3) }, encounterModifierIds: new[] { Addition }, unlockRewardModifierIds: new[] { Locked }),
                new EncounterDefinition(TestEncounters.IdOf("encounter.unlisted"), stages, unlockRewardModifierIds: new[] { Locked }),
                new EncounterDefinition(TestEncounters.IdOf("encounter.pooled"), stages, rewardPolicy: policy),
                new EncounterDefinition(TestEncounters.IdOf("encounter.scoped"), stages, rewardPolicy: twoPools)
            };
            var modifiers = new[]
            {
                Modifier(Locked, access: ModifierAccess.PermanentUnlock), Modifier(Addition), Modifier(Pooled), Modifier(Fallback),
                Modifier(Ineligible, eligible: false)
            };
            return new MechanicalCatalog("test.modifier_sources", TestContent.Hands(), encounters, modifiers);
        }

        private static ModifierDefinition Modifier(string id, ModifierAccess access = ModifierAccess.Starter, bool eligible = true)
            => new ModifierDefinition(id, TestFamilies.Of("family." + id), ModifierPhase.PlayHand, new NeverCondition(), Array.Empty<ModifierEffect>(),
                rewardEligible: eligible, availability: new ModifierAvailabilityDefinition(access: access));
    }
}
