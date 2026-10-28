using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    public enum ModifierAvailabilityScope { Global, Spirit, Encounter }
    public enum ModifierAccess { Starter, PermanentUnlock, EncounterExclusive }

    public sealed class ModifierAvailabilityDefinition
    {
        public int PowerTier { get; }
        public ModifierAvailabilityScope Scope { get; }
        public string ScopeId { get; }
        public ModifierAccess Access { get; }

        public ModifierAvailabilityDefinition(int powerTier = 1, ModifierAvailabilityScope scope = ModifierAvailabilityScope.Global,
            string scopeId = null, ModifierAccess access = ModifierAccess.Starter)
        {
            if (powerTier < 1) throw new ArgumentOutOfRangeException(nameof(powerTier));
            if (!Enum.IsDefined(typeof(ModifierAvailabilityScope), scope)) throw new ArgumentOutOfRangeException(nameof(scope));
            if (!Enum.IsDefined(typeof(ModifierAccess), access)) throw new ArgumentOutOfRangeException(nameof(access));
            if (scope == ModifierAvailabilityScope.Global)
            {
                if (scopeId != null || access == ModifierAccess.EncounterExclusive)
                    throw new ArgumentException("Global modifiers cannot have a scope ID or exclusive access.");
            }
            else DefinitionValidation.RequireId(scopeId, nameof(scopeId));
            PowerTier = powerTier;
            Scope = scope;
            ScopeId = scopeId;
            Access = access;
        }

        public bool MatchesEncounter(EncounterDefinition encounter)
            => Scope == ModifierAvailabilityScope.Global || (encounter != null &&
                (Scope == ModifierAvailabilityScope.Spirit ? ScopeId == encounter.SpiritId : ScopeId == encounter.Id));

        internal bool IsAvailable(EncounterDefinition encounter, ISet<string> unlockedIds, string modifierId)
            => MatchesEncounter(encounter) && (Access != ModifierAccess.PermanentUnlock || unlockedIds.Contains(modifierId));
    }

    public enum ModifierPhase { InitialRoll, PlayerReroll, PlayHand }
    /** The effect kinds the skin words; each effect class names its own. */
    public enum ModifierEffectKind { AddBaseScore, AddMultiplier, AddRerolls }
    public enum ModifierActivationLimit { Unlimited, OncePerHand, OncePerStage }

    public sealed class ModifierDefinition
    {
        public string Id { get; }
        public string FamilyId { get; }
        public ModifierPhase Phase { get; }
        public IModifierCondition Condition { get; }
        public ReadOnlyCollection<ModifierEffect> Effects { get; }
        public ModifierActivationLimit ActivationLimit { get; }
        public bool RewardEligible { get; }
        public ModifierAvailabilityDefinition Availability { get; }

        /** A charm that can be offered and can ever do something when it activates. */
        public bool IsPlayable => RewardEligible && Effects.Count > 0 && Condition.CanEverActivate;

        public ModifierDefinition(string id, string familyId, ModifierPhase phase, IModifierCondition condition,
            IEnumerable<ModifierEffect> effects, ModifierActivationLimit activationLimit = ModifierActivationLimit.OncePerHand,
            bool rewardEligible = true, ModifierAvailabilityDefinition availability = null)
        {
            DefinitionValidation.RequireId(id, nameof(id));
            DefinitionValidation.RequireId(familyId, nameof(familyId));
            if (!Enum.IsDefined(typeof(ModifierPhase), phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            if (!Enum.IsDefined(typeof(ModifierActivationLimit), activationLimit)) throw new ArgumentOutOfRangeException(nameof(activationLimit));
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            if (effects == null) throw new ArgumentNullException(nameof(effects));
            var entries = new List<ModifierEffect>(effects);
            foreach (var effect in entries)
                if (effect == null) throw new ArgumentException("An effect is missing.", nameof(effects));
            Id = id;
            FamilyId = familyId;
            Phase = phase;
            Condition = condition;
            Effects = entries.AsReadOnly();
            ActivationLimit = activationLimit;
            RewardEligible = rewardEligible;
            Availability = availability ?? new ModifierAvailabilityDefinition();
        }
    }

    public readonly struct ModifierContext : IEquatable<ModifierContext>
    {
        public DiceValues CurrentDice { get; }
        public DiceValues InitialDice { get; }
        public DiceValues? BeforeRerollDice { get; }
        public DiceValues? PreviousPlayedDice { get; }
        public int SelectedMask { get; }
        public int EverRerolledMask { get; }
        public int RerollsSpentOnHand { get; }
        public int HandsRemaining { get; }
        public int RerollsRemaining { get; }
        public int HandsPlayedInStage { get; }
        public int StartingHands { get; }
        public int? PreviousStageStartingHands { get; }
        public int? PreviousStageHandsRemaining { get; }
        /** The encounter's rules as conditions observe them; a condition depending on a rule reads that rule's snapshot type. */
        public IReadOnlyList<EncounterRuleSnapshot> EncounterRules { get; }
        public ModifierContext(DiceValues currentDice, DiceValues initialDice, DiceValues? beforeRerollDice,
            DiceValues? previousPlayedDice, int selectedMask, int everRerolledMask, int rerollsSpentOnHand,
            int handsRemaining, int rerollsRemaining, int handsPlayedInStage, int startingHands,
            int? previousStageStartingHands, int? previousStageHandsRemaining,
            IReadOnlyList<EncounterRuleSnapshot> encounterRules = null)
        {
            if (!currentDice.IsValid || !initialDice.IsValid ||
                (beforeRerollDice.HasValue && !beforeRerollDice.Value.IsValid) ||
                (previousPlayedDice.HasValue && !previousPlayedDice.Value.IsValid))
                throw new ArgumentException("Context dice must contain five valid faces.");
            if ((selectedMask & ~DiceValues.AllMask) != 0 || (everRerolledMask & ~DiceValues.AllMask) != 0)
                throw new ArgumentOutOfRangeException(nameof(selectedMask));
            if (rerollsSpentOnHand < 0 || handsRemaining < 0 || rerollsRemaining < 0 ||
                handsPlayedInStage < 0 || startingHands < 1 || previousStageStartingHands < 1 ||
                previousStageHandsRemaining < 0)
                throw new ArgumentOutOfRangeException(nameof(handsRemaining));
            CurrentDice = currentDice;
            InitialDice = initialDice;
            BeforeRerollDice = beforeRerollDice;
            PreviousPlayedDice = previousPlayedDice;
            SelectedMask = selectedMask;
            EverRerolledMask = everRerolledMask;
            RerollsSpentOnHand = rerollsSpentOnHand;
            HandsRemaining = handsRemaining;
            RerollsRemaining = rerollsRemaining;
            HandsPlayedInStage = handsPlayedInStage;
            StartingHands = startingHands;
            PreviousStageStartingHands = previousStageStartingHands;
            PreviousStageHandsRemaining = previousStageHandsRemaining;
            EncounterRules = encounterRules ?? Array.Empty<EncounterRuleSnapshot>();
        }

        public bool Equals(ModifierContext other)
        {
            if (CurrentDice != other.CurrentDice || InitialDice != other.InitialDice ||
                BeforeRerollDice != other.BeforeRerollDice || PreviousPlayedDice != other.PreviousPlayedDice ||
                SelectedMask != other.SelectedMask || EverRerolledMask != other.EverRerolledMask ||
                RerollsSpentOnHand != other.RerollsSpentOnHand || HandsRemaining != other.HandsRemaining ||
                RerollsRemaining != other.RerollsRemaining || HandsPlayedInStage != other.HandsPlayedInStage ||
                StartingHands != other.StartingHands || PreviousStageStartingHands != other.PreviousStageStartingHands ||
                PreviousStageHandsRemaining != other.PreviousStageHandsRemaining) return false;
            // A default-constructed context has no list; it observes no rules, like an empty list.
            var rules = EncounterRules ?? Array.Empty<EncounterRuleSnapshot>();
            var otherRules = other.EncounterRules ?? Array.Empty<EncounterRuleSnapshot>();
            if (rules.Count != otherRules.Count) return false;
            for (int i = 0; i < rules.Count; ++i)
                if (!Equals(rules[i], otherRules[i])) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is ModifierContext other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = CurrentDice.GetHashCode();
                hash = hash * 397 ^ InitialDice.GetHashCode();
                hash = hash * 397 ^ SelectedMask;
                hash = hash * 397 ^ EverRerolledMask;
                hash = hash * 397 ^ HandsRemaining;
                hash = hash * 397 ^ RerollsRemaining;
                hash = hash * 397 ^ HandsPlayedInStage;
                return hash * 397 ^ (EncounterRules?.Count ?? 0);
            }
        }

        public static bool operator ==(ModifierContext left, ModifierContext right) => left.Equals(right);
        public static bool operator !=(ModifierContext left, ModifierContext right) => !left.Equals(right);
    }
}
