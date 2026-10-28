using System;
using System.Collections.Generic;

namespace Dice.Presentation
{
    public enum EncounterJourneyPhase { Investigation, Sequence, Playing, MainMenu }

    public interface IEncounterJourneyControls
    {
        EncounterJourneyPhase JourneyPhase { get; }
        EncounterJourneyCatalogAsset JourneyCatalog { get; }
        EncounterChapterAsset SelectedChapter { get; }
        EncounterJourneyAsset SelectedJourney { get; }
        int BeatIndex { get; }
        bool CanSkipIntro { get; }
        bool HasActiveEncounter { get; }
        bool CanResume { get; }
        SpiritStatus JourneyStatus { get; }
        IReadOnlyList<string> VisitedEncounterIds { get; }
        IReadOnlyList<string> HighlightedEncounterIds { get; }
        IReadOnlyList<string> CompletedStoryIds { get; }
        bool IsJourneyAvailable(string id);
        bool IsChapterAvailable(string id);
        bool IsChapterCompleted(string id);

        event Action JourneyChanged;
        void SelectChapter(string id);
        void SelectEncounter(string encounterId);
        void AdvanceJourney();
        void SkipIntro();
        void AcceptEncounter();
        void ReturnToInvestigation();
        void ResumeEncounter();
        void RetryEncounter();
    }
}
