using System;
using System.Collections.Generic;
using Dice.Engine;

namespace Dice.Presentation
{
    /**
     * The presenters of an encounter screen's table piece props: one per prop whose piece the encounter places, created by
     * the prop's presenter asset for the piece's interaction of the asset's kind and disposed with the encounter screen's
     * presentation of the encounter. It knows no interaction or presenter kind.
     */
    public sealed class TablePiecePresenters : IDisposable
    {
        private readonly List<SpiritTablePieceView> pieces;
        // Each presenter's interaction, by its place in its piece's interaction list.
        private readonly List<int> interactionIndices;
        private readonly List<PieceInteractionPresenter> presenters;

        private TablePiecePresenters(List<SpiritTablePieceView> pieces, List<int> interactionIndices,
            List<PieceInteractionPresenter> presenters)
        {
            this.pieces = pieces;
            this.interactionIndices = interactionIndices;
            this.presenters = presenters;
        }

        public IReadOnlyList<PieceInteractionPresenter> Presenters => presenters;

        /**
         * Creates the presenters of the props whose piece the encounter places. Fails when a prop is missing, lacks its
         * piece or references no presenter, or when the encounter gives the piece no interaction of the presenter's kind.
         */
        public static TablePiecePresenters Create(IReadOnlyList<SpiritTablePieceView> props, EncounterDefinition encounter,
            PieceInteractionPresenterContext context)
        {
            if (props == null) throw new ArgumentNullException(nameof(props));
            if (encounter == null) throw new ArgumentNullException(nameof(encounter));
            if (context == null) throw new ArgumentNullException(nameof(context));
            var pieces = new List<SpiritTablePieceView>();
            var interactionIndices = new List<int>();
            var presenters = new List<PieceInteractionPresenter>();
            try
            {
                foreach (var piece in props)
                {
                    if (piece == null) throw new InvalidOperationException("An authored table piece is missing.");
                    if (piece.Piece == null)
                        throw new InvalidOperationException("The table piece " + piece.name + " references no table piece asset.");
                    if (piece.Presenter == null)
                        throw new InvalidOperationException("The table piece " + piece.name + " references no interaction presenter.");
                    var definition = Definition(encounter, piece);
                    // Encounters sharing a table scene need not all place its pieces; only a presenter makes a prop usable, so a
                    // prop the encounter lacks stays in place and unusable.
                    if (definition == null) continue;
                    presenters.Add(piece.Presenter.Create(piece, definition, context));
                    interactionIndices.Add(piece.Presenter.InteractionIndex(definition));
                    pieces.Add(piece);
                }
            }
            catch
            {
                foreach (var created in presenters) created.Dispose();
                throw;
            }
            return new TablePiecePresenters(pieces, interactionIndices, presenters);
        }

        /** Binds every presenter to its interaction in the snapshot. */
        public void Bind(EncounterSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            for (int i = 0; i < presenters.Count; ++i) presenters[i].Bind(Interaction(snapshot, pieces[i], interactionIndices[i]));
        }

        public void Layout()
        {
            foreach (var presenter in presenters) presenter.Layout();
        }

        public void Dispose()
        {
            foreach (var presenter in presenters) presenter.Dispose();
            presenters.Clear();
            interactionIndices.Clear();
            pieces.Clear();
        }

        private static TablePieceDefinition Definition(EncounterDefinition encounter, SpiritTablePieceView piece)
        {
            foreach (var candidate in encounter.Pieces)
                if (candidate.Identity == piece.Piece.Identity) return candidate;
            return null;
        }

        private static PieceInteractionSnapshot Interaction(EncounterSnapshot snapshot, SpiritTablePieceView piece, int interactionIndex)
        {
            foreach (var interaction in snapshot.PieceInteractions)
                if (interaction.PieceId == piece.Piece.Identity && interaction.InteractionIndex == interactionIndex) return interaction;
            return null;
        }
    }
}
