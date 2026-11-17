using System;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * The position interaction's presentation for an item's piece that the scene gives no prop, through the item's slot and
     * the bay indicator together: the slot always shows the item, at full strength while it rests there; on a bay the bay
     * indicator (the item's glow on the bay floor and its emblem at the bay's label anchor) shows the item there and the
     * slot dims; while the player holds the item the slot dims and the emblem hides, the held icon being the item in the
     * hand, and the glow stays until the engine's position changes. The draggable icon is invisible at rest, the handle over
     * the slot or the emblem. A drag of the icon released near a bay or the slot proposes that move, as a prop's drag does
     * (PositionInteractionBinding). It follows the icon's announced state changes and the bound snapshots, never polls.
     */
    [CreateAssetMenu(menuName = "Dice/Presentation/Piece Interaction Presenter/Item Position")]
    public sealed class ItemPositionPresenterAsset : ItemPiecePresenterAsset<PositionInteractionDefinition>
    {
        [Tooltip("The bay indicator prefab the tray hangs from the bay the item's piece rests on.")]
        [SerializeField] private SpiritBayIndicator indicatorPrefab;

        /** Fails, naming the presentation and this presenter, without a prefab or with one the tray cannot hang from a bay. */
        public override void Validate(ItemPresentationAsset presentation)
        {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            string where = "The item position presenter " + name + " of the item presentation " + presentation.name;
            if (indicatorPrefab == null) throw new InvalidOperationException(where + " references no bay indicator prefab.");
            try { indicatorPrefab.Validate(); }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException(where + " is invalid: " + exception.Message, exception);
            }
        }

        protected override PieceInteractionPresenter Create(ItemPresentationAsset item, TablePieceDefinition piece,
            PositionInteractionDefinition interaction, int interactionIndex, PieceInteractionPresenterContext context)
        {
            if (item.Item == null) throw new InvalidOperationException("The item presentation " + item.name + " has no item reference.");
            SpiritItemSlot slot = context.ItemSlot(item.Item.Identity);
            BayIndicator indicator = context.CreateBayIndicator(indicatorPrefab, item.Icon, item.GlowColor);
            return new ItemPositionPresenter(item, slot, indicator, piece, interaction, interactionIndex, context);
        }

        private sealed class ItemPositionPresenter : PieceInteractionPresenter
        {
            private readonly SpiritItemSlot slot;
            private readonly BayIndicator indicator;
            private readonly PieceInteractionPresenterContext context;
            private readonly PositionInteractionBinding binding;
            private readonly Vector3[] bayPoints = new Vector3[DiceValues.Count];
            // The bay the indicator is shown on, or -1 while it is hidden.
            private int shownBay = -1;

            internal ItemPositionPresenter(ItemPresentationAsset item, SpiritItemSlot slot, BayIndicator indicator,
                TablePieceDefinition piece, PositionInteractionDefinition definition, int interactionIndex,
                PieceInteractionPresenterContext context)
            {
                this.slot = slot;
                this.indicator = indicator;
                this.context = context;
                slot.Show(item.Icon);
                binding = new PositionInteractionBinding(slot.Icon, definition, piece.Identity, interactionIndex, context,
                    piece.Identity + " of the item presentation " + item.name);
                slot.Icon.Changed += ShowHeld;
            }

            public override void Bind(PieceInteractionSnapshot interaction)
            {
                PiecePosition? position = binding.Bind(interaction);
                shownBay = position.HasValue && position.Value.Kind == PiecePositionKind.DiceBay ? position.Value.Index : -1;
                if (shownBay >= 0) indicator.ShowOn(shownBay);
                else indicator.Hide();
                ShowHeld();
            }

            /** The slot dims unless the item rests on it, and the emblem hides while the item is in the player's hand. */
            private void ShowHeld()
            {
                var icon = slot.Icon;
                bool restsOnSlot = icon.IsBound && icon.Position.Kind == PiecePositionKind.Neutral && !icon.IsDragging && !icon.IsSettling;
                slot.SetDimmed(icon.IsBound && !restsOnSlot);
                if (shownBay >= 0) indicator.ShowEmblem(!icon.IsDragging);
            }

            /**
             * The icon's bay points: over its own emblem on the bay the indicator is shown on, so items sharing a bay each have
             * their own handle, and over the label anchor of every other bay.
             */
            public override void Layout()
            {
                for (int bay = 0; bay < DiceValues.Count; ++bay)
                    bayPoints[bay] = bay == shownBay ? context.TrayPoint(indicator.EmblemViewportPoint) : context.BayLabelPoint(bay);
                slot.Icon.Layout(bayPoints);
            }

            public override void Dispose()
            {
                slot.Icon.Changed -= ShowHeld;
                binding.Unbind();
                indicator.Release();
                slot.Hide();
            }
        }
    }
}
