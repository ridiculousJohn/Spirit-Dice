using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class SpiritTrayCompositionPlayTests
    {
        [UnityTest]
        public IEnumerator AllThreeEncounterTablesCaptureGameplayAtBothPortraitWidths()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = ContentReady.EncounterView(controller);
            Assert.That(controller.JourneyCatalog.Entries.Count(value => !value.IsStoryOnly), Is.EqualTo(4));
            Camera previousCamera = null;
            RenderTexture previousTexture = null;
            foreach (var journey in controller.JourneyCatalog.Entries.Where(value => !value.IsStoryOnly))
            {
                controller.ReturnToInvestigation();
                yield return ContentReady.Routed(controller);
                view = ContentReady.EncounterView(controller);
                ContentReady.SeedJourneyPrerequisites(controller, journey.Id);
                controller.SelectEncounter(journey.EncounterId);
                yield return ContentReady.Routed(controller);
                view = ContentReady.EncounterView(controller);
                while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                    controller.AdvanceJourney();
                controller.AcceptEncounter();
                yield return ContentReady.Routed(controller);
                view = ContentReady.EncounterView(controller);
                float deadline = Time.realtimeSinceStartup + 30;
                while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(controller.IsBusy, Is.False);
                yield return null;
                Assert.That(view.TrayScene.Presentation, Is.SameAs(journey.TablePresentation.TrayPresentation));
                Assert.That(view.DiceTray.RenderCamera, Is.SameAs(view.ModifierHistory.Renderer.RenderCamera));
                Assert.That(view.DiceTray.Texture, Is.SameAs(view.ModifierHistory.Renderer.Texture));
                Assert.That(view.TrayScene.RenderRoot.GetComponentsInChildren<Camera>().Length, Is.EqualTo(1));
                if (previousCamera != null || previousTexture != null)
                    Assert.Fail("Switching tables retained a previous shared camera or texture.");
                var faceNormals = new[] { Vector3.forward, Vector3.up, Vector3.right,
                    Vector3.left, Vector3.down, Vector3.back };
                for (int i = 0; i < DiceValues.Count; ++i)
                {
                    var slot = view.TrayScene.Geometry.DiceSlots[i];
                    var die = ContentReady.Die(view.DiceTray, i);
                    Assert.That(die, Is.Not.Null, "Die " + i);
                    Assert.That(die.IsChildOf(slot), Is.True, "The authored die belongs to its tray slot.");
                    Vector3 resultNormal = die.TransformDirection(faceNormals[controller.Snapshot.CurrentDice[i] - 1]);
                    // The result faces upward; a shallow camera can favor a side face.
                    Assert.That(Vector3.Dot(resultNormal, slot.up), Is.GreaterThan(.999f), "Die " + i + " result face");
                    var camera = view.DiceTray.RenderCamera;
                    Vector3 towardCamera = camera.orthographic ? -camera.transform.forward
                        : (camera.transform.position - die.position).normalized;
                    Assert.That(Vector3.Dot(resultNormal, towardCamera), Is.GreaterThan(0f), "Die " + i + " result visibility");
                }
                var definition = new ModifierDefinition("test.capture.history", "test.capture.history", ModifierPhase.InitialRoll,
                    new AlwaysCondition(), new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 1) });
                var encounter = new EncounterDefinition("test.capture.tray", new[] { new StageDefinition(1000000, 3, 3) },
                    startingModifiers: new[] { definition.Id });
                var catalog = new MechanicalCatalog("test.capture.tray", controller.Catalog.Hands,
                    new[] { encounter }, new[] { definition });
                var contribution = EncounterSession.CreateEncounter(catalog, encounter.Id, 60).Snapshot.ModifierContributions[0];
                view.ModifierHistory.Clear();
                for (int i = 0; i < 6; ++i) view.ModifierHistory.Add(contribution, definition, false);
                foreach (int width in new[] { 540, 443 })
                {
                    var portrait = ContentReady.Portrait();
                    portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                    portrait.sizeDelta = new Vector2(width, 960);
                    portrait.anchoredPosition = Vector2.zero;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    var trayImage = view.GameplayHud.DiceTrayImage;
                    Assert.That(trayImage.texture, Is.SameAs(view.TrayScene.Texture));
                    Assert.That(view.ModifierHistory.GetComponentInParent<RawImage>(), Is.SameAs(trayImage));
                    Assert.That(view.ModifierHistory.GetComponentsInChildren<RawImage>(true), Is.Empty,
                        "History hit targets use the shared tray display.");
                    for (int index = 0; index < DiceValues.Count; ++index)
                    {
                        Rect bounds = view.DiceTray.GetSlotViewportRect(index);
                        var button = view.GetComponentsInChildren<Button>(true).Single(value => value.name == "DieButton" + (index + 1));
                        var hit = (RectTransform)button.transform;
                        Assert.That(Vector2.Distance(hit.anchorMin, bounds.min), Is.LessThan(.001f));
                        Assert.That(Vector2.Distance(hit.anchorMax, bounds.max), Is.LessThan(.001f));
                        Assert.That(hit.rect.width, Is.GreaterThan(0));
                        Assert.That(hit.rect.height, Is.GreaterThan(0));
                    }
                    yield return Capture(view, journey.EncounterId, width);
                }
                if (view.TablePieces.Count > 0) yield return CapturePieces(controller, view, journey.EncounterId);
                // Selection refreshes the history from the run, so these captures follow the history captures.
                yield return CaptureSelection(controller, view, journey.EncounterId);
                previousCamera = view.TrayScene.RenderCamera;
                previousTexture = view.TrayScene.Texture;
            }
            var presentationRoot = ContentReady.Router(controller).PresentationRoot;
            presentationRoot.SetActive(false);
            yield return null;
            Assert.That(view.TrayScene.RenderRoot.gameObject.activeInHierarchy, Is.False);
            presentationRoot.SetActive(true);
            yield return null;
            Assert.That(view.TrayScene.RenderRoot.gameObject.activeInHierarchy, Is.True);
            Assert.That(view.DiceTray.Texture, Is.SameAs(view.ModifierHistory.Renderer.Texture));
            LogAssert.NoUnexpectedReceived();
        }

        /** The merchant's candle draws and receives input above the tray and dice targets, below every overlay, and hides with the dice. */
        [UnityTest]
        public IEnumerator MerchantCandleDrawsAboveDiceAndBelowOverlays()
        {
            const string encounterId = "encounter.merchant.unquiet_cart";
            const int width = 540;
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = controller.JourneyCatalog.Entries.Single(value => !value.IsStoryOnly && value.EncounterId == encounterId);
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            ContentReady.SeedJourneyPrerequisites(controller, journey.Id);
            controller.SelectEncounter(journey.EncounterId);
            yield return ContentReady.Routed(controller);
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                controller.AdvanceJourney();
            controller.AcceptEncounter();
            yield return ContentReady.Routed(controller);
            var view = ContentReady.EncounterView(controller);
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            yield return null;
            var portrait = ContentReady.Portrait();
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(width, 960);
            portrait.anchoredPosition = Vector2.zero;

            var hud = view.GameplayHud;
            var piece = view.TablePieces.Single();
            var layer = (RectTransform)piece.transform.parent;
            Assert.That(layer.parent, Is.SameAs(hud.PlayingPanel), "The piece layer belongs to the gameplay surface.");
            Assert.That(layer.GetSiblingIndex(), Is.EqualTo(hud.DiceTrayFrame.GetSiblingIndex() + 1),
                "The piece layer follows the dice tray frame directly.");
            var dieButtons = Enumerable.Range(1, DiceValues.Count)
                .Select(index => view.GetComponentsInChildren<Button>(true).Single(value => value.name == "DieButton" + index)).ToArray();
            var below = new List<Transform> { hud.DiceTrayImage.transform, view.ModifierHistory.transform };
            below.AddRange(dieButtons.Select(button => button.transform));
            foreach (var lower in below) AssertDrawsBelow(lower, layer, lower.name);
            var scorePreview = view.BaseScoreLabel.transform;
            while (scorePreview.parent != hud.PlayingPanel) scorePreview = scorePreview.parent;
            var pause = view.GetComponentInChildren<SpiritPauseMenu>(true);
            var overlays = new Transform[]
            {
                hud.StageIntroPanel, view.CharmPopup.Root, pause.transform, view.OwnedCharmsPanel.Root,
                view.GetComponentInChildren<SpiritHandRanksPanel>(true).transform, view.RewardsPanel.Root,
                view.GetComponentInChildren<SpiritEncounterEndingView>(true).transform, view.JourneyView.transform,
                view.GetComponentInChildren<SpiritCollectionView>(true).transform, scorePreview,
                hud.SelectionLabel.transform, hud.RerollButton.transform, hud.PlayButton.transform,
                hud.CharmsButton.transform, hud.HandRanksButton.transform, hud.StatusLabel.transform
            };
            foreach (var overlay in overlays) AssertDrawsBelow(layer, overlay, overlay.name);

            var bay = PiecePosition.Bay(2);
            controller.InteractWithPiece(piece.PieceId, piece.InteractionId, new MovePieceInput(bay));
            deadline = Time.realtimeSinceStartup + 10;
            while ((controller.IsBusy || piece.Position != bay) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(piece.Position, Is.EqualTo(bay));
            yield return Settle(piece);
            Canvas.ForceUpdateCanvases();
            var candle = piece.GetComponentsInChildren<Graphic>().Single(graphic => graphic.raycastTarget);
            Rect candleRect = WorldRect(candle.rectTransform);
            yield return Capture(view, encounterId + "-candle-bay3-uncovered", width);

            // The stage intro over the live surface shows the order; playback hides the surface while the intro shows.
            var snapshot = controller.Snapshot;
            hud.StageIntroTitle.text = "STAGE 2 / " + snapshot.StageCount;
            hud.StageIntroDetail.fontSize = hud.StageIntroDetailFontSize;
            hud.StageIntroDetail.text = "CLEAR " + DiagnosticText.Number(snapshot.StartingThreshold) + " POINTS\n" +
                snapshot.StartingHands + " hands / " + snapshot.StartingRerolls + " rerolls";
            hud.StageIntroPanel.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Assert.That(WorldRect(hud.StageIntroPanel).Overlaps(candleRect), Is.True, "The stage intro covers the candle's bay.");
            yield return Capture(view, encounterId + "-stage-intro-over-candle", width);
            hud.PlayingPanel.gameObject.SetActive(false);
            Assert.That(piece.gameObject.activeInHierarchy, Is.False, "The candle hides with the dice tray.");
            yield return Capture(view, encounterId + "-stage-intro-playback", width, .125f);
            hud.StageIntroPanel.gameObject.SetActive(false);
            hud.PlayingPanel.gameObject.SetActive(true);
            yield return Settle(piece);

            // The merchant starts without charms, so the popup inspects a charm instance from a one-charm session.
            var charm = controller.Catalog.Modifiers.First();
            var charmEncounter = new EncounterDefinition("test.capture.candle-popup", new[] { new StageDefinition(1000000, 3, 3) },
                startingModifiers: new[] { charm.Id });
            var charmCatalog = new MechanicalCatalog("test.capture.candle-popup", controller.Catalog.Hands,
                new[] { charmEncounter }, new[] { charm });
            var instance = EncounterSession.CreateEncounter(charmCatalog, charmEncounter.Id, 60).Snapshot.Modifiers[0];
            view.CharmPopup.Show(instance.InstanceId, charm,
                SpiritCharmStatusPresentation.Create(instance, charm, EncounterPhase.Playing, null), EncounterPhase.Playing);
            Assert.That(view.IsCharmPopupOpen, Is.True);
            AssertDrawsBelow(layer, view.CharmPopup.Root, "opened charm popup");
            TestContext.WriteLine("Charm popup panel overlaps the candle: " + WorldRect(view.CharmPopup.Panel).Overlaps(candleRect));
            yield return Capture(view, encounterId + "-charm-popup-over-candle", width, .05f);
            view.CloseCharmPopup();

            hud.MenuButton.onClick.Invoke();
            Assert.That(pause.CloseButton.gameObject.activeInHierarchy, Is.True, "The pause menu opens.");
            TestContext.WriteLine("Pause menu overlaps the candle: " + WorldRect((RectTransform)pause.transform).Overlaps(candleRect));
            yield return Capture(view, encounterId + "-pause-menu-over-candle", width, .05f);
            pause.CloseButton.onClick.Invoke();
            Assert.That(pause.CloseButton.gameObject.activeInHierarchy, Is.False);
            yield return Settle(piece);

            // Grabbing the candle and carrying it over a die keeps the drag on the candle and never selects the die.
            Canvas.ForceUpdateCanvases();
            Vector3 grabWorld = WorldRect(candle.rectTransform).center;
            var grab = TopHit(RectTransformUtility.WorldToScreenPoint(null, grabWorld));
            Assert.That(ExecuteEvents.GetEventHandler<IDragHandler>(grab.gameObject), Is.SameAs(piece.gameObject));
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, grabWorld),
                pressPosition = RectTransformUtility.WorldToScreenPoint(null, grabWorld),
                button = PointerEventData.InputButton.Left,
                pointerId = -1,
                pointerCurrentRaycast = grab,
                pointerPressRaycast = grab
            };
            ExecuteEvents.Execute(piece.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(piece.gameObject, pointer, ExecuteEvents.beginDragHandler);
            Assert.That(piece.IsDragging, Is.True, "The candle accepts the drag.");
            Vector3 grabOffset = piece.transform.position - grabWorld;
            var targetDie = (RectTransform)dieButtons[1].transform;
            Vector2 dieScreen = RectTransformUtility.WorldToScreenPoint(null, WorldRect(targetDie).center);
            pointer.position = dieScreen;
            ExecuteEvents.Execute(piece.gameObject, pointer, ExecuteEvents.dragHandler);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = dieScreen }, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject), Is.SameAs(piece.gameObject),
                "The carried candle receives input over the die.");
            Assert.That(dieButtons.Select(button => button.gameObject),
                Has.No.Member(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)),
                "A tap or release on the candle never reaches a die target.");
            Assert.That(hits.Any(hit => ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject) == targetDie.gameObject),
                Is.True, "The die target lies under the carried candle.");
            yield return Capture(view, encounterId + "-candle-dragged-over-die2", width);
            // The capture renders through a camera; world positions return to overlay space once the canvas updates.
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(piece.IsDragging, Is.True, "The candle stays in hand through the capture.");

            var destination = PiecePosition.Bay(1);
            RectTransform trayFrame = hud.DiceTrayImage.rectTransform;
            Vector3 bayWorld = trayFrame.TransformPoint(SpiritTablePieceLayout.ViewportPoint(trayFrame.rect,
                view.DiceTray.GetSlotViewportRect(destination.Index), piece.BayPlacement));
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, bayWorld - grabOffset);
            ExecuteEvents.Execute(piece.gameObject, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(piece.gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(piece.IsDragging, Is.False);
            deadline = Time.realtimeSinceStartup + 10;
            while ((controller.IsBusy || piece.Position != destination) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(piece.Position, Is.EqualTo(destination), "Releasing near bay 2 moves the candle there; it rests at " +
                piece.Position.Kind + " " + piece.Position.Index);
            Assert.That(controller.SelectedMask, Is.Zero, "Dragging the candle over a die never selects it.");
            ContentReady.AssertDiceSelection(view, 0, "after the candle drag");
            yield return Settle(piece);
            LogAssert.NoUnexpectedReceived();
        }

        /** Within one canvas, a later transform in hierarchy order draws and raycasts above an earlier one. */
        private static void AssertDrawsBelow(Transform lower, Transform upper, string label)
        {
            var canvas = lower.GetComponentInParent<Canvas>(true);
            Assert.That(canvas, Is.Not.Null, label);
            Assert.That(upper.GetComponentInParent<Canvas>(true), Is.SameAs(canvas), label + " shares the candle's canvas");
            Assert.That(CompareHierarchyOrder(lower, upper), Is.LessThan(0), lower.name + " must draw below " + label);
        }

        private static int CompareHierarchyOrder(Transform a, Transform b)
        {
            var left = SiblingPath(a);
            var right = SiblingPath(b);
            for (int i = 0; i < Mathf.Min(left.Count, right.Count); ++i)
                if (left[i] != right[i]) return left[i].CompareTo(right[i]);
            return left.Count.CompareTo(right.Count);
        }

        private static List<int> SiblingPath(Transform transform)
        {
            var path = new List<int>();
            for (var current = transform; current != null; current = current.parent) path.Add(current.GetSiblingIndex());
            path.Reverse();
            return path;
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static RaycastResult TopHit(Vector2 screen)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
            Assert.That(hits, Is.Not.Empty, "Nothing receives input at " + screen);
            return hits[0];
        }

        /** Places each table piece at its neutral spot and on the outer and middle bays at both portrait widths. */
        private static IEnumerator CapturePieces(SpiritEncounterController controller, SpiritEncounterView view, string encounterId)
        {
            var trayImage = view.GameplayHud.DiceTrayImage;
            foreach (var piece in view.TablePieces)
                foreach (var position in new[] { PiecePosition.Neutral, PiecePosition.Bay(0), PiecePosition.Bay(2), PiecePosition.Bay(4) })
                {
                    if (piece.Position != position)
                    {
                        controller.InteractWithPiece(piece.PieceId, piece.InteractionId, new MovePieceInput(position));
                        float deadline = Time.realtimeSinceStartup + 10;
                        while ((controller.IsBusy || piece.Position != position) && Time.realtimeSinceStartup < deadline) yield return null;
                    }
                    Assert.That(piece.Position, Is.EqualTo(position), piece.PieceId);
                    foreach (int width in new[] { 540, 443 })
                    {
                        var portrait = ContentReady.Portrait();
                        portrait.sizeDelta = new Vector2(width, 960);
                        yield return Settle(piece);
                        Canvas.ForceUpdateCanvases();
                        RectTransform frame = trayImage.rectTransform;
                        Vector2 local = frame.InverseTransformPoint(piece.transform.position);
                        if (position.Kind == PiecePositionKind.DiceBay)
                        {
                            Rect slot = view.DiceTray.GetSlotViewportRect(position.Index);
                            Vector2 expected = SpiritTablePieceLayout.ViewportPoint(frame.rect, slot, piece.BayPlacement);
                            Assert.That(Vector2.Distance(local, expected), Is.LessThan(.5f), piece.PieceId + " rests at its bay point");
                            // A 2D prop cannot pass behind a 3D die, so it stands above the die and below the resource row.
                            Assert.That(local.y, Is.GreaterThanOrEqualTo(frame.rect.yMin + frame.rect.height * slot.yMax),
                                piece.PieceId + " base must clear the top of die " + (position.Index + 1) + " at " + width);
                            var corners = new Vector3[4];
                            ((RectTransform)piece.transform).GetWorldCorners(corners);
                            float pieceTop = frame.InverseTransformPoint(corners[1]).y;
                            view.GameplayHud.HandsPanel.GetWorldCorners(corners);
                            float resourcesBottom = frame.InverseTransformPoint(corners[0]).y;
                            Assert.That(pieceTop, Is.LessThanOrEqualTo(resourcesBottom),
                                piece.PieceId + " must stay below the hands and rerolls row at " + width);
                        }
                        else
                            for (int bay = 0; bay < DiceValues.Count; ++bay)
                            {
                                Rect slot = view.DiceTray.GetSlotViewportRect(bay);
                                float left = frame.rect.xMin + frame.rect.width * slot.xMin;
                                float right = frame.rect.xMin + frame.rect.width * slot.xMax;
                                Assert.That(local.x < left || local.x > right, Is.True,
                                    piece.PieceId + " neutral spot must not stand above bay " + (bay + 1) + " at " + width);
                            }
                        string label = position.Kind == PiecePositionKind.Neutral ? "neutral" : "bay" + (position.Index + 1);
                        yield return Capture(view, encounterId + "-" + piece.PieceId + "-" + label, width);
                    }
                }
        }

        /** Selects an outer and the middle die, the widest and narrowest slots, at both portrait widths. */
        private static IEnumerator CaptureSelection(SpiritEncounterController controller, SpiritEncounterView view, string encounterId)
        {
            const int selectedMask = 1 << 0 | 1 << 2;
            controller.ToggleDie(0);
            controller.ToggleDie(2);
            Assert.That(controller.SelectedMask, Is.EqualTo(selectedMask));
            foreach (int width in new[] { 540, 443 })
            {
                var portrait = ContentReady.Portrait();
                portrait.sizeDelta = new Vector2(width, 960);
                yield return null;
                ContentReady.AssertDiceSelection(view, selectedMask, "at " + width);
                yield return Capture(view, encounterId + "-selected", width);
            }
            controller.ToggleDie(0);
            controller.ToggleDie(2);
            Assert.That(controller.SelectedMask, Is.Zero);
            yield return null;
        }

        /** Waits until the prop has eased into its bound position for the current layout. */
        private static IEnumerator Settle(SpiritTablePieceView piece)
        {
            Vector3 previous = piece.transform.position;
            int stable = 0;
            float deadline = Time.realtimeSinceStartup + 5;
            while (stable < 3 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                stable = (piece.transform.position - previous).sqrMagnitude < 1e-6f ? stable + 1 : 0;
                previous = piece.transform.position;
            }
            Assert.That(stable, Is.EqualTo(3), piece.PieceId + " must settle.");
        }

        private static IEnumerator Capture(SpiritEncounterView view, string encounterId, int width, float minimumLitFraction = .25f)
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) yield break;
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "shared-tray-" + encounterId + "-" + width + "x960.png");
            Assert.That(File.Exists(path), Is.False, "Capture output must be fresh.");
            var canvas = ContentReady.PresentationRoot().GetComponentInChildren<Canvas>(true);
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousPlane = canvas.planeDistance;
            var cameraRoot = new GameObject("Tray Capture Camera");
            var camera = cameraRoot.AddComponent<Camera>();
            var target = new RenderTexture(width, 960, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D readback = null;
            RenderTexture previousTarget = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                target.Create();
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -100);
                camera.orthographic = true;
                camera.orthographicSize = 480;
                camera.nearClipPlane = .1f; camera.farClipPlane = 200;
                camera.cullingMask = ~(1 << SpiritDiceTray.RenderLayer);
                camera.backgroundColor = new Color(.04f, .055f, .05f, 1);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.allowHDR = camera.allowMSAA = false;
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                readback = new Texture2D(width, 960, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, width, 960), 0, 0);
                readback.Apply();
                int lit = readback.GetPixels32().Count(pixel => pixel.r > 45 || pixel.g > 45 || pixel.b > 45);
                // A hidden or dimmed play area leaves darker artwork, so those captures pass their own lower fraction.
                Assert.That(lit, Is.GreaterThan(width * 960 * minimumLitFraction),
                    minimumLitFraction >= .25f ? "Gameplay artwork must fill the captured portrait."
                        : "The capture must render the portrait behind the hidden or covered play area.");
                File.WriteAllBytes(path, readback.EncodeToPNG());
                TestContext.WriteLine("Tray composition capture: " + path);
            }
            finally
            {
                canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; canvas.planeDistance = previousPlane;
                RenderTexture.active = previousTarget; GL.sRGBWrite = previousSrgb;
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(cameraRoot);
                if (readback != null) UnityEngine.Object.Destroy(readback);
            }
        }
    }
}
