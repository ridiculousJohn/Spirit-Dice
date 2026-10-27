using System;
using System.Collections.Generic;

namespace Dice.Engine
{
    public sealed class DieMarkedEvent : RuleEvent
    {
        public string RuleId { get; }
        public int Bay { get; }

        internal DieMarkedEvent(EncounterState state, string ruleId, int bay) : base(state.StageIndex, state.HandIndex)
        {
            RuleId = ruleId;
            Bay = bay;
        }

        protected override string DescribeDetail(IDiagnosticFormat format) => "Die " + (Bay + 1) + " marked for binding.";
    }

    public sealed class DieBoundEvent : RuleEvent
    {
        public string RuleId { get; }
        public int Bay { get; }
        public int Face { get; }

        internal DieBoundEvent(EncounterState state, string ruleId, int bay, int face) : base(state.StageIndex, state.HandIndex)
        {
            RuleId = ruleId;
            Bay = bay;
            Face = face;
        }

        protected override string DescribeDetail(IDiagnosticFormat format) => "Die " + (Bay + 1) + " bound at " + Face + ".";
    }

    /** A binding rule's snapshot: the dice it has bound and the die it has marked during the current hand. */
    public sealed class DieBindingRuleSnapshot : EncounterRuleSnapshot
    {
        /** Bound dice score normally but cannot be rerolled. */
        public int BoundMask { get; }
        /** The die that binds after the next player reroll. */
        public int MarkedMask { get; }

        public DieBindingRuleSnapshot(string id, bool isAvailable, int boundMask, int markedMask) : base(id, isAvailable)
        {
            if ((boundMask & ~DiceValues.AllMask) != 0) throw new ArgumentOutOfRangeException(nameof(boundMask));
            if ((markedMask & ~DiceValues.AllMask) != 0) throw new ArgumentOutOfRangeException(nameof(markedMask));
            BoundMask = boundMask;
            MarkedMask = markedMask;
        }

        public override string DiagnosticDieLabel(int bay)
        {
            int bit = 1 << bay;
            return (BoundMask & bit) != 0 ? "BOUND" : (MarkedMask & bit) != 0 ? "MARKED" : null;
        }

        private protected override bool StateEquals(EncounterRuleSnapshot other)
        {
            var binding = (DieBindingRuleSnapshot)other;
            return BoundMask == binding.BoundMask && MarkedMask == binding.MarkedMask;
        }

        private protected override int StateHash => BoundMask << DiceValues.Count | MarkedMask;
    }

    /** The dice a binding rule has bound and the die it has marked during the current hand. */
    [Serializable]
    public sealed class DieBindingSaveData
    {
        public int BoundMask;
        public int MarkedMask;
    }

    internal sealed class DieBindingState : EncounterRuleState
    {
        internal int BoundMask;
        internal int MarkedMask;

        internal override EncounterRuleState Copy() => (DieBindingState)MemberwiseClone();
    }

    /**
     * After a roll a third participant marks one unbound die; the marked die binds at its face after the next player
     * reroll, whether or not it was rerolled. Bound dice score normally, cannot be rerolled, and release at the next hand.
     */
    public sealed class DieBindingRuleDefinition : EncounterRuleDefinition
    {
        public bool MarksAfterOpeningRoll { get; }
        public bool MarksAfterPlayerReroll { get; }

        public DieBindingRuleDefinition(string id, bool marksAfterOpeningRoll, bool marksAfterPlayerReroll) : base(id)
        {
            MarksAfterOpeningRoll = marksAfterOpeningRoll;
            MarksAfterPlayerReroll = marksAfterPlayerReroll;
        }

        internal override bool IsAvailable(EncounterState state) => true;
        internal override bool UsesRuleRandom => true;
        public override Type SaveDataType => typeof(DieBindingSaveData);
        internal override EncounterRuleState CreateState() => new DieBindingState();

        internal override object CaptureState(EncounterRuleState state)
        {
            var value = (DieBindingState)state;
            return new DieBindingSaveData { BoundMask = value.BoundMask, MarkedMask = value.MarkedMask };
        }

        internal override bool TryRestoreState(EncounterState restored, object saved, bool postPlay, out EncounterRuleState state,
            out string detail)
        {
            var source = (DieBindingSaveData)saved;
            state = null;
            int bound = source.BoundMask;
            int marked = source.MarkedMask;
            ExpectedBindings(restored.RerollsSpentOnHand, out int expectedBound, out bool expectsMark);
            if ((bound & ~DiceValues.AllMask) != 0 || (marked & ~DiceValues.AllMask) != 0 || (bound & marked) != 0 ||
                (marked & (marked - 1)) != 0)
                detail = "Bound and marked dice masks are inconsistent.";
            else if (DiceValues.CountBays(bound) != expectedBound || (marked != 0) != expectsMark)
                detail = "Bound and marked dice disagree with the hand's rerolls.";
            else
            {
                detail = null;
                state = new DieBindingState { BoundMask = bound, MarkedMask = marked };
            }
            return state != null;
        }

        /** Replays the marking schedule for a hand; it settles once every die is bound or nothing remains to mark. */
        private void ExpectedBindings(int rerollsSpentOnHand, out int bound, out bool marked)
        {
            bound = 0;
            marked = MarksAfterOpeningRoll;
            for (int i = 0; i < rerollsSpentOnHand && (marked || (MarksAfterPlayerReroll && bound < DiceValues.Count)); ++i)
            {
                if (marked) ++bound;
                marked = MarksAfterPlayerReroll && bound < DiceValues.Count;
            }
        }

        internal override void HandStarted(EncounterState state, EncounterRuleState ruleState, List<RuleEvent> events)
        {
            var binding = (DieBindingState)ruleState;
            binding.BoundMask = 0;
            binding.MarkedMask = 0;
            if (MarksAfterOpeningRoll) Mark(state, binding, events);
        }

        internal override void PlayerRerollResolved(EncounterState state, EncounterRuleState ruleState, int rerolledMask,
            List<RuleEvent> events)
        {
            var binding = (DieBindingState)ruleState;
            if (binding.MarkedMask != 0)
            {
                int bay = DiceValues.NthBay(binding.MarkedMask, 0);
                binding.BoundMask |= binding.MarkedMask;
                binding.MarkedMask = 0;
                events.Add(new DieBoundEvent(state, Id, bay, state.CurrentDice[bay]));
            }
            if (MarksAfterPlayerReroll) Mark(state, binding, events);
        }

        internal override int LockedDiceMask(EncounterRuleState ruleState) => ((DieBindingState)ruleState).BoundMask;

        internal override EncounterRuleSnapshot CreateSnapshot(EncounterState state, EncounterRuleState ruleState)
        {
            var binding = (DieBindingState)ruleState;
            return new DieBindingRuleSnapshot(Id, ShowsAvailable(state), binding.BoundMask, binding.MarkedMask);
        }

        private void Mark(EncounterState state, DieBindingState binding, List<RuleEvent> events)
        {
            int unbound = DiceValues.AllMask & ~binding.BoundMask;
            if (unbound == 0) return;
            int bay = DiceValues.NthBay(unbound, state.RuleRandom.NextInt(DiceValues.CountBays(unbound)));
            binding.MarkedMask = 1 << bay;
            events.Add(new DieMarkedEvent(state, Id, bay));
        }
    }
}
