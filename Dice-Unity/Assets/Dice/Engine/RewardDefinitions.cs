using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    public sealed class ProgressionRewardDefinition
    {
        public ReadOnlyCollection<string> DiscoveredFamilyIds { get; }
        public ReadOnlyCollection<string> GrantedModifierIds { get; }

        public ProgressionRewardDefinition(IEnumerable<string> discoveredFamilyIds, IEnumerable<string> grantedModifierIds)
        {
            DiscoveredFamilyIds = CopyIds(discoveredFamilyIds, nameof(discoveredFamilyIds));
            GrantedModifierIds = CopyIds(grantedModifierIds, nameof(grantedModifierIds));
        }

        public void Validate(MechanicalCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var families = new HashSet<string>(StringComparer.Ordinal);
            foreach (var modifier in catalog.Modifiers) families.Add(modifier.FamilyId);
            foreach (string id in DiscoveredFamilyIds)
                if (!families.Contains(id))
                    throw new ArgumentException("A discovered family is missing from the catalog.", nameof(catalog));
            foreach (string id in GrantedModifierIds)
            {
                ModifierDefinition modifier;
                try { modifier = catalog.GetModifier(id); }
                catch (KeyNotFoundException exception)
                {
                    throw new ArgumentException("A granted modifier is missing from the catalog.", nameof(catalog), exception);
                }
                if (modifier.Availability.Access != ModifierAccess.PermanentUnlock ||
                    modifier.Availability.Scope == ModifierAvailabilityScope.Encounter ||
                    !modifier.IsPlayable)
                    throw new ArgumentException("Progression grants require playable permanent modifiers outside encounter scope.", nameof(catalog));
            }
        }

        private static ReadOnlyCollection<string> CopyIds(IEnumerable<string> source, string parameter)
        {
            if (source == null) throw new ArgumentNullException(parameter);
            var entries = new List<string>(source);
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in entries)
            {
                DefinitionValidation.RequireId(id, parameter);
                if (id.Trim() != id || id.Length > 256)
                    throw new ArgumentException("Progression IDs must be trimmed and at most 256 characters.", parameter);
                foreach (char value in id)
                    if (char.IsControl(value)) throw new ArgumentException("Progression IDs cannot contain control characters.", parameter);
                if (!unique.Add(id)) throw new ArgumentException("Progression IDs must be unique.", parameter);
            }
            return entries.AsReadOnly();
        }
    }

    public sealed class ModifierRewardPoolDefinition
    {
        public string Id { get; }
        public ReadOnlyCollection<string> ModifierIds { get; }
        public string FallbackModifierId { get; }

        public ModifierRewardPoolDefinition(string id, IEnumerable<string> modifierIds, string fallbackModifierId = null)
        {
            DefinitionValidation.RequireId(id, nameof(id));
            if (modifierIds == null) throw new ArgumentNullException(nameof(modifierIds));
            var entries = new List<string>(modifierIds);
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string modifierId in entries)
            {
                DefinitionValidation.RequireId(modifierId, nameof(modifierIds));
                if (!unique.Add(modifierId)) throw new ArgumentException("Pool members must be unique.", nameof(modifierIds));
            }
            if (entries.Count == 0) throw new ArgumentException("A reward pool requires at least one member.", nameof(modifierIds));
            if (fallbackModifierId != null)
            {
                DefinitionValidation.RequireId(fallbackModifierId, nameof(fallbackModifierId));
                if (unique.Contains(fallbackModifierId))
                    throw new ArgumentException("The fallback is separate from normal pool members.", nameof(fallbackModifierId));
            }
            Id = id;
            ModifierIds = entries.AsReadOnly();
            FallbackModifierId = fallbackModifierId;
        }
    }

    public sealed class ModifierRewardLayout
    {
        public ReadOnlyCollection<string> PoolIds { get; }

        public ModifierRewardLayout(IEnumerable<string> poolIds)
        {
            if (poolIds == null) throw new ArgumentNullException(nameof(poolIds));
            var entries = new List<string>(poolIds);
            foreach (string poolId in entries) DefinitionValidation.RequireId(poolId, nameof(poolIds));
            if (entries.Count == 0) throw new ArgumentException("A reward layout requires at least one slot.", nameof(poolIds));
            PoolIds = entries.AsReadOnly();
        }
    }

    public readonly struct HandUpgradeHistoryRule
    {
        public int MaxIncomingStageNumber { get; }
        public int ExcludedRecentChoices { get; }

        public HandUpgradeHistoryRule(int maxIncomingStageNumber, int excludedRecentChoices)
        {
            if (maxIncomingStageNumber < 1) throw new ArgumentOutOfRangeException(nameof(maxIncomingStageNumber));
            if (excludedRecentChoices < 0) throw new ArgumentOutOfRangeException(nameof(excludedRecentChoices));
            MaxIncomingStageNumber = maxIncomingStageNumber;
            ExcludedRecentChoices = excludedRecentChoices;
        }
    }

    public sealed class ModifierTierRule
    {
        public int FirstIncomingStageNumber { get; }
        public ReadOnlyCollection<int> TierWeights { get; }

        public ModifierTierRule(int firstIncomingStageNumber, IEnumerable<int> tierWeights)
        {
            if (firstIncomingStageNumber < 1) throw new ArgumentOutOfRangeException(nameof(firstIncomingStageNumber));
            if (tierWeights == null) throw new ArgumentNullException(nameof(tierWeights));
            var weights = new List<int>(tierWeights);
            long total = 0;
            foreach (int weight in weights)
            {
                if (weight < 0) throw new ArgumentOutOfRangeException(nameof(tierWeights));
                total += weight;
            }
            if (total < 1 || total > int.MaxValue) throw new ArgumentException("Tier weights require a positive integer total.", nameof(tierWeights));
            FirstIncomingStageNumber = firstIncomingStageNumber;
            TierWeights = weights.AsReadOnly();
        }
    }

    public readonly struct ModifierTierMilestone
    {
        public int IncomingStageNumber { get; }
        public int MinimumTier { get; }
        public ModifierTierMilestone(int incomingStageNumber, int minimumTier)
        {
            if (incomingStageNumber < 1) throw new ArgumentOutOfRangeException(nameof(incomingStageNumber));
            if (minimumTier < 1) throw new ArgumentOutOfRangeException(nameof(minimumTier));
            IncomingStageNumber = incomingStageNumber;
            MinimumTier = minimumTier;
        }
    }

    public sealed class RewardPolicyDefinition
    {
        public string Id { get; }
        public ReadOnlyCollection<ModifierRewardLayout> ModifierLayouts { get; }
        public int StartingRefreshes { get; }
        public int RefreshesPerClear { get; }
        public int RefreshCost { get; }
        public int HandUpgradeOptionCount { get; }
        public ReadOnlyCollection<HandUpgradeHistoryRule> UpgradeHistory { get; }
        public ReadOnlyCollection<ModifierTierRule> TierRules { get; }
        public ReadOnlyCollection<ModifierTierMilestone> TierMilestones { get; }

        public RewardPolicyDefinition(string id, IEnumerable<ModifierRewardLayout> modifierLayouts,
            int startingRefreshes, int refreshesPerClear, int refreshCost, int handUpgradeOptionCount,
            IEnumerable<HandUpgradeHistoryRule> upgradeHistory, IEnumerable<ModifierTierRule> tierRules = null,
            IEnumerable<ModifierTierMilestone> tierMilestones = null)
        {
            DefinitionValidation.RequireId(id, nameof(id));
            if (modifierLayouts == null) throw new ArgumentNullException(nameof(modifierLayouts));
            if (upgradeHistory == null) throw new ArgumentNullException(nameof(upgradeHistory));
            if (startingRefreshes < 0) throw new ArgumentOutOfRangeException(nameof(startingRefreshes));
            if (refreshesPerClear < 0) throw new ArgumentOutOfRangeException(nameof(refreshesPerClear));
            if (refreshCost < 1) throw new ArgumentOutOfRangeException(nameof(refreshCost));
            if (handUpgradeOptionCount < 1 || handUpgradeOptionCount > HandDefinition.CategoryCount)
                throw new ArgumentOutOfRangeException(nameof(handUpgradeOptionCount));
            var layouts = new List<ModifierRewardLayout>(modifierLayouts);
            if (layouts.Count == 0 || layouts.Exists(layout => layout == null))
                throw new ArgumentException("Reward layouts must be nonempty and nonnull.", nameof(modifierLayouts));
            var rules = new List<HandUpgradeHistoryRule>(upgradeHistory);
            int previousBoundary = 0;
            foreach (var rule in rules)
            {
                if (rule.MaxIncomingStageNumber <= previousBoundary || rule.ExcludedRecentChoices < 0)
                    throw new ArgumentException("Upgrade history rules require increasing positive boundaries.", nameof(upgradeHistory));
                previousBoundary = rule.MaxIncomingStageNumber;
            }
            if (previousBoundary != int.MaxValue)
                throw new ArgumentException("Upgrade history rules must cover every incoming stage.", nameof(upgradeHistory));
            Id = id;
            ModifierLayouts = layouts.AsReadOnly();
            StartingRefreshes = startingRefreshes;
            RefreshesPerClear = refreshesPerClear;
            RefreshCost = refreshCost;
            HandUpgradeOptionCount = handUpgradeOptionCount;
            UpgradeHistory = rules.AsReadOnly();
            var tiers = tierRules == null ? new List<ModifierTierRule>() : new List<ModifierTierRule>(tierRules);
            int previousStage = 0;
            foreach (var rule in tiers)
            {
                if (rule == null || rule.FirstIncomingStageNumber <= previousStage)
                    throw new ArgumentException("Tier rules require increasing stage boundaries.", nameof(tierRules));
                previousStage = rule.FirstIncomingStageNumber;
            }
            if (tiers.Count > 0 && tiers[0].FirstIncomingStageNumber != 1)
                throw new ArgumentException("Tier rules must cover the first incoming stage.", nameof(tierRules));
            TierRules = tiers.AsReadOnly();
            var milestones = tierMilestones == null ? new List<ModifierTierMilestone>() : new List<ModifierTierMilestone>(tierMilestones);
            var milestoneStages = new HashSet<int>();
            foreach (var milestone in milestones)
                if (milestone.IncomingStageNumber < 1 || milestone.MinimumTier < 1 || !milestoneStages.Add(milestone.IncomingStageNumber))
                    throw new ArgumentException("Tier milestones require unique positive stages and tiers.", nameof(tierMilestones));
            TierMilestones = milestones.AsReadOnly();
        }
    }

    public sealed class ModifierRewardGeneration
    {
        public ReadOnlyCollection<string> ModifierIds { get; }
        public ReadOnlyCollection<string> ExcludedFamilyIds { get; }

        internal ModifierRewardGeneration(IEnumerable<string> modifierIds, IEnumerable<string> excludedFamilyIds)
        {
            ModifierIds = new List<string>(modifierIds).AsReadOnly();
            ExcludedFamilyIds = new List<string>(excludedFamilyIds).AsReadOnly();
        }
    }
}
