using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    /**
     * How table pieces are bound to their interactions: the encounter screen's piece presenters create one presenter per
     * prop through the prop's presenter asset and one per item piece without a prop through the item presentation's piece
     * presenter, and know no interaction kind; the position kind binds its prop to the position state and turns a release
     * into a move input, and the bay indicator kind shows its item's mark on the bay of the position state.
     */
    public sealed class PieceInteractionPresenterTests
    {
        private const string FirstPiece = "piece.test.first";
        private const string SecondPiece = "piece.test.second";
        private const string AbsentPiece = "piece.test.absent";
        private static readonly ItemDefinition.Id PropItemId = new ItemDefinition.Id(new Guid("6e2b8d4f0a3c4e5a9b1d3f5a7c9e2b4d"));
        private static readonly ItemDefinition.Id SlotlessItemId = new ItemDefinition.Id(new Guid("7f3c9e5a1b4d4f6b8c2e4a6c8e0f3a5b"));
        private readonly List<Object> created = new List<Object>();
        private readonly List<string> log = new List<string>();
        private RecordingContext context;
        private SyntheticCharmAssets assets;

        [SetUp]
        public void SetUp()
        {
            context = new RecordingContext();
            assets = new SyntheticCharmAssets();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                if (value != null) Object.DestroyImmediate(value);
            created.Clear();
            log.Clear();
            assets.Dispose();
        }

        /**
         * Checks the position presenter binds its prop to the snapshot's position, places it on the context's bay point and
         * submits a release near another bay as a move, and that a missing interaction or disposal leaves the prop unusable /
         * fails if the prop shows another position, rests elsewhere or a release reaches the engine as anything but that move.
         */
        [Test]
        public void PositionPresenterBindsThePositionAndSubmitsAReleaseAsAMove()
        {
            var definition = new PositionInteractionDefinition(PiecePosition.Bay(2), PieceMovementTiming.DuringHand, false);
            var piece = Piece(FirstPiece, Asset<PositionInteractionPresenterAsset>(), true);
            var position = piece.GetComponent<SpiritTablePiecePosition>();
            var pieceDefinition = Piece(FirstPiece, definition);
            var interaction = Session(Catalog(pieceDefinition)).Snapshot.PieceInteractions.Single();
            var presenter = piece.Presenter.Create(piece, pieceDefinition, context);

            presenter.Bind(interaction);
            presenter.Layout();
            Assert.That(position.IsBound, Is.True);
            Assert.That(position.Position, Is.EqualTo(PiecePosition.Bay(2)));
            Assert.That(Vector3.Distance(piece.transform.position, RecordingContext.Point(2)), Is.LessThan(.001f));

            Drag(position, RecordingContext.Point(2), RecordingContext.Point(0) + new Vector3(10, 5, 0));
            var move = context.Inputs.Single();
            Assert.That(move.Key, Is.EqualTo(TestPieces.IdOf(FirstPiece) + " 0"));
            Assert.That(((MovePieceInput)move.Value).Position, Is.EqualTo(PiecePosition.Bay(0)));

            presenter.Bind(null);
            Assert.That(position.IsBound, Is.False, "A snapshot without the interaction leaves the prop unusable.");
            presenter.Bind(interaction);
            presenter.Dispose();
            Assert.That(position.IsBound, Is.False, "A disposed presenter leaves the prop unusable.");
        }

        /**
         * Checks a presenter asset finds the piece's interaction of its kind by its place in the piece's list, and refuses a
         * missing piece, a piece without an interaction of its kind and a piece that is not its prop's, and the position kind
         * refuses a prop without its position component / fails if a prop is bound to an interaction it does not present or
         * cannot show.
         */
        [Test]
        public void PresenterAssetPresentsOnlyThePropsPieceInteractionOfItsKind()
        {
            var piece = Piece(FirstPiece, Asset<PositionInteractionPresenterAsset>(), true);
            var position = new PositionInteractionDefinition(PiecePosition.Neutral, PieceMovementTiming.DuringHand);
            Assert.That(piece.Presenter.InteractionIndex(Piece(FirstPiece, new ToggleInteractionDefinition(), position)), Is.EqualTo(1));
            Assert.Throws<ArgumentNullException>(() => piece.Presenter.Create(piece, null, context));
            Assert.Throws<InvalidOperationException>(() => piece.Presenter.Create(piece,
                Piece(FirstPiece, new ToggleInteractionDefinition()), context), "A piece without an interaction of the kind is rejected.");
            Assert.Throws<InvalidOperationException>(() => piece.Presenter.Create(piece, Piece(SecondPiece, position), context));
            var bare = Piece(FirstPiece, piece.Presenter, false);
            Assert.Throws<InvalidOperationException>(() => bare.Presenter.Create(bare, Piece(FirstPiece, position), context));
        }

        /**
         * Checks the screen's piece presenters create one presenter per prop whose piece the encounter places, with that
         * piece's definition, bind each to its own interaction, lay out and dispose them all, and create none for a prop the
         * encounter lacks / fails if a prop is bound through another prop's interaction, a prop the encounter lacks gets a
         * presenter or a prop without its piece or presenter is accepted.
         */
        [Test]
        public void ScreenBindsEachPropThroughItsOwnPresenter()
        {
            var recording = Asset<RecordingPresenterAsset>();
            recording.Log = log;
            var first = Piece(FirstPiece, recording, false);
            var second = Piece(SecondPiece, recording, false);
            var absent = Piece(AbsentPiece, recording, false);
            var firstDefinition = Piece(FirstPiece,
                new PositionInteractionDefinition(PiecePosition.Bay(1), PieceMovementTiming.DuringHand));
            var secondDefinition = Piece(SecondPiece,
                new PositionInteractionDefinition(PiecePosition.Bay(3), PieceMovementTiming.DuringHand));
            var catalog = Catalog(firstDefinition, secondDefinition);
            var session = Session(catalog);
            var installedPieces = session.InstalledPieces;

            var noItems = Items();
            var presenters = TablePiecePresenters.Create(new[] { first, absent, second }, installedPieces, Array.Empty<ItemDefinition>(),
                noItems, context);
            Assert.That(presenters.Presenters.Count, Is.EqualTo(2));
            Assert.That(recording.Received.Count, Is.EqualTo(2));
            Assert.That(recording.Received[0], Is.SameAs(firstDefinition), "Each presenter is created with its prop's piece.");
            Assert.That(recording.Received[1], Is.SameAs(secondDefinition), "Each presenter is created with its prop's piece.");
            presenters.Bind(session.Snapshot);
            presenters.Layout();
            presenters.Dispose();
            Assert.That(log, Is.EqualTo(new[]
            {
                FirstPiece + " create", SecondPiece + " create",
                FirstPiece + " bind " + TestPieces.IdOf(FirstPiece) + " 0", SecondPiece + " bind " + TestPieces.IdOf(SecondPiece) + " 0",
                FirstPiece + " layout", SecondPiece + " layout", FirstPiece + " dispose", SecondPiece + " dispose"
            }));

            log.Clear();
            var unpresented = Piece(SecondPiece, null, false);
            Assert.Throws<InvalidOperationException>(() => TablePiecePresenters.Create(new[] { first, unpresented }, installedPieces,
                Array.Empty<ItemDefinition>(), noItems, context));
            Assert.That(log, Is.EqualTo(new[] { FirstPiece + " create", FirstPiece + " dispose" }),
                "A failed creation disposes the presenters it created.");
            var unnamed = Piece(null, recording, false);
            Assert.Throws<InvalidOperationException>(() => TablePiecePresenters.Create(new[] { unnamed }, installedPieces,
                Array.Empty<ItemDefinition>(), noItems, context));
        }

        /**
         * Checks an item's piece that a scene prop presents gets the prop's presenter and no item presenter, and an item's
         * piece without a prop gets its item presentation's piece presenter, bound to its own interaction / fails if a scene
         * prop's piece is presented twice or an item piece without a prop is left unpresented.
         */
        [Test]
        public void AnItemPieceWithoutAPropIsPresentedByItsItemPresentation()
        {
            var recording = Asset<RecordingPresenterAsset>();
            recording.Log = log;
            var itemRecording = Asset<RecordingItemPresenterAsset>();
            itemRecording.Log = log;
            var prop = Piece(FirstPiece, recording, false);
            var propPiece = Piece(FirstPiece, new PositionInteractionDefinition(PiecePosition.Bay(1), PieceMovementTiming.DuringHand));
            var slotlessPiece = Piece(SecondPiece, new PositionInteractionDefinition(PiecePosition.Bay(3), PieceMovementTiming.DuringHand));
            var propItem = new ItemDefinition(PropItemId, piece: propPiece);
            var slotlessItem = new ItemDefinition(SlotlessItemId, piece: slotlessPiece);
            var items = Items((propItem, itemRecording), (slotlessItem, itemRecording));
            var session = Session(Catalog(propPiece, slotlessPiece));

            var presenters = TablePiecePresenters.Create(new[] { prop }, new[] { propPiece, slotlessPiece },
                new[] { propItem, slotlessItem }, items, context);
            Assert.That(presenters.Presenters.Count, Is.EqualTo(2));
            Assert.That(itemRecording.Received, Is.EqualTo(new[] { slotlessPiece }), "Only the piece without a prop gets an item presenter.");
            presenters.Bind(session.Snapshot);
            presenters.Dispose();
            Assert.That(log, Is.EqualTo(new[]
            {
                FirstPiece + " create", "item " + SecondPiece + " create",
                FirstPiece + " bind " + TestPieces.IdOf(FirstPiece) + " 0", "item " + SecondPiece + " bind " + TestPieces.IdOf(SecondPiece) + " 0",
                FirstPiece + " dispose", "item " + SecondPiece + " dispose"
            }));
        }

        /**
         * Checks the item position presenter shows its item on the slot the context gives it and one bay indicator with the
         * item's icon and glow colour: at neutral the slot shows the item undimmed, the indicator is hidden and the draggable
         * icon is the invisible handle; on a bay the slot dims and the indicator shows there; while the icon is held it shows,
         * the slot stays dimmed and the emblem hides, and after the release the emblem shows again; a release near another bay
         * proposes that move; disposing it releases the indicator and hides the slot / fails if the item shows twice at rest
         * or in the hand, the slot ignores its item's place, a release reaches the engine as anything but that move or the
         * slot outlives its presenter.
         */
        [Test]
        public void ItemPositionPresenterShowsItsItemOnItsSlotAndItsBay()
        {
            var asset = Asset<ItemPositionPresenterAsset>();
            var prefab = new GameObject("Bay Indicator").AddComponent<SpiritBayIndicator>();
            created.Add(prefab.gameObject);
            SyntheticUi.Set(asset, "indicatorPrefab", prefab);
            var definition = Piece(FirstPiece, new PositionInteractionDefinition(PiecePosition.Neutral, PieceMovementTiming.DuringHand));
            var item = new ItemDefinition(SlotlessItemId, piece: definition);
            var presentation = Presentation(item, asset);
            context.Slot = Slot();
            var presenter = asset.Create(presentation, definition, context);
            var indicator = context.Indicators.Single();
            Assert.That(context.SlotRequests, Is.EqualTo(new[] { item.Identity }));
            Assert.That(indicator.Prefab, Is.SameAs(prefab));
            Assert.That(indicator.Icon, Is.SameAs(presentation.Icon));
            Assert.That(indicator.GlowColor, Is.EqualTo(presentation.GlowColor));
            var icon = context.Slot.Icon;

            presenter.Bind(Position(definition, PiecePosition.Neutral));
            presenter.Layout();
            Assert.That(context.Slot.gameObject.activeSelf, Is.True);
            Assert.That(context.Slot.IsDimmed, Is.False);
            Assert.That(context.Slot.GetComponentInChildren<Image>().sprite, Is.SameAs(presentation.Icon), "The slot shows its item.");
            Assert.That(icon.Position, Is.EqualTo(PiecePosition.Neutral));
            Assert.That(icon.IsVisible, Is.False, "At rest the slot shows the item; the icon is the handle.");

            presenter.Bind(Position(definition, PiecePosition.Bay(2)));
            presenter.Layout();
            Assert.That(context.Slot.IsDimmed, Is.True);
            Assert.That(icon.Position, Is.EqualTo(PiecePosition.Bay(2)));
            Assert.That(icon.IsVisible, Is.False, "On a bay the indicator shows the item; the icon is the handle.");

            icon.OnBeginDrag(Pointer(RecordingContext.Point(2)));
            icon.OnDrag(Pointer(RecordingContext.Point(1)));
            Assert.That(icon.IsVisible, Is.True, "The held icon shows.");
            Assert.That(context.Slot.IsDimmed, Is.True, "The slot stays dimmed while its item is in the hand.");
            var release = RecordingContext.Point(0) + new Vector3(10, 5, 0);
            icon.OnDrag(Pointer(release));
            icon.OnEndDrag(Pointer(release));
            var move = context.Inputs.Single();
            Assert.That(move.Key, Is.EqualTo(TestPieces.IdOf(FirstPiece) + " 0"));
            Assert.That(((MovePieceInput)move.Value).Position, Is.EqualTo(PiecePosition.Bay(0)));

            presenter.Dispose();
            Assert.That(indicator.Log, Is.EqualTo(new[] { "hide", "show 2", "emblem on", "emblem off", "emblem on", "release" }),
                "The emblem hides only while the item is in the hand.");
            Assert.That(context.Slot.gameObject.activeSelf, Is.False, "Disposing the presenter clears its slot.");
            Assert.That(context.Slot.IsDimmed, Is.False);
        }

        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            created.Add(asset);
            return asset;
        }

        /**
         * A prop of the test piece named by the text, without a piece when it is null, under a table rect; with a position
         * component that settles at once when one is asked for.
         */
        private SpiritTablePieceView Piece(string pieceName, PieceInteractionPresenterAsset presenter, bool positioned)
        {
            var table = new GameObject("Table", typeof(RectTransform));
            created.Add(table);
            var prop = new GameObject("Table Piece " + pieceName, typeof(RectTransform));
            prop.transform.SetParent(table.transform, false);
            var piece = prop.AddComponent<SpiritTablePieceView>();
            using (var serialized = new SerializedObject(piece))
            {
                serialized.FindProperty("piece").objectReferenceValue = pieceName != null ? assets.Piece(pieceName) : null;
                serialized.FindProperty("presenter").objectReferenceValue = presenter;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            if (positioned)
            {
                using var position = new SerializedObject(prop.AddComponent<SpiritTablePiecePosition>());
                position.FindProperty("settleDuration").floatValue = 0f;
                position.ApplyModifiedPropertiesWithoutUndo();
            }
            return piece;
        }

        private static TablePieceDefinition Piece(string pieceName, params PieceInteractionDefinition[] interactions)
            => new TablePieceDefinition(TestPieces.IdOf(pieceName), interactions);

        private static MechanicalCatalog Catalog(params TablePieceDefinition[] pieces)
        {
            var encounter = new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1000000, 20, 20) },
                pieces: pieces);
            return new MechanicalCatalog("test.piece.presenters", TestContent.Hands(), new[] { encounter });
        }

        private static EncounterSession Session(MechanicalCatalog catalog)
            => EncounterSession.CreateEncounter(catalog, TestContent.EncounterId, 3);

        /**
         * An item slot under a table rect with its icon beside it, the icon's neutral anchor on the slot, settling at once;
         * hidden, as a slot no item is assigned to is.
         */
        private SpiritItemSlot Slot()
        {
            var table = new GameObject("Table", typeof(RectTransform));
            created.Add(table);
            var slotObject = new GameObject("Item Slot", typeof(RectTransform), typeof(CanvasGroup));
            slotObject.transform.SetParent(table.transform, false);
            slotObject.transform.position = new Vector3(-300, 200, 0);
            var itemImage = new GameObject("Item Image", typeof(RectTransform), typeof(Image));
            itemImage.transform.SetParent(slotObject.transform, false);
            var anchor = new GameObject("Icon Anchor", typeof(RectTransform));
            anchor.transform.SetParent(slotObject.transform, false);
            var iconObject = new GameObject("Item Slot Icon", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            iconObject.transform.SetParent(table.transform, false);
            var icon = iconObject.AddComponent<SpiritItemSlotIcon>();
            using (var serialized = new SerializedObject(icon))
            {
                serialized.FindProperty("image").objectReferenceValue = iconObject.GetComponent<Image>();
                serialized.FindProperty("neutralAnchor").objectReferenceValue = anchor.transform;
                serialized.FindProperty("settleDuration").floatValue = 0f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var slot = slotObject.AddComponent<SpiritItemSlot>();
            using (var serialized = new SerializedObject(slot))
            {
                serialized.FindProperty("icon").objectReferenceValue = icon;
                serialized.FindProperty("itemImage").objectReferenceValue = itemImage.GetComponent<Image>();
                serialized.FindProperty("frame").objectReferenceValue = slotObject.GetComponent<CanvasGroup>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            slot.Hide();
            return slot;
        }

        /** The piece's first interaction in a position state. */
        private static PieceInteractionSnapshot Position(TablePieceDefinition piece, PiecePosition position)
            => new PieceInteractionSnapshot(piece.Identity, 0, true, new PiecePositionState(position));

        /** The item presentation of the items, each presented by Presentation with its piece presenter. */
        private ItemPresentationCatalog Items(params (ItemDefinition item, ItemPiecePresenterAsset presenter)[] items)
        {
            var encounter = new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1000000, 20, 20) });
            var definitions = items.Select(entry => entry.item).ToArray();
            var catalog = new MechanicalCatalog("test.item.pieces", TestContent.Hands(), new[] { encounter }, items: definitions);
            var pairs = new List<KeyValuePair<ItemAsset, ItemDefinition>>();
            var presentations = new List<ItemPresentationAsset>();
            foreach (var (item, presenter) in items)
            {
                var presentation = Presentation(item, presenter);
                pairs.Add(new KeyValuePair<ItemAsset, ItemDefinition>(presentation.Item, item));
                presentations.Add(presentation);
            }
            var skin = Asset<ItemSkinAsset>();
            SyntheticUi.Set(skin, "items", presentations.ToArray());
            var content = new MechanicalContent(catalog, Array.Empty<KeyValuePair<ModifierAsset, ModifierDefinition>>(), pairs);
            return new ItemPresentationCatalog(skin, content);
        }

        /** A presentation of the item with a name, an icon, a glow colour, no rule cards and the piece presenter. */
        private ItemPresentationAsset Presentation(ItemDefinition item, ItemPiecePresenterAsset presenter)
        {
            var asset = Asset<ItemAsset>();
            asset.name = "Item " + item.Identity;
            SyntheticUi.Set(asset, ContentAsset.AssetGuidField, item.Identity.Value.ToString("N"));
            var texture = new Texture2D(2, 2);
            created.Add(texture);
            var icon = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f));
            created.Add(icon);
            var presentation = Asset<ItemPresentationAsset>();
            SyntheticUi.Set(presentation, "item", asset);
            SyntheticUi.Set(presentation, "displayName", "Test item");
            SyntheticUi.Set(presentation, "icon", icon);
            SyntheticUi.Set(presentation, "glowColor", new Color(.2f, .6f, .9f, .8f));
            SyntheticUi.Set(presentation, "piecePresenter", presenter);
            return presentation;
        }

        private static void Drag(SpiritPositionDrag piece, Vector3 from, Vector3 to)
        {
            piece.OnBeginDrag(Pointer(from));
            piece.OnDrag(Pointer(to));
            piece.OnEndDrag(Pointer(to));
        }

        private static PointerEventData Pointer(Vector3 position) => new PointerEventData(null) { position = position, pointerId = 0 };

        /** A further interaction kind, fabricated so presenters can be shown it; the engine never runs it here. */
        private sealed class ToggleInteractionDefinition : PieceInteractionDefinition
        {
            internal override PieceInteractionState InitialState => throw new NotSupportedException();
            internal override bool IsAvailable(EncounterState state) => throw new NotSupportedException();
            internal override bool Accepts(PieceInteractionState current, PieceInteractionInput input) => throw new NotSupportedException();
            internal override PieceInteractionState Apply(PieceInteractionState current, PieceInteractionInput input)
                => throw new NotSupportedException();
            internal override int[] Capture(PieceInteractionState state) => throw new NotSupportedException();
            internal override bool TryRestore(int[] values, out PieceInteractionState state) => throw new NotSupportedException();
        }

        /** A table that always accepts piece input, records each input by piece and interaction, and spaces bays 100 apart. */
        private sealed class RecordingContext : PieceInteractionPresenterContext
        {
            public readonly List<KeyValuePair<string, PieceInteractionInput>> Inputs = new List<KeyValuePair<string, PieceInteractionInput>>();

            public static Vector3 Point(int bay) => new Vector3(100 * bay, 0, 0);

            public override bool CanInteract(TablePieceDefinition.Id pieceId, int interactionIndex) => true;

            public override bool Interact(TablePieceDefinition.Id pieceId, int interactionIndex, PieceInteractionInput input)
            {
                Inputs.Add(new KeyValuePair<string, PieceInteractionInput>(pieceId + " " + interactionIndex, input));
                return true;
            }

            public override Vector3 BayPoint(int bay, Vector2 placement) => Point(bay);

            /** A bay's label anchor is its bay point. */
            public override Vector3 BayLabelPoint(int bay) => Point(bay);

            /** A tray viewport point's x counts bays, as the recording indicator reports its emblem. */
            public override Vector3 TrayPoint(Vector2 trayViewportPoint) => Point(0) + Vector3.right * (100 * trayViewportPoint.x);

            public readonly List<RecordingBayIndicator> Indicators = new List<RecordingBayIndicator>();

            /** The slot the table gives every item, and the items it was asked for. */
            public SpiritItemSlot Slot;
            public readonly List<ItemDefinition.Id> SlotRequests = new List<ItemDefinition.Id>();

            public override SpiritItemSlot ItemSlot(ItemDefinition.Id item)
            {
                SlotRequests.Add(item);
                return Slot;
            }

            public override BayIndicator CreateBayIndicator(SpiritBayIndicator prefab, Sprite icon, Color glowColor)
            {
                var indicator = new RecordingBayIndicator(prefab, icon, glowColor);
                Indicators.Add(indicator);
                return indicator;
            }
        }

        /** A bay indicator that records what it was made with and what it is asked to do. */
        private sealed class RecordingBayIndicator : BayIndicator
        {
            public readonly List<string> Log = new List<string>();

            public RecordingBayIndicator(SpiritBayIndicator prefab, Sprite icon, Color glowColor)
            {
                Prefab = prefab;
                Icon = icon;
                GlowColor = glowColor;
            }

            public SpiritBayIndicator Prefab { get; }
            public Sprite Icon { get; }
            public Color GlowColor { get; }

            private int shownBay = -1;

            public override void ShowOn(int bay)
            {
                shownBay = bay;
                Log.Add("show " + bay);
            }

            public override void Hide()
            {
                shownBay = -1;
                Log.Add("hide");
            }

            public override void ShowEmblem(bool shown) => Log.Add(shown ? "emblem on" : "emblem off");

            /** The emblem of a shown mark is at its bay's index across the tray's viewport. */
            public override Vector2 EmblemViewportPoint => new Vector2(shownBay, 0);
            public override void Release() => Log.Add("release");
        }

        /** A presenter kind of position interactions that records, under its prop's piece name, what it is created with and asked to do. */
        private sealed class RecordingPresenterAsset : PieceInteractionPresenterAsset
        {
            [NonSerialized] public List<string> Log;
            [NonSerialized] public readonly List<TablePieceDefinition> Received = new List<TablePieceDefinition>();

            public override Type InteractionKind => typeof(PositionInteractionDefinition);

            public override int InteractionIndex(TablePieceDefinition definition) => definition.IndexOf<PositionInteractionDefinition>();

            public override PieceInteractionPresenter Create(SpiritTablePieceView piece, TablePieceDefinition definition,
                PieceInteractionPresenterContext context)
            {
                Log.Add(piece.Piece.name + " create");
                Received.Add(definition);
                return new RecordingPresenter(Log, piece.Piece.name);
            }
        }

        private sealed class RecordingPresenter : PieceInteractionPresenter
        {
            private readonly List<string> log;
            private readonly string pieceName;

            public RecordingPresenter(List<string> log, string pieceName)
            {
                this.log = log;
                this.pieceName = pieceName;
            }

            public override void Bind(PieceInteractionSnapshot interaction)
                => log.Add(pieceName + " bind " + interaction.PieceId + " " + interaction.InteractionIndex);

            public override void Layout() => log.Add(pieceName + " layout");

            public override void Dispose() => log.Add(pieceName + " dispose");
        }

        /**
         * An item piece presenter kind of position interactions that records, under "item" and its piece's name, what it is
         * created with and asked to do.
         */
        private sealed class RecordingItemPresenterAsset : ItemPiecePresenterAsset
        {
            [NonSerialized] public List<string> Log;
            [NonSerialized] public readonly List<TablePieceDefinition> Received = new List<TablePieceDefinition>();

            public override Type InteractionKind => typeof(PositionInteractionDefinition);

            public override int InteractionIndex(TablePieceDefinition definition) => definition.IndexOf<PositionInteractionDefinition>();

            public override PieceInteractionPresenter Create(ItemPresentationAsset item, TablePieceDefinition piece,
                PieceInteractionPresenterContext context)
            {
                string pieceName = piece.Identity == TestPieces.IdOf(FirstPiece) ? FirstPiece : SecondPiece;
                Log.Add("item " + pieceName + " create");
                Received.Add(piece);
                return new RecordingPresenter(Log, "item " + pieceName);
            }
        }
    }
}
