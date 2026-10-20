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
    public sealed class SpiritEncounterEndingPlayTests
    {
        private SpiritEncounterController controller;
        private SpiritEncounterView view;
        private EncounterJourneyAsset journey;
        private ModifierDefinition[] unlockDefinitions;
        private string[] initialCompletedEncounterIds;

        [UnitySetUp]
        public IEnumerator InvestigationReady()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit(false);
            controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            view = controller.GetComponent<SpiritEncounterView>();
            SetField(controller, "useFixedSeed", true);
            SetField(controller, "initialSeed", "60");
        }

        [UnityTest]
        public IEnumerator VictoryPlaysHostMotionThenDialogueBeforeShowingClueAndScopedUnlocks()
        {
            yield return VictoryEnding("encounter.fox.introduction", "encounter.owl.watchtower", "", "fox-victory", true);
        }

        [UnityTest]
        public IEnumerator WatchtowerVictoryShowsItsClueAndHighlightsTheMothLead()
        {
            yield return VictoryEnding("encounter.owl.watchtower", "encounter.moth.crossroads", "owl-", "owl-victory", false,
                useAuthoredUnlocks: true);
        }

        [UnityTest]
        public IEnumerator MothVictoryPersistsItsAuthoredGlobalMilestonesAndShowsTheClueWithoutAFabricatedLead()
        {
            yield return VictoryEnding("encounter.moth.crossroads", "story.fox.chapter-one-return", "moth-", "moth-victory", false,
                useAuthoredUnlocks: true, preserveAuthoredMilestones: true);
        }

        private IEnumerator VictoryEnding(string encounterId, string followUpId, string capturePrefix, string motionPhase,
            bool followUpAtTop, bool useAuthoredUnlocks = false, bool preserveAuthoredMilestones = false)
        {
            InstallShortEncounter(true, encounterId, useAuthoredUnlocks, preserveAuthoredMilestones);
            yield return EnterEncounter();
            if (preserveAuthoredMilestones)
            {
                var definition = controller.Catalog.GetEncounter(encounterId);
                var finalMilestone = definition.UnlockMilestones.Last();
                Assert.That(finalMilestone.StageNumber, Is.EqualTo(definition.Stages.Count));
                var session = EncounterSession.CreateEncounter(controller.Catalog, encounterId, 60);
                for (int stage = 1; stage < definition.Stages.Count; ++stage)
                {
                    Assert.That(session.Execute(PlayHandAction.Instance, session.Snapshot.Revision).Accepted, Is.True);
                    if (session.Snapshot.Phase == EncounterPhase.ChoosingUnlock)
                    {
                        var offer = session.Snapshot.PendingReward;
                        Assert.That(session.Execute(new SelectModifierUnlockAction(offer.OfferId, offer.Options[0].OptionId),
                            session.Snapshot.Revision).Accepted, Is.True);
                    }
                    Assert.That(session.Snapshot.StageIndex, Is.EqualTo(stage));
                }
                EncounterSaveFileStore.Save(controller.SavePath, session.CaptureSave());
                controller.Load();
                yield return WaitForPresentation();
                Assert.That(controller.Snapshot.StageIndex, Is.EqualTo(definition.Stages.Count - 1));
                var persisted = PlayerProfileFileStore.Load(controller.ProfilePath);
                Assert.That(controller.Snapshot.NewlyUnlockedModifierIds.Count, Is.EqualTo(definition.UnlockMilestones.Count - 1));
                Assert.That(persisted.UnlockedModifierIds.Count, Is.EqualTo(definition.UnlockMilestones.Count - 1));
                foreach (string id in controller.Snapshot.NewlyUnlockedModifierIds)
                    Assert.That(persisted.UnlockedModifierIds, Does.Contain(id), "Loading must retain earlier milestones.");
            }
            controller.PlayHand();
            yield return WaitForPresentation();
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingUnlock));
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.None));
            Assert.That(view.HostPortrait.State, Is.Not.EqualTo(SpiritPresentationState.Victory));
            var finalOffer = controller.Snapshot.PendingReward;
            Assert.That(finalOffer.Options.Count, Is.InRange(1, 3));
            Assert.That(finalOffer.Options.All(option => !controller.ProfileUnlockedModifierIds.Contains(option.DefinitionId)), Is.True);
            var pendingSave = EncounterSaveFileStore.Load(controller.SavePath);
            Assert.That(pendingSave.Phase, Is.EqualTo(EncounterPhase.ChoosingUnlock));
            Assert.That(pendingSave.PendingReward.Options.Select(option => option.DefinitionId),
                Is.EqualTo(finalOffer.Options.Select(option => option.DefinitionId)));
            Click("RewardCard1");
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.None), "Selecting a card still requires confirmation.");
            Assert.That(Find<RectTransform>("RewardsPanel").gameObject.activeInHierarchy, Is.True);
            Assert.That(Find<Button>("EndingSkipButton").gameObject.activeInHierarchy, Is.False);
            Coroutine motion = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DICE_SPIRIT_MOTION_CAPTURE_ROOT"))
                ? null : view.StartCoroutine(CaptureVictoryMotion(motionPhase));
            Click("UpgradeHandButton");
            Assert.That(Find<RectTransform>("RewardsPanel").gameObject.activeInHierarchy, Is.False,
                "The confirmed final offer must close before the farewell starts.");
            Assert.That(view.CharmsButton.gameObject.activeInHierarchy, Is.False);
            Assert.That(view.HandRanksButton.gameObject.activeInHierarchy, Is.False);
            unlockDefinitions = controller.Snapshot.NewlyUnlockedModifierIds.Select(controller.Catalog.GetModifier).ToArray();
            Assert.That(unlockDefinitions.Length, Is.EqualTo(preserveAuthoredMilestones ?
                controller.Catalog.GetEncounter(encounterId).UnlockMilestones.Count : 1));
            float motionDeadline = Time.realtimeSinceStartup + 45;
            while (view.HostPortrait.State != SpiritPresentationState.Victory && Time.realtimeSinceStartup < motionDeadline)
            {
                Assert.That(Find<RectTransform>("RewardsPanel").gameObject.activeInHierarchy, Is.False);
                Assert.That(Find<RectTransform>("StageIntroPanel").gameObject.activeInHierarchy &&
                    Find<Text>("StageIntroTitle").text == "CHARM UNLOCKED", Is.False,
                    "Final-stage unlocks belong in the summary after the host's response.");
                yield return null;
            }
            Assert.That(view.HostPortrait.State, Is.EqualTo(SpiritPresentationState.Victory));
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(controller.IsBusy, Is.True);
            Assert.That(Find<Button>("EndingNewRunButton").gameObject.activeInHierarchy, Is.False);
            Assert.That(Find<Button>("EndingSkipButton").IsInteractable(), Is.True);
            Assert.That(Find<RectTransform>("RewardsPanel").gameObject.activeInHierarchy, Is.False,
                "Skip farewell must not appear over the offer screen.");
            float duration = view.HostPortrait.GetDuration(SpiritPresentationState.Victory);
            Assert.That(duration, Is.GreaterThan(.1f));
            float started = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(Mathf.Min(.15f, duration / 4));
            Assert.That(view.HostPortrait.Progress, Is.GreaterThan(0));
            if (motion == null) yield return Capture(capturePrefix + "victory-motion");
            yield return Until(() => Find<Button>("EndingDialogueContinueButton").gameObject.activeInHierarchy, "victory dialogue");
            Assert.That(Time.realtimeSinceStartup - started, Is.GreaterThanOrEqualTo(duration * .8f));
            yield return WaitForPresentation();
            if (motion != null) yield return motion;
            Assert.That(Find<Text>("EndingTitle").text, Is.EqualTo(journey.SpiritName));
            Assert.That(Find<Text>("EndingDetail").text, Is.EqualTo(journey.VictoryText));
            Assert.That(Find<Text>("EndingClueTitle").gameObject.activeInHierarchy, Is.False);
            Assert.That(Find<RectTransform>("PlayingPanel").gameObject.activeInHierarchy, Is.False);
            RefreshView();
            Assert.That(Find<Button>("EndingDialogueContinueButton").gameObject.activeInHierarchy, Is.True,
                "Refreshing the same result must retain the current dialogue.");
            yield return CheckPortraitSizes(capturePrefix + "victory-dialogue");
            Click("EndingDialogueContinueButton");
            yield return null;
            Assert.That(Find<Text>("EndingTitle").text, Is.EqualTo(journey.VictoryTitle));
            Assert.That(Find<Text>("EndingClueTitle").text, Is.EqualTo(journey.CompletionClueTitle));
            Assert.That(Find<Text>("EndingClueText").text, Is.EqualTo(journey.CompletionClueText));
            Assert.That(Find<Button>("EndingRetryButton").gameObject.activeInHierarchy, Is.False);
            foreach (var definition in unlockDefinitions)
            {
                Assert.That(Find<Text>("EndingUnlocks").text, Does.Contain(SpiritModifierPresentation.GetName(definition)));
                Assert.That(controller.ProfileUnlockedModifierIds, Does.Contain(definition.Id));
                Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).UnlockedModifierIds, Does.Contain(definition.Id));
            }
            Assert.That(Find<Text>("EndingUnlocks").text, Does.Contain("PERMANENTLY UNLOCKED THIS RUN"));
            Assert.That(Find<Text>("EndingUnlocks").text, Does.Contain("All encounters"));
            yield return CheckPortraitSizes(capturePrefix + "victory-summary");
            Click("EndingNewRunButton");
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.CompletedEncounterIds, Does.Contain(journey.EncounterId));
            Assert.That(view.GetComponentsInChildren<Text>().Where(label => label.name == "ClueTitle")
                .Select(label => label.text), Is.EquivalentTo(controller.JourneyCatalog.Entries
                    .Where(value => controller.IsJourneyAvailable(value.Id)).Select(value => value.LocationTitle)));
            if (preserveAuthoredMilestones)
            {
                Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).CompletedEncounterIds, Does.Contain(journey.EncounterId));
                foreach (var definition in unlockDefinitions)
                {
                    Assert.That(definition.Availability.Scope, Is.EqualTo(ModifierAvailabilityScope.Global));
                    Assert.That(definition.Availability.MatchesEncounter(controller.Catalog.GetEncounter("encounter.fox.introduction")), Is.True);
                    Assert.That(definition.Availability.MatchesEncounter(controller.Catalog.GetEncounter("encounter.owl.watchtower")), Is.True);
                    Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).UnlockedModifierIds, Does.Contain(definition.Id));
                }
            }
            Assert.That(controller.HighlightedEncounterIds, Does.Contain(followUpId));
            Canvas.ForceUpdateCanvases();
            var followUp = controller.JourneyCatalog.Find(followUpId);
            var leadTitle = view.GetComponentsInChildren<Text>().Single(label =>
                label.name == "ClueTitle" && label.text == followUp.LocationTitle);
            var marker = leadTitle.transform.parent.GetComponentsInChildren<Text>().Single(label => label.name == "ClueNewLead");
            Assert.That(marker.text, Is.EqualTo("NEW LEAD"));
            AssertCompleteText(marker);
            var viewport = Find<ScrollRect>("InvestigationClues").viewport;
            var corners = new Vector3[4];
            marker.rectTransform.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector2 local = viewport.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(viewport.rect.xMin - 2, viewport.rect.xMax + 2), marker.name);
                Assert.That(local.y, Is.InRange(viewport.rect.yMin - 2, viewport.rect.yMax + 2), marker.name);
            }
            if (followUpAtTop && viewport.InverseTransformPoint(corners[1]).y <= viewport.rect.yMax - 35)
            {
                var content = Find<ScrollRect>("InvestigationClues").content;
                var bottom = viewport.InverseTransformPoint(content.TransformPoint(new Vector3(0, content.rect.yMin, 0)));
                Assert.That(bottom.y, Is.EqualTo(viewport.rect.yMin).Within(2),
                    "A follow-up below the top must be limited by the end of the available leads.");
            }
            AssertCompleteText(leadTitle);
            yield return Capture(capturePrefix + "victory-new-lead-board");
            if (preserveAuthoredMilestones)
            {
                controller.SelectEncounter(journey.EncounterId);
                while (journey.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation) controller.AdvanceJourney();
                yield return Until(() => !view.JourneyView.IsAnimating, "replay invitation");
                controller.AcceptEncounter();
                yield return WaitForPresentation();
                Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.None));
                Assert.That(controller.Snapshot.Revision, Is.Zero);
                foreach (var definition in unlockDefinitions)
                    Assert.That(controller.Snapshot.UnlockedModifierIds, Does.Contain(definition.Id), "Replay must inherit permanent progress.");
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DefeatCanSkipItsMotionThenRetryFromTheInvitation()
        {
            yield return DefeatEnding("encounter.fox.introduction", "");
        }

        [UnityTest]
        public IEnumerator WatchtowerDefeatCanSkipItsMotionAndRetryTheInvitation()
        {
            yield return DefeatEnding("encounter.owl.watchtower", "owl-");
        }

        [UnityTest]
        public IEnumerator MothDefeatPlaysItsWithdrawalAndRetriesWithoutLosingPermanentProgress()
        {
            var profile = PlayerProfileFileStore.LoadOrCreate(controller.ProfilePath);
            string unlockedId = controller.Catalog.Modifiers.First(value =>
                value.Availability.Access == ModifierAccess.PermanentUnlock &&
                value.Availability.Scope == ModifierAvailabilityScope.Global).Id;
            profile.UnlockModifier(unlockedId);
            PlayerProfileFileStore.Save(controller.ProfilePath, profile);
            yield return DefeatEnding("encounter.moth.crossroads", "moth-", skipMotion: false);
            Assert.That(controller.ProfileUnlockedModifierIds, Does.Contain(unlockedId));
            Assert.That(controller.Snapshot.UnlockedModifierIds, Does.Contain(unlockedId));
            Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).UnlockedModifierIds, Does.Contain(unlockedId));
        }

        private IEnumerator DefeatEnding(string encounterId, string capturePrefix, bool skipMotion = true)
        {
            InstallShortEncounter(false, encounterId);
            yield return EnterEncounter();
            Coroutine motion = skipMotion || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DICE_SPIRIT_MOTION_CAPTURE_ROOT"))
                ? null : view.StartCoroutine(CaptureVictoryMotion(capturePrefix + "defeat"));
            controller.PlayHand();
            yield return Until(() => view.HostPortrait.State == SpiritPresentationState.Defeat, "defeat motion");
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Lost));
            Assert.That(controller.IsBusy, Is.True);
            var previous = controller.Snapshot;
            float started = Time.realtimeSinceStartup;
            if (skipMotion) Click("EndingSkipButton");
            yield return Until(() => Find<Button>("EndingDialogueContinueButton").gameObject.activeInHierarchy, "defeat dialogue");
            yield return WaitForPresentation();
            if (!skipMotion)
                Assert.That(Time.realtimeSinceStartup - started,
                    Is.GreaterThanOrEqualTo(view.HostPortrait.GetDuration(SpiritPresentationState.Defeat) * .8f));
            if (motion != null) yield return motion;
            Assert.That(Find<Text>("EndingDetail").text, Is.EqualTo(journey.DefeatText));
            Assert.That(Find<Button>("EndingNewRunButton").gameObject.activeInHierarchy, Is.False);
            yield return CheckPortraitSizes(capturePrefix + "defeat-dialogue");
            Click("EndingDialogueContinueButton");
            yield return null;
            Assert.That(Find<Text>("EndingTitle").text, Is.EqualTo(journey.DefeatTitle));
            Assert.That(Find<Text>("EndingClueTitle").gameObject.activeInHierarchy, Is.False);
            Assert.That(controller.CompletedEncounterIds, Is.EquivalentTo(initialCompletedEncounterIds));
            yield return CheckPortraitSizes(capturePrefix + "defeat-summary");
            Click("EndingRetryButton");
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(controller.BeatIndex, Is.EqualTo(journey.Beats.Count - 1));
            Assert.That(controller.Snapshot, Is.SameAs(previous), "Retry must wait for invitation acceptance.");
            Click("JourneyAcceptButton");
            yield return null;
            if (Find<RectTransform>("EncounterCharmsIntroduction").gameObject.activeInHierarchy)
            {
                Assert.That(controller.Snapshot, Is.SameAs(previous), "The encounter starts after its charm introduction.");
                Click("BeginEncounterButton");
            }
            yield return WaitForPresentation();
            Assert.That(controller.Snapshot, Is.Not.SameAs(previous));
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.None));
            Assert.That(controller.Snapshot.Revision, Is.Zero);
            Assert.That(Find<RectTransform>("EndingView").gameObject.activeInHierarchy, Is.False);
            Assert.That(controller.HighlightedEncounterIds, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AbandonShowsABriefFarewellSummaryWithoutDefeatOrDialogue()
        {
            yield return AbandonEnding("encounter.fox.introduction", "");
        }

        [UnityTest]
        public IEnumerator WatchtowerAbandonShowsItsBriefFarewellSummary()
        {
            yield return AbandonEnding("encounter.owl.watchtower", "owl-");
        }

        [UnityTest]
        public IEnumerator MothAbandonPlaysABriefFarewellAndReturnsWithoutACompletionOrLead()
        {
            yield return AbandonEnding("encounter.moth.crossroads", "moth-", observeFarewell: true);
        }

        private IEnumerator AbandonEnding(string encounterId, string capturePrefix, bool observeFarewell = false)
        {
            InstallShortEncounter(false, encounterId);
            yield return EnterEncounter();
            Coroutine motion = !observeFarewell || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DICE_SPIRIT_MOTION_CAPTURE_ROOT"))
                ? null : view.StartCoroutine(CaptureVictoryMotion(capturePrefix + "farewell", showsDialogue: false));
            controller.Abandon();
            if (observeFarewell)
            {
                yield return Until(() => view.HostPortrait.State == SpiritPresentationState.Farewell, "abandon farewell");
                Assert.That(controller.IsBusy, Is.True);
                Assert.That(view.HostPortrait.GetDuration(SpiritPresentationState.Farewell),
                    Is.LessThan(view.HostPortrait.GetDuration(SpiritPresentationState.Defeat)));
                Assert.That(Find<Button>("EndingDialogueContinueButton").gameObject.activeInHierarchy, Is.False);
            }
            yield return WaitForPresentation();
            if (motion != null) yield return motion;
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Abandoned));
            Assert.That(Find<Text>("EndingDetail").text, Is.EqualTo(journey.AbandonText));
            Assert.That(Find<Text>("EndingTitle").text, Is.Not.EqualTo(journey.DefeatTitle));
            Assert.That(Find<Button>("EndingDialogueContinueButton").gameObject.activeInHierarchy, Is.False);
            Assert.That(Find<Button>("EndingRetryButton").gameObject.activeInHierarchy, Is.False);
            Assert.That(Find<Text>("EndingClueTitle").gameObject.activeInHierarchy, Is.False);
            yield return CheckPortraitSizes(capturePrefix + "abandon-summary");
            Click("EndingNewRunButton");
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.CompletedEncounterIds, Is.EquivalentTo(initialCompletedEncounterIds));
            Assert.That(controller.CanResume, Is.False);
            Assert.That(controller.HighlightedEncounterIds, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DisablingTheViewDuringItsEndingFadeCancelsMotionAndRestoresOpacity()
        {
            yield return CancelEndingDuringFade("encounter.fox.introduction");
        }

        [UnityTest]
        public IEnumerator MothEndingCancellationRestoresOpacityAndCannotReopenTheOldResult()
        {
            yield return CancelEndingDuringFade("encounter.moth.crossroads");
        }

        private IEnumerator CancelEndingDuringFade(string encounterId)
        {
            InstallShortEncounter(false, encounterId);
            SetField(journey.GetOutcomePresentation(EncounterOutcome.Abandoned), "fadeDuration", 1f);
            yield return EnterEncounter();
            controller.Abandon();
            yield return Until(() => OpeningGroups().Any(group => group.alpha > .05f && group.alpha < .95f), "ending fade");
            Assert.That(controller.IsBusy, Is.True);
            view.enabled = false;
            yield return null;
            Assert.That(controller.IsBusy, Is.False, "Canceling the view must release the controller presentation.");
            foreach (var group in OpeningGroups()) Assert.That(group.alpha, Is.EqualTo(1).Within(.001f), group.name);
            view.enabled = true;
            yield return null;
            Assert.That(Find<Button>("EndingSkipButton").gameObject.activeInHierarchy, Is.False);
            Assert.That(Find<Button>("EndingDialogueContinueButton").gameObject.activeInHierarchy, Is.False);
            Click("EndingNewRunButton");
            yield return null;
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(Find<RectTransform>("EndingView").gameObject.activeInHierarchy, Is.False,
                "A canceled coroutine must not reopen the old ending.");
            LogAssert.NoUnexpectedReceived();
        }

        private void InstallShortFox(bool winning)
        {
            InstallShortEncounter(winning, "encounter.fox.introduction");
        }

        private void InstallShortEncounter(bool winning, string encounterId, bool useAuthoredUnlocks = false,
            bool preserveAuthoredMilestones = false)
        {
            var original = controller.Catalog;
            var authored = controller.JourneyCatalog.Entries.Single(value => value.EncounterId == encounterId);
            journey = UnityEngine.Object.Instantiate(authored);
            foreach (var beat in journey.Beats)
            {
                SetField(beat, "duration", 0f);
                SetField(beat, "autoAdvance", false);
            }
            var definition = original.GetEncounter(journey.EncounterId);
            unlockDefinitions = useAuthoredUnlocks
                ? definition.UnlockRewardModifierIds.Select(original.GetModifier).ToArray()
                : original.Modifiers.Where(value => value.Availability.Access == ModifierAccess.PermanentUnlock &&
                    value.Availability.Scope == ModifierAvailabilityScope.Global).Take(2).ToArray();
            if (useAuthoredUnlocks) Assert.That(unlockDefinitions.Length, Is.GreaterThan(2));
            else Assert.That(unlockDefinitions.Length, Is.EqualTo(2));
            var shortEncounter = new EncounterDefinition(definition.Id,
                Enumerable.Range(0, preserveAuthoredMilestones ? definition.Stages.Count : 1)
                    .Select(_ => new StageDefinition(winning ? 1 : 1000000, 1, 0)), rules: definition.Rules,
                spiritId: definition.SpiritId, unlockMilestones: winning
                    ? preserveAuthoredMilestones ? definition.UnlockMilestones.ToArray()
                        : new[] { new ModifierUnlockMilestone(1, unlockDefinitions.Select(value => value.Id)) }
                    : Array.Empty<ModifierUnlockMilestone>(), encounterModifierIds: definition.EncounterModifierIds,
                unlockRewardModifierIds: unlockDefinitions.Select(value => value.Id));
            var entries = controller.JourneyCatalog.Entries.Select(value => value == authored ? journey : value).ToArray();
            var mechanical = new MechanicalCatalog("ending-playmode.v1", original.Hands,
                entries.Where(value => !value.IsStoryOnly)
                    .Select(value => value == journey ? shortEncounter : original.GetEncounter(value.EncounterId)),
                original.Modifiers, original.RewardPools);
            var catalog = UnityEngine.Object.Instantiate(controller.JourneyCatalog);
            SetField(catalog, "chapters", Array.Empty<EncounterChapterAsset>());
            SetField(catalog, "entries", entries);
            Assert.That(controller.InitializeJourney(mechanical, catalog, controller.SavePath), Is.True, controller.JourneyStatus);
            var profile = PlayerProfileFileStore.LoadOrCreate(controller.ProfilePath);
            foreach (var definitionToUnlock in unlockDefinitions) profile.DiscoverFamily(definitionToUnlock.FamilyId);
            PlayerProfileFileStore.Save(controller.ProfilePath, profile);
            ContentReady.SeedJourneyPrerequisites(controller, journey.Id);
            initialCompletedEncounterIds = controller.CompletedEncounterIds.ToArray();
            Assert.That(journey.GetOutcomePresentation(EncounterOutcome.Won), Is.Not.Null);
            Assert.That(journey.GetOutcomePresentation(EncounterOutcome.Lost), Is.Not.Null);
            Assert.That(journey.GetOutcomePresentation(EncounterOutcome.Abandoned), Is.Not.Null);
        }

        private IEnumerator EnterEncounter()
        {
            controller.SelectEncounter(journey.EncounterId);
            while (journey.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation) controller.AdvanceJourney();
            yield return Until(() => !view.JourneyView.IsAnimating, "invitation");
            controller.AcceptEncounter();
            yield return WaitForPresentation();
            Assert.That(view.HostPortrait, Is.Not.Null);
        }

        private IEnumerator WaitForPresentation()
        {
            yield return Until(() => !controller.IsBusy, "presentation completion");
            yield return null;
        }

        private IEnumerator CaptureVictoryMotion(string phase, bool showsDialogue = true)
        {
            string directory = Path.Combine(Environment.GetEnvironmentVariable("DICE_SPIRIT_MOTION_CAPTURE_ROOT"), phase);
            Directory.CreateDirectory(directory);
            string manifest = Path.Combine(directory, "timestamps.csv");
            Assert.That(File.Exists(manifest), Is.False, "Use a fresh motion capture directory for each validation run.");
            var paths = new List<string>();
            using var timestamps = File.CreateText(manifest);
            timestamps.WriteLine("frame,seconds,realtime_seconds,file,state,progress,dialogue");
            double started = Time.realtimeSinceStartupAsDouble;
            double nextCapture = 0;
            while (true)
            {
                yield return new WaitForEndOfFrame();
                double now = Time.realtimeSinceStartupAsDouble;
                double elapsed = now - started;
                bool complete = Find<Button>(showsDialogue ? "EndingDialogueContinueButton" : "EndingNewRunButton").gameObject.activeInHierarchy;
                if (elapsed >= nextCapture || complete)
                {
                    string filename = string.Format(CultureInfo.InvariantCulture,
                        "frame-{0:D5}-{1:D6}ms.png", paths.Count, (int)(elapsed * 1000));
                    string path = Path.Combine(directory, filename);
                    ScreenCapture.CaptureScreenshot(path);
                    timestamps.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "{0},{1:F6},{2:F6},{3},{4},{5:F6},{6}", paths.Count, elapsed, now,
                        filename, view.HostPortrait.State, view.HostPortrait.Progress, complete ? 1 : 0));
                    paths.Add(path);
                    nextCapture = elapsed + .05;
                }
                if (complete) break;
                Assert.That(elapsed, Is.LessThan(45), "Outcome motion did not reach its result.");
                yield return null;
            }
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (paths.Any(path => !File.Exists(path) || new FileInfo(path).Length == 0) &&
                Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            foreach (string path in paths)
            {
                Assert.That(File.Exists(path), Is.True, path);
                Assert.That(new FileInfo(path).Length, Is.GreaterThan(0), path);
            }
            TestContext.WriteLine("Encounter outcome motion manifest: " + manifest);
        }

        private static IEnumerator Until(Func<bool> ready, string description)
        {
            float deadline = Time.realtimeSinceStartup + 45;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ready(), Is.True, description);
        }

        private IEnumerable<CanvasGroup> OpeningGroups() => (IEnumerable<CanvasGroup>)typeof(SpiritEncounterView)
            .GetField("openingGroups", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);

        private void RefreshView() => typeof(SpiritEncounterView).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(view, null);

        private IEnumerator CheckPortraitSizes(string name)
        {
            var portrait = (RectTransform)view.transform.Find("SpiritCanvas/SafeArea/Portrait");
            foreach (var size in new[] { new Vector2(443, 912), new Vector2(540, 960) })
            {
                portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                portrait.sizeDelta = size;
                portrait.anchoredPosition = Vector2.zero;
                yield return null;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                var panel = Find<RectTransform>("EndingPanel");
                Assert.That(panel.anchorMax.y, Is.LessThanOrEqualTo(.62f));
                foreach (var label in panel.GetComponentsInChildren<Text>())
                {
                    if (string.IsNullOrWhiteSpace(label.text)) continue;
                    AssertCompleteText(label);
                }
                var scroll = Find<ScrollRect>("EndingUnlockScroll");
                Assert.That(scroll.content.rect.height, Is.GreaterThan(0));
                Assert.That(scroll.viewport.rect.height, Is.GreaterThan(150));
                yield return Capture(name + "-" + size.x + "x" + size.y);
            }
        }

        private static void AssertCompleteText(Text label)
        {
            var rect = label.rectTransform.rect;
            var settings = label.GetGenerationSettings(rect.size);
            Assert.That(label.cachedTextGenerator.Populate(label.text, settings), Is.True, label.name);
            settings.fontSize = label.resizeTextForBestFit ? label.cachedTextGenerator.fontSizeUsedForBestFit : label.fontSize;
            settings.resizeTextForBestFit = false;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            using (var complete = new TextGenerator())
            {
                Assert.That(complete.Populate(label.text, settings), Is.True, label.name);
                Assert.That(label.cachedTextGenerator.vertexCount, Is.EqualTo(complete.vertexCount), label.name + " truncates text.");
            }
            Assert.That(label.cachedTextGenerator.vertexCount, Is.GreaterThan(4), label.name);
            foreach (var vertex in label.cachedTextGenerator.verts.Take(label.cachedTextGenerator.verts.Count - 4))
            {
                var position = vertex.position / label.pixelsPerUnit;
                Assert.That(position.x, Is.InRange(rect.xMin - 3, rect.xMax + 3), label.name);
                Assert.That(position.y, Is.InRange(rect.yMin - 3, rect.yMax + 3), label.name);
            }
        }

        private T Find<T>(string name) where T : Component
        {
            var value = view.GetComponentsInChildren<T>(true).FirstOrDefault(item => item.name == name);
            Assert.That(value, Is.Not.Null, name);
            return value;
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

        private static IEnumerator Capture(string name)
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) yield break;
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "ending-" + name + ".png");
            Assert.That(File.Exists(path), Is.False, "Use a fresh capture directory for each validation run.");
            ScreenCapture.CaptureScreenshot(path);
            for (int frame = 0; frame < 240 && !File.Exists(path); ++frame) yield return null;
            Assert.That(File.Exists(path), Is.True, path);
            TestContext.WriteLine("Encounter ending screenshot: " + path);
        }
    }
}
