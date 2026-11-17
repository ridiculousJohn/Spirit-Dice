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
        /** The multiplier this rule adds to a scored hand, over the hand's counted dice. */
        internal virtual long ScoredHandMultiplierBonus(HandDice hand, long totalHandsPlayed) => 0;
        internal virtual void ValidateDefinition(EncounterInPlay inPlay) { }

        /** Rules that draw from the rule random stream; encounters without one keep the stream empty. */
        internal virtual bool UsesRuleRandom => false;

        /** The rule as the snapshot shows it, from its own state; a rule exposing more state returns its own snapshot type. */
        internal virtual EncounterRuleSnapshot CreateSnapshot(EncounterState state, InstalledRule installed)
            => new EncounterRuleSnapshot(Identity, ShowsAvailable(state));

        /** The names of the values this rule's snapshots supply in TextValues, which its card text may show as {name}. */
        public virtual IReadOnlyList<string> TextValueNames => Array.Empty<string>();

        /** Snapshots report a rule as available only while the encounter is playing. */
        private protected bool ShowsAvailable(EncounterState state) => state.Phase == EncounterPhase.Playing && IsAvailable(state);

        /**
         * The class of the rule's own state, which the run save writes as it is, or null for a rule that keeps no state. The
         * class's public fields are the saved fields.
         */
        public virtual Type StateType => null;

        /** The rule's state when it is installed in a run; null for a rule that keeps none. */
        internal virtual EncounterRuleState CreateState() => null;

        /** Whether a state of StateType has values in range for safe use; detail describes a rejection. */
        internal virtual bool IsStateUsable(EncounterRuleState state, out string detail)
        {
            detail = "The rule keeps no state.";
            return false;
        }

        /** The rule installed at the start of a run, with its starting state. */
        internal InstalledRule Install() => new InstalledRule(this, CreateState());

        /** The rule's saved entry: its id and a copy of its state. */
        internal EncounterSaveRuleData Save(InstalledRule installed)
            => new EncounterSaveRuleData { RuleId = Identity, State = installed.State?.Copy() };

        /**
         * The rule installed from its saved entry: its state of StateType with usable values, or absent exactly when it keeps
         * none. The run holds a copy of the saved state. A failure rejects the save, naming the rule.
         */
        internal InstalledRule Load(EncounterSaveRuleData saved)
        {
            string rule = "The rule " + Identity;
            if (StateType == null)
            {
                SavedEntries.Require(saved.State == null, rule + " keeps no state but its entry holds one.");
                return new InstalledRule(this, null);
            }
            SavedEntries.Require(saved.State != null && saved.State.GetType() == StateType, rule + " has no saved state of its state class.");
            SavedEntries.Require(IsStateUsable((EncounterRuleState)saved.State, out string detail),
                rule + " has an unusable saved state: " + detail);
            return new InstalledRule(this, ((EncounterRuleState)saved.State).Copy());
        }

        /**
         * The charms this rule adds to the charm additions in play while it is in play, in authored order; the encounter's
         * charm additions in play list them after its own.
         */
        public virtual IReadOnlyList<ModifierDefinition.Id> AddedModifierIds => Array.Empty<ModifierDefinition.Id>();

        /** Dice the next opening roll leaves as they lie: they keep the face and bay they show while the others are rolled. */
        internal virtual int OpeningRollHeldDiceMask(InstalledRule installed) => 0;

        /** Runs after the opening roll lands and before initial-roll modifiers resolve; rolledMask names the dice it threw. */
        internal virtual void HandStarted(EncounterState state, InstalledRule installed, int rolledMask, List<RuleEvent> events) { }

        /**
         * Runs after a committed hand is scored and before the next hand or stage starts; scoredHand is the hand as it
         * scored, naming the dice that counted. Previews do not run it.
         */
        internal virtual void HandPlayed(EncounterState state, InstalledRule installed, HandDice scoredHand,
            List<RuleEvent> events) { }

        /** Runs after rerolled dice land, before interference and reroll modifiers resolve. */
        internal virtual void PlayerRerollResolved(EncounterState state, InstalledRule installed, int rerolledMask,
            List<RuleEvent> events) { }

        /** Dice that cannot be selected for a player reroll. */
        internal virtual int LockedDiceMask(InstalledRule installed) => 0;

        /** Dice that do not count in the current hand: scoring, hand ranks and charm conditions read only the counted dice. */
        internal virtual int UncountedDiceMask(InstalledRule installed) => 0;

        /** Whether UncountedDiceMask can ever name a die. */
        internal virtual bool CanLeaveDiceUncounted => false;

        /** Returns true when this rule attempted a disturbance, whether it changed a die or was blocked. */
        internal virtual bool Interfere(EncounterState state, InstalledRule installed, InterferenceTrigger trigger,
            int eligibleMask, List<RuleEvent> events) => false;

        internal virtual bool ProtectsBay(EncounterState state, int bay, DieInterferenceKind kind) => false;
    }

    /**
     * Mutable runtime state owned by one encounter rule, which the run save writes as it is: its public fields are the
     * saved fields. Copies never share mutable data.
     */
    public abstract class EncounterRuleState
    {
        internal abstract EncounterRuleState Copy();
    }

    /** A rule installed in a run and its own state, null for a rule that keeps none. */
    internal sealed class InstalledRule
    {
        internal EncounterRuleDefinition Rule { get; }
        internal EncounterRuleState State { get; }

        internal InstalledRule(EncounterRuleDefinition rule, EncounterRuleState state)
        {
            Rule = rule ?? throw new ArgumentNullException(nameof(rule));
            State = state;
        }

        /** A copy holding a copy of the rule's state. */
        internal InstalledRule Copy() => new InstalledRule(Rule, State?.Copy());
    }

    /** Installs, saves and loads a list of rules, as an encounter does its own rules and an item its rules. */
    internal static class RuleList
    {
        internal static List<InstalledRule> Install(IReadOnlyList<EncounterRuleDefinition> rules)
        {
            var installed = new List<InstalledRule>(rules.Count);
            foreach (var rule in rules) installed.Add(rule.Install());
            return installed;
        }

        /** One entry per installed rule, in the owner's order. */
        internal static EncounterSaveRuleData[] Save(List<InstalledRule> installed)
            => installed.ConvertAll(rule => rule.Rule.Save(rule)).ToArray();

        /**
         * The owner's rules installed from its saved entries, each read by its rule's id; owner names the encounter or item
         * in failures, such as "encounter X".
         */
        internal static List<InstalledRule> Load(string owner, IReadOnlyList<EncounterRuleDefinition> rules, EncounterSaveRuleData[] saved)
        {
            var entries = SavedEntries.ById(owner, saved, entry => entry.RuleId, "rule");
            var installed = new List<InstalledRule>(rules.Count);
            foreach (var rule in rules) installed.Add(rule.Load(SavedEntries.Take(owner, entries, rule.Identity, "rule")));
            SavedEntries.RequireNoneLeft(owner, entries, "rule");
            return installed;
        }
    }

    public sealed class FirstRerollFreeRuleDefinition : EncounterRuleDefinition
    {
        public FirstRerollFreeRuleDefinition(Id identity) : base(identity) { }

        internal override bool IsAvailable(EncounterState state) => state.RerollsSpentOnHand == 0;
        internal override bool WaivesRerollCost(EncounterState state) => IsAvailable(state);
    }

    public sealed class DistinctFacesMultiplierRuleDefinition : EncounterRuleDefinition
    {
        public DistinctFacesMultiplierRuleDefinition(Id identity) : base(identity) { }

        internal override bool IsAvailable(EncounterState state) => true;

        internal override long ScoredHandMultiplierBonus(HandDice hand, long totalHandsPlayed) => hand.DistinctFaceCount;
    }

    public sealed class CyclingFavoredFaceMultiplierRuleDefinition : EncounterRuleDefinition
    {
        /** The name the snapshot supplies the favored face under, shown in card text as {face}. */
        internal const string FavoredFaceTextName = "face";
        private static readonly IReadOnlyList<string> FavoredFaceTextNames = Array.AsReadOnly(new[] { FavoredFaceTextName });

        /** The multiplier each scored die showing the favored face adds. */
        public int MultiplierPerFavoredDie { get; }

        public CyclingFavoredFaceMultiplierRuleDefinition(Id identity, int multiplierPerFavoredDie) : base(identity)
        {
            if (multiplierPerFavoredDie < 1) throw new ArgumentOutOfRangeException(nameof(multiplierPerFavoredDie));
            MultiplierPerFavoredDie = multiplierPerFavoredDie;
        }

        internal override bool IsAvailable(EncounterState state) => true;

        public override IReadOnlyList<string> TextValueNames => FavoredFaceTextNames;

        /** The persisted encounter-wide hand count keeps the cycle stable through stage changes and resume. */
        internal static int FavoredFace(long totalHandsPlayed) => (int)(totalHandsPlayed % DiceValues.FaceCount) + DiceValues.MinimumFace;

        internal override EncounterRuleSnapshot CreateSnapshot(EncounterState state, InstalledRule installed)
            => new FavoredFaceRuleSnapshot(Identity, ShowsAvailable(state), FavoredFace(state.TotalHandsPlayed));

        internal override long ScoredHandMultiplierBonus(HandDice hand, long totalHandsPlayed)
            => (long)hand.CountOf(FavoredFace(totalHandsPlayed)) * MultiplierPerFavoredDie;
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
            textValues = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal)
                { [CyclingFavoredFaceMultiplierRuleDefinition.FavoredFaceTextName] = favoredFace });
        }

        private protected override bool StateEquals(EncounterRuleSnapshot other) => FavoredFace == ((FavoredFaceRuleSnapshot)other).FavoredFace;
        private protected override int StateHash => FavoredFace;
    }

    /** Runs the run's installed rules, in installed order, each with its own state. */
    internal static class EncounterRuleResolver
    {
        internal static ReadOnlyCollection<EncounterRuleSnapshot> Snapshots(EncounterState state)
        {
            var snapshots = new List<EncounterRuleSnapshot>();
            foreach (var installed in state.InstalledRules) snapshots.Add(installed.Rule.CreateSnapshot(state, installed));
            return snapshots.AsReadOnly();
        }

        /** Whether the player can pay for a reroll or a rule waives its cost. */
        internal static bool CanAffordReroll(EncounterState state)
            => state.RerollsRemaining > 0 || WaiveRerollCost(state) != null;

        /** The rule that waives this player reroll's cost, or null for a paid reroll. */
        internal static EncounterRuleDefinition WaiveRerollCost(EncounterState state)
        {
            // A reroll has one cost to waive; the first applicable rule owns the activation event.
            foreach (var installed in state.InstalledRules)
                if (installed.Rule.WaivesRerollCost(state)) return installed.Rule;
            return null;
        }

        internal static bool UsesRuleRandom(EncounterState state)
        {
            foreach (var installed in state.InstalledRules)
                if (installed.Rule.UsesRuleRandom) return true;
            return false;
        }

        /** The dice every rule holds out of the next opening roll. */
        internal static int OpeningRollHeldDice(EncounterState state)
        {
            int mask = 0;
            foreach (var installed in state.InstalledRules) mask |= installed.Rule.OpeningRollHeldDiceMask(installed);
            return mask;
        }

        internal static void HandStarted(EncounterState state, int rolledMask, List<RuleEvent> events)
        {
            foreach (var installed in state.InstalledRules) installed.Rule.HandStarted(state, installed, rolledMask, events);
        }

        internal static void HandPlayed(EncounterState state, HandDice scoredHand, List<RuleEvent> events)
        {
            foreach (var installed in state.InstalledRules) installed.Rule.HandPlayed(state, installed, scoredHand, events);
        }

        internal static void PlayerRerollResolved(EncounterState state, int rerolledMask, List<RuleEvent> events)
        {
            foreach (var installed in state.InstalledRules)
                installed.Rule.PlayerRerollResolved(state, installed, rerolledMask, events);
        }

        internal static int LockedDice(EncounterState state)
        {
            int mask = 0;
            foreach (var installed in state.InstalledRules) mask |= installed.Rule.LockedDiceMask(installed);
            return mask;
        }

        internal static int UncountedDice(EncounterState state)
        {
            int mask = 0;
            foreach (var installed in state.InstalledRules) mask |= installed.Rule.UncountedDiceMask(installed);
            return mask;
        }

        /** The current dice with the dice every rule leaves counted. */
        internal static HandDice CurrentHand(EncounterState state)
            => new HandDice(state.CurrentDice, DiceValues.AllMask & ~UncountedDice(state));

        internal static void Interfere(EncounterState state, InterferenceTrigger trigger, int eligibleMask, List<RuleEvent> events)
        {
            // At most one disturbance attempt resolves for each committed action; later rules see no eligible dice.
            bool attempted = false;
            foreach (var installed in state.InstalledRules)
                if (installed.Rule.Interfere(state, installed, trigger, attempted ? 0 : eligibleMask, events))
                    attempted = true;
        }

        internal static EncounterRuleDefinition Protector(EncounterState state, int bay, DieInterferenceKind kind)
        {
            foreach (var installed in state.InstalledRules)
                if (installed.Rule.ProtectsBay(state, bay, kind)) return installed.Rule;
            return null;
        }
    }
}
