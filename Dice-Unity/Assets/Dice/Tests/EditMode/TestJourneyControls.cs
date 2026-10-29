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
        public readonly List<string> SelectedChapters = new List<string>();
        public int Accepted;
        public Func<EncounterJourneyAsset, bool> JourneyAvailable { get; set; } = journey => true;
        public Func<string, bool> ChapterAvailable { get; set; } = id => false;

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
        public IReadOnlyList<string> CompletedStoryIds => Array.Empty<string>();
        public bool IsJourneyAvailable(EncounterJourneyAsset journey) => JourneyAvailable(journey);
        public bool IsChapterAvailable(string id) => ChapterAvailable(id);
        public bool IsChapterCompleted(string id) => false;

        public event Action JourneyChanged { add { } remove { } }
        public void SelectChapter(string id) => SelectedChapters.Add(id);
        public void SelectEncounter(EncounterJourneyAsset journey) { }
        public void AdvanceJourney() { }
        public void SkipIntro() { }
        public void AcceptEncounter() => ++Accepted;
        public void ReturnToInvestigation() { }
        public void ResumeEncounter() { }
        public void RetryEncounter() { }
    }
}
