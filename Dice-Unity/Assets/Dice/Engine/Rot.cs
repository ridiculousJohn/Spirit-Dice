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

        internal DiceRottedEvent(EncounterState state, EncounterRuleDefinition.Id ruleId, int rottedMask, DiceValues before,
            int witheredMask, int rerolledMask)
            : base(state.StageIndex, state.HandIndex)
        {
            RuleId = ruleId;
            RottedMask = rottedMask;
            Before = before;
            After = state.CurrentDice;
            WitheredMask = witheredMask;
            RerolledMask = rerolledMask;
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => RuleId + " rotted mask " + RottedMask + ": [" + DescribeDice(Before) + "] -> [" + DescribeDice(After) +
                "]. Withered mask " + WitheredMask + ", rerolled mask " + RerolledMask + ".";
    }

    /** Withered dice restored by the roll that threw them: a player reroll or the next hand's opening roll. */
    public sealed class DiceRestoredEvent : RuleEvent
    {
        public EncounterRuleDefinition.Id RuleId { get; }
        public int RestoredMask { get; }
        public RollKind Roll { get; }

        internal DiceRestoredEvent(EncounterState state, EncounterRuleDefinition.Id ruleId, int restoredMask, RollKind roll)
            : base(state.StageIndex, state.HandIndex)
        {
            RuleId = ruleId;
            RestoredMask = restoredMask;
            Roll = roll;
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => RuleId + " restored mask " + RestoredMask + " by the " + format.Humanize(Roll.ToString()) + ".";
    }

    /** A rot rule's snapshot: the dice withered out of the current hand. */
    public sealed class RotRuleSnapshot : EncounterRuleSnapshot
    {
        public int WitheredMask { get; }

        public RotRuleSnapshot(EncounterRuleDefinition.Id id, bool isAvailable, int witheredMask) : base(id, isAvailable)
        {
            if ((witheredMask & ~DiceValues.AllMask) != 0) throw new ArgumentOutOfRangeException(nameof(witheredMask));
            WitheredMask = witheredMask;
        }

        public override string DiagnosticDieLabel(int bay) => (WitheredMask & (1 << bay)) != 0 ? "WITHERED" : null;

        private protected override bool StateEquals(EncounterRuleSnapshot other) => WitheredMask == ((RotRuleSnapshot)other).WitheredMask;
        private protected override int StateHash => WitheredMask;
    }

    /** The dice a rot rule withered during the current hand. */
    public sealed class RotSaveData
    {
        public int WitheredMask;
    }

    internal sealed class RotState : EncounterRuleState
    {
        internal int WitheredMask;

        internal override EncounterRuleState Copy() => (RotState)MemberwiseClone();
    }

    /**
     * Unused things rot away: after each player reroll lands, every die the player did not select drops by one face.
     * The endpoint decides what a die that rots from its lowest face becomes. A withered die keeps that face internally
     * but counts toward nothing; it stays withered while kept, and the reroll that throws it, or the next opening roll,
     * restores it. Playing a hand and the opening roll rot nothing.
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
            return new RotSaveData { WitheredMask = value.WitheredMask };
        }

        /** The withered mask names dice within the five bays; restore checks that the hand still counts a die. */
        internal override bool TryRestoreState(object saved, out EncounterRuleState state, out string detail)
        {
            var source = (RotSaveData)saved;
            state = null;
            if ((source.WitheredMask & ~DiceValues.AllMask) != 0)
                detail = "Withered dice lie outside the five bays.";
            else
            {
                detail = null;
                state = new RotState { WitheredMask = source.WitheredMask };
            }
            return state != null;
        }

        /** The opening roll throws all five dice, so it restores every withered die. */
        internal override void HandStarted(EncounterState state, EncounterRuleState ruleState, List<RuleEvent> events)
        {
            var rot = (RotState)ruleState;
            if (rot.WitheredMask != 0) events.Add(new DiceRestoredEvent(state, Identity, rot.WitheredMask, RollKind.Initial));
            rot.WitheredMask = 0;
        }

        internal override void PlayerRerollResolved(EncounterState state, EncounterRuleState ruleState, int rerolledMask,
            List<RuleEvent> events)
        {
            var rot = (RotState)ruleState;
            int restoredMask = rot.WitheredMask & rerolledMask;
            if (restoredMask != 0)
            {
                rot.WitheredMask &= ~restoredMask;
                events.Add(new DiceRestoredEvent(state, Identity, restoredMask, RollKind.PlayerReroll));
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
            events.Add(new DiceRottedEvent(state, Identity, rotting, before, witheredMask, ruleRerolledMask));
        }

        /** Withered dice count toward nothing. */
        internal override int UncountedDiceMask(EncounterRuleState ruleState) => ((RotState)ruleState).WitheredMask;

        internal override EncounterRuleSnapshot CreateSnapshot(EncounterState state, EncounterRuleState ruleState)
            => new RotRuleSnapshot(Identity, ShowsAvailable(state), ((RotState)ruleState).WitheredMask);
    }
}
