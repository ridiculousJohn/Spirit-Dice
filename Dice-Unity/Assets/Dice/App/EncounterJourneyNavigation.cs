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
        private readonly List<EncounterJourneyAsset> highlightedJourneys = new List<EncounterJourneyAsset>();

        public EncounterJourneyCatalogAsset JourneyCatalog { get; private set; }
        public EncounterChapterAsset SelectedChapter { get; private set; }
        public EncounterJourneyPhase JourneyPhase { get; private set; } = EncounterJourneyPhase.Playing;
        public EncounterJourneyAsset SelectedJourney { get; private set; }
        public int BeatIndex { get; private set; } = -1;
        /** The follow-up leads of won encounters that the board highlights until they are selected. */
        public IReadOnlyList<EncounterJourneyAsset> HighlightedJourneys { get; }

        public EncounterJourneyNavigation()
        {
            HighlightedJourneys = highlightedJourneys.AsReadOnly();
        }

        public void Initialize(EncounterJourneyCatalogAsset catalog, EncounterDefinition.Id encounterId)
        {
            JourneyCatalog = catalog;
            SelectedChapter = null;
            highlightedJourneys.Clear();
            Commit(new Transition(catalog?.Find(encounterId), EncounterJourneyPhase.Playing, -1));
        }

        public void InitializeJourney(EncounterJourneyCatalogAsset catalog, Progress progress = default)
        {
            JourneyCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            highlightedJourneys.Clear();
            SelectedChapter = CurrentChapter(progress);
            Commit(new Transition(null, EncounterJourneyPhase.Investigation, -1));
        }

        public bool IsChapterAvailable(string id, Progress progress)
        {
            var chapter = FindChapter(id);
            return chapter != null && chapter.Entries.Any(journey => journey != null && IsJourneyAvailable(journey, progress));
        }

        public void ShowMainMenu()
        {
            Commit(new Transition(null, EncounterJourneyPhase.MainMenu, -1));
        }

        public bool IsChapterCompleted(string id, Progress progress)
        {
            var chapter = FindChapter(id);
            return chapter != null && progress.HasProfile && chapter.Entries.Count > 0 && chapter.Entries.All(journey =>
                journey != null && IsJourneyCompleted(journey, progress));
        }

        private static bool IsJourneyCompleted(EncounterJourneyAsset journey, Progress progress)
            => progress.CompletedJourneyIds.Contains(journey.Identity);

        private static bool IsJourneyVisited(EncounterJourneyAsset journey, Progress progress)
            => !journey.IsStoryOnly && progress.VisitedEncounterIds.Contains(journey.Encounter.Identity);

        public bool SelectChapter(string id, Progress progress, out SpiritStatusKind error)
        {
            error = SpiritStatusKind.None;
            if (JourneyPhase != EncounterJourneyPhase.Investigation || JourneyCatalog == null) return false;
            var chapter = FindChapter(id);
            if (chapter == null) { error = SpiritStatusKind.ChapterNotFound; return false; }
            if (!IsChapterAvailable(id, progress)) { error = SpiritStatusKind.ChapterLocked; return false; }
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

        public bool IsJourneyAvailable(EncounterJourneyAsset journey, Progress progress)
        {
            if (!IsCatalogJourney(journey) || !progress.HasProfile) return false;
            if (IsJourneyCompleted(journey, progress) || IsJourneyVisited(journey, progress)) return true;
            return journey.RequiredCompletedJourneys.All(required => IsJourneyCompleted(required, progress));
        }

        public bool SelectEncounter(EncounterJourneyAsset journey, Progress progress, out SpiritStatusKind error)
        {
            error = SpiritStatusKind.None;
            if (JourneyCatalog == null || JourneyPhase != EncounterJourneyPhase.Investigation) return false;
            if (!IsCatalogJourney(journey)) { error = SpiritStatusKind.LeadNotFound; return false; }
            if (!IsJourneyAvailable(journey, progress)) { error = SpiritStatusKind.LocationLocked; return false; }
            highlightedJourneys.Remove(journey);
            Commit(new Transition(journey, EncounterJourneyPhase.Sequence, 0));
            return true;
        }

        private bool IsCatalogJourney(EncounterJourneyAsset journey)
            => journey != null && JourneyCatalog != null && JourneyCatalog.Entries.Contains(journey);

        public bool CanSkipIntro(Progress progress) => JourneyPhase == EncounterJourneyPhase.Sequence &&
            SelectedJourney != null && !SelectedJourney.IsStoryOnly && BeatIndex < SelectedJourney.Beats.Count - 1 &&
            progress.HasProfile && progress.VisitedEncounterIds.Contains(SelectedJourney.Encounter.Identity);

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

        public bool Load(EncounterDefinition.Id encounterId, bool requireLead, out Transition transition, out SpiritStatusKind error)
        {
            transition = null;
            error = SpiritStatusKind.None;
            var journey = JourneyCatalog?.Find(encounterId);
            if (requireLead && journey == null)
            {
                error = SpiritStatusKind.SavedEncounterHasNoLead;
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

        public bool ResumeEncounter(EncounterDefinition.Id? encounterId, bool hasActiveEncounter, out SpiritStatusKind error)
        {
            error = SpiritStatusKind.None;
            if (!CanResume(hasActiveEncounter)) return false;
            var journey = encounterId.HasValue ? JourneyCatalog?.Find(encounterId.Value) : null;
            if (journey == null) { error = SpiritStatusKind.ActiveEncounterHasNoLead; return false; }
            Commit(new Transition(journey, EncounterJourneyPhase.Playing, -1));
            return true;
        }

        public bool RetryEncounter(EncounterDefinition.Id encounterId, EncounterOutcome outcome)
        {
            if (JourneyPhase != EncounterJourneyPhase.Playing || outcome == EncounterOutcome.None) return false;
            var journey = JourneyCatalog?.Find(encounterId);
            if (journey == null) return false;
            Commit(new Transition(journey, EncounterJourneyPhase.Sequence, journey.Beats.Count - 1));
            return true;
        }

        public void RecordVictory(EncounterDefinition.Id encounterId, Progress progress)
        {
            var journey = JourneyCatalog?.Find(encounterId);
            if (!progress.HasProfile || journey == null || IsJourneyCompleted(journey, progress)) return;
            foreach (var followUp in journey.FollowUpJourneys)
                if (!IsJourneyVisited(followUp, progress) && !IsJourneyCompleted(followUp, progress) &&
                    !highlightedJourneys.Contains(followUp)) highlightedJourneys.Add(followUp);
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
            public IReadOnlyList<EncounterDefinition.Id> VisitedEncounterIds { get; }
            public IReadOnlyList<EncounterJourneyAsset.Id> CompletedJourneyIds { get; }

            public Progress(bool hasProfile, IReadOnlyList<EncounterDefinition.Id> visitedEncounterIds,
                IReadOnlyList<EncounterJourneyAsset.Id> completedJourneyIds)
            {
                HasProfile = hasProfile;
                VisitedEncounterIds = visitedEncounterIds ?? Array.Empty<EncounterDefinition.Id>();
                CompletedJourneyIds = completedJourneyIds ?? Array.Empty<EncounterJourneyAsset.Id>();
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
