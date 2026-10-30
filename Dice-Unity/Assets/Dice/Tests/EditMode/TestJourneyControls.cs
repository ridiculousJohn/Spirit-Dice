using System;
using System.Collections.Generic;
using Dice.Engine;
using Dice.Presentation;

namespace Dice.Tests
{
    /**
     * Journey controls whose state the test sets directly. They record chapter choices and acceptances and ignore
     * every other action.
     */
    internal sealed class TestJourneyControls : IEncounterJourneyControls
    {
        public readonly List<EncounterChapterAsset> SelectedChapters = new List<EncounterChapterAsset>();
        public int Accepted;
        public Func<EncounterJourneyAsset, bool> JourneyAvailable { get; set; } = journey => true;
        public Func<EncounterChapterAsset, bool> ChapterAvailable { get; set; } = chapter => false;

        public EncounterJourneyPhase JourneyPhase { get; set; }
        public EncounterJourneyCatalogAsset JourneyCatalog { get; set; }
        public EncounterChapterAsset SelectedChapter { get; set; }
        public EncounterJourneyAsset SelectedJourney { get; set; }
        public int BeatIndex { get; set; }
        public bool CanSkipIntro => false;
        public bool HasActiveEncounter => false;
        public bool CanResume => false;
        public SpiritStatus JourneyStatus => SpiritStatus.None;
        public IReadOnlyList<EncounterDefinition.Id> VisitedEncounterIds => Array.Empty<EncounterDefinition.Id>();
        public IReadOnlyList<EncounterJourneyAsset> HighlightedJourneys => Array.Empty<EncounterJourneyAsset>();
        public IReadOnlyList<EncounterJourneyAsset.Id> CompletedJourneyIds => Array.Empty<EncounterJourneyAsset.Id>();
        public bool IsJourneyAvailable(EncounterJourneyAsset journey) => JourneyAvailable(journey);
        public bool IsChapterAvailable(EncounterChapterAsset chapter) => ChapterAvailable(chapter);
        public bool IsChapterCompleted(EncounterChapterAsset chapter) => false;

        public event Action JourneyChanged { add { } remove { } }
        public void SelectChapter(EncounterChapterAsset chapter) => SelectedChapters.Add(chapter);
        public void SelectEncounter(EncounterJourneyAsset journey) { }
        public void AdvanceJourney() { }
        public void SkipIntro() { }
        public void AcceptEncounter() => ++Accepted;
        public void ReturnToInvestigation() { }
        public void ResumeEncounter() { }
        public void RetryEncounter() { }
    }
}
