using System;
using System.Collections;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
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
            yield return ContentReady.LoadApplication();
            controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            ContentReady.UseFixedSeed(controller);
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
                .Where(journey => journey.Encounters.Any() && match(controller.Catalog.GetEncounter(journey.Encounters.First().Identity))).ToArray();

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
            int introducedStage = controller.Catalog.GetEncounter(journey.Encounters.First().Identity).EncounterModifiersFirstClearedStageNumber;
            yield return ReachIntroducedOffer(controller, journey);

            // The first charm offer that can contain the additions is covered by the introduction.
            var introducedOffer = controller.Snapshot.PendingReward;
            AssertIntroductionOpen(controller, view, journey);
            yield return AssertOfferCovered(view, true);

            yield return ContentReady.ClickWhenShown(view.EncounterCharmsIntroduction.Screen.BeginButton);
            Assert.That(view.EncounterCharmsIntroduction.IsOpen, Is.False, journey.name + ": Continue dismisses the introduction.");
            Assert.That(controller.Snapshot.PendingReward.OfferId, Is.EqualTo(introducedOffer.OfferId));
            Assert.That(view.RewardsPanel.Root.gameObject.activeInHierarchy, Is.True);
            yield return AssertOfferCovered(view, false);

            // A refreshed offer keeps its target stage and is not introduced again. Refreshing is available only when the
            // encounter's reward policy grants refreshes.
            if (view.RewardsPanel.RefreshButton.IsInteractable())
            {
                yield return ContentReady.ClickWhenShown(view.RewardsPanel.RefreshButton);
                yield return ContentReady.Idle(controller);
                Assert.That(controller.Snapshot.PendingReward.OfferId, Is.Not.EqualTo(introducedOffer.OfferId));
                Assert.That(controller.Snapshot.PendingReward.TargetStageIndex, Is.EqualTo(introducedStage));
                Assert.That(view.EncounterCharmsIntroduction.IsOpen, Is.False, journey.name + ": refresh does not show it again.");
                yield return AssertOfferCovered(view, false);
            }

            // Loading the run while the offer is pending shows it again.
            Assert.That(controller.TrySave(), Is.True, controller.Status.ToString());
            Assert.That(controller.TryLoad(), Is.True, controller.Status.ToString());
            yield return ContentReady.Idle(controller);
            AssertIntroductionOpen(controller, view, journey);
            yield return ContentReady.ClickWhenShown(view.EncounterCharmsIntroduction.Screen.BeginButton);
            Assert.That(view.EncounterCharmsIntroduction.IsOpen, Is.False);

            // Returning to the table from the investigation board resumes the run and shows it again.
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            yield return null;
            Assert.That(controller.CanResume, Is.True);
            controller.ResumeEncounter();
            yield return ContentReady.Idle(controller);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            AssertIntroductionOpen(controller, view, journey);
            yield return ContentReady.ClickWhenShown(view.EncounterCharmsIntroduction.Screen.BeginButton);
            Assert.That(view.EncounterCharmsIntroduction.IsOpen, Is.False);

            // Taking a charm moves on without the introduction.
            yield return ChooseFirst(controller, view);
            Assert.That(controller.Snapshot.Phase, Is.Not.EqualTo(EncounterPhase.ChoosingModifier), journey.name);
            Assert.That(view.EncounterCharmsIntroduction.IsOpen, Is.False, journey.name);
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
            var mechanical = ContentReady.ShortStages(authored, journey.Encounters.First().Identity, "charms-introduction-playmode.v1");
            Assert.That(controller.InitializeJourney(mechanical, controller.JourneyCatalog, controller.SavePath), Is.True,
                controller.JourneyStatus.ToString());
            ContentReady.SeedJourneyPrerequisites(controller, journey);
            yield return SelectToInvitation(controller, journey);
            var view = ContentReady.EncounterView(controller);
            view.JourneyView.AcceptButton.onClick.Invoke();
            Assert.That(view.JourneyView.EncounterCharms.IsOpen, Is.False, "The pre-encounter screen is skipped.");
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            yield return ContentReady.Idle(controller);
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo(journey.Encounters.First().Identity));
        }

        /**
         * Plays the shortened encounter to the stage clear that first offers its additions and takes any permanent unlock,
         * leaving that charm offer pending. Earlier charm offers and the playing table, shown as resumed runs, and any
         * unlock choice are not introduced.
         */
        internal static IEnumerator ReachIntroducedOffer(SpiritEncounterController controller, EncounterJourneyAsset journey)
        {
            SpiritEncounterView View() => ContentReady.EncounterView(controller);
            var encounterId = journey.Encounters.First().Identity;
            string context = journey.name;
            int introducedStage = controller.Catalog.GetEncounter(encounterId).EncounterModifiersFirstClearedStageNumber;
            var session = EncounterSession.CreateEncounter(controller.Catalog, encounterId, ContentReady.Seed);
            while (session.Snapshot.StageIndex < introducedStage - 1)
            {
                Execute(session, PlayHandAction.Instance);
                while (session.Snapshot.Phase != EncounterPhase.Playing)
                {
                    if (session.Snapshot.Phase == EncounterPhase.ChoosingModifier)
                    {
                        int target = session.Snapshot.PendingReward.TargetStageIndex;
                        Assert.That(target, Is.LessThan(introducedStage), context);
                        yield return LoadSession(controller, session);
                        Assert.That(View().RewardsPanel.Root.gameObject.activeInHierarchy, Is.True, context);
                        Assert.That(View().EncounterCharmsIntroduction.IsOpen, Is.False, context + ": stage " + target + " offer");
                    }
                    ResolveFirst(session);
                }
            }
            Assert.That(session.Snapshot.StageIndex, Is.EqualTo(introducedStage - 1), context);
            yield return LoadSession(controller, session);
            Assert.That(View().EncounterCharmsIntroduction.IsOpen, Is.False, context + ": playing");

            // Clearing the stage offers any permanent unlock first, without the introduction.
            controller.PlayHand();
            yield return ContentReady.Idle(controller);
            while (controller.Snapshot.Phase == EncounterPhase.ChoosingUnlock)
            {
                Assert.That(View().EncounterCharmsIntroduction.IsOpen, Is.False, context + ": unlock choice");
                yield return ChooseFirst(controller, View());
            }
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingModifier), context);
            Assert.That(controller.Snapshot.PendingReward.TargetStageIndex, Is.EqualTo(introducedStage), context);
        }

        /** The introduction is open with the journey's narration, if any, the encounter's families, and no Back button. */
        internal static void AssertIntroductionOpen(SpiritEncounterController controller, SpiritEncounterView view,
            EncounterJourneyAsset journey)
        {
            var introduction = view.EncounterCharmsIntroduction;
            Assert.That(introduction.IsOpen, Is.True, journey.name + ": the charm offer is introduced.");
            var screen = introduction.Screen;
            Assert.That(screen.gameObject.activeInHierarchy, Is.True);
            string line = journey.EncounterBeatOf(journey.Encounters.First().Identity).EncounterCharmsIntroductionText;
            bool narrated = !string.IsNullOrWhiteSpace(line);
            Assert.That(screen.Narration.gameObject.activeInHierarchy, Is.EqualTo(narrated), journey.name + ": narration shown");
            if (narrated) Assert.That(screen.Narration.text, Does.Contain(line.Trim()), journey.name);
            Assert.That(screen.BackButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(screen.BeginButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(screen.BeginButton.IsInteractable(), Is.True, "Continue is available immediately.");
            var encounter = controller.Catalog.GetEncounter(journey.Encounters.First().Identity);
            var families = encounter.EncounterModifierIds.Select(controller.Catalog.GetModifier)
                .GroupBy(definition => definition.Family).Select(group => group.First()).ToArray();
            var rows = screen.Rows.Where(row => row.gameObject.activeSelf).ToArray();
            Assert.That(rows.Select(row => row.Family), Is.EquivalentTo(families.Select(family => family.Family)), journey.name);
            foreach (var family in families)
                Assert.That(rows.Single(row => row.Family == family.Family).GetComponentsInChildren<Text>().Select(text => text.text),
                    Does.Contain(ContentReady.Charms.GetFamilyName(family)), journey.name);
        }

        /** Raycasts at the first offer card: covered, the topmost hit belongs to the introduction; uncovered, it is the card. */
        internal static IEnumerator AssertOfferCovered(SpiritEncounterView view, bool covered)
        {
            yield return null;
            yield return null;
            var card = view.RewardsPanel.Cards.First(value => value.Button.gameObject.activeInHierarchy).Button;
            var handler = ContentReady.TopClickHandler(card, out _, out var hit);
            if (covered)
            {
                Assert.That(hit.gameObject, Is.Not.Null);
                Assert.That(hit.gameObject.transform.IsChildOf(view.EncounterCharmsIntroduction.Screen.Root), Is.True,
                    "The introduction takes input over the offer: " + hit.gameObject.name);
                Assert.That(handler, Is.Not.SameAs(card.gameObject));
            }
            else Assert.That(handler, Is.SameAs(card.gameObject), "The offer is interactable.");
        }

        private static IEnumerator ChooseFirst(SpiritEncounterController controller, SpiritEncounterView view)
        {
            var panel = view.RewardsPanel;
            var card = panel.Cards.FirstOrDefault(value => value.Button.gameObject.activeInHierarchy);
            if (card != null) card.Button.onClick.Invoke();
            Assert.That(panel.ConfirmButton.IsInteractable(), Is.True);
            panel.ConfirmButton.onClick.Invoke();
            yield return ContentReady.Idle(controller);
        }

        private static IEnumerator LoadSession(SpiritEncounterController controller, EncounterSession session)
        {
            EncounterSaveFileStore.Save(controller.SavePath, session.CaptureSave());
            Assert.That(controller.TryLoad(), Is.True, controller.Status.ToString());
            yield return ContentReady.Idle(controller);
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(session.Snapshot.Revision));
        }

        private static IEnumerator SelectToInvitation(SpiritEncounterController controller, EncounterJourneyAsset journey)
        {
            controller.SelectEncounter(journey);
            yield return ContentReady.Routed(controller);
            while (!(controller.SelectedJourney.Beats[controller.BeatIndex] is InvitationBeat))
                controller.AdvanceJourney();
            yield return ContentReady.Routed(controller);
            yield return null;
        }

        private static void Execute(EncounterSession session, EncounterAction action)
        {
            var result = session.Execute(action, session.Snapshot.Revision);
            Assert.That(result.Accepted, Is.True, result.Rejection.ToString());
        }
    }
}
