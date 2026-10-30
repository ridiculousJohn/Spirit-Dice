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
     * A second capture shows the stage 3 charms introduction over the stage 3 charm offer at 540 x 960 and 443 x 960, then the offer
     * after Continue, under DICE_SPIRIT_CAPTURE_ROOT/stage3-intro. The fixture is explicit: it runs only when a run selects
     * it by name, never in a run of the whole suite. The binding behaviour it shows is tested by
     * DieBindingPresentationPlayTests, the introduction by EncounterCharmsIntroductionPlayTests.
     */
    [Explicit("Review capture tool; select it by name with DICE_SPIRIT_CAPTURE_ROOT set.")]
    [Category("Capture")]
    public sealed class BoundTreeReviewCapturePlayTests
    {
        private const int Width = 540, NarrowWidth = 443, Height = 960;
        // The Bound Tree journey's beats where the nymph first speaks and where the spirit takes up the challenge.
        private const int NymphBeat = 3, ChallengeBeat = 9;

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
            var journey = ContentReady.JourneyWithRule<DieBindingRuleDefinition>(controller);
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            ContentReady.SeedJourneyPrerequisites(controller, journey);
            controller.SelectEncounter(journey);
            yield return ContentReady.Routed(controller);
            var view = ContentReady.EncounterView(controller);
            Assert.That(view.TablePresentation, Is.SameAs(journey.TableSceneAt(0)));

            yield return ShowBeat(controller, view, 0);
            yield return Capture(root, "a-first-beat.png");

            yield return ShowBeat(controller, view, NymphBeat);
            Assert.That(view.HostPortrait.State, Is.EqualTo(SpiritPresentationState.Waiting), "The host is plain bark before the challenge.");
            yield return Capture(root, "b-nymph-beat.png");

            yield return ShowBeat(controller, view, ChallengeBeat);
            Assert.That(view.HostPortrait.State, Is.EqualTo(SpiritPresentationState.Arrival), "The face splits open at the challenge.");
            yield return Capture(root, "c-challenge-beat.png");

            yield return ShowBeat(controller, view, journey.IndexOfEncounter(journey.Encounters.First().Identity) - 1);
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
            var journey = ContentReady.JourneyWithRule<DieBindingRuleDefinition>(controller);
            yield return EncounterCharmsIntroductionPlayTests.EnterShortEncounter(controller, controller.Catalog, journey);
            yield return EncounterCharmsIntroductionPlayTests.ReachIntroducedOffer(controller, journey);
            var view = ContentReady.EncounterView(controller);
            yield return SizePortrait(Width);
            EncounterCharmsIntroductionPlayTests.AssertIntroductionOpen(controller, view, journey);
            AssertIntroductionLayout(view);
            yield return EncounterCharmsIntroductionPlayTests.AssertOfferCovered(view, true);
            yield return Capture(root, "a-stage3-introduction-" + Width + "x" + Height + ".png");
            yield return SizePortrait(NarrowWidth);
            AssertIntroductionLayout(view);
            yield return Capture(root, "a-stage3-introduction-" + NarrowWidth + "x" + Height + ".png", NarrowWidth);
            yield return SizePortrait(Width);

            view.EncounterCharmsIntroduction.Screen.BeginButton.onClick.Invoke();
            Assert.That(view.EncounterCharmsIntroduction.IsOpen, Is.False);
            yield return EncounterCharmsIntroductionPlayTests.AssertOfferCovered(view, false);
            yield return Capture(root, "b-stage3-offer-after-continue-" + Width + "x" + Height + ".png");
            LogAssert.NoUnexpectedReceived();
        }

        /** Sizes the portrait to the given width at the authoring height. */
        private static IEnumerator SizePortrait(int width)
        {
            var portrait = ContentReady.Portrait();
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(width, Height);
            portrait.anchoredPosition = Vector2.zero;
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        /** The narration, family row, scope sentence and Continue are complete and stacked without overlap. */
        private static void AssertIntroductionLayout(SpiritEncounterView view)
        {
            Canvas.ForceUpdateCanvases();
            var screen = view.EncounterCharmsIntroduction.Screen;
            var title = Child<Text>(screen, "EncounterCharmsTitle");
            var scope = Child<Text>(screen, "EncounterCharmsScope");
            bool narrated = screen.Narration.gameObject.activeInHierarchy;
            var stack = new[] { title.rectTransform, narrated ? screen.Narration.rectTransform : null, scope.rectTransform,
                (RectTransform)screen.Scroll.transform, (RectTransform)screen.BeginButton.transform }.Where(rect => rect != null).ToArray();
            for (int index = 1; index < stack.Length; ++index) AssertBelow(stack[index - 1], stack[index]);
            foreach (var text in new[] { title, narrated ? screen.Narration : null, scope, screen.BeginButton.GetComponentInChildren<Text>() })
                if (text != null) AssertCompleteText(text);
            var viewport = (RectTransform)screen.Scroll.viewport.transform;
            var row = (RectTransform)screen.Rows[0].transform;
            var rowCorners = new Vector3[4];
            var viewportCorners = new Vector3[4];
            row.GetWorldCorners(rowCorners);
            viewport.GetWorldCorners(viewportCorners);
            Assert.That(rowCorners[0].y, Is.GreaterThanOrEqualTo(viewportCorners[0].y - .5f), "The family row fits its list.");
            Assert.That(rowCorners[2].y, Is.LessThanOrEqualTo(viewportCorners[2].y + .5f));
            var portraitCorners = new Vector3[4];
            view.Portrait.GetWorldCorners(portraitCorners);
            var screenCorners = new Vector3[4];
            screen.Root.GetWorldCorners(screenCorners);
            Assert.That(Vector3.Distance(screenCorners[0], portraitCorners[0]), Is.LessThan(.01f));
            Assert.That(Vector3.Distance(screenCorners[2], portraitCorners[2]), Is.LessThan(.01f),
                "The introduction covers the whole portrait.");
        }

        private static void AssertBelow(RectTransform upper, RectTransform lower)
        {
            var upperCorners = new Vector3[4];
            var lowerCorners = new Vector3[4];
            upper.GetWorldCorners(upperCorners);
            lower.GetWorldCorners(lowerCorners);
            Assert.That(upperCorners[0].y, Is.GreaterThanOrEqualTo(lowerCorners[2].y - .01f), upper.name + " overlaps " + lower.name);
        }

        private static void AssertCompleteText(Text label)
        {
            var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            var rendered = label.cachedTextGenerator;
            Assert.That(rendered.Populate(label.text, settings), Is.True, label.name);
            settings.fontSize = label.resizeTextForBestFit ? rendered.fontSizeUsedForBestFit : label.fontSize;
            settings.resizeTextForBestFit = false;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            using (var complete = new TextGenerator())
            {
                Assert.That(complete.Populate(label.text, settings), Is.True, label.name);
                Assert.That(rendered.vertexCount, Is.EqualTo(complete.vertexCount), label.name + " truncates text.");
            }
            Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(label.preferredHeight - .5f),
                label.name + " is clipped vertically.");
        }

        private static T Child<T>(SpiritEncounterCharmsScreen screen, string name) where T : Component
        {
            var result = screen.GetComponentsInChildren<T>(true).FirstOrDefault(value => value.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
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

        /** Advances the journey to the beat at the index and waits until the view shows it with its motion complete. */
        private static IEnumerator ShowBeat(SpiritEncounterController controller, SpiritEncounterView view, int index)
        {
            Assert.That(index, Is.InRange(0, controller.SelectedJourney.Beats.Count - 1));
            string id = "beat " + index;
            float deadline = Time.realtimeSinceStartup + 15;
            while (controller.BeatIndex != index && Time.realtimeSinceStartup < deadline)
            {
                controller.AdvanceJourney();
                yield return null;
            }
            Assert.That(controller.BeatIndex, Is.EqualTo(index));
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
