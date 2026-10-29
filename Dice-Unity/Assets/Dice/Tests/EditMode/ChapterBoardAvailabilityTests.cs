using System.Linq;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    /** The investigation board and chapter selector, bound to test-only journey controls on a minimal test hierarchy. */
    public sealed class ChapterBoardAvailabilityTests
    {
        private readonly SyntheticCharmAssets content = new SyntheticCharmAssets();
        private SyntheticUi ui;
        private TestJourneyControls controls;

        [SetUp]
        public void SetUp()
        {
            ui = new SyntheticUi();
            controls = new TestJourneyControls { BeatIndex = -1 };
        }

        [TearDown]
        public void TearDown()
        {
            ui.Dispose();
            content.Dispose();
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
            controls.JourneyAvailable = journey => journey == open || journey == elsewhere;

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
            controls.ChapterAvailable = id => id == open.Id;

            var selector = BuildSelector(ui.Root);
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
            var boardRoot = ui.Rect(ui.Root, "Board");
            var board = boardRoot.gameObject.AddComponent<SpiritInvestigationBoard>();
            SyntheticUi.Set(board, "<Root>k__BackingField", boardRoot);
            SyntheticUi.Set(board, "<Title>k__BackingField", ui.Label(boardRoot, "Title"));
            SyntheticUi.Set(board, "<Description>k__BackingField", ui.Label(boardRoot, "Description"));
            SyntheticUi.Set(board, "<Status>k__BackingField", ui.Label(boardRoot, "Status"));
            var scroll = Scroll(boardRoot, out var content);
            SyntheticUi.Set(board, "<Scroll>k__BackingField", scroll);
            SyntheticUi.Set(board, "<Content>k__BackingField", content);
            SyntheticUi.Set(board, "<ResumeButton>k__BackingField", ui.LabeledButton(boardRoot, "Resume"));
            SyntheticUi.Set(board, "<LoadButton>k__BackingField", ui.LabeledButton(boardRoot, "Load"));
            SyntheticUi.Set(board, "<CollectionButton>k__BackingField", ui.LabeledButton(boardRoot, "Collection"));
            SyntheticUi.Set(board, "<ChapterButton>k__BackingField", ui.LabeledButton(boardRoot, "Chapters"));
            SyntheticUi.Set(board, "<ChapterSelector>k__BackingField", BuildSelector(boardRoot));
            SyntheticUi.Set(board, "chapterLabel", ui.Label(boardRoot, "ChapterLabel"));
            SyntheticUi.Set(board, "clueCardPrefab", CardTemplate(ui.Root));
            return board;
        }

        private SpiritChapterSelector BuildSelector(Transform parent)
        {
            var selectorRoot = ui.Rect(parent, "ChapterSelector");
            var selector = selectorRoot.gameObject.AddComponent<SpiritChapterSelector>();
            SyntheticUi.Set(selector, "<Root>k__BackingField", selectorRoot);
            var scroll = Scroll(selectorRoot, out var content);
            SyntheticUi.Set(selector, "<Scroll>k__BackingField", scroll);
            SyntheticUi.Set(selector, "<Content>k__BackingField", content);
            SyntheticUi.Set(selector, "<CloseButton>k__BackingField", ui.LabeledButton(selectorRoot, "Close"));
            SyntheticUi.Set(selector, "chapterCardPrefab", CardTemplate(ui.Root));
            selector.Build();
            return selector;
        }

        // A card template outside every list, so a list's content holds only the cards bound into it.
        private SpiritJourneyCard CardTemplate(Transform parent)
        {
            var cardRoot = ui.Rect(parent, "CardTemplate");
            var card = cardRoot.gameObject.AddComponent<SpiritJourneyCard>();
            var artwork = ui.Rect(cardRoot, "Artwork").gameObject.AddComponent<RawImage>();
            var state = ui.Label(cardRoot, "State");
            SyntheticUi.Set(card, "button", cardRoot.gameObject.AddComponent<Button>());
            SyntheticUi.Set(card, "artwork", artwork);
            SyntheticUi.Set(card, "artworkAspect", artwork.gameObject.AddComponent<AspectRatioFitter>());
            SyntheticUi.Set(card, "title", ui.Label(cardRoot, "Title"));
            SyntheticUi.Set(card, "description", ui.Label(cardRoot, "Description"));
            SyntheticUi.Set(card, "number", ui.Label(cardRoot, "Number"));
            SyntheticUi.Set(card, "state", state);
            SyntheticUi.Set(card, "highlight", state.gameObject.AddComponent<Outline>());
            SyntheticUi.Set(card, "newLead", ui.Rect(cardRoot, "NewLead").gameObject);
            return card;
        }

        private ScrollRect Scroll(Transform parent, out RectTransform content)
        {
            var scrollRoot = ui.Rect(parent, "Scroll");
            var viewport = ui.Rect(scrollRoot, "Viewport");
            content = ui.Rect(viewport, "Content");
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            return scroll;
        }

        private EncounterJourneyCatalogAsset Catalog(params EncounterChapterAsset[] chapters)
            => TestJourneys.Catalog(Create<EncounterJourneyCatalogAsset>(), chapters);

        private EncounterChapterAsset Chapter(string id, params EncounterJourneyAsset[] entries)
            => TestJourneys.Chapter(Create<EncounterChapterAsset>(), id, entries);

        private EncounterJourneyAsset Journey(string id)
        {
            var journey = TestJourneys.Identify(Create<EncounterJourneyAsset>(), id);
            TestJourneys.SetEncounterBeats(journey, null, content.Encounter(id));
            SyntheticUi.Set(journey, "locationTitle", id);
            return journey;
        }

        private T Create<T>() where T : ScriptableObject
        {
            return ui.Own(ScriptableObject.CreateInstance<T>());
        }
    }
}
