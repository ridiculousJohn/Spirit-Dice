using System;
using System.Collections.Generic;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;

namespace Dice.Tests
{
    /**
     * Journey controls whose state the test sets directly. They record chapter choices, acceptances and the rewards they
     * are given, which their profile receives, and ignore every other action.
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
        public IReadOnlyList<EncounterJourneyAsset.Id> CompletedJourneyIds { get; set; } = Array.Empty<EncounterJourneyAsset.Id>();
        /** The catalog rewards are built from; without one nothing is lacking. */
        public MechanicalCatalog RewardCatalog { get; set; }
        /** The player's profile, which says what a reward lacks and receives what is given. */
        public PlayerProfile Profile { get; } = new PlayerProfile();
        /** Each call's rewards, in order. */
        public readonly List<ProgressionRewardAsset[]> Given = new List<ProgressionRewardAsset[]>();

        public RewardLack Lacks(IEnumerable<ProgressionRewardAsset> rewards)
        {
            if (RewardCatalog == null) return RewardLack.None;
            var items = new List<ItemDefinition.Id>();
            bool any = false;
            foreach (var reward in rewards) any |= Profile.Lacks(reward.Build(RewardCatalog), RewardCatalog, items);
            return new RewardLack(any, items);
        }

        public void GiveRewards(IEnumerable<ProgressionRewardAsset> rewards)
        {
            var given = rewards.ToArray();
            Given.Add(given);
            foreach (var reward in given) Profile.ApplyReward(reward.Build(RewardCatalog), RewardCatalog);
        }
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
