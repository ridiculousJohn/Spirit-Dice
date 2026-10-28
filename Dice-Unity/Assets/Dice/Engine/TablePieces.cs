using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Dice.Engine
{
    public enum PiecePositionKind { Neutral, DiceBay }
    public enum PieceMovementTiming { DuringHand, UntilFirstReroll }

    public readonly struct PiecePosition : IEquatable<PiecePosition>
    {
        public PiecePositionKind Kind { get; }
        public int Index { get; }

        private PiecePosition(PiecePositionKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        public static PiecePosition Neutral => new PiecePosition(PiecePositionKind.Neutral, 0);

        public static PiecePosition Bay(int index)
        {
            if ((uint)index >= DiceValues.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return new PiecePosition(PiecePositionKind.DiceBay, index);
        }

        public bool Equals(PiecePosition other) => Kind == other.Kind && Index == other.Index;
        public override bool Equals(object obj) => obj is PiecePosition other && Equals(other);
        public override int GetHashCode() => (int)Kind * 31 + Index;
        public static bool operator ==(PiecePosition left, PiecePosition right) => left.Equals(right);
        public static bool operator !=(PiecePosition left, PiecePosition right) => !left.Equals(right);
    }

    /** An object an encounter places on the table; its interactions decide how the player can affect it. */
    public sealed class TablePieceDefinition
    {
        public string Id { get; }
        public ReadOnlyCollection<PieceInteractionDefinition> Interactions { get; }

        public TablePieceDefinition(string id, IEnumerable<PieceInteractionDefinition> interactions = null)
        {
            DefinitionValidation.RequireId(id, nameof(id));
            var list = interactions == null ? new List<PieceInteractionDefinition>() : new List<PieceInteractionDefinition>(interactions);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var interaction in list)
                if (interaction == null || !ids.Add(interaction.Id))
                    throw new ArgumentException("Piece interaction IDs must be unique and nonnull.", nameof(interactions));
            Id = id;
            Interactions = list.AsReadOnly();
        }
    }

    /** A reusable kind of player interaction; each kind validates its input and owns its saved state. */
    public abstract class PieceInteractionDefinition
    {
        public string Id { get; }

        internal PieceInteractionDefinition(string id)
        {
            DefinitionValidation.RequireId(id, nameof(id));
            Id = id;
        }

        internal abstract PieceInteractionState InitialState { get; }
        internal abstract bool IsAvailable(EncounterState state);
        internal abstract bool Accepts(PieceInteractionState current, PieceInteractionInput input);
        internal abstract PieceInteractionState Apply(PieceInteractionState current, PieceInteractionInput input);
        internal abstract int[] Capture(PieceInteractionState state);
        internal abstract bool TryRestore(int[] values, out PieceInteractionState state);
    }

    /** Interaction states are immutable so encounter state copies can share them. */
    public abstract class PieceInteractionState
    {
        internal PieceInteractionState() { }

        /** Developer diagnostics, not player-facing text: the state in the interaction kind's own words. */
        internal abstract string DescribeDiagnostic(IDiagnosticFormat format);
    }

    public abstract class PieceInteractionInput
    {
        internal PieceInteractionInput() { }
    }

    public sealed class PiecePositionState : PieceInteractionState
    {
        public PiecePosition Position { get; }
        internal PiecePositionState(PiecePosition position) => Position = position;

        internal override string DescribeDiagnostic(IDiagnosticFormat format)
            => Position.Kind == PiecePositionKind.DiceBay ? "die " + (Position.Index + 1) : format.Humanize(Position.Kind.ToString());
    }

    public sealed class MovePieceInput : PieceInteractionInput
    {
        public PiecePosition Position { get; }
        public MovePieceInput(PiecePosition position) => Position = position;
    }

    public sealed class PositionInteractionDefinition : PieceInteractionDefinition
    {
        public PiecePosition StartingPosition { get; }
        public PieceMovementTiming Timing { get; }
        public bool AllowsNeutral { get; }
        public int AllowedBayMask { get; }

        public PositionInteractionDefinition(string id, PiecePosition startingPosition, PieceMovementTiming timing,
            bool allowsNeutral = true, int allowedBayMask = DiceValues.AllMask) : base(id)
        {
            if (timing != PieceMovementTiming.DuringHand && timing != PieceMovementTiming.UntilFirstReroll)
                throw new ArgumentOutOfRangeException(nameof(timing));
            if ((allowedBayMask & ~DiceValues.AllMask) != 0) throw new ArgumentOutOfRangeException(nameof(allowedBayMask));
            StartingPosition = startingPosition;
            Timing = timing;
            AllowsNeutral = allowsNeutral;
            AllowedBayMask = allowedBayMask;
            if (!Allows(startingPosition)) throw new ArgumentException("The starting position must be allowed.", nameof(startingPosition));
        }

        public bool Allows(PiecePosition position)
            => position.Kind == PiecePositionKind.Neutral ? AllowsNeutral
                : position.Kind == PiecePositionKind.DiceBay && (uint)position.Index < DiceValues.Count &&
                  (AllowedBayMask & (1 << position.Index)) != 0;

        internal override PieceInteractionState InitialState => new PiecePositionState(StartingPosition);

        internal override bool IsAvailable(EncounterState state)
            => Timing == PieceMovementTiming.DuringHand || state.RerollsSpentOnHand == 0;

        internal override bool Accepts(PieceInteractionState current, PieceInteractionInput input)
            => input is MovePieceInput move && Allows(move.Position) &&
                current is PiecePositionState position && position.Position != move.Position;

        internal override PieceInteractionState Apply(PieceInteractionState current, PieceInteractionInput input)
            => new PiecePositionState(((MovePieceInput)input).Position);

        internal override int[] Capture(PieceInteractionState state)
        {
            var position = ((PiecePositionState)state).Position;
            return new[] { (int)position.Kind, position.Index };
        }

        internal override bool TryRestore(int[] values, out PieceInteractionState state)
        {
            state = null;
            if (values == null || values.Length != 2) return false;
            PiecePosition position;
            if (values[0] == (int)PiecePositionKind.Neutral && values[1] == 0) position = PiecePosition.Neutral;
            else if (values[0] == (int)PiecePositionKind.DiceBay && (uint)values[1] < DiceValues.Count) position = PiecePosition.Bay(values[1]);
            else return false;
            if (!Allows(position)) return false;
            state = new PiecePositionState(position);
            return true;
        }
    }

    public sealed class InteractWithPieceAction : EncounterAction
    {
        public string PieceId { get; }
        public string InteractionId { get; }
        public PieceInteractionInput Input { get; }

        public InteractWithPieceAction(string pieceId, string interactionId, PieceInteractionInput input)
        {
            PieceId = pieceId;
            InteractionId = interactionId;
            Input = input;
        }

        internal override CommandRejection Validate(EncounterSession session)
        {
            EncounterState state = session.State;
            if (state.Phase != EncounterPhase.Playing) return CommandRejection.InvalidPhase;
            if (!TablePieceStates.TryFind(session.Definition, PieceId, InteractionId, out int slot, out var piece))
                return CommandRejection.InvalidPiece;
            if (!piece.IsAvailable(state)) return CommandRejection.PieceUnavailable;
            return piece.Accepts(state.PieceStates[slot], Input) ? CommandRejection.None : CommandRejection.InvalidPieceInput;
        }

        internal override void Resolve(EncounterSession session, EncounterState next, List<RuleEvent> events)
            => session.InteractWithPiece(next, this, events);
    }

    public sealed class PieceInteractionSnapshot
    {
        public string PieceId { get; }
        public string InteractionId { get; }
        public bool IsAvailable { get; }
        public PieceInteractionState State { get; }

        internal PieceInteractionSnapshot(string pieceId, string interactionId, bool isAvailable, PieceInteractionState state)
        {
            PieceId = pieceId;
            InteractionId = interactionId;
            IsAvailable = isAvailable;
            State = state;
        }
    }

    public sealed class PieceInteractionChangedEvent : RuleEvent
    {
        public string PieceId { get; }
        public string InteractionId { get; }
        public PieceInteractionState Before { get; }
        public PieceInteractionState After { get; }

        internal PieceInteractionChangedEvent(EncounterState state, string pieceId, string interactionId,
            PieceInteractionState before, PieceInteractionState after) : base(state.StageIndex, state.HandIndex)
        {
            PieceId = pieceId;
            InteractionId = interactionId;
            Before = before;
            After = after;
        }

        protected override string DescribeDetail(IDiagnosticFormat format)
            => PieceId + " " + InteractionId + ": " + Before.DescribeDiagnostic(format) + " -> " + After.DescribeDiagnostic(format) + ".";
    }

    /** Interaction states are stored in definition order: each piece's interactions, piece by piece. */
    internal static class TablePieceStates
    {
        internal static PieceInteractionState[] Initial(EncounterDefinition definition)
        {
            var states = new List<PieceInteractionState>();
            foreach (var piece in definition.Pieces)
                foreach (var interaction in piece.Interactions) states.Add(interaction.InitialState);
            return states.ToArray();
        }

        internal static bool TryFind(EncounterDefinition definition, string pieceId, string interactionId,
            out int slot, out PieceInteractionDefinition interaction)
        {
            slot = 0;
            foreach (var piece in definition.Pieces)
                foreach (var candidate in piece.Interactions)
                {
                    if (string.Equals(piece.Id, pieceId, StringComparison.Ordinal) &&
                        string.Equals(candidate.Id, interactionId, StringComparison.Ordinal))
                    {
                        interaction = candidate;
                        return true;
                    }
                    ++slot;
                }
            interaction = null;
            return false;
        }

        internal static ReadOnlyCollection<PieceInteractionSnapshot> Capture(EncounterDefinition definition, EncounterState state)
        {
            var values = new List<PieceInteractionSnapshot>();
            int slot = 0;
            bool playing = state.Phase == EncounterPhase.Playing;
            foreach (var piece in definition.Pieces)
                foreach (var interaction in piece.Interactions)
                {
                    values.Add(new PieceInteractionSnapshot(piece.Id, interaction.Id,
                        playing && interaction.IsAvailable(state), state.PieceStates[slot]));
                    ++slot;
                }
            return values.AsReadOnly();
        }
    }
}
