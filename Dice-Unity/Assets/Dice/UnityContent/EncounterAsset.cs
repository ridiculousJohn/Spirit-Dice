using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.UnityContent
{
    /**
     * An encounter: its stages, rules, pieces, charms and reward policy. Its identity is its asset GUID. The game's
     * encounters are those the mechanical catalog lists.
     */
    [CreateAssetMenu(menuName = "Dice/Encounter")]
    public sealed class EncounterAsset : ContentAsset
    {
        [SerializeField] private string spiritId;
        [SerializeField] private UnlockMilestoneData[] unlockMilestones = Array.Empty<UnlockMilestoneData>();
        [SerializeField] private ModifierAsset[] encounterModifiers = Array.Empty<ModifierAsset>();
        [SerializeField, Min(1)] private int encounterModifiersFirstClearedStageNumber = 1;
        [SerializeField] private ModifierAsset[] unlockRewardModifiers = Array.Empty<ModifierAsset>();
        [SerializeField] private int[] startingUpgrades = new int[HandDefinition.CategoryCount];
        [SerializeField] private ModifierAsset[] startingModifiers = Array.Empty<ModifierAsset>();
        [SerializeField] private StageData[] stages = Array.Empty<StageData>();
        [SerializeField] private RewardPolicyData rewardPolicy;
        [SerializeField] private EncounterRuleAsset[] rules = Array.Empty<EncounterRuleAsset>();
        [SerializeField] private PieceData[] pieces = Array.Empty<PieceData>();

        public EncounterDefinition.Id Identity => new EncounterDefinition.Id(AssetGuid);

        /** The encounter's definition; each charm it references is passed to use, so the catalog collects its charms. */
        internal EncounterDefinition Build(Action<ModifierAsset> use)
        {
            if (stages == null) throw new InvalidOperationException("The encounter " + name + " has no stage list.");
            var stageDefinitions = new StageDefinition[stages.Length];
            for (int i = 0; i < stageDefinitions.Length; ++i)
            {
                var stage = stages[i] ?? throw new InvalidOperationException("The encounter " + name + " has a missing stage.");
                stageDefinitions[i] = new StageDefinition(stage.threshold, stage.startingHands, stage.startingRerolls, stage.exitBehavior);
            }
            return new EncounterDefinition(Identity, stageDefinitions, startingUpgrades, MechanicalCatalogAsset.Ids(startingModifiers, use),
                BuildRewardPolicy(rewardPolicy), BuildRules(rules), string.IsNullOrEmpty(spiritId) ? null : spiritId,
                BuildMilestones(unlockMilestones), MechanicalCatalogAsset.Ids(encounterModifiers, use),
                MechanicalCatalogAsset.Ids(unlockRewardModifiers, use), BuildPieces(pieces),
                encounterModifiersFirstClearedStageNumber);
        }

        private static ModifierUnlockMilestone[] BuildMilestones(UnlockMilestoneData[] sources)
        {
            if (sources == null) throw new InvalidOperationException("Unlock milestones are missing.");
            var definitions = new ModifierUnlockMilestone[sources.Length];
            for (int i = 0; i < sources.Length; ++i)
            {
                var source = sources[i] ?? throw new InvalidOperationException("An unlock milestone is missing.");
                definitions[i] = new ModifierUnlockMilestone(source.stageNumber);
            }
            return definitions;
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

        private static RewardPolicyDefinition BuildRewardPolicy(RewardPolicyData source)
        {
            // An encounter without reward offers leaves its policy id empty; it then awaits rewards from outside the engine.
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
        private sealed class PieceData
        {
            public string id;
            public PieceInteractionAsset[] interactions = Array.Empty<PieceInteractionAsset>();
        }

        /** The reward policy names the catalog's reward pools by their ids. */
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
