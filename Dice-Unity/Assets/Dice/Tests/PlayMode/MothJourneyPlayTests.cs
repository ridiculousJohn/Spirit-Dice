using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public sealed class MothJourneyPlayTests
    {
        private const string EncounterId = "encounter.moth.crossroads";
        private SpiritEncounterController controller;
        private SpiritEncounterView view;
        private EncounterJourneyAsset journey;

        [UnitySetUp]
        public IEnumerator InvestigationReady()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit(false);
            controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            ContentReady.SeedJourneyPrerequisites(controller, EncounterId);
            view = controller.GetComponent<SpiritEncounterView>();
            journey = controller.JourneyCatalog.Entries.Single(value => value.EncounterId == EncounterId);
            SetField(controller, "useFixedSeed", true);
            SetField(controller, "initialSeed", "60");
            yield return null;
        }

        [UnityTest]
        public IEnumerator MothCausewayLanternArrivalOpeningIdleAndReactionPlayInRealTime()
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
                captureRoot = Path.Combine(captureRoot, "moth-" +
                    DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
                Assert.That(Directory.Exists(captureRoot), Is.False);
                Directory.CreateDirectory(captureRoot);
                TestContext.WriteLine("Moth motion capture: " + captureRoot);
            }

            Assert.That(journey.Beats.Select(beat => beat.Id), Is.EqualTo(new[]
                { "causeway", "lantern", "arrival", "witness", "bargain", "discovery", "invitation" }));
            Assert.That(journey.Beats.Where(beat => beat.AutoAdvance).Select(beat => beat.Id),
                Is.EqualTo(new[] { "arrival" }));
            Assert.That(journey.Beats.Where(beat => beat.Id != "arrival").All(beat => beat.Duration == 0), Is.True);
            int resolutions = 0;
            int rolls = 0;
            controller.Resolved += resolution =>
            {
                ++resolutions;
                rolls += resolution.Events.OfType<DiceRolledEvent>().Count();
            };
            AssertNoEngine(resolutions);
            yield return CaptureMotion("causeway", 3f, () => controller.SelectEncounter(EncounterId),
                () => view.JourneyView.IsAnimating, () => AssertNoEngine(resolutions));
            AssertBeat("causeway");

            yield return CaptureMotion("lantern", 11.1f, () => Click("JourneyAdvanceButton"),
                () => view.JourneyView.IsAnimating, () =>
                {
                    AssertNoEngine(resolutions);
                    Assert.That(Moth().State, Is.EqualTo(SpiritPresentationState.Waiting));
                });
            AssertBeat("lantern");

            yield return CaptureMotion("arrival", 7f, () =>
            {
                var moths = Enumerable.Range(1, 3).Select(index => Moth().GetLayer("Lantern moth " + index)).ToArray();
                var positions = moths.Select(moth => moth.rectTransform.anchoredPosition).ToArray();
                var rotations = moths.Select(moth => moth.rectTransform.localRotation).ToArray();
                Click("JourneyAdvanceButton");
                for (int index = 0; index < moths.Length; ++index)
                {
                    Assert.That(Vector2.Distance(moths[index].rectTransform.anchoredPosition, positions[index]),
                        Is.LessThan(.01f), "Arrival must start from the visible waiting position.");
                    Assert.That(Quaternion.Angle(moths[index].rectTransform.localRotation, rotations[index]),
                        Is.LessThan(.01f), "Arrival must preserve the visible banking angle.");
                }
            },
                () => view.JourneyView.IsAnimating, () =>
                {
                    AssertNoEngine(resolutions);
                    if (journey.Beats[controller.BeatIndex].Id == "arrival")
                    {
                        Assert.That(Moth().State, Is.EqualTo(SpiritPresentationState.Arrival));
                        Assert.That(Find<CanvasGroup>("JourneyNarrative").alpha, Is.Zero);
                        Assert.That(Find<Button>("JourneyAdvanceButton").IsInteractable(), Is.False);
                    }
                    else Assert.That(journey.Beats[controller.BeatIndex].Id, Is.EqualTo("witness"));
                });
            AssertBeat("witness");
            Assert.That(Find<CanvasGroup>("JourneyNarrative").alpha, Is.EqualTo(1).Within(.001f));
            yield return CaptureMotion("witness", 2f, null, null, () => AssertNoEngine(resolutions));

            foreach (string phase in new[] { "bargain", "discovery", "invitation" })
            {
                yield return CaptureMotion(phase, 2f, () => Click("JourneyAdvanceButton"),
                    () => view.JourneyView.IsAnimating, () =>
                    {
                        AssertNoEngine(resolutions);
                        Assert.That(Moth().State, Is.EqualTo(SpiritPresentationState.Idle));
                    });
                AssertBeat(phase);
            }
            Assert.That(Find<Text>("JourneyRuleName").text,
                Is.EqualTo(journey.TablePresentation.RulePresentations.Single().DisplayName));
            AssertNoEngine(resolutions);

            bool observedRoll = false;
            bool observedFade = false;
            var openingGroup = Find<CanvasGroup>("TargetPanel");
            yield return CaptureMotion("opening", .5f, () =>
            {
                Click("JourneyAcceptButton");
                Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
                Assert.That(controller.Snapshot.EncounterId, Is.EqualTo(EncounterId));
                Assert.That(controller.Snapshot.Revision, Is.Zero);
                Assert.That(controller.IsBusy, Is.True);
                Assert.That(openingGroup.alpha, Is.Zero);
                Assert.That(view.DiceTray.IsRolling, Is.False);
                observedRoll |= view.DiceTray.IsRolling;
                var opening = controller.Snapshot;
                controller.AcceptEncounter();
                Assert.That(controller.Snapshot, Is.SameAs(opening));
            }, () => controller.IsBusy, () =>
            {
                observedRoll |= view.DiceTray.IsRolling;
                if (openingGroup.alpha > 0 && openingGroup.alpha < 1)
                {
                    observedFade = true;
                    Assert.That(view.DiceTray.IsRolling, Is.False);
                    Assert.That(view.PlayButton.IsInteractable(), Is.False);
                }
                if (view.DiceTray.IsRolling) Assert.That(openingGroup.alpha, Is.EqualTo(1));
                Assert.That(resolutions, Is.EqualTo(1));
                Assert.That(rolls, Is.EqualTo(1));
                Assert.That(controller.Snapshot.Revision, Is.Zero);
            });
            Assert.That(observedRoll, Is.True, "Accept must visibly present the initial dice roll.");
            Assert.That(observedFade, Is.True, "Gameplay must fade in before the opening roll.");
            Assert.That(view.DiceTray.IsRolling, Is.False);
            Assert.That(view.PlayButton.IsInteractable(), Is.True);
            var expected = EncounterSession.CreateEncounter(controller.Catalog, EncounterId, 60);
            for (int index = 0; index < DiceValues.Count; ++index)
                Assert.That(controller.Snapshot.CurrentDice[index], Is.EqualTo(expected.Snapshot.CurrentDice[index]));

            yield return CaptureMotion("idle", 8.1f, null, null, () =>
            {
                Assert.That(Moth().State, Is.EqualTo(SpiritPresentationState.Idle));
                Assert.That(resolutions, Is.EqualTo(1));
                Assert.That(rolls, Is.EqualTo(1));
            });
            Assert.That(Moth().GestureDuration, Is.EqualTo(1.5f).Within(.001f));
            yield return CaptureMotion("reaction", 1.5f, () =>
            {
                Moth().React();
                Assert.That(Moth().IsReacting, Is.True);
            }, () => Moth().IsReacting);
            Assert.That(Moth().State, Is.EqualTo(SpiritPresentationState.Idle));
            Assert.That(resolutions, Is.EqualTo(1));
            Assert.That(rolls, Is.EqualTo(1));
            TestContext.WriteLine("Moth journey completed: one resolution, one opening roll, seed 60.");
            LogAssert.NoUnexpectedReceived();

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
                            filename, view.HostPortrait != null ? view.HostPortrait.State.ToString() : "",
                            view.HostPortrait != null ? view.HostPortrait.Progress : 0));
                        paths.Add(path);
                        nextCapture = elapsed + (phase == "lantern" || phase == "arrival" ? .04 : .1);
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
                TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "Moth phase {0}: {1:F3}s, {2} frames, state {3}, resolutions {4}, rolls {5}.",
                    phase, Time.realtimeSinceStartupAsDouble - started, paths.Count,
                    view.HostPortrait != null ? view.HostPortrait.State.ToString() : "", resolutions, rolls));
            }
        }

        private SpiritHostPortrait Moth()
        {
            Assert.That(view.HostPortrait, Is.Not.Null);
            return view.HostPortrait;
        }

        private void AssertBeat(string id)
        {
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(journey.Beats[controller.BeatIndex].Id, Is.EqualTo(id));
            Assert.That(view.JourneyView.VisibleBeatIndex, Is.EqualTo(controller.BeatIndex));
            Assert.That(view.JourneyView.IsAnimating, Is.False);
            Assert.That(Find<Text>("JourneyBody").text, Is.EqualTo(journey.Beats[controller.BeatIndex].Text));
            Assert.That(controller.CanSkipIntro, Is.False);
            Assert.That(Find<Button>(id == "invitation" ? "JourneyAcceptButton" : "JourneyAdvanceButton")
                .IsInteractable(), Is.True);
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
            Assert.That(EventSystem.current, Is.Not.Null);
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
