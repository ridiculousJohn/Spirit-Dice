using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    public abstract class EncounterRuleDefinition
    {
        /** Identifies an encounter rule where an identity is stored: the run save keeps the rule's state under it. */
        public readonly struct Id : IEquatable<Id>, IContentId
        {
            public Guid Value { get; }

            public Id(Guid value)
            {
                if (value == Guid.Empty) throw new ArgumentException("An encounter rule id requires a nonempty GUID.", nameof(value));
                Value = value;
            }

            public bool Equals(Id other) => Value == other.Value;
            public override bool Equals(object obj) => obj is Id other && Equals(other);
            public override int GetHashCode() => Value.GetHashCode();
            /** The 32 hexadecimal digits, as the rule's asset and its .meta file write its GUID. */
            public override string ToString() => Value.ToString("N");
            public static bool operator ==(Id left, Id right) => left.Equals(right);
            public static bool operator !=(Id left, Id right) => !left.Equals(right);
        }

        // A nested type and a member cannot share a name, so the id property is not called Id.
        public Id Identity { get; }

        internal EncounterRuleDefinition(Id identity)
        {
            if (identity.Value == Guid.Empty) throw new ArgumentException("An encounter rule requires an id.", nameof(identity));
            Identity = identity;
        }

        internal abstract bool IsAvailable(EncounterState state);
        internal virtual bool WaivesRerollCost(EncounterState state) => false;
        internal virtual int FreeRerollsUsedOnHand(int rerollsSpentOnHand) => 0;
        internal virtual int MaximumFreeRerollsPerHand => 0;

        /** Rerolls whose cost this rule waived during the current stage. */
        internal virtual int FreeRerollsUsedInStage(EncounterRuleState ruleState) => 0;

        /** Rerolls whose cost this rule waived across the encounter. */
        internal virtual long TotalFreeRerollsUsed(EncounterRuleState ruleState) => 0;
        internal virtual long ScoredHandMultiplierBonus(DiceValues dice, long totalHandsPlayed) => 0;
        internal virtual void ValidateDefinition(EncounterDefinition encounter) { }

        /** Rules that draw from the rule random stream; encounters without one keep the stream empty. */
        internal virtual bool UsesRuleRandom => false;

        /** The rule as the snapshot shows it; a rule exposing more state returns its own snapshot type. */
        internal virtual EncounterRuleSnapshot CreateSnapshot(EncounterState state, EncounterRuleState ruleState)
            => new EncounterRuleSnapshot(Identity, ShowsAvailable(state));

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

        /** Runs when a stage starts, before its first hand. */
        internal virtual void StageStarted(EncounterState state, EncounterRuleState ruleState) { }

        /** Runs on the rule whose waiver applies to a player reroll, before the reroll is counted. */
        internal virtual void RerollCostWaived(EncounterState state, EncounterRuleState ruleState) { }

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

    /** The rerolls whose cost the first-reroll-free rule waived during the current stage and across the encounter. */
    [Serializable]
    public sealed class FirstRerollFreeSaveData
    {
        public int FreeRerollsUsedInStage;
        public long TotalFreeRerollsUsed;
    }

    internal sealed class FirstRerollFreeState : EncounterRuleState
    {
        internal int FreeRerollsUsedInStage;
        internal long TotalFreeRerollsUsed;

        internal override EncounterRuleState Copy() => (FirstRerollFreeState)MemberwiseClone();
    }

    public sealed class FirstRerollFreeRuleDefinition : EncounterRuleDefinition
    {
        public FirstRerollFreeRuleDefinition(Id identity) : base(identity) { }

        internal override bool IsAvailable(EncounterState state) => state.RerollsSpentOnHand == 0;
        internal override bool WaivesRerollCost(EncounterState state) => IsAvailable(state);
        internal override int FreeRerollsUsedOnHand(int rerollsSpentOnHand) => rerollsSpentOnHand > 0 ? 1 : 0;
        internal override int MaximumFreeRerollsPerHand => 1;
        public override Type SaveDataType => typeof(FirstRerollFreeSaveData);
        internal override EncounterRuleState CreateState() => new FirstRerollFreeState();

        internal override object CaptureState(EncounterRuleState state)
        {
            var value = (FirstRerollFreeState)state;
            return new FirstRerollFreeSaveData
            {
                FreeRerollsUsedInStage = value.FreeRerollsUsedInStage, TotalFreeRerollsUsed = value.TotalFreeRerollsUsed
            };
        }

        /** The counters must be ordered here; restore checks them against the hand and reroll history through the resolver. */
        internal override bool TryRestoreState(EncounterState restored, object saved, bool postPlay, out EncounterRuleState state,
            out string detail)
        {
            var source = (FirstRerollFreeSaveData)saved;
            state = null;
            if (source.FreeRerollsUsedInStage < 0 || source.TotalFreeRerollsUsed < source.FreeRerollsUsedInStage)
                detail = "Free reroll counters are inconsistent.";
            else
            {
                detail = null;
                state = new FirstRerollFreeState
                {
                    FreeRerollsUsedInStage = source.FreeRerollsUsedInStage, TotalFreeRerollsUsed = source.TotalFreeRerollsUsed
                };
            }
            return state != null;
        }

        internal override void StageStarted(EncounterState state, EncounterRuleState ruleState)
            => ((FirstRerollFreeState)ruleState).FreeRerollsUsedInStage = 0;

        internal override void RerollCostWaived(EncounterState state, EncounterRuleState ruleState)
        {
            var free = (FirstRerollFreeState)ruleState;
            free.FreeRerollsUsedInStage = checked(free.FreeRerollsUsedInStage + 1);
            free.TotalFreeRerollsUsed = checked(free.TotalFreeRerollsUsed + 1);
        }

        internal override int FreeRerollsUsedInStage(EncounterRuleState ruleState)
            => ((FirstRerollFreeState)ruleState).FreeRerollsUsedInStage;

        internal override long TotalFreeRerollsUsed(EncounterRuleState ruleState)
            => ((FirstRerollFreeState)ruleState).TotalFreeRerollsUsed;
    }

    public sealed class DistinctFacesMultiplierRuleDefinition : EncounterRuleDefinition
    {
        public DistinctFacesMultiplierRuleDefinition(Id identity) : base(identity) { }

        internal override bool IsAvailable(EncounterState state) => true;

        internal override long ScoredHandMultiplierBonus(DiceValues dice, long totalHandsPlayed) => dice.DistinctFaceCount;
    }

    public sealed class CyclingFavoredFaceMultiplierRuleDefinition : EncounterRuleDefinition
    {
        /** The multiplier each scored die showing the favored face adds. */
        public int MultiplierPerFavoredDie { get; }

        public CyclingFavoredFaceMultiplierRuleDefinition(Id identity, int multiplierPerFavoredDie) : base(identity)
        {
            if (multiplierPerFavoredDie < 1) throw new ArgumentOutOfRangeException(nameof(multiplierPerFavoredDie));
            MultiplierPerFavoredDie = multiplierPerFavoredDie;
        }

        internal override bool IsAvailable(EncounterState state) => true;

        /** The persisted encounter-wide hand count keeps the cycle stable through stage changes and resume. */
        internal static int FavoredFace(long totalHandsPlayed) => (int)(totalHandsPlayed % DiceValues.FaceCount) + DiceValues.MinimumFace;

        internal override EncounterRuleSnapshot CreateSnapshot(EncounterState state, EncounterRuleState ruleState)
            => new FavoredFaceRuleSnapshot(Identity, ShowsAvailable(state), FavoredFace(state.TotalHandsPlayed));

        internal override long ScoredHandMultiplierBonus(DiceValues dice, long totalHandsPlayed)
        {
            int favoredFace = FavoredFace(totalHandsPlayed);
            long bonus = 0;
            for (int i = 0; i < DiceValues.Count; ++i)
                if (dice[i] == favoredFace) bonus += MultiplierPerFavoredDie;
            return bonus;
        }
    }

    /** What every encounter rule shows: its id and whether it is available now. Rules with more state extend it. */
    public class EncounterRuleSnapshot : IEquatable<EncounterRuleSnapshot>
    {
        private static readonly IReadOnlyDictionary<string, int> NoTextValues =
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());

        public EncounterRuleDefinition.Id Id { get; }
        public bool IsAvailable { get; }

        /** Named values the rule's card text may show as {name} placeholders. */
        public virtual IReadOnlyDictionary<string, int> TextValues => NoTextValues;

        /** Developer diagnostics, not player-facing text: what this rule's state says about one die, or null for nothing. */
        public virtual string DiagnosticDieLabel(int bay) => null;

        public EncounterRuleSnapshot(EncounterRuleDefinition.Id id, bool isAvailable)
        {
            if (id.Value == Guid.Empty) throw new ArgumentException("A rule snapshot requires its rule's id.", nameof(id));
            Id = id;
            IsAvailable = isAvailable;
        }

        public bool Equals(EncounterRuleSnapshot other)
            => other != null && other.GetType() == GetType() && Id == other.Id && IsAvailable == other.IsAvailable &&
                StateEquals(other);

        public sealed override bool Equals(object obj) => obj is EncounterRuleSnapshot other && Equals(other);
        public sealed override int GetHashCode()
            => unchecked((Id.GetHashCode() * 397 ^ IsAvailable.GetHashCode()) * 397 ^ StateHash);

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

        public FavoredFaceRuleSnapshot(EncounterRuleDefinition.Id id, bool isAvailable, int favoredFace) : base(id, isAvailable)
        {
            if (!DiceValues.IsFace(favoredFace)) throw new ArgumentOutOfRangeException(nameof(favoredFace));
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

        /** Whether the player can pay for a reroll or a rule waives its cost. */
        internal static bool CanAffordReroll(EncounterDefinition definition, EncounterState state)
            => state.RerollsRemaining > 0 || RerollCostWaiverSlot(definition, state) >= 0;

        /** Tells the rule that waives this player reroll's cost that it did so; returns that rule, or null for a paid reroll. */
        internal static EncounterRuleDefinition WaiveRerollCost(EncounterDefinition definition, EncounterState state)
        {
            int slot = RerollCostWaiverSlot(definition, state);
            if (slot < 0) return null;
            definition.Rules[slot].RerollCostWaived(state, state.RuleStates[slot]);
            return definition.Rules[slot];
        }

        private static int RerollCostWaiverSlot(EncounterDefinition definition, EncounterState state)
        {
            // A reroll has one cost to waive; the first applicable rule owns the activation event.
            for (int i = 0; i < definition.Rules.Count; ++i)
                if (definition.Rules[i].WaivesRerollCost(state)) return i;
            return -1;
        }

        internal static long FreeRerollsUsedInStage(EncounterDefinition definition, EncounterState state)
        {
            long used = 0;
            for (int i = 0; i < definition.Rules.Count; ++i) used += definition.Rules[i].FreeRerollsUsedInStage(state.RuleStates[i]);
            return used;
        }

        internal static long TotalFreeRerollsUsed(EncounterDefinition definition, EncounterState state)
        {
            long used = 0;
            for (int i = 0; i < definition.Rules.Count; ++i)
                used = checked(used + definition.Rules[i].TotalFreeRerollsUsed(state.RuleStates[i]));
            return used;
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

        internal static void StageStarted(EncounterDefinition definition, EncounterState state)
        {
            for (int i = 0; i < definition.Rules.Count; ++i) definition.Rules[i].StageStarted(state, state.RuleStates[i]);
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
