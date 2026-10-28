using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * One kind of table piece interaction presentation, referenced by the piece's authored prop. Each kind creates its own
     * runtime presenter, which binds the prop to its interaction's state and input, so a new kind needs no change to the
     * encounter screen.
     */
    public abstract class PieceInteractionPresenterAsset : ScriptableObject
    {
        /**
         * Creates an independent presenter binding the prop to the interaction. Fails when the interaction is not the prop's
         * or not the kind this asset presents.
         */
        public abstract PieceInteractionPresenter Create(SpiritTablePieceView piece, PieceInteractionDefinition interaction,
            PieceInteractionPresenterContext context);
    }

    /** A presenter asset for one kind of piece interaction definition; any other kind of interaction fails at creation. */
    public abstract class PieceInteractionPresenterAsset<TInteraction> : PieceInteractionPresenterAsset
        where TInteraction : PieceInteractionDefinition
    {
        public sealed override PieceInteractionPresenter Create(SpiritTablePieceView piece, PieceInteractionDefinition interaction,
            PieceInteractionPresenterContext context)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            if (interaction == null) throw new ArgumentNullException(nameof(interaction));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!string.Equals(piece.InteractionId, interaction.Id, StringComparison.Ordinal))
                throw new InvalidOperationException("The table piece " + piece.name + " presents interaction " + piece.InteractionId +
                    ", not " + interaction.Id + ".");
            if (!(interaction is TInteraction typed))
                throw new InvalidOperationException("The piece interaction presenter " + name + " presents " + typeof(TInteraction).Name +
                    " interactions, but interaction " + interaction.Id + " of piece " + piece.PieceId + " is a " +
                    interaction.GetType().Name + ".");
            return Create(piece, typed, context);
        }

        /** Creates the presenter binding the prop to one interaction of this kind. */
        protected abstract PieceInteractionPresenter Create(SpiritTablePieceView piece, TInteraction interaction,
            PieceInteractionPresenterContext context);
    }

    /**
     * What piece interaction presentation may use from the encounter table, nothing kind-specific: whether the player may
     * use a piece's interaction now, submitting an input to it, and points on the tray's dice bays.
     */
    public abstract class PieceInteractionPresenterContext
    {
        /** Whether the game accepts input during a hand and the piece's interaction is available. */
        public abstract bool CanInteract(string pieceId, string interactionId);

        /**
         * Submits the input to the piece's interaction when the player may use it now; true when submitted. The resulting
         * snapshot binds the interaction's new or unchanged state.
         */
        public abstract bool Interact(string pieceId, string interactionId, PieceInteractionInput input);

        /**
         * The world point at a placement within the bay's resting tray slot, normalized within the slot (0,0 is
         * bottom-left); valid during the presenters' layout.
         */
        public abstract Vector3 BayPoint(int bay, Vector2 placement);
    }

    /**
     * A piece interaction's presentation for the lifetime of the encounter screen's presentation of the encounter: it binds
     * its prop to the interaction's state in each snapshot and turns the player's handling of the prop into input.
     */
    public abstract class PieceInteractionPresenter : IDisposable
    {
        /**
         * Shows the interaction's state; null when the snapshot has no such interaction, which leaves the prop in place and
         * unusable.
         */
        public abstract void Bind(PieceInteractionSnapshot interaction);

        /** Places the prop for the frame, after the tray has placed its dice. */
        public virtual void Layout() { }

        /** Leaves the prop in place and unusable; the presenter is not used afterwards. */
        public abstract void Dispose();
    }
}
