using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;

namespace Dice.App
{
    /** Owns journey location and transition rules, without changing profile progress or the live encounter. */
    public sealed class EncounterJourneyNavigation
    {
        private readonly List<string> highlightedEncounterIds = new List<string>();

        public EncounterJourneyCatalogAsset JourneyCatalog { get; private set; }
        public EncounterChapterAsset SelectedChapter { get; private set; }
        public EncounterJourneyPhase JourneyPhase { get; private set; } = EncounterJourneyPhase.Playing;
        public EncounterJourneyAsset SelectedJourney { get; private set; }
        public int BeatIndex { get; private set; } = -1;
        public IReadOnlyList<string> HighlightedEncounterIds { get; }

        public EncounterJourneyNavigation()
        {
            HighlightedEncounterIds = highlightedEncounterIds.AsReadOnly();
        }

        public void Initialize(EncounterJourneyCatalogAsset catalog, string encounterId)
        {
            JourneyCatalog = catalog;
            SelectedChapter = null;
            highlightedEncounterIds.Clear();
            Commit(new Transition(catalog?.Find(encounterId), EncounterJourneyPhase.Playing, -1));
        }

        public void InitializeJourney(EncounterJourneyCatalogAsset catalog, Progress progress = default)
        {
            JourneyCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            highlightedEncounterIds.Clear();
            SelectedChapter = CurrentChapter(progress);
            Commit(new Transition(null, EncounterJourneyPhase.Investigation, -1));
        }

        public bool IsChapterAvailable(string id, Progress progress)
        {
            var chapter = FindChapter(id);
            return chapter != null && chapter.Entries.Any(journey => journey != null && IsJourneyAvailable(journey.Id, progress));
        }

        public void ShowMainMenu()
        {
            Commit(new Transition(null, EncounterJourneyPhase.MainMenu, -1));
        }

        public bool IsChapterCompleted(string id, Progress progress)
        {
            var chapter = FindChapter(id);
            return chapter != null && progress.HasProfile && chapter.Entries.Count > 0 && chapter.Entries.All(journey =>
                journey != null && (journey.IsStoryOnly ? progress.CompletedStoryIds.Contains(journey.StoryId) :
                    progress.CompletedEncounterIds.Contains(journey.EncounterId)));
        }

        public bool SelectChapter(string id, Progress progress, out string error)
        {
            error = null;
            if (JourneyPhase != EncounterJourneyPhase.Investigation || JourneyCatalog == null) return false;
            var chapter = FindChapter(id);
            if (chapter == null) { error = "The chapter does not exist in the investigation."; return false; }
            if (!IsChapterAvailable(id, progress)) { error = "Follow the current lead before opening this chapter."; return false; }
            SelectedChapter = chapter;
            return true;
        }

        private EncounterChapterAsset FindChapter(string id) => JourneyCatalog?.Chapters?.FirstOrDefault(chapter => chapter != null && chapter.Id == id);

        private EncounterChapterAsset CurrentChapter(Progress progress)
        {
            EncounterChapterAsset latest = null;
            EncounterChapterAsset incomplete = null;
            if (JourneyCatalog?.Chapters == null) return null;
            foreach (var chapter in JourneyCatalog.Chapters)
            {
                if (chapter == null || !IsChapterAvailable(chapter.Id, progress)) continue;
                latest = chapter;
                if (!IsChapterCompleted(chapter.Id, progress)) incomplete = chapter;
            }
            return incomplete ?? latest;
        }

        public bool IsJourneyAvailable(string id, Progress progress)
        {
            var journey = JourneyCatalog?.Find(id);
            if (journey == null || !progress.HasProfile) return false;
            if (journey.IsStoryOnly ? progress.CompletedStoryIds.Contains(journey.StoryId) :
                progress.CompletedEncounterIds.Contains(journey.EncounterId) || progress.VisitedEncounterIds.Contains(journey.EncounterId)) return true;
            return journey.RequiredCompletedEncounterIds.All(progress.CompletedEncounterIds.Contains) &&
                journey.RequiredCompletedStoryIds.All(progress.CompletedStoryIds.Contains);
        }

