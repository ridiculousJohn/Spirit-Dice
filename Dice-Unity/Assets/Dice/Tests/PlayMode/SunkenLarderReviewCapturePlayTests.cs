using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
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
     * Review capture of the Sunken Larder reached through the campaign. The first capture shows the journey's arrival
     * picture, the old woman waiting over the pot, her looking up as she first speaks, the invitation with the rot card and
     * the pre-encounter charms screen, then the table after the opening roll at the 540 x 960 authoring width and the
     * narrow 443 x 960 width, a selection (a kept 1 shows the wither warning), the rot step while the rerolled dice are in
     * the air and the table once the reroll has landed. The second capture plays a copy of the encounter whose stages have
     * a target of 1 and whose first stage has at least WitherRerolls rerolls: it keeps the lowest die through rerolls of
     * the other four until it withers, shows the mould closing and the withered die, then plays the hand and shows the first
     * stage's charm offer, recording its options in offer.txt. The third capture plays the same copy: it keeps the highest
     * die through MouldRerolls rerolls of the other four, showing each rot step with the kept die turning under its mould
     * and the mould grown after each reroll, then rerolls that die and shows its mould crumbling as it is thrown, recording
     * the die's mould level at each frame in levels.txt. It writes PNGs under DICE_SPIRIT_CAPTURE_ROOT/table,
     * DICE_SPIRIT_CAPTURE_ROOT/wither and DICE_SPIRIT_CAPTURE_ROOT/mould and
     * is ignored without it; it is a capture, not a regression test. The fixture is explicit: it runs only when a run
     * selects it by name, never in a run of the whole suite. The rot behaviour it shows is tested by RotRuleTests and
     * RotPresenterTests.
     */
    [Explicit("Review capture tool; select it by name with DICE_SPIRIT_CAPTURE_ROOT set.")]
    [Category("Capture")]
    public sealed class SunkenLarderReviewCapturePlayTests
    {
        private const int Width = 540, NarrowWidth = 443, Height = 960;
        // The Sunken Larder journey's beats where she stirs the pot without looking up and where she first speaks.
        private const int WaitingBeat = 3, OldWomanBeat = 4;
        // Rerolls the wither capture's first stage has at least, enough to wither a kept 6.
        private const int WitherRerolls = 6;
        // Real seconds after a reroll is committed at which the rot step is captured with the rerolled dice in the air.
        private const float RotStepCaptureDelay = .2f;
        // Rerolls through which the mould capture keeps its die, fewer than a kept 4 needs to wither.
        private const int MouldRerolls = 3;
        // Real seconds after a reroll of the mouldy die at which its mould is captured crumbling, early and late.
        private const float CrumbleEarlyDelay = .1f, CrumbleLateDelay = .25f;

        [UnityTest]
        public IEnumerator SunkenLarderTableReviewCapture()
        {
            string root = Fresh("table");
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.JourneyAtBoard();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = ContentReady.JourneyWithRule<RotRuleDefinition>(controller);
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            ContentReady.SeedJourneyPrerequisites(controller, journey);
            controller.SelectEncounter(journey);
            yield return ContentReady.Routed(controller);
            var view = ContentReady.EncounterView(controller);

            yield return ShowBeat(controller, view, 0);
            yield return Capture(root, "a-arrival-picture.png");

            yield return ShowBeat(controller, view, WaitingBeat);
            Assert.That(view.HostPortrait.State, Is.EqualTo(SpiritPresentationState.Waiting), "She stirs without looking up.");
            yield return Capture(root, "b-by-the-fire-waiting.png");

            yield return ShowBeat(controller, view, OldWomanBeat);
            Assert.That(view.HostPortrait.State, Is.EqualTo(SpiritPresentationState.Arrival), "She looks up as she speaks.");
            yield return Capture(root, "c-the-old-woman-arrival.png");

            yield return ShowBeat(controller, view, journey.IndexOfEncounter(journey.Encounters.First().Identity) - 1);
            yield return Capture(root, "d-invitation-" + Width + "x" + Height + ".png");
            yield return Capture(root, "d-invitation-" + NarrowWidth + "x" + Height + ".png", NarrowWidth);

            controller.ConfigureSeedPolicy(true, ContentReady.Seed);
            view.JourneyView.AcceptButton.onClick.Invoke();
            var charms = view.JourneyView.EncounterCharms;
            Assert.That(charms.IsOpen, Is.True, "Her additions are introduced before play.");
            yield return null;
            yield return Capture(root, "e-encounter-charms.png");
            charms.BeginButton.onClick.Invoke();
            yield return ContentReady.Routed(controller);
            view = ContentReady.EncounterView(controller);
            yield return WaitIdle(controller);
            Assert.That(controller.Snapshot, Is.Not.Null, controller.Status.ToString());
            yield return Capture(root, "f-table-at-rest-" + Width + "x" + Height + ".png");
            yield return Capture(root, "f-table-at-rest-" + NarrowWidth + "x" + Height + ".png", NarrowWidth);

            // Selecting two dice; no die shows mould before a rot step, and a kept 1 shows the wither warning.
            controller.ToggleDie(0);
            controller.ToggleDie(1);
            yield return new WaitForSecondsRealtime(.5f);
            yield return Capture(root, "g-marks-beside-selection.png");

            controller.Reroll();
            yield return new WaitForSecondsRealtime(RotStepCaptureDelay);
            yield return Capture(root, "h-rot-step-during-reroll.png");
            yield return WaitIdle(controller);
            yield return new WaitForSecondsRealtime(.5f);
            yield return Capture(root, "i-after-reroll.png");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SunkenLarderWitherAndOfferCapture()
        {
            string root = Fresh("wither");
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.JourneyAtBoard();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = ContentReady.JourneyWithRule<RotRuleDefinition>(controller);
            var encounterId = journey.Encounters.First().Identity;
            var mechanical = WitheringFirstStage(controller.Catalog, encounterId);
            Assert.That(controller.InitializeJourney(mechanical, controller.JourneyCatalog, controller.SavePath), Is.True,
                controller.JourneyStatus.ToString());
            yield return ContentReady.EnterEncounter(controller, journey, ContentReady.Seed);
            var view = ContentReady.EncounterView(controller);

            var dice = controller.Snapshot.CurrentDice;
            int keep = Enumerable.Range(0, DiceValues.Count).OrderBy(bay => dice[bay]).First();
            int keepMask = 1 << keep;
            var mould = Visual(view, keepMask);
            bool midCaptured = false;
            while (controller.Snapshot.Rot().WitheredMask == 0 && controller.Snapshot.RerollsRemaining > 0)
            {
                Select(controller, DiceValues.AllMask & ~keepMask);
                bool withers = controller.Snapshot.CurrentDice[keep] == 1;
                controller.Reroll();
                if (withers)
                {
                    float deadline = Time.realtimeSinceStartup + 10;
                    while (mould.Growth < .4f && Time.realtimeSinceStartup < deadline) yield return null;
                    if (mould.Growth >= .4f && mould.Growth <= .95f)
                    {
                        yield return Capture(root, "a-mould-closing.png");
                        midCaptured = true;
                    }
                }
                yield return WaitIdle(controller);
            }
            Assert.That(controller.Snapshot.Rot().WitheredMask, Is.EqualTo(keepMask), "The kept die withers.");
            TestContext.WriteLine("Mould closing captured: " + midCaptured);
            yield return new WaitForSecondsRealtime(1f);
            yield return Capture(root, "b-withered-die-" + Width + "x" + Height + ".png");
            yield return Capture(root, "b-withered-die-" + NarrowWidth + "x" + Height + ".png", NarrowWidth);

            controller.PlayHand();
            yield return ContentReady.WaitUntil(() => !controller.IsBusy && controller.Snapshot.PendingReward != null,
                "The first stage's offer is pending.", ContentReady.SequenceTimeout);
            yield return new WaitForSecondsRealtime(.5f);
            var offer = controller.Snapshot.PendingReward;
            var additions = controller.Catalog.GetEncounter(encounterId).EncounterModifierIds;
            var report = new StringBuilder();
            report.AppendLine("Offer " + offer.Kind + " for stage index " + offer.TargetStageIndex);
            bool bogOffering = false;
            foreach (var option in offer.Options)
            {
                if (option.DefinitionId is ModifierDefinition.Id id)
                {
                    var definition = controller.Catalog.GetModifier(id);
                    bool addition = additions.Contains(id);
                    bogOffering |= addition;
                    report.AppendLine(ContentReady.Charms.GetName(definition) + " (" + ContentReady.Charms.GetFamilyName(definition) +
                        ")" + (addition ? " - encounter addition" : ""));
                }
                else report.AppendLine("Hand upgrade " + option.Category);
            }
            report.AppendLine("Contains an encounter addition (Bog Offering): " + bogOffering);
            File.WriteAllText(Path.Combine(root, "offer.txt"), report.ToString());
            TestContext.WriteLine(report.ToString());
            yield return Capture(root, "c-stage1-offer.png");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SunkenLarderProgressiveMouldCapture()
        {
            string root = Fresh("mould");
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.JourneyAtBoard();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = ContentReady.JourneyWithRule<RotRuleDefinition>(controller);
            var mechanical = WitheringFirstStage(controller.Catalog, journey.Encounters.First().Identity);
            Assert.That(controller.InitializeJourney(mechanical, controller.JourneyCatalog, controller.SavePath), Is.True,
                controller.JourneyStatus.ToString());
            yield return ContentReady.EnterEncounter(controller, journey, ContentReady.Seed);
            var view = ContentReady.EncounterView(controller);

            var dice = controller.Snapshot.CurrentDice;
            int keep = Enumerable.Range(0, DiceValues.Count).OrderByDescending(bay => dice[bay]).First();
            Assert.That(dice[keep], Is.GreaterThan(MouldRerolls), "The kept die rots through every reroll without withering.");
            int keepMask = 1 << keep;
            var mould = Visual(view, keepMask);
            var levels = new StringBuilder();
            levels.AppendLine("Kept die: bay " + keep + ", opening face " + dice[keep]);
            yield return Capture(root, "a-opening-roll.png");
            for (int reroll = 1; reroll <= MouldRerolls; ++reroll)
            {
                Select(controller, DiceValues.AllMask & ~keepMask);
                controller.Reroll();
                yield return new WaitForSecondsRealtime(RotStepCaptureDelay);
                string turning = "b" + reroll + "-reroll-" + reroll + "-rot-step-turning.png";
                levels.AppendLine(turning + ": mark " + mould.Mark.ToString("F3") + " (target " + mould.MarkTarget + ")");
                yield return Capture(root, turning);
                yield return WaitIdle(controller);
                yield return new WaitForSecondsRealtime(.6f);
                Assert.That(controller.Snapshot.Rot().Counts[keep], Is.EqualTo(reroll));
                string landed = "c" + reroll + "-after-reroll-" + reroll + ".png";
                levels.AppendLine(landed + ": count " + reroll + ", mark " + mould.Mark.ToString("F3") + ", counts [" +
                    controller.Snapshot.Rot().Counts + "]");
                yield return Capture(root, landed);
            }

            Select(controller, keepMask);
            controller.Reroll();
            yield return new WaitForSecondsRealtime(CrumbleEarlyDelay);
            levels.AppendLine("d1-crumbling-early.png: mark " + mould.Mark.ToString("F3"));
            yield return Capture(root, "d1-crumbling-early.png");
            yield return new WaitForSecondsRealtime(CrumbleLateDelay - CrumbleEarlyDelay);
            levels.AppendLine("d2-crumbling-late.png: mark " + mould.Mark.ToString("F3"));
            yield return Capture(root, "d2-crumbling-late.png");
            yield return WaitIdle(controller);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(controller.Snapshot.Rot().Counts[keep], Is.Zero);
            Assert.That(mould.Mark, Is.Zero, "The thrown die lands clean.");
            levels.AppendLine("e-after-crumble.png: counts [" + controller.Snapshot.Rot().Counts + "]");
            yield return Capture(root, "e-after-crumble.png");
            File.WriteAllText(Path.Combine(root, "levels.txt"), levels.ToString());
            TestContext.WriteLine(levels.ToString());
            LogAssert.NoUnexpectedReceived();
        }

        /** Selects exactly the masked dice for the next reroll. */
        private static void Select(SpiritEncounterController controller, int mask)
        {
            for (int bay = 0; bay < DiceValues.Count; ++bay)
                if (((controller.SelectedMask ^ mask) & (1 << bay)) != 0) controller.ToggleDie(bay);
        }

        private static string Fresh(string folder)
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Review capture only; set DICE_SPIRIT_CAPTURE_ROOT to write it.");
            root = Path.Combine(root, folder);
            Assert.That(Directory.Exists(root), Is.False, "Capture output must be fresh.");
            Directory.CreateDirectory(root);
            return root;
        }

        /** A copy of the catalog in which the encounter's stages have a target of 1 and its first stage at least WitherRerolls rerolls. */
        private static MechanicalCatalog WitheringFirstStage(MechanicalCatalog authored, EncounterDefinition.Id encounterId)
        {
            var definition = authored.GetEncounter(encounterId);
            var withering = new EncounterDefinition(definition.Identity,
                definition.Stages.Select((stage, index) => new StageDefinition(1, stage.StartingHands,
                    index == 0 ? Math.Max(stage.StartingRerolls, WitherRerolls) : stage.StartingRerolls, stage.ExitBehavior)),
                definition.StartingUpgrades, definition.StartingModifiers, definition.RewardPolicy, definition.Rules,
                definition.UnlockMilestones, definition.EncounterModifierIds,
                definition.UnlockRewardModifierIds, definition.Pieces, definition.EncounterModifiersFirstClearedStageNumber);
            return new MechanicalCatalog("sunken-larder-wither-capture.v1", authored.Hands,
                authored.Encounters.Select(value => value.Identity == definition.Identity ? withering : value), authored.Modifiers);
        }

        private static DieBindingVisual Visual(SpiritEncounterView view, int mask)
        {
            for (int bay = 0; bay < DiceValues.Count; ++bay)
                if ((mask & (1 << bay)) != 0) return view.DiceTray.Dice[bay].GetComponentInChildren<DieBindingVisual>(true);
            throw new ArgumentException("No die in the mask.", nameof(mask));
        }

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
            var cameraRoot = new GameObject("Sunken Larder Review Capture Camera");
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
                TestContext.WriteLine("Sunken Larder review capture: " + path);
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
