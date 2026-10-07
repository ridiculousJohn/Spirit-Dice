using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    public static class RewardGenerator
    {
        private const int MinimumCandidatesBeforeHistoryBypass = 3;

        public static ModifierRewardGeneration GenerateModifiers(MechanicalCatalog catalog, RewardPolicyDefinition policy,
            int rewardCycleIndex, IEnumerable<string> ownedModifierIds, IEnumerable<string> excludedFamilyIds,
            ref DeterministicRandom random)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (rewardCycleIndex < 0) throw new ArgumentOutOfRangeException(nameof(rewardCycleIndex));
            if (ownedModifierIds == null) throw new ArgumentNullException(nameof(ownedModifierIds));
            if (excludedFamilyIds == null) throw new ArgumentNullException(nameof(excludedFamilyIds));
            var owned = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ownedModifierIds) { catalog.GetModifier(id); owned.Add(id); }
            var knownFamilies = new HashSet<string>(StringComparer.Ordinal);
            foreach (var modifier in catalog.Modifiers) knownFamilies.Add(modifier.FamilyId);
            var history = new List<string>();
            var excluded = new HashSet<string>(StringComparer.Ordinal);
            foreach (string familyId in excludedFamilyIds)
            {
                DefinitionValidation.RequireId(familyId, nameof(excludedFamilyIds));
                if (!knownFamilies.Contains(familyId))
                    throw new ArgumentException("An excluded modifier family is missing from the catalog.", nameof(excludedFamilyIds));
                if (excluded.Add(familyId)) history.Add(familyId);
            }
            var layout = policy.ModifierLayouts[rewardCycleIndex % policy.ModifierLayouts.Count];
            var pools = new List<ModifierRewardPoolDefinition>();
            foreach (string poolId in layout.PoolIds) pools.Add(catalog.GetRewardPool(poolId));
            var offeredIds = new List<string>();
            var currentFamilies = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pool in pools)
            {
                var candidates = EligibleModifiers(catalog, pool, owned, excluded, currentFamilies);
                if (candidates.Count < MinimumCandidatesBeforeHistoryBypass)
                    candidates = EligibleModifiers(catalog, pool, owned,
                        new HashSet<string>(StringComparer.Ordinal), currentFamilies);
                string selected = candidates.Count == 0 ? pool.FallbackModifierId : candidates[random.NextInt(candidates.Count)];
                if (selected == null) continue;
                string selectedFamily = catalog.GetModifier(selected).FamilyId;
                offeredIds.Add(selected);
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
            int historyWindow = 0;
            foreach (var rule in policy.UpgradeHistory)
                if (incomingStageNumber <= rule.MaxIncomingStageNumber) { historyWindow = rule.ExcludedRecentChoices; break; }
            var excluded = new HashSet<HandCategory>();
            int historyCount = Math.Min(recentUpgrades.Count, historyWindow);
            for (int i = 0; i < historyCount; ++i) excluded.Add(recentUpgrades[i]);
            var candidates = EligibleHands(catalog, upgrades, excluded);
            if (candidates.Count < policy.HandUpgradeOptionCount)
                candidates = EligibleHands(catalog, upgrades, new HashSet<HandCategory>());
            if (candidates.Count <= 3 && candidates.Count <= policy.HandUpgradeOptionCount)
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

        private static List<string> EligibleModifiers(MechanicalCatalog catalog, ModifierRewardPoolDefinition pool,
            HashSet<string> owned, HashSet<string> excluded, HashSet<string> currentFamilies)
        {
            var candidates = new List<string>();
            foreach (string id in pool.ModifierIds)
            {
                var modifier = catalog.GetModifier(id);
                if (modifier.RewardEligible && !owned.Contains(id) && !excluded.Contains(modifier.FamilyId) &&
                    !currentFamilies.Contains(modifier.FamilyId)) candidates.Add(id);
            }
            return candidates;
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
