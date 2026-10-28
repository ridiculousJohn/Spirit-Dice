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
    /** The moth's own choreography: its lantern moths circle while it waits and carry that flight into its arrival. */
    public sealed class MothJourneyPlayTests
    {
        private const string EncounterId = "encounter.moth.crossroads";
        private const string LanternMothLayerPrefix = "Lantern moth";
        private SpiritEncounterController controller;
        private SpiritEncounterView view => ContentReady.EncounterView(controller);
        private EncounterJourneyAsset journey;

        [UnitySetUp]
        public IEnumerator InvestigationReady()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            ContentReady.SeedJourneyPrerequisites(controller, EncounterId);
            journey = controller.JourneyCatalog.Find(EncounterId);
            Assert.That(journey, Is.Not.Null, EncounterId);
            SetField(controller, "useFixedSeed", true);
            SetField(controller, "initialSeed", "60");
            yield return null;
        }

        [UnityTest]
        [Explicit("Art-generation check; run it by name while the moth art is being changed.")]
        [Category("ArtAuthoring")]
        public IEnumerator MothArrivalStartsFromTheVisibleWaitingFlightOfItsLanternMoths()
        {
            int arrival = ArrivalIndex();
            controller.SelectEncounter(EncounterId);
            yield return ContentReady.Routed(controller);
            while (controller.BeatIndex < arrival - 1) controller.AdvanceJourney();
            yield return WaitForJourney();
            Assert.That(controller.BeatIndex, Is.EqualTo(arrival - 1));
            var moths = LanternMoths();
            Assert.That(moths.All(moth => moth != null && moth.isActiveAndEnabled), Is.True,
                "The lantern moths are visible while the moth waits.");
            // Waiting moths circle the lantern, so their pose is sampled in the same frame as the click that starts the arrival.
            var positions = moths.Select(moth => moth.rectTransform.anchoredPosition).ToArray();
            var rotations = moths.Select(moth => moth.rectTransform.localRotation).ToArray();
            Click("JourneyAdvanceButton");
            Assert.That(controller.BeatIndex, Is.EqualTo(arrival));
            Assert.That(Moth().State, Is.EqualTo(SpiritPresentationState.Arrival));
            for (int index = 0; index < moths.Length; ++index)
            {
                Assert.That(Vector2.Distance(moths[index].rectTransform.anchoredPosition, positions[index]),
                    Is.LessThan(.01f), moths[index].name + ": the arrival must start from the visible waiting position.");
                Assert.That(Quaternion.Angle(moths[index].rectTransform.localRotation, rotations[index]),
                    Is.LessThan(.01f), moths[index].name + ": the arrival must preserve the visible banking angle.");
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Explicit("Inspection capture of the moth journey's motion, written under DICE_SPIRIT_CAPTURE_ROOT.")]
        [Category("Capture")]
        public IEnumerator MothJourneyMotionCapture()
        {
            string captureRoot = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(captureRoot)) Assert.Ignore("Set DICE_SPIRIT_CAPTURE_ROOT to capture the moth journey's motion.");
            captureRoot = Path.Combine(captureRoot, "moth-motion-" +
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture));
            Assert.That(Directory.Exists(captureRoot), Is.False);
            Directory.CreateDirectory(captureRoot);
            TestContext.WriteLine("Moth motion capture: " + captureRoot);

            controller.SelectEncounter(EncounterId);
            yield return ContentReady.Routed(controller);
            var portrait = ContentReady.Portrait();
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(540, 960);
            portrait.anchoredPosition = Vector2.zero;
            yield return null;
            Canvas.ForceUpdateCanvases();

            // Each beat is held for its authored motion, or briefly when it has none.
            for (int index = 0; index < journey.Beats.Count; ++index)
            {
                if (controller.BeatIndex > index) continue;
                Action begin = controller.BeatIndex == index - 1 ? () => Click("JourneyAdvanceButton") : (Action)null;
                yield return CaptureMotion(journey.Beats[index].Id, Mathf.Max(journey.Beats[index].Duration, 2f), begin,
                    () => view.JourneyView.IsAnimating);
            }
            yield return CaptureMotion("accept", .5f, () => Click("JourneyAcceptButton"), () => controller.IsBusy);
            if (view.JourneyView.EncounterCharms.IsOpen)
            {
                yield return WaitUntilClickable("BeginEncounterButton");
                yield return CaptureMotion("opening", .5f, () => Click("BeginEncounterButton"), () => controller.IsBusy);
            }
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            yield return CaptureMotion("idle", Moth().GetDuration(SpiritPresentationState.Idle));
            yield return CaptureMotion("reaction", Moth().GestureDuration, () => Moth().React(), () => Moth().IsReacting);

            IEnumerator CaptureMotion(string phase, float seconds, Action start = null, Func<bool> pending = null)
            {
                string directory = Path.Combine(captureRoot, phase);
                Directory.CreateDirectory(directory);
                var paths = new List<string>();
                using var timestamps = File.CreateText(Path.Combine(directory, "timestamps.csv"));
                timestamps.WriteLine("frame,seconds,realtime_seconds,file,state,progress");
                yield return new WaitForEndOfFrame();
                double started = Time.realtimeSinceStartupAsDouble;
                double nextCapture = 0;
                bool began = false;
                while (true)
                {
                    double now = Time.realtimeSinceStartupAsDouble;
                    double elapsed = now - started;
                    bool complete = began && elapsed >= seconds && (pending == null || !pending());
                    if (elapsed >= nextCapture || complete)
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
                        nextCapture = elapsed + .04;
                    }
                    if (!began)
                    {
                        start?.Invoke();
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
                TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture, "Moth phase {0}: {1:F3}s, {2} frames.",
                    phase, Time.realtimeSinceStartupAsDouble - started, paths.Count));
            }
        }

        // The moth's arrival is its journey's arrival beat; the beat before it shows the moth waiting at the lantern.
        private int ArrivalIndex()
        {
            for (int index = 0; index < journey.Beats.Count; ++index)
                if (journey.Beats[index].Kind == EncounterJourneyBeatKind.Arrival)
                {
                    Assert.That(index, Is.GreaterThan(0), "The moth's arrival needs a waiting beat before it.");
                    return index;
                }
            Assert.Fail("The moth journey has no arrival beat.");
            return -1;
        }

        private SpiritPortraitImage[] LanternMoths()
        {
            var host = journey.TablePresentation.HostPresentation;
            Assert.That(host, Is.Not.Null);
            var moths = host.Layers.Where(layer => layer.Id.StartsWith(LanternMothLayerPrefix, StringComparison.Ordinal))
                .Select(layer => Moth().GetLayer(layer.Id)).ToArray();
            Assert.That(moths, Is.Not.Empty, "The moth host has no lantern moth layers.");
            return moths;
        }

        private SpiritHostPortrait Moth()
        {
            Assert.That(view.HostPortrait, Is.Not.Null);
            return view.HostPortrait;
        }

        private IEnumerator WaitForJourney()
        {
            yield return ContentReady.Routed();
            float deadline = Time.realtimeSinceStartup + 30;
            while (view.JourneyView.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(view.JourneyView.IsAnimating, Is.False, controller.JourneyStatus.ToString());
            yield return new WaitForSecondsRealtime(.15f);
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
            Assert.That(EventSystem.current, Is.Not.Null);
            var handler = TopClickHandler(button, out var pointer, out var hit);
            Assert.That(hit.gameObject, Is.Not.Null, name);
            Assert.That(handler, Is.SameAs(button.gameObject), "A graphic blocks " + name);
            pointer.pointerCurrentRaycast = hit;
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }

        // Newly shown graphics are skipped by GraphicRaycaster until the canvas assigns their depth when rendering.
        private IEnumerator WaitUntilClickable(string name)
        {
            var button = Find<Button>(name);
            float deadline = Time.realtimeSinceStartup + 1;
            while (TopClickHandler(button, out _, out _) != button.gameObject)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), name + " never became clickable.");
                yield return null;
            }
        }

        private static GameObject TopClickHandler(Button button, out PointerEventData pointer, out RaycastResult hit)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left,
                pointerId = -1
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            hit = hits.Count > 0 ? hits[0] : default;
            return hits.Count > 0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) : null;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }
}
