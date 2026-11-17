using System;
using System.Collections.Generic;
using Dice.Engine;

namespace Dice.Presentation
{
    /**
     * The presenters of an encounter screen's table pieces: one per prop whose piece the encounter places, created by the
     * prop's presenter asset for the piece's interaction of the asset's kind, and one per item in play whose piece has no
     * prop, created by the item presentation's piece presenter; all are disposed with the encounter screen's
     * presentation of the encounter. It knows no interaction or presenter kind.
     */
    public sealed class TablePiecePresenters : IDisposable
    {
        // Each presenter's piece, and its interaction by its place in the piece's interaction list.
        private readonly List<TablePieceDefinition.Id> pieceIds;
        private readonly List<int> interactionIndices;
        private readonly List<PieceInteractionPresenter> presenters;

        private TablePiecePresenters(List<TablePieceDefinition.Id> pieceIds, List<int> interactionIndices,
            List<PieceInteractionPresenter> presenters)
        {
            this.pieceIds = pieceIds;
            this.interactionIndices = interactionIndices;
            this.presenters = presenters;
        }

        public IReadOnlyList<PieceInteractionPresenter> Presenters => presenters;

        /**
         * Creates the presenters of the props whose piece is installed in the run, then of the items in play whose piece has
         * no prop, through each item's presentation. Fails when a prop is missing, lacks its piece or references no
         * presenter, when what places the piece gives it no interaction of the presenter's kind, or when an item in play has
         * no presentation.
         */
        public static TablePiecePresenters Create(IReadOnlyList<SpiritTablePieceView> props,
            IReadOnlyList<TablePieceDefinition> installedPieces, IReadOnlyList<ItemDefinition> items,
            ItemPresentationCatalog itemPresentations, PieceInteractionPresenterContext context)
        {
            if (props == null) throw new ArgumentNullException(nameof(props));
            if (installedPieces == null) throw new ArgumentNullException(nameof(installedPieces));
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (itemPresentations == null) throw new ArgumentNullException(nameof(itemPresentations));
            if (context == null) throw new ArgumentNullException(nameof(context));
            var pieceIds = new List<TablePieceDefinition.Id>();
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
                    var definition = Definition(installedPieces, piece.Piece.Identity);
                    // Encounters sharing a table scene need not all place its pieces; only a presenter makes a prop usable, so a
                    // prop the encounter lacks stays in place and unusable.
                    if (definition == null) continue;
                    presenters.Add(piece.Presenter.Create(piece, definition, context));
                    interactionIndices.Add(piece.Presenter.InteractionIndex(definition));
                    pieceIds.Add(definition.Identity);
                }
                // An item in play has its piece installed, and its presentation was validated at load to have a piece
                // presenter for it.
                foreach (var item in items)
                {
                    // A scene's own prop always presents its piece.
                    if (item.Piece == null || SpiritTablePieceView.Find(props, item.Piece.Identity) != null) continue;
                    var presentation = itemPresentations.PresentationOf(item);
                    presenters.Add(presentation.PiecePresenter.Create(presentation, item.Piece, context));
                    interactionIndices.Add(presentation.PiecePresenter.InteractionIndex(item.Piece));
                    pieceIds.Add(item.Piece.Identity);
                }
            }
            catch
            {
                foreach (var created in presenters) created.Dispose();
                throw;
            }
            return new TablePiecePresenters(pieceIds, interactionIndices, presenters);
        }

        /** Binds every presenter to its interaction in the snapshot. */
        public void Bind(EncounterSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            for (int i = 0; i < presenters.Count; ++i) presenters[i].Bind(Interaction(snapshot, pieceIds[i], interactionIndices[i]));
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
            pieceIds.Clear();
        }

        private static TablePieceDefinition Definition(IReadOnlyList<TablePieceDefinition> installedPieces, TablePieceDefinition.Id pieceId)
        {
            foreach (var candidate in installedPieces)
                if (candidate.Identity == pieceId) return candidate;
            return null;
        }

        private static PieceInteractionSnapshot Interaction(EncounterSnapshot snapshot, TablePieceDefinition.Id pieceId, int interactionIndex)
        {
            foreach (var interaction in snapshot.PieceInteractions)
                if (interaction.PieceId == pieceId && interaction.InteractionIndex == interactionIndex) return interaction;
            return null;
        }
    }
}
