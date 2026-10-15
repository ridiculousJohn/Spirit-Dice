using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class WatchtowerJourneyPlayTests
    {
        private const string EncounterId = "encounter.owl.watchtower";
        private SpiritEncounterController controller;
        private SpiritEncounterView view;
        private EncounterJourneyAsset journey;

        [UnitySetUp]
        public IEnumerator InvestigationReady()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit(false);
            controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            view = controller.GetComponent<SpiritEncounterView>();
            journey = controller.JourneyCatalog.Entries.Single(value => value.EncounterId == EncounterId);
            SetField(controller, "useFixedSeed", true);
            SetField(controller, "initialSeed", "60");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ApproachClimbPerchAndFlightIntroduceTheWatcherBeforeOneOpeningRoll()
        {
            Assert.That(journey.Beats.Select(beat => beat.Id), Is.EqualTo(new[]
                { "approach", "climb", "perch", "flight", "witness", "bargain", "invitation" }));
            Assert.That(journey.Beats.Select(beat => beat.Kind), Is.EqualTo(new[]
            {
                EncounterJourneyBeatKind.Illustration, EncounterJourneyBeatKind.Illustration,
                EncounterJourneyBeatKind.Dialogue, EncounterJourneyBeatKind.Arrival,
                EncounterJourneyBeatKind.Dialogue, EncounterJourneyBeatKind.Dialogue,
                EncounterJourneyBeatKind.Invitation
            }));
            Assert.That(journey.Beats[0].Background, Is.Not.Null);
            Assert.That(journey.Beats[1].Background, Is.SameAs(journey.Beats[0].Background));
            Assert.That(journey.Beats[1].BackgroundScaleFrom, Is.EqualTo(1));
            Assert.That(journey.Beats[1].BackgroundScaleTo, Is.EqualTo(1.12f));
            Assert.That(journey.Beats[1].BackgroundMotionTo, Is.EqualTo(new Vector2(0, -.035f)));
            Assert.That(journey.Beats[2].HostState, Is.EqualTo(SpiritPresentationState.Waiting));
            Assert.That(journey.Beats[3].HostState, Is.EqualTo(SpiritPresentationState.Arrival));
            Assert.That(journey.Beats[3].HideNarrativeDuringMotion, Is.True);
            Assert.That(journey.Beats.Where(beat => beat.AutoAdvance).Select(beat => beat.Id),
                Is.EqualTo(new[] { "flight" }));
            Assert.That(journey.TablePresentation.RoomTexture, Is.Not.Null);
            Assert.That(journey.TablePresentation.AudioTheme, Is.Not.Null);
            Assert.That(journey.TablePresentation.AudioTheme.ambience, Is.Null);
            Assert.That(journey.TablePresentation.AudioTheme.creak, Is.Null);
            Assert.That(journey.TablePresentation.HostPresentation, Is.TypeOf<SpiritHostPresentationAsset>());
            var presentation = (SpiritHostPresentationAsset)journey.TablePresentation.HostPresentation;
            Assert.That(presentation.Layers.Single(layer => layer.Id == "Physical Rest").Texture, Is.Not.Null);
            Assert.That(presentation.Layers.Single(layer => layer.Id == "Physical Arrival").Texture, Is.Not.Null);
            Assert.That(presentation.Layers.Single(layer => layer.Id == "Physical Settle").Texture, Is.Not.Null);
            Assert.That(journey.Beats[2].Text, Is.EqualTo("An owl watches from the upper arch."));
            Assert.That(journey.Beats[4].Text, Does.StartWith("I saw someone cross the marsh."));
            Assert.That(journey.Beats[5].Text, Is.EqualTo("Sit. Show me what you see, and I will tell you what I saw."));

            int resolutions = 0;
            controller.Resolved += _ => ++resolutions;
            controller.SelectEncounter(EncounterId);
            yield return null;
            for (int index = 0; index < journey.Beats.Count; ++index)
            {
                Assert.That(controller.BeatIndex, Is.EqualTo(index));
                AssertNoEngine(resolutions);
                if (index == 3)
                {
                    yield return new WaitForSecondsRealtime(1.8f);
                    Assert.That(view.JourneyView.IsAnimating, Is.True);
                    var owl = Owl();
                    Assert.That(owl.State, Is.EqualTo(SpiritPresentationState.Arrival));
                    Assert.That(owl.GetLayer("Physical Arrival").color.a, Is.GreaterThan(.1f));
                    Assert.That(Find<Button>("JourneyAdvanceButton").IsInteractable(), Is.False,
                        "The descent must finish before the next dialogue.");
                    Assert.That(Find<CanvasGroup>("JourneyNarrative").alpha, Is.Zero,
                        "The flight has the chamber to itself.");
                    yield return CheckPortraitSizes("flight");
                    float deadline = Time.realtimeSinceStartup + 10;
                    while (controller.BeatIndex == index && Time.realtimeSinceStartup < deadline)
                    {
                        Assert.That(Find<CanvasGroup>("JourneyNarrative").alpha, Is.Zero,
                            "The flight must not reveal an empty plaque before its dialogue.");
                        Assert.That(Find<Button>("JourneyAdvanceButton").IsInteractable(), Is.False);
                        yield return null;
                    }
                    Assert.That(controller.BeatIndex, Is.EqualTo(index + 1),
                        "Landing must advance directly to the first dialogue exactly once.");
                    ++index;
                }
                yield return WaitForJourney();
                Assert.That(view.JourneyView.VisibleBeatIndex, Is.EqualTo(index));
                Assert.That(Find<Text>("JourneyTitle").text, Is.EqualTo(journey.Beats[index].Title));
                Assert.That(controller.CanSkipIntro, Is.False);
                AssertNoEngine(resolutions);
                if (index == 2)
                {
                    Assert.That(Owl().State, Is.EqualTo(SpiritPresentationState.Waiting));
                    Assert.That(Vector2.Distance(Contact(), new Vector2(.814f, .884f)), Is.LessThan(.00001f));
                }
                if (index == 4)
                {
                    Assert.That(Owl().State, Is.EqualTo(SpiritPresentationState.Idle));
                    Assert.That(Vector2.Distance(Contact(), new Vector2(.5f, .612f)), Is.LessThan(.00001f));
                    Assert.That(Owl().GetLayer("Physical Rest").color.a, Is.EqualTo(1).Within(.001f));
                    Assert.That(Owl().GetLayer("Physical Arrival").color.a, Is.Zero);
                    Assert.That(Owl().GetLayer("Physical Settle").color.a, Is.Zero);
                }
                yield return CheckPortraitSizes(journey.Beats[index].Id + "-settled");
                if (index < journey.Beats.Count - 1) Click("JourneyAdvanceButton");
            }

            var rule = journey.TablePresentation.RulePresentations.Single();
            Assert.That(rule.RuleId, Is.EqualTo("rule.distinct_faces_multiplier"));
            Assert.That(Regex.Replace(rule.Description, "<[^>]*>", ""),
                Is.EqualTo("Each distinct face in your scored hand adds +1 multiplier."));
            Assert.That(Find<Text>("JourneyRuleName").text, Is.EqualTo(rule.DisplayName));
            Assert.That(Find<Text>("JourneyRuleText").text, Does.Contain(rule.Description));
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(controller.BeatIndex, Is.EqualTo(journey.Beats.Count - 1));
            AssertNoEngine(resolutions);
            var openingGroups = OpeningGroups();
            Click("JourneyAcceptButton");
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo(EncounterId));
            Assert.That(controller.Snapshot.Revision, Is.Zero);
            Assert.That(controller.IsBusy, Is.True);
            Assert.That(view.DiceTray.IsRolling, Is.False);
            foreach (var group in openingGroups) Assert.That(group.alpha, Is.Zero, group.name);
            Assert.That(view.PlayButton.IsInteractable(), Is.False);
            Assert.That(resolutions, Is.EqualTo(1));
            var opening = controller.Snapshot;
            controller.AcceptEncounter();
            Assert.That(controller.Snapshot, Is.SameAs(opening));
            float rollDeadline = Time.realtimeSinceStartup + 5;
            float previousOpacity = 0;
            bool observedFade = false;
            while (!view.DiceTray.IsRolling && Time.realtimeSinceStartup < rollDeadline)
            {
                float opacity = openingGroups[0].alpha;
                Assert.That(opacity, Is.GreaterThanOrEqualTo(previousOpacity));
                foreach (var group in openingGroups) Assert.That(group.alpha, Is.EqualTo(opacity), group.name);
                observedFade |= opacity > 0 && opacity < 1;
                Assert.That(controller.IsBusy, Is.True);
                Assert.That(view.PlayButton.IsInteractable(), Is.False);
                Assert.That(resolutions, Is.EqualTo(1));
                previousOpacity = opacity;
                yield return null;
            }
            Assert.That(observedFade, Is.True, "The gameplay HUD must fade in before the opening roll.");
            Assert.That(view.DiceTray.IsRolling, Is.True);
            foreach (var group in openingGroups) Assert.That(group.alpha, Is.EqualTo(1), group.name);
            Assert.That(resolutions, Is.EqualTo(1));
            yield return WaitForPresentation();
            Assert.That(Owl().State, Is.EqualTo(SpiritPresentationState.Idle));
            yield return CheckPortraitSizes("opening-table");
            Assert.That(rule.ActivationPhase, Is.EqualTo(ModifierPhase.PlayHand));
            var ruleLabel = Find<Text>("EncounterRuleLabel");
            Assert.That(ruleLabel.gameObject.activeInHierarchy, Is.True);
            Assert.That(ruleLabel.transform.IsChildOf(view.PlayButton.transform), Is.True);
            Assert.That(ruleLabel.transform.IsChildOf(view.RerollButton.transform), Is.False);
            Assert.That(view.MultiplierLabel.text, Is.EqualTo(DiagnosticText.Number(controller.Snapshot.ModifierMultiplier)),
                "The watcher bonus must remain outside the roll bonus bank until the hand is scored.");

            HandScoredEvent scored = null;
            EncounterRuleActivatedEvent activation = null;
            int ruleActivations = 0;
            controller.Resolved += resolution =>
            {
                scored = resolution.Events.OfType<HandScoredEvent>().SingleOrDefault();
                foreach (var value in resolution.Events.OfType<EncounterRuleActivatedEvent>())
                {
                    if (value.RuleId != rule.RuleId) continue;
                    activation = value;
                    ++ruleActivations;
                }
            };
            Click("PlayButton");
            Assert.That(scored, Is.Not.Null);
            Assert.That(activation, Is.Not.Null);
            Assert.That(ruleActivations, Is.EqualTo(1));
            Assert.That(activation.MultiplierBonus, Is.EqualTo(scored.Score.EncounterRuleMultiplier));
            Assert.That(scored.Score.EncounterRuleMultiplier, Is.GreaterThan(0));
            var selection = Find<Text>("SelectionLabel");
            float totalDeadline = Time.realtimeSinceStartup + 30;
            while (selection.text != "TOTAL" && Time.realtimeSinceStartup < totalDeadline) yield return null;
            Assert.That(selection.text, Is.EqualTo("TOTAL"), "The scored hand must present its final total before the next hand.");
            Assert.That(view.MultiplierLabel.text, Is.EqualTo(DiagnosticText.Number(scored.Score.Multiplier)));
            Assert.That(scored.Score.Multiplier, Is.EqualTo(scored.Score.CategoryMultiplier +
                scored.Score.UpgradeMultiplier + scored.Score.ModifierMultiplier + scored.Score.EncounterRuleMultiplier));
            Assert.That(Find<Text>("ScoreBreakdown").text, Is.EqualTo("STEP 4 OF 4 - " + DiagnosticText.Number(scored.Score.BaseScore) +
                " x " + DiagnosticText.Number(scored.Score.Multiplier)));
            yield return CheckPortraitSizes("scored-table");
            yield return WaitForPresentation();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DisablingDuringTheOpeningFadeRestoresTheHudWithoutStartingADelayedRoll()
        {
            int resolutions = 0;
            controller.Resolved += _ => ++resolutions;
            controller.SelectEncounter(EncounterId);
            while (controller.BeatIndex < journey.Beats.Count - 1) controller.AdvanceJourney();
            yield return WaitForJourney();
            var openingGroups = OpeningGroups();
            Click("JourneyAcceptButton");
            var opening = controller.Snapshot;
            float deadline = Time.realtimeSinceStartup + 5;
            while (openingGroups[0].alpha == 0 && !view.DiceTray.IsRolling &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(openingGroups[0].alpha, Is.InRange(.000001f, .999999f));
            Assert.That(controller.IsBusy, Is.True);
            Assert.That(view.DiceTray.IsRolling, Is.False);
            view.enabled = false;
            foreach (var group in openingGroups) Assert.That(group.alpha, Is.EqualTo(1), group.name);
            Assert.That(controller.IsBusy, Is.False);
            yield return new WaitForSecondsRealtime(journey.TablePresentation.OpeningFadeDuration + .1f);
            Assert.That(view.DiceTray.IsRolling, Is.False);
            Assert.That(controller.Snapshot, Is.SameAs(opening));
            Assert.That(resolutions, Is.EqualTo(1));
            view.enabled = true;
            yield return new WaitForSecondsRealtime(journey.TablePresentation.OpeningFadeDuration + .1f);
            foreach (var group in openingGroups) Assert.That(group.alpha, Is.EqualTo(1), group.name);
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(view.DiceTray.IsRolling, Is.False);
            Assert.That(controller.Snapshot, Is.SameAs(opening));
            Assert.That(resolutions, Is.EqualTo(1));
            Assert.That(view.PlayButton.IsInteractable(), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ImmediateAutomaticBeatDefersRefreshAndCannotAdvanceARevisitOrAcceptTheInvitation()
        {
            var runtimeJourney = UnityEngine.Object.Instantiate(journey);
            var runtimeCatalog = UnityEngine.Object.Instantiate(controller.JourneyCatalog);
            try
            {
                journey = runtimeJourney;
                SetField(runtimeCatalog, "entries", new[] { journey });
                foreach (var beat in journey.Beats)
                {
                    SetField(beat, "duration", 0f);
                    SetField(beat, "autoAdvance", false);
                }
                SetField(journey.Beats[0], "id", "automatic-step");
                SetField(journey.Beats[0], "autoAdvance", true);
                SetField(journey.Beats[0], "hideNarrativeDuringMotion", true);
                SetField(journey.Beats[journey.Beats.Count - 1], "autoAdvance", true);
                Assert.That(controller.InitializeJourney(controller.Catalog, runtimeCatalog, controller.SavePath), Is.True);
                int resolutions = 0;
                controller.Resolved += _ => ++resolutions;
                controller.SelectEncounter(EncounterId);
                Assert.That(controller.BeatIndex, Is.Zero);
                Assert.That(view.JourneyView.VisibleBeatIndex, Is.Zero);
                Assert.That(Find<CanvasGroup>("JourneyNarrative").alpha, Is.Zero);
                controller.ReturnToInvestigation();
                controller.SelectEncounter(EncounterId);
                Assert.That(controller.BeatIndex, Is.Zero);
                yield return WaitForJourney();
                Assert.That(controller.BeatIndex, Is.EqualTo(1));
                Assert.That(view.JourneyView.VisibleBeatIndex, Is.EqualTo(1));
                yield return new WaitForSecondsRealtime(.1f);
                Assert.That(controller.BeatIndex, Is.EqualTo(1),
                    "Only the current visit may complete its automatic continuation, exactly once.");
                AssertNoEngine(resolutions);
                while (controller.BeatIndex < journey.Beats.Count - 1) controller.AdvanceJourney();
                yield return WaitForJourney();
                Assert.That(controller.BeatIndex, Is.EqualTo(journey.Beats.Count - 1));
                Assert.That(Find<Button>("JourneyAcceptButton").IsInteractable(), Is.True);
                AssertNoEngine(resolutions);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.Destroy(runtimeCatalog);
                UnityEngine.Object.Destroy(runtimeJourney);
            }
        }

        [UnityTest]
        public IEnumerator WatchtowerFlightOpeningIdleAndReactionPlayInRealTime()
        {
            var portrait = view.transform.Find("SpiritCanvas/SafeArea/Portrait") as RectTransform;
            Assert.That(portrait, Is.Not.Null);
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(540, 960);
            portrait.anchoredPosition = Vector2.zero;
            yield return null;
            Canvas.ForceUpdateCanvases();

            string captureRoot = Environment.GetEnvironmentVariable("DICE_SPIRIT_MOTION_CAPTURE_ROOT");
            if (!string.IsNullOrEmpty(captureRoot))
            {
                captureRoot = Path.Combine(captureRoot, "watchtower-" +
                    DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
                Assert.That(Directory.Exists(captureRoot), Is.False);
                Directory.CreateDirectory(captureRoot);
                TestContext.WriteLine("Watchtower motion capture: " + captureRoot);
            }

            controller.SelectEncounter(EncounterId);
            yield return AdvanceTo("perch");
            yield return WaitForJourney();
            yield return CaptureMotion("flight", 4.5f, () => Click("JourneyAdvanceButton"),
                () => view.JourneyView.IsAnimating);
            Assert.That(journey.Beats[controller.BeatIndex].Id, Is.EqualTo("witness"));
            Assert.That(Owl().State, Is.EqualTo(SpiritPresentationState.Idle));
            Assert.That(Vector2.Distance(Contact(), new Vector2(.5f, .612f)), Is.LessThan(.00001f));
            AssertRestPose();

            yield return AdvanceTo("invitation");
            yield return WaitForJourney();
            yield return CaptureMotion("transition", 0f, () =>
            {
                Click("JourneyAcceptButton");
                Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
                Assert.That(controller.IsBusy, Is.True);
                Assert.That(view.DiceTray.IsRolling, Is.False);
            }, () => controller.IsBusy);
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(Owl().State, Is.EqualTo(SpiritPresentationState.Idle));

            var owl = Owl();
            string[] physicalLayers = { "Physical Rest", "Physical Arrival", "Physical Settle" };
            Vector3 Foot(string name)
            {
                var image = owl.GetLayer(name);
                var rect = image.rectTransform;
                return portrait.InverseTransformPoint(rect.TransformPoint(
                    image.EvaluateDisplacement(rect.pivot, rect.rect.size)));
            }
            Vector3[] feet = physicalLayers.Select(Foot).ToArray();
            void AssertFeet()
            {
                for (int index = 0; index < physicalLayers.Length; ++index)
                    Assert.That(Vector3.Distance(Foot(physicalLayers[index]), feet[index]), Is.LessThan(.01f),
                        physicalLayers[index] + " foot contact moved during table motion.");
            }

            yield return CaptureMotion("idle", 15.6f, null, null, () =>
            {
                Assert.That(owl.State, Is.EqualTo(SpiritPresentationState.Idle));
                AssertFeet();
            });
            Assert.That(owl.GestureDuration, Is.EqualTo(1.8f).Within(.001f));
            yield return CaptureMotion("reaction", 1.8f, () =>
            {
                owl.React();
                Assert.That(owl.IsReacting, Is.True);
            }, () => owl.IsReacting, AssertFeet);
            Assert.That(owl.State, Is.EqualTo(SpiritPresentationState.Idle));
            AssertRestPose();
            LogAssert.NoUnexpectedReceived();

            void AssertRestPose()
            {
                Assert.That(Owl().GetLayer("Physical Rest").color.a, Is.EqualTo(1).Within(.001f));
                Assert.That(Owl().GetLayer("Physical Arrival").color.a, Is.Zero);
                Assert.That(Owl().GetLayer("Physical Settle").color.a, Is.Zero);
            }

            IEnumerator CaptureMotion(string phase, float seconds, Action begin = null,
                Func<bool> pending = null, Action validate = null)
            {
                string directory = string.IsNullOrEmpty(captureRoot) ? null : Path.Combine(captureRoot, phase);
                if (directory != null) Directory.CreateDirectory(directory);
                var paths = new List<string>();
                using var timestamps = directory == null ? null : File.CreateText(Path.Combine(directory, "timestamps.csv"));
                timestamps?.WriteLine("frame,seconds,realtime_seconds,file,state,progress");
                yield return new WaitForEndOfFrame();
                double started = Time.realtimeSinceStartupAsDouble;
                double nextCapture = 0;
                bool began = false;
                while (true)
                {
                    double now = Time.realtimeSinceStartupAsDouble;
                    double elapsed = now - started;
                    if (began) validate?.Invoke();
                    bool complete = began && elapsed >= seconds && (pending == null || !pending());
                    if (directory != null && (elapsed >= nextCapture || complete))
                    {
                        string filename = string.Format(CultureInfo.InvariantCulture,
                            "frame-{0:D5}-{1:D6}ms.png", paths.Count, (int)(elapsed * 1000));
                        string path = Path.Combine(directory, filename);
                        ScreenCapture.CaptureScreenshot(path);
                        timestamps.WriteLine(string.Format(CultureInfo.InvariantCulture,
                            "{0},{1:F6},{2:F6},{3},{4},{5:F6}", paths.Count, elapsed, now,
                            filename, Owl().State, Owl().Progress));
                        paths.Add(path);
                        nextCapture = elapsed + .1;
                    }
                    if (!began)
                    {
                        begin?.Invoke();
                        began = true;
                    }
                    else if (complete) break;
                    Assert.That(elapsed, Is.LessThan(seconds + 45), phase + " did not complete.");
                    yield return null;
                    yield return new WaitForEndOfFrame();
                }
                double deadline = Time.realtimeSinceStartupAsDouble + 15;
                while (paths.Any(path => !File.Exists(path) || new FileInfo(path).Length == 0) &&
                    Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                foreach (string path in paths)
                {
                    Assert.That(File.Exists(path), Is.True, path);
                    Assert.That(new FileInfo(path).Length, Is.GreaterThan(0), path);
                }
            }
        }

        [UnityTest]
        public IEnumerator ReturningDuringFlightCancelsMotionAndRevisitSkipSettlesTheOwlAtTheTable()
        {
            controller.SelectEncounter(EncounterId);
            yield return AdvanceTo("flight");
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(view.JourneyView.IsAnimating, Is.True);
            controller.ReturnToInvestigation();
            yield return null;
            Assert.That(view.JourneyView.IsAnimating, Is.False);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(view.GetComponentsInChildren<SpiritPortraitImage>()
                .Any(layer => layer.isActiveAndEnabled), Is.False,
                "Returning to clues must remove the active flight presentation.");

            controller.SelectEncounter(EncounterId);
            yield return AdvanceTo("perch");
            yield return WaitForJourney();
            Assert.That(Owl().State, Is.EqualTo(SpiritPresentationState.Waiting));
            var presentation = (SpiritHostPresentationAsset)journey.TablePresentation.HostPresentation;
            Assert.That(Vector2.Distance(Contact(), new Vector2(.814f, .884f)), Is.LessThan(.00001f));
            yield return new WaitForSecondsRealtime(journey.Beats[3].Duration + .1f);
            Assert.That(journey.Beats[controller.BeatIndex].Id, Is.EqualTo("perch"),
                "The canceled flight must not advance the new visit.");
            yield return AdvanceTo("invitation");
            yield return WaitForJourney();
            Click("JourneyAcceptButton");
            yield return WaitForPresentation();
            controller.Abandon();
            yield return WaitForPresentation();
            var abandoned = controller.Snapshot;
            Click("EndingNewRunButton");
            yield return null;
            controller.SelectEncounter(EncounterId);
            Assert.That(controller.CanSkipIntro, Is.True);
            yield return AdvanceTo("flight");
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(view.JourneyView.IsAnimating, Is.True);
            Click("JourneySkipButton");
            yield return WaitForJourney();
            yield return new WaitForSecondsRealtime(journey.Beats[3].Duration + .1f);
            Assert.That(controller.BeatIndex, Is.EqualTo(journey.Beats.Count - 1));
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(controller.Snapshot, Is.SameAs(abandoned));
            Assert.That(controller.HasActiveEncounter, Is.False);
            Assert.That(Owl().State, Is.EqualTo(SpiritPresentationState.Idle));
            Assert.That(Vector2.Distance(Contact(), new Vector2(.5f, .612f)), Is.LessThan(.00001f));
            Assert.That(Owl().GetLayer("Physical Rest").color.a, Is.EqualTo(1).Within(.001f));
            Assert.That(Owl().GetLayer("Physical Arrival").color.a, Is.Zero);
            Assert.That(Owl().GetLayer("Physical Settle").color.a, Is.Zero);
            yield return CheckPortraitSizes("revisit-skipped");
            controller.ReturnToInvestigation();
            yield return null;
            Assert.That(view.JourneyView.IsAnimating, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        private SpiritHostPortrait Owl()
        {
            Assert.That(view.HostPortrait, Is.TypeOf<SpiritHostPortrait>());
            return (SpiritHostPortrait)view.HostPortrait;
        }

        private CanvasGroup[] OpeningGroups()
        {
            return new[] { "PlayingPanel", "TargetPanel", "HandsPanel", "RerollsPanel", "StageLabel",
                "StatusMessage", "ResourcesLabel", "CharmsButton", "HandRanksButton", "MenuButton",
                "ReturnToInvestigationButton" }.Select(name => Find<CanvasGroup>(name)).ToArray();
        }

        private Vector2 Contact()
        {
            var layer = Owl().GetLayer("Physical Rest").rectTransform;
            return new Vector2(layer.anchoredPosition.x / 540f, layer.anchoredPosition.y / 960f);
        }
        private void AssertNoEngine(int resolutions)
        {
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.Preview, Is.Null);
            Assert.That(controller.HasActiveEncounter, Is.False);
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(view.DiceTray.IsRolling, Is.False);
            Assert.That(resolutions, Is.Zero);
            Assert.That(typeof(SpiritEncounterController).GetField("application", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller), Is.Null);
            Assert.That(File.Exists(controller.SavePath), Is.False);
        }

        private IEnumerator AdvanceTo(string id)
        {
            while (journey.Beats[controller.BeatIndex].Id != id)
            {
                yield return WaitForJourney();
                if (journey.Beats[controller.BeatIndex].Id == id) break;
                Click("JourneyAdvanceButton");
            }
            yield return null;
        }

        private IEnumerator WaitForJourney()
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (view.JourneyView.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(view.JourneyView.IsAnimating, Is.False, controller.JourneyStatus);
            yield return new WaitForSecondsRealtime(.15f);
        }

        private IEnumerator WaitForPresentation()
        {
            float deadline = Time.realtimeSinceStartup + 45;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False, controller.StatusMessage);
            yield return null;
        }

        private IEnumerator CheckPortraitSizes(string name)
        {
            var portrait = view.transform.Find("SpiritCanvas/SafeArea/Portrait") as RectTransform;
            Assert.That(portrait, Is.Not.Null);
            foreach (var size in new[] { new Vector2(443, 912), new Vector2(540, 960) })
            {
                portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                portrait.sizeDelta = size;
                portrait.anchoredPosition = Vector2.zero;
                yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (var label in view.GetComponentsInChildren<Text>())
                {
                    if (!label.isActiveAndEnabled || string.IsNullOrWhiteSpace(label.text) ||
                        label.GetComponentInParent<ScrollRect>() != null) continue;
                    var corners = new Vector3[4];
                    label.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        Vector2 local = portrait.InverseTransformPoint(corner);
                        Assert.That(local.x, Is.InRange(portrait.rect.xMin - 1, portrait.rect.xMax + 1), label.name);
                        Assert.That(local.y, Is.InRange(portrait.rect.yMin - 1, portrait.rect.yMax + 1), label.name);
                    }
                }
                string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
                if (string.IsNullOrEmpty(root)) continue;
                Directory.CreateDirectory(root);
                string path = Path.Combine(root, "watchtower-" + name + "-" + size.x + ".png");
                Assert.That(File.Exists(path), Is.False, "Use a fresh capture directory for each validation run.");
                ScreenCapture.CaptureScreenshot(path);
                for (int frame = 0; frame < 240 && !File.Exists(path); ++frame) yield return null;
                Assert.That(File.Exists(path), Is.True, path);
                TestContext.WriteLine("Watchtower screenshot: " + path);
            }
        }

        private T Find<T>(string name) where T : Component
        {
            var result = view.GetComponentsInChildren<T>(true).FirstOrDefault(value => value.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }

        private void Click(string name)
        {
            var button = Find<Button>(name);
            Assert.That(button.gameObject.activeInHierarchy, Is.True, name);
            Assert.That(button.IsInteractable(), Is.True, name);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left,
                pointerId = -1
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, name);
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.SameAs(button.gameObject), "A graphic blocks " + name);
            pointer.pointerCurrentRaycast = hits[0];
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }
}
