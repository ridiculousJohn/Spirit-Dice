using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Investigation board listing available journeys as clue cards with navigation and Collection actions. */
    public sealed class SpiritInvestigationBoard : MonoBehaviour
    {
        private readonly List<GameObject> clueCards = new List<GameObject>();
        private string renderedProgress;
        private EncounterChapterAsset renderedChapter;
        private EncounterJourneyCatalogAsset renderedCatalog;
        private IEncounterJourneyControls journeyControls;
        private ISpiritMainMenuControls mainMenuControls;
        [SerializeField] private Text chapterLabel;
        [SerializeField] private SpiritJourneyCard clueCardPrefab;
        [SerializeField] private SpiritCompletionClue completionCluePrefab;
        private Action resume;
        private Action load;
        private Action openCollection;

        [field: SerializeField] public RectTransform Root { get; private set; }
        [field: SerializeField] public Text Title { get; private set; }
        [field: SerializeField] public Text Description { get; private set; }
        [field: SerializeField] public ScrollRect Scroll { get; private set; }
        [field: SerializeField] public RectTransform Content { get; private set; }
        [field: SerializeField] public Text Status { get; private set; }
        [field: SerializeField] public Button ResumeButton { get; private set; }
        [field: SerializeField] public Button LoadButton { get; private set; }
        [field: SerializeField] public Button CollectionButton { get; private set; }
        [field: SerializeField] public Button ChapterButton { get; private set; }
        [field: SerializeField] public SpiritChapterSelector ChapterSelector { get; private set; }

        public void Build(Action resume, Action load, Action openCollection)
        {
            if (resume == null) throw new ArgumentNullException(nameof(resume));
            if (load == null) throw new ArgumentNullException(nameof(load));
            if (openCollection == null) throw new ArgumentNullException(nameof(openCollection));
            if (Root == null || Title == null || Description == null || Scroll == null || Content == null ||
                Status == null || ResumeButton == null || LoadButton == null || CollectionButton == null ||
                ChapterButton == null || ChapterSelector == null || chapterLabel == null ||
                clueCardPrefab == null || completionCluePrefab == null)
                throw new InvalidOperationException("The investigation board requires authored UI references.");
            SpiritUi.RequireWording(transform);
            this.resume = resume;
            this.load = load;
            this.openCollection = openCollection;
            ResumeButton.onClick.RemoveListener(Resume);
            ResumeButton.onClick.AddListener(Resume);
            LoadButton.onClick.RemoveListener(Load);
            LoadButton.onClick.AddListener(Load);
            CollectionButton.onClick.RemoveListener(OpenCollection);
            CollectionButton.onClick.AddListener(OpenCollection);
            ChapterButton.onClick.RemoveListener(OpenChapters);
            ChapterButton.onClick.AddListener(OpenChapters);
            ChapterSelector.Build();
        }

        public void BindMainMenu(ISpiritMainMenuControls menu)
        {
            mainMenuControls = menu;
        }

        /** Clue cards rebuild when forced or when availability, visits, highlights, or completions change. */
        public void Render(IEncounterJourneyControls controls, ISpiritEncounterControls encounter, bool rebuild,
            bool collectionAvailable)
        {
            if (controls == null) throw new ArgumentNullException(nameof(controls));
            journeyControls = controls;
            var catalog = controls.JourneyCatalog;
            var chapter = controls.SelectedChapter;
            bool chapterChanged = renderedChapter != chapter || renderedCatalog != catalog;
            if (rebuild || chapterChanged) ChapterSelector.Close();
            bool chaptersAvailable = catalog != null && catalog.Chapters != null && catalog.Chapters.Count > 0;
            ChapterButton.gameObject.SetActive(chaptersAvailable);
            ChapterButton.interactable = encounter == null || !encounter.IsBusy;
            int chapterIndex = 0;
            if (chaptersAvailable)
                for (int i = 0; i < catalog.Chapters.Count; ++i)
                    if (catalog.Chapters[i] == chapter) { chapterIndex = i + 1; break; }
            var wording = SpiritUi.GetWording(transform);
            chapterLabel.text = chapterIndex > 0 ? wording.investigation.ChapterProgress(chapterIndex) : wording.investigation.chapters;
            Title.text = chapter != null ? chapter.Title : catalog != null ? catalog.Title : wording.investigation.defaultTitle;
            Description.text = chapter != null ? chapter.Description : catalog != null ? catalog.Description : "";
            string progress = Progress(controls);
            if (rebuild || chapterChanged || renderedProgress != progress)
            {
                var firstHighlight = BuildClues(controls);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
                // Highlight focus takes precedence over the chapter's default position at the top.
                if (firstHighlight != null || chapterChanged)
                {
                    Scroll.StopMovement();
                    float scrollableHeight = Content.rect.height - Scroll.viewport.rect.height;
                    float cardTop = firstHighlight != null ? Content.InverseTransformPoint(
                        firstHighlight.TransformPoint(new Vector3(0, firstHighlight.rect.yMax, 0))).y : 0;
                    Scroll.verticalNormalizedPosition = firstHighlight != null && scrollableHeight > 0 ?
                        1 - Mathf.Clamp01(-cardTop / scrollableHeight) : 1;
                }
            }
            renderedProgress = progress;
            renderedChapter = chapter;
            renderedCatalog = catalog;
            Status.text = SpiritStatusText.Describe(controls.JourneyStatus, wording.status);
            ResumeButton.gameObject.SetActive(controls.CanResume);
            bool hasMainMenu = mainMenuControls != null;
            SpiritUi.SetButtonLabel(LoadButton, hasMainMenu ? wording.investigation.mainMenuButton : wording.investigation.loadButton);
            LoadButton.interactable = hasMainMenu ? encounter == null || !encounter.IsBusy :
                encounter != null && !encounter.IsBusy;
            CollectionButton.interactable = collectionAvailable;
        }

        private RectTransform BuildClues(IEncounterJourneyControls controls)
        {
            foreach (var card in clueCards) { card.SetActive(false); Destroy(card); }
            clueCards.Clear();
            if (controls.JourneyCatalog == null) return null;
            int index = 0;
            RectTransform firstHighlight = null;
            var wording = SpiritUi.GetWording(transform).investigation;
            foreach (var journey in Entries(controls))
            {
                if (journey == null || !controls.IsJourneyAvailable(journey)) continue;
                var selected = journey;
                var card = Instantiate(clueCardPrefab, Content, false);
                card.name = "ClueButton" + index++;
                clueCards.Add(card.gameObject);
                bool highlighted = controls.HighlightedJourneys.Contains(journey);
                if (highlighted && firstHighlight == null) firstHighlight = (RectTransform)card.transform;
                bool visited = journey.Encounters.Any(encounter => controls.VisitedEncounterIds.Contains(encounter.Identity));
                bool completed = controls.CompletedJourneyIds.Contains(journey.Identity);
                // A journey that plays an encounter is visited before it can be completed.
                string state = visited ? wording.visitedLead : completed ? wording.readAgain : wording.followLead;
                card.Bind(journey.CardArtwork, journey.LocationTitle, journey.LeadText, "", state,
                    highlighted, highlighted, true, () => controls.SelectEncounter(selected));
                if (completed && !string.IsNullOrWhiteSpace(journey.CompletionClueText))
                {
                    var discovered = Instantiate(completionCluePrefab, Content, false);
                    clueCards.Add(discovered.gameObject);
                    discovered.Bind(journey.CompletionClueTitle, journey.CompletionClueText);
                }
            }
            return firstHighlight;
        }

        private void Resume() => resume?.Invoke();
        private void Load()
        {
            if (mainMenuControls != null) mainMenuControls.ShowMainMenu();
            else load?.Invoke();
        }
        private void OpenCollection() => openCollection?.Invoke();
        private void OpenChapters()
        {
            if (journeyControls != null) ChapterSelector.Show(journeyControls);
        }

        private static string Progress(IEncounterJourneyControls controls)
        {
            var available = new List<string>();
            if (controls.JourneyCatalog != null)
                foreach (var journey in Entries(controls))
                    if (journey != null && controls.IsJourneyAvailable(journey)) available.Add(journey.name);
            return (controls.SelectedChapter != null ? controls.SelectedChapter.Id : "") + "\n\n" +
                string.Join("\n", available) + "\n\n" + string.Join("\n", controls.VisitedEncounterIds) +
                "\n\n" + string.Join("\n", controls.CompletedJourneyIds) + "\n\n" +
                string.Join("\n", controls.HighlightedJourneys.Select(journey => journey.name));
        }

        private static IReadOnlyList<EncounterJourneyAsset> Entries(IEncounterJourneyControls controls)
        {
            if (controls.SelectedChapter != null) return controls.SelectedChapter.Entries;
            if (controls.JourneyCatalog == null ||
                (controls.JourneyCatalog.Chapters != null && controls.JourneyCatalog.Chapters.Count > 0))
                return Array.Empty<EncounterJourneyAsset>();
            return controls.JourneyCatalog.Entries;
        }
    }
}
