using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    /**
     * How table piece props are bound to their interactions: the encounter screen's piece presenters create one presenter
     * per prop through the prop's presenter asset and know no interaction kind, and the position kind binds its prop to the
     * position state and turns a release into a move input.
     */
    public sealed class PieceInteractionPresenterTests
    {
        private const string FirstPiece = "piece.test.first";
        private const string SecondPiece = "piece.test.second";
        private const string AbsentPiece = "piece.test.absent";
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
            var encounter = catalog.GetEncounter(TestContent.EncounterId);

            var presenters = TablePiecePresenters.Create(new[] { first, absent, second }, encounter, context);
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
            Assert.Throws<InvalidOperationException>(() => TablePiecePresenters.Create(new[] { first, unpresented }, encounter, context));
            Assert.That(log, Is.EqualTo(new[] { FirstPiece + " create", FirstPiece + " dispose" }),
                "A failed creation disposes the presenters it created.");
            var unnamed = Piece(null, recording, false);
            Assert.Throws<InvalidOperationException>(() => TablePiecePresenters.Create(new[] { unnamed }, encounter, context));
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

        private static void Drag(SpiritTablePiecePosition piece, Vector3 from, Vector3 to)
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
        }

        /** A presenter kind of the first interaction that records, under its prop's piece name, what it is created with and asked to do. */
        private sealed class RecordingPresenterAsset : PieceInteractionPresenterAsset
        {
            [NonSerialized] public List<string> Log;
            [NonSerialized] public readonly List<TablePieceDefinition> Received = new List<TablePieceDefinition>();

            public override int InteractionIndex(TablePieceDefinition definition) => 0;

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
    }
}
