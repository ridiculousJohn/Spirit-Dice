using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    public static class RewardGenerator
    {
        private const int MinimumCandidatesBeforeHistoryBypass = 3;
        /**
         * An upgrade offer that takes every candidate draws nothing from the reward stream while it has at most this many;
         * the reward streams of existing seeds depend on it.
         */
        private const int MaximumUpgradeCandidatesWithoutDraw = 3;

        /** The offer after clearing the stage numbered rewardCycleIndex, for the stage after it. */
        public static ModifierRewardGeneration GenerateModifiers(MechanicalCatalog catalog, RewardPolicyDefinition policy,
            int rewardCycleIndex, IEnumerable<ModifierDefinition.Id> ownedModifierIds, IEnumerable<ModifierFamilyDefinition.Id> excludedFamilyIds,
            ref DeterministicRandom random, EncounterDefinition encounter = null, IEnumerable<ModifierDefinition.Id> unlockedModifierIds = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (rewardCycleIndex < 0) throw new ArgumentOutOfRangeException(nameof(rewardCycleIndex));
            if (ownedModifierIds == null) throw new ArgumentNullException(nameof(ownedModifierIds));
            if (excludedFamilyIds == null) throw new ArgumentNullException(nameof(excludedFamilyIds));
            int incomingStageNumber = checked(rewardCycleIndex + 1);
            var unlocked = new HashSet<ModifierDefinition.Id>(unlockedModifierIds ?? Array.Empty<ModifierDefinition.Id>());
            var owned = new HashSet<ModifierDefinition.Id>();
            foreach (var id in ownedModifierIds) { catalog.GetModifier(id); owned.Add(id); }
            var history = new List<ModifierFamilyDefinition.Id>();
            var excluded = new HashSet<ModifierFamilyDefinition.Id>();
            foreach (var familyId in excludedFamilyIds)
            {
                if (!catalog.HasFamily(familyId))
                    throw new ArgumentException("An excluded modifier family is missing from the catalog.", nameof(excludedFamilyIds));
                if (excluded.Add(familyId)) history.Add(familyId);
            }
            var offeredIds = new List<ModifierDefinition.Id>();
            var currentFamilies = new HashSet<ModifierFamilyDefinition.Id>();
            foreach (var pool in policy.LayoutFor(rewardCycleIndex).Pools)
            {
                var available = AvailableSlotModifiers(catalog, policy, incomingStageNumber, pool, owned, currentFamilies, encounter, unlocked);
                var candidates = available.FindAll(id => !excluded.Contains(catalog.GetModifier(id).Family.Identity));
                if (candidates.Count < MinimumCandidatesBeforeHistoryBypass)
                    candidates = available;
                ModifierDefinition.Id? selected = candidates.Count == 0 ? EligibleFallback(catalog, policy, incomingStageNumber, pool, unlocked)
                    : SelectModifier(catalog, policy, incomingStageNumber, candidates, ref random);
                if (!selected.HasValue) continue;
                var selectedFamily = catalog.GetModifier(selected.Value).Family.Identity;
                offeredIds.Add(selected.Value);
                currentFamilies.Add(selectedFamily);
                if (excluded.Add(selectedFamily)) history.Add(selectedFamily);
            }
            return new ModifierRewardGeneration(offeredIds, history);
        }

        public static ReadOnlyCollection<HandCategory> GenerateHandUpgrades(MechanicalCatalog catalog,
            RewardPolicyDefinition policy, int incomingStageNumber, IReadOnlyList<int> upgrades,
            IReadOnlyList<HandCategory> recentUpgrades, ref DeterministicRandom random)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (incomingStageNumber < 1) throw new ArgumentOutOfRangeException(nameof(incomingStageNumber));
            if (upgrades == null) throw new ArgumentNullException(nameof(upgrades));
            if (recentUpgrades == null) throw new ArgumentNullException(nameof(recentUpgrades));
            if (upgrades.Count != HandDefinition.CategoryCount)
                throw new ArgumentException("One upgrade count is required per hand category.", nameof(upgrades));
            for (int i = 0; i < upgrades.Count; ++i)
                if (upgrades[i] < 0 || upgrades[i] > catalog.Hands[i].MaximumUpgrades)
                    throw new ArgumentOutOfRangeException(nameof(upgrades));
            foreach (var category in recentUpgrades)
                if ((uint)category >= HandDefinition.CategoryCount) throw new ArgumentOutOfRangeException(nameof(recentUpgrades));
            var candidates = UpgradeCandidates(catalog, policy, incomingStageNumber, upgrades, recentUpgrades);
            if (candidates.Count <= MaximumUpgradeCandidatesWithoutDraw && candidates.Count <= policy.HandUpgradeOptionCount)
            {
                candidates.Sort();
                return candidates.AsReadOnly();
            }
            var selected = new List<HandCategory>();
            while (selected.Count < policy.HandUpgradeOptionCount && candidates.Count > 0)
            {
                int index = random.NextInt(candidates.Count);
                selected.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
            selected.Sort();
            return selected.AsReadOnly();
        }

        /** The hands an upgrade offer for the incoming stage chooses from. */
        internal static List<HandCategory> UpgradeCandidates(MechanicalCatalog catalog, RewardPolicyDefinition policy,
            int incomingStageNumber, IReadOnlyList<int> upgrades, IReadOnlyList<HandCategory> recentUpgrades)
        {
            var excluded = new HashSet<HandCategory>();
            int historyCount = Math.Min(recentUpgrades.Count, policy.ExcludedRecentChoices(incomingStageNumber));
            for (int i = 0; i < historyCount; ++i) excluded.Add(recentUpgrades[i]);
            var candidates = EligibleHands(catalog, upgrades, excluded);
            // Recent choices become eligible again when too few other hands remain to fill the offer.
            if (candidates.Count < policy.HandUpgradeOptionCount)
                candidates = EligibleHands(catalog, upgrades, new HashSet<HandCategory>());
            return candidates;
        }

        private static List<ModifierDefinition.Id> EligibleModifiers(MechanicalCatalog catalog, ModifierRewardPoolDefinition pool,
            int incomingStageNumber, HashSet<ModifierDefinition.Id> owned, HashSet<ModifierFamilyDefinition.Id> currentFamilies,
            EncounterDefinition encounter, ISet<ModifierDefinition.Id> unlocked)
        {
            var candidates = new List<ModifierDefinition.Id>();
            var ids = new List<ModifierDefinition.Id>(pool.ModifierIds);
            // The offer for an incoming stage follows clearing the stage before it.
            if (encounter != null &&
                incomingStageNumber - 1 >= encounter.EncounterModifiersFirstClearedStageNumber)
                foreach (var id in encounter.EncounterModifierIds)
                    if (!ids.Contains(id)) ids.Add(id);
            foreach (var id in ids)
            {
                var modifier = catalog.GetModifier(id);
                if (modifier.RewardEligible && modifier.Availability.IsAvailable(unlocked, id) &&
                    !owned.Contains(id) && !currentFamilies.Contains(modifier.Family.Identity)) candidates.Add(id);
            }
            return candidates;
        }

        internal static List<ModifierDefinition.Id> AvailableSlotModifiers(MechanicalCatalog catalog, RewardPolicyDefinition policy,
            int incomingStageNumber, ModifierRewardPoolDefinition pool, HashSet<ModifierDefinition.Id> owned,
            HashSet<ModifierFamilyDefinition.Id> currentFamilies, EncounterDefinition encounter, ISet<ModifierDefinition.Id> unlocked)
            => RestrictTiers(catalog, policy, incomingStageNumber, EligibleModifiers(catalog, pool, incomingStageNumber, owned,
                currentFamilies, encounter, unlocked));

        internal static ModifierDefinition.Id? EligibleFallback(MechanicalCatalog catalog, RewardPolicyDefinition policy,
            int incomingStageNumber, ModifierRewardPoolDefinition pool, ISet<ModifierDefinition.Id> unlocked)
        {
            if (!pool.FallbackModifierId.HasValue) return null;
            var id = pool.FallbackModifierId.Value;
            if (!catalog.GetModifier(id).Availability.IsAvailable(unlocked, id)) return null;
            return RestrictTiers(catalog, policy, incomingStageNumber, new List<ModifierDefinition.Id> { id }).Count == 0
                ? (ModifierDefinition.Id?)null : id;
        }

        private static ModifierTierRule TierRule(RewardPolicyDefinition policy, int incomingStageNumber)
        {
            ModifierTierRule result = null;
            foreach (var rule in policy.TierRules)
            {
                if (rule.FirstIncomingStageNumber > incomingStageNumber) break;
                result = rule;
            }
            return result;
        }

        private static List<ModifierDefinition.Id> RestrictTiers(MechanicalCatalog catalog, RewardPolicyDefinition policy,
            int incomingStageNumber, List<ModifierDefinition.Id> candidates)
        {
            int minimumTier = 1;
            foreach (var milestone in policy.TierMilestones)
                if (milestone.IncomingStageNumber == incomingStageNumber) minimumTier = milestone.MinimumTier;
            var strong = candidates.FindAll(id => catalog.GetModifier(id).Availability.PowerTier >= minimumTier);
            // A milestone reserves every slot for the minimum tier while that slot still has candidates.
            if (minimumTier > 1 && strong.Count > 0) return strong;
            var rule = TierRule(policy, incomingStageNumber);
            if (rule == null) return candidates;
            return candidates.FindAll(id => TierWeight(rule, catalog.GetModifier(id).Availability.PowerTier) > 0);
        }

        private static int TierWeight(ModifierTierRule rule, int tier)
            => tier <= rule.TierWeights.Count ? rule.TierWeights[tier - 1] : 0;

        private static ModifierDefinition.Id SelectModifier(MechanicalCatalog catalog, RewardPolicyDefinition policy,
            int incomingStageNumber, List<ModifierDefinition.Id> candidates, ref DeterministicRandom random)
        {
            var rule = TierRule(policy, incomingStageNumber);
            if (rule == null) return candidates[random.NextInt(candidates.Count)];
            var tiers = new SortedDictionary<int, List<ModifierDefinition.Id>>();
            foreach (var id in candidates)
            {
                int tier = catalog.GetModifier(id).Availability.PowerTier;
                if (!tiers.TryGetValue(tier, out var entries)) { entries = new List<ModifierDefinition.Id>(); tiers.Add(tier, entries); }
                entries.Add(id);
            }
            int total = 0;
            foreach (int tier in tiers.Keys) total = checked(total + TierWeight(rule, tier));
            bool uniform = total == 0;
            if (uniform) total = tiers.Count;
            int draw = random.NextInt(total);
            foreach (var tier in tiers)
            {
                draw -= uniform ? 1 : TierWeight(rule, tier.Key);
                if (draw < 0) return tier.Value[random.NextInt(tier.Value.Count)];
            }
            throw new InvalidOperationException("Tier selection has no candidate.");
        }

        private static List<HandCategory> EligibleHands(MechanicalCatalog catalog, IReadOnlyList<int> upgrades,
            HashSet<HandCategory> excluded)
        {
            var candidates = new List<HandCategory>();
            foreach (var hand in catalog.Hands)
                if (upgrades[(int)hand.Category] < hand.MaximumUpgrades && !excluded.Contains(hand.Category))
                    candidates.Add(hand.Category);
            return candidates;
        }
    }
}
