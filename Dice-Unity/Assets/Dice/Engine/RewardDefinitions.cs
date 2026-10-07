using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
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

    public sealed class RewardPolicyDefinition
    {
        public string Id { get; }
        public ReadOnlyCollection<ModifierRewardLayout> ModifierLayouts { get; }
        public int StartingRefreshes { get; }
        public int RefreshesPerClear { get; }
        public int RefreshCost { get; }
        public int HandUpgradeOptionCount { get; }
        public ReadOnlyCollection<HandUpgradeHistoryRule> UpgradeHistory { get; }

        public RewardPolicyDefinition(string id, IEnumerable<ModifierRewardLayout> modifierLayouts,
            int startingRefreshes, int refreshesPerClear, int refreshCost, int handUpgradeOptionCount,
            IEnumerable<HandUpgradeHistoryRule> upgradeHistory)
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
