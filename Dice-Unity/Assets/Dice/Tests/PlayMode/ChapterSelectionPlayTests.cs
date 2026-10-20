using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dice.App;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class ChapterSelectionPlayTests
    {
        private SpiritEncounterController controller;
        private SpiritEncounterView view => ContentReady.EncounterView(controller);
        private SpiritInvestigationBoard Board => ContentReady.FrontendView(controller).Board;
        private EncounterJourneyCatalogAsset catalog;
        private EncounterChapterAsset first;
        private EncounterChapterAsset second;
        private EncounterJourneyAsset fox;
        private EncounterJourneyAsset owl;
        private string captureRoot;

        [UnitySetUp]
        public IEnumerator InvestigationReady()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var entries = controller.JourneyCatalog.Entries.ToArray();
            fox = entries.Single(value => value.EncounterId == "encounter.fox.introduction");
            owl = entries.Single(value => value.EncounterId == "encounter.owl.watchtower");
            Assert.That(owl.RequiredCompletedEncounterIds, Does.Contain(fox.EncounterId));
            first = CreateChapter("chapter.test.first", "The Old Road",
                "Follow the lantern through the rain and discover where the missing traveler went.", new[] { fox });
            second = CreateChapter("chapter.test.second", "Beyond the Watchtower",
                "Follow the traveler's trail through the watchtower and the unmarked fork, then return to the lantern.",
                entries.Where(value => value != fox).ToArray());
            catalog = UnityEngine.Object.Instantiate(controller.JourneyCatalog);
            SetField(catalog, "chapters", new[] { first, second });
            InitializeProfile(new PlayerProfile());
            captureRoot = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(captureRoot))
                captureRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                    "chapter-selection-" + Guid.NewGuid().ToString("N")));
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartupSelectsCurrentChapterAndLockedChapterCannotBeOpened()
        {
            Assert.That(controller.SelectedChapter, Is.SameAs(first));
            Assert.That(controller.IsChapterAvailable(first.Id), Is.True);
            Assert.That(controller.IsChapterAvailable(second.Id), Is.False);
            AssertLeads(fox);
            controller.SelectChapter(second.Id);
            yield return null;
            Assert.That(controller.SelectedChapter, Is.SameAs(first));
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.SelectedJourney, Is.Null);
            AssertLeads(fox);

            Click("InvestigationChapterButton");
            yield return ContentReady.Routed();
            yield return null;
            var locked = Find<Button>("ChapterButton1");
            Assert.That(locked.IsInteractable(), Is.False);
            Assert.That(Label(locked, "ChapterState").text, Is.EqualTo("LOCKED"));
            PointerClick(locked);
            Assert.That(controller.SelectedChapter, Is.SameAs(first));
            Assert.That(Selector().gameObject.activeInHierarchy, Is.True);
            Click("CloseChapterSelectorButton");
            yield return ContentReady.Routed();

            InitializeProfile(CompletedFox());
            yield return null;
            Assert.That(controller.SelectedChapter, Is.SameAs(second));
            Assert.That(controller.IsChapterCompleted(first.Id), Is.True);
            Assert.That(controller.IsChapterCompleted(second.Id), Is.False);
            AssertLeads(owl);

            var completed = CompletedFox();
            foreach (var entry in second.Entries)
                if (entry.IsStoryOnly) completed.CompleteStory(entry.StoryId);
                else completed.CompleteEncounter(entry.EncounterId);
            InitializeProfile(completed);
            yield return null;
            Assert.That(controller.SelectedChapter, Is.SameAs(second));
            Assert.That(controller.IsChapterCompleted(second.Id), Is.True);
            Assert.That(controller.Snapshot, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ChapterPointerSelectionFiltersLeadsAndEarlierChapterRemainsReplayable()
        {
            InitializeProfile(CompletedFox());
            yield return null;
            AssertHeader(second);
            AssertLeads(owl);
            var lead = Lead(owl);
            Click("InvestigationChapterButton");
            yield return ContentReady.Routed();
            yield return null;
            Assert.That(Label(Find<Button>("ChapterButton0"), "ChapterState").text, Is.EqualTo("COMPLETED"));
            Assert.That(Label(Find<Button>("ChapterButton1"), "ChapterState").text, Is.EqualTo("CURRENT"));
            AssertPointerCoveredBySelector(lead);
            Click("CloseChapterSelectorButton");
            yield return ContentReady.Routed();
            yield return null;
            Assert.That(Selector().gameObject.activeInHierarchy, Is.False);
            Assert.That(controller.SelectedChapter, Is.SameAs(second));
            AssertLeads(owl);

            Click("InvestigationChapterButton");
            yield return ContentReady.Routed();
            yield return null;
            Click("ChapterButton0");
            yield return ContentReady.Routed();
            yield return null;
            Assert.That(Selector().gameObject.activeInHierarchy, Is.False);
            Assert.That(controller.SelectedChapter, Is.SameAs(first));
            AssertHeader(first);
            AssertLeads(fox);
            PointerClick(Lead(fox));
            yield return ContentReady.Routed(controller);
            yield return null;
            Assert.That(controller.SelectedJourney, Is.SameAs(fox));
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Sequence));
            Assert.That(controller.Snapshot, Is.Null);
            Click("JourneyReturnButton");
            yield return ContentReady.Routed();
            yield return null;
            Assert.That(controller.SelectedChapter, Is.SameAs(second));
            Assert.That(Selector().gameObject.activeInHierarchy, Is.False);

            Click("InvestigationChapterButton");
            yield return ContentReady.Routed();
            yield return null;
            var departingSelector = Selector();
            controller.SelectEncounter(owl.Id);
            yield return ContentReady.Routed(controller);
            yield return null;
            Assert.That(departingSelector == null || !departingSelector.gameObject.activeInHierarchy, Is.True,
                "Leaving the board must close or unload its selector.");
            Click("JourneyReturnButton");
            yield return ContentReady.Routed();
            yield return null;
            Assert.That(Selector().gameObject.activeInHierarchy, Is.False,
                "Leaving the board must not reopen its chapter selector on return.");
            AssertLeads(owl);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BrowsingAnotherChapterPreservesTheResumableEncounter()
        {
            InitializeProfile(CompletedFox());
            yield return null;
            Click("InvestigationChapterButton");
            yield return ContentReady.Routed();
            yield return null;
            Click("ChapterButton0");
            yield return ContentReady.Routed();
            yield return null;
            PointerClick(Lead(fox));
            yield return ContentReady.Routed(controller);
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                controller.AdvanceJourney();
            yield return null;
            Click("JourneyAcceptButton");
            yield return ContentReady.Routed();
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo(fox.EncounterId));
            var snapshot = controller.Snapshot;
            int resolutions = 0;
            controller.Resolved += _ => ++resolutions;
            controller.Save();
            Assert.That(File.Exists(controller.SavePath), Is.True, controller.StatusMessage);
            string savedRun = File.ReadAllText(controller.SavePath);
            Click("ReturnToInvestigationButton");
            yield return ContentReady.Routed();
            yield return null;
            Assert.That(controller.SelectedChapter, Is.SameAs(second));
            Assert.That(controller.CanResume, Is.True);
            AssertLeads(owl);

            Click("InvestigationChapterButton");
            yield return ContentReady.Routed();
            yield return null;
            Click("ChapterButton0");
            yield return ContentReady.Routed();
            yield return null;
            AssertLeads(fox);
            Click("InvestigationChapterButton");
            yield return ContentReady.Routed();
            yield return null;
            Find<ScrollRect>("ChapterSelectorScroll").verticalNormalizedPosition = 0;
            yield return null;
            Click("ChapterButton1");
            yield return ContentReady.Routed();
            yield return null;
            AssertLeads(owl);
            Assert.That(controller.Snapshot, Is.SameAs(snapshot));
            Assert.That(File.ReadAllText(controller.SavePath), Is.EqualTo(savedRun));
            Click("ResumeEncounterButton");
            yield return ContentReady.Routed();
            yield return null;
            Assert.That(controller.SelectedJourney, Is.SameAs(fox));
            Assert.That(controller.SelectedChapter, Is.SameAs(first));
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.Snapshot, Is.SameAs(snapshot));
            Assert.That(controller.BeatIndex, Is.EqualTo(-1));
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(view == null || !view.DiceTray.IsRolling, Is.True);
            Assert.That(resolutions, Is.Zero);
            Assert.That(File.ReadAllText(controller.SavePath), Is.EqualTo(savedRun));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ChapterBoardAndSelectorFitBothPortraitSizes()
        {
            foreach (var size in new[] { new Vector2(443, 912), new Vector2(540, 960) })
            {
                InitializeProfile(CompletedFox());
                var portrait = ContentReady.Portrait();
                portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                portrait.sizeDelta = size;
                portrait.anchoredPosition = Vector2.zero;
                yield return null;
                yield return new WaitForEndOfFrame();
                Canvas.ForceUpdateCanvases();
                AssertHeader(second);
                AssertCompleteText(Find<Text>("InvestigationTitle"));
                AssertCompleteText(Find<Text>("InvestigationSubtitle"));
                AssertCompleteText(Label(Lead(owl), "ClueTitle"));
                AssertCompleteText(Label(Lead(owl), "ClueLead"));
                AssertCompleteText(Label(Lead(owl), "ClueState"));
                yield return Capture("chapter-board-" + (int)size.x);
                Click("InvestigationChapterButton");
                yield return ContentReady.Routed();
                yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (var label in Selector().GetComponentsInChildren<Text>()) AssertCompleteText(label);
                AssertWithin(Find<Button>("CloseChapterSelectorButton").transform as RectTransform, portrait);
                foreach (string name in new[] { "ChapterButton0", "ChapterButton1" })
                {
                    var button = Find<Button>(name);
                    foreach (var label in button.GetComponentsInChildren<Text>())
                        AssertWithin(label.rectTransform, button.transform as RectTransform);
                }
                yield return Capture("chapter-selector-" + (int)size.x);
                Click("ChapterButton0");
                yield return ContentReady.Routed();
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertHeader(first);
                AssertLeads(fox);
                AssertCompleteText(Find<Text>("InvestigationTitle"));
                AssertCompleteText(Find<Text>("InvestigationSubtitle"));
                AssertCompleteText(Label(Lead(fox), "ClueTitle"));
                AssertCompleteText(Label(Lead(fox), "ClueLead"));
                yield return Capture("chapter-replay-board-" + (int)size.x);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private EncounterChapterAsset CreateChapter(string id, string title, string description, EncounterJourneyAsset[] entries)
        {
            var chapter = ScriptableObject.CreateInstance<EncounterChapterAsset>();
            SetField(chapter, "id", id);
            SetField(chapter, "title", title);
            SetField(chapter, "description", description);
            SetField(chapter, "artwork", entries[0].CardArtwork);
            SetField(chapter, "entries", entries);
            return chapter;
        }

        private PlayerProfile CompletedFox()
        {
            var profile = new PlayerProfile();
            profile.CompleteEncounter(fox.EncounterId);
            return profile;
        }

        private void InitializeProfile(PlayerProfile profile)
        {
            string path = ContentReady.SpiritSavePath();
            PlayerProfileFileStore.Save(Path.Combine(Path.GetDirectoryName(path), "spirit-profile.json"), profile);
            Assert.That(controller.InitializeJourney(controller.Catalog, catalog, path), Is.True, controller.JourneyStatus);
        }

        private void AssertHeader(EncounterChapterAsset chapter)
        {
            Assert.That(Find<Text>("InvestigationTitle").text, Is.EqualTo(chapter.Title));
            Assert.That(Find<Text>("InvestigationSubtitle").text, Is.EqualTo(chapter.Description));
        }

        private void AssertLeads(params EncounterJourneyAsset[] expected)
        {
            var titles = Board.Content.GetComponentsInChildren<SpiritJourneyCard>()
                .Select(value => value.Title.text).ToArray();
            Assert.That(titles, Is.EquivalentTo(expected.Select(value => value.LocationTitle)));
        }

        private Button Lead(EncounterJourneyAsset journey) => Board.Content.GetComponentsInChildren<SpiritJourneyCard>()
            .Single(value => value.Title.text == journey.LocationTitle).Button;

        private RectTransform Selector() => Board.ChapterSelector.Root;

        private static Text Label(Component parent, string name)
        {
            var card = parent.GetComponent<SpiritJourneyCard>();
            if (card != null)
            {
                if (name == "ChapterState" || name == "ClueState") return card.State;
                if (name == "ClueTitle") return card.Title;
                if (name == "ClueLead") return card.Description;
            }
            return parent.GetComponentsInChildren<Text>().Single(value => value.name == name);
        }

        private T Find<T>(string name) where T : Component
        {
            Component reference = null;
            if (ContentReady.FrontendView(controller) != null)
            {
                if (name == "InvestigationTitle") reference = Board.Title;
                else if (name == "InvestigationSubtitle") reference = Board.Description;
                else if (name == "InvestigationChapterButton") reference = Board.ChapterButton;
                else if (name == "CloseChapterSelectorButton") reference = Board.ChapterSelector.CloseButton;
                else if (name == "ChapterSelectorScroll") reference = Board.ChapterSelector.Scroll;
                else if (name.StartsWith("ChapterButton", StringComparison.Ordinal))
                {
                    int index = int.Parse(name.Substring("ChapterButton".Length));
                    var cards = Board.ChapterSelector.Content.GetComponentsInChildren<SpiritJourneyCard>();
                    Assert.That(index, Is.LessThan(cards.Length), name);
                    reference = cards[index].Button;
                }
            }
            var result = reference as T ?? ContentReady.PresentationRoot(controller)
                .GetComponentsInChildren<T>(true).FirstOrDefault(value => value.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }

        private void Click(string name)
        {
            var button = Find<Button>(name);
            Assert.That(button.gameObject.activeInHierarchy, Is.True, name);
            Assert.That(button.IsInteractable(), Is.True, name);
            PointerClick(button);
        }

        private static PointerEventData PointerFor(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            return new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left,
                pointerId = -1
            };
        }

        private static void PointerClick(Button button)
        {
            var pointer = PointerFor(button);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, button.name);
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.SameAs(button.gameObject), "A graphic blocks " + button.name);
            pointer.pointerCurrentRaycast = hits[0];
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }

        private void AssertPointerCoveredBySelector(Button button)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(PointerFor(button), hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.transform.IsChildOf(Selector()), Is.True,
                "The chapter selector must receive input over the investigation leads.");
        }

        private static void AssertCompleteText(Text label)
        {
            var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            var rendered = label.cachedTextGenerator;
            Assert.That(rendered.Populate(label.text, settings), Is.True, label.name);
            settings.fontSize = label.resizeTextForBestFit ? rendered.fontSizeUsedForBestFit : label.fontSize;
            settings.resizeTextForBestFit = false;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            using (var complete = new TextGenerator())
            {
                Assert.That(complete.Populate(label.text, settings), Is.True, label.name);
                Assert.That(rendered.vertexCount, Is.EqualTo(complete.vertexCount), label.name + " truncates text.");
            }
        }

        private static void AssertWithin(RectTransform rect, RectTransform parent)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector2 local = parent.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(parent.rect.xMin - 1, parent.rect.xMax + 1), rect.name);
                Assert.That(local.y, Is.InRange(parent.rect.yMin - 1, parent.rect.yMax + 1), rect.name);
            }
        }

        private IEnumerator Capture(string name)
        {
            Directory.CreateDirectory(captureRoot);
            string path = Path.Combine(captureRoot, name + ".png");
            Assert.That(File.Exists(path), Is.False, path);
            ScreenCapture.CaptureScreenshot(path);
            for (int frame = 0; frame < 120 && !File.Exists(path); ++frame) yield return null;
            Assert.That(File.Exists(path), Is.True, path);
        }

        private static void SetField(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
