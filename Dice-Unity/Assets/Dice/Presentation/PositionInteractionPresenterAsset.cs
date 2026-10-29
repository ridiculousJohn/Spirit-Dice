using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * The position interaction's presentation: the prop's SpiritTablePiecePosition rests at the engine's position, on the
     * neutral spot or above a dice bay, and a drag released near an allowed position asks the engine to move the piece there.
     */
    [CreateAssetMenu(menuName = "Dice/Presentation/Piece Interaction Presenter/Position")]
    public sealed class PositionInteractionPresenterAsset : PieceInteractionPresenterAsset<PositionInteractionDefinition>
    {
        protected override PieceInteractionPresenter Create(SpiritTablePieceView piece, PositionInteractionDefinition interaction,
            int interactionIndex, PieceInteractionPresenterContext context)
        {
            // The position behaviour is authored on the prop beside its shared view, where its scene references live.
            var placement = piece.GetComponent<SpiritTablePiecePosition>();
            if (placement == null)
                throw new InvalidOperationException("The table piece " + piece.name +
                    " presents a position interaction but has no SpiritTablePiecePosition.");
            return new PositionInteractionPresenter(piece, placement, interaction, interactionIndex, context);
        }

        private sealed class PositionInteractionPresenter : PieceInteractionPresenter
        {
            private readonly SpiritTablePieceView piece;
            private readonly SpiritTablePiecePosition placement;
            private readonly PositionInteractionDefinition definition;
            private readonly int interactionIndex;
            private readonly PieceInteractionPresenterContext context;
            private readonly Vector3[] bayPoints = new Vector3[DiceValues.Count];

            internal PositionInteractionPresenter(SpiritTablePieceView piece, SpiritTablePiecePosition placement,
                PositionInteractionDefinition definition, int interactionIndex, PieceInteractionPresenterContext context)
            {
                this.piece = piece;
                this.placement = placement;
                this.definition = definition;
                this.interactionIndex = interactionIndex;
                this.context = context;
                placement.Initialize(CanMove, RequestMove);
            }

            public override void Bind(PieceInteractionSnapshot interaction)
            {
                if (interaction == null) { placement.Unbind(); return; }
                if (!(interaction.State is PiecePositionState state))
                    throw new InvalidOperationException("The position interaction of the table piece " + piece.Piece.name + " on " +
                        piece.name + " has a " + interaction.State?.GetType().Name + " state.");
                placement.Bind(state.Position, definition);
            }

            /** Bay positions follow the tray's resting dice slots at the prop's authored bay placement. */
            public override void Layout()
            {
                for (int bay = 0; bay < DiceValues.Count; ++bay) bayPoints[bay] = context.BayPoint(bay, placement.BayPlacement);
                placement.Layout(bayPoints);
            }

            public override void Dispose() => placement.Unbind();

            private bool CanMove() => context.CanInteract(piece.Piece.Identity, interactionIndex);

            private bool RequestMove(PiecePosition position)
                => context.Interact(piece.Piece.Identity, interactionIndex, new MovePieceInput(position));
        }
    }
}
