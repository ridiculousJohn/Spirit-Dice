using System;
using System.Collections.Generic;
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
        public Func<string, bool> JourneyAvailable { get; set; } = id => true;
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
        public IReadOnlyList<string> VisitedEncounterIds => Array.Empty<string>();
        public IReadOnlyList<string> HighlightedEncounterIds => Array.Empty<string>();
        public IReadOnlyList<string> CompletedStoryIds => Array.Empty<string>();
        public bool IsJourneyAvailable(string id) => JourneyAvailable(id);
        public bool IsChapterAvailable(string id) => ChapterAvailable(id);
        public bool IsChapterCompleted(string id) => false;

        public event Action JourneyChanged { add { } remove { } }
        public void SelectChapter(string id) => SelectedChapters.Add(id);
        public void SelectEncounter(string encounterId) { }
        public void AdvanceJourney() { }
        public void SkipIntro() { }
        public void AcceptEncounter() => ++Accepted;
        public void ReturnToInvestigation() { }
        public void ResumeEncounter() { }
        public void RetryEncounter() { }
    }
}
