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
            Commit(Playing(catalog?.Find(encounterId), encounterId));
        }

        public void InitializeJourney(EncounterJourneyCatalogAsset catalog, Progress progress = default)
        {
            JourneyCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            highlightedJourneys.Clear();
            SelectedChapter = CurrentChapter(progress);
            Commit(new Transition(null, EncounterJourneyPhase.Investigation, -1));
        }

        public bool IsChapterAvailable(EncounterChapterAsset chapter, Progress progress)
        {
            return IsCatalogChapter(chapter) && chapter.Entries.Any(journey => journey != null && IsJourneyAvailable(journey, progress));
        }

        public void ShowMainMenu()
        {
            Commit(new Transition(null, EncounterJourneyPhase.MainMenu, -1));
        }

        public bool IsChapterCompleted(EncounterChapterAsset chapter, Progress progress)
        {
            return IsCatalogChapter(chapter) && progress.HasProfile && chapter.Entries.Count > 0 && chapter.Entries.All(journey =>
                journey != null && IsJourneyCompleted(journey, progress));
        }

        private static bool IsJourneyCompleted(EncounterJourneyAsset journey, Progress progress)
            => progress.CompletedJourneyIds.Contains(journey.Identity);

        private static bool IsJourneyVisited(EncounterJourneyAsset journey, Progress progress)
            => journey.Encounters.Any(encounter => progress.VisitedEncounterIds.Contains(encounter.Identity));

        public bool SelectChapter(EncounterChapterAsset chapter, Progress progress, out SpiritStatusKind error)
        {
            error = SpiritStatusKind.None;
            if (JourneyPhase != EncounterJourneyPhase.Investigation || JourneyCatalog == null) return false;
            if (!IsCatalogChapter(chapter)) { error = SpiritStatusKind.ChapterNotFound; return false; }
            if (!IsChapterAvailable(chapter, progress)) { error = SpiritStatusKind.ChapterLocked; return false; }
            SelectedChapter = chapter;
            return true;
        }

        private bool IsCatalogChapter(EncounterChapterAsset chapter)
            => chapter != null && JourneyCatalog?.Chapters != null && JourneyCatalog.Chapters.Contains(chapter);

        private EncounterChapterAsset CurrentChapter(Progress progress)
        {
            EncounterChapterAsset latest = null;
            EncounterChapterAsset incomplete = null;
            if (JourneyCatalog?.Chapters == null) return null;
            foreach (var chapter in JourneyCatalog.Chapters)
            {
                if (chapter == null || !IsChapterAvailable(chapter, progress)) continue;
                latest = chapter;
                if (!IsChapterCompleted(chapter, progress)) incomplete = chapter;
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
            Commit(new Transition(journey, EncounterJourneyPhase.Sequence, StartBeat(journey, progress)));
            return true;
        }

        /** A finished journey replays from its start; an unfinished one resumes after its last won Encounter beat. */
        private static int StartBeat(EncounterJourneyAsset journey, Progress progress)
        {
            if (IsJourneyCompleted(journey, progress)) return 0;
            // Winning the last beat completes the journey, so only an earlier won Encounter beat is resumed after.
            for (int index = journey.Beats.Count - 2; index >= 0; --index)
            {
                var beat = journey.EncounterBeatAt(index);
                if (beat != null && progress.CompletedEncounterIds.Contains(beat.Encounter.Identity)) return index + 1;
            }
            return 0;
        }

        private bool IsCatalogJourney(EncounterJourneyAsset journey)
            => journey != null && JourneyCatalog != null && JourneyCatalog.Entries.Contains(journey);

        /** Whether the intro to the next Encounter beat can be skipped: its encounter was visited and its invitation is ahead. */
        public bool CanSkipIntro(Progress progress)
        {
            if (JourneyPhase != EncounterJourneyPhase.Sequence || SelectedJourney == null || !progress.HasProfile) return false;
            int encounter = NextEncounterBeat();
            return encounter > BeatIndex + 1 &&
                progress.VisitedEncounterIds.Contains(SelectedJourney.EncounterBeatAt(encounter).Encounter.Identity);
        }

        /** The index of the first Encounter beat after the current beat, or -1. */
        private int NextEncounterBeat()
        {
            for (int index = BeatIndex + 1; index < SelectedJourney.Beats.Count; ++index)
                if (SelectedJourney.EncounterBeatAt(index) != null) return index;
            return -1;
        }

        public bool CanResume(bool hasActiveEncounter) => hasActiveEncounter && JourneyPhase == EncounterJourneyPhase.Investigation;

        /**
         * Continues from the current beat to the next, or to the board after the last beat. An invitation waits for Accept
         * instead, and an Encounter beat continues only once its encounter is won. Continuing from the last beat completes
         * the journey, except from an Encounter beat, whose win completed it.
         */
        public bool AdvanceJourney(EncounterOutcome outcome, out Transition transition)
        {
            transition = null;
            if (SelectedJourney == null) return false;
            bool encounter = JourneyPhase == EncounterJourneyPhase.Playing;
            if (encounter ? outcome != EncounterOutcome.Won :
                JourneyPhase != EncounterJourneyPhase.Sequence || SelectedJourney.EncounterBeatAt(BeatIndex + 1) != null) return false;
            transition = BeatIndex < SelectedJourney.Beats.Count - 1
                ? new Transition(SelectedJourney, EncounterJourneyPhase.Sequence, BeatIndex + 1, BeatIndex)
                : new Transition(SelectedJourney, EncounterJourneyPhase.Investigation, -1, BeatIndex, !encounter);
            return true;
        }

        public bool SkipIntro(Progress progress, out Transition transition)
        {
            transition = null;
            if (!CanSkipIntro(progress)) return false;
            int invitation = NextEncounterBeat() - 1;
            transition = new Transition(SelectedJourney, EncounterJourneyPhase.Sequence, invitation, invitation - 1);
            return true;
        }

        public bool AcceptEncounter(out Transition transition)
        {
            transition = null;
            // The beat directly before an Encounter beat is its invitation.
            if (JourneyPhase != EncounterJourneyPhase.Sequence || SelectedJourney == null ||
                SelectedJourney.EncounterBeatAt(BeatIndex + 1) == null) return false;
            transition = new Transition(SelectedJourney, EncounterJourneyPhase.Playing, BeatIndex + 1, BeatIndex);
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
            transition = Playing(journey, encounterId);
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
            Commit(Playing(journey, encounterId.Value));
            return true;
        }

        public bool RetryEncounter(EncounterDefinition.Id encounterId, EncounterOutcome outcome)
        {
            if (JourneyPhase != EncounterJourneyPhase.Playing || outcome == EncounterOutcome.None) return false;
            var journey = JourneyCatalog?.Find(encounterId);
            if (journey == null) return false;
            Commit(new Transition(journey, EncounterJourneyPhase.Sequence, journey.IndexOfEncounter(encounterId) - 1));
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
            else if (transition.Journey != null && progress.HasValue) SelectedChapter = CurrentChapter(progress.Value);
            JourneyPhase = transition.Phase;
            SelectedJourney = transition.Phase == EncounterJourneyPhase.Investigation ? null : transition.Journey;
            BeatIndex = transition.BeatIndex;
        }

        /** Playing the encounter at its Encounter beat of the journey; without a journey there is no beat. */
        private static Transition Playing(EncounterJourneyAsset journey, EncounterDefinition.Id encounterId)
            => new Transition(journey, EncounterJourneyPhase.Playing, journey != null ? journey.IndexOfEncounter(encounterId) : -1);

        /** Read-only facts supplied for a navigation decision; the navigator does not retain the profile. */
        public readonly struct Progress
        {
            public bool HasProfile { get; }
            public IReadOnlyList<EncounterDefinition.Id> VisitedEncounterIds { get; }
            public IReadOnlyList<EncounterJourneyAsset.Id> CompletedJourneyIds { get; }
            /** The won encounters, after which an unfinished journey resumes. */
            public IReadOnlyList<EncounterDefinition.Id> CompletedEncounterIds { get; }

            public Progress(bool hasProfile, IReadOnlyList<EncounterDefinition.Id> visitedEncounterIds,
                IReadOnlyList<EncounterJourneyAsset.Id> completedJourneyIds,
                IReadOnlyList<EncounterDefinition.Id> completedEncounterIds = null)
            {
                HasProfile = hasProfile;
                VisitedEncounterIds = visitedEncounterIds ?? Array.Empty<EncounterDefinition.Id>();
                CompletedJourneyIds = completedJourneyIds ?? Array.Empty<EncounterJourneyAsset.Id>();
                CompletedEncounterIds = completedEncounterIds ?? Array.Empty<EncounterDefinition.Id>();
            }
        }

        /** A synchronous transition proposal; creating one leaves navigation unchanged until Commit succeeds. */
        public sealed class Transition
        {
            private readonly int rewardThroughBeat;
            public EncounterJourneyAsset Journey { get; }
            public EncounterJourneyPhase Phase { get; }
            public int BeatIndex { get; }
            /** Whether the transition finishes the journey's last beat, which completes the journey. */
            public bool CompletesJourney { get; }

            internal Transition(EncounterJourneyAsset journey, EncounterJourneyPhase phase, int beatIndex, int rewardThroughBeat = -1,
                bool completesJourney = false)
            {
                Journey = journey;
                Phase = phase;
                BeatIndex = beatIndex;
                this.rewardThroughBeat = rewardThroughBeat;
                CompletesJourney = completesJourney;
            }

            public IEnumerable<ProgressionRewardAsset> Rewards
            {
                get
                {
                    // Revisit earlier beats for skipped sequences and retries; grants are idempotent.
                    for (int index = 0; index <= rewardThroughBeat; ++index)
                        if (Journey.Beats[index].ProgressionReward != null) yield return Journey.Beats[index].ProgressionReward;
                    if (CompletesJourney && Journey.CompletionReward != null) yield return Journey.CompletionReward;
                }
            }
        }
    }
}
