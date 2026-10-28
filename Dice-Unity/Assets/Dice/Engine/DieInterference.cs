using System;
using System.Collections.Generic;

namespace Dice.Engine
{
    public enum DieInterferenceKind { ForcedReroll, Flip }
    public enum InterferenceTrigger { PlayerReroll, PlayHand }

    public sealed class DieDisturbedEvent : RuleEvent
    {
        public string RuleId { get; }
        public int Bay { get; }
        public DieInterferenceKind Kind { get; }
        /** The committed action the disturbance followed. */
        public InterferenceTrigger Trigger { get; }
        public int Before { get; }
        public int After { get; }

        internal DieDisturbedEvent(EncounterState state, string ruleId, int bay, DieInterferenceKind kind, InterferenceTrigger trigger,
            int before, int after)
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
        public string RuleId { get; }
        public int Bay { get; }
        public DieInterferenceKind Kind { get; }
        /** The committed action the blocked disturbance followed. */
        public InterferenceTrigger Trigger { get; }
        public string ProtectingRuleId { get; }

        internal DisturbanceBlockedEvent(EncounterState state, string ruleId, int bay, DieInterferenceKind kind,
            InterferenceTrigger trigger, string protectingRuleId)
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

    /** The dice an interference rule disturbed during the current hand. */
    [Serializable]
    public sealed class DieInterferenceSaveData
    {
        public int DisturbedMask;
        public int LatestDisturbedMask;
    }

    /** An interference rule's snapshot: the dice it disturbed during the current hand and the one it disturbed last. */
    public sealed class DieInterferenceRuleSnapshot : EncounterRuleSnapshot
    {
        /** Dice changed by interference during this hand; they do not count as rerolled or changed for charms. */
        public int DisturbedMask { get; }
        /** The die disturbed by the latest committed action, if any. */
        public int LatestDisturbedMask { get; }

        public DieInterferenceRuleSnapshot(string id, bool isAvailable, int disturbedMask, int latestDisturbedMask) : base(id, isAvailable)
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

    internal sealed class DieInterferenceState : EncounterRuleState
    {
        internal int DisturbedMask;
        internal int LatestDisturbedMask;

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

        public DieInterferenceRuleDefinition(string id, int chancePercent, int forcedRerollWeight, int flipWeight,
            bool afterPlayerReroll, bool afterPlayHand) : base(id)
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
        public override Type SaveDataType => typeof(DieInterferenceSaveData);
        internal override EncounterRuleState CreateState() => new DieInterferenceState();

        internal override EncounterRuleSnapshot CreateSnapshot(EncounterState state, EncounterRuleState ruleState)
        {
            var value = (DieInterferenceState)ruleState;
            return new DieInterferenceRuleSnapshot(Id, ShowsAvailable(state), value.DisturbedMask, value.LatestDisturbedMask);
        }

        internal override object CaptureState(EncounterRuleState state)
        {
            var value = (DieInterferenceState)state;
            return new DieInterferenceSaveData { DisturbedMask = value.DisturbedMask, LatestDisturbedMask = value.LatestDisturbedMask };
        }

        internal override bool TryRestoreState(EncounterState restored, object saved, bool postPlay, out EncounterRuleState state,
            out string detail)
        {
            var source = (DieInterferenceSaveData)saved;
            state = null;
            int disturbed = source.DisturbedMask;
            int latest = source.LatestDisturbedMask;
            if ((disturbed & ~DiceValues.AllMask) != 0 || (latest & ~disturbed) != 0 || (latest & (latest - 1)) != 0)
                detail = "Disturbed dice masks are inconsistent.";
            // Before a hand is played, the latest disturbance followed a reroll and could only target a kept die.
            else if (!postPlay && (latest & restored.LatestRerollMask) != 0)
                detail = "A reroll disturbance must target a kept die.";
            // Each committed action resolves at most one disturbance.
            else if (DiceValues.CountBays(disturbed) > (long)restored.RerollsSpentOnHand + (postPlay ? 1 : 0))
                detail = "Disturbed dice exceed the hand's committed actions.";
            else
            {
                detail = null;
                state = new DieInterferenceState { DisturbedMask = disturbed, LatestDisturbedMask = latest };
            }
            return state != null;
        }

