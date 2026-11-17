using System;
using System.Collections.Generic;

namespace Dice.Engine
{
    public enum DieInterferenceKind { ForcedReroll, Flip }
    public enum InterferenceTrigger { PlayerReroll, PlayHand }

    public sealed class DieDisturbedEvent : RuleEvent
    {
        public EncounterRuleDefinition.Id RuleId { get; }
        public int Bay { get; }
        public DieInterferenceKind Kind { get; }
        /** The committed action the disturbance followed. */
        public InterferenceTrigger Trigger { get; }
        public int Before { get; }
        public int After { get; }

        internal DieDisturbedEvent(EncounterState state, EncounterRuleDefinition.Id ruleId, int bay, DieInterferenceKind kind,
            InterferenceTrigger trigger, int before, int after)
            : base(state.StageIndex, state.HandIndex)
        {
            RuleId = ruleId;
            Bay = bay;
            Kind = kind;
            Trigger = trigger;
            Before = before;
            After = after;
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => RuleId + " " + format.Humanize(Kind.ToString()) + " on die " + (Bay + 1) + " after " +
                format.Humanize(Trigger.ToString()) + ": " + Before + " -> " + After + ".";
    }

    public sealed class DisturbanceBlockedEvent : RuleEvent
    {
        public EncounterRuleDefinition.Id RuleId { get; }
        public int Bay { get; }
        public DieInterferenceKind Kind { get; }
        /** The committed action the blocked disturbance followed. */
        public InterferenceTrigger Trigger { get; }
        public EncounterRuleDefinition.Id ProtectingRuleId { get; }

        internal DisturbanceBlockedEvent(EncounterState state, EncounterRuleDefinition.Id ruleId, int bay, DieInterferenceKind kind,
            InterferenceTrigger trigger, EncounterRuleDefinition.Id protectingRuleId)
            : base(state.StageIndex, state.HandIndex)
        {
            RuleId = ruleId;
            Bay = bay;
            Kind = kind;
            Trigger = trigger;
            ProtectingRuleId = protectingRuleId;
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => RuleId + " " + format.Humanize(Kind.ToString()) + " on die " + (Bay + 1) + " after " +
                format.Humanize(Trigger.ToString()) + " blocked by " + ProtectingRuleId + ".";
    }

    /** An interference rule's snapshot: the dice it disturbed during the current hand and the one it disturbed last. */
    public sealed class DieInterferenceRuleSnapshot : EncounterRuleSnapshot
    {
        /** Dice changed by interference during this hand; they do not count as rerolled or changed for charms. */
        public int DisturbedMask { get; }
        /** The die disturbed by the latest committed action, if any. */
        public int LatestDisturbedMask { get; }

        public DieInterferenceRuleSnapshot(EncounterRuleDefinition.Id id, bool isAvailable, int disturbedMask, int latestDisturbedMask)
            : base(id, isAvailable)
        {
            if ((disturbedMask & ~DiceValues.AllMask) != 0) throw new ArgumentOutOfRangeException(nameof(disturbedMask));
            if ((latestDisturbedMask & ~disturbedMask) != 0) throw new ArgumentOutOfRangeException(nameof(latestDisturbedMask));
            DisturbedMask = disturbedMask;
            LatestDisturbedMask = latestDisturbedMask;
        }

        public override string DiagnosticDieLabel(int bay) => (DisturbedMask & (1 << bay)) != 0 ? "DISTURBED" : null;

        private protected override bool StateEquals(EncounterRuleSnapshot other)
        {
            var interference = (DieInterferenceRuleSnapshot)other;
            return DisturbedMask == interference.DisturbedMask && LatestDisturbedMask == interference.LatestDisturbedMask;
        }

        private protected override int StateHash => DisturbedMask << DiceValues.Count | LatestDisturbedMask;
    }

    /** The dice an interference rule disturbed during the current hand. */
    public sealed class DieInterferenceState : EncounterRuleState
    {
        public int DisturbedMask;
        public int LatestDisturbedMask;

        internal override EncounterRuleState Copy() => (DieInterferenceState)MemberwiseClone();
    }

    /** A third participant may disturb one die after a committed reroll or Play Hand. */
    public sealed class DieInterferenceRuleDefinition : EncounterRuleDefinition
    {
        public int ChancePercent { get; }
        public int ForcedRerollWeight { get; }
        public int FlipWeight { get; }
        public bool AfterPlayerReroll { get; }
        public bool AfterPlayHand { get; }

        public DieInterferenceRuleDefinition(Id identity, int chancePercent, int forcedRerollWeight, int flipWeight,
            bool afterPlayerReroll, bool afterPlayHand) : base(identity)
        {
            if (chancePercent < 0 || chancePercent > 100) throw new ArgumentOutOfRangeException(nameof(chancePercent));
            if (forcedRerollWeight < 0) throw new ArgumentOutOfRangeException(nameof(forcedRerollWeight));
            if (flipWeight < 0) throw new ArgumentOutOfRangeException(nameof(flipWeight));
            if ((long)forcedRerollWeight + flipWeight <= 0 || (long)forcedRerollWeight + flipWeight > int.MaxValue)
                throw new ArgumentException("Interference effects require a positive total weight.", nameof(flipWeight));
            ChancePercent = chancePercent;
            ForcedRerollWeight = forcedRerollWeight;
            FlipWeight = flipWeight;
            AfterPlayerReroll = afterPlayerReroll;
            AfterPlayHand = afterPlayHand;
        }

