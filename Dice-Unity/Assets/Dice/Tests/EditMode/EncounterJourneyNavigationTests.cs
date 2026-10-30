using System;
using System.Collections.Generic;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEngine;
using Progress = Dice.App.EncounterJourneyNavigation.Progress;

namespace Dice.Tests
{
    public sealed class EncounterJourneyNavigationTests
    {
        private static readonly EncounterDefinition.Id FirstId = TestEncounters.IdOf("encounter.harbor");
        private static readonly EncounterDefinition.Id SecondId = TestEncounters.IdOf("encounter.observatory");
        private static readonly EncounterDefinition.Id MissingId = TestEncounters.IdOf("encounter.missing");
        private static readonly EncounterDefinition.Id OnwardId = TestEncounters.IdOf("encounter.onward");
        private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        private readonly SyntheticCharmAssets content = new SyntheticCharmAssets();
        private EncounterJourneyNavigation navigation;
        private EncounterJourneyCatalogAsset catalog;
        private EncounterJourneyAsset first;
        private EncounterJourneyAsset second;
        private EncounterJourneyAsset story;

        [SetUp]
        public void SetUp()
        {
            navigation = new EncounterJourneyNavigation();
            first = Journey("encounter.harbor");
            second = Journey("encounter.observatory");
            story = Journey("story.return", true);
            catalog = Create<EncounterJourneyCatalogAsset>();
            SyntheticUi.Set(catalog, "chapters", new[] { Chapter("chapter.all", first, second, story) });
            navigation.InitializeJourney(catalog);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();
            content.Dispose();
        }

