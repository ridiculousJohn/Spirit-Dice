using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * An asset that presents one kind of table piece interaction, whatever shows the piece: the shared base of the kinds a
     * scene prop references and the kinds an item presentation references. It finds the piece's interaction of its kind.
     */
    public abstract class PieceInteractionKindAsset : ScriptableObject
    {
        /** The interaction definition type this asset presents, named in failures. */
        public abstract Type InteractionKind { get; }

        /** The place in the piece's interaction list of its interaction of the kind this asset presents, or -1 without one. */
        public abstract int InteractionIndex(TablePieceDefinition definition);

        /**
         * The place of the piece's interaction of this asset's kind; fails, naming this asset, when it has none.
         * givenNone completes the failure from what places the piece, such as "the encounter gives the table piece X".
         */
        protected int RequireInteractionIndex(TablePieceDefinition definition, string givenNone)
        {
            int index = InteractionIndex(definition);
            if (index < 0)
                throw new InvalidOperationException("The piece interaction presenter " + name + " presents " + InteractionKind.Name +
                    " interactions, but " + givenNone + " none.");
            return index;
        }
    }

    /**
     * One kind of table piece interaction presentation, referenced by the piece's authored prop. Each kind creates its own
     * runtime presenter, which binds the prop to the state and input of the piece's interaction of that kind, so a new kind
     * needs no change to the encounter screen.
     */
    public abstract class PieceInteractionPresenterAsset : PieceInteractionKindAsset
    {
        /**
         * Creates an independent presenter binding the prop to the piece's interaction of the kind this asset presents.
         * Fails when the piece is not the prop's or has no interaction of that kind.
         */
        public abstract PieceInteractionPresenter Create(SpiritTablePieceView piece, TablePieceDefinition definition,
            PieceInteractionPresenterContext context);
    }

    /** A presenter asset for one kind of piece interaction definition; a piece without one fails at creation. */
    public abstract class PieceInteractionPresenterAsset<TInteraction> : PieceInteractionPresenterAsset
        where TInteraction : PieceInteractionDefinition
    {
        public sealed override Type InteractionKind => typeof(TInteraction);

        public sealed override int InteractionIndex(TablePieceDefinition definition) => definition.IndexOf<TInteraction>();

        public sealed override PieceInteractionPresenter Create(SpiritTablePieceView piece, TablePieceDefinition definition,
            PieceInteractionPresenterContext context)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (piece.Piece == null || piece.Piece.Identity != definition.Identity)
                throw new InvalidOperationException("The table piece " + piece.name + " does not present the piece " +
                    definition.Identity + ".");
            int index = RequireInteractionIndex(definition, "the encounter gives the table piece " + piece.Piece.name);
            return Create(piece, (TInteraction)definition.Interactions[index], index, context);
        }

        /** Creates the presenter binding the prop to the piece's interaction of this kind, at its place in the piece's list. */
        protected abstract PieceInteractionPresenter Create(SpiritTablePieceView piece, TInteraction interaction, int interactionIndex,
            PieceInteractionPresenterContext context);
    }

    /**
     * What piece interaction presentation may use from the encounter table, nothing kind-specific: whether the player may
     * use a piece's interaction now, submitting an input to it, points on the tray's dice bays, marks on them, and where an
     * item's icon rests.
     */
    public abstract class PieceInteractionPresenterContext
    {
        /** Whether the game accepts input during a hand and the piece's interaction is available. */
        public abstract bool CanInteract(TablePieceDefinition.Id pieceId, int interactionIndex);

        /**
         * Submits the input to the piece's interaction when the player may use it now; true when submitted. The resulting
         * snapshot binds the interaction's new or unchanged state.
         */
        public abstract bool Interact(TablePieceDefinition.Id pieceId, int interactionIndex, PieceInteractionInput input);

        /**
         * The world point at a placement within the bay's resting tray slot, normalized within the slot (0,0 is
         * bottom-left); valid during the presenters' layout.
         */
        public abstract Vector3 BayPoint(int bay, Vector2 placement);

        /** The world point over the bay's label anchor, where the tray geometry places the bay's emblems; valid during the presenters' layout. */
        public abstract Vector3 BayLabelPoint(int bay);

        /** The world point over a point of the tray, given as a viewport point of the tray's camera, such as a shown emblem's. */
        public abstract Vector3 TrayPoint(Vector2 trayViewportPoint);

        /**
         * Where the item's icon rests: the item slot the table assigns to the item in play while the presenters exist. Fails,
         * naming the item and the table, for an item that has none.
         */
        public abstract SpiritItemSlot ItemSlot(ItemDefinition.Id item);

        /**
         * A mark an item puts on a dice bay, made from the prefab and showing the item's icon and glow colour; it is hidden
         * until shown on a bay, and the tray destroys it when it is released or the tray releases its presentation.
         */
        public abstract BayIndicator CreateBayIndicator(SpiritBayIndicator prefab, Sprite icon, Color glowColor);
    }

    /**
     * A mark an item puts on a dice bay: shown on one bay at a time or hidden, until released. Releasing one the tray has
     * already released with its presentation does nothing.
     */
    public abstract class BayIndicator
    {
        /** Shows the mark on the bay, moving it from the bay it was shown on. */
        public abstract void ShowOn(int bay);

        public abstract void Hide();

        /**
         * Whether the mark's emblem shows while the mark is shown, true by default; the glow stays either way, as when what
         * the emblem stands for is in the player's hand.
         */
        public abstract void ShowEmblem(bool shown);

        /** The point of the mark's own emblem as the tray renders it, a viewport point of the tray's camera; only while shown. */
        public abstract Vector2 EmblemViewportPoint { get; }

        /** Destroys the mark; it is not used afterwards. */
        public abstract void Release();
    }

    /**
     * A piece interaction's presentation for the lifetime of the encounter screen's presentation of the encounter: it binds
     * what shows the piece (a prop, or an item's slot and bay indicator) to the interaction's state in each snapshot and
     * turns the player's handling of it into input.
     */
    public abstract class PieceInteractionPresenter : IDisposable
    {
        /**
         * Shows the interaction's state; null when the snapshot has no such interaction, which leaves the prop in place and
         * unusable.
         */
        public abstract void Bind(PieceInteractionSnapshot interaction);

        /**
         * Places what shows the piece at its bay points; runs when presenters are created, after they are bound to a snapshot,
         * when the screen fits and when a roll or flip ends.
         */
        public virtual void Layout() { }

        /** Leaves the prop in place and unusable; the presenter is not used afterwards. */
        public abstract void Dispose();
    }
}
