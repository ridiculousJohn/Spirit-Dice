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
            private readonly SpiritTablePiecePosition placement;
            private readonly PieceInteractionPresenterContext context;
            private readonly PositionInteractionBinding binding;
            private readonly Vector3[] bayPoints = new Vector3[DiceValues.Count];

            internal PositionInteractionPresenter(SpiritTablePieceView piece, SpiritTablePiecePosition placement,
                PositionInteractionDefinition definition, int interactionIndex, PieceInteractionPresenterContext context)
            {
                this.placement = placement;
                this.context = context;
                binding = new PositionInteractionBinding(placement, definition, piece.Piece.Identity, interactionIndex, context,
                    piece.Piece.name + " on " + piece.name);
            }

            public override void Bind(PieceInteractionSnapshot interaction) => binding.Bind(interaction);

            /** Bay positions follow the tray's resting dice slots at the prop's authored bay placement. */
            public override void Layout()
            {
                for (int bay = 0; bay < DiceValues.Count; ++bay) bayPoints[bay] = context.BayPoint(bay, placement.BayPlacement);
                placement.Layout(bayPoints);
            }

            public override void Dispose() => binding.Unbind();
        }
    }
}
