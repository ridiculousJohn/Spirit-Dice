using System;
using System.Collections.Generic;
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
        [SerializeField] private UnlockMilestoneData[] unlockMilestones = Array.Empty<UnlockMilestoneData>();
        [SerializeField] private ModifierAsset[] encounterModifiers = Array.Empty<ModifierAsset>();
        [SerializeField, Min(1)] private int encounterModifiersFirstClearedStageNumber = 1;
        [SerializeField] private ModifierAsset[] unlockRewardModifiers = Array.Empty<ModifierAsset>();
        [SerializeField] private int[] startingUpgrades = new int[HandDefinition.CategoryCount];
        [SerializeField] private ModifierAsset[] startingModifiers = Array.Empty<ModifierAsset>();
        [SerializeField] private StageData[] stages = Array.Empty<StageData>();
        /** The encounter's reward offers; an encounter without a policy makes none and awaits rewards from outside the engine. */
        [SerializeReference, SelectImplementation] private RewardPolicyData rewardPolicy;
        [SerializeField] private EncounterRuleAsset[] rules = Array.Empty<EncounterRuleAsset>();
        [SerializeField] private PieceData[] pieces = Array.Empty<PieceData>();

        public EncounterDefinition.Id Identity => new EncounterDefinition.Id(AssetGuid);

        /**
         * The charms this encounter's offers add to its pools, as authored. For an encounter-exclusive charm this list is
         * the only statement of the encounter that offers it.
         */
        public IReadOnlyList<ModifierAsset> EncounterModifiers => encounterModifiers;

        /**
         * The encounter's definition; each charm it references is passed to use, so the catalog collects its charms, and
         * each reward pool its layouts reference is built by pool.
         */
        internal EncounterDefinition Build(Action<ModifierAsset> use, Func<RewardPoolAsset, ModifierRewardPoolDefinition> pool)
        {
            if (stages == null) throw new InvalidOperationException("The encounter " + name + " has no stage list.");
            var stageDefinitions = new StageDefinition[stages.Length];
            for (int i = 0; i < stageDefinitions.Length; ++i)
            {
                var stage = stages[i] ?? throw new InvalidOperationException("The encounter " + name + " has a missing stage.");
                try
                {
                    stageDefinitions[i] = new StageDefinition(stage.threshold, stage.startingHands, stage.startingRerolls, stage.exitBehavior);
                }
                catch (ArgumentException exception)
                {
                    throw new InvalidOperationException("Stage " + (i + 1) + " of the encounter " + name + " is invalid: " +
                        exception.Message, exception);
                }
            }
            // Built in this order, the charms reach use in the order the catalog lists them.
            var startingIds = MechanicalCatalogAsset.Ids(startingModifiers, "the starting charms of the encounter " + name, use);
            RewardPolicyDefinition policy = BuildRewardPolicy(rewardPolicy, pool);
            EncounterRuleDefinition[] ruleDefinitions = BuildRules(rules);
            ModifierUnlockMilestone[] milestones = BuildMilestones(unlockMilestones);
            var additionIds = MechanicalCatalogAsset.Ids(encounterModifiers, "the charm additions of the encounter " + name, use);
            var unlockIds = MechanicalCatalogAsset.Ids(unlockRewardModifiers, "the unlock rewards of the encounter " + name, use);
            TablePieceDefinition[] pieceDefinitions = BuildPieces(pieces);
            try
            {
                return new EncounterDefinition(Identity, stageDefinitions, startingUpgrades, startingIds, policy, ruleDefinitions,
                    milestones, additionIds, unlockIds, pieceDefinitions, encounterModifiersFirstClearedStageNumber);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException("The encounter " + name + " is invalid: " + exception.Message, exception);
            }
        }

        private ModifierUnlockMilestone[] BuildMilestones(UnlockMilestoneData[] sources)
        {
            if (sources == null) throw new InvalidOperationException("The encounter " + name + " has no unlock milestone list.");
            var definitions = new ModifierUnlockMilestone[sources.Length];
            for (int i = 0; i < sources.Length; ++i)
            {
                var source = sources[i] ?? throw new InvalidOperationException("The encounter " + name + " has a missing unlock milestone.");
                try { definitions[i] = new ModifierUnlockMilestone(source.stageNumber); }
                catch (ArgumentException exception)
                {
                    throw new InvalidOperationException("Unlock milestone " + (i + 1) + " of the encounter " + name + " is invalid: " +
                        exception.Message, exception);
                }
            }
            return definitions;
        }

        private EncounterRuleDefinition[] BuildRules(EncounterRuleAsset[] sources)
        {
            if (sources == null) throw new InvalidOperationException("The encounter " + name + " has no rule list.");
            var definitions = new EncounterRuleDefinition[sources.Length];
            for (int i = 0; i < sources.Length; ++i)
            {
                var source = sources[i] ?? throw new InvalidOperationException("The encounter " + name + " has a missing rule asset.");
                try { definitions[i] = source.Build(); }
                catch (ArgumentException exception)
                {
                    throw new InvalidOperationException("The rule " + source.name + " of the encounter " + name + " is invalid: " +
                        exception.Message, exception);
                }
            }
            return definitions;
        }

        private TablePieceDefinition[] BuildPieces(PieceData[] sources)
        {
            if (sources == null) throw new InvalidOperationException("The encounter " + name + " has no table piece list.");
            var definitions = new TablePieceDefinition[sources.Length];
            for (int i = 0; i < sources.Length; ++i)
            {
                var source = sources[i] ??
                    throw new InvalidOperationException("The encounter " + name + " has a missing table piece entry.");
                if (source.piece == null)
                    throw new InvalidOperationException("The encounter " + name + " has a table piece entry without its piece.");
                string where = "The table piece " + source.piece.name + " of the encounter " + name;
                if (source.interactions == null) throw new InvalidOperationException(where + " has no interaction list.");
                var interactions = new PieceInteractionDefinition[source.interactions.Length];
                for (int j = 0; j < interactions.Length; ++j)
                {
                    var interaction = source.interactions[j] ?? throw new InvalidOperationException(where + " has a missing interaction.");
                    // An interaction config has no name of its own, so the piece and encounter name its failures.
                    try { interactions[j] = interaction.Build(); }
                    catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
                    {
                        throw new InvalidOperationException(where + " has an invalid interaction: " + exception.Message, exception);
                    }
                }
                try { definitions[i] = new TablePieceDefinition(source.piece.Identity, interactions); }
                catch (ArgumentException exception)
                {
                    throw new InvalidOperationException(where + " is invalid: " + exception.Message, exception);
                }
            }
            return definitions;
        }

        private RewardPolicyDefinition BuildRewardPolicy(RewardPolicyData source, Func<RewardPoolAsset, ModifierRewardPoolDefinition> pool)
        {
            if (source == null) return null;
            string where = "The reward policy of the encounter " + name;
            if (source.modifierLayouts == null || source.upgradeHistory == null)
                throw new InvalidOperationException(where + " has no layout or upgrade history list.");
            if (source.tierRules == null || source.tierMilestones == null)
                throw new InvalidOperationException(where + " has no tier rule or tier milestone list.");
            try
            {
                var layouts = new ModifierRewardLayout[source.modifierLayouts.Length];
                for (int i = 0; i < layouts.Length; ++i)
                {
                    var layout = source.modifierLayouts[i] ?? throw new InvalidOperationException(where + " has a missing reward layout.");
                    if (layout.pools == null) throw new InvalidOperationException(where + " has a reward layout without a pool list.");
                    var pools = new ModifierRewardPoolDefinition[layout.pools.Length];
                    for (int j = 0; j < pools.Length; ++j)
                        pools[j] = pool(layout.pools[j] ??
                            throw new InvalidOperationException(where + " has a reward layout with a missing pool."));
                    layouts[i] = new ModifierRewardLayout(pools);
                }
                var history = new HandUpgradeHistoryRule[source.upgradeHistory.Length];
                for (int i = 0; i < history.Length; ++i)
                {
                    var rule = source.upgradeHistory[i] ?? throw new InvalidOperationException(where + " has a missing upgrade history rule.");
                    history[i] = new HandUpgradeHistoryRule(rule.maxIncomingStageNumber, rule.excludedRecentChoices);
                }
                var tierRules = new ModifierTierRule[source.tierRules.Length];
                for (int i = 0; i < tierRules.Length; ++i)
                {
                    var rule = source.tierRules[i] ?? throw new InvalidOperationException(where + " has a missing tier rule.");
                    tierRules[i] = new ModifierTierRule(rule.firstIncomingStageNumber, rule.tierWeights);
                }
                var tierMilestones = new ModifierTierMilestone[source.tierMilestones.Length];
                for (int i = 0; i < tierMilestones.Length; ++i)
                {
                    var milestone = source.tierMilestones[i] ?? throw new InvalidOperationException(where + " has a missing tier milestone.");
                    tierMilestones[i] = new ModifierTierMilestone(milestone.incomingStageNumber, milestone.minimumTier);
                }
                return new RewardPolicyDefinition(layouts, source.startingRefreshes,
                    source.refreshesPerClear, source.refreshCost, source.handUpgradeOptionCount, history, tierRules, tierMilestones);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException(where + " is invalid: " + exception.Message, exception);
            }
        }

        /**
         * A table piece the encounter places and the piece's interactions in this encounter, at most one of each kind;
         * another encounter can place the same piece with other interactions.
         */
        [Serializable]
        private sealed class PieceData
        {
            public TablePieceAsset piece;
            [SerializeReference, SelectImplementation]
            public PieceInteractionConfig[] interactions = Array.Empty<PieceInteractionConfig>();
        }

        /**
         * An encounter's reward offers: its charm offer layouts, refreshes, hand upgrade offers and charm tiers. It is public
         * so that code outside the encounter, such as tests, can create an encounter's policy.
         */
        [Serializable]
        public sealed class RewardPolicyData
        {
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
        public sealed class TierRuleData
        {
            public int firstIncomingStageNumber;
            public int[] tierWeights = Array.Empty<int>();
        }

        [Serializable]
        public sealed class TierMilestoneData
        {
            public int incomingStageNumber;
            public int minimumTier;
        }

        /** One charm offer: each slot draws from the pool it references. */
        [Serializable]
        public sealed class RewardLayoutData
        {
            public RewardPoolAsset[] pools = Array.Empty<RewardPoolAsset>();
        }

        [Serializable]
        public sealed class UpgradeHistoryData
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
