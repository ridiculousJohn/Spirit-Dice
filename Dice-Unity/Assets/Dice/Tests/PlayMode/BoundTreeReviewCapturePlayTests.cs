using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
     * after Continue, under DICE_SPIRIT_CAPTURE_ROOT/stage3-intro. A third capture loses the encounter's first hand and shows
     * the nymph mid-flinch and then back in her sob after the flinch, under DICE_SPIRIT_CAPTURE_ROOT/defeat-flinch. A fourth
     * capture shows the ambient moss, mist and creatures, under DICE_SPIRIT_CAPTURE_ROOT/ambient. The
     * fixture is explicit: it runs only when a run selects it by name, never in a run of the whole suite. The binding
     * behaviour it shows is tested by DieBindingPresentationPlayTests, the introduction by
     * EncounterCharmsIntroductionPlayTests, the outcome animation by EncounterOutcomeAnimationTests.
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
            yield return ContentReady.JourneyAtBoard();
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
            yield return ContentReady.JourneyAtBoard();
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

        [UnityTest]
        public IEnumerator BoundTreeDefeatFlinchCapture()
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Review capture only; set DICE_SPIRIT_CAPTURE_ROOT to write it.");
            root = Path.Combine(root, "defeat-flinch");
            Assert.That(Directory.Exists(root), Is.False, "Capture output must be fresh.");
            Directory.CreateDirectory(root);

            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.JourneyAtBoard();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = ContentReady.JourneyWithRule<DieBindingRuleDefinition>(controller);
            var mechanical = LosingFirstHand(controller.Catalog, journey.Encounters.First().Identity);
            Assert.That(controller.InitializeJourney(mechanical, controller.JourneyCatalog, controller.SavePath), Is.True,
                controller.JourneyStatus.ToString());
            yield return ContentReady.EnterEncounter(controller, journey, ContentReady.Seed);
            var view = ContentReady.EncounterView(controller);
            var nymph = view.OutcomeAnimations.Single();

            controller.PlayHand();
            yield return ContentReady.WaitUntil(() => nymph.IsPlayingOutcome, "The nymph flinches on defeat.", ContentReady.SequenceTimeout);
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Lost));
            // Into the flinch's held peak, which NymphFlinch.anim keeps from 0.12 s to 0.58 s.
            yield return new WaitForSecondsRealtime(.3f);
            yield return Capture(root, "a-defeat-mid-flinch.png");
            yield return ContentReady.WaitUntil(() => !nymph.IsPlayingOutcome, "The flinch plays once.", ContentReady.SequenceTimeout);
            // Into the sob that continues under the flinch and after it.
            yield return new WaitForSecondsRealtime(1f);
            yield return Capture(root, "b-defeat-after-flinch-sob.png");
            yield return WaitIdle(controller);
            LogAssert.NoUnexpectedReceived();
        }

        // Seconds into the run of each creature in the group at which it is half out of hiding, in the middle of its run and
        // half hidden again, from Tools/Presentation/author-bound-tree-ambient.py (report.json, creatures.*.capture_times_s).
        private static readonly (string Id, float Emerging, float Middle, float Hiding)[] CreatureMoments =
        {
            ("beetle", 3.6f, 7.66f, 12.1f), ("centipede", 2.85f, 5.61f, 7.38f),
        };
        // Activity clock step while the capture drives the creatures' group; a run starts within one step of its detection.
        private const float CreatureStep = .01f;

        /**
         * The ambient layers at 540 x 960 and 443 x 960: the table at rest with the moss and mist, the moss at two opposite sway
         * extremes (its three sway channels at +1, -1, +1 and at -1, +1, -1), the mist at two drift positions (offset 0 and half a
         * tile) and each creature of the group half emerged, in the middle of its run and half hidden again. Moss and mist are held by
         * stopping their clips and setting their pose; the creature group is driven by seeding the ambient activity and
         * ticking its clock with the view's own ticking suspended, finding for each creature a seed whose first turn is
         * that creature's. Written under DICE_SPIRIT_CAPTURE_ROOT/ambient.
         */
        [UnityTest]
        public IEnumerator BoundTreeAmbientCapture()
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Review capture only; set DICE_SPIRIT_CAPTURE_ROOT to write it.");
            root = Path.Combine(root, "ambient");
            Assert.That(Directory.Exists(root), Is.False, "Capture output must be fresh.");
            Directory.CreateDirectory(root);

            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.JourneyAtBoard();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = ContentReady.JourneyWithRule<DieBindingRuleDefinition>(controller);
            yield return ContentReady.EnterEncounter(controller, journey, ContentReady.Seed);
            var view = ContentReady.EncounterView(controller);
            var activity = (EncounterAmbientActivity)typeof(SpiritEncounterView)
                .GetField("ambientActivity", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            Assert.That(activity, Is.Not.Null, "The bound tree table authors ambient activity.");
            // The capture drives the activity's clock itself, so the view's own ticking is suspended as on focus loss.
            view.SendMessage("OnApplicationFocus", false);
            activity.Stop();
            var transforms = view.GetComponentsInChildren<RectTransform>(true);
            RectTransform Layer(string name) => transforms.Single(rect => rect.name == name);
            var moss = new[] { "Moss Left", "Moss Right" }.Select(name => Layer(name).GetComponent<SpiritPortraitImage>()).ToArray();
            var mist = new[] { "Mist Far", "Mist Near" }.Select(name => Layer(name).GetComponent<RawImage>()).ToArray();
            var clips = moss.Cast<Component>().Concat(mist).Select(layer => layer.GetComponent<Animation>()).ToArray();
            foreach (var clip in clips) clip.enabled = false;
            void Pose(float[] sway, float drift)
            {
                foreach (var image in moss) image.SetWeights(sway);
                foreach (var band in mist) band.uvRect = new Rect(drift, 0f, band.uvRect.width, band.uvRect.height);
            }
            IEnumerator Both(string name)
            {
                yield return Capture(root, name + "-" + Width + "x" + Height + ".png");
                yield return Capture(root, name + "-" + NarrowWidth + "x" + Height + ".png", NarrowWidth);
            }

            Pose(new[] { 0f, 0f, 0f }, 0f);
            yield return Both("a-table-at-rest");
            Pose(new[] { 1f, -1f, 1f }, 0f);
            yield return Both("b-moss-sway-extreme-1");
            Pose(new[] { -1f, 1f, -1f }, 0f);
            yield return Both("b-moss-sway-extreme-2");
            Pose(new[] { 0f, 0f, 0f }, -.5f);
            yield return Both("c-mist-drifted-half-tile");
            Pose(new[] { 0f, 0f, 0f }, 0f);

            var bindings = CreatureMoments.ToDictionary(moment => moment.Id,
                moment => transforms.Single(rect => rect.name.Equals(moment.Id, StringComparison.OrdinalIgnoreCase)));
            var rest = bindings.ToDictionary(pair => pair.Key, pair => (pair.Value.anchoredPosition, pair.Value.localRotation));
            string Moving() => bindings.Keys.FirstOrDefault(id => (bindings[id].anchoredPosition - rest[id].anchoredPosition).sqrMagnitude > 1e-8f
                || Quaternion.Angle(bindings[id].localRotation, rest[id].localRotation) > 1e-3f);
            foreach (var moment in CreatureMoments)
            {
                // Each seed's first turn falls due 30 to 60 s after the activity begins; the first seed giving this creature that turn is used.
                int seed = -1;
                float elapsed = 0f;
                for (int candidate = 0; candidate < 200 && seed < 0; ++candidate)
                {
                    activity.Stop();
                    activity.Begin(candidate);
                    string moving = null;
                    for (elapsed = 0f; elapsed < 61f && moving == null; elapsed += CreatureStep)
                    {
                        activity.Tick(CreatureStep);
                        moving = Moving();
                    }
                    if (moving == moment.Id) seed = candidate;
                }
                Assert.That(seed, Is.GreaterThanOrEqualTo(0), moment.Id + " takes a turn for some seed.");
                float run = 0f;
                foreach (var (label, at) in new[] { ("emerging", moment.Emerging), ("middle", moment.Middle), ("hiding", moment.Hiding) })
                {
                    for (; run < at; run += CreatureStep) activity.Tick(CreatureStep);
                    TestContext.WriteLine("Bound tree ambient capture: " + moment.Id + " seed " + seed + ", run from " + elapsed.ToString("F2")
                        + " s, " + label + " at " + run.ToString("F2") + " s, offset " + (bindings[moment.Id].anchoredPosition - rest[moment.Id].anchoredPosition)
                        + ", rotation " + bindings[moment.Id].localEulerAngles.z.ToString("F1"));
                    yield return Both("d-" + moment.Id + "-" + label);
                }
            }
            activity.Stop();
            foreach (var id in bindings.Keys)
            {
                Assert.That(bindings[id].anchoredPosition, Is.EqualTo(rest[id].anchoredPosition), id + " rests.");
                Assert.That(Quaternion.Angle(bindings[id].localRotation, rest[id].localRotation), Is.LessThan(.01f), id + " rests.");
            }
            foreach (var clip in clips) clip.enabled = true;
            view.SendMessage("OnApplicationFocus", true);
            yield return WaitIdle(controller);
            LogAssert.NoUnexpectedReceived();
        }

        /** A copy of the catalog in which the encounter's first stage has one hand and a target no hand reaches, so playing it loses. */
        private static MechanicalCatalog LosingFirstHand(MechanicalCatalog authored, EncounterDefinition.Id encounterId)
        {
            var definition = authored.GetEncounter(encounterId);
            var losing = new EncounterDefinition(definition.Identity,
                definition.Stages.Select((stage, index) => index == 0
                    ? new StageDefinition(1000000, 1, stage.StartingRerolls, stage.ExitBehavior) : stage),
                definition.StartingUpgrades, definition.StartingModifiers, definition.RewardPolicy, definition.Rules,
                definition.UnlockMilestones, definition.EncounterModifierIds,
                definition.UnlockRewardModifierIds, definition.Pieces, definition.EncounterModifiersFirstClearedStageNumber);
            return new MechanicalCatalog("bound-tree-defeat-capture.v1", authored.Hands,
                authored.Encounters.Select(value => value.Identity == definition.Identity ? losing : value), authored.Modifiers);
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
