using System;
using System.Collections;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
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

        private static IEnumerator Capture(SpiritEncounterView view, string encounterId, int width)
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
                Assert.That(readback.GetPixels32().Count(pixel => pixel.r > 45 || pixel.g > 45 || pixel.b > 45),
                    Is.GreaterThan(width * 960 / 4), "Gameplay artwork must fill the captured portrait.");
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
