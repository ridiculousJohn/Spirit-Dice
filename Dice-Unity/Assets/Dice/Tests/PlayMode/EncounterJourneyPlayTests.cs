using System;
using System.Collections;
using System.Collections.Generic;
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
    public sealed class EncounterJourneyPlayTests
    {
        private SpiritEncounterController controller;
        private SpiritEncounterView view;

        [UnitySetUp]
        public IEnumerator InvestigationReady()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit(false);
            controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            view = controller.GetComponent<SpiritEncounterView>();
            SetField(controller, "useFixedSeed", true);
            SetField(controller, "initialSeed", "60");
            yield return null;
        }

        [UnityTest]
        public IEnumerator AuthoredLeadArrivalBargainAndInvitationReachOneAnimatedOpeningRoll()
        {
            var journey = controller.JourneyCatalog.Entries[0];
            int resolutions = 0;
            controller.Resolved += _ => ++resolutions;
            Assert.That(journey.EncounterId, Is.EqualTo("encounter.fox.introduction"));
            Assert.That(journey.LocationTitle, Is.EqualTo("The Last Lantern"));
            Assert.That(journey.LeadText, Is.EqualTo("A missing traveler was last seen following a lantern through the rain. The light is still burning."));
            Assert.That(journey.Beats.Count, Is.EqualTo(4));
            Assert.That(journey.Beats.Select(value => value.Id), Is.EqualTo(new[] { "approach", "arrival", "bargain", "invitation" }));
            Assert.That(journey.Beats[2].Text, Is.EqualTo("They sheltered here. I remember where they went. Win, and I will tell you."));
            Assert.That(journey.Beats[3].Text, Is.EqualTo("Your first mistake costs you nothing."));
            Assert.That(journey.Beats.Select(value => value.Kind), Does.Contain(EncounterJourneyBeatKind.Arrival));
            Assert.That(journey.Beats.Select(value => value.Kind), Does.Contain(EncounterJourneyBeatKind.Dialogue));
            AssertNoEngine(resolutions);
            Assert.That(ActiveText(), Does.Contain(journey.LocationTitle));
            Assert.That(ActiveText(), Does.Contain(journey.LeadText));
            AssertGameplayVisible(false);
            yield return CheckPortraitSizes("investigation");
            yield return Capture("investigation");
            Click("ClueButton0");
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(view.JourneyView.IsAnimating, Is.True, "The approach must visibly carry its moving silhouette.");
            yield return Capture("approach-shadow");

            for (int index = 0; index < journey.Beats.Count; ++index)
            {
                if (journey.Beats[index].Kind == EncounterJourneyBeatKind.Arrival)
                {
                    yield return new WaitForSecondsRealtime(.7f);
                    Assert.That(view.JourneyView.IsAnimating, Is.True, "The arrival must part its panels visibly.");
                    yield return Capture("arrival-parting");
                }
                yield return WaitForJourney();
                Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
                Assert.That(controller.BeatIndex, Is.EqualTo(index));
                Assert.That(view.JourneyView.VisibleBeatIndex, Is.EqualTo(index));
                var beat = journey.Beats[index];
                Assert.That(Find<Text>("JourneyTitle").text, Is.EqualTo(beat.Title));
                Assert.That(Find<Text>("JourneyBody").text, Is.EqualTo(beat.Text));
                Assert.That(controller.CanSkipIntro, Is.False);
                Assert.That(Find<Button>("JourneySkipButton").gameObject.activeInHierarchy, Is.False);
                AssertNoEngine(resolutions);
                AssertGameplayVisible(false);
                if (beat.Kind == EncounterJourneyBeatKind.Arrival)
                {
                    Assert.That(view.HostPortrait, Is.Not.Null);
                    Assert.That(view.HostPortrait.GetComponentsInChildren<RawImage>()
                        .Any(image => image.gameObject.activeInHierarchy), Is.True,
                        "The table host must arrive before the gameplay HUD or engine dice.");
                }
                yield return CheckPortraitSizes(beat.Id);
                yield return Capture(beat.Id);
                if (beat.Kind != EncounterJourneyBeatKind.Invitation) Click("JourneyAdvanceButton");
            }

            var rule = journey.TablePresentation.RulePresentations.Single();
            Assert.That(rule.RuleId, Is.EqualTo(FirstRerollFreeRuleDefinition.StableId));
            Assert.That(Find<Text>("JourneyRuleText").text, Does.Contain(rule.DisplayName));
            Assert.That(Find<Text>("JourneyRuleText").text, Does.Contain(rule.Description));
            Assert.That(Find<Text>("JourneyRuleText").text.ToLowerInvariant(), Does.Contain("first reroll"));
            Assert.That(Find<Text>("JourneyRuleText").text.ToLowerInvariant(), Does.Contain("hand"));
            Click("JourneyAcceptButton");
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo(journey.EncounterId));
            Assert.That(controller.Snapshot.Revision, Is.Zero);
            Assert.That(controller.IsBusy, Is.True);
            Assert.That(view.DiceTray.IsRolling, Is.True, "Accept must present the initial roll physically.");
            Assert.That(resolutions, Is.EqualTo(1));
            var opening = controller.Snapshot;
            controller.AcceptEncounter();
            controller.AdvanceJourney();
            controller.SkipIntro();
            Assert.That(controller.Snapshot, Is.SameAs(opening));
            Assert.That(resolutions, Is.EqualTo(1));
            AssertGameplayVisible(true);
            yield return Capture("opening-roll");
            yield return WaitForPresentation();
            controller.AcceptEncounter();
            Assert.That(controller.Snapshot, Is.SameAs(opening));
            Assert.That(resolutions, Is.EqualTo(1), "Repeated acceptance must not roll or create another run.");
            var expected = EncounterSession.CreateEncounter(controller.Catalog, journey.EncounterId, 60);
            for (int index = 0; index < DiceValues.Count; ++index)
            {
                Assert.That(controller.Snapshot.CurrentDice[index], Is.EqualTo(expected.Snapshot.CurrentDice[index]));
                Assert.That(view.DiceTray.GetVisibleFace(index), Is.EqualTo(expected.Snapshot.CurrentDice[index]));
            }
            Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).VisitedEncounterIds,
                Does.Contain(journey.EncounterId));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FirstVisitCannotSkipAndPersistedRevisitCanSkipToTheInvitation()
        {
            var journey = controller.JourneyCatalog.Entries[0];
            Click("ClueButton0");
            yield return null;
            Assert.That(view.JourneyView.IsAnimating, Is.True);
            Assert.That(controller.CanSkipIntro, Is.False);
            controller.SkipIntro();
            Assert.That(controller.BeatIndex, Is.Zero);
            Click("JourneyReturnButton");
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(view.JourneyView.IsAnimating, Is.False, "Returning to clues must cancel the active intro motion.");
            Assert.That(controller.VisitedEncounterIds, Is.Empty);
            Click("ClueButton0");
            yield return AdvanceToInvitation();
            Click("JourneyAcceptButton");
            yield return WaitForPresentation();
            controller.Abandon();
            yield return WaitForPresentation();
            Click("EndingNewRunButton");
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.CanResume, Is.False);
            Click("ClueButton0");
            yield return null;
            Assert.That(view.JourneyView.IsAnimating, Is.True);
            Assert.That(controller.CanSkipIntro, Is.True);
            Assert.That(Find<Button>("JourneySkipButton").gameObject.activeInHierarchy, Is.True);
            Assert.That(Find<Button>("JourneySkipButton").IsInteractable(), Is.True);
            Click("JourneySkipButton");
            yield return WaitForJourney();
            Assert.That(controller.BeatIndex, Is.EqualTo(journey.Beats.Count - 1));
            Assert.That(journey.Beats[controller.BeatIndex].Kind, Is.EqualTo(EncounterJourneyBeatKind.Invitation));
            Assert.That(controller.CanSkipIntro, Is.False);
            Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).VisitedEncounterIds.Count, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ReturnResumeAndLoadRetainTheRunWithoutReplayingItsIntroOrOpeningRoll()
        {
            Click("ClueButton0");
            yield return AdvanceToInvitation();
            Click("JourneyAcceptButton");
            yield return WaitForPresentation();
            int resolutions = 0;
            controller.Resolved += _ => ++resolutions;
            Click("DieButton1");
            Click("RerollButton");
            yield return WaitForPresentation();
            Click("MenuButton");
            yield return null;
            Click("SaveRunButton");
            Assert.That(File.Exists(controller.SavePath), Is.True, controller.StatusMessage);
            var saved = EncounterSaveFileStore.Load(controller.SavePath);
            var beforeReturn = controller.Snapshot;
            int beforeReturnResolutions = resolutions;
            Click("ReturnToInvestigationButton");
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.Snapshot, Is.SameAs(beforeReturn));
            Assert.That(controller.CanResume, Is.True);
            AssertGameplayVisible(false);
            Click("ResumeEncounterButton");
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.BeatIndex, Is.EqualTo(-1));
            Assert.That(controller.Snapshot, Is.SameAs(beforeReturn));
            Assert.That(resolutions, Is.EqualTo(beforeReturnResolutions));
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(view.DiceTray.IsRolling, Is.False);
            Click("ReturnToInvestigationButton");
            yield return null;
            Click("LoadJourneyButton");
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.BeatIndex, Is.EqualTo(-1));
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(view.DiceTray.IsRolling, Is.False);
            Assert.That(resolutions, Is.EqualTo(beforeReturnResolutions));
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(saved.Revision));
            Assert.That(controller.Snapshot.RerollsRemaining, Is.EqualTo(saved.RerollsRemaining));
            Assert.That(controller.Snapshot.RerollsSpentOnHand, Is.EqualTo(saved.RerollsSpentOnHand));
            for (int index = 0; index < DiceValues.Count; ++index)
                Assert.That(view.DiceTray.GetVisibleFace(index), Is.EqualTo(saved.CurrentDice[index]));
            controller.Save();
            Assert.That(EncounterSaveCodec.Serialize(EncounterSaveFileStore.Load(controller.SavePath)),
                Is.EqualTo(EncounterSaveCodec.Serialize(saved)), "Resume/load must preserve both random stream states.");
            yield return Capture("loaded-table");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TableModalsCoverCluesAndClosingRestoresItsNavigation()
        {
            Click("ClueButton0");
            yield return AdvanceToInvitation();
            Click("JourneyAcceptButton");
            yield return WaitForPresentation();
            var portrait = view.transform.Find("SpiritCanvas/SafeArea/Portrait") as RectTransform;
            var clues = Find<Button>("ReturnToInvestigationButton");
            var overlays = new[] { "CharmsOverlay", "HandRanksOverlay", "MenuOverlay" };
            var openButtons = new[] { "CharmsButton", "HandRanksButton", "MenuButton" };
            var closeButtons = new[] { "CloseCharmsButton", "CloseHandRanksButton", "CloseMenuButton" };
            var titles = new[] { "CharmsTitle", "HandRanksTitle", "MenuTitle" };

            foreach (var size in new[] { new Vector2(443, 912), new Vector2(540, 960) })
            {
                portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                portrait.sizeDelta = size;
                portrait.anchoredPosition = Vector2.zero;
                yield return null;
                Canvas.ForceUpdateCanvases();
                for (int index = 0; index < overlays.Length; ++index)
                {
                    Click(openButtons[index]);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    var overlay = Find<RectTransform>(overlays[index]);
                    var title = Find<Text>(titles[index]);
                    Assert.That(overlay.gameObject.activeInHierarchy, Is.True, overlay.name);
                    Assert.That(overlay.parent, Is.SameAs(clues.transform.parent), overlay.name);
                    Assert.That(overlay.GetSiblingIndex(), Is.GreaterThan(clues.transform.GetSiblingIndex()),
                        overlay.name + " must draw over the table's Clues button.");
                    Assert.That(overlay.GetComponent<Graphic>().depth, Is.GreaterThan(clues.targetGraphic.depth),
                        overlay.name + " must cover the entire Clues button.");
                    Assert.That(title.depth, Is.GreaterThan(clues.targetGraphic.depth), title.name);
                    AssertCompleteText(title);
                    foreach (var rect in new[] { (RectTransform)clues.transform, title.rectTransform })
                    {
                        var pointer = new PointerEventData(EventSystem.current)
                        {
                            position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
                        };
                        var hits = new List<RaycastResult>();
                        EventSystem.current.RaycastAll(pointer, hits);
                        Assert.That(hits, Is.Not.Empty, overlay.name + ": " + rect.name);
                        Assert.That(hits[0].gameObject.transform.IsChildOf(overlay), Is.True,
                            overlay.name + " must receive input above " + rect.name);
                        Assert.That(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),
                            Is.Not.SameAs(clues.gameObject), overlay.name);
                    }
                    yield return Capture("modal-" + overlays[index] + "-" + size.x);
                    Click(closeButtons[index]);
                    yield return null;
                    Assert.That(overlay.gameObject.activeInHierarchy, Is.False, overlay.name);
                    Click("ReturnToInvestigationButton");
                    yield return null;
                    Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
                    Click("ResumeEncounterButton");
                    yield return null;
                    Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ASecondSyntheticEntryUsesItsOwnLeadDialogueTableAndVictoryResult()
        {
            InstallSyntheticCatalog();
            var second = controller.JourneyCatalog.Entries[1];
            Assert.That(ActiveText(), Does.Contain("The Observatory"));
            yield return null;
            Canvas.ForceUpdateCanvases();
            Find<ScrollRect>("InvestigationClues").verticalNormalizedPosition = 0;
            yield return null;
            Click("ClueButton1");
            yield return WaitForJourney();
            Assert.That(controller.SelectedJourney, Is.SameAs(second));
            Assert.That(Find<Text>("JourneyTitle").text, Is.EqualTo("The brass stair"));
            Assert.That(Find<Text>("JourneyBody").text, Is.EqualTo("A blue filament leads above the clouds."));
            Assert.That(ActiveText(), Does.Not.Contain("Lantern"));
            yield return AdvanceToInvitation();
            Assert.That(Find<Text>("JourneyBody").text, Is.EqualTo("Set one constellation in the astrolabe."));
            Assert.That(view.HostPortrait, Is.Null, "A table without an authored host must not retain the previous host.");
            yield return CheckPortraitSizes();
            Click("JourneyAcceptButton");
            yield return WaitForPresentation();
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo("encounter.journey-test.observatory"));
            Click("PlayButton");
            yield return WaitForPresentation();
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(Find<Text>("EndingTitle").text, Is.EqualTo("A chart recovered"));
            Assert.That(ActiveText(), Does.Contain("The chart points to the northern platform."));
            Assert.That(Find<Button>("EndingNewRunButton").GetComponentInChildren<Text>().text,
                Is.EqualTo("Follow the chart"));
            yield return CheckPortraitSizes();
            yield return Capture("synthetic-victory");
            Click("EndingNewRunButton");
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.CanResume, Is.False);
            Assert.That(ActiveText(), Does.Contain("The northern platform"));
            Assert.That(ActiveText(), Does.Contain("An annotated chart identifies the next place to search."));
            Assert.That(controller.CompletedEncounterIds, Does.Contain(second.EncounterId));
            yield return CheckPortraitSizes();
            yield return Capture("synthetic-victory-board");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SyntheticLossReturnsToTheBoardWithoutRecordingVictoryOrOfferingResume()
        {
            InstallSyntheticCatalog();
            yield return null;
            Canvas.ForceUpdateCanvases();
            Click("ClueButton0");
            yield return AdvanceToInvitation();
            Click("JourneyAcceptButton");
            yield return WaitForPresentation();
            Click("PlayButton");
            yield return WaitForPresentation();
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Lost));
            Assert.That(Find<Text>("EndingTitle").text, Is.EqualTo("The tide takes the map"));
            Assert.That(ActiveText(), Does.Contain("The archivist keeps the answer."));
            Assert.That(controller.CompletedEncounterIds, Is.Empty);
            yield return CheckPortraitSizes();
            yield return Capture("synthetic-loss");
            Click("EndingNewRunButton");
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.CanResume, Is.False);
            Assert.That(Find<Button>("ResumeEncounterButton").gameObject.activeInHierarchy, Is.False);
            yield return Capture("synthetic-loss-board");
            LogAssert.NoUnexpectedReceived();
        }

        private void InstallSyntheticCatalog()
        {
            var losing = new EncounterDefinition("encounter.journey-test.archive",
                new[] { new StageDefinition(1000000, 1, 0) });
            var winning = new EncounterDefinition("encounter.journey-test.observatory",
                new[] { new StageDefinition(1, 1, 0) });
            var catalog = new MechanicalCatalog("journey-playmode.v1", controller.Catalog.Hands,
                new[] { losing, winning }, Array.Empty<ModifierDefinition>());
            var first = CreateJourney(losing.Id, "The Tidal Archive", "The Archivist",
                "A watermarked map carries a name erased from the register.",
                "The flooded shelf", "Follow the waterline into the archive.",
                "Place one name in the register.", "A name restored", "The register yields a route.",
                "The tide takes the map", "The archivist keeps the answer.", "Search the archive");
            var second = CreateJourney(winning.Id, "The Observatory", "The Astronomer",
                "A scorched chart records a signal above the clouds.",
                "The brass stair", "A blue filament leads above the clouds.",
                "Set one constellation in the astrolabe.", "A chart recovered",
                "The chart points to the northern platform.", "The stars fall quiet",
                "The astronomer closes the dome.", "Follow the chart");
            SetField(second, "completionClueTitle", "The northern platform");
            SetField(second, "completionClueText", "An annotated chart identifies the next place to search.");
            var journeys = ScriptableObject.CreateInstance<EncounterJourneyCatalogAsset>();
            SetField(journeys, "title", "Unfinished inquiries");
            SetField(journeys, "description", "Two witnesses hold unrelated answers.");
            SetField(journeys, "entries", new[] { first, second });
            Assert.That(controller.InitializeJourney(catalog, journeys, controller.SavePath), Is.True,
                controller.JourneyStatus);
        }

        private static EncounterJourneyAsset CreateJourney(string encounterId, string location, string spirit,
            string lead, string title, string approach, string invitation, string victoryTitle, string victory,
            string defeatTitle, string defeat, string continueLabel)
        {
            var table = ScriptableObject.CreateInstance<EncounterTablePresentationAsset>();
            SetField(table, "displayTitle", location);
            var journey = ScriptableObject.CreateInstance<EncounterJourneyAsset>();
            SetField(journey, "encounterId", encounterId);
            SetField(journey, "locationTitle", location);
            SetField(journey, "spiritName", spirit);
            SetField(journey, "leadText", lead);
            SetField(journey, "tablePresentation", table);
            SetField(journey, "victoryTitle", victoryTitle);
            SetField(journey, "victoryText", victory);
            SetField(journey, "defeatTitle", defeatTitle);
            SetField(journey, "defeatText", defeat);
            SetField(journey, "abandonText", "The witness waits for another inquiry.");
            SetField(journey, "resultContinueLabel", continueLabel);
            SetField(journey, "beats", new[]
            {
                Beat("approach", EncounterJourneyBeatKind.Illustration, title, approach),
                Beat("arrival", EncounterJourneyBeatKind.Arrival, "A witness waits", "The room opens around the table."),
                Beat("dialogue", EncounterJourneyBeatKind.Dialogue, spirit, "One answer for one completed bargain."),
                Beat("invitation", EncounterJourneyBeatKind.Invitation, "The terms", invitation)
            });
            return journey;
        }

        private static EncounterJourneyBeat Beat(string id, EncounterJourneyBeatKind kind, string title, string text)
        {
            var beat = new EncounterJourneyBeat();
            SetField(beat, "id", id);
            SetField(beat, "kind", kind);
            SetField(beat, "title", title);
            SetField(beat, "text", text);
            SetField(beat, "continueLabel", "Continue");
            SetField(beat, "duration", .05f);
            return beat;
        }

        private void AssertNoEngine(int resolutions)
        {
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.Preview, Is.Null);
            Assert.That(controller.HasActiveEncounter, Is.False);
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(resolutions, Is.Zero);
            Assert.That(typeof(SpiritEncounterController).GetField("application", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller), Is.Null, "The narrative must not create an engine or consume its random streams.");
            Assert.That(File.Exists(controller.SavePath), Is.False);
        }

        private void AssertGameplayVisible(bool visible)
        {
            foreach (string name in new[] { "PlayingPanel", "TargetPanel", "HandsPanel", "RerollsPanel" })
                Assert.That(Find<RectTransform>(name).gameObject.activeInHierarchy, Is.EqualTo(visible), name);
            if (!visible) Assert.That(view.DiceTray.IsRolling, Is.False);
        }

        private IEnumerator AdvanceToInvitation()
        {
            yield return WaitForJourney();
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
            {
                Click("JourneyAdvanceButton");
                yield return WaitForJourney();
            }
        }

        private IEnumerator WaitForJourney()
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (view.JourneyView.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(view.JourneyView.IsAnimating, Is.False, controller.JourneyStatus);
            yield return new WaitForSecondsRealtime(.15f);
            if (controller.JourneyPhase == EncounterJourneyPhase.Sequence)
            {
                bool invitation = controller.SelectedJourney.Beats[controller.BeatIndex].Kind == EncounterJourneyBeatKind.Invitation;
                var button = Find<Button>(invitation ? "JourneyAcceptButton" : "JourneyAdvanceButton");
                Assert.That(button.gameObject.activeInHierarchy, Is.True, button.name);
                Assert.That(button.IsInteractable(), Is.True, button.name);
            }
        }

        private IEnumerator WaitForPresentation()
        {
            float deadline = Time.realtimeSinceStartup + 45;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False, controller.StatusMessage);
            Assert.That(view.DiceTray.IsRolling, Is.False);
            yield return null;
        }

        private IEnumerator CheckPortraitSizes(string captureName = null)
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
                    if (!label.isActiveAndEnabled || string.IsNullOrWhiteSpace(label.text)) continue;
                    AssertCompleteText(label);
                    var boundary = label.GetComponentInParent<ScrollRect>() != null
                        ? label.GetComponentInParent<Button>()?.transform as RectTransform : portrait;
                    if (boundary == null) continue;
                    var corners = new Vector3[4];
                    label.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        Vector2 local = boundary.InverseTransformPoint(corner);
                        Assert.That(local.x, Is.InRange(boundary.rect.xMin - 1, boundary.rect.xMax + 1), label.name);
                        Assert.That(local.y, Is.InRange(boundary.rect.yMin - 1, boundary.rect.yMax + 1), label.name);
                    }
                }
                if (captureName != null) yield return Capture(captureName + "-" + size.x);
            }
        }

        private static void AssertCompleteText(Text label)
        {
            var rect = label.rectTransform.rect;
            var settings = label.GetGenerationSettings(rect.size);
            var generated = label.cachedTextGenerator;
            Assert.That(generated.Populate(label.text, settings), Is.True, label.name);
            Assert.That(generated.vertexCount, Is.GreaterThan(4), label.name + " has no rendered glyphs.");
            float scale = 1 / label.pixelsPerUnit;
            for (int index = 0; index < generated.verts.Count - 4; ++index)
            {
                Vector2 position = generated.verts[index].position * scale;
                Assert.That(position.x, Is.InRange(rect.xMin - 3, rect.xMax + 3), label.name + ": " + label.text);
                Assert.That(position.y, Is.InRange(rect.yMin - 3, rect.yMax + 3), label.name + ": " + label.text);
            }
            settings.fontSize = label.resizeTextForBestFit ? generated.fontSizeUsedForBestFit : label.fontSize;
            settings.resizeTextForBestFit = false;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            using (var complete = new TextGenerator())
            {
                Assert.That(complete.Populate(label.text, settings), Is.True, label.name);
                Assert.That(generated.vertexCount, Is.EqualTo(complete.vertexCount),
                    label.name + " truncates glyphs or whole lines: " + label.text);
            }
        }

        private string ActiveText() => string.Join("\n", view.GetComponentsInChildren<Text>()
            .Where(label => label.isActiveAndEnabled).Select(label => label.text));

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

        private static IEnumerator Capture(string name)
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) yield break;
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "journey-" + name + ".png");
            Assert.That(File.Exists(path), Is.False, "Use a fresh capture directory for each validation run.");
            ScreenCapture.CaptureScreenshot(path);
            for (int frame = 0; frame < 240 && !File.Exists(path); ++frame) yield return null;
            Assert.That(File.Exists(path), Is.True, path);
            TestContext.WriteLine("Encounter journey screenshot: " + path);
        }
    }
}
