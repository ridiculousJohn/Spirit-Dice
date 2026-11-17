using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * The position interaction's presentation for an item's piece that the scene gives no prop: a bay indicator, the item's
     * glow on the bay floor and its emblem on the bay's front lip, shown on the bay the piece rests on and hidden at the
     * neutral position. It is display only; it acts when bound and when disposed.
     */
    [CreateAssetMenu(menuName = "Dice/Presentation/Piece Interaction Presenter/Bay Indicator")]
    public sealed class BayIndicatorPresenterAsset : ItemPiecePresenterAsset<PositionInteractionDefinition>
    {
        [Tooltip("The bay indicator prefab the tray hangs from the bay the item's piece rests on.")]
        [SerializeField] private SpiritBayIndicator indicatorPrefab;

        /** Fails, naming the presentation and this presenter, without a prefab or with one the tray cannot hang from a bay. */
        public override void Validate(ItemPresentationAsset presentation)
        {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            string where = "The bay indicator presenter " + name + " of the item presentation " + presentation.name;
            if (indicatorPrefab == null) throw new InvalidOperationException(where + " references no bay indicator prefab.");
            try { indicatorPrefab.Validate(); }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException(where + " is invalid: " + exception.Message, exception);
            }
        }

        protected override PieceInteractionPresenter Create(ItemPresentationAsset item, TablePieceDefinition piece,
            PositionInteractionDefinition interaction, int interactionIndex, PieceInteractionPresenterContext context)
            => new BayIndicatorPresenter(item, context.CreateBayIndicator(indicatorPrefab, item.Icon, item.GlowColor));

        private sealed class BayIndicatorPresenter : PieceInteractionPresenter
        {
            private readonly ItemPresentationAsset item;
            private readonly BayIndicator indicator;

            internal BayIndicatorPresenter(ItemPresentationAsset item, BayIndicator indicator)
            {
                this.item = item;
                this.indicator = indicator;
            }

            public override void Bind(PieceInteractionSnapshot interaction)
            {
                if (interaction == null) { indicator.Hide(); return; }
                if (!(interaction.State is PiecePositionState state))
                    throw new InvalidOperationException("The position interaction of the table piece " + interaction.PieceId +
                        " of the item presentation " + item.name + " has a " + interaction.State?.GetType().Name + " state.");
                if (state.Position.Kind == PiecePositionKind.DiceBay) indicator.ShowOn(state.Position.Index);
                else indicator.Hide();
            }

            public override void Dispose() => indicator.Release();
        }
    }
}
