using System;
using Dice.Engine;

namespace Dice.Presentation
{
    /**
     * What every presenter of a position interaction shares, whatever shows the piece: it binds the piece's drag to the
     * interaction's state in each snapshot, lets the player move it when the context allows, and proposes the drag's moves
     * to the engine.
     */
    internal sealed class PositionInteractionBinding
    {
        private readonly SpiritPositionDrag drag;
        private readonly PositionInteractionDefinition definition;
        private readonly TablePieceDefinition.Id pieceId;
        private readonly int interactionIndex;
        private readonly PieceInteractionPresenterContext context;
        // Names the piece and what shows it in failures, such as "Candle on Merchant Candle".
        private readonly string shownBy;

        internal PositionInteractionBinding(SpiritPositionDrag drag, PositionInteractionDefinition definition,
            TablePieceDefinition.Id pieceId, int interactionIndex, PieceInteractionPresenterContext context, string shownBy)
        {
            this.drag = drag ?? throw new ArgumentNullException(nameof(drag));
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.pieceId = pieceId;
            this.interactionIndex = interactionIndex;
            this.shownBy = shownBy;
            drag.Initialize(CanMove, RequestMove);
        }

        /**
         * Binds the drag to the interaction's position and returns it; a snapshot without the interaction leaves the drag in
         * place and unusable and returns null.
         */
        internal PiecePosition? Bind(PieceInteractionSnapshot interaction)
        {
            if (interaction == null)
            {
                drag.Unbind();
                return null;
            }
            if (!(interaction.State is PiecePositionState state))
                throw new InvalidOperationException("The position interaction of the table piece " + shownBy + " has a " +
                    interaction.State?.GetType().Name + " state.");
            drag.Bind(state.Position, definition);
            return state.Position;
        }

        /** Leaves the drag in place and unusable; the binding is not used afterwards. */
        internal void Unbind() => drag.Unbind();

        private bool CanMove() => context.CanInteract(pieceId, interactionIndex);

        private bool RequestMove(PiecePosition position) => context.Interact(pieceId, interactionIndex, new MovePieceInput(position));
    }
}
