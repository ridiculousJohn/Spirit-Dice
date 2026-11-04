using System;
using System.Collections.Generic;

namespace Dice.Engine
{
    /** What happens to a die that rots from its lowest face. */
    public enum RotEndpoint
    {
        /** The die withers: it counts toward nothing until a reroll or the next opening roll restores it. */
        Wither,
        /** The die stays at its lowest face. */
        Clamp,
        /** The die turns to its highest face. */
        Wrap,
        /** The die is rolled again from the rule random stream. */
        Reroll
    }

    /** One player reroll's rot step: every kept die that was not withered dropped by one face. */
    public sealed class DiceRottedEvent : RuleEvent
    {
        public EncounterRuleDefinition.Id RuleId { get; }
        /** The dice that rotted on this step. */
        public int RottedMask { get; }
        public DiceValues Before { get; }
        public DiceValues After { get; }
        /** The rotted dice that withered on this step. */
        public int WitheredMask { get; }
        /** The rotted dice the rule rolled again, under the Reroll endpoint. */
        public int RerolledMask { get; }
        /** Every die's rot count after this step: each rotted die's count rose by one, and the dice the rule rolled again are at 0. */
        public RotCounts Counts { get; }

        internal DiceRottedEvent(EncounterState state, EncounterRuleDefinition.Id ruleId, int rottedMask, DiceValues before,
            int witheredMask, int rerolledMask, RotCounts counts)
            : base(state.StageIndex, state.HandIndex)
        {
            RuleId = ruleId;
            RottedMask = rottedMask;
            Before = before;
            After = state.CurrentDice;
            WitheredMask = witheredMask;
            RerolledMask = rerolledMask;
            Counts = counts;
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => RuleId + " rotted mask " + RottedMask + ": [" + DescribeDice(Before) + "] -> [" + DescribeDice(After) +
                "]. Withered mask " + WitheredMask + ", rerolled mask " + RerolledMask + ", rot counts [" + Counts + "].";
    }

    /**
     * The rot a roll cleared from the dice it threw, a player reroll or the next hand's opening roll: their rot counts
     * returned to 0 and the withered dice among them were restored.
     */
    public sealed class DiceRestoredEvent : RuleEvent
    {
        public EncounterRuleDefinition.Id RuleId { get; }
        /** The thrown dice that were withered. */
        public int RestoredMask { get; }
        /** The thrown dice whose rot count was above 0. */
        public int ClearedMask { get; }
        /** Every die's rot count after the roll. */
        public RotCounts Counts { get; }
        public RollKind Roll { get; }

        internal DiceRestoredEvent(EncounterState state, EncounterRuleDefinition.Id ruleId, int restoredMask, int clearedMask,
            RotCounts counts, RollKind roll)
            : base(state.StageIndex, state.HandIndex)
        {
            RuleId = ruleId;
            RestoredMask = restoredMask;
            ClearedMask = clearedMask;
            Counts = counts;
            Roll = roll;
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => RuleId + " restored mask " + RestoredMask + ", cleared mask " + ClearedMask + " by the " +
                format.Humanize(Roll.ToString()) + "; rot counts [" + Counts + "].";
    }

