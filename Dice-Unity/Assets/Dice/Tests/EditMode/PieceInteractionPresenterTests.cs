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
        private const string InteractionId = "position";
        private readonly List<Object> created = new List<Object>();
        private readonly List<string> log = new List<string>();
        private RecordingContext context;

        [SetUp]
        public void SetUp() => context = new RecordingContext();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in created)
                if (value != null) Object.DestroyImmediate(value);
            created.Clear();
            log.Clear();
        }

        /**
         * Checks the position presenter binds its prop to the snapshot's position, places it on the context's bay point and
         * submits a release near another bay as a move, and that a missing interaction or disposal leaves the prop unusable /
         * fails if the prop shows another position, rests elsewhere or a release reaches the engine as anything but that move.
         */
        [Test]
        public void PositionPresenterBindsThePositionAndSubmitsAReleaseAsAMove()
        {
            var definition = new PositionInteractionDefinition(InteractionId, PiecePosition.Bay(2), PieceMovementTiming.DuringHand, false);
            var piece = Piece(FirstPiece, Asset<PositionInteractionPresenterAsset>(), true);
            var position = piece.GetComponent<SpiritTablePiecePosition>();
            var interaction = Session(Catalog(Piece(FirstPiece, definition))).Snapshot.PieceInteractions.Single();
            var presenter = piece.Presenter.Create(piece, definition, context);

            presenter.Bind(interaction);
            presenter.Layout();
            Assert.That(position.IsBound, Is.True);
            Assert.That(position.Position, Is.EqualTo(PiecePosition.Bay(2)));
            Assert.That(Vector3.Distance(piece.transform.position, RecordingContext.Point(2)), Is.LessThan(.001f));

            Drag(position, RecordingContext.Point(2), RecordingContext.Point(0) + new Vector3(10, 5, 0));
            var move = context.Inputs.Single();
            Assert.That(move.Key, Is.EqualTo(FirstPiece + " " + InteractionId));
            Assert.That(((MovePieceInput)move.Value).Position, Is.EqualTo(PiecePosition.Bay(0)));

            presenter.Bind(null);
            Assert.That(position.IsBound, Is.False, "A snapshot without the interaction leaves the prop unusable.");
            presenter.Bind(interaction);
            presenter.Dispose();
            Assert.That(position.IsBound, Is.False, "A disposed presenter leaves the prop unusable.");
        }

        /**
         * Checks a presenter asset refuses a missing interaction, another kind of interaction and an interaction that is not
         * its prop's, and the position kind refuses a prop without its position component / fails if a prop is bound to an
         * interaction it does not present or cannot show.
         */
        [Test]
        public void PresenterAssetRejectsAnInteractionItDoesNotPresent()
        {
            var piece = Piece(FirstPiece, Asset<PositionInteractionPresenterAsset>(), true);
            Assert.Throws<ArgumentNullException>(() => piece.Presenter.Create(piece, null, context));
            Assert.Throws<InvalidOperationException>(() => piece.Presenter.Create(piece, new ToggleInteractionDefinition(InteractionId),
                context), "Another kind of interaction is rejected.");
            var other = new PositionInteractionDefinition("position.other", PiecePosition.Neutral, PieceMovementTiming.DuringHand);
            Assert.Throws<InvalidOperationException>(() => piece.Presenter.Create(piece, other, context));
            var bare = Piece(FirstPiece, piece.Presenter, false);
            var definition = new PositionInteractionDefinition(InteractionId, PiecePosition.Neutral, PieceMovementTiming.DuringHand);
            Assert.Throws<InvalidOperationException>(() => bare.Presenter.Create(bare, definition, context));
        }

        /**
         * Checks the screen's piece presenters create one presenter per prop whose interaction the encounter has, with that
         * definition, bind each to its own interaction, lay out and dispose them all, and create none for a prop the
         * encounter lacks / fails if a prop is bound through another prop's interaction, a prop the encounter lacks gets a
         * presenter or a prop without IDs or presenter is accepted.
         */
        [Test]
        public void ScreenBindsEachPropThroughItsOwnPresenter()
        {
            var recording = Asset<RecordingPresenterAsset>();
            recording.Log = log;
            var first = Piece(FirstPiece, recording, false);
            var second = Piece(SecondPiece, recording, false);
            var absent = Piece(AbsentPiece, recording, false);
            var firstDefinition = new PositionInteractionDefinition(InteractionId, PiecePosition.Bay(1), PieceMovementTiming.DuringHand);
            var secondDefinition = new PositionInteractionDefinition(InteractionId, PiecePosition.Bay(3), PieceMovementTiming.DuringHand);
            var catalog = Catalog(Piece(FirstPiece, firstDefinition), Piece(SecondPiece, secondDefinition));
            var session = Session(catalog);
            var encounter = catalog.GetEncounter(TestContent.EncounterId);

            var presenters = TablePiecePresenters.Create(new[] { first, absent, second }, encounter, context);
            Assert.That(presenters.Presenters.Count, Is.EqualTo(2));
            Assert.That(recording.Received.Count, Is.EqualTo(2));
            Assert.That(recording.Received[0], Is.SameAs(firstDefinition), "Each presenter is created with its prop's interaction.");
            Assert.That(recording.Received[1], Is.SameAs(secondDefinition), "Each presenter is created with its prop's interaction.");
            presenters.Bind(session.Snapshot);
            presenters.Layout();
            presenters.Dispose();
            Assert.That(log, Is.EqualTo(new[]
            {
                FirstPiece + " create", SecondPiece + " create",
                FirstPiece + " bind " + FirstPiece + " " + InteractionId, SecondPiece + " bind " + SecondPiece + " " + InteractionId,
                FirstPiece + " layout", SecondPiece + " layout", FirstPiece + " dispose", SecondPiece + " dispose"
            }));

            log.Clear();
            var unpresented = Piece(SecondPiece, null, false);
            Assert.Throws<InvalidOperationException>(() => TablePiecePresenters.Create(new[] { first, unpresented }, encounter, context));
            Assert.That(log, Is.EqualTo(new[] { FirstPiece + " create", FirstPiece + " dispose" }),
                "A failed creation disposes the presenters it created.");
            var unnamed = Piece("", recording, false);
            Assert.Throws<InvalidOperationException>(() => TablePiecePresenters.Create(new[] { unnamed }, encounter, context));
        }

        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            created.Add(asset);
            return asset;
        }

        /**
         * A prop with the piece's ID and the test interaction ID under a table rect; with a position component that settles at
         * once when one is asked for.
         */
        private SpiritTablePieceView Piece(string pieceId, PieceInteractionPresenterAsset presenter, bool positioned)
        {
            var table = new GameObject("Table", typeof(RectTransform));
            created.Add(table);
            var prop = new GameObject("Table Piece " + pieceId, typeof(RectTransform));
            prop.transform.SetParent(table.transform, false);
            var piece = prop.AddComponent<SpiritTablePieceView>();
            using (var serialized = new SerializedObject(piece))
            {
                serialized.FindProperty("pieceId").stringValue = pieceId;
                serialized.FindProperty("interactionId").stringValue = InteractionId;
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

        private static TablePieceDefinition Piece(string pieceId, PieceInteractionDefinition interaction)
            => new TablePieceDefinition(pieceId, new[] { interaction });

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
            public ToggleInteractionDefinition(string id) : base(id) { }

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

            public override bool CanInteract(string pieceId, string interactionId) => true;

            public override bool Interact(string pieceId, string interactionId, PieceInteractionInput input)
            {
                Inputs.Add(new KeyValuePair<string, PieceInteractionInput>(pieceId + " " + interactionId, input));
                return true;
            }

            public override Vector3 BayPoint(int bay, Vector2 placement) => Point(bay);
        }

        /** A presenter kind that records, under its prop's piece ID, what it is created with and asked to do. */
        private sealed class RecordingPresenterAsset : PieceInteractionPresenterAsset
        {
            [NonSerialized] public List<string> Log;
            [NonSerialized] public readonly List<PieceInteractionDefinition> Received = new List<PieceInteractionDefinition>();

            public override PieceInteractionPresenter Create(SpiritTablePieceView piece, PieceInteractionDefinition interaction,
                PieceInteractionPresenterContext context)
            {
                Log.Add(piece.PieceId + " create");
                Received.Add(interaction);
                return new RecordingPresenter(Log, piece.PieceId);
            }
        }

        private sealed class RecordingPresenter : PieceInteractionPresenter
        {
            private readonly List<string> log;
            private readonly string pieceId;

            public RecordingPresenter(List<string> log, string pieceId)
            {
                this.log = log;
                this.pieceId = pieceId;
            }

            public override void Bind(PieceInteractionSnapshot interaction)
                => log.Add(pieceId + " bind " + interaction.PieceId + " " + interaction.InteractionId);

            public override void Layout() => log.Add(pieceId + " layout");

            public override void Dispose() => log.Add(pieceId + " dispose");
        }
    }
}
