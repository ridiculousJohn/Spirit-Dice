using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    [CreateAssetMenu(menuName = "Dice/Mechanical Catalog")]
    public sealed class MechanicalCatalogAsset : ScriptableObject
    {
        [SerializeField] private string catalogVersion;
        [SerializeField] private HandData[] hands = Array.Empty<HandData>();
        [SerializeField] private EncounterData[] encounters = Array.Empty<EncounterData>();
        [SerializeField] private RewardPoolData[] rewardPools = Array.Empty<RewardPoolData>();

        public MechanicalCatalog Build(IEnumerable<ModifierAsset> modifiers)
        {
            if (modifiers == null) throw new ArgumentNullException(nameof(modifiers));
            if (hands == null || encounters == null || rewardPools == null)
                throw new InvalidOperationException("Catalog arrays are missing.");
            var modifierAssets = new List<ModifierAsset>(modifiers);
            if (modifierAssets.Exists(source => source == null))
                throw new ArgumentException("A modifier asset is missing.", nameof(modifiers));
            modifierAssets.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            var handDefinitions = new HandDefinition[hands.Length];
            for (int i = 0; i < hands.Length; ++i)
            {
                var source = hands[i] ?? throw new InvalidOperationException("A hand entry is missing.");
                handDefinitions[i] = new HandDefinition(source.id, source.category, source.baseScore, source.multiplier,
                    source.baseScorePerUpgrade, source.multiplierPerUpgrade, source.maximumUpgrades);
            }
            var encounterDefinitions = new EncounterDefinition[encounters.Length];
            for (int i = 0; i < encounters.Length; ++i)
            {
                var source = encounters[i] ?? throw new InvalidOperationException("An encounter entry is missing.");
                if (source.stages == null) throw new InvalidOperationException("Encounter stages are missing.");
                var stages = new StageDefinition[source.stages.Length];
                for (int j = 0; j < stages.Length; ++j)
                {
                    var stage = source.stages[j] ?? throw new InvalidOperationException("A stage entry is missing.");
                    stages[j] = new StageDefinition(stage.threshold, stage.startingHands, stage.startingRerolls, stage.exitBehavior);
                }
                encounterDefinitions[i] = new EncounterDefinition(source.id, stages, source.startingUpgrades,
                    source.startingModifiers, BuildRewardPolicy(source.rewardPolicy), BuildRules(source.rules),
                    string.IsNullOrEmpty(source.spiritId) ? null : source.spiritId, BuildMilestones(source.unlockMilestones),
                    source.encounterModifierIds, source.unlockRewardModifierIds, BuildPieces(source.pieces),
                    source.encounterModifiersFirstClearedStageNumber);
            }
            var modifierDefinitions = new ModifierDefinition[modifierAssets.Count];
            for (int i = 0; i < modifierAssets.Count; ++i)
            {
                modifierDefinitions[i] = modifierAssets[i].Build();
            }
            var rewardPoolDefinitions = new ModifierRewardPoolDefinition[rewardPools.Length];
            for (int i = 0; i < rewardPools.Length; ++i)
            {
                var source = rewardPools[i] ?? throw new InvalidOperationException("A reward pool entry is missing.");
                rewardPoolDefinitions[i] = new ModifierRewardPoolDefinition(source.id, source.modifierIds,
                    string.IsNullOrEmpty(source.fallbackModifierId) ? null : source.fallbackModifierId);
            }
            return new MechanicalCatalog(catalogVersion, handDefinitions, encounterDefinitions,
                modifierDefinitions, rewardPoolDefinitions);
        }

        private static EncounterRuleDefinition[] BuildRules(EncounterRuleAsset[] sources)
        {
            if (sources == null) throw new InvalidOperationException("Encounter rules are missing.");
            var definitions = new EncounterRuleDefinition[sources.Length];
            for (int i = 0; i < sources.Length; ++i)
            {
                var source = sources[i] ?? throw new InvalidOperationException("An encounter rule asset is missing.");
                definitions[i] = source.Build();
            }
            return definitions;
        }

        private static TablePieceDefinition[] BuildPieces(PieceData[] sources)
        {
            if (sources == null) throw new InvalidOperationException("Table pieces are missing.");
            var definitions = new TablePieceDefinition[sources.Length];
            for (int i = 0; i < sources.Length; ++i)
            {
                var source = sources[i] ?? throw new InvalidOperationException("A table piece entry is missing.");
                if (source.interactions == null) throw new InvalidOperationException("Table piece interactions are missing.");
                var interactions = new PieceInteractionDefinition[source.interactions.Length];
                for (int j = 0; j < interactions.Length; ++j)
                {
                    var interaction = source.interactions[j] ?? throw new InvalidOperationException("A piece interaction asset is missing.");
                    interactions[j] = interaction.Build();
                }
                definitions[i] = new TablePieceDefinition(source.id, interactions);
            }
            return definitions;
        }

        private static ModifierUnlockMilestone[] BuildMilestones(UnlockMilestoneData[] sources)
        {
            if (sources == null) throw new InvalidOperationException("Unlock milestones are missing.");
            var definitions = new ModifierUnlockMilestone[sources.Length];
            for (int i = 0; i < sources.Length; ++i)
            {
                var source = sources[i] ?? throw new InvalidOperationException("An unlock milestone is missing.");
                definitions[i] = new ModifierUnlockMilestone(source.stageNumber, source.modifierIds);
            }
            return definitions;
        }

        private static RewardPolicyDefinition BuildRewardPolicy(RewardPolicyData source)
        {
            if (source == null || string.IsNullOrEmpty(source.id)) return null;
            if (source.modifierLayouts == null || source.upgradeHistory == null)
                throw new InvalidOperationException("Reward policy arrays are missing.");
            var layouts = new ModifierRewardLayout[source.modifierLayouts.Length];
            for (int i = 0; i < layouts.Length; ++i)
            {
                var layout = source.modifierLayouts[i] ?? throw new InvalidOperationException("A reward layout is missing.");
                layouts[i] = new ModifierRewardLayout(layout.poolIds);
            }
            var history = new HandUpgradeHistoryRule[source.upgradeHistory.Length];
            for (int i = 0; i < history.Length; ++i)
            {
                var rule = source.upgradeHistory[i] ?? throw new InvalidOperationException("An upgrade history rule is missing.");
                history[i] = new HandUpgradeHistoryRule(rule.maxIncomingStageNumber, rule.excludedRecentChoices);
            }
            if (source.tierRules == null || source.tierMilestones == null)
                throw new InvalidOperationException("Reward tier schedules are missing.");
            var tierRules = new ModifierTierRule[source.tierRules.Length];
            for (int i = 0; i < tierRules.Length; ++i)
            {
                var rule = source.tierRules[i] ?? throw new InvalidOperationException("A tier rule is missing.");
                tierRules[i] = new ModifierTierRule(rule.firstIncomingStageNumber, rule.tierWeights);
            }
            var tierMilestones = new ModifierTierMilestone[source.tierMilestones.Length];
            for (int i = 0; i < tierMilestones.Length; ++i)
            {
                var milestone = source.tierMilestones[i] ?? throw new InvalidOperationException("A tier milestone is missing.");
                tierMilestones[i] = new ModifierTierMilestone(milestone.incomingStageNumber, milestone.minimumTier);
            }
            return new RewardPolicyDefinition(source.id, layouts, source.startingRefreshes,
                source.refreshesPerClear, source.refreshCost, source.handUpgradeOptionCount, history, tierRules, tierMilestones);
        }

        [Serializable]
        private sealed class HandData
        {
            public string id;
            public HandCategory category;
            public long baseScore;
            public long multiplier;
            public long baseScorePerUpgrade;
            public long multiplierPerUpgrade;
            public int maximumUpgrades;
        }

        [Serializable]
        private sealed class EncounterData
        {
            public string id;
            public string spiritId;
            public UnlockMilestoneData[] unlockMilestones = Array.Empty<UnlockMilestoneData>();
            public string[] encounterModifierIds = Array.Empty<string>();
            [Min(1)] public int encounterModifiersFirstClearedStageNumber = 1;
            public string[] unlockRewardModifierIds = Array.Empty<string>();
            public int[] startingUpgrades = new int[HandDefinition.CategoryCount];
            public string[] startingModifiers = Array.Empty<string>();
            public StageData[] stages = Array.Empty<StageData>();
            public RewardPolicyData rewardPolicy;
            public EncounterRuleAsset[] rules = Array.Empty<EncounterRuleAsset>();
            public PieceData[] pieces = Array.Empty<PieceData>();
        }

        [Serializable]
        private sealed class PieceData
        {
            public string id;
            public PieceInteractionAsset[] interactions = Array.Empty<PieceInteractionAsset>();
        }

        [Serializable]
        private sealed class RewardPoolData
        {
            public string id;
            public string[] modifierIds = Array.Empty<string>();
            public string fallbackModifierId;
        }

        [Serializable]
        private sealed class RewardPolicyData
        {
            public string id;
            public RewardLayoutData[] modifierLayouts = Array.Empty<RewardLayoutData>();
            public int startingRefreshes;
            public int refreshesPerClear;
            public int refreshCost;
            public int handUpgradeOptionCount;
            public UpgradeHistoryData[] upgradeHistory = Array.Empty<UpgradeHistoryData>();
            public TierRuleData[] tierRules = Array.Empty<TierRuleData>();
            public TierMilestoneData[] tierMilestones = Array.Empty<TierMilestoneData>();
        }

        [Serializable]
        private sealed class UnlockMilestoneData
        {
            public int stageNumber;
            public string[] modifierIds = Array.Empty<string>();
        }

        [Serializable]
        private sealed class TierRuleData
        {
            public int firstIncomingStageNumber;
            public int[] tierWeights = Array.Empty<int>();
        }

        [Serializable]
        private sealed class TierMilestoneData
        {
            public int incomingStageNumber;
            public int minimumTier;
        }

        [Serializable]
        private sealed class RewardLayoutData
        {
            public string[] poolIds = Array.Empty<string>();
        }

        [Serializable]
        private sealed class UpgradeHistoryData
        {
            public int maxIncomingStageNumber;
            public int excludedRecentChoices;
        }

        [Serializable]
        private sealed class StageData
        {
            public long threshold;
            public int startingHands;
            public int startingRerolls;
            public StageExitBehavior exitBehavior;
        }
    }
}
