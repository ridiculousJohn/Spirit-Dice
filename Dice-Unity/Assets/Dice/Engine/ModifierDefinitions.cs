using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    public enum ModifierAccess { Starter, PermanentUnlock, EncounterExclusive }

    public sealed class ModifierAvailabilityDefinition
    {
        public int PowerTier { get; }
        public ModifierAccess Access { get; }

        public ModifierAvailabilityDefinition(int powerTier = 1, ModifierAccess access = ModifierAccess.Starter)
        {
            if (powerTier < 1) throw new ArgumentOutOfRangeException(nameof(powerTier));
            if (!Enum.IsDefined(typeof(ModifierAccess), access)) throw new ArgumentOutOfRangeException(nameof(access));
            PowerTier = powerTier;
            Access = access;
        }

        internal bool IsAvailable(ISet<ModifierDefinition.Id> unlockedIds, ModifierDefinition.Id modifierId)
            => Access != ModifierAccess.PermanentUnlock || unlockedIds.Contains(modifierId);
    }

    public enum ModifierPhase { InitialRoll, PlayerReroll, PlayHand }
    /** The effect kinds the skin words; each effect class names its own. */
    public enum ModifierEffectKind { AddBaseScore, AddMultiplier, AddRerolls }
    public enum ModifierActivationLimit { Unlimited, OncePerHand, OncePerStage }

    public sealed class ModifierDefinition
    {
        /** Identifies a charm where an identity is stored, as in saves and the player profile. */
        public readonly struct Id : IEquatable<Id>, IContentId
        {
            public Guid Value { get; }

            public Id(Guid value)
            {
                if (value == Guid.Empty) throw new ArgumentException("A charm id requires a nonempty GUID.", nameof(value));
                Value = value;
            }

            public bool Equals(Id other) => Value == other.Value;
            public override bool Equals(object obj) => obj is Id other && Equals(other);
            public override int GetHashCode() => Value.GetHashCode();
            /** The 32 hexadecimal digits, as the charm's asset and its .meta file write its GUID. */
            public override string ToString() => Value.ToString("N");
            public static bool operator ==(Id left, Id right) => left.Equals(right);
            public static bool operator !=(Id left, Id right) => !left.Equals(right);
        }

        // A nested type and a member cannot share a name, so the id property is not called Id.
        public Id Identity { get; }
        public ModifierFamilyDefinition Family { get; }
        public ModifierPhase Phase { get; }
        public IModifierCondition Condition { get; }
        public ReadOnlyCollection<ModifierEffect> Effects { get; }
        public ModifierActivationLimit ActivationLimit { get; }
        public bool RewardEligible { get; }
        public ModifierAvailabilityDefinition Availability { get; }

        /** A charm that can be offered and can ever do something when it activates. */
        public bool IsPlayable => RewardEligible && Effects.Count > 0 && Condition.CanEverActivate;

        /** A playable starter charm; every player has discovered its family. */
        public bool IsDiscoveredFromStart => Availability.Access == ModifierAccess.Starter && IsPlayable;

        public ModifierDefinition(Id identity, ModifierFamilyDefinition family, ModifierPhase phase, IModifierCondition condition,
            IEnumerable<ModifierEffect> effects, ModifierActivationLimit activationLimit = ModifierActivationLimit.OncePerHand,
            bool rewardEligible = true, ModifierAvailabilityDefinition availability = null)
        {
            if (identity.Value == Guid.Empty) throw new ArgumentException("A charm requires an id.", nameof(identity));
            if (!Enum.IsDefined(typeof(ModifierPhase), phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            if (!Enum.IsDefined(typeof(ModifierActivationLimit), activationLimit)) throw new ArgumentOutOfRangeException(nameof(activationLimit));
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            if (effects == null) throw new ArgumentNullException(nameof(effects));
            var entries = new List<ModifierEffect>(effects);
            foreach (var effect in entries)
                if (effect == null) throw new ArgumentException("An effect is missing.", nameof(effects));
            Identity = identity;
            Family = family ?? throw new ArgumentNullException(nameof(family));
            Phase = phase;
            Condition = condition;
            Effects = entries.AsReadOnly();
            ActivationLimit = activationLimit;
            RewardEligible = rewardEligible;
            Availability = availability ?? new ModifierAvailabilityDefinition();
        }

        /**
         * Rejects an encounter-exclusive charm listed at place: an encounter's own charm list is the only statement of
         * the encounter that offers it.
         */
        internal void RejectEncounterExclusive(string place, string parameter)
        {
            if (Availability.Access == ModifierAccess.EncounterExclusive)
                throw new ArgumentException("The encounter-exclusive charm " + Identity + " is listed in " + place +
                    "; only an encounter's own charm list offers it.", parameter);
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
        /**
         * The stage's hands remaining including the hand being resolved, in every phase: a hand being played still counts, so
         * the last hand sees 1 and HandsPlayedInStage + HandsRemaining == StartingHands.
         */
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
