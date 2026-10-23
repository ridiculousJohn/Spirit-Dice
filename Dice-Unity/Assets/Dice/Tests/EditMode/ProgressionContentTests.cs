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
        [TestCase("encounter.owl.watchtower", "edge_pair_sums_equal", "alternating_parity", "ordered")]
        [TestCase("encounter.moth.crossroads", "contains_rerolls_remaining", "every_die_rerolled", "maximum_at_most_rerolls_remaining")]
        [TestCase("encounter.nymph.bound_tree", "three_smallest_sum_at_most_maximum", "adjacent_difference_at_most", "no_dice_rerolled")]
        public void ProvisionalSpiritUnlockListsOfferOnlyBaseAndMultOfThreeAuthoredFamilies(
            string encounterId, string first, string second, string third)
        {
            var encounter = Catalog().GetEncounter(encounterId);
            Assert.That(encounter.UnlockRewardModifierIds, Is.EquivalentTo(new[] { first, second, third }
                .SelectMany(family => new[] { "modifier.play_hand." + family + ".base_score",
                    "modifier.play_hand." + family + ".multiplier" })));
            Assert.That(encounter.UnlockMilestones.Select(value => value.StageNumber), Is.EqualTo(new[] { 3, 6, 10 }));
        }

        [Test]
        public void AuthoredFoxMilestonesShareSixGlobalPermanentUnlockCandidates()
        {
            var catalog = Catalog();
            var encounter = catalog.GetEncounter("encounter.fox.introduction");
            Assert.That(encounter.SpiritId, Is.EqualTo("spirit.fox"));
            Assert.That(encounter.UnlockMilestones.Select(value => value.StageNumber), Is.EqualTo(new[] { 3, 6, 10 }));
            string[] families = { "modifier.initial_roll.first_hand", "modifier.player_reroll.rank_equals_before_reroll",
                "modifier.initial_roll.initial_rank_above_previous" };
            Assert.That(encounter.UnlockRewardModifierIds, Is.EquivalentTo(families.SelectMany(family =>
                new[] { family + ".base_score", family + ".multiplier" })));
            Assert.That(encounter.UnlockMilestones.All(value => value.ModifierIds.Count == 4), Is.True);
            foreach (string family in families)
            {
                var modifiers = catalog.Modifiers.Where(value => value.FamilyId == family).ToArray();
                Assert.That(modifiers.Length, Is.EqualTo(4));
                foreach (var modifier in modifiers)
                {
                    Assert.That(modifier.Availability.Access, Is.EqualTo(ModifierAccess.PermanentUnlock));
                    Assert.That(modifier.Availability.Scope, Is.EqualTo(ModifierAvailabilityScope.Global));
                    Assert.That(modifier.Availability.ScopeId, Is.Null);
                    string role = modifier.Id.Substring(modifier.FamilyId.Length + 1);
                    Assert.That(catalog.GetRewardPool("reward_pool.fox.introduction." + role).ModifierIds,
                        Does.Contain(modifier.Id));
                }
            }
        }

        [Test]
        public void OrdinaryModifiersAreGlobalAndOnlyLanternSeekersAreEncounterExclusive()
        {
            var catalog = Catalog();
            var functional = catalog.Modifiers.Where(value => value.RewardEligible).ToArray();
            Assert.That(functional.Count(value => value.Availability.Access == ModifierAccess.Starter), Is.EqualTo(72));
            var exclusive = functional.Where(value => value.Availability.Access == ModifierAccess.EncounterExclusive).ToArray();
            Assert.That(exclusive.Length, Is.EqualTo(8));
            var familyEncounters = new Dictionary<string, string>
            {
                { "modifier.play_hand.favored_face_count_at_least", "encounter.moth.crossroads" },
                { "modifier.play_hand.bound_face_count_at_least", "encounter.nymph.bound_tree" }
            };
            Assert.That(exclusive.Select(value => value.FamilyId).Distinct(), Is.EquivalentTo(familyEncounters.Keys));
            Assert.That(exclusive.All(value => value.Availability.Scope == ModifierAvailabilityScope.Encounter), Is.True);
            Assert.That(exclusive.All(value => value.Availability.ScopeId == familyEncounters[value.FamilyId]), Is.True);
            Assert.That(catalog.Modifiers.Except(exclusive).All(value => value.Availability.Scope == ModifierAvailabilityScope.Global), Is.True);
            foreach (var family in familyEncounters)
                Assert.That(catalog.GetEncounter(family.Value).EncounterModifierIds,
                    Is.EquivalentTo(exclusive.Where(value => value.FamilyId == family.Key).Select(value => value.Id)), family.Value);
            Assert.That(catalog.GetEncounter("encounter.fox.introduction").EncounterModifierIds, Is.Empty);
            Assert.That(catalog.GetEncounter("encounter.owl.watchtower").EncounterModifierIds, Is.Empty);
            Assert.That(catalog.GetEncounter("encounter.merchant.unquiet_cart").EncounterModifierIds, Is.Empty);
            Assert.That(functional.All(value => value.Availability.PowerTier >= 1 && value.Availability.PowerTier <= 3), Is.True);
        }

        [Test]
        public void AuthoredNymphEncounterBindsDiceAndOffersGraftedBranchFromTheThirdClear()
        {
            string[] roles = { "base_score", "multiplier", "combined", "rerolls" };
            string[] families = { "three_smallest_sum_at_most_maximum", "adjacent_difference_at_most", "no_dice_rerolled" };
            var catalog = Catalog();
            var encounter = catalog.GetEncounter("encounter.nymph.bound_tree");
            var moth = catalog.GetEncounter("encounter.moth.crossroads");
            Assert.That(encounter.SpiritId, Is.EqualTo("spirit.nymph"));
            Assert.That(encounter.Rules.Single(), Is.TypeOf<DieBindingRuleDefinition>());
            var binding = (DieBindingRuleDefinition)encounter.Rules.Single();
            Assert.That(binding.Id, Is.EqualTo("rule.nymph.binding"));
            Assert.That(binding.MarksAfterOpeningRoll, Is.True);
            Assert.That(binding.MarksAfterPlayerReroll, Is.True);
            Assert.That(encounter.Pieces, Is.Empty);
            Assert.That(encounter.Stages.Count, Is.EqualTo(10));
            Assert.That(encounter.Stages.Select(Describe), Is.EqualTo(moth.Stages.Select(Describe)));

            Assert.That(encounter.UnlockRewardModifierIds, Is.EqualTo(families.SelectMany(family => new[]
                { "modifier.play_hand." + family + ".base_score", "modifier.play_hand." + family + ".multiplier" })));
            Assert.That(encounter.UnlockMilestones.Select(value => value.StageNumber), Is.EqualTo(new[] { 3, 6, 10 }));
            for (int i = 0; i < families.Length; ++i)
                Assert.That(encounter.UnlockMilestones[i].ModifierIds,
                    Is.EqualTo(roles.Select(role => "modifier.play_hand." + families[i] + "." + role)), families[i]);

            Assert.That(encounter.EncounterModifierIds,
                Is.EqualTo(roles.Select(role => "modifier.play_hand.bound_face_count_at_least." + role)));
            Assert.That(encounter.EncounterModifiersFirstClearedStageNumber, Is.EqualTo(3));
            long[][] effects = { new[] { 10L, 0, 0 }, new[] { 0L, 2, 0 }, new[] { 5L, 1, 0 }, new[] { 0L, 0, 1 } };
            for (int i = 0; i < roles.Length; ++i)
            {
                var modifier = catalog.GetModifier(encounter.EncounterModifierIds[i]);
                Assert.That(modifier.FamilyId, Is.EqualTo("modifier.play_hand.bound_face_count_at_least"));
                Assert.That(modifier.Phase, Is.EqualTo(ModifierPhase.PlayHand), modifier.Id);
                Assert.That(modifier.Condition, Is.TypeOf<BoundFaceCountAtLeastCondition>(), modifier.Id);
                Assert.That(((BoundFaceCountAtLeastCondition)modifier.Condition).Parameter, Is.EqualTo(3), modifier.Id);
                Assert.That(modifier.ActivationLimit, Is.EqualTo(ModifierActivationLimit.OncePerHand), modifier.Id);
                Assert.That(modifier.RewardEligible, Is.True, modifier.Id);
                Assert.That(modifier.Availability.PowerTier, Is.EqualTo(1), modifier.Id);
                Assert.That(modifier.Availability.Access, Is.EqualTo(ModifierAccess.EncounterExclusive), modifier.Id);
                Assert.That(modifier.Availability.MatchesEncounter(encounter), Is.True, modifier.Id);
                Assert.That(modifier.Availability.MatchesEncounter(moth), Is.False, modifier.Id);
                for (int kind = 0; kind < 3; ++kind)
                    Assert.That(modifier.Effects.Where(value => (int)value.Kind == kind).Sum(value => value.Amount),
                        Is.EqualTo(effects[i][kind]), modifier.Id + " effect " + (ModifierEffectKind)kind);
            }

            Assert.That(encounter.RewardPolicy.ModifierLayouts.SelectMany(value => value.PoolIds).Distinct(),
                Is.EquivalentTo(roles.Select(role => "reward_pool.nymph.bound_tree." + role)));
            foreach (string role in roles)
                Assert.That(catalog.GetRewardPool("reward_pool.nymph.bound_tree." + role).ModifierIds,
                    Is.EquivalentTo(catalog.GetRewardPool("reward_pool.moth.crossroads." + role).ModifierIds
                        .Concat(families.Select(family => "modifier.play_hand." + family + "." + role)).Distinct()), role);

            var reward = AssetDatabase.LoadAssetAtPath<ProgressionRewardAsset>(
                "Assets/Dice/Content/Mechanical/ProgressionRewards/NymphFamilyDiscovery.asset");
            Assert.That(reward, Is.Not.Null);
            var discovery = reward.Build(catalog);
            Assert.That(discovery.DiscoveredFamilyIds, Is.EqualTo(families.Select(family => "modifier.play_hand." + family)));
            Assert.That(discovery.GrantedModifierIds, Is.Empty);
        }

        private static string Describe(StageDefinition stage)
            => stage.Threshold + "/" + stage.StartingHands + "/" + stage.StartingRerolls + "/" + stage.ExitBehavior;

        [TestCase("encounter.fox.introduction", 4, 7, 10)]
        [TestCase("encounter.owl.watchtower", 4, 7, 10)]
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