        internal override bool IsAvailable(EncounterState state) => true;
        internal override bool UsesRuleRandom => true;
        public override Type StateType => typeof(DieInterferenceState);
        internal override EncounterRuleState CreateState() => new DieInterferenceState();

        internal override EncounterRuleSnapshot CreateSnapshot(EncounterState state, InstalledRule installed)
        {
            var value = (DieInterferenceState)installed.State;
            return new DieInterferenceRuleSnapshot(Identity, ShowsAvailable(state), value.DisturbedMask, value.LatestDisturbedMask);
        }

        /** The disturbed mask names dice within the five bays and holds the latest disturbed die, as the snapshot requires. */
        internal override bool IsStateUsable(EncounterRuleState state, out string detail)
        {
            var value = (DieInterferenceState)state;
            bool usable = (value.DisturbedMask & ~DiceValues.AllMask) == 0 && (value.LatestDisturbedMask & ~value.DisturbedMask) == 0;
            detail = usable ? null : "Disturbed dice lie outside the five bays or the latest disturbed die is not among them.";
            return usable;
        }

        internal override void HandStarted(EncounterState state, InstalledRule installed, int rolledMask,
            List<RuleEvent> events)
        {
            var value = (DieInterferenceState)installed.State;
            value.DisturbedMask = 0;
            value.LatestDisturbedMask = 0;
        }

        internal override bool Interfere(EncounterState state, InstalledRule installed, InterferenceTrigger trigger,
            int eligibleMask, List<RuleEvent> events)
        {
            var disturbance = (DieInterferenceState)installed.State;
            disturbance.LatestDisturbedMask = 0;
            if (trigger == InterferenceTrigger.PlayerReroll ? !AfterPlayerReroll : !AfterPlayHand) return false;
            if (eligibleMask == 0 || state.RuleRandom.NextInt(100) >= ChancePercent) return false;
            int bay = DiceValues.NthBay(eligibleMask, state.RuleRandom.NextInt(DiceValues.CountBays(eligibleMask)));
            var kind = state.RuleRandom.NextInt(ForcedRerollWeight + FlipWeight) < ForcedRerollWeight
                ? DieInterferenceKind.ForcedReroll : DieInterferenceKind.Flip;
            var protector = EncounterRuleResolver.Protector(state, bay, kind);
            if (protector != null)
            {
                events.Add(new DisturbanceBlockedEvent(state, Identity, bay, kind, trigger, protector.Identity));
                return true;
            }
            int before = state.CurrentDice[bay];
            int after = kind == DieInterferenceKind.Flip ? DiceValues.OppositeFace(before) : DiceValues.RollFace(ref state.RuleRandom);
            state.CurrentDice = state.CurrentDice.With(bay, after);
            disturbance.DisturbedMask |= 1 << bay;
            disturbance.LatestDisturbedMask = 1 << bay;
            events.Add(new DieDisturbedEvent(state, Identity, bay, kind, trigger, before, after));
            return true;
        }
    }

    /** Protects the bay occupied by a positioned table piece against the named interference effects. */
    public sealed class BayWardRuleDefinition : EncounterRuleDefinition
    {
        private readonly HashSet<DieInterferenceKind> blocked;
        public TablePieceDefinition.Id PieceId { get; }

        public BayWardRuleDefinition(Id identity, TablePieceDefinition.Id pieceId, IEnumerable<DieInterferenceKind> blockedKinds)
            : base(identity)
        {
            if (blockedKinds == null) throw new ArgumentNullException(nameof(blockedKinds));
            blocked = new HashSet<DieInterferenceKind>();
            foreach (var kind in blockedKinds)
            {
                if (kind != DieInterferenceKind.ForcedReroll && kind != DieInterferenceKind.Flip)
                    throw new ArgumentOutOfRangeException(nameof(blockedKinds));
                blocked.Add(kind);
            }
            if (blocked.Count == 0) throw new ArgumentException("A ward must block at least one effect.", nameof(blockedKinds));
            PieceId = pieceId;
        }

        public bool Blocks(DieInterferenceKind kind) => blocked.Contains(kind);

        internal override bool IsAvailable(EncounterState state) => true;

        internal override void ValidateDefinition(EncounterInPlay inPlay)
        {
            if (!TablePieceStates.Holds<PositionInteractionDefinition>(inPlay, PieceId))
                throw new ArgumentException("The ward " + Identity + " must reference a table piece in play with a position interaction: piece " +
                    PieceId + ".", nameof(inPlay));
        }

        internal override bool ProtectsBay(EncounterState state, int bay, DieInterferenceKind kind)
        {
            if (!blocked.Contains(kind)) return false;
            var position = ((PiecePositionState)TablePieceStates.Find(state, PieceId).StateOf<PositionInteractionDefinition>()).Position;
            return position.Kind == PiecePositionKind.DiceBay && position.Index == bay;
        }
    }
}