    /**
     * Each die's rot count: the rot steps the die has rotted in since it was last rolled. A count stops rising at
     * MaximumCount, far beyond any hand's rerolls, so every count stays within the range a save accepts.
     */
    public readonly struct RotCounts : IEquatable<RotCounts>
    {
        public const int MaximumCount = 99;
        // Null while every count is 0, so the default value is a hand nothing has rotted in.
        private readonly int[] counts;

        public RotCounts(IReadOnlyList<int> values)
        {
            if (!AreCounts(values)) throw new ArgumentOutOfRangeException(nameof(values));
            counts = new int[DiceValues.Count];
            for (int bay = 0; bay < counts.Length; ++bay) counts[bay] = values[bay];
        }

        public int this[int bay]
        {
            get
            {
                if ((uint)bay >= DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(bay));
                return counts?[bay] ?? 0;
            }
        }

        /** The dice whose count is above 0. */
        public int NonzeroMask
        {
            get
            {
                int mask = 0;
                for (int bay = 0; bay < DiceValues.Count; ++bay)
                    if (this[bay] > 0) mask |= 1 << bay;
                return mask;
            }
        }

        public int[] ToArray()
        {
            var values = new int[DiceValues.Count];
            for (int bay = 0; bay < values.Length; ++bay) values[bay] = this[bay];
            return values;
        }

        /** Whether the values are five counts from 0 to MaximumCount. */
        public static bool AreCounts(IReadOnlyList<int> values)
        {
            if (values == null || values.Count != DiceValues.Count) return false;
            foreach (int value in values)
                if (value < 0 || value > MaximumCount) return false;
            return true;
        }

        /** The counts with each masked die's count raised by one, up to MaximumCount. */
        internal RotCounts Raised(int mask)
        {
            int[] values = ToArray();
            for (int bay = 0; bay < values.Length; ++bay)
                if ((mask & (1 << bay)) != 0 && values[bay] < MaximumCount) ++values[bay];
            return new RotCounts(values);
        }

        /** The counts with each masked die's count at 0. */
        internal RotCounts Cleared(int mask)
        {
            int[] values = ToArray();
            for (int bay = 0; bay < values.Length; ++bay)
                if ((mask & (1 << bay)) != 0) values[bay] = 0;
            return new RotCounts(values);
        }

        public bool Equals(RotCounts other)
        {
            for (int bay = 0; bay < DiceValues.Count; ++bay)
                if (this[bay] != other[bay]) return false;
            return true;
        }

        public override bool Equals(object obj) => obj is RotCounts other && Equals(other);

        public override int GetHashCode()
        {
            int hash = 0;
            for (int bay = 0; bay < DiceValues.Count; ++bay) hash = unchecked(hash * (MaximumCount + 1) + this[bay]);
            return hash;
        }

        public static bool operator ==(RotCounts left, RotCounts right) => left.Equals(right);
        public static bool operator !=(RotCounts left, RotCounts right) => !left.Equals(right);

        /** The counts in bay order, for diagnostics. */
        public override string ToString() => string.Join(", ", ToArray());
    }

    /** A rot rule's snapshot: the dice withered out of the current hand and each die's rot count. */
    public sealed class RotRuleSnapshot : EncounterRuleSnapshot
    {
        public int WitheredMask { get; }
        public RotCounts Counts { get; }

        public RotRuleSnapshot(EncounterRuleDefinition.Id id, bool isAvailable, int witheredMask, RotCounts counts)
            : base(id, isAvailable)
        {
            if ((witheredMask & ~DiceValues.AllMask) != 0) throw new ArgumentOutOfRangeException(nameof(witheredMask));
            WitheredMask = witheredMask;
            Counts = counts;
        }

        public override string DiagnosticDieLabel(int bay)
            => (WitheredMask & (1 << bay)) != 0 ? "WITHERED" : Counts[bay] > 0 ? "ROT " + Counts[bay] : null;

        private protected override bool StateEquals(EncounterRuleSnapshot other)
        {
            var rot = (RotRuleSnapshot)other;
            return WitheredMask == rot.WitheredMask && Counts == rot.Counts;
        }

        private protected override int StateHash => unchecked(WitheredMask * 397 ^ Counts.GetHashCode());
    }

    /** The dice a rot rule withered during the current hand and each die's rot count. */
    public sealed class RotSaveData
    {
        public int WitheredMask;
        /** Each die's rot count, in bay order. */
        public int[] Counts;
    }

    internal sealed class RotState : EncounterRuleState
    {
        internal int WitheredMask;
        internal RotCounts Counts;

        internal override EncounterRuleState Copy() => (RotState)MemberwiseClone();
    }

    /**
     * Unused things rot away: after each player reroll lands, every die the player did not select drops by one face.
     * The endpoint decides what a die that rots from its lowest face becomes. A withered die keeps that face internally
     * but counts toward nothing; it stays withered while kept, and the reroll that throws it, or the next opening roll,
     * restores it. Playing a hand and the opening roll rot nothing. The rule counts, per die, the rot steps the die has
     * rotted in since it was last rolled: a player reroll, the Reroll endpoint and the next opening roll return the dice
     * they throw to 0.
     */
    public sealed class RotRuleDefinition : EncounterRuleDefinition
    {
        public RotEndpoint Endpoint { get; }

        public RotRuleDefinition(Id identity, RotEndpoint endpoint) : base(identity)
        {
            if (!Enum.IsDefined(typeof(RotEndpoint), endpoint)) throw new ArgumentOutOfRangeException(nameof(endpoint));
            Endpoint = endpoint;
        }

