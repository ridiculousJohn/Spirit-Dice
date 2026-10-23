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
            ref DeterministicRandom random, EncounterDefinition encounter = null, IEnumerable<string> unlockedModifierIds = null,
            int incomingStageNumber = 0)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (rewardCycleIndex < 0) throw new ArgumentOutOfRangeException(nameof(rewardCycleIndex));
            if (ownedModifierIds == null) throw new ArgumentNullException(nameof(ownedModifierIds));
            if (excludedFamilyIds == null) throw new ArgumentNullException(nameof(excludedFamilyIds));
            if (incomingStageNumber == 0) incomingStageNumber = checked(rewardCycleIndex + 1);
            if (incomingStageNumber < 1) throw new ArgumentOutOfRangeException(nameof(incomingStageNumber));
            var unlocked = new HashSet<string>(unlockedModifierIds ?? Array.Empty<string>(), StringComparer.Ordinal);
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
                var available = AvailableSlotModifiers(catalog, policy, incomingStageNumber, pool, owned, currentFamilies, encounter, unlocked);
                var candidates = available.FindAll(id => !excluded.Contains(catalog.GetModifier(id).FamilyId));
                if (candidates.Count < MinimumCandidatesBeforeHistoryBypass)
                    candidates = available;
                string selected = candidates.Count == 0 ? EligibleFallback(catalog, policy, incomingStageNumber, pool, encounter, unlocked)
                    : SelectModifier(catalog, policy, incomingStageNumber, candidates, ref random);
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
            int incomingStageNumber, HashSet<string> owned, HashSet<string> excluded, HashSet<string> currentFamilies,
            EncounterDefinition encounter, ISet<string> unlocked, bool includeEncounterModifiers)
        {
            var candidates = new List<string>();
            var ids = new List<string>(pool.ModifierIds);
            // The offer for an incoming stage follows clearing the stage before it.
            if (includeEncounterModifiers && encounter != null &&
                incomingStageNumber - 1 >= encounter.EncounterModifiersFirstClearedStageNumber)
                foreach (string id in encounter.EncounterModifierIds)
                    if (!ids.Contains(id)) ids.Add(id);
            foreach (string id in ids)
            {
                var modifier = catalog.GetModifier(id);
                if (modifier.RewardEligible && modifier.Availability.IsAvailable(encounter, unlocked, id) &&
                    !owned.Contains(id) && !excluded.Contains(modifier.FamilyId) &&
                    !currentFamilies.Contains(modifier.FamilyId)) candidates.Add(id);
            }
            return candidates;
        }

        internal static List<string> AvailableSlotModifiers(MechanicalCatalog catalog, RewardPolicyDefinition policy,
            int incomingStageNumber, ModifierRewardPoolDefinition pool, HashSet<string> owned,
            HashSet<string> currentFamilies, EncounterDefinition encounter, ISet<string> unlocked,
            bool includeEncounterModifiers = true)
            => RestrictTiers(catalog, policy, incomingStageNumber, EligibleModifiers(catalog, pool, incomingStageNumber, owned,
                new HashSet<string>(StringComparer.Ordinal), currentFamilies, encounter, unlocked, includeEncounterModifiers));

        internal static string EligibleFallback(MechanicalCatalog catalog, RewardPolicyDefinition policy, int incomingStageNumber,
            ModifierRewardPoolDefinition pool, EncounterDefinition encounter, ISet<string> unlocked)
        {
            string id = pool.FallbackModifierId;
            if (id == null || !catalog.GetModifier(id).Availability.IsAvailable(encounter, unlocked, id)) return null;
            return RestrictTiers(catalog, policy, incomingStageNumber, new List<string> { id }).Count == 0 ? null : id;
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

        private static List<string> RestrictTiers(MechanicalCatalog catalog, RewardPolicyDefinition policy,
            int incomingStageNumber, List<string> candidates)
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

        private static string SelectModifier(MechanicalCatalog catalog, RewardPolicyDefinition policy,
            int incomingStageNumber, List<string> candidates, ref DeterministicRandom random)
        {
            var rule = TierRule(policy, incomingStageNumber);
            if (rule == null) return candidates[random.NextInt(candidates.Count)];
            var tiers = new SortedDictionary<int, List<string>>();
            foreach (string id in candidates)
            {
                int tier = catalog.GetModifier(id).Availability.PowerTier;
                if (!tiers.TryGetValue(tier, out var entries)) { entries = new List<string>(); tiers.Add(tier, entries); }
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
