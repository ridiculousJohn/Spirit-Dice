using System;
using System.Collections.Generic;
using Dice.Engine;

namespace Dice.Presentation
{
    /**
     * The presenters of an encounter screen's table piece props: one per prop whose interaction the encounter has, created
     * by the prop's presenter asset for that interaction's definition and disposed with the encounter screen's presentation
     * of the encounter. It knows no interaction or presenter kind.
     */
    public sealed class TablePiecePresenters : IDisposable
    {
        private readonly List<SpiritTablePieceView> pieces;
        private readonly List<PieceInteractionPresenter> presenters;

        private TablePiecePresenters(List<SpiritTablePieceView> pieces, List<PieceInteractionPresenter> presenters)
        {
            this.pieces = pieces;
            this.presenters = presenters;
        }

        public IReadOnlyList<PieceInteractionPresenter> Presenters => presenters;

        /**
         * Creates the presenters of the props whose interaction the encounter has. Fails when a prop is missing, lacks its
         * IDs or references no presenter, or when a presenter rejects its interaction.
         */
        public static TablePiecePresenters Create(IReadOnlyList<SpiritTablePieceView> props, EncounterDefinition encounter,
            PieceInteractionPresenterContext context)
        {
            if (props == null) throw new ArgumentNullException(nameof(props));
            if (encounter == null) throw new ArgumentNullException(nameof(encounter));
            if (context == null) throw new ArgumentNullException(nameof(context));
            var pieces = new List<SpiritTablePieceView>();
            var presenters = new List<PieceInteractionPresenter>();
            try
            {
                foreach (var piece in props)
                {
                    if (piece == null) throw new InvalidOperationException("An authored table piece is missing.");
                    if (string.IsNullOrWhiteSpace(piece.PieceId) || string.IsNullOrWhiteSpace(piece.InteractionId))
                        throw new InvalidOperationException("The table piece " + piece.name + " requires authored piece and interaction IDs.");
                    if (piece.Presenter == null)
                        throw new InvalidOperationException("The table piece " + piece.name + " references no interaction presenter.");
                    var interaction = Definition(encounter, piece);
                    // Encounters sharing a table scene need not all place its pieces; only a presenter makes a prop usable, so a
                    // prop the encounter lacks stays in place and unusable.
                    if (interaction == null) continue;
                    presenters.Add(piece.Presenter.Create(piece, interaction, context));
                    pieces.Add(piece);
                }
            }
            catch
            {
                foreach (var created in presenters) created.Dispose();
                throw;
            }
            return new TablePiecePresenters(pieces, presenters);
        }

        /** Binds every presenter to its interaction in the snapshot. */
        public void Bind(EncounterSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            for (int i = 0; i < presenters.Count; ++i) presenters[i].Bind(Interaction(snapshot, pieces[i]));
        }

        public void Layout()
        {
            foreach (var presenter in presenters) presenter.Layout();
        }

        public void Dispose()
        {
            foreach (var presenter in presenters) presenter.Dispose();
            presenters.Clear();
            pieces.Clear();
        }

        private static PieceInteractionDefinition Definition(EncounterDefinition encounter, SpiritTablePieceView piece)
        {
            foreach (var candidate in encounter.Pieces)
            {
                if (!string.Equals(candidate.Id, piece.PieceId, StringComparison.Ordinal)) continue;
                foreach (var interaction in candidate.Interactions)
                    if (string.Equals(interaction.Id, piece.InteractionId, StringComparison.Ordinal)) return interaction;
            }
            return null;
        }

        private static PieceInteractionSnapshot Interaction(EncounterSnapshot snapshot, SpiritTablePieceView piece)
        {
            foreach (var interaction in snapshot.PieceInteractions)
                if (string.Equals(interaction.PieceId, piece.PieceId, StringComparison.Ordinal) &&
                    string.Equals(interaction.InteractionId, piece.InteractionId, StringComparison.Ordinal)) return interaction;
            return null;
        }
    }
}
