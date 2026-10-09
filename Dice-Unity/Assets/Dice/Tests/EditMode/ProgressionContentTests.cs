using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;

namespace Dice.Tests
{
    public sealed class ProgressionContentTests
    {
        [Test]
        public void AuthoredFoxMilestonesUnlockAllFourVariantsOnlyForFoxEncounters()
        {
            var catalog = Catalog();
            var encounter = catalog.GetEncounter("encounter.fox.introduction");
            Assert.That(encounter.SpiritId, Is.EqualTo("spirit.fox"));
            Assert.That(encounter.UnlockMilestones.Select(value => value.StageNumber), Is.EqualTo(new[] { 3, 6, 10 }));
            string[] families = { "modifier.initial_roll.first_hand", "modifier.player_reroll.rank_equals_before_reroll",
                "modifier.initial_roll.initial_rank_above_previous" };
            for (int i = 0; i < families.Length; ++i)
            {
                var modifiers = encounter.UnlockMilestones[i].ModifierIds.Select(catalog.GetModifier).ToArray();
                Assert.That(modifiers.Length, Is.EqualTo(4));
                Assert.That(modifiers.All(value => value.FamilyId == families[i]), Is.True);
                foreach (var modifier in modifiers)
                {
                    Assert.That(modifier.Availability.Access, Is.EqualTo(ModifierAccess.PermanentUnlock));
                    Assert.That(modifier.Availability.Scope, Is.EqualTo(ModifierAvailabilityScope.Spirit));
                    Assert.That(modifier.Availability.ScopeId, Is.EqualTo(encounter.SpiritId));
                    string role = modifier.Id.Substring(modifier.FamilyId.Length + 1);
                    Assert.That(catalog.GetRewardPool("reward_pool.fox.introduction." + role).ModifierIds,
                        Does.Contain(modifier.Id));
                }
            }
        }

        [Test]
        public void NewPlayerHasFortyFourGeneralStartersAndFourIntroductionExclusives()
        {
            var catalog = Catalog();
            var functional = catalog.Modifiers.Where(value => value.RewardEligible).ToArray();
            Assert.That(functional.Count(value => value.Availability.Access == ModifierAccess.Starter), Is.EqualTo(44));
            var exclusive = functional.Where(value => value.Availability.Access == ModifierAccess.EncounterExclusive).ToArray();
            Assert.That(exclusive.Length, Is.EqualTo(4));
            Assert.That(exclusive.All(value => value.FamilyId == "modifier.initial_roll.initial_pair"), Is.True);
            Assert.That(exclusive.All(value => value.Availability.Scope == ModifierAvailabilityScope.Encounter &&
                value.Availability.ScopeId == "encounter.fox.introduction"), Is.True);
            Assert.That(functional.All(value => value.Availability.PowerTier >= 1 && value.Availability.PowerTier <= 3), Is.True);
        }

        [TestCase("encounter.fox.introduction", 4, 7, 10)]
        [TestCase("encounter.standard", 9, 17, 23)]
        public void EveryAuthoredEncounterUsesIncreasingTierWeightsAndMilestoneOffers(string id, int middle, int late, int final)
        {
            var catalog = Catalog();
            var encounter = catalog.GetEncounter(id);
            var policy = encounter.RewardPolicy;
            Assert.That(policy.TierRules.Select(value => value.FirstIncomingStageNumber), Is.EqualTo(new[] { 1, middle, late, final }));
            Assert.That(policy.TierRules[0].TierWeights, Is.EqualTo(new[] { 100, 0, 0 }));
            Assert.That(policy.TierRules[3].TierWeights, Is.EqualTo(new[] { 15, 45, 40 }));
            Assert.That(policy.TierMilestones.Select(value => value.IncomingStageNumber), Is.EqualTo(new[] { middle, late, final }));
            for (ulong seed = 1; seed <= 64; ++seed)
            {
                var random = new DeterministicRandom(seed);
                var early = RewardGenerator.GenerateModifiers(catalog, policy, 1, Array.Empty<string>(),
                    Array.Empty<string>(), ref random, encounter, Array.Empty<string>(), 2);
                Assert.That(early.ModifierIds.Count, Is.EqualTo(3));
                Assert.That(early.ModifierIds.All(value => catalog.GetModifier(value).Availability.PowerTier == 1), Is.True);
                var milestone = RewardGenerator.GenerateModifiers(catalog, policy, middle - 1, Array.Empty<string>(),
                    Array.Empty<string>(), ref random, encounter, Array.Empty<string>(), middle);
                Assert.That(milestone.ModifierIds.Count, Is.EqualTo(3));
                Assert.That(milestone.ModifierIds.All(value => catalog.GetModifier(value).Availability.PowerTier >= 2), Is.True);
            }
        }

        private static MechanicalCatalog Catalog()
            => AssetDatabase.LoadAssetAtPath<MechanicalCatalogAsset>("Assets/Dice/Content/Mechanical/Standard.asset")
                .Build(TestContent.ModifierAssets());
    }
}
