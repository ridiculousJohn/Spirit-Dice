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
        public HandCategory Category { get; }
        public long BaseScore { get; }
        public long Multiplier { get; }
        public long BaseScorePerUpgrade { get; }
        public long MultiplierPerUpgrade { get; }
        public int MaximumUpgrades { get; }

        public HandDefinition(HandCategory category, long baseScore, long multiplier,
            long baseScorePerUpgrade, long multiplierPerUpgrade, int maximumUpgrades)
        {
            if ((uint)category >= CategoryCount) throw new ArgumentOutOfRangeException(nameof(category));
            if (baseScore < 0) throw new ArgumentOutOfRangeException(nameof(baseScore));
            if (multiplier < 1) throw new ArgumentOutOfRangeException(nameof(multiplier));
            if (baseScorePerUpgrade < 0) throw new ArgumentOutOfRangeException(nameof(baseScorePerUpgrade));
            if (multiplierPerUpgrade < 0) throw new ArgumentOutOfRangeException(nameof(multiplierPerUpgrade));
            if (maximumUpgrades < 0) throw new ArgumentOutOfRangeException(nameof(maximumUpgrades));
            checked
            {
                long maximumBase = DiceValues.MaximumSum + baseScore + baseScorePerUpgrade * maximumUpgrades;
                long maximumMultiplier = multiplier + multiplierPerUpgrade * maximumUpgrades;
                _ = maximumBase * maximumMultiplier;
            }
            Category = category;
            BaseScore = baseScore;
            Multiplier = multiplier;
            BaseScorePerUpgrade = baseScorePerUpgrade;
            MultiplierPerUpgrade = multiplierPerUpgrade;
            MaximumUpgrades = maximumUpgrades;
        }

        /** The category's base score at an upgrade level from zero to the maximum. */
        public long BaseScoreAt(int level) => BaseScore + BaseScorePerUpgrade * RequireLevel(level);

        /** The category's multiplier at an upgrade level from zero to the maximum. */
        public long MultiplierAt(int level) => Multiplier + MultiplierPerUpgrade * RequireLevel(level);

        // The constructor proves the values at the maximum level cannot overflow.
        private int RequireLevel(int level)
            => level >= 0 && level <= MaximumUpgrades ? level : throw new ArgumentOutOfRangeException(nameof(level));
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

    /** Clearing this stage offers a choice from the encounter's UnlockRewardModifierIds. */
    public sealed class ModifierUnlockMilestone
    {
        /** The most charms one unlock offer shows. */
        public const int OfferSize = 3;

        public int StageNumber { get; }

        public ModifierUnlockMilestone(int stageNumber)
        {
            if (stageNumber < 1) throw new ArgumentOutOfRangeException(nameof(stageNumber));
            StageNumber = stageNumber;
        }
    }

    public sealed class EncounterDefinition
    {
        /** Identifies an encounter where an identity is stored, as in saves and the player profile. */
        public readonly struct Id : IEquatable<Id>, IContentId
        {
            public Guid Value { get; }

            public Id(Guid value)
            {
                if (value == Guid.Empty) throw new ArgumentException("An encounter id requires a nonempty GUID.", nameof(value));
                Value = value;
            }

            public bool Equals(Id other) => Value == other.Value;
            public override bool Equals(object obj) => obj is Id other && Equals(other);
            public override int GetHashCode() => Value.GetHashCode();
            /** The 32 hexadecimal digits, as the encounter's asset and its .meta file write its GUID. */
            public override string ToString() => Value.ToString("N");
            public static bool operator ==(Id left, Id right) => left.Equals(right);
            public static bool operator !=(Id left, Id right) => !left.Equals(right);
        }

        // A nested type and a member cannot share a name, so the id property is not called Id.
        public Id Identity { get; }
        public ReadOnlyCollection<StageDefinition> Stages { get; }
        public ReadOnlyCollection<int> StartingUpgrades { get; }
        public ReadOnlyCollection<ModifierDefinition.Id> StartingModifiers { get; }
        public RewardPolicyDefinition RewardPolicy { get; }
        public ReadOnlyCollection<EncounterRuleDefinition> Rules { get; }
        public ReadOnlyCollection<ModifierUnlockMilestone> UnlockMilestones { get; }
        public ReadOnlyCollection<ModifierDefinition.Id> EncounterModifierIds { get; }
        /** The first cleared stage whose charm offer may include EncounterModifierIds; 1 offers them from the first offer. */
        public int EncounterModifiersFirstClearedStageNumber { get; }
        /** Whether the encounter has charms of its own and its first charm offer can already contain them. */
        public bool EncounterModifiersAvailableFromStart => EncounterModifierIds.Count > 0 && EncounterModifiersFirstClearedStageNumber == 1;
        public ReadOnlyCollection<ModifierDefinition.Id> UnlockRewardModifierIds { get; }
        public ReadOnlyCollection<TablePieceDefinition> Pieces { get; }

        public EncounterDefinition(Id identity, IEnumerable<StageDefinition> stages, IEnumerable<int> startingUpgrades = null,
            IEnumerable<ModifierDefinition.Id> startingModifiers = null, RewardPolicyDefinition rewardPolicy = null,
            IEnumerable<EncounterRuleDefinition> rules = null,
            IEnumerable<ModifierUnlockMilestone> unlockMilestones = null, IEnumerable<ModifierDefinition.Id> encounterModifierIds = null,
            IEnumerable<ModifierDefinition.Id> unlockRewardModifierIds = null, IEnumerable<TablePieceDefinition> pieces = null,
            int encounterModifiersFirstClearedStageNumber = 1)
        {
            if (identity.Value == Guid.Empty) throw new ArgumentException("An encounter requires an id.", nameof(identity));
            if (stages == null) throw new ArgumentNullException(nameof(stages));
            var stageList = new List<StageDefinition>(stages);
            if (stageList.Count == 0 || stageList.Exists(stage => stage == null))
                throw new ArgumentException("Stages must be nonempty and contain no null entries.", nameof(stages));
            var upgrades = startingUpgrades == null
                ? new List<int>(new int[HandDefinition.CategoryCount]) : new List<int>(startingUpgrades);
            if (upgrades.Count != HandDefinition.CategoryCount || upgrades.Exists(count => count < 0))
                throw new ArgumentException("One nonnegative upgrade count is required per category.", nameof(startingUpgrades));
            Identity = identity;
            Stages = stageList.AsReadOnly();
            StartingUpgrades = upgrades.AsReadOnly();
            var modifiers = startingModifiers == null ? new List<ModifierDefinition.Id>() : new List<ModifierDefinition.Id>(startingModifiers);
            foreach (var modifierId in modifiers) RequireModifierId(modifierId, nameof(startingModifiers));
            StartingModifiers = modifiers.AsReadOnly();
            RewardPolicy = rewardPolicy;
            var ruleList = rules == null ? new List<EncounterRuleDefinition>() : new List<EncounterRuleDefinition>(rules);
            var ruleIds = new HashSet<EncounterRuleDefinition.Id>();
            foreach (var rule in ruleList)
                if (rule == null || !ruleIds.Add(rule.Identity))
                    throw new ArgumentException("Encounter rule IDs must be unique and nonnull.", nameof(rules));
            Rules = ruleList.AsReadOnly();
            var milestones = unlockMilestones == null ? new List<ModifierUnlockMilestone>() : new List<ModifierUnlockMilestone>(unlockMilestones);
            var milestoneStages = new HashSet<int>();
            foreach (var milestone in milestones)
                if (milestone == null || milestone.StageNumber > Stages.Count || !milestoneStages.Add(milestone.StageNumber))
                    throw new ArgumentException("Unlock stages must be unique and within the encounter.", nameof(unlockMilestones));
            UnlockMilestones = milestones.AsReadOnly();
            EncounterModifierIds = CopyUniqueIds(encounterModifierIds, nameof(encounterModifierIds));
            if (encounterModifiersFirstClearedStageNumber < 1 || encounterModifiersFirstClearedStageNumber > Stages.Count)
                throw new ArgumentOutOfRangeException(nameof(encounterModifiersFirstClearedStageNumber));
            EncounterModifiersFirstClearedStageNumber = encounterModifiersFirstClearedStageNumber;
            UnlockRewardModifierIds = CopyUniqueIds(unlockRewardModifierIds, nameof(unlockRewardModifierIds));
            var pieceList = pieces == null ? new List<TablePieceDefinition>() : new List<TablePieceDefinition>(pieces);
            var pieceIds = new HashSet<TablePieceDefinition.Id>();
            foreach (var piece in pieceList)
                if (piece == null || !pieceIds.Add(piece.Identity))
                    throw new ArgumentException("Table piece IDs must be unique and nonnull.", nameof(pieces));
            Pieces = pieceList.AsReadOnly();
            foreach (var rule in Rules) rule.ValidateDefinition(this);
        }

        /** Whether clearing the stage numbered stageNumber offers a choice from UnlockRewardModifierIds. */
        public bool HasUnlockMilestone(int stageNumber)
        {
            foreach (var milestone in UnlockMilestones)
                if (milestone.StageNumber == stageNumber) return true;
            return false;
        }

        /** The unlock rewards an offer draws from: those not yet unlocked whose families are discovered, in authored order. */
        internal List<ModifierDefinition.Id> UnlockCandidates(MechanicalCatalog catalog, ICollection<ModifierDefinition.Id> unlockedIds,
            ICollection<ModifierFamilyDefinition.Id> discoveredFamilyIds)
        {
            var candidates = new List<ModifierDefinition.Id>();
            foreach (var id in UnlockRewardModifierIds)
                if (!unlockedIds.Contains(id) && discoveredFamilyIds.Contains(catalog.GetModifier(id).Family.Identity)) candidates.Add(id);
            return candidates;
        }

        private static ReadOnlyCollection<ModifierDefinition.Id> CopyUniqueIds(IEnumerable<ModifierDefinition.Id> source, string parameter)
        {
            var ids = new List<ModifierDefinition.Id>(source ?? Array.Empty<ModifierDefinition.Id>());
            var unique = new HashSet<ModifierDefinition.Id>();
            foreach (var id in ids)
            {
                RequireModifierId(id, parameter);
                if (!unique.Add(id)) throw new ArgumentException("Modifier IDs must be unique.", parameter);
            }
            return ids.AsReadOnly();
        }

        private static void RequireModifierId(ModifierDefinition.Id id, string parameter)
        {
            if (id.Value == Guid.Empty) throw new ArgumentException("A charm id is required.", parameter);
        }
    }

    public sealed class MechanicalCatalog
    {
        private readonly Dictionary<EncounterDefinition.Id, EncounterDefinition> encounters;
        private readonly Dictionary<ModifierDefinition.Id, ModifierDefinition> modifiersById;
        private readonly Dictionary<ModifierFamilyDefinition.Id, ModifierFamilyDefinition> familiesById;
        public string Version { get; }
        public ReadOnlyCollection<HandDefinition> Hands { get; }
        public ReadOnlyCollection<ModifierDefinition> Modifiers { get; }
        /** The families of the catalog's charms, in the order their first charms appear. */
        public ReadOnlyCollection<ModifierFamilyDefinition> Families { get; }
        public ReadOnlyCollection<EncounterDefinition> Encounters { get; }

        public MechanicalCatalog(string version, IEnumerable<HandDefinition> hands, IEnumerable<EncounterDefinition> encounters,
            IEnumerable<ModifierDefinition> modifiers = null)
        {
            DefinitionValidation.RequireId(version, nameof(version));
            if (hands == null) throw new ArgumentNullException(nameof(hands));
            if (encounters == null) throw new ArgumentNullException(nameof(encounters));
            var byCategory = new HandDefinition[HandDefinition.CategoryCount];
            foreach (var hand in hands)
            {
                if (hand == null || byCategory[(int)hand.Category] != null)
                    throw new ArgumentException("Hand categories must be unique and nonnull.", nameof(hands));
                byCategory[(int)hand.Category] = hand;
            }
            if (Array.Exists(byCategory, hand => hand == null))
                throw new ArgumentException("All eight hand categories are required.", nameof(hands));
            modifiersById = new Dictionary<ModifierDefinition.Id, ModifierDefinition>();
            var modifierList = new List<ModifierDefinition>();
            if (modifiers != null)
                foreach (var modifier in modifiers)
                {
                    if (modifier == null || modifiersById.ContainsKey(modifier.Identity))
                        throw new ArgumentException("Modifier IDs must be unique and nonnull.", nameof(modifiers));
                    modifiersById.Add(modifier.Identity, modifier);
                    modifierList.Add(modifier);
                }
            Modifiers = modifierList.AsReadOnly();
            familiesById = new Dictionary<ModifierFamilyDefinition.Id, ModifierFamilyDefinition>();
            var families = new List<ModifierFamilyDefinition>();
            foreach (var modifier in modifierList)
            {
                if (!familiesById.TryGetValue(modifier.Family.Identity, out var known))
                {
                    familiesById.Add(modifier.Family.Identity, modifier.Family);
                    families.Add(modifier.Family);
                }
                else if (known != modifier.Family)
                    throw new ArgumentException("Two charm families share the id " + modifier.Family.Identity + ".", nameof(modifiers));
            }
            Families = families.AsReadOnly();
            this.encounters = new Dictionary<EncounterDefinition.Id, EncounterDefinition>();
            var encounterList = new List<EncounterDefinition>();
            // Encounters can share a pool, which is validated once.
            var validatedPools = new HashSet<ModifierRewardPoolDefinition>();
            foreach (var encounter in encounters)
            {
                if (encounter == null) throw new ArgumentException("Encounters must be nonnull.", nameof(encounters));
                if (this.encounters.ContainsKey(encounter.Identity))
                    throw new ArgumentException("Two encounters share the id " + encounter.Identity + ".", nameof(encounters));
                for (int i = 0; i < byCategory.Length; ++i)
                    if (encounter.StartingUpgrades[i] > byCategory[i].MaximumUpgrades)
                        throw new ArgumentException("The starting upgrades of the encounter " + encounter.Identity +
                            " exceed the limit of the category " + (HandCategory)i + ".", nameof(encounters));
                foreach (var modifierId in encounter.StartingModifiers)
                {
                    if (!modifiersById.TryGetValue(modifierId, out var modifier))
                        throw new ArgumentException("The charm " + modifierId + " in the starting charms of the encounter " +
                            encounter.Identity + " is missing from the catalog.", nameof(encounters));
                    modifier.RejectEncounterExclusive("the starting charms of the encounter " + encounter.Identity, nameof(encounters));
                }
                foreach (var modifierId in encounter.UnlockRewardModifierIds)
                {
                    if (!modifiersById.TryGetValue(modifierId, out var modifier))
                        throw new ArgumentException("The charm " + modifierId + " in the unlock rewards of the encounter " +
                            encounter.Identity + " is missing from the catalog.", nameof(encounters));
                    modifier.RejectEncounterExclusive("the unlock rewards of the encounter " + encounter.Identity, nameof(encounters));
                    if (modifier.Availability.Access != ModifierAccess.PermanentUnlock || !modifier.RewardEligible)
                        throw new ArgumentException("The charm " + modifier.Identity + " in the unlock rewards of the encounter " +
                            encounter.Identity + " is not a reward-eligible permanent unlock.", nameof(encounters));
                }
                foreach (var modifierId in encounter.EncounterModifierIds)
                    if (!modifiersById.TryGetValue(modifierId, out var modifier) || !modifier.RewardEligible)
                        throw new ArgumentException("The charm " + modifierId + " in the charm additions of the encounter " +
                            encounter.Identity + " is missing from the catalog or not reward-eligible.", nameof(encounters));
                if (encounter.RewardPolicy != null)
                    foreach (var layout in encounter.RewardPolicy.ModifierLayouts)
                        foreach (var pool in layout.Pools)
                            if (validatedPools.Add(pool)) ValidatePool(pool, nameof(encounters));
                this.encounters.Add(encounter.Identity, encounter);
                encounterList.Add(encounter);
            }
            if (this.encounters.Count == 0) throw new ArgumentException("At least one encounter is required.", nameof(encounters));
            Version = version;
            Hands = Array.AsReadOnly(byCategory);
            Encounters = encounterList.AsReadOnly();
        }

        private void ValidatePool(ModifierRewardPoolDefinition pool, string parameter)
        {
            foreach (var modifierId in pool.ModifierIds)
            {
                if (!modifiersById.TryGetValue(modifierId, out var modifier) || !modifier.RewardEligible)
                    throw new ArgumentException("The charm " + modifierId + " in the reward pool " + pool.Name +
                        " is missing from the catalog or not reward-eligible.", parameter);
                modifier.RejectEncounterExclusive("the reward pool " + pool.Name, parameter);
            }
            if (pool.FallbackModifierId != null)
            {
                if (!modifiersById.TryGetValue(pool.FallbackModifierId.Value, out var fallback))
                    throw new ArgumentException("The fallback charm " + pool.FallbackModifierId.Value + " of the reward pool " + pool.Name +
                        " is missing from the catalog.", parameter);
                fallback.RejectEncounterExclusive("the fallback of the reward pool " + pool.Name, parameter);
            }
        }

        public HandDefinition GetHand(HandCategory category)
        {
            if ((uint)category >= HandDefinition.CategoryCount) throw new ArgumentOutOfRangeException(nameof(category));
            return Hands[(int)category];
        }

        public ModifierDefinition GetModifier(ModifierDefinition.Id id)
        {
            if (id.Value == Guid.Empty) throw new ArgumentException("A charm id is required.", nameof(id));
            if (!modifiersById.TryGetValue(id, out var modifier)) throw new KeyNotFoundException(id.ToString());
            return modifier;
        }

        /** Whether a charm of the catalog belongs to the family. */
        public bool HasFamily(ModifierFamilyDefinition.Id familyId) => familiesById.ContainsKey(familyId);

        public EncounterDefinition GetEncounter(EncounterDefinition.Id id)
        {
            if (id.Value == Guid.Empty) throw new ArgumentException("An encounter id is required.", nameof(id));
            if (!encounters.TryGetValue(id, out var definition)) throw new KeyNotFoundException(id.ToString());
            return definition;
        }

        /** Where a charm can be obtained in the catalog's encounters. */
        public ModifierSources GetModifierSources(ModifierDefinition.Id modifierId)
        {
            ModifierDefinition modifier = GetModifier(modifierId);
            var unlocks = new List<ModifierUnlockSource>();
            var runRewards = new List<EncounterDefinition>();
            foreach (var encounter in Encounters)
            {
                if (encounter.UnlockMilestones.Count > 0 && encounter.UnlockRewardModifierIds.Contains(modifier.Identity))
                    unlocks.Add(new ModifierUnlockSource(encounter));
                if (modifier.RewardEligible && IsRunRewardSource(encounter, modifier.Identity))
                    runRewards.Add(encounter);
            }
            return new ModifierSources(unlocks, runRewards);
        }

        /** Whether the charm is one of the encounter's additions or an entry or fallback of a pool its charm offers draw from. */
        private static bool IsRunRewardSource(EncounterDefinition encounter, ModifierDefinition.Id modifierId)
        {
            if (encounter.EncounterModifierIds.Contains(modifierId)) return true;
            if (encounter.RewardPolicy == null) return false;
            foreach (var layout in encounter.RewardPolicy.ModifierLayouts)
                foreach (var pool in layout.Pools)
                    if (pool.ModifierIds.Contains(modifierId) || pool.FallbackModifierId == modifierId) return true;
            return false;
        }
    }

    /** Where a charm can be obtained in a catalog's encounters, each list in catalog order. */
    public sealed class ModifierSources
    {
        /** The encounters whose unlock offers can contain the charm. */
        public ReadOnlyCollection<ModifierUnlockSource> Unlocks { get; }
        /** The encounters whose run charm offers can contain the charm, as an encounter addition, pool entry or pool fallback. */
        public ReadOnlyCollection<EncounterDefinition> RunRewards { get; }

        internal ModifierSources(List<ModifierUnlockSource> unlocks, List<EncounterDefinition> runRewards)
        {
            Unlocks = unlocks.AsReadOnly();
            RunRewards = runRewards.AsReadOnly();
        }
    }

    /** An encounter whose unlock offers can contain a charm. */
    public sealed class ModifierUnlockSource
    {
        public EncounterDefinition Encounter { get; }
        /**
         * The numbers of the stages whose clear makes an unlock offer, in authored order. Each offer draws from every
         * still-locked charm of the encounter's unlock list, so any of them can offer the charm.
         */
        public ReadOnlyCollection<int> StageNumbers { get; }

        internal ModifierUnlockSource(EncounterDefinition encounter)
        {
            Encounter = encounter;
            var stageNumbers = new List<int>();
            foreach (var milestone in encounter.UnlockMilestones) stageNumbers.Add(milestone.StageNumber);
            StageNumbers = stageNumbers.AsReadOnly();
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
