using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    /** A die carried into a hand: the opening roll left it at the face and bay it showed when the last hand was played. */
    public sealed class DieCarriedEvent : RuleEvent
    {
        public EncounterRuleDefinition.Id RuleId { get; }
        public int Bay { get; }
        public int Face { get; }

        internal DieCarriedEvent(EncounterState state, EncounterRuleDefinition.Id ruleId, int bay, int face)
            : base(state.StageIndex, state.HandIndex)
        {
            RuleId = ruleId;
            Bay = bay;
            Face = face;
        }

        protected override string DescribeDetail(IDiagnosticFormat format) => "Die " + (Bay + 1) + " carried into the hand at " + Face + ".";
    }

    /** A player reroll threw a carried die, which ends its carried status for the hand. */
    public sealed class CarriedStatusEndedEvent : RuleEvent
    {
        public EncounterRuleDefinition.Id RuleId { get; }
        public int Bay { get; }

        internal CarriedStatusEndedEvent(EncounterState state, EncounterRuleDefinition.Id ruleId, int bay)
            : base(state.StageIndex, state.HandIndex)
        {
            RuleId = ruleId;
            Bay = bay;
        }

        protected override string DescribeDetail(IDiagnosticFormat format) => "Die " + (Bay + 1) + " rerolled; its carried status ends.";
    }

    /** A carry-over rule's snapshot: the dice carried into the current hand that keep their carried status. */
    public sealed class DieCarryOverRuleSnapshot : EncounterRuleSnapshot
    {
        public int CarriedMask { get; }

        public DieCarryOverRuleSnapshot(EncounterRuleDefinition.Id id, bool isAvailable, int carriedMask) : base(id, isAvailable)
        {
            if ((carriedMask & ~DiceValues.AllMask) != 0) throw new ArgumentOutOfRangeException(nameof(carriedMask));
            CarriedMask = carriedMask;
        }

        public override string DiagnosticDieLabel(int bay) => (CarriedMask & (1 << bay)) != 0 ? "CARRIED" : null;

        private protected override bool StateEquals(EncounterRuleSnapshot other) => CarriedMask == ((DieCarryOverRuleSnapshot)other).CarriedMask;
        private protected override int StateHash => CarriedMask;
    }

    /** The die a carry-over rule carries into the next hand and the die it carried into the current hand. */
    public sealed class DieCarryOverState : EncounterRuleState
    {
        /** The die the next opening roll holds, at most one; 0 when the next hand carries nothing. */
        public int PendingMask;
        /** The die carried into the current hand that keeps its carried status, at most one. */
        public int CarriedMask;

        internal override EncounterRuleState Copy() => (DieCarryOverState)MemberwiseClone();
    }
    /**
     * Carries a die from one hand into the next: when a hand is played, the die in the bay a positioned table piece sits
     * on keeps its face and bay through the next opening roll while the others are rolled, if it counted in the scored
     * hand. A carried die keeps its carried status for that hand until a player reroll throws it; disturbances and rot
     * change its face but not its status. The carry survives a stage clear; the first hand of an encounter carries nothing.
     */
    public sealed class DieCarryOverRuleDefinition : EncounterRuleDefinition
    {
        private readonly ReadOnlyCollection<ModifierDefinition.Id> modifierIds;

        public TablePieceDefinition.Id PieceId { get; }

        /** The charms the rule adds to the charm additions in play while it is in play. */
        public override IReadOnlyList<ModifierDefinition.Id> AddedModifierIds => modifierIds;

        public DieCarryOverRuleDefinition(Id identity, TablePieceDefinition.Id pieceId, IEnumerable<ModifierDefinition.Id> modifierIds)
            : base(identity)
        {
            if (modifierIds == null) throw new ArgumentNullException(nameof(modifierIds));
            PieceId = pieceId;
            this.modifierIds = new List<ModifierDefinition.Id>(modifierIds).AsReadOnly();
        }

        internal override bool IsAvailable(EncounterState state) => true;
        public override Type StateType => typeof(DieCarryOverState);
        internal override EncounterRuleState CreateState() => new DieCarryOverState();

        internal override void ValidateDefinition(EncounterInPlay inPlay)
        {
            if (!TablePieceStates.Holds<PositionInteractionDefinition>(inPlay, PieceId))
                throw new ArgumentException("The carry-over " + Identity + " must reference a table piece in play with a position interaction: piece " +
                    PieceId + ".", nameof(inPlay));
        }

        /** The pending and carried masks each name at most one die within the five bays. */
        internal override bool IsStateUsable(EncounterRuleState state, out string detail)
        {
            var value = (DieCarryOverState)state;
            bool usable = IsOneDieAtMost(value.PendingMask) && IsOneDieAtMost(value.CarriedMask);
            detail = usable ? null : "The pending or carried die lies outside the five bays or names more than one die.";
            return usable;
        }

        private static bool IsOneDieAtMost(int mask) => (mask & ~DiceValues.AllMask) == 0 && DiceValues.CountBays(mask) <= 1;

        internal override int OpeningRollHeldDiceMask(InstalledRule installed) => ((DieCarryOverState)installed.State).PendingMask;

        /** The die the opening roll held is carried into the hand; the pending carry is spent. */
        internal override void HandStarted(EncounterState state, InstalledRule installed, int rolledMask, List<RuleEvent> events)
        {
            var carry = (DieCarryOverState)installed.State;
            carry.CarriedMask = carry.PendingMask & ~rolledMask;
            carry.PendingMask = 0;
            if (carry.CarriedMask == 0) return;
            int bay = DiceValues.NthBay(carry.CarriedMask, 0);
            events.Add(new DieCarriedEvent(state, Identity, bay, state.CurrentDice[bay]));
        }

        /** Only a player reroll that throws the carried die ends its status, even if the same face returns. */
        internal override void PlayerRerollResolved(EncounterState state, InstalledRule installed, int rerolledMask,
            List<RuleEvent> events)
        {
            var carry = (DieCarryOverState)installed.State;
            int ended = carry.CarriedMask & rerolledMask;
            if (ended == 0) return;
            carry.CarriedMask &= ~ended;
            events.Add(new CarriedStatusEndedEvent(state, Identity, DiceValues.NthBay(ended, 0)));
        }

        /** The die in the piece's bay carries into the next hand if it counted; the neutral position carries nothing. */
        internal override void HandPlayed(EncounterState state, InstalledRule installed, HandDice scoredHand,
            List<RuleEvent> events)
        {
            var carry = (DieCarryOverState)installed.State;
            carry.PendingMask = 0;
            var position = ((PiecePositionState)TablePieceStates.Find(state, PieceId).StateOf<PositionInteractionDefinition>()).Position;
            if (position.Kind != PiecePositionKind.DiceBay) return;
            int bayMask = 1 << position.Index;
            if (scoredHand.Counts(bayMask)) carry.PendingMask = bayMask;
        }

        internal override EncounterRuleSnapshot CreateSnapshot(EncounterState state, InstalledRule installed)
            => new DieCarryOverRuleSnapshot(Identity, ShowsAvailable(state), ((DieCarryOverState)installed.State).CarriedMask);
    }
}
