using System;
using Dice.Engine;

namespace Dice.Presentation
{
    /**
     * One kind of presentation of an item's table piece where the scene gives the piece no prop, referenced by the item's
     * presentation. Each kind creates its own runtime presenter, which binds what it shows to the state of the piece's
     * interaction of that kind, so a new kind needs no change to the encounter screen.
     */
    public abstract class ItemPiecePresenterAsset : PieceInteractionKindAsset
    {
        /**
         * Fails, naming the presentation and this asset, when the asset's own settings cannot present a piece; it runs when
         * the item presentation loads. A kind with no settings of its own has nothing to check.
         */
        public virtual void Validate(ItemPresentationAsset presentation) { }

        /**
         * Creates an independent presenter showing the item's piece through the piece's interaction of the kind this asset
         * presents. Fails when the piece has no interaction of that kind.
         */
        public abstract PieceInteractionPresenter Create(ItemPresentationAsset item, TablePieceDefinition piece,
            PieceInteractionPresenterContext context);
    }

    /** An item piece presenter asset for one kind of piece interaction definition; a piece without one fails at creation. */
    public abstract class ItemPiecePresenterAsset<TInteraction> : ItemPiecePresenterAsset
        where TInteraction : PieceInteractionDefinition
    {
        public sealed override Type InteractionKind => typeof(TInteraction);

        public sealed override int InteractionIndex(TablePieceDefinition definition) => definition.IndexOf<TInteraction>();

        public sealed override PieceInteractionPresenter Create(ItemPresentationAsset item, TablePieceDefinition piece,
            PieceInteractionPresenterContext context)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            if (context == null) throw new ArgumentNullException(nameof(context));
            int index = RequireInteractionIndex(piece, "the item of the item presentation " + item.name + " gives its table piece " +
                piece.Identity);
            return Create(item, piece, (TInteraction)piece.Interactions[index], index, context);
        }

        /** Creates the presenter showing the item's piece through its interaction of this kind, at its place in the piece's list. */
        protected abstract PieceInteractionPresenter Create(ItemPresentationAsset item, TablePieceDefinition piece,
            TInteraction interaction, int interactionIndex, PieceInteractionPresenterContext context);
    }
}
