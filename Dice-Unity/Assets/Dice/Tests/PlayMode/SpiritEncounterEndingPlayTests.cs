using System;
using System.Collections;
using System.Collections.Generic;
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
     * Encounter outcomes, each on the first authored encounter journey that authors a presentation of that outcome. The
     * journey runs against a short copy of its encounter (its rules, pieces and additions kept, its stages replaced) so
     * an outcome is reached in one or a few hands, while its host, texts, outcome presentations and follow-up leads come
     * from the authored journey.
     */
    public sealed class SpiritEncounterEndingPlayTests
    {
        // A victory clears this many stages, with a permanent unlock milestone at every stage.
        private const int VictoryStages = 3;
        private SpiritEncounterController controller;
        private SpiritEncounterView view => ContentReady.EncounterView(controller);
        private EncounterJourneyAsset journey;
        private ModifierDefinition[] unlockDefinitions;
        private EncounterDefinition.Id[] initialCompletedEncounterIds;

        [UnitySetUp]
        public IEnumerator InvestigationReady()
        {
            yield return Reload();
        }

        /** Loads a fresh application with its own isolated profile and save, ready at the investigation board. */
        private IEnumerator Reload()
        {
            yield return ContentReady.LoadApplication();
            controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            ContentReady.UseFixedSeed(controller);
        }

        /** The first authored encounter journey, in catalog order, that authors a presentation of the outcome. */
        private EncounterJourneyAsset EncounterJourney(EncounterOutcome outcome)
        {
            var found = controller.JourneyCatalog.Entries.FirstOrDefault(value => value != null && !value.IsStoryOnly &&
                value.GetOutcomePresentation(outcome) != null);
            Assert.That(found, Is.Not.Null, "The authored journey catalog has no encounter journey presenting " + outcome + ".");
            return found;
        }

        /**
         * Checks, on one encounter journey, that the final milestone offer precedes the victory, the host plays its
         * authored victory motion before the dialogue, the summary shows the clue and every permanent unlock earned in
         * the run (including milestones restored from a save), every journey this victory makes available is marked
         * as a new lead, and a replay inherits the unlocks. Fails if any of these does not happen.
         */
        [UnityTest]
        public IEnumerator VictoryPlaysHostMotionThenDialogueBeforeShowingClueUnlocksAndNewLeads()
        {
            yield return VictoryEnding(EncounterJourney(EncounterOutcome.Won));
        }

        private IEnumerator VictoryEnding(EncounterJourneyAsset authored)
        {
            InstallShortEncounter(true, authored);
            var outcome = journey.GetOutcomePresentation(EncounterOutcome.Won);
            yield return EnterEncounter();
            var definition = controller.Catalog.GetEncounter(journey.Encounter.Identity);
            var unlockedBefore = controller.ProfileUnlockedModifierIds.ToArray();
            // A parallel session clears every stage but the last, choosing each milestone's first unlock; the run is
            // then loaded from its save so the final victory follows restored milestones.
            var session = EncounterSession.CreateEncounter(controller.Catalog, journey.Encounter.Identity, ContentReady.Seed, unlockedBefore,
                controller.ProfileDiscoveredFamilyIds);
            for (int stage = 1; stage < definition.Stages.Count; ++stage)
            {
                Assert.That(session.Execute(PlayHandAction.Instance, session.Snapshot.Revision).Accepted, Is.True, journey.name);
                if (session.Snapshot.Phase == EncounterPhase.ChoosingUnlock)
                {
                    var offer = session.Snapshot.PendingReward;
                    Assert.That(session.Execute(new SelectModifierUnlockAction(offer.OfferId, offer.Options[0].OptionId),
                        session.Snapshot.Revision).Accepted, Is.True, journey.name);
                }
                Assert.That(session.Snapshot.StageIndex, Is.EqualTo(stage), journey.name);
            }
            EncounterSaveFileStore.Save(controller.SavePath, session.CaptureSave());
            controller.Load();
            yield return WaitForPresentation();
            Assert.That(controller.Snapshot.StageIndex, Is.EqualTo(definition.Stages.Count - 1), journey.name);
            Assert.That(controller.Snapshot.NewlyUnlockedModifierIds.Count, Is.EqualTo(definition.Stages.Count - 1), journey.name);
            Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).UnlockedModifierIds,
                Is.EquivalentTo(unlockedBefore.Concat(controller.Snapshot.NewlyUnlockedModifierIds)),
                journey.name + ": loading must retain earlier milestones.");

            controller.PlayHand();
            yield return WaitForPresentation();
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingUnlock), journey.name);
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.None), journey.name);
            Assert.That(view.HostPortrait.State, Is.Not.EqualTo(outcome.HostState), journey.name);
            var finalOffer = controller.Snapshot.PendingReward;
            Assert.That(finalOffer.Options.Count, Is.InRange(1, 3), journey.name);
            Assert.That(finalOffer.Options.All(option => !controller.ProfileUnlockedModifierIds.Contains(option.DefinitionId)),
                Is.True, journey.name);
            var pendingSave = EncounterSaveFileStore.Load(controller.SavePath, controller.Catalog);
            Assert.That(pendingSave.Phase, Is.EqualTo(EncounterPhase.ChoosingUnlock), journey.name);
            Assert.That(pendingSave.PendingReward.Options.Select(option => option.DefinitionId),
                Is.EqualTo(finalOffer.Options.Select(option => option.DefinitionId)), journey.name);
            Click("RewardCard1");
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.None), "Selecting a card still requires confirmation.");
            Assert.That(Find<RectTransform>("SpiritRewardsPanel").gameObject.activeInHierarchy, Is.True, journey.name);
            Assert.That(Find<Button>("EndingSkipButton").gameObject.activeInHierarchy, Is.False, journey.name);
            Click("UpgradeHandButton");
            Assert.That(Find<RectTransform>("SpiritRewardsPanel").gameObject.activeInHierarchy, Is.False,
                "The confirmed final offer must close before the farewell starts.");
            Assert.That(Find<Button>("CharmsButton").gameObject.activeInHierarchy, Is.False, journey.name);
            Assert.That(Find<Button>("HandRanksButton").gameObject.activeInHierarchy, Is.False, journey.name);
            unlockDefinitions = controller.Snapshot.NewlyUnlockedModifierIds.Select(controller.Catalog.GetModifier).ToArray();
            Assert.That(unlockDefinitions.Length, Is.EqualTo(definition.UnlockMilestones.Count), journey.name);
            float motionDeadline = Time.realtimeSinceStartup + ContentReady.SequenceTimeout;
            while (view.HostPortrait.State != outcome.HostState && Time.realtimeSinceStartup < motionDeadline)
            {
                Assert.That(Find<RectTransform>("SpiritRewardsPanel").gameObject.activeInHierarchy, Is.False, journey.name);
                Assert.That(Find<RectTransform>("StageIntroPanel").gameObject.activeInHierarchy, Is.False,
                    "No stage or unlock announcement plays over the host's response; final-stage unlocks belong in the summary.");
                yield return null;
            }
            Assert.That(view.HostPortrait.State, Is.EqualTo(outcome.HostState), journey.name);
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won), journey.name);
            Assert.That(controller.IsBusy, Is.True, journey.name);
            Assert.That(Find<Button>("EndingNewRunButton").gameObject.activeInHierarchy, Is.False, journey.name);
            Assert.That(Find<Button>("EndingSkipButton").IsInteractable(), Is.True, journey.name);
            Assert.That(Find<RectTransform>("SpiritRewardsPanel").gameObject.activeInHierarchy, Is.False,
                "Skip farewell must not appear over the offer screen.");
            string firstStep = outcome.ShowDialogue ? "EndingDialogueContinueButton" : "EndingNewRunButton";
            yield return ContentReady.WaitUntil(() => Find<Button>(firstStep).gameObject.activeInHierarchy, journey.name + " victory result", ContentReady.SequenceTimeout);
            yield return WaitForPresentation();
            Assert.That(Find<RectTransform>("PlayingPanel").gameObject.activeInHierarchy, Is.False, journey.name);
            if (outcome.ShowDialogue)
            {
                Assert.That(Find<Text>("EndingTitle").text, Is.EqualTo(journey.SpiritName), journey.name);
                Assert.That(Find<Text>("EndingDetail").text, Is.EqualTo(journey.VictoryText), journey.name);
                Assert.That(Find<Text>("EndingClueTitle").gameObject.activeInHierarchy, Is.False, journey.name);
                view.Refresh();
                Assert.That(Find<Button>("EndingDialogueContinueButton").gameObject.activeInHierarchy, Is.True,
                    "Refreshing the same result must retain the current dialogue.");
                Click("EndingDialogueContinueButton");
                yield return null;
            }
            Assert.That(Find<Text>("EndingTitle").text, Is.EqualTo(journey.VictoryTitle), journey.name);
            Assert.That(Find<Text>("EndingClueTitle").text, Is.EqualTo(journey.CompletionClueTitle), journey.name);
            Assert.That(Find<Text>("EndingClueText").text, Is.EqualTo(journey.CompletionClueText), journey.name);
            Assert.That(Find<Button>("EndingRetryButton").gameObject.activeInHierarchy, Is.False, journey.name);
            foreach (var unlocked in unlockDefinitions)
            {
                Assert.That(Find<Text>("EndingUnlocks").text, Does.Contain(ContentReady.Charms.GetName(unlocked)), journey.name);
                Assert.That(controller.ProfileUnlockedModifierIds, Does.Contain(unlocked.Id), journey.name);
                Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).UnlockedModifierIds, Does.Contain(unlocked.Id), journey.name);
            }
            // The summary opens with the authored wording of permanent unlocks and names each unlock's scope.
            var wording = Find<Text>("EndingUnlocks").GetComponentInParent<SpiritUiContext>(true).Wording;
            string permanentHeading = wording.ending.permanentUnlocks.Substring(0, wording.ending.permanentUnlocks.IndexOf("{charms}"));
            Assert.That(Find<Text>("EndingUnlocks").text, Does.Contain(permanentHeading), journey.name);
            Assert.That(Find<Text>("EndingUnlocks").text, Does.Contain(wording.charmAvailability.allEncounters), journey.name);

            Click("EndingNewRunButton");
            yield return ContentReady.Routed(controller);
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation), journey.name);
            Assert.That(controller.CompletedEncounterIds, Does.Contain(journey.Encounter.Identity), journey.name);
            Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).CompletedEncounterIds, Does.Contain(journey.Encounter.Identity),
                journey.name);
            Assert.That(ContentReady.PresentationRoot(controller).GetComponentsInChildren<Text>().Where(label => label.name == "ClueTitle")
                .Select(label => label.text), Is.EquivalentTo(controller.JourneyCatalog.Entries
                    .Where(value => controller.IsJourneyAvailable(value)).Select(value => value.LocationTitle)), journey.name);
            yield return CheckNewLeads();

            controller.SelectEncounter(journey);
            yield return ContentReady.Routed(controller);
            while (journey.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation) controller.AdvanceJourney();
            yield return ContentReady.WaitUntil(() => !view.JourneyView.IsAnimating, journey.name + " replay invitation", ContentReady.SequenceTimeout);
            controller.AcceptEncounter();
            yield return WaitForPresentation();
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.None), journey.name);
            Assert.That(controller.Snapshot.Revision, Is.Zero, journey.name);
            foreach (var unlocked in unlockDefinitions)
                Assert.That(controller.Snapshot.UnlockedModifierIds, Does.Contain(unlocked.Id), "Replay must inherit permanent progress.");
            LogAssert.NoUnexpectedReceived();
        }

        /**
         * The leads this victory opens are the journeys whose completion prerequisites name its journey and that are
         * now available. Each is highlighted with a visible new-lead marker, and every other card is unmarked.
         */
        private IEnumerator CheckNewLeads()
        {
            var followUps = controller.JourneyCatalog.Entries.Where(value => value != null &&
                value.RequiredCompletedJourneys.Any(required => required.Identity == journey.Identity) &&
                controller.IsJourneyAvailable(value)).ToArray();
            foreach (var followUp in followUps)
                Assert.That(controller.HighlightedJourneys, Does.Contain(followUp), journey.name + " -> " + followUp.name);
            yield return null;
            var titles = ContentReady.PresentationRoot(controller).GetComponentsInChildren<Text>().Where(label => label.name == "ClueTitle")
                .ToArray();
            foreach (var title in titles)
            {
                bool isFollowUp = followUps.Any(value => value.LocationTitle == title.text);
                var marker = title.transform.parent.GetComponentsInChildren<Text>(true).Single(label => label.name == "ClueNewLead");
                Assert.That(marker.gameObject.activeInHierarchy, Is.EqualTo(isFollowUp), journey.name + " new-lead marker on " + title.text);
            }
        }

        /**
         * Checks, on one encounter journey, that a defeat plays the host's authored defeat motion, shows the defeat text
         * and summary without completing the encounter, retries from the invitation without losing permanent progress,
         * and that a second defeat can skip its motion. Fails if any step is missing or the retry reuses the old run.
         */
        [UnityTest]
        public IEnumerator DefeatPlaysOrSkipsItsMotionThenRetriesFromTheInvitationKeepingPermanentProgress()
        {
            var profile = PlayerProfileFileStore.LoadOrCreate(controller.ProfilePath);
            var permanent = controller.Catalog.Modifiers.FirstOrDefault(value =>
                value.Availability.Access == ModifierAccess.PermanentUnlock);
            Assert.That(permanent, Is.Not.Null, "The catalog has no permanent unlock to retain across a retry.");
            profile.UnlockModifier(permanent.Id);
            PlayerProfileFileStore.Save(controller.ProfilePath, profile);
            InstallShortEncounter(false, EncounterJourney(EncounterOutcome.Lost));
            yield return EnterEncounter();
            yield return DefeatAndRetry(skipMotion: false);
            Assert.That(controller.ProfileUnlockedModifierIds, Does.Contain(permanent.Id), journey.name);
            Assert.That(controller.Snapshot.UnlockedModifierIds, Does.Contain(permanent.Id), journey.name);
            Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).UnlockedModifierIds, Does.Contain(permanent.Id), journey.name);
            // A defeat without host motion has nothing to skip.
            if (view.HostPortrait.GetDuration(journey.GetOutcomePresentation(EncounterOutcome.Lost).HostState) > 0)
                yield return DefeatAndRetry(skipMotion: true);
        }

        private IEnumerator DefeatAndRetry(bool skipMotion)
        {
            var outcome = journey.GetOutcomePresentation(EncounterOutcome.Lost);
            controller.PlayHand();
            yield return ContentReady.WaitUntil(() => view.HostPortrait.State == outcome.HostState, journey.name + " defeat motion", ContentReady.SequenceTimeout);
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Lost), journey.name);
            Assert.That(controller.IsBusy, Is.True, journey.name);
            var previous = controller.Snapshot;
            if (skipMotion) Click("EndingSkipButton");
            string firstStep = outcome.ShowDialogue ? "EndingDialogueContinueButton" : "EndingRetryButton";
            yield return ContentReady.WaitUntil(() => Find<Button>(firstStep).gameObject.activeInHierarchy, journey.name + " defeat result", ContentReady.SequenceTimeout);
            yield return WaitForPresentation();
            if (outcome.ShowDialogue)
            {
                Assert.That(Find<Text>("EndingDetail").text, Is.EqualTo(journey.DefeatText), journey.name);
                Assert.That(Find<Button>("EndingNewRunButton").gameObject.activeInHierarchy, Is.False, journey.name);
                Click("EndingDialogueContinueButton");
                yield return ContentReady.Routed();
                yield return null;
            }
            Assert.That(Find<Text>("EndingTitle").text, Is.EqualTo(journey.DefeatTitle), journey.name);
            Assert.That(Find<Text>("EndingClueTitle").gameObject.activeInHierarchy, Is.False, journey.name);
            Assert.That(controller.CompletedEncounterIds, Is.EquivalentTo(initialCompletedEncounterIds), journey.name);
            Click("EndingRetryButton");
            yield return ContentReady.Routed();
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence), journey.name);
            Assert.That(controller.BeatIndex, Is.EqualTo(journey.Beats.Count - 1), journey.name);
            Assert.That(controller.Snapshot, Is.SameAs(previous), "Retry must wait for invitation acceptance.");
            Click("JourneyAcceptButton");
            yield return ContentReady.Routed();
            yield return null;
            if (view.JourneyView.EncounterCharms.IsOpen)
            {
                Assert.That(controller.Snapshot, Is.SameAs(previous), "The encounter starts after its charm introduction.");
                Click("BeginEncounterButton");
                yield return ContentReady.Routed();
            }
            yield return WaitForPresentation();
            Assert.That(controller.Snapshot, Is.Not.SameAs(previous), journey.name);
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.None), journey.name);
            Assert.That(controller.Snapshot.Revision, Is.Zero, journey.name);
            Assert.That(view.GetComponentInChildren<SpiritEncounterEndingView>(true).Root.gameObject.activeInHierarchy, Is.False,
                journey.name);
            Assert.That(controller.HighlightedJourneys, Is.Empty, journey.name);
            LogAssert.NoUnexpectedReceived();
        }

        /**
         * Checks, on one encounter journey, that abandoning plays the host's authored farewell state, shows the abandon
         * text without the defeat title, a retry or a clue, and returns to the board without a completion, a resumable
         * run or a highlighted lead. Fails if any of these does not hold.
         */
        [UnityTest]
        public IEnumerator AbandonPlaysItsFarewellAndReturnsWithoutACompletionOrLead()
        {
            yield return AbandonEnding(EncounterJourney(EncounterOutcome.Abandoned));
        }

        private IEnumerator AbandonEnding(EncounterJourneyAsset authored)
        {
            InstallShortEncounter(false, authored);
            var outcome = journey.GetOutcomePresentation(EncounterOutcome.Abandoned);
            yield return EnterEncounter();
            controller.Abandon();
            yield return ContentReady.WaitUntil(() => view.HostPortrait.State == outcome.HostState, journey.name + " abandon farewell", ContentReady.SequenceTimeout);
            Assert.That(controller.IsBusy, Is.True, journey.name);
            Assert.That(Find<Button>("EndingDialogueContinueButton").gameObject.activeInHierarchy, Is.False, journey.name);
            yield return WaitForPresentation();
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Abandoned), journey.name);
            Assert.That(Find<Button>("EndingDialogueContinueButton").gameObject.activeInHierarchy, Is.EqualTo(outcome.ShowDialogue),
                journey.name);
            if (outcome.ShowDialogue)
            {
                Click("EndingDialogueContinueButton");
                yield return null;
            }
            // Without authored abandon text the summary shows the screen's own fallback line.
            if (!string.IsNullOrWhiteSpace(journey.AbandonText))
                Assert.That(Find<Text>("EndingDetail").text, Is.EqualTo(journey.AbandonText), journey.name);
            Assert.That(Find<Text>("EndingTitle").text, Is.Not.EqualTo(journey.DefeatTitle), journey.name);
            Assert.That(Find<Button>("EndingRetryButton").gameObject.activeInHierarchy, Is.False, journey.name);
            Assert.That(Find<Text>("EndingClueTitle").gameObject.activeInHierarchy, Is.False, journey.name);
            Click("EndingNewRunButton");
            yield return ContentReady.Routed();
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation), journey.name);
            Assert.That(controller.CompletedEncounterIds, Is.EquivalentTo(initialCompletedEncounterIds), journey.name);
            Assert.That(controller.CanResume, Is.False, journey.name);
            Assert.That(controller.HighlightedJourneys, Is.Empty, journey.name);
            LogAssert.NoUnexpectedReceived();
        }

        /**
         * Installs a catalog in which the journey's encounter keeps its rules, pieces and additions but has short
         * stages: a won run clears VictoryStages one-point stages with a milestone at each, offering global permanent
         * unlocks; a lost or abandoned run has one stage it cannot clear. The journey's prerequisites are completed.
         */
        private void InstallShortEncounter(bool winning, EncounterJourneyAsset authored)
        {
            var original = controller.Catalog;
            journey = UnityEngine.Object.Instantiate(authored);
            foreach (var beat in journey.Beats)
            {
                ContentReady.SetField(beat, "duration", 0f);
                ContentReady.SetField(beat, "autoAdvance", false);
            }
            var definition = original.GetEncounter(journey.Encounter.Identity);
            int stageCount = winning ? VictoryStages : 1;
            // Charms the journey grants on its own beats are already owned when its milestones are offered.
            var granted = new HashSet<string>(journey.Beats.Where(beat => beat.ProgressionReward != null)
                .SelectMany(beat => beat.ProgressionReward.Build(original).GrantedModifierIds), StringComparer.Ordinal);
            // Each milestone offers up to three unowned charms, so the final one still has a full offer.
            int unlockCount = stageCount + 2;
            unlockDefinitions = original.Modifiers.Where(value => value.Availability.Access == ModifierAccess.PermanentUnlock &&
                value.RewardEligible && !granted.Contains(value.Id))
                .Take(unlockCount).ToArray();
            Assert.That(unlockDefinitions.Length, Is.EqualTo(unlockCount),
                "The catalog has too few permanent unlocks for a milestone at every stage.");
            var unlockIds = unlockDefinitions.Select(value => value.Id).ToArray();
            var shortEncounter = new EncounterDefinition(definition.Identity,
                Enumerable.Range(0, stageCount).Select(_ => new StageDefinition(winning ? 1 : 1000000, 1, 0)),
                rules: definition.Rules,
                unlockMilestones: winning
                    ? Enumerable.Range(1, stageCount).Select(stage => new ModifierUnlockMilestone(stage)).ToArray()
                    : Array.Empty<ModifierUnlockMilestone>(),
                encounterModifierIds: definition.EncounterModifierIds, unlockRewardModifierIds: unlockIds,
                pieces: definition.Pieces,
                encounterModifiersFirstClearedStageNumber: Math.Min(definition.EncounterModifiersFirstClearedStageNumber, stageCount));
            var entries = controller.JourneyCatalog.Entries.Select(value => value == authored ? journey : value).ToArray();
            var mechanical = new MechanicalCatalog("ending-playmode.v1", original.Hands,
                entries.Where(value => !value.IsStoryOnly)
                    .Select(value => value == journey ? shortEncounter : original.GetEncounter(value.Encounter.Identity)),
                original.Modifiers);
            var catalog = UnityEngine.Object.Instantiate(controller.JourneyCatalog);
            ContentReady.SetField(catalog, "chapters", controller.JourneyCatalog.Chapters.Select(chapter =>
            {
                var copy = UnityEngine.Object.Instantiate(chapter);
                ContentReady.SetField(copy, "entries", chapter.Entries.Select(value => value == authored ? journey : value).ToArray());
                return copy;
            }).ToArray());
            Assert.That(controller.InitializeJourney(mechanical, catalog, controller.SavePath), Is.True, controller.JourneyStatus.ToString());
            var profile = PlayerProfileFileStore.LoadOrCreate(controller.ProfilePath);
            foreach (var definitionToUnlock in unlockDefinitions) profile.DiscoverFamily(definitionToUnlock.Family.Identity);
            PlayerProfileFileStore.Save(controller.ProfilePath, profile);
            ContentReady.SeedJourneyPrerequisites(controller, journey);
            initialCompletedEncounterIds = controller.CompletedEncounterIds.ToArray();
        }

        private IEnumerator EnterEncounter()
        {
            controller.SelectEncounter(journey);
            yield return ContentReady.Routed(controller);
            while (journey.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation) controller.AdvanceJourney();
            yield return ContentReady.WaitUntil(() => !view.JourneyView.IsAnimating, journey.name + " invitation", ContentReady.SequenceTimeout);
            controller.AcceptEncounter();
            yield return ContentReady.Routed(controller);
            yield return WaitForPresentation();
            Assert.That(view.HostPortrait, Is.Not.Null, journey.name);
        }

        private IEnumerator WaitForPresentation()
        {
            yield return ContentReady.Routed();
            yield return ContentReady.WaitUntil(() => !controller.IsBusy, "presentation completion", ContentReady.SequenceTimeout);
            yield return null;
        }

        private T Find<T>(string name) where T : Component => ContentReady.Find<T>(controller, name);

        private void Click(string name) => ContentReady.Click(Find<Button>(name));
    }
}
