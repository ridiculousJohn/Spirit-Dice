using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Dice.App;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Dice.Tests
{
    /** The watcher's own choreography: it waits on a perch, flies down during the journey's automatic arrival and lands at the table. */
    [Explicit("Art-generation check; run it by name while the owl art is being changed.")]
    [Category("ArtAuthoring")]
    public sealed class WatchtowerJourneyPlayTests
    {
        private const string EncounterId = "encounter.owl.watchtower";
        private SpiritEncounterController controller;
        private SpiritEncounterView view => ContentReady.EncounterView(controller);
        private EncounterJourneyAsset journey;
        private int flight;

        [UnitySetUp]
        public IEnumerator InvestigationReady()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            ContentReady.SeedJourneyPrerequisites(controller, EncounterId);
            journey = controller.JourneyCatalog.Find(EncounterId);
            Assert.That(journey, Is.Not.Null, EncounterId);
            flight = FlightIndex();
            SetField(controller, "useFixedSeed", true);
            SetField(controller, "initialSeed", "60");
            yield return null;
        }

        [UnityTest]
        public IEnumerator WatchtowerFlightOpeningIdleAndReactionPlayInRealTime()
        {
            controller.SelectEncounter(EncounterId);
            yield return ContentReady.Routed(controller);
            var portrait = ContentReady.Portrait();
            Assert.That(portrait, Is.Not.Null);
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(540, 960);
            portrait.anchoredPosition = Vector2.zero;
            yield return null;
            Canvas.ForceUpdateCanvases();

            // Motion frames are an inspection capture, written only when a capture root is set.
            string captureRoot = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (!string.IsNullOrEmpty(captureRoot))
            {
                captureRoot = Path.Combine(captureRoot, "watchtower-motion-" +
                    DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
                Assert.That(Directory.Exists(captureRoot), Is.False);
                Directory.CreateDirectory(captureRoot);
                TestContext.WriteLine("Watchtower motion capture: " + captureRoot);
            }

            yield return AdvanceTo(flight - 1);
            yield return WaitForJourney();
            Assert.That(Owl().State, Is.EqualTo(SpiritPresentationState.Waiting), "The watcher waits on its perch before the flight.");
            Vector2 perch = Contact();
            float arrivalOpacity = 0;
            yield return CaptureMotion("flight", journey.Beats[flight].Duration, () => Click("JourneyAdvanceButton"),
                () => view.JourneyView.IsAnimating, () =>
                {
                    if (controller.BeatIndex != flight) return;
                    Assert.That(Owl().State, Is.EqualTo(SpiritPresentationState.Arrival));
                    arrivalOpacity = Mathf.Max(arrivalOpacity, Owl().GetLayer("Physical Arrival").color.a);
                });
            Assert.That(arrivalOpacity, Is.GreaterThan(.1f), "The watcher's arrival pose must show during the descent.");
            Assert.That(controller.BeatIndex, Is.EqualTo(flight + 1), "Landing continues to the beat after the flight.");
            Assert.That(Owl().State, Is.EqualTo(SpiritPresentationState.Idle));
            AssertRestPose();
            Vector2 landed = Contact();
            Assert.That(Vector2.Distance(landed, perch), Is.GreaterThan(.01f), "The flight carries the watcher off its perch.");

            yield return AdvanceTo(journey.Beats.Count - 1);
            yield return WaitForJourney();
            yield return CaptureMotion("transition", 0f, () =>
            {
                Click("JourneyAcceptButton");
                Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
                Assert.That(controller.IsBusy, Is.True);
                Assert.That(view == null || !view.DiceTray.IsRolling, Is.True);
            }, () => controller.IsBusy);
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(Owl().State, Is.EqualTo(SpiritPresentationState.Idle));
            Assert.That(Vector2.Distance(Contact(), landed), Is.LessThan(.00001f),
                "The watcher stays where it landed when the table opens.");

            var owl = Owl();
            string[] physicalLayers = { "Physical Rest", "Physical Arrival", "Physical Settle" };
            Vector3 Foot(string name)
            {
                var image = owl.GetLayer(name);
                var rect = image.rectTransform;
                return ContentReady.Portrait(controller).InverseTransformPoint(rect.TransformPoint(
                    image.EvaluateDisplacement(rect.pivot, rect.rect.size)));
            }
            Vector3[] feet = physicalLayers.Select(Foot).ToArray();
            void AssertFeet()
            {
                for (int index = 0; index < physicalLayers.Length; ++index)
                    Assert.That(Vector3.Distance(Foot(physicalLayers[index]), feet[index]), Is.LessThan(.01f),
                        physicalLayers[index] + " foot contact moved during table motion.");
            }

            float idle = owl.GetDuration(SpiritPresentationState.Idle);
            Assert.That(idle, Is.GreaterThan(0), "The watcher's idle loop must have a length to observe.");
            yield return CaptureMotion("idle", idle, null, null, () =>
            {
                Assert.That(owl.State, Is.EqualTo(SpiritPresentationState.Idle));
                AssertFeet();
            });
            yield return CaptureMotion("reaction", owl.GestureDuration, () =>
            {
                owl.React();
                Assert.That(owl.IsReacting, Is.True);
            }, () => owl.IsReacting, AssertFeet);
            Assert.That(owl.State, Is.EqualTo(SpiritPresentationState.Idle));
            AssertRestPose();
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
            yield return ContentReady.Routed(controller);
            yield return AdvanceTo(flight);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(view.JourneyView.IsAnimating, Is.True);
            var activeJourney = view.JourneyView;
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            yield return null;
            Assert.That(activeJourney == null || !activeJourney.IsAnimating, Is.True);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(ContentReady.PresentationRoot(controller).GetComponentsInChildren<SpiritPortraitImage>()
                .Any(layer => layer.isActiveAndEnabled), Is.False,
                "Returning to clues must remove the active flight presentation.");

            controller.SelectEncounter(EncounterId);
            yield return ContentReady.Routed(controller);
            yield return AdvanceTo(flight - 1);
            yield return WaitForJourney();
            Assert.That(Owl().State, Is.EqualTo(SpiritPresentationState.Waiting));
            Vector2 perch = Contact();
            yield return new WaitForSecondsRealtime(journey.Beats[flight].Duration + .1f);
            Assert.That(controller.BeatIndex, Is.EqualTo(flight - 1), "The canceled flight must not advance the new visit.");
            yield return AdvanceTo(journey.Beats.Count - 1);
            yield return WaitForJourney();
            Click("JourneyAcceptButton");
            yield return ContentReady.Routed();
            yield return WaitForPresentation();
            Assert.That(Owl().State, Is.EqualTo(SpiritPresentationState.Idle));
            Vector2 table = Contact();
            Assert.That(Vector2.Distance(table, perch), Is.GreaterThan(.01f), "The watcher plays from the table, not its perch.");
            controller.Abandon();
            yield return WaitForPresentation();
            var abandoned = controller.Snapshot;
            Click("EndingNewRunButton");
            yield return ContentReady.Routed();
            yield return null;
            controller.SelectEncounter(EncounterId);
            yield return ContentReady.Routed(controller);
            Assert.That(controller.CanSkipIntro, Is.True);
            yield return AdvanceTo(flight);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(view.JourneyView.IsAnimating, Is.True);
            Click("JourneySkipButton");
            yield return ContentReady.Routed();
            yield return WaitForJourney();
            yield return new WaitForSecondsRealtime(journey.Beats[flight].Duration + .1f);
            Assert.That(controller.BeatIndex, Is.EqualTo(journey.Beats.Count - 1));
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(controller.Snapshot, Is.SameAs(abandoned));
            Assert.That(controller.HasActiveEncounter, Is.False);
            Assert.That(Owl().State, Is.EqualTo(SpiritPresentationState.Idle));
            Assert.That(Vector2.Distance(Contact(), table), Is.LessThan(.00001f),
                "Skipping the flight settles the watcher where it sits at the table.");
            AssertRestPose();
            yield return CheckPortraitSizes("revisit-skipped");
            activeJourney = view.JourneyView;
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            yield return null;
            Assert.That(activeJourney == null || !activeJourney.IsAnimating, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        // The flight is the journey's automatic arrival; the beat before it is the perch and the beat after it the landing.
        private int FlightIndex()
        {
            for (int index = 0; index < journey.Beats.Count; ++index)
            {
                var beat = journey.Beats[index];
                if (beat.Kind != EncounterJourneyBeatKind.Arrival || !beat.AutoAdvance) continue;
                Assert.That(index, Is.InRange(1, journey.Beats.Count - 2),
                    "The watcher's flight needs a perch beat before it and a landing beat after it.");
                return index;
            }
            Assert.Fail("The watchtower journey has no automatic arrival for the watcher's flight.");
            return -1;
        }

        private void AssertRestPose()
        {
            Assert.That(Owl().GetLayer("Physical Rest").color.a, Is.EqualTo(1).Within(.001f));
            Assert.That(Owl().GetLayer("Physical Arrival").color.a, Is.Zero);
            Assert.That(Owl().GetLayer("Physical Settle").color.a, Is.Zero);
        }

        private SpiritHostPortrait Owl()
        {
            Assert.That(view.HostPortrait, Is.TypeOf<SpiritHostPortrait>());
            return (SpiritHostPortrait)view.HostPortrait;
        }

        private Vector2 Contact()
        {
            var layer = Owl().GetLayer("Physical Rest").rectTransform;
            return new Vector2(layer.anchoredPosition.x / 540f, layer.anchoredPosition.y / 960f);
        }

        private IEnumerator AdvanceTo(int index)
        {
            while (controller.BeatIndex != index)
            {
                yield return WaitForJourney();
                if (controller.BeatIndex == index) break;
                Click("JourneyAdvanceButton");
                yield return ContentReady.Routed();
            }
            yield return null;
        }

        private IEnumerator WaitForJourney()
        {
            yield return ContentReady.Routed();
            float deadline = Time.realtimeSinceStartup + 15;
            while (view.JourneyView.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(view.JourneyView.IsAnimating, Is.False, controller.JourneyStatus.ToString());
            yield return new WaitForSecondsRealtime(.15f);
        }

        private IEnumerator WaitForPresentation()
        {
            yield return ContentReady.Routed();
            float deadline = Time.realtimeSinceStartup + 45;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False, controller.Status.ToString());
            yield return null;
        }

        private IEnumerator CheckPortraitSizes(string name)
        {
            var portrait = ContentReady.Portrait();
            Assert.That(portrait, Is.Not.Null);
            foreach (var size in new[] { new Vector2(443, 912), new Vector2(540, 960) })
            {
                portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                portrait.sizeDelta = size;
                portrait.anchoredPosition = Vector2.zero;
                yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (var label in ContentReady.PresentationRoot(controller).GetComponentsInChildren<Text>())
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
            var result = ContentReady.PresentationRoot(controller).GetComponentsInChildren<T>(true).FirstOrDefault(value => value.name == name);
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