        internal override void HandStarted(EncounterState state, EncounterRuleState ruleState, List<RuleEvent> events)
        {
            var value = (DieInterferenceState)ruleState;
            value.DisturbedMask = 0;
            value.LatestDisturbedMask = 0;
        }

        internal override int DiceChangedOutsideRerollsMask(EncounterRuleState ruleState)
            => ((DieInterferenceState)ruleState).DisturbedMask;

        internal override bool Interfere(EncounterDefinition encounter, EncounterState state, EncounterRuleState ruleState,
            InterferenceTrigger trigger, int eligibleMask, List<RuleEvent> events)
        {
            var disturbance = (DieInterferenceState)ruleState;
            disturbance.LatestDisturbedMask = 0;
            if (trigger == InterferenceTrigger.PlayerReroll ? !AfterPlayerReroll : !AfterPlayHand) return false;
            if (eligibleMask == 0 || state.RuleRandom.NextInt(100) >= ChancePercent) return false;
            int bay = DiceValues.NthBay(eligibleMask, state.RuleRandom.NextInt(DiceValues.CountBays(eligibleMask)));
            var kind = state.RuleRandom.NextInt(ForcedRerollWeight + FlipWeight) < ForcedRerollWeight
                ? DieInterferenceKind.ForcedReroll : DieInterferenceKind.Flip;
            var protector = EncounterRuleResolver.Protector(encounter, state, bay, kind);
            if (protector != null)
            {
                events.Add(new DisturbanceBlockedEvent(state, Id, bay, kind, trigger, protector.Id));
                return true;
            }
            int before = state.CurrentDice[bay];
            int after = kind == DieInterferenceKind.Flip ? DiceValues.OppositeFace(before) : DiceValues.RollFace(ref state.RuleRandom);
            state.CurrentDice = state.CurrentDice.With(bay, after);
            disturbance.DisturbedMask |= 1 << bay;
            disturbance.LatestDisturbedMask = 1 << bay;
            events.Add(new DieDisturbedEvent(state, Id, bay, kind, trigger, before, after));
            return true;
        }
    }

    /** Protects the bay occupied by a positioned table piece against the named interference effects. */
    public sealed class BayWardRuleDefinition : EncounterRuleDefinition
    {
        private readonly HashSet<DieInterferenceKind> blocked;
        public string PieceId { get; }
        public string InteractionId { get; }

        public BayWardRuleDefinition(string id, string pieceId, string interactionId, IEnumerable<DieInterferenceKind> blockedKinds)
            : base(id)
        {
            DefinitionValidation.RequireId(pieceId, nameof(pieceId));
            DefinitionValidation.RequireId(interactionId, nameof(interactionId));
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
            InteractionId = interactionId;
        }

        public bool Blocks(DieInterferenceKind kind) => blocked.Contains(kind);

        internal override bool IsAvailable(EncounterState state) => true;

        internal override void ValidateDefinition(EncounterDefinition encounter)
        {
            if (!TablePieceStates.TryFind(encounter, PieceId, InteractionId, out _, out var interaction) ||
                !(interaction is PositionInteractionDefinition))
                throw new ArgumentException("A ward must reference a positioned table piece.", nameof(encounter));
        }

        internal override bool ProtectsBay(EncounterDefinition encounter, EncounterState state, int bay, DieInterferenceKind kind)
        {
            if (!blocked.Contains(kind) || !TablePieceStates.TryFind(encounter, PieceId, InteractionId, out int slot, out _))
                return false;
            var position = ((PiecePositionState)state.PieceStates[slot]).Position;
            return position.Kind == PiecePositionKind.DiceBay && position.Index == bay;
        }
    }
}
