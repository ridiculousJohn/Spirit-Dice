using System;
using System.Collections.Generic;
using Dice.Engine;
using Dice.UnityContent;

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
        IReadOnlyList<EncounterDefinition.Id> VisitedEncounterIds { get; }
        /** The follow-up leads of won encounters, highlighted until they are selected. */
        IReadOnlyList<EncounterJourneyAsset> HighlightedJourneys { get; }
        IReadOnlyList<EncounterJourneyAsset.Id> CompletedJourneyIds { get; }
        /** What giving the rewards would add that the player lacks now. */
        RewardLack Lacks(IEnumerable<ProgressionRewardAsset> rewards);
        /** Gives the rewards to the player. */
        void GiveRewards(IEnumerable<ProgressionRewardAsset> rewards);
        bool IsJourneyAvailable(EncounterJourneyAsset journey);
        bool IsChapterAvailable(EncounterChapterAsset chapter);
        bool IsChapterCompleted(EncounterChapterAsset chapter);

        event Action JourneyChanged;
        void SelectChapter(EncounterChapterAsset chapter);
        void SelectEncounter(EncounterJourneyAsset journey);
        void AdvanceJourney();
        void SkipIntro();
        void AcceptEncounter();
        void ReturnToInvestigation();
        void ResumeEncounter();
        void RetryEncounter();
    }
}
