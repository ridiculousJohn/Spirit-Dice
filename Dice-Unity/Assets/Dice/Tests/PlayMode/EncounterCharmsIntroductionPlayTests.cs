using System;
using System.Collections;
using System.Collections.Generic;
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
    /**
     * The introduction of an encounter's own charm families inside the encounter, for the first authored journey whose
     * encounter first offers them after a later stage clear: over the first charm offer that can contain them and after any
     * permanent unlock choice. Stages use a target of 1 so each played hand clears; earlier stages are reached through
     * isolated saves rather than played hands.
     */
    public sealed class EncounterCharmsIntroductionPlayTests
    {
        private SpiritEncounterController controller;
        private SpiritEncounterView view => ContentReady.EncounterView(controller);

        [UnitySetUp]
        public IEnumerator InvestigationReady()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            SetField(controller, "useFixedSeed", true);
            SetField(controller, "initialSeed", "60");
            yield return null;
        }

        [UnityTest]
        public IEnumerator LaterStageAdditionsAreIntroducedOverTheirFirstEligibleOfferUntilContinue()
        {
            var authored = controller.Catalog;
            var journeys = Journeys(controller, encounter => encounter.EncounterModifierIds.Count > 0 &&
                encounter.EncounterModifiersFirstClearedStageNumber > 1);
            Assert.That(journeys, Is.Not.Empty,
                "No authored journey first offers its encounter additions after a later stage clear; nothing is introduced inside an encounter.");
            yield return IntroducesInsideTheEncounter(authored, journeys[0]);
            LogAssert.NoUnexpectedReceived();
        }

        /** Non-story journeys of the authored catalog whose encounters match. */
        private static EncounterJourneyAsset[] Journeys(SpiritEncounterController controller, Func<EncounterDefinition, bool> match)
            => controller.JourneyCatalog.Entries
                .Where(journey => !journey.IsStoryOnly && match(controller.Catalog.GetEncounter(journey.EncounterId))).ToArray();

        /**
         * Plays the journey's shortened encounter to the first charm offer that can contain its additions: earlier offers
         * and the playing table, shown as resumed runs, are not introduced; any unlock choice comes first; the offer is
         * covered until Continue, stays dismissed for a refreshed offer, and is introduced again after a load and a resume.
         */
        private IEnumerator IntroducesInsideTheEncounter(MechanicalCatalog authored, EncounterJourneyAsset journey)
        {
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            yield return EnterShortEncounter(controller, authored, journey);
            var encounter = controller.Catalog.GetEncounter(journey.EncounterId);
            int introducedStage = encounter.EncounterModifiersFirstClearedStageNumber;
            var session = EncounterSession.CreateEncounter(controller.Catalog, journey.EncounterId, 60);

            // Earlier charm offers, shown as a resumed run, are not introduced.
            while (session.Snapshot.StageIndex < introducedStage - 1)
            {
                Execute(session, PlayHandAction.Instance);
                while (session.Snapshot.Phase != EncounterPhase.Playing)
                {
                    if (session.Snapshot.Phase == EncounterPhase.ChoosingModifier)
                    {
                        int target = session.Snapshot.PendingReward.TargetStageIndex;
                        Assert.That(target, Is.LessThan(introducedStage), journey.Id);
                        yield return LoadSession(controller, session);
                        Assert.That(view.RewardsPanel.Root.gameObject.activeInHierarchy, Is.True, journey.Id);
                        Assert.That(view.EncounterCharmsIntroduction.IsOpen, Is.False, journey.Id + ": stage " + target + " offer");
                    }
                    ResolveFirst(session);
                }
            }
            Assert.That(session.Snapshot.StageIndex, Is.EqualTo(introducedStage - 1), journey.Id);
            yield return LoadSession(controller, session);
            Assert.That(view.EncounterCharmsIntroduction.IsOpen, Is.False, journey.Id + ": playing");

            // Clearing the stage offers any permanent unlock first, without the introduction.
            controller.PlayHand();
            yield return Idle(controller);
            while (controller.Snapshot.Phase == EncounterPhase.ChoosingUnlock)
            {
                Assert.That(view.EncounterCharmsIntroduction.IsOpen, Is.False, journey.Id + ": unlock choice");
                yield return ChooseFirst(controller, view);
            }

            // The first charm offer that can contain the additions is covered by the introduction.
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingModifier), journey.Id);
            var introducedOffer = controller.Snapshot.PendingReward;
            Assert.That(introducedOffer.TargetStageIndex, Is.EqualTo(introducedStage), journey.Id);
            AssertIntroductionOpen(controller, view, journey);
            yield return AssertOfferCovered(view, true);

            yield return Click(view.EncounterCharmsIntroduction.Screen.BeginButton);
            Assert.That(view.EncounterCharmsIntroduction.IsOpen, Is.False, journey.Id + ": Continue dismisses the introduction.");
            Assert.That(controller.Snapshot.PendingReward.OfferId, Is.EqualTo(introducedOffer.OfferId));
            Assert.That(view.RewardsPanel.Root.gameObject.activeInHierarchy, Is.True);
            yield return AssertOfferCovered(view, false);

            // A refreshed offer keeps its target stage and is not introduced again. Refreshing is available only when the
            // encounter's reward policy grants refreshes.
            if (view.RefreshButton.IsInteractable())
            {
                yield return Click(view.RefreshButton);
                yield return Idle(controller);
                Assert.That(controller.Snapshot.PendingReward.OfferId, Is.Not.EqualTo(introducedOffer.OfferId));
                Assert.That(controller.Snapshot.PendingReward.TargetStageIndex, Is.EqualTo(introducedStage));
                Assert.That(view.EncounterCharmsIntroduction.IsOpen, Is.False, journey.Id + ": refresh does not show it again.");
                yield return AssertOfferCovered(view, false);
            }

            // Loading the run while the offer is pending shows it again.
            Assert.That(controller.TrySave(), Is.True, controller.Status.ToString());
            Assert.That(controller.TryLoad(), Is.True, controller.Status.ToString());
            yield return Idle(controller);
            AssertIntroductionOpen(controller, view, journey);
            yield return Click(view.EncounterCharmsIntroduction.Screen.BeginButton);
            Assert.That(view.EncounterCharmsIntroduction.IsOpen, Is.False);

            // Returning to the table from the investigation board resumes the run and shows it again.
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            yield return null;
            Assert.That(controller.CanResume, Is.True);
            controller.ResumeEncounter();
            yield return Idle(controller);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            AssertIntroductionOpen(controller, view, journey);
            yield return Click(view.EncounterCharmsIntroduction.Screen.BeginButton);
            Assert.That(view.EncounterCharmsIntroduction.IsOpen, Is.False);

            // Taking a charm moves on without the introduction.
            yield return ChooseFirst(controller, view);
            Assert.That(controller.Snapshot.Phase, Is.Not.EqualTo(EncounterPhase.ChoosingModifier), journey.Id);
            Assert.That(view.EncounterCharmsIntroduction.IsOpen, Is.False, journey.Id);
        }

        /** Takes the first option of the session's pending unlock, charm or hand upgrade choice. */
        private static void ResolveFirst(EncounterSession session)
        {
            var offer = session.Snapshot.PendingReward;
            switch (session.Snapshot.Phase)
            {
                case EncounterPhase.ChoosingUnlock:
                    Execute(session, offer.Options.Count == 0 ? (EncounterAction)new AcknowledgeModifierUnlockAction(offer.OfferId)
                        : new SelectModifierUnlockAction(offer.OfferId, offer.Options[0].OptionId));
                    break;
                case EncounterPhase.ChoosingModifier:
                    Execute(session, new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId));
                    break;
                case EncounterPhase.ChoosingHandUpgrade:
                    Execute(session, new SelectHandUpgradeAction(offer.OfferId, offer.Options[0].OptionId));
                    break;
                default:
                    Assert.Fail("Unexpected phase " + session.Snapshot.Phase + " before the introduced offer.");
                    break;
            }
        }

        /**
         * Installs the journey's encounter with every stage target at 1 (otherwise as authored) and enters it through its
         * journey; Accept starts it at once because its additions are introduced inside the encounter.
         */
        internal static IEnumerator EnterShortEncounter(SpiritEncounterController controller, MechanicalCatalog authored,
            EncounterJourneyAsset journey)
        {
            var definition = authored.GetEncounter(journey.EncounterId);
            var shortEncounter = new EncounterDefinition(definition.Id,
                definition.Stages.Select(stage => new StageDefinition(1, stage.StartingHands, stage.StartingRerolls, stage.ExitBehavior)),
                definition.StartingUpgrades, definition.StartingModifiers, definition.RewardPolicy, definition.Rules,
                definition.SpiritId, definition.UnlockMilestones, definition.EncounterModifierIds,
                definition.UnlockRewardModifierIds, definition.Pieces, definition.EncounterModifiersFirstClearedStageNumber);
            var mechanical = new MechanicalCatalog("charms-introduction-playmode.v1", authored.Hands,
                authored.Encounters.Select(value => value.Id == definition.Id ? shortEncounter : value),
                authored.Modifiers, authored.RewardPools);
            Assert.That(controller.InitializeJourney(mechanical, controller.JourneyCatalog, controller.SavePath), Is.True,
                controller.JourneyStatus.ToString());
            ContentReady.SeedJourneyPrerequisites(controller, journey.Id);
            yield return SelectToInvitation(controller, journey.EncounterId);
            var view = ContentReady.EncounterView(controller);
            view.JourneyView.GetComponentsInChildren<Button>(true).Single(button => button.name == "JourneyAcceptButton")
                .onClick.Invoke();
            Assert.That(view.JourneyView.EncounterCharms.IsOpen, Is.False, "The pre-encounter screen is skipped.");
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            yield return Idle(controller);
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo(journey.EncounterId));
        }

        /**
         * Plays the shortened encounter to the stage clear that first offers its additions and takes any permanent unlock,
         * leaving that charm offer pending.
         */
        internal static IEnumerator ReachIntroducedOffer(SpiritEncounterController controller, string encounterId)
        {
            int introducedStage = controller.Catalog.GetEncounter(encounterId).EncounterModifiersFirstClearedStageNumber;
            var session = EncounterSession.CreateEncounter(controller.Catalog, encounterId, 60);
            while (session.Snapshot.StageIndex < introducedStage - 1)
            {
                Execute(session, PlayHandAction.Instance);
                while (session.Snapshot.Phase != EncounterPhase.Playing) ResolveFirst(session);
            }
            yield return LoadSession(controller, session);
            controller.PlayHand();
            yield return Idle(controller);
            while (controller.Snapshot.Phase == EncounterPhase.ChoosingUnlock)
                yield return ChooseFirst(controller, ContentReady.EncounterView(controller));
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingModifier));
            Assert.That(controller.Snapshot.PendingReward.TargetStageIndex, Is.EqualTo(introducedStage));
        }

        /** The introduction is open with the journey's narration, if any, the encounter's families, and no Back button. */
        internal static void AssertIntroductionOpen(SpiritEncounterController controller, SpiritEncounterView view,
            EncounterJourneyAsset journey)
        {
            var introduction = view.EncounterCharmsIntroduction;
            Assert.That(introduction.IsOpen, Is.True, journey.Id + ": the charm offer is introduced.");
            var screen = introduction.Screen;
            Assert.That(screen.gameObject.activeInHierarchy, Is.True);
            string line = journey.EncounterCharmsIntroductionText;
            bool narrated = !string.IsNullOrWhiteSpace(line);
            Assert.That(screen.Narration.gameObject.activeInHierarchy, Is.EqualTo(narrated), journey.Id + ": narration shown");
            if (narrated) Assert.That(screen.Narration.text, Does.Contain(line.Trim()), journey.Id);
            Assert.That(screen.BackButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(screen.BeginButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(screen.BeginButton.IsInteractable(), Is.True, "Continue is available immediately.");
            var encounter = controller.Catalog.GetEncounter(journey.EncounterId);
            var families = encounter.EncounterModifierIds.Select(controller.Catalog.GetModifier)
                .GroupBy(definition => definition.FamilyId).Select(group => group.First()).ToArray();
            var rows = screen.Rows.Where(row => row.gameObject.activeSelf).ToArray();
            Assert.That(rows.Select(row => row.FamilyId), Is.EquivalentTo(families.Select(family => family.FamilyId)), journey.Id);
            foreach (var family in families)
                Assert.That(rows.Single(row => row.FamilyId == family.FamilyId).GetComponentsInChildren<Text>().Select(text => text.text),
                    Does.Contain(ContentReady.Charms.GetFamilyName(family)), journey.Id);
        }

        /** The narration, family row, scope sentence and Continue are complete and stacked without overlap. */
        internal static void AssertIntroductionLayout(SpiritEncounterView view)
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

        /** Raycasts at the first offer card: covered, the topmost hit belongs to the introduction; uncovered, it is the card. */
        internal static IEnumerator AssertOfferCovered(SpiritEncounterView view, bool covered)
        {
            yield return null;
            yield return null;
            var card = view.RewardsPanel.Cards.First(value => value.Button.gameObject.activeInHierarchy).Button;
            var handler = TopClickHandler(card, out _, out var hit);
            if (covered)
            {
                Assert.That(hit.gameObject, Is.Not.Null);
                Assert.That(hit.gameObject.transform.IsChildOf(view.EncounterCharmsIntroduction.Screen.Root), Is.True,
                    "The introduction takes input over the offer: " + hit.gameObject.name);
                Assert.That(handler, Is.Not.SameAs(card.gameObject));
            }
            else Assert.That(handler, Is.SameAs(card.gameObject), "The offer is interactable.");
        }

        internal static IEnumerator SizePortrait(int width = 540)
        {
            var portrait = ContentReady.Portrait();
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(width, 960);
            portrait.anchoredPosition = Vector2.zero;
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        private static IEnumerator ChooseFirst(SpiritEncounterController controller, SpiritEncounterView view)
        {
            var panel = view.RewardsPanel;
            var card = panel.Cards.FirstOrDefault(value => value.Button.gameObject.activeInHierarchy);
            if (card != null) card.Button.onClick.Invoke();
            Assert.That(panel.ConfirmButton.IsInteractable(), Is.True);
            panel.ConfirmButton.onClick.Invoke();
            yield return Idle(controller);
        }

        private static IEnumerator LoadSession(SpiritEncounterController controller, EncounterSession session)
        {
            EncounterSaveFileStore.Save(controller.SavePath, session.CaptureSave());
            Assert.That(controller.TryLoad(), Is.True, controller.Status.ToString());
            yield return Idle(controller);
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(session.Snapshot.Revision));
        }

        private static IEnumerator SelectToInvitation(SpiritEncounterController controller, string encounterId)
        {
            controller.SelectEncounter(encounterId);
            yield return ContentReady.Routed(controller);
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                controller.AdvanceJourney();
            yield return ContentReady.Routed(controller);
            yield return null;
        }

        private static IEnumerator Idle(SpiritEncounterController controller)
        {
            yield return ContentReady.Routed(controller);
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False, controller.Status.ToString());
            yield return null;
        }

        private static IEnumerator Click(Button button)
        {
            Assert.That(button.gameObject.activeInHierarchy, Is.True, button.name);
            Assert.That(button.IsInteractable(), Is.True, button.name);
            // Newly shown graphics are skipped by GraphicRaycaster until the canvas assigns their depth when rendering.
            float deadline = Time.realtimeSinceStartup + 1;
            while (TopClickHandler(button, out _, out _) != button.gameObject)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "A graphic blocks " + button.name);
                yield return null;
            }
            var handler = TopClickHandler(button, out var pointer, out var hit);
            pointer.pointerCurrentRaycast = hit;
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static GameObject TopClickHandler(Button button, out PointerEventData pointer, out RaycastResult hit)
        {
            Canvas.ForceUpdateCanvases();
            Assert.That(EventSystem.current, Is.Not.Null);
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

        private static void Execute(EncounterSession session, EncounterAction action)
        {
            var result = session.Execute(action, session.Snapshot.Revision);
            Assert.That(result.Accepted, Is.True, result.Rejection.ToString());
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }
    }
}
