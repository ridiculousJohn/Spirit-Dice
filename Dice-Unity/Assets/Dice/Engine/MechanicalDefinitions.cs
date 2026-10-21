using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    public enum HandCategory
    {
        HighDie, Pair, TwoPair, ThreeOfAKind, FullHouse, FourOfAKind, Straight, FiveOfAKind
    }

    public enum StageExitBehavior
    {
        AdvanceAutomatically, AwaitRewards
    }

    public sealed class HandDefinition
    {
        public const int CategoryCount = 8;
        public string Id { get; }
        public HandCategory Category { get; }
        public int Rank => (int)Category;
        public long BaseScore { get; }
        public long Multiplier { get; }
        public long BaseScorePerUpgrade { get; }
        public long MultiplierPerUpgrade { get; }
        public int MaximumUpgrades { get; }

        public HandDefinition(string id, HandCategory category, long baseScore, long multiplier,
            long baseScorePerUpgrade, long multiplierPerUpgrade, int maximumUpgrades)
        {
            DefinitionValidation.RequireId(id, nameof(id));
            if ((uint)category >= CategoryCount) throw new ArgumentOutOfRangeException(nameof(category));
            if (baseScore < 0) throw new ArgumentOutOfRangeException(nameof(baseScore));
            if (multiplier < 1) throw new ArgumentOutOfRangeException(nameof(multiplier));
            if (baseScorePerUpgrade < 0) throw new ArgumentOutOfRangeException(nameof(baseScorePerUpgrade));
            if (multiplierPerUpgrade < 0) throw new ArgumentOutOfRangeException(nameof(multiplierPerUpgrade));
            if (maximumUpgrades < 0) throw new ArgumentOutOfRangeException(nameof(maximumUpgrades));
            checked
            {
                long maximumBase = 30 + baseScore + baseScorePerUpgrade * maximumUpgrades;
                long maximumMultiplier = multiplier + multiplierPerUpgrade * maximumUpgrades;
                _ = maximumBase * maximumMultiplier;
            }
            Id = id;
            Category = category;
            BaseScore = baseScore;
            Multiplier = multiplier;
            BaseScorePerUpgrade = baseScorePerUpgrade;
            MultiplierPerUpgrade = multiplierPerUpgrade;
            MaximumUpgrades = maximumUpgrades;
        }
    }

    public sealed class StageDefinition
    {
        public long Threshold { get; }
        public int StartingHands { get; }
        public int StartingRerolls { get; }
        public StageExitBehavior ExitBehavior { get; }

        public StageDefinition(long threshold, int startingHands, int startingRerolls,
            StageExitBehavior exitBehavior = StageExitBehavior.AdvanceAutomatically)
        {
            if (threshold < 1) throw new ArgumentOutOfRangeException(nameof(threshold));
            if (startingHands < 1) throw new ArgumentOutOfRangeException(nameof(startingHands));
            if (startingRerolls < 0) throw new ArgumentOutOfRangeException(nameof(startingRerolls));
            if (exitBehavior != StageExitBehavior.AdvanceAutomatically && exitBehavior != StageExitBehavior.AwaitRewards)
                throw new ArgumentOutOfRangeException(nameof(exitBehavior));
            Threshold = threshold;
            StartingHands = startingHands;
            StartingRerolls = startingRerolls;
            ExitBehavior = exitBehavior;
        }
    }

    public sealed class ModifierUnlockMilestone
    {
        public int StageNumber { get; }
        public ReadOnlyCollection<string> ModifierIds { get; }

        public ModifierUnlockMilestone(int stageNumber, IEnumerable<string> modifierIds)
        {
            if (stageNumber < 1) throw new ArgumentOutOfRangeException(nameof(stageNumber));
            if (modifierIds == null) throw new ArgumentNullException(nameof(modifierIds));
            var ids = new List<string>(modifierIds);
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                DefinitionValidation.RequireId(id, nameof(modifierIds));
                if (!unique.Add(id)) throw new ArgumentException("Unlock IDs must be unique.", nameof(modifierIds));
            }
            StageNumber = stageNumber;
            ModifierIds = ids.AsReadOnly();
        }
    }

    public sealed class EncounterDefinition
    {
        public string Id { get; }
        public ReadOnlyCollection<StageDefinition> Stages { get; }
        public ReadOnlyCollection<int> StartingUpgrades { get; }
        public ReadOnlyCollection<string> StartingModifiers { get; }
        public RewardPolicyDefinition RewardPolicy { get; }
        public ReadOnlyCollection<EncounterRuleDefinition> Rules { get; }
        public string SpiritId { get; }
        public ReadOnlyCollection<ModifierUnlockMilestone> UnlockMilestones { get; }
        public ReadOnlyCollection<string> EncounterModifierIds { get; }
        public ReadOnlyCollection<string> UnlockRewardModifierIds { get; }
        public ReadOnlyCollection<TablePieceDefinition> Pieces { get; }

        public EncounterDefinition(string id, IEnumerable<StageDefinition> stages, IEnumerable<int> startingUpgrades = null,
            IEnumerable<string> startingModifiers = null, RewardPolicyDefinition rewardPolicy = null,
            IEnumerable<EncounterRuleDefinition> rules = null, string spiritId = null,
            IEnumerable<ModifierUnlockMilestone> unlockMilestones = null, IEnumerable<string> encounterModifierIds = null,
            IEnumerable<string> unlockRewardModifierIds = null, IEnumerable<TablePieceDefinition> pieces = null)
        {
            DefinitionValidation.RequireId(id, nameof(id));
            if (stages == null) throw new ArgumentNullException(nameof(stages));
            var stageList = new List<StageDefinition>(stages);
            if (stageList.Count == 0 || stageList.Exists(stage => stage == null))
                throw new ArgumentException("Stages must be nonempty and contain no null entries.", nameof(stages));
            var upgrades = startingUpgrades == null
                ? new List<int>(new int[HandDefinition.CategoryCount]) : new List<int>(startingUpgrades);
            if (upgrades.Count != HandDefinition.CategoryCount || upgrades.Exists(count => count < 0))
                throw new ArgumentException("One nonnegative upgrade count is required per category.", nameof(startingUpgrades));
            Id = id;
            Stages = stageList.AsReadOnly();
            StartingUpgrades = upgrades.AsReadOnly();
            var modifiers = startingModifiers == null ? new List<string>() : new List<string>(startingModifiers);
            foreach (string modifierId in modifiers) DefinitionValidation.RequireId(modifierId, nameof(startingModifiers));
            StartingModifiers = modifiers.AsReadOnly();
            RewardPolicy = rewardPolicy;
            var ruleList = rules == null ? new List<EncounterRuleDefinition>() : new List<EncounterRuleDefinition>(rules);
            var ruleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in ruleList)
                if (rule == null || !ruleIds.Add(rule.Id))
                    throw new ArgumentException("Encounter rule IDs must be unique and nonnull.", nameof(rules));
            Rules = ruleList.AsReadOnly();
            if (spiritId != null) DefinitionValidation.RequireId(spiritId, nameof(spiritId));
            SpiritId = spiritId;
            var milestones = unlockMilestones == null ? new List<ModifierUnlockMilestone>() : new List<ModifierUnlockMilestone>(unlockMilestones);
            var milestoneStages = new HashSet<int>();
            foreach (var milestone in milestones)
                if (milestone == null || milestone.StageNumber > Stages.Count || !milestoneStages.Add(milestone.StageNumber))
                    throw new ArgumentException("Unlock stages must be unique and within the encounter.", nameof(unlockMilestones));
            UnlockMilestones = milestones.AsReadOnly();
            EncounterModifierIds = CopyUniqueIds(encounterModifierIds, nameof(encounterModifierIds));
            UnlockRewardModifierIds = CopyUniqueIds(unlockRewardModifierIds, nameof(unlockRewardModifierIds));
            var pieceList = pieces == null ? new List<TablePieceDefinition>() : new List<TablePieceDefinition>(pieces);
            var pieceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var piece in pieceList)
                if (piece == null || !pieceIds.Add(piece.Id))
                    throw new ArgumentException("Table piece IDs must be unique and nonnull.", nameof(pieces));
            Pieces = pieceList.AsReadOnly();
            foreach (var rule in Rules) rule.ValidateDefinition(this);
        }

        private static ReadOnlyCollection<string> CopyUniqueIds(IEnumerable<string> source, string parameter)
        {
            var ids = new List<string>(source ?? Array.Empty<string>());
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                DefinitionValidation.RequireId(id, parameter);
                if (!unique.Add(id)) throw new ArgumentException("Modifier IDs must be unique.", parameter);
            }
            return ids.AsReadOnly();
        }
    }

    public sealed class MechanicalCatalog
    {
        private readonly Dictionary<string, EncounterDefinition> encounters;
        private readonly Dictionary<string, ModifierDefinition> modifiersById;
        private readonly Dictionary<string, ModifierRewardPoolDefinition> rewardPoolsById;
        public string Version { get; }
        public ReadOnlyCollection<HandDefinition> Hands { get; }
        public ReadOnlyCollection<ModifierDefinition> Modifiers { get; }
        public ReadOnlyCollection<ModifierRewardPoolDefinition> RewardPools { get; }
        public ReadOnlyCollection<EncounterDefinition> Encounters { get; }

        public MechanicalCatalog(string version, IEnumerable<HandDefinition> hands, IEnumerable<EncounterDefinition> encounters,
            IEnumerable<ModifierDefinition> modifiers = null, IEnumerable<ModifierRewardPoolDefinition> rewardPools = null)
        {
            DefinitionValidation.RequireId(version, nameof(version));
            if (hands == null) throw new ArgumentNullException(nameof(hands));
            if (encounters == null) throw new ArgumentNullException(nameof(encounters));
            var byCategory = new HandDefinition[HandDefinition.CategoryCount];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var hand in hands)
            {
                if (hand == null || !ids.Add(hand.Id) || byCategory[(int)hand.Category] != null)
                    throw new ArgumentException("Hand IDs and categories must be unique and nonnull.", nameof(hands));
                byCategory[(int)hand.Category] = hand;
            }
            if (Array.Exists(byCategory, hand => hand == null))
                throw new ArgumentException("All eight hand categories are required.", nameof(hands));
            modifiersById = new Dictionary<string, ModifierDefinition>(StringComparer.Ordinal);
            var modifierList = new List<ModifierDefinition>();
            if (modifiers != null)
                foreach (var modifier in modifiers)
                {
                    if (modifier == null || modifiersById.ContainsKey(modifier.Id))
                        throw new ArgumentException("Modifier IDs must be unique and nonnull.", nameof(modifiers));
                    modifiersById.Add(modifier.Id, modifier);
                    modifierList.Add(modifier);
                }
            Modifiers = modifierList.AsReadOnly();
            rewardPoolsById = new Dictionary<string, ModifierRewardPoolDefinition>(StringComparer.Ordinal);
            var poolList = new List<ModifierRewardPoolDefinition>();
            if (rewardPools != null)
                foreach (var pool in rewardPools)
                {
                    if (pool == null || rewardPoolsById.ContainsKey(pool.Id))
                        throw new ArgumentException("Reward pool IDs must be unique and nonnull.", nameof(rewardPools));
                    foreach (string modifierId in pool.ModifierIds)
                        if (!modifiersById.TryGetValue(modifierId, out var modifier) || !modifier.RewardEligible)
                            throw new ArgumentException("Reward pool entries must reference eligible modifiers.", nameof(rewardPools));
                    if (pool.FallbackModifierId != null && !modifiersById.ContainsKey(pool.FallbackModifierId))
                        throw new ArgumentException("The reward fallback modifier is missing.", nameof(rewardPools));
                    rewardPoolsById.Add(pool.Id, pool);
                    poolList.Add(pool);
                }
            RewardPools = poolList.AsReadOnly();
            this.encounters = new Dictionary<string, EncounterDefinition>(StringComparer.Ordinal);
            var encounterList = new List<EncounterDefinition>();
            foreach (var encounter in encounters)
            {
                if (encounter == null || this.encounters.ContainsKey(encounter.Id))
                    throw new ArgumentException("Encounter IDs must be unique and nonnull.", nameof(encounters));
                for (int i = 0; i < byCategory.Length; ++i)
                    if (encounter.StartingUpgrades[i] > byCategory[i].MaximumUpgrades)
                        throw new ArgumentException("Starting upgrades exceed the category limit.", nameof(encounters));
                foreach (string modifierId in encounter.StartingModifiers)
                    if (!modifiersById.ContainsKey(modifierId))
                        throw new ArgumentException("A starting modifier is missing from the catalog.", nameof(encounters));
                foreach (var milestone in encounter.UnlockMilestones)
                    foreach (string modifierId in milestone.ModifierIds)
                        if (!modifiersById.TryGetValue(modifierId, out var modifier) ||
                            modifier.Availability.Access != ModifierAccess.PermanentUnlock ||
                            !modifier.Availability.MatchesEncounter(encounter))
                            throw new ArgumentException("Milestones must reference permanent unlocks within the encounter scope.", nameof(encounters));
                foreach (string modifierId in encounter.UnlockRewardModifierIds)
                    if (!modifiersById.TryGetValue(modifierId, out var modifier) ||
                        modifier.Availability.Access != ModifierAccess.PermanentUnlock ||
                        !modifier.RewardEligible || !modifier.Availability.MatchesEncounter(encounter))
                        throw new ArgumentException("Unlock rewards must reference eligible permanent unlocks within the encounter scope.", nameof(encounters));
                foreach (string modifierId in encounter.EncounterModifierIds)
                    if (!modifiersById.TryGetValue(modifierId, out var modifier) ||
                        !modifier.RewardEligible || !modifier.Availability.MatchesEncounter(encounter))
                        throw new ArgumentException("Encounter additions must reference eligible modifiers within the encounter scope.", nameof(encounters));
                if (encounter.RewardPolicy != null)
                    foreach (var layout in encounter.RewardPolicy.ModifierLayouts)
                        foreach (string poolId in layout.PoolIds)
                            if (!rewardPoolsById.ContainsKey(poolId))
                                throw new ArgumentException("A reward layout references a missing pool.", nameof(encounters));
                this.encounters.Add(encounter.Id, encounter);
                encounterList.Add(encounter);
            }
            if (this.encounters.Count == 0) throw new ArgumentException("At least one encounter is required.", nameof(encounters));
            Version = version;
            Hands = Array.AsReadOnly(byCategory);
            Encounters = encounterList.AsReadOnly();
        }

        public HandDefinition GetHand(HandCategory category)
        {
            if ((uint)category >= HandDefinition.CategoryCount) throw new ArgumentOutOfRangeException(nameof(category));
            return Hands[(int)category];
        }

        public ModifierDefinition GetModifier(string id)
        {
            DefinitionValidation.RequireId(id, nameof(id));
            if (!modifiersById.TryGetValue(id, out var modifier)) throw new KeyNotFoundException(id);
            return modifier;
        }

        public ModifierRewardPoolDefinition GetRewardPool(string id)
        {
            DefinitionValidation.RequireId(id, nameof(id));
            if (!rewardPoolsById.TryGetValue(id, out var pool)) throw new KeyNotFoundException(id);
            return pool;
        }

        public EncounterDefinition GetEncounter(string id)
        {
            DefinitionValidation.RequireId(id, nameof(id));
            if (!encounters.TryGetValue(id, out var definition)) throw new KeyNotFoundException(id);
            return definition;
        }
    }

    internal static class DefinitionValidation
    {
        internal static void RequireId(string id, string parameter)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A stable ID is required.", parameter);
        }
    }
}