        [Test]
        public void MainMenuClearsJourneyLocation()
        {
            Assert.That(navigation.SelectEncounter(first, EmptyProgress(), out _), Is.True);
            navigation.ShowMainMenu();
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));
            Assert.That(navigation.SelectedJourney, Is.Null);
            Assert.That(navigation.BeatIndex, Is.EqualTo(-1));
            Assert.That(navigation.CanResume(true), Is.False);
            Assert.That(navigation.SelectEncounter(first, EmptyProgress(), out _), Is.False);
        }

        [Test]
        public void AvailabilityRequiresAProfileAndEveryPrerequisite()
        {
            SyntheticUi.Set(second, "requiredCompletedJourneys", new[] { first, story });
            Assert.That(navigation.IsJourneyAvailable(second, new Progress(false, null, Completed(first, story))), Is.False);
            Assert.That(navigation.IsJourneyAvailable(second, new Progress(true, null, Completed(first))), Is.False);
            Assert.That(navigation.IsJourneyAvailable(second, new Progress(true, null, Completed(story))), Is.False);
            Assert.That(navigation.IsJourneyAvailable(second, new Progress(true, null, Completed(first, story))), Is.True);
            Assert.That(navigation.SelectEncounter(second, EmptyProgress(), out var error), Is.False);
            Assert.That(error, Is.EqualTo(SpiritStatusKind.LocationLocked));
            AssertInvestigation();
            Assert.That(navigation.IsJourneyAvailable(Journey("encounter.missing"), EmptyProgress()), Is.False);
        }

        [TestCase("visited")]
        [TestCase("encounter-completed")]
        [TestCase("story-completed")]
        public void ExistingProgressKeepsALeadAvailableWhenItsPrerequisitesAreMissing(string progressKind)
        {
            var selected = progressKind == "story-completed" ? story : second;
            SyntheticUi.Set(selected, "requiredCompletedJourneys", new[] { first });
            var progress = new Progress(true, progressKind == "visited" ? new[] { SecondId } : null,
                progressKind == "visited" ? null : Completed(selected));
            Assert.That(navigation.IsJourneyAvailable(selected, progress), Is.True);
            Assert.That(navigation.SelectEncounter(selected, progress, out _), Is.True);
            Assert.That(navigation.SelectedJourney, Is.SameAs(selected));
            Assert.That(navigation.BeatIndex, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FirstVisitsAndStoriesCannotSkipTheSequence(bool storyOnly)
        {
            var selected = storyOnly ? story : first;
            var progress = new Progress(true, null, null);
            Assert.That(navigation.SelectEncounter(selected, progress, out _), Is.True);
            Assert.That(navigation.CanSkipIntro(progress), Is.False);
            Assert.That(navigation.SkipIntro(progress, out var transition), Is.False);
            Assert.That(transition, Is.Null);
            Assert.That(navigation.BeatIndex, Is.Zero);
        }

        [Test]
        public void RepeatedVisitSkipProposesEveryIntroRewardAndWaitsAtTheInvitation()
        {
            var progress = new Progress(true, new[] { FirstId }, null);
            Assert.That(navigation.SelectEncounter(first, progress, out _), Is.True);
            Assert.That(navigation.CanSkipIntro(new Progress(false, new[] { FirstId }, null)), Is.False);
            Assert.That(navigation.SkipIntro(progress, out var transition), Is.True);
            int invitation = first.IndexOfEncounter(FirstId) - 1;
            Assert.That(transition.Rewards, Is.EqualTo(first.Beats.Take(invitation).Select(beat => beat.ProgressionReward)));
            Assert.That(navigation.BeatIndex, Is.Zero);
            navigation.Commit(transition);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(navigation.BeatIndex, Is.EqualTo(invitation));
            Assert.That(navigation.CanSkipIntro(progress), Is.False);
            Assert.That(navigation.AdvanceJourney(EncounterOutcome.None, out _), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdvanceAndAcceptCanBeRetriedAfterAnUncommittedSideEffect(bool accepting)
        {
            Assert.That(navigation.SelectEncounter(first, EmptyProgress(), out _), Is.True);
            if (accepting) ReachInvitation();
            int before = navigation.BeatIndex;
            bool proposed = accepting ? navigation.AcceptEncounter(out var transition)
                : navigation.AdvanceJourney(EncounterOutcome.None, out transition);
            Assert.That(proposed, Is.True);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(navigation.SelectedJourney, Is.SameAs(first));
            Assert.That(navigation.BeatIndex, Is.EqualTo(before));
            Assert.That(transition.Rewards, Is.EqualTo(first.Beats.Take(before + 1).Select(beat => beat.ProgressionReward)));

            bool retried = accepting ? navigation.AcceptEncounter(out var retry) : navigation.AdvanceJourney(EncounterOutcome.None, out retry);
            Assert.That(retried, Is.True);
            Assert.That(retry.Journey, Is.SameAs(transition.Journey));
            Assert.That(retry.Phase, Is.EqualTo(transition.Phase));
            Assert.That(retry.BeatIndex, Is.EqualTo(transition.BeatIndex));
            Assert.That(retry.Rewards, Is.EqualTo(transition.Rewards));
            navigation.Commit(retry);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(accepting ? EncounterJourneyPhase.Playing : EncounterJourneyPhase.Sequence));
            // Accepting plays the Encounter beat after the invitation.
            Assert.That(navigation.BeatIndex, Is.EqualTo(before + 1));
        }

        [Test]
        public void AdvancingNeverAcceptsTheInvitation()
        {
            Assert.That(navigation.SelectEncounter(first, EmptyProgress(), out _), Is.True);
            Assert.That(navigation.AcceptEncounter(out _), Is.False);
            ReachInvitation();
            Assert.That(navigation.AdvanceJourney(EncounterOutcome.Won, out var transition), Is.False);
            Assert.That(transition, Is.Null);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(navigation.BeatIndex, Is.EqualTo(first.IndexOfEncounter(FirstId) - 1));
            Assert.That(navigation.AcceptEncounter(out transition), Is.True);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            navigation.Commit(transition);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
        }

        [Test]
        public void StoryCompletionProposesAllRewardsBeforeReturningToInvestigation()
        {
            Assert.That(navigation.SelectEncounter(story, EmptyProgress(), out _), Is.True);
            Assert.That(navigation.AcceptEncounter(out _), Is.False);
            Assert.That(navigation.AdvanceJourney(EncounterOutcome.None, out var advance), Is.True);
            Assert.That(advance.CompletesJourney, Is.False);
            navigation.Commit(advance);
            Assert.That(navigation.AdvanceJourney(EncounterOutcome.None, out var completion), Is.True);
            Assert.That(completion.CompletesJourney, Is.True);
            Assert.That(completion.Phase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(completion.Rewards, Is.EqualTo(story.Beats.Select(beat => beat.ProgressionReward).Concat(new[] { story.CompletionReward })));
            Assert.That(navigation.SelectedJourney, Is.SameAs(story));
            Assert.That(navigation.BeatIndex, Is.EqualTo(story.Beats.Count - 1));
            Assert.That(navigation.AdvanceJourney(EncounterOutcome.None, out var retry), Is.True);
            Assert.That(retry.Rewards, Is.EqualTo(completion.Rewards));
            navigation.Commit(retry);
            AssertInvestigation();
        }

        [Test]
        public void AWinContinuesWithTheNextBeatAndTheLastBeatCompletesTheJourney()
        {
            var journey = OnwardJourney();
            navigation.Initialize(catalog, OnwardId);
            Assert.That(navigation.BeatIndex, Is.EqualTo(2));
            Assert.That(navigation.AdvanceJourney(EncounterOutcome.Lost, out var refused), Is.False);
            Assert.That(refused, Is.Null);
            Assert.That(navigation.AdvanceJourney(EncounterOutcome.Won, out var onward), Is.True);
            Assert.That(onward.Phase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(onward.BeatIndex, Is.EqualTo(3));
            Assert.That(onward.CompletesJourney, Is.False);
            Assert.That(onward.Rewards, Is.EqualTo(journey.Beats.Take(3).Select(beat => beat.ProgressionReward)));
            navigation.Commit(onward);
            Assert.That(navigation.AdvanceJourney(EncounterOutcome.None, out var completion), Is.True);
            Assert.That(completion.Phase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(completion.CompletesJourney, Is.True);
            Assert.That(completion.Rewards,
                Is.EqualTo(journey.Beats.Select(beat => beat.ProgressionReward).Concat(new[] { journey.CompletionReward })));
        }

        [Test]
        public void AWonLastEncounterBeatReturnsToTheBoardItsWinCompleted()
        {
            navigation.Initialize(catalog, FirstId);
            Assert.That(navigation.AdvanceJourney(EncounterOutcome.Won, out var transition), Is.True);
            Assert.That(transition.Phase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(transition.CompletesJourney, Is.False);
            Assert.That(transition.Rewards, Is.EqualTo(first.Beats.Select(beat => beat.ProgressionReward)));
            navigation.Commit(transition);
            AssertInvestigation();
        }

        [Test]
        public void AnUnfinishedJourneyResumesAfterItsLastWonEncounterBeat()
        {
            var journey = OnwardJourney();
            var won = new[] { OnwardId };
            Assert.That(navigation.SelectEncounter(journey, new Progress(true, null, null, won), out _), Is.True);
            Assert.That(navigation.BeatIndex, Is.EqualTo(3));
            navigation.ReturnToInvestigation();
            Assert.That(navigation.SelectEncounter(journey, new Progress(true, null, Completed(journey), won), out _), Is.True);
            Assert.That(navigation.BeatIndex, Is.Zero, "A finished journey replays from its start.");
            navigation.ReturnToInvestigation();
            Assert.That(navigation.SelectEncounter(journey, EmptyProgress(), out _), Is.True);
            Assert.That(navigation.BeatIndex, Is.Zero);
        }

        [Test]
        public void ResumeUsesTheActiveEncounterAfterAnotherLeadWasSelectedAndCancelled()
        {
            navigation.Initialize(catalog, FirstId);
            Assert.That(navigation.ReturnToInvestigation(), Is.True);
            Assert.That(navigation.SelectEncounter(second, EmptyProgress(), out _), Is.True);
            Assert.That(navigation.ResumeEncounter(FirstId, true, out _), Is.False);
            Assert.That(navigation.SelectedJourney, Is.SameAs(second));
            Assert.That(navigation.ReturnToInvestigation(), Is.True);
            Assert.That(navigation.ResumeEncounter(FirstId, false, out _), Is.False);
            Assert.That(navigation.ResumeEncounter(MissingId, true, out var error), Is.False);
            Assert.That(error, Is.EqualTo(SpiritStatusKind.ActiveEncounterHasNoLead));
            AssertInvestigation();
            Assert.That(navigation.ResumeEncounter(FirstId, true, out error), Is.True);
            Assert.That(error, Is.EqualTo(SpiritStatusKind.None));
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(navigation.SelectedJourney, Is.SameAs(first));
            Assert.That(navigation.BeatIndex, Is.EqualTo(first.IndexOfEncounter(FirstId)));
        }

        [TestCase(EncounterOutcome.None)]
        [TestCase(EncounterOutcome.Won)]
        [TestCase(EncounterOutcome.Lost)]
        [TestCase(EncounterOutcome.Abandoned)]
        public void RetryRequiresAnOutcomeAndReturnsToTheSameInvitation(EncounterOutcome outcome)
        {
            navigation.Initialize(catalog, FirstId);
            Assert.That(navigation.RetryEncounter(FirstId, outcome), Is.EqualTo(outcome != EncounterOutcome.None));
            Assert.That(navigation.SelectedJourney, Is.SameAs(first));
            if (outcome == EncounterOutcome.None)
            {
                Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
                Assert.That(navigation.BeatIndex, Is.EqualTo(first.IndexOfEncounter(FirstId)));
                return;
            }
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(navigation.BeatIndex, Is.EqualTo(first.IndexOfEncounter(FirstId) - 1));
            Assert.That(navigation.AdvanceJourney(EncounterOutcome.None, out _), Is.False);
            Assert.That(navigation.AcceptEncounter(out _), Is.True);
        }

        [Test]
        public void DirectInitializationSupportsACatalogWithoutJourneyLeads()
        {
            navigation.Initialize(null, FirstId);
            Assert.That(navigation.JourneyCatalog, Is.Null);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(navigation.SelectedJourney, Is.Null);
            Assert.That(navigation.BeatIndex, Is.EqualTo(-1));
            Assert.That(navigation.ReturnToInvestigation(), Is.False);
            Assert.That(navigation.Load(SecondId, false, out var transition, out var error), Is.True);
            Assert.That(error, Is.EqualTo(SpiritStatusKind.None));
            navigation.Commit(transition);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(navigation.SelectedJourney, Is.Null);
        }

        [Test]
        public void MissingLoadLeadRetainsTheCurrentSequenceWhileDirectPlayCanProceed()
        {
            Assert.That(navigation.SelectEncounter(first, EmptyProgress(), out _), Is.True);
            Assert.That(navigation.Load(MissingId, true, out var transition, out var error), Is.False);
            Assert.That(transition, Is.Null);
            Assert.That(error, Is.EqualTo(SpiritStatusKind.SavedEncounterHasNoLead));
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(navigation.SelectedJourney, Is.SameAs(first));
            Assert.That(navigation.BeatIndex, Is.Zero);
            Assert.That(navigation.Load(MissingId, false, out transition, out error), Is.True);
            Assert.That(error, Is.EqualTo(SpiritStatusKind.None));
            Assert.That(navigation.SelectedJourney, Is.SameAs(first));
            navigation.Commit(transition);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(navigation.SelectedJourney, Is.Null);
            Assert.That(navigation.BeatIndex, Is.EqualTo(-1));
        }

        [Test]
        public void VictoryHighlightsOnlyUnvisitedIncompleteFollowUpsWithoutWritingCompletion()
        {
            var visited = Journey("encounter.visited");
            var completed = Journey("encounter.completed");
            SyntheticUi.Set(catalog, "chapters", new[] { Chapter("chapter.all", first, second, story, visited, completed) });
            SyntheticUi.Set(first, "followUpJourneys", new[] { second, visited, completed, story });
            var visitedIds = new[] { TestEncounters.IdOf("encounter.visited") };
            var completedIds = Completed(completed);
            var progress = new Progress(true, visitedIds, completedIds);
            navigation.RecordVictory(FirstId, new Progress(false, null, null));
            Assert.That(navigation.HighlightedJourneys, Is.Empty);
            navigation.RecordVictory(FirstId, progress);
            navigation.RecordVictory(FirstId, progress);
            Assert.That(navigation.HighlightedJourneys, Is.EqualTo(new[] { second, story }));
            Assert.That(progress.VisitedEncounterIds, Is.EqualTo(visitedIds));
            Assert.That(progress.CompletedJourneyIds, Is.EqualTo(completedIds));
            AssertInvestigation();
            Assert.That(navigation.SelectEncounter(second, progress, out _), Is.True);
            Assert.That(navigation.HighlightedJourneys, Is.EqualTo(new[] { story }));
            navigation.ReturnToInvestigation();
            Assert.That(navigation.SelectEncounter(story, progress, out _), Is.True);
            Assert.That(navigation.HighlightedJourneys, Is.Empty);
            navigation.ReturnToInvestigation();
            navigation.RecordVictory(FirstId, new Progress(true, visitedIds, Completed(first, completed)));
            Assert.That(navigation.HighlightedJourneys, Is.Empty);
        }

        [Test]
        public void ChaptersRequireAnAvailableJourneyAndCompletionIncludesEveryStory()
        {
            var chapters = Chapters();
            Assert.That(navigation.SelectedChapter, Is.SameAs(chapters[0]));
            Assert.That(navigation.IsChapterAvailable(chapters[0], new Progress(false, null, null)), Is.False);
            Assert.That(navigation.IsChapterAvailable(chapters[1], EmptyProgress()), Is.False);
            // A chapter outside the catalog is neither available nor completed, though its journeys are.
            var outside = Chapter("chapter.outside", first, story);
            Assert.That(navigation.IsChapterAvailable(outside, EmptyProgress()), Is.False);
            Assert.That(navigation.IsChapterCompleted(outside, new Progress(true, null, Completed(first, story))), Is.False);
            var afterEncounter = new Progress(true, null, Completed(first));
            Assert.That(navigation.IsChapterCompleted(chapters[0], afterEncounter), Is.False);
            Assert.That(navigation.IsChapterAvailable(chapters[1], afterEncounter), Is.False);
            var afterStory = new Progress(true, null, Completed(first, story));
            Assert.That(navigation.IsChapterCompleted(chapters[0], afterStory), Is.True);
            Assert.That(navigation.IsChapterAvailable(chapters[1], afterStory), Is.True);
            Assert.That(navigation.IsChapterCompleted(chapters[1], afterStory), Is.False);
            Assert.That(navigation.IsChapterCompleted(chapters[0],
                new Progress(false, null, Completed(first, story))), Is.False);
        }

        [Test]
        public void DefaultChapterUsesLatestAvailableIncompleteThenLatestCompleted()
        {
            var chapters = Chapters();
            var progress = new Progress(true, null, Completed(first, story));
            navigation.InitializeJourney(catalog, progress);
            Assert.That(navigation.SelectedChapter, Is.SameAs(chapters[1]));
            navigation.InitializeJourney(catalog, new Progress(true, null, Completed(first, second, story)));
            Assert.That(navigation.SelectedChapter, Is.SameAs(chapters[1]));
            SyntheticUi.Set(second, "requiredCompletedJourneys", Array.Empty<EncounterJourneyAsset>());
            navigation.InitializeJourney(catalog, new Progress(true, null, Completed(first, second)));
            Assert.That(navigation.SelectedChapter, Is.SameAs(chapters[0]), "An incomplete available chapter takes precedence over completed chapters.");
        }

        [Test]
        public void ChapterSelectionIsAvailableOnlyOnTheBoardAndSurvivesBoardRefresh()
        {
            var chapters = Chapters();
            Assert.That(navigation.SelectChapter(chapters[1], EmptyProgress(), out SpiritStatusKind error), Is.False);
            Assert.That(error, Is.EqualTo(SpiritStatusKind.ChapterLocked));
            Assert.That(navigation.SelectChapter(Chapter("chapter.outside", first), EmptyProgress(), out error), Is.False);
            Assert.That(error, Is.EqualTo(SpiritStatusKind.ChapterNotFound));
            var progress = new Progress(true, null, Completed(first, story));
            navigation.InitializeJourney(catalog, progress);
            Assert.That(navigation.SelectChapter(chapters[0], progress, out error), Is.True);
            Assert.That(error, Is.EqualTo(SpiritStatusKind.None));
            navigation.ReturnToInvestigation(progress);
            Assert.That(navigation.SelectedChapter, Is.SameAs(chapters[0]));
            Assert.That(navigation.SelectEncounter(first, progress, out _), Is.True);
            Assert.That(navigation.SelectChapter(chapters[1], progress, out _), Is.False);
            Assert.That(navigation.SelectedChapter, Is.SameAs(chapters[0]));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EarlierChapterEncountersAndStoriesRemainReplayable(bool storyOnly)
        {
            var chapters = Chapters();
            var progress = new Progress(true, new[] { FirstId }, Completed(first, second, story));
            navigation.InitializeJourney(catalog, progress);
            Assert.That(navigation.SelectedChapter, Is.SameAs(chapters[1]));
            Assert.That(navigation.SelectChapter(chapters[0], progress, out _), Is.True);
            Assert.That(navigation.SelectEncounter(storyOnly ? story : first, progress, out _), Is.True);
            Assert.That(navigation.SelectedChapter, Is.SameAs(chapters[0]));
            Assert.That(navigation.SelectedJourney, Is.SameAs(storyOnly ? story : first));
            Assert.That(navigation.CanSkipIntro(progress), Is.EqualTo(!storyOnly));
        }

        [Test]
        public void StoryCompletionAndEncounterReturnRevealTheCurrentChapter()
        {
            var chapters = Chapters();
            var progress = new Progress(true, null, Completed(first));
            Assert.That(navigation.SelectEncounter(story, progress, out _), Is.True);
            Assert.That(navigation.AdvanceJourney(EncounterOutcome.None, out var beat), Is.True);
            navigation.Commit(beat);
            Assert.That(navigation.AdvanceJourney(EncounterOutcome.None, out var completion), Is.True);
            var afterStory = new Progress(true, null, Completed(first, story));
            navigation.Commit(completion, afterStory);
            Assert.That(navigation.SelectedChapter, Is.SameAs(chapters[1]));
            AssertInvestigation();
            Assert.That(navigation.SelectEncounter(first, afterStory, out _), Is.True);
            navigation.ReturnToInvestigation(afterStory);
            Assert.That(navigation.SelectedChapter, Is.SameAs(chapters[1]));
        }

        [Test]
        public void LoadAndResumeSelectTheActiveJourneysChapter()
        {
            var chapters = Chapters();
            Assert.That(navigation.Load(SecondId, true, out var load, out _), Is.True);
            Assert.That(navigation.SelectedChapter, Is.SameAs(chapters[0]), "A load proposal must not change chapter selection.");
            navigation.Commit(load);
            Assert.That(navigation.SelectedChapter, Is.SameAs(chapters[1]));
            navigation.ReturnToInvestigation(EmptyProgress());
            Assert.That(navigation.SelectedChapter, Is.SameAs(chapters[0]));
            Assert.That(navigation.ResumeEncounter(SecondId, true, out _), Is.True);
            Assert.That(navigation.SelectedChapter, Is.SameAs(chapters[1]));
            navigation.Initialize(catalog, FirstId);
            Assert.That(navigation.SelectedChapter, Is.SameAs(chapters[0]));
        }

        [Test]
        public void ReinitializationReplacesChapterSelection()
        {
            var chapters = Chapters();
            var replacement = Create<EncounterJourneyCatalogAsset>();
            var replacementChapter = Chapter("chapter.replacement", first);
            SyntheticUi.Set(replacement, "chapters", new[] { replacementChapter });
            navigation.InitializeJourney(replacement, EmptyProgress());
            Assert.That(navigation.SelectedChapter, Is.SameAs(replacementChapter));
            Assert.That(navigation.SelectChapter(chapters[0], EmptyProgress(), out _), Is.False);
            Assert.That(navigation.SelectEncounter(first, EmptyProgress(), out _), Is.True);
            Assert.That(navigation.SelectedChapter, Is.SameAs(replacementChapter));
            navigation.InitializeJourney(catalog, EmptyProgress());
            Assert.That(navigation.SelectedChapter, Is.SameAs(chapters[0]));
            navigation.Initialize(null, FirstId);
            Assert.That(navigation.SelectedChapter, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExistingProgressCanKeepALaterChapterAvailable(bool completed)
        {
            var chapters = Chapters();
            var progress = new Progress(true, completed ? null : new[] { SecondId }, completed ? Completed(second) : null);
            Assert.That(navigation.IsChapterAvailable(chapters[1], progress), Is.True);
            navigation.InitializeJourney(catalog, progress);
            Assert.That(navigation.SelectedChapter, Is.SameAs(completed ? chapters[0] : chapters[1]));
        }

        private EncounterChapterAsset[] Chapters()
        {
            SyntheticUi.Set(story, "requiredCompletedJourneys", new[] { first });
            SyntheticUi.Set(second, "requiredCompletedJourneys", new[] { story });
            var chapters = new[] { Chapter("chapter.first", first, story), Chapter("chapter.second", second) };
            SyntheticUi.Set(catalog, "chapters", chapters);
            navigation.InitializeJourney(catalog, EmptyProgress());
            return chapters;
        }

        /** A catalog journey whose Encounter beat, of the onward encounter, is followed by a dialogue beat. */
        private EncounterJourneyAsset OnwardJourney()
        {
            var journey = TestJourneys.Identify(Create<EncounterJourneyAsset>(), "journey.onward");
            TestJourneys.SetBeats(journey, Create<EncounterTablePresentationAsset>(), new DialogueBeat(), new InvitationBeat(),
                new EncounterBeat(), new DialogueBeat());
            TestJourneys.SetBeat(journey, 2, "encounter", content.Encounter("encounter.onward"));
            for (int beat = 0; beat < journey.Beats.Count; ++beat)
                TestJourneys.SetBeat(journey, beat, "progressionReward", Create<ProgressionRewardAsset>());
            SyntheticUi.Set(journey, "completionReward", Create<ProgressionRewardAsset>());
            SyntheticUi.Set(catalog, "chapters", new[] { Chapter("chapter.all", first, second, story, journey) });
            navigation.InitializeJourney(catalog);
            return journey;
        }

        private EncounterChapterAsset Chapter(string title, params EncounterJourneyAsset[] entries)
            => TestJourneys.Chapter(Create<EncounterChapterAsset>(), title, entries);

        private void ReachInvitation()
        {
            while (navigation.SelectedJourney.EncounterBeatAt(navigation.BeatIndex + 1) == null)
            {
                Assert.That(navigation.AdvanceJourney(EncounterOutcome.None, out var transition), Is.True);
                navigation.Commit(transition);
            }
        }

        private void AssertInvestigation()
        {
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(navigation.SelectedJourney, Is.Null);
            Assert.That(navigation.BeatIndex, Is.EqualTo(-1));
        }

        private static Progress EmptyProgress() => new Progress(true, null, null);

        private static EncounterJourneyAsset.Id[] Completed(params EncounterJourneyAsset[] journeys)
            => journeys.Select(journey => journey.Identity).ToArray();

        /**
         * A journey identified by the name: two dialogue beats without an encounter, or an intro, an invitation and the
         * Encounter beat of a new encounter asset of the name.
         */
        private EncounterJourneyAsset Journey(string name, bool storyOnly = false)
        {
            var journey = TestJourneys.Identify(Create<EncounterJourneyAsset>(), name);
            var table = Create<EncounterTablePresentationAsset>();
            if (storyOnly) TestJourneys.SetBeats(journey, table, new DialogueBeat(), new DialogueBeat());
            else TestJourneys.SetEncounterBeats(journey, table, content.Encounter(name), new IllustrationBeat(), new DialogueBeat());
            for (int beat = 0; beat < journey.Beats.Count; ++beat)
                TestJourneys.SetBeat(journey, beat, "progressionReward", Create<ProgressionRewardAsset>());
            SyntheticUi.Set(journey, "completionReward", Create<ProgressionRewardAsset>());
            return journey;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }
    }
}