        internal override bool IsAvailable(EncounterState state) => true;
        internal override bool UsesRuleRandom => Endpoint == RotEndpoint.Reroll;
        public override Type SaveDataType => typeof(RotSaveData);
        internal override EncounterRuleState CreateState() => new RotState();

        internal override object CaptureState(EncounterRuleState state)
        {
            var value = (RotState)state;
            return new RotSaveData { WitheredMask = value.WitheredMask, Counts = value.Counts.ToArray() };
        }

        /**
         * The withered mask names dice within the five bays and the counts are five counts from 0 to RotCounts.MaximumCount;
         * restore checks that the hand still counts a die.
         */
        internal override bool TryRestoreState(object saved, out EncounterRuleState state, out string detail)
        {
            var source = (RotSaveData)saved;
            state = null;
            if ((source.WitheredMask & ~DiceValues.AllMask) != 0)
                detail = "Withered dice lie outside the five bays.";
            else if (!RotCounts.AreCounts(source.Counts))
                detail = "Rot counts must be five counts from 0 to " + RotCounts.MaximumCount + ".";
            else
            {
                detail = null;
                state = new RotState { WitheredMask = source.WitheredMask, Counts = new RotCounts(source.Counts) };
            }
            return state != null;
        }

        /** The opening roll throws all five dice, so it restores every withered die and returns every count to 0. */
        internal override void HandStarted(EncounterState state, EncounterRuleState ruleState, List<RuleEvent> events)
        {
            var rot = (RotState)ruleState;
            int restoredMask = rot.WitheredMask, clearedMask = rot.Counts.NonzeroMask;
            rot.WitheredMask = 0;
            rot.Counts = default;
            if (restoredMask != 0 || clearedMask != 0)
                events.Add(new DiceRestoredEvent(state, Identity, restoredMask, clearedMask, rot.Counts, RollKind.Initial));
        }

        internal override void PlayerRerollResolved(EncounterState state, EncounterRuleState ruleState, int rerolledMask,
            List<RuleEvent> events)
        {
            var rot = (RotState)ruleState;
            int restoredMask = rot.WitheredMask & rerolledMask, clearedMask = rot.Counts.NonzeroMask & rerolledMask;
            if (restoredMask != 0 || clearedMask != 0)
            {
                rot.WitheredMask &= ~restoredMask;
                rot.Counts = rot.Counts.Cleared(rerolledMask);
                events.Add(new DiceRestoredEvent(state, Identity, restoredMask, clearedMask, rot.Counts, RollKind.PlayerReroll));
            }
            // A withered die left out of the reroll stays withered without rotting further.
            int rotting = DiceValues.AllMask & ~rerolledMask & ~rot.WitheredMask;
            if (rotting == 0) return;
            DiceValues before = state.CurrentDice;
            int[] faces = before.ToArray();
            int witheredMask = 0, ruleRerolledMask = 0;
            for (int bay = 0; bay < DiceValues.Count; ++bay)
            {
                if ((rotting & (1 << bay)) == 0) continue;
                if (faces[bay] > DiceValues.MinimumFace)
                {
                    --faces[bay];
                    continue;
                }
                switch (Endpoint)
                {
                    case RotEndpoint.Wither:
                        witheredMask |= 1 << bay;
                        break;
                    case RotEndpoint.Clamp:
                        break;
                    case RotEndpoint.Wrap:
                        faces[bay] = DiceValues.MaximumFace;
                        break;
                    case RotEndpoint.Reroll:
                        faces[bay] = DiceValues.RollFace(ref state.RuleRandom);
                        ruleRerolledMask |= 1 << bay;
                        break;
                }
            }
            state.CurrentDice = new DiceValues(faces[0], faces[1], faces[2], faces[3], faces[4]);
            rot.WitheredMask |= witheredMask;
            // A die the rule rolls again has just been rolled, so its count starts over.
            rot.Counts = rot.Counts.Raised(rotting).Cleared(ruleRerolledMask);
            events.Add(new DiceRottedEvent(state, Identity, rotting, before, witheredMask, ruleRerolledMask, rot.Counts));
        }

        /** Withered dice count toward nothing. */
        internal override int UncountedDiceMask(EncounterRuleState ruleState) => ((RotState)ruleState).WitheredMask;

        internal override EncounterRuleSnapshot CreateSnapshot(EncounterState state, EncounterRuleState ruleState)
        {
            var rot = (RotState)ruleState;
            return new RotRuleSnapshot(Identity, ShowsAvailable(state), rot.WitheredMask, rot.Counts);
        }
    }
}