        public bool SelectEncounter(string id, Progress progress, out string error)
        {
            error = null;
            if (JourneyCatalog == null || JourneyPhase != EncounterJourneyPhase.Investigation) return false;
            var selected = JourneyCatalog.Find(id);
            if (selected == null) { error = "The encounter has no investigation lead."; return false; }
            if (!IsJourneyAvailable(id, progress)) { error = "Follow the current lead before visiting this location."; return false; }
            highlightedEncounterIds.Remove(id);
            Commit(new Transition(selected, EncounterJourneyPhase.Sequence, 0));
            return true;
        }

        public bool CanSkipIntro(Progress progress) => JourneyPhase == EncounterJourneyPhase.Sequence &&
            SelectedJourney != null && !SelectedJourney.IsStoryOnly && BeatIndex < SelectedJourney.Beats.Count - 1 &&
            progress.HasProfile && progress.VisitedEncounterIds.Contains(SelectedJourney.EncounterId);

        public bool CanResume(bool hasActiveEncounter) => hasActiveEncounter && JourneyPhase == EncounterJourneyPhase.Investigation;

        public bool AdvanceJourney(out Transition transition)
        {
            transition = null;
            if (JourneyPhase != EncounterJourneyPhase.Sequence || SelectedJourney == null) return false;
            if (BeatIndex == SelectedJourney.Beats.Count - 1)
            {
                if (!SelectedJourney.IsStoryOnly) return false;
                transition = new Transition(SelectedJourney, EncounterJourneyPhase.Investigation, -1, BeatIndex);
            }
            else transition = new Transition(SelectedJourney, EncounterJourneyPhase.Sequence, BeatIndex + 1, BeatIndex);
            return true;
        }

        public bool SkipIntro(Progress progress, out Transition transition)
        {
            transition = null;
            if (!CanSkipIntro(progress)) return false;
            int last = SelectedJourney.Beats.Count - 1;
            transition = new Transition(SelectedJourney, EncounterJourneyPhase.Sequence, last, last - 1);
            return true;
        }

        public bool AcceptEncounter(out Transition transition)
        {
            transition = null;
            if (JourneyPhase != EncounterJourneyPhase.Sequence || SelectedJourney == null || SelectedJourney.IsStoryOnly ||
                SelectedJourney.Beats[BeatIndex].Kind != EncounterJourneyBeatKind.Invitation) return false;
            transition = new Transition(SelectedJourney, EncounterJourneyPhase.Playing, -1, BeatIndex);
            return true;
        }

        public bool Load(string encounterId, bool requireLead, out Transition transition, out string error)
        {
            transition = null;
            error = null;
            var journey = JourneyCatalog?.Find(encounterId);
            if (requireLead && journey == null)
            {
                error = "Cannot load the encounter: no investigation lead exists for the saved encounter.";
                return false;
            }
            transition = new Transition(journey, EncounterJourneyPhase.Playing, -1);
            return true;
        }

        public bool ReturnToInvestigation(Progress? progress = null)
        {
            if (JourneyCatalog == null) return false;
            if (JourneyPhase != EncounterJourneyPhase.Investigation && progress.HasValue)
                SelectedChapter = CurrentChapter(progress.Value);
            Commit(new Transition(null, EncounterJourneyPhase.Investigation, -1));
            return true;
        }

        public bool ResumeEncounter(string encounterId, bool hasActiveEncounter, out string error)
        {
            error = null;
            if (!CanResume(hasActiveEncounter)) return false;
            var journey = JourneyCatalog?.Find(encounterId);
            if (journey == null) { error = "The active encounter has no investigation lead."; return false; }
            Commit(new Transition(journey, EncounterJourneyPhase.Playing, -1));
            return true;
        }

