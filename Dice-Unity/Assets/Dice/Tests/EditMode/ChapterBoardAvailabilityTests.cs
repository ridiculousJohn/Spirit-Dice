using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    /** The investigation board and chapter selector, bound to test-only journey controls on a minimal test hierarchy. */
    public sealed class ChapterBoardAvailabilityTests
    {
        private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        private GameObject root;
        private JourneyControls controls;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Chapter board test", typeof(RectTransform));
            controls = new JourneyControls();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();
        }

        /** Checks the board shows a lead only when it is available and belongs to the selected chapter; fails if a locked lead or another chapter's lead appears. */
        [Test]
        public void BoardShowsOnlyAvailableLeadsOfTheSelectedChapter()
        {
            var open = Journey("encounter.open");
            var locked = Journey("encounter.locked");
            var elsewhere = Journey("encounter.elsewhere");
            var selected = Chapter("chapter.selected", open, locked);
            controls.JourneyCatalog = Catalog(selected, Chapter("chapter.other", elsewhere));
            controls.SelectedChapter = selected;
            controls.AvailableJourneys.Add(open.Id);
            controls.AvailableJourneys.Add(elsewhere.Id);

            var board = BuildBoard();
            board.Render(controls, null, true, false);
            Assert.That(board.Content.GetComponentsInChildren<SpiritJourneyCard>(true).Select(card => card.Title.text),
                Is.EqualTo(new[] { open.LocationTitle }));
        }

        /** Checks the selector lets only available chapters be chosen and closes on a choice; fails if a locked chapter is choosable or the selector stays open. */
        [Test]
        public void SelectorOffersOnlyAvailableChaptersAndClosesOnAChoice()
        {
            var open = Chapter("chapter.open", Journey("encounter.open"));
            var locked = Chapter("chapter.locked", Journey("encounter.locked"));
            controls.JourneyCatalog = Catalog(open, locked);
            controls.SelectedChapter = open;
            controls.AvailableChapters.Add(open.Id);

            var selector = BuildSelector(root.transform);
            selector.Show(controls);
            Assert.That(selector.IsOpen, Is.True);
            var cards = selector.Content.GetComponentsInChildren<SpiritJourneyCard>(true);
            Assert.That(cards.Select(card => card.Button.interactable), Is.EqualTo(new[] { true, false }));
            cards[0].Button.onClick.Invoke();
            Assert.That(selector.IsOpen, Is.False);
            Assert.That(controls.SelectedChapters, Is.EqualTo(new[] { open.Id }));
        }

        private SpiritInvestigationBoard BuildBoard()
        {
            var boardRoot = Rect(root.transform, "Board");
            var board = boardRoot.gameObject.AddComponent<SpiritInvestigationBoard>();
            SetProperty(board, "Root", boardRoot);
            SetProperty(board, "Title", Label(boardRoot, "Title"));
            SetProperty(board, "Description", Label(boardRoot, "Description"));
            SetProperty(board, "Status", Label(boardRoot, "Status"));
            var scroll = Scroll(boardRoot, out var content);
            SetProperty(board, "Scroll", scroll);
            SetProperty(board, "Content", content);
            SetProperty(board, "ResumeButton", NewButton(boardRoot, "Resume"));
            SetProperty(board, "LoadButton", NewButton(boardRoot, "Load"));
            SetProperty(board, "CollectionButton", NewButton(boardRoot, "Collection"));
            SetProperty(board, "ChapterButton", NewButton(boardRoot, "Chapters"));
            SetProperty(board, "ChapterSelector", BuildSelector(boardRoot));
            Set(board, "chapterLabel", Label(boardRoot, "ChapterLabel"));
            Set(board, "clueCardPrefab", CardTemplate(root.transform));
            return board;
        }

        private SpiritChapterSelector BuildSelector(Transform parent)
        {
            var selectorRoot = Rect(parent, "ChapterSelector");
            var selector = selectorRoot.gameObject.AddComponent<SpiritChapterSelector>();
            SetProperty(selector, "Root", selectorRoot);
            var scroll = Scroll(selectorRoot, out var content);
            SetProperty(selector, "Scroll", scroll);
            SetProperty(selector, "Content", content);
            SetProperty(selector, "CloseButton", NewButton(selectorRoot, "Close"));
            Set(selector, "chapterCardPrefab", CardTemplate(root.transform));
            selector.Build(selectorRoot);
            return selector;
        }

        // A card template outside every list, so a list's content holds only the cards bound into it.
        private static SpiritJourneyCard CardTemplate(Transform parent)
        {
            var cardRoot = Rect(parent, "CardTemplate");
            var card = cardRoot.gameObject.AddComponent<SpiritJourneyCard>();
            var artwork = Rect(cardRoot, "Artwork").gameObject.AddComponent<RawImage>();
            var state = Label(cardRoot, "State");
            Set(card, "button", cardRoot.gameObject.AddComponent<Button>());
            Set(card, "artwork", artwork);
            Set(card, "artworkAspect", artwork.gameObject.AddComponent<AspectRatioFitter>());
            Set(card, "title", Label(cardRoot, "Title"));
            Set(card, "description", Label(cardRoot, "Description"));
            Set(card, "number", Label(cardRoot, "Number"));
            Set(card, "state", state);
            Set(card, "highlight", state.gameObject.AddComponent<Outline>());
            Set(card, "newLead", Rect(cardRoot, "NewLead").gameObject);
            return card;
        }

        private static ScrollRect Scroll(Transform parent, out RectTransform content)
        {
            var scrollRoot = Rect(parent, "Scroll");
            var viewport = Rect(scrollRoot, "Viewport");
            content = Rect(viewport, "Content");
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            return scroll;
        }

        private EncounterJourneyCatalogAsset Catalog(params EncounterChapterAsset[] chapters)
        {
            var catalog = Create<EncounterJourneyCatalogAsset>();
            Set(catalog, "chapters", chapters);
            return catalog;
        }

        private EncounterChapterAsset Chapter(string id, params EncounterJourneyAsset[] entries)
        {
            var chapter = Create<EncounterChapterAsset>();
            Set(chapter, "id", id);
            Set(chapter, "title", id);
            Set(chapter, "entries", entries);
            return chapter;
        }

        private EncounterJourneyAsset Journey(string id)
        {
            var journey = Create<EncounterJourneyAsset>();
            Set(journey, "encounterId", id);
            Set(journey, "locationTitle", id);
            return journey;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }

        private static RectTransform Rect(Transform parent, string name)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            return (RectTransform)item.transform;
        }

        private static Text Label(Transform parent, string name) => Rect(parent, name).gameObject.AddComponent<Text>();

        private static Button NewButton(Transform parent, string name)
        {
            var button = Rect(parent, name).gameObject.AddComponent<Button>();
            Label(button.transform, "Label");
            return button;
        }

        private static void SetProperty(object target, string name, object value)
            => target.GetType().GetProperty(name).GetSetMethod(true).Invoke(target, new[] { value });

        private static void Set(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        /** Journey controls whose availability the test sets directly; they record chapter choices and ignore other actions. */
        private sealed class JourneyControls : IEncounterJourneyControls
        {
            public readonly HashSet<string> AvailableJourneys = new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> AvailableChapters = new HashSet<string>(StringComparer.Ordinal);
            public readonly List<string> SelectedChapters = new List<string>();
            public EncounterJourneyPhase JourneyPhase => EncounterJourneyPhase.Investigation;
            public EncounterJourneyCatalogAsset JourneyCatalog { get; set; }
            public EncounterChapterAsset SelectedChapter { get; set; }
            public EncounterJourneyAsset SelectedJourney => null;
            public int BeatIndex => -1;
            public bool CanSkipIntro => false;
            public bool HasActiveEncounter => false;
            public bool CanResume => false;
            public SpiritStatus JourneyStatus => SpiritStatus.None;
            public IReadOnlyList<string> VisitedEncounterIds => Array.Empty<string>();
            public IReadOnlyList<string> HighlightedEncounterIds => Array.Empty<string>();
            public IReadOnlyList<string> CompletedStoryIds => Array.Empty<string>();
            public bool IsJourneyAvailable(string id) => AvailableJourneys.Contains(id);
            public bool IsChapterAvailable(string id) => AvailableChapters.Contains(id);
            public bool IsChapterCompleted(string id) => false;

            public event Action JourneyChanged { add { } remove { } }
            public void SelectChapter(string id) => SelectedChapters.Add(id);
            public void SelectEncounter(string encounterId) { }
            public void AdvanceJourney() { }
            public void SkipIntro() { }
            public void AcceptEncounter() { }
            public void ReturnToInvestigation() { }
            public void ResumeEncounter() { }
            public void RetryEncounter() { }
        }
    }
}
