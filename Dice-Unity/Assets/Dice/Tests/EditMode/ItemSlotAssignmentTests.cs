using System;
using System.Collections.Generic;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    /** Which item slot each item in play rests on at a table, given the player's slot, the scene's slots and props. */
    public sealed class ItemSlotAssignmentTests
    {
        private const string Table = "Test Table";
        private static readonly ItemDefinition.Id LentId = new ItemDefinition.Id(new Guid("1a2b3c4d5e6f40718293a4b5c6d7e8f9"));
        private static readonly ItemDefinition.Id CarriedId = new ItemDefinition.Id(new Guid("2b3c4d5e6f70418293a4b5c6d7e8f90a"));
        private static readonly ItemDefinition.Id OtherCarriedId = new ItemDefinition.Id(new Guid("3c4d5e6f708142939a4b5c6d7e8f90ab"));
        private static readonly ItemDefinition.Id AbsentId = new ItemDefinition.Id(new Guid("4d5e6f70819243a4ab5c6d7e8f90abbc"));
        private readonly List<Object> created = new List<Object>();
        private SyntheticCharmAssets assets;

        [SetUp]
        public void SetUp() => assets = new SyntheticCharmAssets();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; --i)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
            assets.Dispose();
        }

        /**
         * Checks a lent item rests on the scene slot naming it, a carried item on the player's slot, an item whose piece a
         * scene prop shows on none, and a scene slot whose item is not in play is unused / fails if an item rests on another
         * slot, a prop's item is given a slot or an idle slot is shown.
         */
        [Test]
        public void ItemsRestOnTheirSlotsAndIdleSlotsAreUnused()
        {
            var player = Slot("Player Slot", null);
            var lentSlot = Slot("Lent Slot", LentId);
            var absentSlot = Slot("Absent Slot", AbsentId);
            var lent = Item(LentId, "piece.test.lent");
            var carried = Item(CarriedId, "piece.test.carried");
            var propped = Item(OtherCarriedId, "piece.test.propped");
            var props = new[] { Prop("piece.test.propped") };

            var assignment = ItemSlotAssignment.Assign(player, new[] { lentSlot, absentSlot }, new[] { lent },
                new[] { carried, propped }, props, Table);
            Assert.That(assignment.SlotOf(LentId), Is.SameAs(lentSlot));
            Assert.That(assignment.SlotOf(CarriedId), Is.SameAs(player));
            Assert.Throws<InvalidOperationException>(() => assignment.SlotOf(OtherCarriedId), "A prop's item rests on no slot.");
            Assert.That(assignment.Unused, Is.EqualTo(new[] { absentSlot }));

            var unusedPlayer = ItemSlotAssignment.Assign(player, new[] { lentSlot }, new[] { lent }, Array.Empty<ItemDefinition>(),
                props, Table);
            Assert.That(unusedPlayer.Unused, Is.EqualTo(new[] { player }), "No carried item needs the player's slot.");
        }

        /**
         * Checks a lent item with a piece and neither a prop nor a scene slot fails naming the item and the table, and two
         * carried items needing the player's slot fail naming both / fails if an item is left without a place or the
         * player's slot takes more than one item.
         */
        [Test]
        public void AnItemWithoutAPlaceAndTwoItemsForThePlayersSlotFail()
        {
            var player = Slot("Player Slot", null);
            var unplaced = Assert.Throws<InvalidOperationException>(() => ItemSlotAssignment.Assign(player,
                Array.Empty<SpiritItemSlot>(), new[] { Item(LentId, "piece.test.lent") }, Array.Empty<ItemDefinition>(),
                Array.Empty<SpiritTablePieceView>(), Table));
            Assert.That(unplaced.Message, Does.Contain(LentId.ToString()).And.Contain(Table));

            var crowded = Assert.Throws<InvalidOperationException>(() => ItemSlotAssignment.Assign(player,
                Array.Empty<SpiritItemSlot>(), Array.Empty<ItemDefinition>(),
                new[] { Item(CarriedId, "piece.test.carried"), Item(OtherCarriedId, "piece.test.other") },
                Array.Empty<SpiritTablePieceView>(), Table));
            Assert.That(crowded.Message, Does.Contain(CarriedId.ToString()).And.Contain(OtherCarriedId.ToString()));
        }

        private static ItemDefinition Item(ItemDefinition.Id id, string pieceName)
            => new ItemDefinition(id, piece: new TablePieceDefinition(TestPieces.IdOf(pieceName),
                new[] { new PositionInteractionDefinition(PiecePosition.Neutral, PieceMovementTiming.DuringHand) }));

        /** A slot naming the item with the id, or the player's slot without one. */
        private SpiritItemSlot Slot(string name, ItemDefinition.Id? item)
        {
            var slot = Own(new GameObject(name, typeof(RectTransform))).AddComponent<SpiritItemSlot>();
            if (item.HasValue)
            {
                var asset = Own(ScriptableObject.CreateInstance<ItemAsset>());
                asset.name = "Item " + item.Value;
                SyntheticUi.Set(asset, ContentAsset.AssetGuidField, item.Value.Value.ToString("N"));
                SyntheticUi.Set(slot, "item", asset);
            }
            return slot;
        }

        /** A scene prop of the test piece named by the text. */
        private SpiritTablePieceView Prop(string pieceName)
        {
            var prop = Own(new GameObject("Prop " + pieceName, typeof(RectTransform))).AddComponent<SpiritTablePieceView>();
            SyntheticUi.Set(prop, "piece", assets.Piece(pieceName));
            return prop;
        }

        private T Own<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }
    }
}
