using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;

namespace Dice.Presentation
{
    /**
     * Which item slot each item in play rests on at a table. A scene's own prop always presents its piece, so an item whose
     * piece a prop shows has no slot. A lent item with a piece rests on the scene slot assigned to it, never on the player's
     * slot; a carried item with a piece rests on the player's slot, which holds one. It fails, naming the items and the
     * table, when a lent item's piece has neither a prop nor a scene slot, when more carried items need the player's slot
     * than it holds or the HUD has none, or when two scene slots hold one item. A scene slot whose item is not lent in play,
     * and the player's slot when no carried item needs it, are unused and hidden.
     */
    public sealed class ItemSlotAssignment
    {
        private readonly Dictionary<ItemDefinition.Id, SpiritItemSlot> slots;
        private readonly string table;

        private ItemSlotAssignment(Dictionary<ItemDefinition.Id, SpiritItemSlot> slots, List<SpiritItemSlot> unused, string table)
        {
            this.slots = slots;
            Unused = unused;
            this.table = table;
        }

        /** The slots no item in play rests on, which are hidden. */
        public IReadOnlyList<SpiritItemSlot> Unused { get; }

        /**
         * Assigns the slots for the items in play: the encounter's lent items and the items the player carries, given the
         * player's slot (null when the HUD has none), the scene's own slots and the scene's props; table names the table in
         * failures.
         */
        public static ItemSlotAssignment Assign(SpiritItemSlot playerSlot, IReadOnlyList<SpiritItemSlot> sceneSlots,
            IReadOnlyList<ItemDefinition> lentItems, IReadOnlyList<ItemDefinition> carriedItems,
            IReadOnlyList<SpiritTablePieceView> props, string table)
        {
            if (sceneSlots == null) throw new ArgumentNullException(nameof(sceneSlots));
            if (lentItems == null) throw new ArgumentNullException(nameof(lentItems));
            if (carriedItems == null) throw new ArgumentNullException(nameof(carriedItems));
            if (props == null) throw new ArgumentNullException(nameof(props));
            var bySceneItem = new Dictionary<ItemDefinition.Id, SpiritItemSlot>();
            foreach (var slot in sceneSlots)
            {
                if (slot == null) throw new InvalidOperationException("The table " + table + " lists a missing item slot.");
                if (slot.Item == null)
                    throw new InvalidOperationException("The item slot " + slot.name + " of the table " + table + " names no item.");
                if (bySceneItem.ContainsKey(slot.Item.Identity))
                    throw new InvalidOperationException("The table " + table + " has more than one item slot for the item " +
                        slot.Item.name + ".");
                bySceneItem.Add(slot.Item.Identity, slot);
            }

            var assigned = new Dictionary<ItemDefinition.Id, SpiritItemSlot>();
            foreach (var item in lentItems)
            {
                if (!NeedsSlot(item, props)) continue;
                if (!bySceneItem.TryGetValue(item.Identity, out SpiritItemSlot slot))
                    throw new InvalidOperationException("The item " + item.Identity + " lent at the table " + table +
                        " has a table piece, but the table gives it neither a prop nor an item slot.");
                assigned.Add(item.Identity, slot);
            }
            var needingPlayerSlot = carriedItems.Where(item => NeedsSlot(item, props)).ToList();
            if (needingPlayerSlot.Count > 1)
                throw new InvalidOperationException("The carried items " + string.Join(", ", needingPlayerSlot.Select(item => item.Identity)) +
                    " each need the player's item slot at the table " + table + ", which holds one.");
            if (needingPlayerSlot.Count == 1)
            {
                if (playerSlot == null)
                    throw new InvalidOperationException("The carried item " + needingPlayerSlot[0].Identity + " needs the player's item slot," +
                        " but the HUD at the table " + table + " has none.");
                assigned.Add(needingPlayerSlot[0].Identity, playerSlot);
            }

            var unused = new List<SpiritItemSlot>();
            foreach (var slot in sceneSlots)
                if (!assigned.ContainsValue(slot)) unused.Add(slot);
            if (playerSlot != null && !assigned.ContainsValue(playerSlot)) unused.Add(playerSlot);
            return new ItemSlotAssignment(assigned, unused, table);
        }

        /** The slot the item rests on; fails, naming the item and the table, for an item that has none. */
        public SpiritItemSlot SlotOf(ItemDefinition.Id item)
            => slots.TryGetValue(item, out SpiritItemSlot slot) ? slot
                : throw new InvalidOperationException("The item " + item + " has no item slot at the table " + table + ".");

        // An item's piece without a scene prop rests on a slot; an item without a piece needs none.
        private static bool NeedsSlot(ItemDefinition item, IReadOnlyList<SpiritTablePieceView> props)
            => item.Piece != null && SpiritTablePieceView.Find(props, item.Piece.Identity) == null;
    }
}
