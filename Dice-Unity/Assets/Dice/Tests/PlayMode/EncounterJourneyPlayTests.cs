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
        private IEncounterJourneyControls presentedJourney;
        private SpiritEncounterView view => ContentReady.EncounterView(controller);

        [UnitySetUp]
        public IEnumerator InvestigationReady()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            presentedJourney = null;
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
            Assert.That(journey.Beats.Count, Is.EqualTo(6));
            Assert.That(journey.Beats.Select(value => value.Id),
                Is.EqualTo(new[] { "approach", "arrival", "bargain", "discovery", "gift", "invitation" }));
            Assert.That(journey.Beats.Single(value => value.Id == "bargain").Text,
                Is.EqualTo("They sheltered here. I know where they went. Spirits trade knowledge through bargains. Win our game, and my answer is yours."));
            Assert.That(journey.Beats.Single(value => value.Id == "invitation").Text,
                Is.EqualTo("Your first mistake costs you nothing. Accept, and we begin."));
            Assert.That(journey.Beats.Select(value => value.Kind), Does.Contain(EncounterJourneyBeatKind.Arrival));
            Assert.That(journey.Beats.Select(value => value.Kind), Does.Contain(EncounterJourneyBeatKind.Dialogue));
            AssertNoEngine(resolutions);
            Assert.That(ActiveText(), Does.Contain(journey.LocationTitle));
            Assert.That(ActiveText(), Does.Contain(journey.LeadText));
            AssertGameplayVisible(false);
            yield return CheckPortraitSizes("investigation");
            yield return Capture("investigation");
            Click("ClueButton0");
            yield return ContentReady.Routed();
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
                ChapterJourneyPlayTests.AssertCharmPresentation(view, beat, controller.Catalog);
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
            Assert.That(Find<Text>("JourneyRuleName").text, Is.EqualTo(rule.DisplayName));
            Assert.That(Find<Text>("JourneyRuleText").text, Does.Contain(rule.Description));
            Assert.That(Find<Text>("JourneyRuleText").text.ToLowerInvariant(), Does.Contain("first reroll"));
            Assert.That(Find<Text>("JourneyRuleText").text.ToLowerInvariant(), Does.Contain("hand"));
            Click("JourneyAcceptButton");
            yield return ContentReady.Routed();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo(journey.EncounterId));
            Assert.That(controller.Snapshot.Revision, Is.Zero);
            Assert.That(controller.IsBusy, Is.True);
            Assert.That(view.DiceTray.IsRolling, Is.True, "Accept must present the initial roll physically.");
            Assert.That(resolutions, Is.EqualTo(1));
            var opening = controller.Snapshot;
            controller.AcceptEncounter();
            yield return ContentReady.Routed(controller);
            controller.AdvanceJourney();
            controller.SkipIntro();
            Assert.That(controller.Snapshot, Is.SameAs(opening));
            Assert.That(resolutions, Is.EqualTo(1));
            AssertGameplayVisible(true);
            yield return Capture("opening-roll");
            yield return WaitForPresentation();
            controller.AcceptEncounter();
            yield return ContentReady.Routed(controller);
            Assert.That(controller.Snapshot, Is.SameAs(opening));
            Assert.That(resolutions, Is.EqualTo(1), "Repeated acceptance must not roll or create another run.");
            var expected = EncounterSession.CreateEncounter(controller.Catalog, journey.EncounterId, 60);
            for (int index = 0; index < DiceValues.Count; ++index)
            {
                Assert.That(controller.Snapshot.CurrentDice[index], Is.EqualTo(expected.Snapshot.CurrentDice[index]));
            }
            AssertVisibleDice(expected.Snapshot.CurrentDice);
            Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).VisitedEncounterIds,
                Does.Contain(journey.EncounterId));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator IntroButtonsAdvanceImmediatelyAndAcceptStartsOnlyOneEncounter()
        {
            var journey = controller.JourneyCatalog.Entries[0];
            int resolutions = 0;
            controller.Resolved += _ => ++resolutions;
            Click("ClueButton0");
            yield return ContentReady.Routed();
            for (int index = 0; index < journey.Beats.Count - 1; ++index)
            {
                yield return null;
                Assert.That(controller.BeatIndex, Is.EqualTo(index));
                Assert.That(view.JourneyView.IsAnimating, Is.True);
                Assert.That(Find<Button>("JourneyAdvanceButton").IsInteractable(), Is.True);
                Click("JourneyAdvanceButton");
                yield return ContentReady.Routed();
            }
            yield return null;
            Assert.That(view.JourneyView.IsAnimating, Is.True);
            Assert.That(Find<Button>("JourneyAcceptButton").IsInteractable(), Is.True);
            Click("JourneyAcceptButton");
            yield return ContentReady.Routed();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(resolutions, Is.EqualTo(1));
            yield return WaitForPresentation();
            Assert.That(view.JourneyView.IsAnimating, Is.False);
            Assert.That(resolutions, Is.EqualTo(1), "Interrupted intro beats must not continue after acceptance.");
            Assert.That(controller.Snapshot.Revision, Is.Zero);
            Assert.That(controller.ProfileUnlockedModifierIds, Does.Contain("modifier.initial_roll.first_hand.base_score"));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FirstVisitCannotSkipAndPersistedRevisitCanSkipToTheInvitation()
        {
            var journey = controller.JourneyCatalog.Entries[0];
            Click("ClueButton0");
            yield return ContentReady.Routed();
            yield return null;
            Assert.That(view.JourneyView.IsAnimating, Is.True);
            Assert.That(controller.CanSkipIntro, Is.False);
            controller.SkipIntro();
            Assert.That(controller.BeatIndex, Is.Zero);
            var departingSequence = view.JourneyView;
            Click("JourneyReturnButton");
            yield return ContentReady.Routed();
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(departingSequence == null || !departingSequence.IsAnimating, Is.True,
                "Returning to clues must cancel or unload the active intro motion.");
            Assert.That(view, Is.Null, "Returning to clues must unload the encounter scene.");
            Assert.That(controller.VisitedEncounterIds, Is.Empty);
            Click("ClueButton0");
            yield return ContentReady.Routed();
            yield return AdvanceToInvitation();
            Click("JourneyAcceptButton");
            yield return ContentReady.Routed();
            yield return WaitForPresentation();
            controller.Abandon();
            yield return WaitForPresentation();
            Click("EndingNewRunButton");
            yield return ContentReady.Routed();
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.CanResume, Is.False);
            Click("ClueButton0");
            yield return ContentReady.Routed();
            yield return null;
            Assert.That(view.JourneyView.IsAnimating, Is.True);
            Assert.That(controller.CanSkipIntro, Is.True);
            Assert.That(Find<Button>("JourneySkipButton").gameObject.activeInHierarchy, Is.True);
            Assert.That(Find<Button>("JourneySkipButton").IsInteractable(), Is.True);
            Click("JourneySkipButton");
            yield return ContentReady.Routed();
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
            yield return ContentReady.Routed();
            yield return AdvanceToInvitation();
            Click("JourneyAcceptButton");
            yield return ContentReady.Routed();
            yield return WaitForPresentation();
            int resolutions = 0;
            controller.Resolved += _ => ++resolutions;
            Click("DieButton1");
            yield return ContentReady.Routed();
            Click("RerollButton");
            yield return ContentReady.Routed();
            yield return WaitForPresentation();
            Click("MenuButton");
            yield return ContentReady.Routed();
            yield return null;
            Click("SaveRunButton");
            yield return ContentReady.Routed();
            Assert.That(File.Exists(controller.SavePath), Is.True, controller.StatusMessage);
            var saved = EncounterSaveFileStore.Load(controller.SavePath);
            var beforeReturn = controller.Snapshot;
            int beforeReturnResolutions = resolutions;
            Click("ReturnToInvestigationButton");
            yield return ContentReady.Routed();
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.Snapshot, Is.SameAs(beforeReturn));
            Assert.That(controller.CanResume, Is.True);
            AssertGameplayVisible(false);
            Click("ResumeEncounterButton");
            yield return ContentReady.Routed();
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.BeatIndex, Is.EqualTo(-1));
            Assert.That(controller.Snapshot, Is.SameAs(beforeReturn));
            Assert.That(resolutions, Is.EqualTo(beforeReturnResolutions));
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(view == null || !view.DiceTray.IsRolling, Is.True);
            Click("ReturnToInvestigationButton");
            yield return ContentReady.Routed();
            yield return null;
            controller.Load();
            yield return ContentReady.Routed(controller);
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.BeatIndex, Is.EqualTo(-1));
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(view == null || !view.DiceTray.IsRolling, Is.True);
            Assert.That(resolutions, Is.EqualTo(beforeReturnResolutions));
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(saved.Revision));
            Assert.That(controller.Snapshot.RerollsRemaining, Is.EqualTo(saved.RerollsRemaining));
            Assert.That(controller.Snapshot.RerollsSpentOnHand, Is.EqualTo(saved.RerollsSpentOnHand));
            AssertVisibleDice(new DiceValues(saved.CurrentDice[0], saved.CurrentDice[1], saved.CurrentDice[2],
                saved.CurrentDice[3], saved.CurrentDice[4]));
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
            yield return ContentReady.Routed();
            yield return AdvanceToInvitation();
            Click("JourneyAcceptButton");
            yield return ContentReady.Routed();
            yield return WaitForPresentation();
            var overlays = new[] { "SpiritOwnedCharmsPanel", "SpiritHandRanksPanel", "MenuOverlay" };
            var openButtons = new[] { "CharmsButton", "HandRanksButton", "MenuButton" };
            var closeButtons = new[] { "CloseCharmsButton", "CloseHandRanksButton", "CloseMenuButton" };
            var titles = new[] { "CharmsTitle", "HandRanksTitle", "MenuTitle" };

            foreach (var size in new[] { new Vector2(443, 912), new Vector2(540, 960) })
            {
                for (int index = 0; index < overlays.Length; ++index)
                {
                    var portrait = view.Portrait;
                    var clues = view.GameplayHud.InvestigationButton;
                    portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                    portrait.sizeDelta = size;
                    portrait.anchoredPosition = Vector2.zero;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    Click(openButtons[index]);
                    yield return ContentReady.Routed();
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    var overlay = index == 0 ? view.OwnedCharmsPanel.Root : index == 1 ?
                        view.GetComponentInChildren<SpiritHandRanksPanel>(true).Root :
                        view.GetComponentInChildren<SpiritPauseMenu>(true).Root;
                    var title = Find<Text>(titles[index]);
                    Assert.That(overlay.gameObject.activeInHierarchy, Is.True, overlay.name);
                    Assert.That(overlay.parent, Is.SameAs(portrait), overlay.name);
                    Assert.That(clues.transform.IsChildOf(view.GameplayHud.Root), Is.True);
                    Assert.That(view.GameplayHud.Root.parent, Is.SameAs(portrait));
                    Assert.That(overlay.GetSiblingIndex(), Is.GreaterThan(view.GameplayHud.Root.GetSiblingIndex()),
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
                    yield return ContentReady.Routed();
                    yield return null;
                    Assert.That(overlay.gameObject.activeInHierarchy, Is.False, overlay.name);
                    Click("ReturnToInvestigationButton");
                    yield return ContentReady.Routed();
                    yield return null;
                    Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
                    Click("ResumeEncounterButton");
                    yield return ContentReady.Routed();
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
            yield return ContentReady.Routed();
            yield return WaitForJourney();
            Assert.That(controller.SelectedJourney, Is.SameAs(second));
            Assert.That(Find<Text>("JourneyTitle").text, Is.EqualTo("The brass stair"));
            Assert.That(Find<Text>("JourneyBody").text, Is.EqualTo("A blue filament leads above the clouds."));
            Assert.That(ActiveText(), Does.Not.Contain("Lantern"));
            yield return AdvanceToInvitation();
            Assert.That(Find<Text>("JourneyBody").text, Is.EqualTo("Set one constellation in the astrolabe."));
            Assert.That(view.TablePresentation, Is.SameAs(second.TablePresentation));
            Assert.That(view.HostPortrait, Is.Not.Null);
            Assert.That(view.HostPortrait.GetComponentsInChildren<SpiritPortraitImage>(true).Length,
                Is.EqualTo(second.TablePresentation.HostPresentation.Layers.Length), "The routed scene must display its authored host.");
            yield return CheckPortraitSizes();
            Click("JourneyAcceptButton");
            yield return ContentReady.Routed();
            yield return WaitForPresentation();
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo("encounter.journey-test.observatory"));
            Click("PlayButton");
            yield return ContentReady.Routed();
            yield return WaitForPresentation();
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(Find<Text>("EndingTitle").text, Is.EqualTo("A chart recovered"));
            Assert.That(ActiveText(), Does.Contain("The chart points to the northern platform."));
            Assert.That(Find<Button>("EndingNewRunButton").GetComponentInChildren<Text>().text,
                Is.EqualTo("Follow the chart"));
            yield return CheckPortraitSizes();
            yield return Capture("synthetic-victory");
            Click("EndingNewRunButton");
            yield return ContentReady.Routed();
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
            yield return ContentReady.Routed();
            yield return AdvanceToInvitation();
            Click("JourneyAcceptButton");
            yield return ContentReady.Routed();
            yield return WaitForPresentation();
            Click("PlayButton");
            yield return ContentReady.Routed();
            yield return WaitForPresentation();
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Lost));
            Assert.That(Find<Text>("EndingTitle").text, Is.EqualTo("The tide takes the map"));
            Assert.That(ActiveText(), Does.Contain("The archivist keeps the answer."));
            Assert.That(controller.CompletedEncounterIds, Is.Empty);
            yield return CheckPortraitSizes();
            yield return Capture("synthetic-loss");
            Click("EndingNewRunButton");
            yield return ContentReady.Routed();
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.CanResume, Is.False);
            Assert.That(Find<Button>("ResumeEncounterButton").gameObject.activeInHierarchy, Is.False);
            yield return Capture("synthetic-loss-board");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AuthoredRuleInvitationsFitPortraitSizesBeforeCreatingAnEncounter()
        {
            int resolutions = 0;
            controller.Resolved += _ => ++resolutions;
            var controls = new InvitationControls(controller.JourneyCatalog);
            foreach (var authored in controller.JourneyCatalog.Entries.Where(value => !value.IsStoryOnly))
            {
                ContentReady.SeedJourneyPrerequisites(controller, authored.Id);
                controller.SelectEncounter(authored.Id);
                yield return ContentReady.Routed(controller);
                var invitation = CloneForInvitation(authored);
                controls.Show(invitation, invitation.Beats.Count - 1);
                presentedJourney = controls;
                view.JourneyView.Bind(controls, controller);
                view.JourneyView.Refresh();
                yield return WaitForJourney();
                foreach (var size in InvitationSizes())
                {
                    yield return SetPortraitSize(size);
                    AssertRuleInvitation(invitation);
                    AssertNoEngine(resolutions);
                    AssertGameplayVisible(false);
                    yield return Capture("rules-" + authored.EncounterId + "-" + size.x + "x" + size.y);
                }
            }
            Assert.That(controller.JourneyCatalog.Entries.Count(value => !value.IsStoryOnly), Is.EqualTo(4));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RuleRowsResizeForMultipleRulesAndRestoreOrdinaryBeatsWithoutRules()
        {
            var authored = controller.JourneyCatalog.Entries[0];
            controller.SelectEncounter(authored.Id);
            yield return ContentReady.Routed(controller);
            var noRules = CloneForInvitation(authored);
            var noRuleTable = UnityEngine.Object.Instantiate(authored.TablePresentation);
            SetField(noRuleTable, "rulePresentations", Array.Empty<EncounterRulePresentationAsset>());
            SetField(noRules, "tablePresentation", noRuleTable);
            var multiple = CloneForInvitation(authored);
            var multipleTable = UnityEngine.Object.Instantiate(authored.TablePresentation);
            SetField(multipleTable, "rulePresentations", new[]
            {
                authored.TablePresentation.RulePresentations.Single(),
                controller.JourneyCatalog.Entries.Single(value => value.EncounterId == "encounter.moth.crossroads")
                    .TablePresentation.RulePresentations.Single()
            });
            SetField(multiple, "tablePresentation", multipleTable);
            var controls = new InvitationControls(controller.JourneyCatalog);
            controls.Show(multiple, multiple.Beats.Count - 1);
            presentedJourney = controls;
            view.JourneyView.Bind(controls, controller);
            view.JourneyView.Refresh();
            yield return WaitForJourney();

            foreach (var size in InvitationSizes())
            {
                controls.Show(multiple, multiple.Beats.Count - 1);
                yield return WaitForJourney();
                yield return SetPortraitSize(size);
                AssertRuleInvitation(multiple);
                yield return Capture("rules-multiple-" + size.x + "x" + size.y);
                controls.Show(noRules, noRules.Beats.Count - 1);
                yield return WaitForJourney();
                AssertOrdinaryNarrative(noRules.Beats.Last());
                Assert.That(Find<Text>("JourneyRuleName2").gameObject.activeInHierarchy, Is.False);
                yield return Capture("rules-none-" + size.x + "x" + size.y);
                controls.Show(multiple, 0);
                yield return WaitForJourney();
                AssertOrdinaryNarrative(multiple.Beats[0]);
                Assert.That(Find<Button>("JourneyAdvanceButton").gameObject.activeInHierarchy, Is.True);
                Assert.That(Find<Button>("JourneyAcceptButton").gameObject.activeInHierarchy, Is.False);
                AssertNoEngine(0);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static Vector2[] InvitationSizes() => new[]
        {
            new Vector2(443, 912), new Vector2(443, 960), new Vector2(540, 960)
        };

        private static EncounterJourneyAsset CloneForInvitation(EncounterJourneyAsset authored)
        {
            var clone = UnityEngine.Object.Instantiate(authored);
            foreach (var beat in clone.Beats) SetField(beat, "duration", 0f);
            return clone;
        }

        private IEnumerator SetPortraitSize(Vector2 size)
        {
            var portrait = ContentReady.Portrait();
            Assert.That(portrait, Is.Not.Null);
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = size;
            portrait.anchoredPosition = Vector2.zero;
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        private void AssertRuleInvitation(EncounterJourneyAsset journey)
        {
            var narrative = Find<ScrollRect>("NarrativeScroll").content;
            var body = Find<Text>("JourneyBody");
            var heading = Find<Text>("JourneyRuleHeading");
            var scope = Find<Text>("JourneyRuleScope");
            Assert.That(Find<Text>("JourneyTitle").gameObject.activeInHierarchy, Is.False,
                "The rule name supplies the invitation title.");
            Assert.That(body.text, Is.EqualTo(journey.Beats.Last().Text));
            Assert.That(body.fontStyle, Is.EqualTo(FontStyle.Normal));
            Assert.That(body.color, Is.EqualTo(body.GetComponentInParent<SpiritUiContext>().Theme.cream));
            Assert.That(body.fontSize, Is.EqualTo(23));
            Assert.That(heading.text, Is.EqualTo(journey.TablePresentation.RulePresentations.Count == 1
                ? "ENCOUNTER RULE" : "ENCOUNTER RULES"));
            Assert.That(scope.text, Is.EqualTo("Active throughout this encounter."));
            var ordered = new List<Rect> { BoundsIn(narrative, body.rectTransform), BoundsIn(narrative, heading.rectTransform) };
            for (int index = 0; index < journey.TablePresentation.RulePresentations.Count; ++index)
            {
                string suffix = index == 0 ? "" : (index + 1).ToString();
                var rule = journey.TablePresentation.RulePresentations[index];
                var name = Find<Text>("JourneyRuleName" + suffix);
                var effect = Find<Text>("JourneyRuleText" + suffix);
                var icon = Find<Graphic>("JourneyRuleIcon" + suffix);
                Assert.That(name.text, Is.EqualTo(rule.DisplayName));
                Assert.That(effect.text, Is.EqualTo(rule.Description));
                Assert.That(effect.fontSize, Is.LessThan(body.fontSize), "Narration must remain more prominent than rule details.");
                Assert.That(icon.gameObject.activeInHierarchy, Is.True, icon.name);
                Assert.That(icon.color.a, Is.GreaterThan(0), icon.name);
                var iconBounds = BoundsIn(narrative, icon.rectTransform);
                var nameBounds = BoundsIn(narrative, name.rectTransform);
                Assert.That(iconBounds.width, Is.GreaterThan(0), icon.name);
                Assert.That(iconBounds.height, Is.GreaterThan(0), icon.name);
                Assert.That(iconBounds.xMax, Is.LessThanOrEqualTo(nameBounds.xMin + 1),
                    "The rule icon must leave room for its name.");
                ordered.Add(Rect.MinMaxRect(iconBounds.xMin, Mathf.Min(iconBounds.yMin, nameBounds.yMin),
                    nameBounds.xMax, Mathf.Max(iconBounds.yMax, nameBounds.yMax)));
                ordered.Add(BoundsIn(narrative, effect.rectTransform));
            }
            ordered.Add(BoundsIn(narrative, scope.rectTransform));
            for (int index = 1; index < ordered.Count; ++index)
                Assert.That(ordered[index].yMax, Is.LessThanOrEqualTo(ordered[index - 1].yMin + 1),
                    "Narration, rule heading, names, effects, and scope must not overlap.");
            foreach (var label in narrative.GetComponentsInChildren<Text>())
            {
                if (string.IsNullOrWhiteSpace(label.text)) continue;
                AssertCompleteText(label);
                AssertInside(narrative, label.rectTransform);
            }
            for (int index = 0; index < journey.TablePresentation.RulePresentations.Count; ++index)
                AssertInside(narrative, Find<Graphic>("JourneyRuleIcon" + (index == 0 ? "" : (index + 1).ToString())).rectTransform);
            var panel = Find<RectTransform>("JourneyNarrative");
            var sequence = (RectTransform)panel.parent;
            var panelBounds = BoundsIn(sequence, panel);
            var acceptBounds = BoundsIn(sequence, Find<Button>("JourneyAcceptButton").transform as RectTransform);
            Assert.That(panelBounds.yMin - acceptBounds.yMax, Is.InRange(0, 16),
                "The measured narrative must sit immediately above Accept.");
            AssertInside(sequence, panel);
            Assert.That(Find<Button>("JourneyAcceptButton").IsInteractable(), Is.True);
        }

        private void AssertOrdinaryNarrative(EncounterJourneyBeat beat)
        {
            var title = Find<Text>("JourneyTitle");
            var body = Find<Text>("JourneyBody");
            Assert.That(title.gameObject.activeInHierarchy, Is.True);
            Assert.That(title.text, Is.EqualTo(beat.Title));
            Assert.That(body.text, Is.EqualTo(beat.Text));
            Assert.That(body.fontStyle, Is.EqualTo(FontStyle.Normal));
            foreach (string name in new[] { "JourneyRuleHeading", "JourneyRuleName", "JourneyRuleText", "JourneyRuleScope" })
                Assert.That(Find<Text>(name).gameObject.activeInHierarchy, Is.False, name);
            Assert.That(Find<Graphic>("JourneyRuleIcon").gameObject.activeInHierarchy, Is.False);
            AssertCompleteText(title);
            AssertCompleteText(body);
        }

        private static Rect BoundsIn(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector2 min = parent.InverseTransformPoint(corners[0]);
            Vector2 max = min;
            foreach (var corner in corners)
            {
                Vector2 local = parent.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void AssertInside(RectTransform parent, RectTransform child)
        {
            var bounds = BoundsIn(parent, child);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(parent.rect.xMin - 1), child.name);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(parent.rect.xMax + 1), child.name);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(parent.rect.yMin - 1), child.name);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(parent.rect.yMax + 1), child.name);
        }

        private sealed class InvitationControls : IEncounterJourneyControls
        {
            public EncounterJourneyPhase JourneyPhase => EncounterJourneyPhase.Sequence;
            public EncounterJourneyCatalogAsset JourneyCatalog { get; }
            public EncounterChapterAsset SelectedChapter => null;
            public bool IsChapterAvailable(string id) => false;
            public bool IsChapterCompleted(string id) => false;
            public void SelectChapter(string id) { }
            public EncounterJourneyAsset SelectedJourney { get; private set; }
            public int BeatIndex { get; private set; }
            public bool CanSkipIntro => false;
            public bool HasActiveEncounter => false;
            public bool CanResume => false;
            public string JourneyStatus => "";
            public IReadOnlyList<string> VisitedEncounterIds => Array.Empty<string>();
            public IReadOnlyList<string> HighlightedEncounterIds => Array.Empty<string>();
            public IReadOnlyList<string> CompletedStoryIds => Array.Empty<string>();
            public bool IsJourneyAvailable(string id) => true;
            public event Action JourneyChanged;

            public InvitationControls(EncounterJourneyCatalogAsset catalog) => JourneyCatalog = catalog;

            public void Show(EncounterJourneyAsset journey, int index)
            {
                SelectedJourney = journey;
                BeatIndex = index;
                JourneyChanged?.Invoke();
            }

            public void SelectEncounter(string encounterId) { }
            public void AdvanceJourney() { }
            public void SkipIntro() { }
            public void AcceptEncounter() { }
            public void ReturnToInvestigation() { }
            public void ResumeEncounter() { }
            public void RetryEncounter() { }
        }

        private void InstallSyntheticCatalog()
        {
            var authored = controller.JourneyCatalog.Entries.Where(entry => !entry.IsStoryOnly).ToArray();
            var tables = authored.Select(entry => entry.TablePresentation).ToArray();
            var losing = new EncounterDefinition("encounter.journey-test.archive",
                new[] { new StageDefinition(1000000, 1, 0) },
                rules: controller.Catalog.GetEncounter(authored[0].EncounterId).Rules);
            var winning = new EncounterDefinition("encounter.journey-test.observatory",
                new[] { new StageDefinition(1, 1, 0) },
                rules: controller.Catalog.GetEncounter(authored[1].EncounterId).Rules);
            var catalog = new MechanicalCatalog("journey-playmode.v1", controller.Catalog.Hands,
                new[] { losing, winning }, Array.Empty<ModifierDefinition>());
            var first = CreateJourney(tables[0], losing.Id, "The Tidal Archive", "The Archivist",
                "A watermarked map carries a name erased from the register.",
                "The flooded shelf", "Follow the waterline into the archive.",
                "Place one name in the register.", "A name restored", "The register yields a route.",
                "The tide takes the map", "The archivist keeps the answer.", "Search the archive");
            var second = CreateJourney(tables[1], winning.Id, "The Observatory", "The Astronomer",
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

        private static EncounterJourneyAsset CreateJourney(EncounterTablePresentationAsset table, string encounterId, string location, string spirit,
            string lead, string title, string approach, string invitation, string victoryTitle, string victory,
            string defeatTitle, string defeat, string continueLabel)
        {
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

        private void AssertVisibleDice(DiceValues values)
        {
            var faceNormals = new[] { Vector3.forward, Vector3.up, Vector3.right,
                Vector3.left, Vector3.down, Vector3.back };
            for (int index = 0; index < DiceValues.Count; ++index)
            {
                var slot = view.TrayScene.Geometry.DiceSlots[index];
                var die = ContentReady.Die(view.DiceTray, index);
                Assert.That(die, Is.Not.Null, "Die " + index);
                Assert.That(die.IsChildOf(slot), Is.True, "The authored die belongs to its tray slot.");
                Vector3 resultNormal = die.TransformDirection(faceNormals[values[index] - 1]);
                Assert.That(Vector3.Dot(resultNormal, slot.up), Is.GreaterThan(.999f), "Die " + index + " result face");
                var camera = view.DiceTray.RenderCamera;
                Vector3 towardCamera = camera.orthographic ? -camera.transform.forward
                    : (camera.transform.position - die.position).normalized;
                Assert.That(Vector3.Dot(resultNormal, towardCamera), Is.GreaterThan(0f), "Die " + index + " result visibility");
            }
        }

        private void AssertGameplayVisible(bool visible)
        {
            if (view == null)
            {
                Assert.That(visible, Is.False);
                Assert.That(ContentReady.PresentationRoot(controller).GetComponentsInChildren<SpiritGameplayHud>(true), Is.Empty);
                return;
            }
            foreach (string name in new[] { "PlayingPanel", "TargetPanel", "HandsPanel", "RerollsPanel" })
                Assert.That(Find<RectTransform>(name).gameObject.activeInHierarchy, Is.EqualTo(visible), name);
            if (!visible) Assert.That(view == null || !view.DiceTray.IsRolling, Is.True);
        }

        private IEnumerator AdvanceToInvitation()
        {
            yield return WaitForJourney();
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
            {
                Click("JourneyAdvanceButton");
                yield return ContentReady.Routed();
                yield return WaitForJourney();
            }
        }

        private IEnumerator WaitForJourney()
        {
            yield return ContentReady.Routed();
            view.JourneyView.Refresh();
            float deadline = Time.realtimeSinceStartup + 15;
            while (view.JourneyView.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(view.JourneyView.IsAnimating, Is.False, controller.JourneyStatus);
            yield return new WaitForSecondsRealtime(.15f);
            var state = presentedJourney ?? controller;
            if (state.JourneyPhase == EncounterJourneyPhase.Sequence)
            {
                bool invitation = state.SelectedJourney.Beats[state.BeatIndex].Kind == EncounterJourneyBeatKind.Invitation;
                var button = Find<Button>(invitation ? "JourneyAcceptButton" : "JourneyAdvanceButton");
                Assert.That(button.gameObject.activeInHierarchy, Is.True, button.name);
                Assert.That(button.IsInteractable(), Is.True, button.name);
            }
        }

        private IEnumerator WaitForPresentation()
        {
            yield return ContentReady.Routed();
            float deadline = Time.realtimeSinceStartup + 45;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False, controller.StatusMessage);
            Assert.That(view == null || !view.DiceTray.IsRolling, Is.True);
            yield return null;
        }

        private IEnumerator CheckPortraitSizes(string captureName = null)
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
                if (controller.JourneyPhase == EncounterJourneyPhase.Sequence)
                    ChapterJourneyPlayTests.AssertCharmPresentation(view,
                        controller.SelectedJourney.Beats[controller.BeatIndex], controller.Catalog);
                foreach (var label in ContentReady.PresentationRoot(controller).GetComponentsInChildren<Text>())
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

        private string ActiveText() => string.Join("\n", ContentReady.PresentationRoot(controller).GetComponentsInChildren<Text>()
            .Where(label => label.isActiveAndEnabled).Select(label => label.text));

        private T Find<T>(string name) where T : Component
        {
            foreach (string prefix in new[] { "JourneyRuleName", "JourneyRuleText", "JourneyRuleIcon" })
            {
                if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                int index = name.Length == prefix.Length ? 0 : int.Parse(name.Substring(prefix.Length)) - 1;
                var rows = view.JourneyView.GetComponentsInChildren<SpiritInvitationRuleView>(true);
                Assert.That(index, Is.LessThan(rows.Length), name);
                Component match = prefix == "JourneyRuleIcon" ?
                    rows[index].IconRoot.GetComponentsInChildren<Graphic>().First(graphic => !(graphic is Text)) :
                    prefix == "JourneyRuleName" ? rows[index].Title : rows[index].Description;
                return match as T;
            }
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
