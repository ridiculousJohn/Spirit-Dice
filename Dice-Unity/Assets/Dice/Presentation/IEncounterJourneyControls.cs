using System;
using System.Collections.Generic;

namespace Dice.Presentation
{
    public enum EncounterJourneyPhase { Investigation, Sequence, Playing }

    public interface IEncounterJourneyControls
    {
        EncounterJourneyPhase JourneyPhase { get; }
        EncounterJourneyCatalogAsset JourneyCatalog { get; }
        EncounterJourneyAsset SelectedJourney { get; }
        int BeatIndex { get; }
        bool CanSkipIntro { get; }
        bool HasActiveEncounter { get; }
        bool CanResume { get; }
        string JourneyStatus { get; }
        IReadOnlyList<string> VisitedEncounterIds { get; }
        IReadOnlyList<string> HighlightedEncounterIds { get; }

        event Action JourneyChanged;
        void SelectEncounter(string encounterId);
        void AdvanceJourney();
        void SkipIntro();
        void AcceptEncounter();
        void ReturnToInvestigation();
        void ResumeEncounter();
        void RetryEncounter();
    }
}
