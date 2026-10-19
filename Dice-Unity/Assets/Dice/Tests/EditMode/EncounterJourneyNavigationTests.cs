using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private const string FirstId = "encounter.harbor";
        private const string SecondId = "encounter.observatory";
        private const string StoryId = "story.return";
        private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        private EncounterJourneyNavigation navigation;
        private EncounterJourneyCatalogAsset catalog;
        private EncounterJourneyAsset first;
        private EncounterJourneyAsset second;
        private EncounterJourneyAsset story;

        [SetUp]
        public void SetUp()
        {
            navigation = new EncounterJourneyNavigation();
            first = Journey(FirstId);
            second = Journey(SecondId);
            story = Journey(StoryId, true);
            catalog = Create<EncounterJourneyCatalogAsset>();
            Set(catalog, "entries", new[] { first, second, story });
            navigation.InitializeJourney(catalog);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();
        }

        [Test]
        public void AvailabilityRequiresAProfileAndEveryPrerequisite()
        {
            Set(second, "requiredCompletedEncounterIds", new[] { FirstId });
            Set(second, "requiredCompletedStoryIds", new[] { StoryId });
            Assert.That(navigation.IsJourneyAvailable(SecondId, new Progress(false, null, new[] { FirstId }, new[] { StoryId })), Is.False);
            Assert.That(navigation.IsJourneyAvailable(SecondId, new Progress(true, null, new[] { FirstId }, null)), Is.False);
            Assert.That(navigation.IsJourneyAvailable(SecondId, new Progress(true, null, null, new[] { StoryId })), Is.False);
            Assert.That(navigation.IsJourneyAvailable(SecondId, new Progress(true, null, new[] { FirstId }, new[] { StoryId })), Is.True);
            Assert.That(navigation.SelectEncounter(SecondId, EmptyProgress(), out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
            AssertInvestigation();
            Assert.That(navigation.IsJourneyAvailable("encounter.missing", EmptyProgress()), Is.False);
        }

        [TestCase("visited")]
        [TestCase("encounter-completed")]
        [TestCase("story-completed")]
        public void ExistingProgressKeepsALeadAvailableWhenItsPrerequisitesAreMissing(string progressKind)
        {
            var selected = progressKind == "story-completed" ? story : second;
            Set(selected, "requiredCompletedEncounterIds", new[] { FirstId });
            var progress = new Progress(true,
                progressKind == "visited" ? new[] { SecondId } : null,
                progressKind == "encounter-completed" ? new[] { SecondId } : null,
                progressKind == "story-completed" ? new[] { StoryId } : null);
            Assert.That(navigation.IsJourneyAvailable(selected.Id, progress), Is.True);
            Assert.That(navigation.SelectEncounter(selected.Id, progress, out _), Is.True);
            Assert.That(navigation.SelectedJourney, Is.SameAs(selected));
            Assert.That(navigation.BeatIndex, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FirstVisitsAndStoriesCannotSkipTheSequence(bool storyOnly)
        {
            var selected = storyOnly ? story : first;
            var progress = new Progress(true, storyOnly ? new[] { selected.Id } : null, null, null);
            Assert.That(navigation.SelectEncounter(selected.Id, progress, out _), Is.True);
            Assert.That(navigation.CanSkipIntro(progress), Is.False);
            Assert.That(navigation.SkipIntro(progress, out var transition), Is.False);
            Assert.That(transition, Is.Null);
            Assert.That(navigation.BeatIndex, Is.Zero);
        }

        [Test]
        public void RepeatedVisitSkipProposesEveryIntroRewardAndWaitsAtTheInvitation()
        {
            var progress = new Progress(true, new[] { FirstId }, null, null);
            Assert.That(navigation.SelectEncounter(FirstId, progress, out _), Is.True);
            Assert.That(navigation.CanSkipIntro(new Progress(false, new[] { FirstId }, null, null)), Is.False);
            Assert.That(navigation.SkipIntro(progress, out var transition), Is.True);
            Assert.That(transition.Rewards, Is.EqualTo(first.Beats.Take(first.Beats.Count - 1).Select(beat => beat.ProgressionReward)));
            Assert.That(navigation.BeatIndex, Is.Zero);
            navigation.Commit(transition);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(navigation.BeatIndex, Is.EqualTo(first.Beats.Count - 1));
            Assert.That(navigation.CanSkipIntro(progress), Is.False);
            Assert.That(navigation.AdvanceJourney(out _), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdvanceAndAcceptCanBeRetriedAfterAnUncommittedSideEffect(bool accepting)
        {
            Assert.That(navigation.SelectEncounter(FirstId, EmptyProgress(), out _), Is.True);
            if (accepting) ReachInvitation();
            int before = navigation.BeatIndex;
            bool proposed = accepting ? navigation.AcceptEncounter(out var transition) : navigation.AdvanceJourney(out transition);
            Assert.That(proposed, Is.True);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(navigation.SelectedJourney, Is.SameAs(first));
            Assert.That(navigation.BeatIndex, Is.EqualTo(before));
            Assert.That(transition.Rewards, Is.EqualTo(first.Beats.Take(before + 1).Select(beat => beat.ProgressionReward)));

            bool retried = accepting ? navigation.AcceptEncounter(out var retry) : navigation.AdvanceJourney(out retry);
            Assert.That(retried, Is.True);
            Assert.That(retry.Journey, Is.SameAs(transition.Journey));
            Assert.That(retry.Phase, Is.EqualTo(transition.Phase));
            Assert.That(retry.BeatIndex, Is.EqualTo(transition.BeatIndex));
            Assert.That(retry.Rewards, Is.EqualTo(transition.Rewards));
            navigation.Commit(retry);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(accepting ? EncounterJourneyPhase.Playing : EncounterJourneyPhase.Sequence));
            Assert.That(navigation.BeatIndex, Is.EqualTo(accepting ? -1 : before + 1));
        }

        [Test]
        public void AutomaticBeatsNeverAcceptTheFinalInvitation()
        {
            foreach (var beat in first.Beats) Set(beat, "autoAdvance", true);
            Assert.That(navigation.SelectEncounter(FirstId, EmptyProgress(), out _), Is.True);
            Assert.That(navigation.AcceptEncounter(out _), Is.False);
            ReachInvitation();
            Assert.That(navigation.AdvanceJourney(out var transition), Is.False);
            Assert.That(transition, Is.Null);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(navigation.BeatIndex, Is.EqualTo(first.Beats.Count - 1));
            Assert.That(navigation.AcceptEncounter(out transition), Is.True);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            navigation.Commit(transition);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
        }

        [Test]
        public void StoryCompletionProposesAllRewardsBeforeReturningToInvestigation()
        {
            Assert.That(navigation.SelectEncounter(StoryId, EmptyProgress(), out _), Is.True);
            Assert.That(navigation.AcceptEncounter(out _), Is.False);
            Assert.That(navigation.AdvanceJourney(out var advance), Is.True);
            navigation.Commit(advance);
            Assert.That(navigation.AdvanceJourney(out var completion), Is.True);
            Assert.That(completion.CompletesStory, Is.True);
            Assert.That(completion.Phase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(completion.Rewards, Is.EqualTo(story.Beats.Select(beat => beat.ProgressionReward).Concat(new[] { story.CompletionReward })));
            Assert.That(navigation.SelectedJourney, Is.SameAs(story));
            Assert.That(navigation.BeatIndex, Is.EqualTo(story.Beats.Count - 1));
            Assert.That(navigation.AdvanceJourney(out var retry), Is.True);
            Assert.That(retry.Rewards, Is.EqualTo(completion.Rewards));
            navigation.Commit(retry);
            AssertInvestigation();
        }

        [Test]
        public void ResumeUsesTheActiveEncounterAfterAnotherLeadWasSelectedAndCancelled()
        {
            navigation.Initialize(catalog, FirstId);
            Assert.That(navigation.ReturnToInvestigation(), Is.True);
            Assert.That(navigation.SelectEncounter(SecondId, EmptyProgress(), out _), Is.True);
            Assert.That(navigation.ResumeEncounter(FirstId, true, out _), Is.False);
            Assert.That(navigation.SelectedJourney, Is.SameAs(second));
            Assert.That(navigation.ReturnToInvestigation(), Is.True);
            Assert.That(navigation.ResumeEncounter(FirstId, false, out _), Is.False);
            Assert.That(navigation.ResumeEncounter("encounter.missing", true, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
            AssertInvestigation();
            Assert.That(navigation.ResumeEncounter(FirstId, true, out error), Is.True);
            Assert.That(error, Is.Null);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(navigation.SelectedJourney, Is.SameAs(first));
            Assert.That(navigation.BeatIndex, Is.EqualTo(-1));
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
                Assert.That(navigation.BeatIndex, Is.EqualTo(-1));
                return;
            }
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(navigation.BeatIndex, Is.EqualTo(first.Beats.Count - 1));
            Assert.That(navigation.AdvanceJourney(out _), Is.False);
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
            Assert.That(error, Is.Null);
            navigation.Commit(transition);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(navigation.SelectedJourney, Is.Null);
        }

        [Test]
        public void MissingLoadLeadRetainsTheCurrentSequenceWhileDirectPlayCanProceed()
        {
            Assert.That(navigation.SelectEncounter(FirstId, EmptyProgress(), out _), Is.True);
            Assert.That(navigation.Load("encounter.missing", true, out var transition, out var error), Is.False);
            Assert.That(transition, Is.Null);
            Assert.That(error, Is.Not.Empty);
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(navigation.SelectedJourney, Is.SameAs(first));
            Assert.That(navigation.BeatIndex, Is.Zero);
            Assert.That(navigation.Load("encounter.missing", false, out transition, out error), Is.True);
            Assert.That(error, Is.Null);
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
            Set(catalog, "entries", new[] { first, second, story, visited, completed });
            Set(first, "followUpEncounterIds", new[] { SecondId, visited.Id, completed.Id });
            var visitedIds = new[] { visited.Id };
            var completedIds = new[] { completed.Id };
            var progress = new Progress(true, visitedIds, completedIds, null);
            navigation.RecordVictory(FirstId, new Progress(false, null, null, null));
            Assert.That(navigation.HighlightedEncounterIds, Is.Empty);
            navigation.RecordVictory(FirstId, progress);
            navigation.RecordVictory(FirstId, progress);
            Assert.That(navigation.HighlightedEncounterIds, Is.EqualTo(new[] { SecondId }));
            Assert.That(progress.VisitedEncounterIds, Is.EqualTo(new[] { visited.Id }));
            Assert.That(progress.CompletedEncounterIds, Is.EqualTo(new[] { completed.Id }));
            AssertInvestigation();
            Assert.That(navigation.SelectEncounter(SecondId, progress, out _), Is.True);
            Assert.That(navigation.HighlightedEncounterIds, Is.Empty);
            navigation.ReturnToInvestigation();
            navigation.RecordVictory(FirstId, new Progress(true, visitedIds, new[] { FirstId, completed.Id }, null));
            Assert.That(navigation.HighlightedEncounterIds, Is.Empty);
        }

        private void ReachInvitation()
        {
            while (navigation.BeatIndex < navigation.SelectedJourney.Beats.Count - 1)
            {
                Assert.That(navigation.AdvanceJourney(out var transition), Is.True);
                navigation.Commit(transition);
            }
        }

        private void AssertInvestigation()
        {
            Assert.That(navigation.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(navigation.SelectedJourney, Is.Null);
            Assert.That(navigation.BeatIndex, Is.EqualTo(-1));
        }

        private static Progress EmptyProgress() => new Progress(true, null, null, null);

        private EncounterJourneyAsset Journey(string id, bool storyOnly = false)
        {
            var journey = Create<EncounterJourneyAsset>();
            Set(journey, storyOnly ? "storyId" : "encounterId", id);
            Set(journey, "beats", storyOnly
                ? new[] { Beat("report", EncounterJourneyBeatKind.Dialogue), Beat("prepare", EncounterJourneyBeatKind.Dialogue) }
                : new[] { Beat("approach", EncounterJourneyBeatKind.Illustration), Beat("bargain", EncounterJourneyBeatKind.Dialogue),
                    Beat("invitation", EncounterJourneyBeatKind.Invitation) });
            Set(journey, "completionReward", Create<ProgressionRewardAsset>());
            return journey;
        }

        private EncounterJourneyBeat Beat(string id, EncounterJourneyBeatKind kind)
        {
            var beat = new EncounterJourneyBeat();
            Set(beat, "id", id);
            Set(beat, "kind", kind);
            Set(beat, "progressionReward", Create<ProgressionRewardAsset>());
            return beat;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }

        private static void Set(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
