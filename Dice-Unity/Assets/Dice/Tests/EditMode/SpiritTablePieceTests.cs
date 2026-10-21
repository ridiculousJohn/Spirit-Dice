using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class SpiritTablePieceTests
    {
        private const string PieceId = "piece.candle";
        private const string InteractionId = "position";
        private static readonly Vector3[] BayPoints =
        {
            new Vector3(0, 0, 0), new Vector3(100, 0, 0), new Vector3(200, 0, 0), new Vector3(300, 0, 0), new Vector3(400, 0, 0)
        };
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        private readonly List<PiecePosition> requests = new List<PiecePosition>();
        private SpiritTablePieceView piece;
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
            var prop = new GameObject("Candle", typeof(RectTransform));
            prop.transform.SetParent(parent.transform, false);
            piece = prop.AddComponent<SpiritTablePieceView>();
            Set(piece, "pieceId", PieceId);
            Set(piece, "interactionId", InteractionId);
            Set(piece, "neutralAnchor", (RectTransform)anchor.transform);
            Set(piece, "snapRadius", 90f);
            Set(piece, "settleDuration", 0f);
            // Bay 3 is not an allowed position, so releases near it must not request it.
            definition = new PositionInteractionDefinition(InteractionId, PiecePosition.Neutral, PieceMovementTiming.DuringHand,
                true, DiceValues.AllMask & ~(1 << 3));
            movable = submit = accept = true;
            engine = PiecePosition.Neutral;
            requests.Clear();
            piece.Initialize(_ => movable, RequestMove);
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

        [Test]
        public void NearestPositionSnapsWithinTheRadiusAndPrefersTheEarlierTie()
        {
            var candidates = new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(20, 0) };
            Assert.That(SpiritTablePieceLayout.FindNearest(new Vector2(9, 3), candidates, 5), Is.EqualTo(1));
            Assert.That(SpiritTablePieceLayout.FindNearest(new Vector2(5, 0), candidates, 5), Is.EqualTo(0));
            Assert.That(SpiritTablePieceLayout.FindNearest(new Vector2(40, 0), candidates, 5), Is.EqualTo(-1));
            Assert.That(SpiritTablePieceLayout.FindNearest(new Vector2(25, 0), candidates, 5), Is.EqualTo(2), "The radius is inclusive.");
            Assert.That(SpiritTablePieceLayout.FindNearest(Vector2.zero, Array.Empty<Vector2>(), 5), Is.EqualTo(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => SpiritTablePieceLayout.FindNearest(Vector2.zero, candidates, -1));
        }

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
            AssertCoordinate(new Vector2(0, 120), table, tableUv, room, roomUv, new Vector2(.5f, .4f));
            // Beyond both images, the nearer image extrapolates its own slice.
            AssertCoordinate(new Vector2(0, -540), table, tableUv, room, roomUv, new Vector2(.5f, -.04f));
            AssertCoordinate(new Vector2(0, 540), table, tableUv, room, roomUv, new Vector2(.5f, 1.1f));
            // Where both contain the point, the front image is what the player sees.
            Assert.That(SpiritTablePieceLayout.TextureCoordinate(new Vector2(0, 0), table, tableUv, new Vector2(0, 0), table, roomUv),
                Is.EqualTo(new Vector2(.5f, .32f)).Using(Vector2EqualityComparer));
            Assert.That(SpiritTablePieceLayout.DistanceOutside(new Vector2(3, 4), new Rect(-1, -1, 1, 1)), Is.EqualTo(5).Within(.0001f));
        }

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

            // A release far from every position returns the prop without an action.
            Drag(new Vector2(200, 0), new Vector2(200, 500));
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

        [Test]
        public void EmissiveImagesFollowTheLightAndRestoreTheirAuthoredColor()
        {
            var flame = new GameObject("Flame", typeof(RectTransform), typeof(Image));
            created.Add(flame);
            var image = flame.GetComponent<Image>();
            image.color = new Color(1, .8f, .4f, .8f);
            Set(piece, "emissiveImages", new Graphic[] { image });
            piece.SetEmission(.5f);
            Assert.That(image.color.a, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(image.color.g, Is.EqualTo(.8f).Within(.0001f));
            piece.SetEmission(2f);
            Assert.That(image.color.a, Is.EqualTo(.8f).Within(.0001f), "Emission never exceeds the authored alpha.");
            piece.SetEmission(0f);
            piece.RestoreEmission();
            Assert.That(image.color, Is.EqualTo(new Color(1, .8f, .4f, .8f)));
            Assert.That(piece.LightIntensity, Is.EqualTo(1f), "A piece only brightens its light while flaring.");
        }

        [Test]
        public void BindingRequiresAnAuthoredNeutralAnchorWhenNeutralIsAllowed()
        {
            Set(piece, "neutralAnchor", null);
            Assert.Throws<InvalidOperationException>(() => piece.Bind(PiecePosition.Neutral, definition));
            Assert.DoesNotThrow(() => piece.Bind(PiecePosition.Bay(0), new PositionInteractionDefinition(InteractionId,
                PiecePosition.Bay(0), PieceMovementTiming.DuringHand, false)));
            Set(piece, "pieceId", "");
            Assert.Throws<InvalidOperationException>(() => piece.Initialize(_ => true, (_, __) => true));
        }

        [Test]
        public void MerchantCandleBindsToItsEncounterPieceWithItsLightUnderTheNeutralCandle()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(
                "Assets/Dice/Content/Presentation/HauntedMerchant/MerchantUnquietCartTable.asset");
            Assert.That(table, Is.Not.Null);
            var light = table.PieceLights.Single();
            var catalog = TestContent.BuildStandardCatalog();
            var encounter = catalog.GetEncounter("encounter.merchant.unquiet_cart");
            var snapshot = EncounterSession.CreateEncounter(catalog, encounter.Id, 42).Snapshot;
            var scene = EditorSceneManager.OpenPreviewScene(AssetDatabase.GUIDToAssetPath(table.Scene.AssetGUID));
            try
            {
                var view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpiritEncounterView>(true)).Single();
                var candle = view.TablePieces.Single();
                Assert.That(candle.PieceId, Is.EqualTo(light.PieceId));
                Assert.That(candle.InteractionId, Is.EqualTo(light.InteractionId));
                var interaction = snapshot.PieceInteractions.Single(value =>
                    value.PieceId == candle.PieceId && value.InteractionId == candle.InteractionId);
                var position = ((PiecePositionState)interaction.State).Position;
                Assert.That(position, Is.EqualTo(PiecePosition.Neutral));
                var definition = (PositionInteractionDefinition)encounter.Pieces.Single(value => value.Id == candle.PieceId)
                    .Interactions.Single(value => value.Id == candle.InteractionId);

                // The piece layer belongs to the gameplay surface and follows the dice tray frame directly, so the prop
                // receives drags before the dice touch targets beneath it and draws below the HUD's popups.
                Transform frame = candle.transform.parent;
                Assert.That(frame.parent, Is.SameAs(view.GameplayHud.PlayingPanel));
                Assert.That(frame.GetSiblingIndex(), Is.EqualTo(view.GameplayHud.DiceTrayFrame.GetSiblingIndex() + 1));
                Assert.That(view.GameplayHud.DiceTrayFrame.parent, Is.SameAs(view.GameplayHud.PlayingPanel));
                Assert.That(view.GameplayHud.transform.parent, Is.SameAs(view.Portrait));
                Assert.That(candle.GetComponentsInChildren<Graphic>(true).Any(graphic => graphic.raycastTarget), Is.True);

                var neutral = (RectTransform)typeof(SpiritTablePieceView)
                    .GetField("neutralAnchor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(candle);
                Assert.That(neutral, Is.Not.Null);
                SceneLightLayer sceneLight = table.SceneLighting.Lights[light.LightIndex];
                Vector2 footprintCenter = BrightestCenter(sceneLight.Contribution);
                Vector2? neutralCoordinate = null;
                foreach (float width in new[] { 540f, 443f })
                {
                    view.Portrait.anchorMin = view.Portrait.anchorMax = new Vector2(.5f, .5f);
                    view.Portrait.sizeDelta = new Vector2(width, 960);
                    view.Portrait.anchoredPosition = Vector2.zero;
                    // A preview scene neither sizes nor scales its overlay canvas, so the test gives it unit scale and recalculates rects.
                    view.Canvas.transform.localScale = Vector3.one;
                    foreach (var rect in view.GetComponentsInChildren<RectTransform>(true)) rect.ForceUpdateRectTransforms();
                    candle.Bind(position, definition);
                    candle.Layout(BayPoints);
                    Assert.That(candle.IsBound, Is.True);
                    Assert.That(Vector3.Distance(candle.transform.position, neutral.position), Is.LessThan(.001f), "Width " + width);
                    RectTransform front = view.TableImage.rectTransform, back = view.RoomImage.rectTransform;
                    Vector3 world = candle.transform.position;
                    Vector2 coordinate = SpiritTablePieceLayout.TextureCoordinate(front.InverseTransformPoint(world), front.rect,
                        view.TableImage.uvRect, back.InverseTransformPoint(world), back.rect, view.RoomImage.uvRect);
                    // The neutral spot is registered to the artwork, so it maps to one room-texture point at every width.
                    if (neutralCoordinate == null) neutralCoordinate = coordinate;
                    Assert.That(Vector2.Distance(coordinate, neutralCoordinate.Value), Is.LessThan(.0005f),
                        "Width " + width + ": " + coordinate.ToString("F5"));
                    // The light moves by the prop's displacement from its reference, so its footprint center stays at the candle's base.
                    Vector2 footprint = footprintCenter + sceneLight.UvOffset + (coordinate - light.ReferencePosition);
                    Assert.That(Vector2.Distance(footprint, coordinate), Is.LessThan(.002f),
                        "Width " + width + ": footprint " + footprint.ToString("F5") + ", candle " + coordinate.ToString("F5"));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        /** Normalized center of the brightest pixels of a light contribution, read from its source file. */
        private static Vector2 BrightestCenter(Texture2D contribution)
        {
            Assert.That(contribution, Is.Not.Null);
            var decoded = new Texture2D(2, 2);
            try
            {
                Assert.That(decoded.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(contribution))), Is.True);
                Color32[] pixels = decoded.GetPixels32();
                int peak = pixels.Max(pixel => Math.Max(pixel.r, Math.Max(pixel.g, pixel.b)));
                Assert.That(peak, Is.GreaterThan(0));
                double x = 0, y = 0;
                int count = 0;
                for (int i = 0; i < pixels.Length; ++i)
                {
                    if (Math.Max(pixels[i].r, Math.Max(pixels[i].g, pixels[i].b)) < peak * .99f) continue;
                    x += i % decoded.width + .5;
                    y += i / decoded.width + .5;
                    ++count;
                }
                // Texture2D rows start at the bottom, matching normalized room-texture coordinates.
                return new Vector2((float)(x / count / decoded.width), (float)(y / count / decoded.height));
            }
            finally { UnityEngine.Object.DestroyImmediate(decoded); }
        }

        private bool RequestMove(SpiritTablePieceView view, PiecePosition position)
        {
            Assert.That(view, Is.SameAs(piece));
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
