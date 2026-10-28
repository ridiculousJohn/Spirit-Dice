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
    /**
     * Review capture of the bound tree encounter reached through the campaign: the journey's first beat, the nymph beat
     * with the host as plain bark, the challenge beat after the face has split open, the invitation with the binding card,
     * and the encounter table after the opening roll at the 540 x 960 authoring width and the narrow 443 x 960 width. It
     * then captures the binding dice visuals at 540 x 960: the opening roll's mark (the table capture), the roots part-way
     * grown and settled after a player reroll with the next mark, that run loaded from the investigation board showing its
     * marked and bound dice at once, the gold selection beside a bound die and on a marked die, and the table after Play
     * Hand once the bindings have released. It writes PNGs under DICE_SPIRIT_CAPTURE_ROOT and is ignored without it; it is a capture, not a regression test.
     * A second capture shows the stage 3 charms introduction over the stage 3 charm offer at 540 x 960, then the offer
     * after Continue, under DICE_SPIRIT_CAPTURE_ROOT/stage3-intro. The fixture is explicit: it runs only when a run selects
     * it by name, never in a run of the whole suite. The binding behaviour it shows is tested by
     * DieBindingPresentationPlayTests, the introduction by EncounterCharmsIntroductionPlayTests.
     */
    [Explicit("Review capture tool; select it by name with DICE_SPIRIT_CAPTURE_ROOT set.")]
    [Category("Capture")]
    public sealed class BoundTreeReviewCapturePlayTests
    {
        private const string EncounterId = "encounter.nymph.bound_tree";
        private const int Width = 540, NarrowWidth = 443, Height = 960;

        [UnityTest]
        public IEnumerator BoundTreeSliceReviewCapture()
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Review capture only; set DICE_SPIRIT_CAPTURE_ROOT to write it.");
            root = Path.Combine(root, "bound-tree-review");
            Assert.That(Directory.Exists(root), Is.False, "Capture output must be fresh.");
            Directory.CreateDirectory(root);

            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = controller.JourneyCatalog.Entries.Single(value => !value.IsStoryOnly && value.EncounterId == EncounterId);
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            ContentReady.SeedJourneyPrerequisites(controller, journey.Id);
            controller.SelectEncounter(journey.EncounterId);
            yield return ContentReady.Routed(controller);
            var view = ContentReady.EncounterView(controller);
            Assert.That(view.TablePresentation, Is.SameAs(journey.TablePresentation));

            yield return ShowBeat(controller, view, journey.Beats[0].Id);
            yield return Capture(root, "a-first-beat.png");

            yield return ShowBeat(controller, view, "nymph");
            Assert.That(view.HostPortrait.State, Is.EqualTo(SpiritPresentationState.Waiting), "The host is plain bark before the challenge.");
            yield return Capture(root, "b-nymph-beat.png");

            yield return ShowBeat(controller, view, "challenge");
            Assert.That(view.HostPortrait.State, Is.EqualTo(SpiritPresentationState.Arrival), "The face splits open at the challenge.");
            yield return Capture(root, "c-challenge-beat.png");

            yield return ShowBeat(controller, view, "invitation");
            yield return Capture(root, "d-invitation-beat.png");

            controller.AcceptEncounter();
            yield return ContentReady.Routed(controller);
            view = ContentReady.EncounterView(controller);
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False, "Opening encounter presentation did not complete.");
            Assert.That(controller.Snapshot, Is.Not.Null, controller.Status.ToString());
            yield return null;
            yield return Capture(root, "e-table-after-opening-roll-" + Width + "x" + Height + ".png");
            yield return Capture(root, "e-table-after-opening-roll-" + NarrowWidth + "x" + Height + ".png", NarrowWidth);
            int marked = controller.Snapshot.Binding().MarkedMask;

            // Reroll one other die, so the marked die binds at its kept face and another die is marked.
            int rerolled = Lowest(DiceValues.AllMask & ~marked);
            controller.ToggleDie(Bay(rerolled));
            controller.Reroll();
            var binding = Visual(view, marked);
            deadline = Time.realtimeSinceStartup + 10;
            while (binding.Growth < .4f && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(binding.Growth, Is.InRange(.4f, .95f), "The roots are captured while they grow.");
            yield return Capture(root, "f-binding-mid-growth.png");
            yield return WaitIdle(controller);
            // The bind flare falls back to rest before the settled capture.
            yield return new WaitForSecondsRealtime(2f);
            int bound = controller.Snapshot.Binding().BoundMask;
            int nextMark = controller.Snapshot.Binding().MarkedMask;
            yield return Capture(root, "g-bound-and-next-mark.png");

            controller.Save();
            var saved = controller.Snapshot;

            // Loading the saved run from the investigation board shows its marked and bound dice at once, as Continue does.
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            yield return null;
            controller.Load();
            yield return ContentReady.Routed(controller);
            yield return null;
            view = ContentReady.EncounterView(controller);
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(saved.Revision), controller.Status.ToString());
            yield return Capture(root, "h-loaded-marked-and-bound.png");

            // Select the marked die and an unbound die beside the bound one; tapping the bound die changes nothing.
            int beside = Bay(bound) > 0 ? 1 << (Bay(bound) - 1) : 1 << (Bay(bound) + 1);
            if (beside == nextMark) beside = Bay(bound) < DiceValues.Count - 1 ? 1 << (Bay(bound) + 1) : 1 << (Bay(bound) - 1);
            controller.ToggleDie(Bay(bound));
            controller.ToggleDie(Bay(nextMark));
            if (beside != nextMark) controller.ToggleDie(Bay(beside));
            yield return null;
            yield return Capture(root, "i-selection-beside-bound-and-on-mark.png");
            controller.ToggleDie(Bay(nextMark));
            if (beside != nextMark) controller.ToggleDie(Bay(beside));

            // The release is captured on the table as soon as the roots have withdrawn after scoring, before a cleared stage
            // would replace the table with its rewards.
            controller.PlayHand();
            var released = Visual(view, bound);
            deadline = Time.realtimeSinceStartup + 30;
            while ((released.Growth > 0f || released.GrowthTarget > 0f) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(released.Growth, Is.Zero, "The capture shows the withdrawn roots.");
            yield return Capture(root, "j-after-play-hand-released.png");
            yield return WaitIdle(controller);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BoundTreeStageThreeIntroductionCapture()
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Review capture only; set DICE_SPIRIT_CAPTURE_ROOT to write it.");
            root = Path.Combine(root, "stage3-intro");
            Assert.That(Directory.Exists(root), Is.False, "Capture output must be fresh.");
            Directory.CreateDirectory(root);

            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = controller.JourneyCatalog.Entries.Single(value => !value.IsStoryOnly && value.EncounterId == EncounterId);
            yield return EncounterCharmsIntroductionPlayTests.EnterShortEncounter(controller, controller.Catalog, journey);
            yield return EncounterCharmsIntroductionPlayTests.ReachIntroducedOffer(controller, EncounterId);
            var view = ContentReady.EncounterView(controller);
            yield return EncounterCharmsIntroductionPlayTests.SizePortrait(Width);
            EncounterCharmsIntroductionPlayTests.AssertIntroductionOpen(controller, view, journey);
            EncounterCharmsIntroductionPlayTests.AssertIntroductionLayout(view);
            yield return EncounterCharmsIntroductionPlayTests.AssertOfferCovered(view, true);
            yield return Capture(root, "a-stage3-introduction-" + Width + "x" + Height + ".png");

            view.EncounterCharmsIntroduction.Screen.BeginButton.onClick.Invoke();
            Assert.That(view.EncounterCharmsIntroduction.IsOpen, Is.False);
            yield return EncounterCharmsIntroductionPlayTests.AssertOfferCovered(view, false);
            yield return Capture(root, "b-stage3-offer-after-continue-" + Width + "x" + Height + ".png");
            LogAssert.NoUnexpectedReceived();
        }

        private static DieBindingVisual Visual(SpiritEncounterView view, int mask)
            => view.DiceTray.Dice[Bay(mask)].GetComponentInChildren<DieBindingVisual>(true);

        private static int Bay(int mask)
        {
            for (int bay = 0; bay < DiceValues.Count; ++bay)
                if ((mask & (1 << bay)) != 0) return bay;
            throw new ArgumentException("No die in the mask.", nameof(mask));
        }

        private static int Lowest(int mask) => mask & -mask;

        private static IEnumerator WaitIdle(SpiritEncounterController controller)
        {
            yield return null;
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False, "Presentation did not complete.");
            yield return null;
        }

        /** Advances the journey to the named beat and waits until the view shows it with its motion complete. */
        private static IEnumerator ShowBeat(SpiritEncounterController controller, SpiritEncounterView view, string id)
        {
            Assert.That(controller.SelectedJourney.Beats.Any(beat => beat.Id == id), Is.True, id);
            float deadline = Time.realtimeSinceStartup + 15;
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Id != id && Time.realtimeSinceStartup < deadline)
            {
                controller.AdvanceJourney();
                yield return null;
            }
            Assert.That(controller.SelectedJourney.Beats[controller.BeatIndex].Id, Is.EqualTo(id));
            yield return ContentReady.Routed(controller);
            deadline = Time.realtimeSinceStartup + 15;
            while ((view.JourneyView.VisibleBeatIndex != controller.BeatIndex || view.JourneyView.IsAnimating) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(view.JourneyView.VisibleBeatIndex, Is.EqualTo(controller.BeatIndex), id);
            Assert.That(view.JourneyView.IsAnimating, Is.False, id);
            yield return null;
        }

        /** Renders the portrait canvas at the given width (540 x 960 by default) through a capture camera and writes it as a PNG. */
        private static IEnumerator Capture(string root, string filename, int width = Width)
        {
            var portrait = ContentReady.Portrait();
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(width, Height);
            portrait.anchoredPosition = Vector2.zero;
            var canvas = ContentReady.PresentationRoot().GetComponentInChildren<Canvas>(true);
            var scaler = canvas.GetComponent<CanvasScaler>();
            bool scalerEnabled = scaler != null && scaler.enabled;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousPlane = canvas.planeDistance;
            float previousScale = canvas.scaleFactor;
            var cameraRoot = new GameObject("Bound Tree Review Capture Camera");
            var camera = cameraRoot.AddComponent<Camera>();
            var target = new RenderTexture(width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D readback = null;
            RenderTexture previousTarget = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                target.Create();
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -100);
                camera.orthographic = true;
                camera.orthographicSize = Height / 2f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 200;
                camera.cullingMask = ~(1 << SpiritDiceTray.RenderLayer);
                camera.backgroundColor = new Color(.04f, .055f, .05f, 1);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.allowHDR = camera.allowMSAA = false;
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                if (scaler != null) scaler.enabled = false;
                canvas.scaleFactor = 1;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                readback = new Texture2D(width, Height, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, width, Height), 0, 0);
                readback.Apply();
                int lit = readback.GetPixels32().Count(pixel => pixel.r > 45 || pixel.g > 45 || pixel.b > 45);
                Assert.That(lit, Is.GreaterThan(width * Height / 20), filename + ": the portrait must render.");
                string path = Path.Combine(root, filename);
                Assert.That(File.Exists(path), Is.False, "Capture output must be fresh.");
                File.WriteAllBytes(path, readback.EncodeToPNG());
                TestContext.WriteLine("Bound tree review capture: " + path);
            }
            finally
            {
                canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; canvas.planeDistance = previousPlane;
                if (scaler != null) scaler.enabled = scalerEnabled;
                if (scaler == null || !scalerEnabled) canvas.scaleFactor = previousScale;
                RenderTexture.active = previousTarget; GL.sRGBWrite = previousSrgb;
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(cameraRoot);
                if (readback != null) UnityEngine.Object.Destroy(readback);
            }
            yield return null;
        }
    }
}