        public bool RetryEncounter(string encounterId, EncounterOutcome outcome)
        {
            if (JourneyPhase != EncounterJourneyPhase.Playing || outcome == EncounterOutcome.None) return false;
            var journey = JourneyCatalog?.Find(encounterId);
            if (journey == null) return false;
            Commit(new Transition(journey, EncounterJourneyPhase.Sequence, journey.Beats.Count - 1));
            return true;
        }

        public void RecordVictory(string encounterId, Progress progress)
        {
            var journey = JourneyCatalog?.Find(encounterId);
            if (!progress.HasProfile || progress.CompletedEncounterIds.Contains(encounterId) || journey == null) return;
            foreach (string id in journey.FollowUpEncounterIds)
                if (!progress.VisitedEncounterIds.Contains(id) && !progress.CompletedEncounterIds.Contains(id) &&
                    !highlightedEncounterIds.Contains(id)) highlightedEncounterIds.Add(id);
        }

        // The controller completes required writes and run preparation before committing, then notifies observers.
        public void Commit(Transition transition, Progress? progress = null)
        {
            if (transition == null) throw new ArgumentNullException(nameof(transition));
            if (transition.Phase != EncounterJourneyPhase.Investigation)
                SelectedChapter = JourneyCatalog?.Chapters?.FirstOrDefault(chapter => chapter != null && chapter.Entries.Contains(transition.Journey));
            else if (transition.CompletesStory && progress.HasValue) SelectedChapter = CurrentChapter(progress.Value);
            JourneyPhase = transition.Phase;
            SelectedJourney = transition.Phase == EncounterJourneyPhase.Investigation ? null : transition.Journey;
            BeatIndex = transition.BeatIndex;
        }

        /** Read-only facts supplied for a navigation decision; the navigator does not retain the profile. */
        public readonly struct Progress
        {
            public bool HasProfile { get; }
            public IReadOnlyList<string> VisitedEncounterIds { get; }
            public IReadOnlyList<string> CompletedEncounterIds { get; }
            public IReadOnlyList<string> CompletedStoryIds { get; }

            public Progress(bool hasProfile, IReadOnlyList<string> visitedEncounterIds,
                IReadOnlyList<string> completedEncounterIds, IReadOnlyList<string> completedStoryIds)
            {
                HasProfile = hasProfile;
                VisitedEncounterIds = visitedEncounterIds ?? Array.Empty<string>();
                CompletedEncounterIds = completedEncounterIds ?? Array.Empty<string>();
                CompletedStoryIds = completedStoryIds ?? Array.Empty<string>();
            }
        }

        /** A synchronous transition proposal; creating one leaves navigation unchanged until Commit succeeds. */
        public sealed class Transition
        {
            private readonly int rewardThroughBeat;
            public EncounterJourneyAsset Journey { get; }
            public EncounterJourneyPhase Phase { get; }
            public int BeatIndex { get; }
            public bool CompletesStory => Phase == EncounterJourneyPhase.Investigation && Journey?.IsStoryOnly == true;

            internal Transition(EncounterJourneyAsset journey, EncounterJourneyPhase phase, int beatIndex, int rewardThroughBeat = -1)
            {
                Journey = journey;
                Phase = phase;
                BeatIndex = beatIndex;
                this.rewardThroughBeat = rewardThroughBeat;
            }

            public IEnumerable<ProgressionRewardAsset> Rewards
            {
                get
                {
                    // Revisit earlier beats for skipped sequences and retries; grants are idempotent.
                    for (int index = 0; index <= rewardThroughBeat; ++index)
                        if (Journey.Beats[index].ProgressionReward != null) yield return Journey.Beats[index].ProgressionReward;
                    if (CompletesStory && Journey.CompletionReward != null) yield return Journey.CompletionReward;
                }
            }
        }
    }
}
