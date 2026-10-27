using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    public abstract class EncounterRuleDefinition
    {
        public string Id { get; }

        internal EncounterRuleDefinition(string id)
        {
            DefinitionValidation.RequireId(id, nameof(id));
            Id = id;
        }

        internal abstract bool IsAvailable(EncounterState state);
        internal virtual bool WaivesRerollCost(EncounterState state) => false;
        internal virtual int FreeRerollsUsedOnHand(int rerollsSpentOnHand) => 0;
        internal virtual int MaximumFreeRerollsPerHand => 0;
        internal virtual long ScoredHandMultiplierBonus(DiceValues dice, long totalHandsPlayed) => 0;
        internal virtual void ValidateDefinition(EncounterDefinition encounter) { }

        /** Rules that draw from the rule random stream; encounters without one keep the stream empty. */
        internal virtual bool UsesRuleRandom => false;

        /** The rule as the snapshot shows it; a rule exposing more state returns its own snapshot type. */
        internal virtual EncounterRuleSnapshot CreateSnapshot(EncounterState state, EncounterRuleState ruleState)
            => new EncounterRuleSnapshot(Id, ShowsAvailable(state));

        /** Snapshots report a rule as available only while the encounter is playing. */
        private protected bool ShowsAvailable(EncounterState state) => state.Phase == EncounterPhase.Playing && IsAvailable(state);

        /** The rule's own serializable save class, or null for a rule without persisted state. */
        public virtual Type SaveDataType => null;

        internal virtual EncounterRuleState CreateState() => null;

        /** Returns a new instance of SaveDataType describing the rule's runtime state. */
        internal virtual object CaptureState(EncounterRuleState state) => null;

        /** Validates a saved state against the restored core encounter state; detail describes a rejection. */
        internal virtual bool TryRestoreState(EncounterState restored, object saved, bool postPlay, out EncounterRuleState state,
            out string detail)
        {
            state = null;
            detail = "The rule has no saved state.";
            return false;
        }

        /** Runs after the opening roll lands and before initial-roll modifiers resolve. */
        internal virtual void HandStarted(EncounterState state, EncounterRuleState ruleState, List<RuleEvent> events) { }

        /** Runs after rerolled dice land, before interference and reroll modifiers resolve. */
        internal virtual void PlayerRerollResolved(EncounterState state, EncounterRuleState ruleState, int rerolledMask,
            List<RuleEvent> events) { }

        /** Dice that cannot be selected for a player reroll. */
        internal virtual int LockedDiceMask(EncounterRuleState ruleState) => 0;

        /** Dice whose faces changed during this hand outside player rerolls. */
        internal virtual int DiceChangedOutsideRerollsMask(EncounterRuleState ruleState) => 0;

        /** Returns true when this rule attempted a disturbance, whether it changed a die or was blocked. */
        internal virtual bool Interfere(EncounterDefinition encounter, EncounterState state, EncounterRuleState ruleState,
            InterferenceTrigger trigger, int eligibleMask, List<RuleEvent> events) => false;

        internal virtual bool ProtectsBay(EncounterDefinition encounter, EncounterState state, int bay, DieInterferenceKind kind)
            => false;
    }

    /** Mutable runtime state owned by one encounter rule; copies never share mutable data. */
    internal abstract class EncounterRuleState
    {
        internal abstract EncounterRuleState Copy();
    }

    public sealed class FirstRerollFreeRuleDefinition : EncounterRuleDefinition
    {
        public const string StableId = "rule.first_reroll_free";

        public FirstRerollFreeRuleDefinition() : base(StableId) { }

        internal override bool IsAvailable(EncounterState state) => state.RerollsSpentOnHand == 0;
        internal override bool WaivesRerollCost(EncounterState state) => IsAvailable(state);
        internal override int FreeRerollsUsedOnHand(int rerollsSpentOnHand) => rerollsSpentOnHand > 0 ? 1 : 0;
        internal override int MaximumFreeRerollsPerHand => 1;
    }

    public sealed class DistinctFacesMultiplierRuleDefinition : EncounterRuleDefinition
    {
        public const string StableId = "rule.distinct_faces_multiplier";

        public DistinctFacesMultiplierRuleDefinition() : base(StableId) { }

        internal override bool IsAvailable(EncounterState state) => true;

        internal override long ScoredHandMultiplierBonus(DiceValues dice, long totalHandsPlayed)
        {
            int faceMask = 0;
            int distinct = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
            {
                int bit = 1 << dice[i];
                if ((faceMask & bit) == 0) ++distinct;
                faceMask |= bit;
            }
            return distinct;
        }
    }

    public sealed class CyclingFavoredFaceMultiplierRuleDefinition : EncounterRuleDefinition
    {
        public const string StableId = "rule.cycling_favored_face_multiplier";

        public CyclingFavoredFaceMultiplierRuleDefinition() : base(StableId) { }

        internal override bool IsAvailable(EncounterState state) => true;

        /** The persisted encounter-wide hand count keeps the cycle stable through stage changes and resume. */
        internal static int FavoredFace(long totalHandsPlayed) => (int)(totalHandsPlayed % 6) + 1;

        internal override EncounterRuleSnapshot CreateSnapshot(EncounterState state, EncounterRuleState ruleState)
            => new FavoredFaceRuleSnapshot(Id, ShowsAvailable(state), FavoredFace(state.TotalHandsPlayed));

        internal override long ScoredHandMultiplierBonus(DiceValues dice, long totalHandsPlayed)
        {
            int favoredFace = FavoredFace(totalHandsPlayed);
            long bonus = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
                if (dice[i] == favoredFace) bonus += 2;
            return bonus;
        }
    }

    /** What every encounter rule shows: its id and whether it is available now. Rules with more state extend it. */
    public class EncounterRuleSnapshot : IEquatable<EncounterRuleSnapshot>
    {
        private static readonly IReadOnlyDictionary<string, int> NoTextValues =
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());

        public string Id { get; }
        public bool IsAvailable { get; }

        /** Named values the rule's card text may show as {name} placeholders. */
        public virtual IReadOnlyDictionary<string, int> TextValues => NoTextValues;

        public EncounterRuleSnapshot(string id, bool isAvailable)
        {
            DefinitionValidation.RequireId(id, nameof(id));
            Id = id;
            IsAvailable = isAvailable;
        }

        public bool Equals(EncounterRuleSnapshot other)
            => other != null && other.GetType() == GetType() && Id == other.Id && IsAvailable == other.IsAvailable &&
                StateEquals(other);

        public sealed override bool Equals(object obj) => obj is EncounterRuleSnapshot other && Equals(other);
        public sealed override int GetHashCode()
            => unchecked((StringComparer.Ordinal.GetHashCode(Id) * 397 ^ IsAvailable.GetHashCode()) * 397 ^ StateHash);

        /** Compares the rule's own state with another snapshot of the same type. */
        private protected virtual bool StateEquals(EncounterRuleSnapshot other) => true;
        private protected virtual int StateHash => 0;
    }

    /** The cycling favored-face rule's snapshot; its card text names the favored face as {face}. */
    public sealed class FavoredFaceRuleSnapshot : EncounterRuleSnapshot
    {
        private readonly IReadOnlyDictionary<string, int> textValues;

        public int FavoredFace { get; }
        public override IReadOnlyDictionary<string, int> TextValues => textValues;

        public FavoredFaceRuleSnapshot(string id, bool isAvailable, int favoredFace) : base(id, isAvailable)
        {
            if (favoredFace < 1 || favoredFace > 6) throw new ArgumentOutOfRangeException(nameof(favoredFace));
            FavoredFace = favoredFace;
            textValues = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal) { ["face"] = favoredFace });
        }

        private protected override bool StateEquals(EncounterRuleSnapshot other) => FavoredFace == ((FavoredFaceRuleSnapshot)other).FavoredFace;
        private protected override int StateHash => FavoredFace;
    }

    internal static class EncounterRuleResolver
    {
        internal static ReadOnlyCollection<EncounterRuleSnapshot> Snapshots(EncounterDefinition definition, EncounterState state)
        {
            var snapshots = new EncounterRuleSnapshot[definition.Rules.Count];
            for (int i = 0; i < snapshots.Length; ++i) snapshots[i] = definition.Rules[i].CreateSnapshot(state, state.RuleStates[i]);
            return Array.AsReadOnly(snapshots);
        }

        internal static EncounterRuleDefinition RerollCostWaiver(EncounterDefinition definition, EncounterState state)
        {
            // A reroll has one cost to waive; the first applicable rule owns the activation event.
            foreach (var rule in definition.Rules)
                if (rule.WaivesRerollCost(state)) return rule;
            return null;
        }

        internal static int FreeRerollsUsedOnHand(EncounterDefinition definition, int rerollsSpentOnHand)
        {
            int used = 0;
            foreach (var rule in definition.Rules) used += rule.FreeRerollsUsedOnHand(rerollsSpentOnHand);
            return used;
        }

        internal static int MaximumFreeRerollsPerHand(EncounterDefinition definition)
        {
            int maximum = 0;
            foreach (var rule in definition.Rules) maximum += rule.MaximumFreeRerollsPerHand;
            return maximum;
        }

        internal static bool UsesRuleRandom(EncounterDefinition definition)
        {
            foreach (var rule in definition.Rules)
                if (rule.UsesRuleRandom) return true;
            return false;
        }

        internal static EncounterRuleState[] InitialStates(EncounterDefinition definition)
        {
            var states = new EncounterRuleState[definition.Rules.Count];
            for (int i = 0; i < states.Length; ++i) states[i] = definition.Rules[i].CreateState();
            return states;
        }

        internal static void HandStarted(EncounterDefinition definition, EncounterState state, List<RuleEvent> events)
        {
            for (int i = 0; i < definition.Rules.Count; ++i) definition.Rules[i].HandStarted(state, state.RuleStates[i], events);
        }

        internal static void PlayerRerollResolved(EncounterDefinition definition, EncounterState state, int rerolledMask,
            List<RuleEvent> events)
        {
            for (int i = 0; i < definition.Rules.Count; ++i)
                definition.Rules[i].PlayerRerollResolved(state, state.RuleStates[i], rerolledMask, events);
        }

        internal static int LockedDice(EncounterDefinition definition, EncounterState state)
        {
            int mask = 0;
            for (int i = 0; i < definition.Rules.Count; ++i) mask |= definition.Rules[i].LockedDiceMask(state.RuleStates[i]);
            return mask;
        }

        internal static int DiceChangedOutsideRerolls(EncounterDefinition definition, EncounterState state)
        {
            int mask = 0;
            for (int i = 0; i < definition.Rules.Count; ++i)
                mask |= definition.Rules[i].DiceChangedOutsideRerollsMask(state.RuleStates[i]);
            return mask;
        }

        internal static void Interfere(EncounterDefinition definition, EncounterState state, InterferenceTrigger trigger,
            int eligibleMask, List<RuleEvent> events)
        {
            // At most one disturbance attempt resolves for each committed action; later rules see no eligible dice.
            bool attempted = false;
            for (int i = 0; i < definition.Rules.Count; ++i)
                if (definition.Rules[i].Interfere(definition, state, state.RuleStates[i], trigger, attempted ? 0 : eligibleMask, events))
                    attempted = true;
        }

        internal static EncounterRuleDefinition Protector(EncounterDefinition definition, EncounterState state, int bay,
            DieInterferenceKind kind)
        {
            foreach (var rule in definition.Rules)
                if (rule.ProtectsBay(definition, state, bay, kind)) return rule;
            return null;
        }
    }
}
