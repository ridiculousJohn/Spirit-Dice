using System;
using System.Collections.Generic;
using System.Reflection;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class SpiritTablePieceTests
    {
        private const string InteractionId = "position";
        private static readonly Vector3[] BayPoints =
        {
            new Vector3(0, 0, 0), new Vector3(100, 0, 0), new Vector3(200, 0, 0), new Vector3(300, 0, 0), new Vector3(400, 0, 0)
        };
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        private readonly List<PiecePosition> requests = new List<PiecePosition>();
        private SpiritTablePiecePosition piece;
        private PositionInteractionDefinition definition;
        private bool movable;
        private bool submit;
        private bool accept;
        private PiecePosition engine;

        [SetUp]
        public void SetUp()
        {
            var parent = new GameObject("Table", typeof(RectTransform));
            created.Add(parent);
            var anchor = new GameObject("NeutralAnchor", typeof(RectTransform));
            anchor.transform.SetParent(parent.transform, false);
            anchor.transform.position = new Vector3(-300, 0, 0);
            var prop = new GameObject("Table Piece", typeof(RectTransform));
            prop.transform.SetParent(parent.transform, false);
            piece = prop.AddComponent<SpiritTablePiecePosition>();
            Set(piece, "neutralAnchor", (RectTransform)anchor.transform);
            Set(piece, "snapRadius", 90f);
            Set(piece, "settleDuration", 0f);
            // Bay 3 is not an allowed position, so releases near it must not request it.
            definition = new PositionInteractionDefinition(InteractionId, PiecePosition.Neutral, PieceMovementTiming.DuringHand,
                true, DiceValues.AllMask & ~(1 << 3));
            movable = submit = accept = true;
            engine = PiecePosition.Neutral;
            requests.Clear();
            piece.Initialize(() => movable, RequestMove);
            piece.Bind(PiecePosition.Neutral, definition);
            piece.Layout(BayPoints);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            created.Clear();
        }

        /** Checks a release picks the nearest candidate within the radius, the earlier on a tie / fails if it snaps from outside the radius. */
        [Test]
        public void NearestPositionSnapsWithinTheRadiusAndPrefersTheEarlierTie()
        {
            var candidates = new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(20, 0) };
            Assert.That(SpiritTablePieceLayout.FindNearest(new Vector2(9, 3), candidates, 5), Is.EqualTo(1));
            Assert.That(SpiritTablePieceLayout.FindNearest(new Vector2(5, 0), candidates, 5), Is.EqualTo(0));
            Assert.That(SpiritTablePieceLayout.FindNearest(new Vector2(40, 0), candidates, 5), Is.EqualTo(-1));
        }

        /**
         * Checks a point maps through the image that shows it, the front image where both do and the nearer image beyond both /
         * fails if a point reads the wrong slice of the room texture.
         */
        [Test]
        public void SplitBackgroundsMapPointsThroughTheImageThatShowsThem()
        {
            // The table shows the lower 40 percent of the room texture below the room image, which shows the rest.
            var table = new Rect(-270, -480, 540, 600);
            var tableUv = new Rect(0, 0, 1, .4f);
            var room = new Rect(-270, 120, 540, 360);
            var roomUv = new Rect(0, .4f, 1, .6f);
            AssertCoordinate(new Vector2(0, -180), table, tableUv, room, roomUv, new Vector2(.5f, .2f));
            AssertCoordinate(new Vector2(-270, 300), table, tableUv, room, roomUv, new Vector2(0, .7f));
            // Beyond both images, the nearer image extrapolates its own slice.
            AssertCoordinate(new Vector2(0, 540), table, tableUv, room, roomUv, new Vector2(.5f, 1.1f));
            // Where both contain the point, the front image is what the player sees.
            Assert.That(SpiritTablePieceLayout.TextureCoordinate(new Vector2(0, 0), table, tableUv, new Vector2(0, 0), table, roomUv),
                Is.EqualTo(new Vector2(.5f, .32f)).Using(Vector2EqualityComparer));
        }

        /** Checks the distance from a point to a rectangle's nearest edge / fails if a point outside reads as inside or mismeasured. */
        [Test]
        public void DistanceOutsideMeasuresToTheNearestEdge()
        {
            Assert.That(SpiritTablePieceLayout.DistanceOutside(new Vector2(3, 4), new Rect(-1, -1, 1, 1)), Is.EqualTo(5).Within(.0001f));
            Assert.That(SpiritTablePieceLayout.DistanceOutside(new Vector2(-.5f, -.5f), new Rect(-1, -1, 1, 1)), Is.Zero);
        }

        /** Checks a slot's viewport rectangle maps into the tray frame / fails if slots are placed off their tray position. */
        [Test]
        public void SlotPlacementMapsThroughTheTrayViewportIntoItsFrame()
        {
            var frame = new Rect(-200, -100, 400, 200);
            var slot = new Rect(.2f, .5f, .1f, .2f);
            Assert.That(SpiritTablePieceLayout.ViewportPoint(frame, slot, new Vector2(.5f, 0)),
                Is.EqualTo(new Vector2(-200 + 400 * .25f, -100 + 200 * .5f)).Using(Vector2EqualityComparer));
            Assert.That(SpiritTablePieceLayout.ViewportPoint(frame, slot, new Vector2(1, 1)),
                Is.EqualTo(new Vector2(-200 + 400 * .3f, -100 + 200 * .7f)).Using(Vector2EqualityComparer));
        }

        /**
         * Checks releasing the prop requests the nearest allowed new position, the neutral spot included / fails if a
         * disallowed bay or the current position is requested.
         */
        [Test]
        public void ReleasingThePropRequestsOnlyTheNearestAllowedNewPosition()
        {
            AssertAt(new Vector3(-300, 0, 0));
            Drag(new Vector2(-300, 0), new Vector2(208, 20));
            Assert.That(requests, Is.EqualTo(new[] { PiecePosition.Bay(2) }));
            AssertAt(BayPoints[2]);

            // Bay 3 is disallowed and the neighbors are outside the snap radius, so the prop returns.
            Drag(new Vector2(200, 0), new Vector2(300, 0));
            Assert.That(requests.Count, Is.EqualTo(1));
            AssertAt(BayPoints[2]);

            // Dropping onto the current position is not a move.
            Drag(new Vector2(200, 0), new Vector2(230, 0));
            Assert.That(requests.Count, Is.EqualTo(1));
            AssertAt(BayPoints[2]);

            Drag(new Vector2(200, 0), new Vector2(-260, -40));
            Assert.That(requests, Is.EqualTo(new[] { PiecePosition.Bay(2), PiecePosition.Neutral }));
            AssertAt(new Vector3(-300, 0, 0));
        }

        /**
         * Checks a refused, unsubmitted, locked or unbound move leaves the prop at its engine position / fails if the prop
         * follows the pointer or keeps a position the engine did not accept.
         */
        [Test]
        public void RefusedOrRejectedMovesReturnThePropAndLockedPiecesDoNotFollowThePointer()
        {
            accept = false;
            Drag(new Vector2(-300, 0), new Vector2(100, 0));
            Assert.That(requests, Is.EqualTo(new[] { PiecePosition.Bay(1) }));
            Assert.That(piece.Position, Is.EqualTo(PiecePosition.Neutral));
            AssertAt(new Vector3(-300, 0, 0));

            submit = false;
            Drag(new Vector2(-300, 0), new Vector2(0, 0));
            Assert.That(requests.Count, Is.EqualTo(1), "An unsubmitted request does not reach the engine.");
            Assert.That(piece.Position, Is.EqualTo(PiecePosition.Neutral));
            AssertAt(new Vector3(-300, 0, 0));

            submit = accept = true;
            movable = false;
            piece.OnBeginDrag(Pointer(new Vector2(-300, 0)));
            Assert.That(piece.IsDragging, Is.False);
            piece.OnDrag(Pointer(new Vector2(100, 0)));
            piece.OnEndDrag(Pointer(new Vector2(100, 0)));
            Assert.That(requests.Count, Is.EqualTo(1));
            AssertAt(new Vector3(-300, 0, 0));

            piece.Unbind();
            movable = true;
            Drag(new Vector2(-300, 0), new Vector2(100, 0));
            Assert.That(requests.Count, Is.EqualTo(1), "An unbound prop cannot be moved.");
        }

        /** Checks emissive images scale their authored alpha, never exceed it and restore it / fails if emission overwrites the authored color. */
        [Test]
        public void EmissiveImagesFollowTheLightAndRestoreTheirAuthoredColor()
        {
            var flame = new GameObject("Flame", typeof(RectTransform), typeof(Image));
            created.Add(flame);
            var image = flame.GetComponent<Image>();
            image.color = new Color(1, .8f, .4f, .8f);
            var view = piece.GetComponent<SpiritTablePieceView>();
            Set(view, "emissiveImages", new Graphic[] { image });
            view.SetEmission(.5f);
            Assert.That(image.color.a, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(image.color.g, Is.EqualTo(.8f).Within(.0001f));
            view.SetEmission(2f);
            Assert.That(image.color.a, Is.EqualTo(.8f).Within(.0001f), "Emission never exceeds the authored alpha.");
            view.SetEmission(0f);
            view.RestoreEmission();
            Assert.That(image.color, Is.EqualTo(new Color(1, .8f, .4f, .8f)));
            Assert.That(view.LightIntensity, Is.EqualTo(1f), "A piece only brightens its light while flaring.");
        }

        /** Checks binding needs a neutral anchor only when neutral is allowed / fails if a prop binds to a neutral position without one. */
        [Test]
        public void BindingRequiresAnAuthoredNeutralAnchorWhenNeutralIsAllowed()
        {
            Set(piece, "neutralAnchor", null);
            Assert.Throws<InvalidOperationException>(() => piece.Bind(PiecePosition.Neutral, definition));
            Assert.DoesNotThrow(() => piece.Bind(PiecePosition.Bay(0), new PositionInteractionDefinition(InteractionId,
                PiecePosition.Bay(0), PieceMovementTiming.DuringHand, false)));
        }

        private bool RequestMove(PiecePosition position)
        {
            if (!submit) return false;
            requests.Add(position);
            // The view submits synchronously; the resulting snapshot rebinds the accepted or unchanged position.
            if (accept) engine = position;
            piece.Bind(engine, definition);
            return true;
        }

        private void Drag(Vector2 from, Vector2 to)
        {
            piece.OnBeginDrag(Pointer(from));
            piece.OnDrag(Pointer((from + to) * .5f));
            piece.OnDrag(Pointer(to));
            piece.OnEndDrag(Pointer(to));
            piece.Layout(BayPoints);
        }

        private static PointerEventData Pointer(Vector2 position) => new PointerEventData(null) { position = position, pointerId = 0 };

        private void AssertAt(Vector3 expected)
            => Assert.That(Vector3.Distance(piece.transform.position, expected), Is.LessThan(.001f), piece.transform.position.ToString());

        private static void AssertCoordinate(Vector2 point, Rect front, Rect frontUv, Rect back, Rect backUv, Vector2 expected)
            => Assert.That(SpiritTablePieceLayout.TextureCoordinate(point, front, frontUv, point, back, backUv),
                Is.EqualTo(expected).Using(Vector2EqualityComparer), point.ToString());

        private static readonly Comparison<Vector2> Vector2EqualityComparer =
            (left, right) => Vector2.Distance(left, right) < .0001f ? 0 : 1;

        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
