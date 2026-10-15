using System;
using System.Collections;
using System.Collections.Generic;
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
                if (index == 3)
                {
                    Assert.That(Owl().Progress, Is.EqualTo(1).Within(.001f));
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
            Click("JourneyAcceptButton");
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo(EncounterId));
            Assert.That(controller.Snapshot.Revision, Is.Zero);
            Assert.That(controller.IsBusy, Is.True);
            Assert.That(view.DiceTray.IsRolling, Is.True);
            Assert.That(resolutions, Is.EqualTo(1));
            var opening = controller.Snapshot;
            controller.AcceptEncounter();
            Assert.That(controller.Snapshot, Is.SameAs(opening));
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
            yield return AdvanceTo("invitation");
            yield return WaitForJourney();
            Click("JourneyAcceptButton");
            yield return WaitForPresentation();
            controller.Abandon();
            yield return WaitForPresentation();
            Click("EndingNewRunButton");
            yield return null;
            controller.SelectEncounter(EncounterId);
            Assert.That(controller.CanSkipIntro, Is.True);
            yield return AdvanceTo("flight");
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(view.JourneyView.IsAnimating, Is.True);
            Click("JourneySkipButton");
            yield return WaitForJourney();
            Assert.That(controller.BeatIndex, Is.EqualTo(journey.Beats.Count - 1));
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
