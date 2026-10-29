using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class EncounterJourneyPlayTests
    {
        private SpiritEncounterController controller;
        private SpiritEncounterView view => ContentReady.EncounterView(controller);
        private int resolutions;
        private int rolls;

        [UnitySetUp]
        public IEnumerator InvestigationReady()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            ContentReady.UseFixedSeed(controller);
            resolutions = 0;
            rolls = 0;
            controller.Resolved += resolution =>
            {
                ++resolutions;
                rolls += resolution.Events.OfType<DiceRolledEvent>().Count();
            };
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClueBeatsArrivalAndInvitationPresentWithoutAnEngineUntilAcceptStartsOneOpeningRoll()
        {
            var journey = BoardJourney("has an arrival beat",
                value => value.Beats.Any(beat => beat.Kind == EncounterJourneyBeatKind.Arrival));
            AssertNoEngine();
            Assert.That(ActiveText(), Does.Contain(journey.LocationTitle));
            Assert.That(ActiveText(), Does.Contain(journey.LeadText));
            AssertGameplayVisible(false);
            yield return ClickClue(journey);

            for (int index = 0; index < journey.Beats.Count; ++index)
            {
                yield return WaitForJourney();
                Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
                Assert.That(controller.BeatIndex, Is.EqualTo(index));
                Assert.That(view.JourneyView.VisibleBeatIndex, Is.EqualTo(index));
                var beat = journey.Beats[index];
                Assert.That(Find<Text>("JourneyTitle").text, Is.EqualTo(beat.Title));
                Assert.That(controller.CanSkipIntro, Is.False);
                Assert.That(Find<Button>("JourneySkipButton").gameObject.activeInHierarchy, Is.False);
                AssertNoEngine();
                AssertGameplayVisible(false);
                if (beat.Kind == EncounterJourneyBeatKind.Arrival)
                {
                    Assert.That(view.HostPortrait, Is.Not.Null);
                    Assert.That(view.HostPortrait.GetComponentsInChildren<RawImage>()
                        .Any(image => image.gameObject.activeInHierarchy), Is.True,
                        "The table host must arrive before the gameplay HUD or engine dice.");
                }
                if (beat.Kind != EncounterJourneyBeatKind.Invitation) Click("JourneyAdvanceButton");
            }

            // The invitation presents every rule of the journey's table, in order.
            var rows = view.JourneyView.GetComponentsInChildren<SpiritInvitationRuleView>(true);
            Assert.That(rows.Length, Is.GreaterThanOrEqualTo(journey.TablePresentation.RulePresentations.Count));
            for (int index = 0; index < journey.TablePresentation.RulePresentations.Count; ++index)
            {
                var rule = journey.TablePresentation.RulePresentations[index];
                Assert.That(rows[index].Title.text, Is.EqualTo(rule.DisplayName));
                Assert.That(rows[index].Description.text, Does.Contain(rule.Description));
            }
            Click("JourneyAcceptButton");
            yield return ContentReady.Routed();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo(journey.Encounter.Identity));
            Assert.That(controller.Snapshot.Revision, Is.Zero);
            Assert.That(controller.IsBusy, Is.True);
            Assert.That(resolutions, Is.EqualTo(1));
            var opening = controller.Snapshot;
            controller.AcceptEncounter();
            yield return ContentReady.Routed(controller);
            controller.AdvanceJourney();
            controller.SkipIntro();
            Assert.That(controller.Snapshot, Is.SameAs(opening));
            Assert.That(resolutions, Is.EqualTo(1));
            AssertGameplayVisible(true);
            yield return WaitForPresentation();
            controller.AcceptEncounter();
            yield return ContentReady.Routed(controller);
            Assert.That(controller.Snapshot, Is.SameAs(opening));
            Assert.That(resolutions, Is.EqualTo(1), "Repeated acceptance must not roll or create another run.");
            Assert.That(rolls, Is.EqualTo(1), "Accepting must roll the opening dice once.");
            var expected = EncounterSession.CreateEncounter(controller.Catalog, journey.Encounter.Identity, ContentReady.Seed);
            for (int index = 0; index < DiceValues.Count; ++index)
            {
                Assert.That(controller.Snapshot.CurrentDice[index], Is.EqualTo(expected.Snapshot.CurrentDice[index]));
            }
            AssertVisibleDice(expected.Snapshot.CurrentDice);
            Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).VisitedEncounterIds,
                Does.Contain(journey.Encounter.Identity));
            LogAssert.NoUnexpectedReceived();
        }

        /**
         * Plays a hand that clears the first stage of a short copy of an encounter (every stage target 1), takes its
         * rewards through the rewards panel, saves from the pause menu, then returns to the board, resumes, and loads
         * the run: each keeps the run and its rewards without replaying the intro or the opening roll.
         */
        [UnityTest]
        public IEnumerator APlayedHandWithRewardsIsRetainedByReturnResumeAndLoadWithoutReplayingTheIntroOrOpeningRoll()
        {
            var journey = BoardJourney("awaits rewards after its first stage", value =>
            {
                var stages = controller.Catalog.GetEncounter(value.Encounter.Identity).Stages;
                return stages.Count > 1 && stages[0].ExitBehavior == StageExitBehavior.AwaitRewards;
            });
            Assert.That(controller.InitializeJourney(ContentReady.ShortStages(controller.Catalog, journey.Encounter.Identity, "journey-playmode.v1"),
                controller.JourneyCatalog, controller.SavePath), Is.True, controller.JourneyStatus.ToString());
            yield return ContentReady.Routed(controller);
            yield return null;
            yield return ClickClue(journey);
            yield return AdvanceToInvitation();
            Click("JourneyAcceptButton");
            yield return ContentReady.Routed();
            yield return WaitForPresentation();

            Click("PlayButton");
            yield return ContentReady.Routed();
            yield return WaitForPresentation();
            Assert.That(controller.Snapshot.Phase, Is.Not.EqualTo(EncounterPhase.Playing),
                "Clearing the first stage must offer its rewards.");
            while (controller.Snapshot.Phase != EncounterPhase.Playing)
            {
                Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.None));
                yield return ChooseFirstReward();
            }
            Assert.That(controller.Snapshot.StageIndex, Is.EqualTo(1), "The rewards lead to the next stage.");
            int rewardedResolutions = resolutions;

            Click("MenuButton");
            yield return ContentReady.Routed();
            yield return null;
            Click("SaveRunButton");
            yield return ContentReady.Routed();
            Assert.That(File.Exists(controller.SavePath), Is.True, controller.Status.ToString());
            var saved = EncounterSaveFileStore.Load(controller.SavePath, controller.Catalog);
            Assert.That(saved.StageIndex, Is.EqualTo(1));
            var beforeReturn = controller.Snapshot;
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
            Assert.That(resolutions, Is.EqualTo(rewardedResolutions));
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
            Assert.That(resolutions, Is.EqualTo(rewardedResolutions));
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(saved.Revision));
            Assert.That(controller.Snapshot.StageIndex, Is.EqualTo(saved.StageIndex));
            Assert.That(controller.Snapshot.RerollsRemaining, Is.EqualTo(saved.RerollsRemaining));
            AssertVisibleDice(new DiceValues(saved.CurrentDice[0], saved.CurrentDice[1], saved.CurrentDice[2],
                saved.CurrentDice[3], saved.CurrentDice[4]));
            controller.Save();
            Assert.That(EncounterSaveCodec.Serialize(EncounterSaveFileStore.Load(controller.SavePath, controller.Catalog)),
                Is.EqualTo(EncounterSaveCodec.Serialize(saved)),
                "Resume/load must preserve the rewards and both random stream states.");
            LogAssert.NoUnexpectedReceived();
        }

        /**
         * The first encounter journey the investigation board offers that Accept starts at once (its encounter introduces
         * no additions on a pre-encounter screen) and that meets the requirement.
         */
        private EncounterJourneyAsset BoardJourney(string requirement, Func<EncounterJourneyAsset, bool> require)
        {
            var candidates = BoardEntries().Where(journey => !journey.IsStoryOnly && require(journey))
                .Where(journey =>
                {
                    var encounter = controller.Catalog.GetEncounter(journey.Encounter.Identity);
                    return encounter.EncounterModifierIds.Count == 0 || encounter.EncounterModifiersFirstClearedStageNumber != 1;
                }).ToArray();
            Assert.That(candidates, Is.Not.Empty,
                "The investigation board offers no encounter journey that Accept starts at once and that " + requirement + ".");
            return candidates[0];
        }

        /** The journeys the investigation board shows as clue cards, in card order. */
        private EncounterJourneyAsset[] BoardEntries()
        {
            IEnumerable<EncounterJourneyAsset> entries = controller.SelectedChapter != null ? controller.SelectedChapter.Entries
                : controller.JourneyCatalog.Chapters != null && controller.JourneyCatalog.Chapters.Count > 0
                    ? Array.Empty<EncounterJourneyAsset>() : controller.JourneyCatalog.Entries;
            return entries.Where(journey => journey != null && controller.IsJourneyAvailable(journey)).ToArray();
        }

        /**
         * Takes the first card of the pending reward through the rewards panel and confirms it; an introduction of the
         * encounter's own charms over the offer is continued first.
         */
        private IEnumerator ChooseFirstReward()
        {
            if (view.EncounterCharmsIntroduction.IsOpen)
            {
                view.EncounterCharmsIntroduction.Screen.BeginButton.onClick.Invoke();
                yield return ContentReady.Routed(controller);
            }
            var panel = view.RewardsPanel;
            Assert.That(panel.Root.gameObject.activeInHierarchy, Is.True, "The pending reward is shown.");
            var card = panel.Cards.FirstOrDefault(value => value.Button.gameObject.activeInHierarchy);
            if (card != null) card.Button.onClick.Invoke();
            Assert.That(panel.ConfirmButton.IsInteractable(), Is.True);
            long revision = controller.Snapshot.Revision;
            panel.ConfirmButton.onClick.Invoke();
            yield return WaitForPresentation();
            Assert.That(controller.Snapshot.Revision, Is.GreaterThan(revision), "Confirming takes the reward.");
        }

        /** Scrolls the journey's clue card into view, clicks it, and checks that it opens that journey. */
        private IEnumerator ClickClue(EncounterJourneyAsset journey)
        {
            int index = Array.IndexOf(BoardEntries(), journey);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), journey.name + " is not on the investigation board.");
            string name = "ClueButton" + index;
            var scroll = Find<ScrollRect>("InvestigationClues");
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.content, Find<Button>(name).transform);
            scroll.StopMovement();
            scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x, Mathf.Max(0, -bounds.max.y));
            yield return null;
            Canvas.ForceUpdateCanvases();
            Click(name);
            yield return ContentReady.Routed();
            Assert.That(controller.SelectedJourney, Is.SameAs(journey), name + " opens " + journey.name);
        }

        private void AssertNoEngine()
        {
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.Preview, Is.Null);
            Assert.That(controller.HasActiveEncounter, Is.False);
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(resolutions, Is.Zero);
            Assert.That(controller.Application, Is.Null, "The narrative must not create an engine or consume its random streams.");
            Assert.That(File.Exists(controller.SavePath), Is.False);
        }

        private void AssertVisibleDice(DiceValues values)
        {
            var faceNormals = new[] { Vector3.forward, Vector3.up, Vector3.right,
                Vector3.left, Vector3.down, Vector3.back };
            for (int index = 0; index < DiceValues.Count; ++index)
            {
                var slot = view.TrayScene.Geometry.DiceSlots[index];
                var die = view.DiceTray.Dice[index];
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
            yield return ContentReady.WaitUntil(() => !view.JourneyView.IsAnimating, controller.JourneyStatus.ToString());
            if (controller.JourneyPhase == EncounterJourneyPhase.Sequence)
            {
                bool invitation = controller.SelectedJourney.Beats[controller.BeatIndex].Kind == EncounterJourneyBeatKind.Invitation;
                var button = Find<Button>(invitation ? "JourneyAcceptButton" : "JourneyAdvanceButton");
                yield return ContentReady.WaitUntil(() => button.gameObject.activeInHierarchy && button.IsInteractable(),
                    button.name + " becomes available once the beat has shown.");
            }
        }

        private IEnumerator WaitForPresentation()
        {
            yield return ContentReady.Routed();
            yield return ContentReady.WaitUntil(() => !controller.IsBusy, controller.Status.ToString(), ContentReady.SequenceTimeout);
            Assert.That(view == null || !view.DiceTray.IsRolling, Is.True);
            yield return null;
        }

        private string ActiveText() => string.Join("\n", ContentReady.PresentationRoot(controller).GetComponentsInChildren<Text>()
            .Where(label => label.isActiveAndEnabled).Select(label => label.text));

        private T Find<T>(string name) where T : Component => ContentReady.Find<T>(controller, name);

        private void Click(string name) => ContentReady.Click(Find<Button>(name));
    }
}
