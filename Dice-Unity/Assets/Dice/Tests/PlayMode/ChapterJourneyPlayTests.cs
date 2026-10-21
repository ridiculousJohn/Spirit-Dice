using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class ChapterJourneyPlayTests
    {
        private SpiritEncounterController controller;
        private SpiritEncounterView view => ContentReady.EncounterView(controller);
        private EncounterJourneyAsset chapter;
        private int resolutions;

        [UnitySetUp]
        public IEnumerator InvestigationReady()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            chapter = controller.JourneyCatalog.Entries.Single(value => value.IsStoryOnly);
            resolutions = 0;
            controller.Resolved += resolution => ++resolutions;
        }

        [UnityTest]
        public IEnumerator ChapterLeadAppearsOnlyAfterMothCompletion()
        {
            Assert.That(chapter.RequiredCompletedEncounterIds, Does.Contain("encounter.moth.crossroads"));
            Assert.That(controller.IsJourneyAvailable(chapter.Id), Is.False);
            Assert.That(ChapterCard(), Is.Null);
            controller.SelectEncounter(chapter.Id);
            yield return ContentReady.Routed(controller);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            AssertNoEncounter();

            var profile = CompletedPrerequisites(false);
            InitializeProfile(profile);
            yield return null;
            Assert.That(controller.IsJourneyAvailable(chapter.Id), Is.False);
            Assert.That(ChapterCard(), Is.Null);

            profile.CompleteEncounter("encounter.moth.crossroads");
            InitializeProfile(profile);
            yield return null;
            Assert.That(controller.IsJourneyAvailable(chapter.Id), Is.True);
            Assert.That(ChapterCard(), Is.Not.Null);
            Assert.That(ChapterCard().GetComponentsInChildren<Text>().Single(value => value.name == "ClueState").text,
                Is.EqualTo("FOLLOW THIS LEAD"));
            AssertNoEncounter();
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AuthoredChapterFitsPortraitAndCompletesWithoutCreatingAnEncounter()
        {
            var rewards = chapter.Beats.Where(value => value.ProgressionReward != null)
                .Select(value => value.ProgressionReward.Build(controller.Catalog)).ToList();
            if (chapter.CompletionReward != null) rewards.Add(chapter.CompletionReward.Build(controller.Catalog));
            var gifts = rewards.SelectMany(value => value.GrantedModifierIds).Distinct().ToArray();
            var discoveries = rewards.SelectMany(value => value.DiscoveredFamilyIds)
                .Concat(gifts.Select(id => controller.Catalog.GetModifier(id).FamilyId)).Distinct().ToArray();
            Assert.That(gifts, Is.Not.Empty, "The chapter must grant its authored permanent charms.");
            var encounterRewards = controller.JourneyCatalog.Entries.Where(value => !value.IsStoryOnly)
                .SelectMany(value => value.Beats.Where(beat => beat.ProgressionReward != null)
                    .Select(beat => beat.ProgressionReward.Build(controller.Catalog))
                    .Concat(value.CompletionReward == null ? Array.Empty<ProgressionRewardDefinition>() :
                        new[] { value.CompletionReward.Build(controller.Catalog) })).ToArray();
            var encounterFamilies = encounterRewards.SelectMany(value => value.DiscoveredFamilyIds)
                .Concat(encounterRewards.SelectMany(value => value.GrantedModifierIds)
                    .Select(id => controller.Catalog.GetModifier(id).FamilyId)).Distinct().ToArray();
            Assert.That(gifts.Any(id => !encounterFamilies.Contains(controller.Catalog.GetModifier(id).FamilyId)), Is.True,
                "The chapter must include a family beyond the encounter reveals and gifts.");

            foreach (var size in new[] { new Vector2(443, 912), new Vector2(540, 960) })
            {
                InitializeProfile(CompletedPrerequisites(true));
                yield return ContentReady.Routed(controller);
                var portrait = ContentReady.Portrait();
                portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                portrait.sizeDelta = size;
                portrait.anchoredPosition = Vector2.zero;
                yield return null;
                Canvas.ForceUpdateCanvases();
                var card = ChapterCard();
                Assert.That(card, Is.Not.Null);
                AssertCompleteText(card.GetComponentsInChildren<Text>().Single(value => value.name == "ClueTitle"));
                AssertCompleteText(card.GetComponentsInChildren<Text>().Single(value => value.name == "ClueLead"));
                card.onClick.Invoke();
                yield return ContentReady.Routed();
                portrait = ContentReady.Portrait();
                portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                portrait.sizeDelta = size;
                portrait.anchoredPosition = Vector2.zero;
                Assert.That(controller.SelectedJourney, Is.SameAs(chapter));
                AssertNoEncounter();

                while (controller.JourneyPhase == EncounterJourneyPhase.Sequence)
                {
                    int index = controller.BeatIndex;
                    float deadline = Time.realtimeSinceStartup + chapter.Beats[index].Duration + 10;
                    while (view.JourneyView.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(view.JourneyView.IsAnimating, Is.False);
                    if (controller.BeatIndex != index) continue;
                    yield return new WaitForEndOfFrame();
                    Canvas.ForceUpdateCanvases();
                    Assert.That(Find<Text>("JourneyTitle").text, Is.EqualTo(chapter.Beats[index].Title));
                    AssertCharmPresentation(view, chapter.Beats[index], controller.Catalog);
                    AssertCompleteText(Find<Text>("JourneyTitle"));
                    AssertCompleteText(Find<Text>("JourneyBody"));
                    Assert.That(Find<Button>("JourneyAcceptButton").gameObject.activeInHierarchy, Is.False);
                    Assert.That(Find<Button>("JourneySkipButton").gameObject.activeInHierarchy, Is.False);
                    Assert.That(controller.CanSkipIntro, Is.False);
                    AssertNoEncounter();
                    if (index == chapter.Beats.Count - 1)
                        Assert.That(controller.CompletedStoryIds, Does.Not.Contain(chapter.Id));
                    yield return Capture("chapter-" + (int)size.x + "-" + chapter.Beats[index].Id);
                    Click("JourneyAdvanceButton");
                    yield return ContentReady.Routed();
                }

                Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
                Assert.That(controller.CompletedStoryIds, Does.Contain(chapter.Id));
                var saved = PlayerProfileFileStore.Load(controller.ProfilePath);
                Assert.That(saved.CompletedStoryIds, Is.EqualTo(new[] { chapter.Id }));
                foreach (string id in gifts)
                {
                    Assert.That(saved.UnlockedModifierIds, Does.Contain(id));
                    Assert.That(SpiritCollectionView.GetSourceText(controller.Catalog.GetModifier(id),
                        controller.Catalog, controller.JourneyCatalog), Does.Contain(chapter.LocationTitle));
                }
                foreach (string id in discoveries) Assert.That(saved.DiscoveredFamilyIds, Does.Contain(id));
                AssertNoEncounter();
                yield return null;
                // Completing the story unlocks the following chapter, which the board opens as the latest available
                // incomplete chapter; the story remains readable from its own chapter.
                var storyChapter = StoryChapter();
                var unlocked = controller.JourneyCatalog.Chapters.Where(value => value != null && value != storyChapter &&
                    value.Entries.Any(journey => journey != null && journey.RequiredCompletedStoryIds.Contains(chapter.Id))).ToArray();
                Assert.That(unlocked, Is.Not.Empty, "The story must unlock a following chapter.");
                Assert.That(unlocked, Does.Contain(controller.SelectedChapter));
                Assert.That(controller.IsChapterAvailable(controller.SelectedChapter.Id), Is.True);
                Assert.That(controller.IsChapterCompleted(controller.SelectedChapter.Id), Is.False);
                Assert.That(controller.IsChapterCompleted(storyChapter.Id), Is.True);
                Assert.That(ChapterCard(), Is.Null, "The completed story belongs to its own chapter's board.");
                controller.SelectChapter(storyChapter.Id);
                yield return ContentReady.Routed();
                yield return null;
                Assert.That(controller.SelectedChapter, Is.SameAs(storyChapter));
                Assert.That(ChapterCard(), Is.Not.Null);
                Assert.That(ChapterCard().GetComponentsInChildren<Text>().Single(value => value.name == "ClueState").text,
                    Is.EqualTo("READ AGAIN"));
                Assert.That(ContentReady.PresentationRoot(controller).GetComponentsInChildren<Text>().Any(value => value.name == "CompletionClueText" &&
                    value.text == chapter.CompletionClueText), Is.True);

                string profileBeforeReplay = File.ReadAllText(controller.ProfilePath);
                ChapterCard().onClick.Invoke();
                yield return ContentReady.Routed();
                portrait = ContentReady.Portrait();
                portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                portrait.sizeDelta = size;
                portrait.anchoredPosition = Vector2.zero;
                while (controller.JourneyPhase == EncounterJourneyPhase.Sequence)
                {
                    int index = controller.BeatIndex;
                    float deadline = Time.realtimeSinceStartup + chapter.Beats[index].Duration + 10;
                    while (view.JourneyView.IsAnimating && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(view.JourneyView.IsAnimating, Is.False);
                    if (controller.BeatIndex == index) Click("JourneyAdvanceButton");
                    AssertNoEncounter();
                }
                yield return ContentReady.Routed(controller);
                Assert.That(File.ReadAllText(controller.ProfilePath), Is.EqualTo(profileBeforeReplay));
                Assert.That(controller.IsJourneyAvailable(chapter.Id), Is.True);
                AssertNoEncounter();
            }
            LogAssert.NoUnexpectedReceived();
        }

        private PlayerProfile CompletedPrerequisites(bool includeMoth)
        {
            var profile = new PlayerProfile();
            foreach (var journey in StoryChapter().Entries.Where(value => !value.IsStoryOnly))
                if (includeMoth || journey.EncounterId != "encounter.moth.crossroads")
                    profile.CompleteEncounter(journey.EncounterId);
            foreach (string id in chapter.RequiredCompletedStoryIds) profile.CompleteStory(id);
            return profile;
        }

        private void InitializeProfile(PlayerProfile profile)
        {
            string path = ContentReady.SpiritSavePath();
            PlayerProfileFileStore.Save(Path.Combine(Path.GetDirectoryName(path), "spirit-profile.json"), profile);
            Assert.That(controller.InitializeJourney(controller.Catalog, controller.JourneyCatalog, path), Is.True,
                controller.JourneyStatus);
        }

        private EncounterChapterAsset StoryChapter() =>
            controller.JourneyCatalog.Chapters.Single(value => value != null && value.Entries.Contains(chapter));

        private Button ChapterCard() => ContentReady.PresentationRoot(controller).GetComponentsInChildren<Button>().FirstOrDefault(value =>
            value.name.StartsWith("ClueButton", StringComparison.Ordinal) &&
            value.GetComponentsInChildren<Text>().Any(label => label.name == "ClueTitle" && label.text == chapter.LocationTitle));

        private void AssertNoEncounter()
        {
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.Preview, Is.Null);
            Assert.That(controller.HasActiveEncounter, Is.False);
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(view == null || !view.DiceTray.IsRolling, Is.True);
            Assert.That(resolutions, Is.Zero);
            Assert.That(typeof(SpiritEncounterController).GetField("application", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller), Is.Null);
            Assert.That(File.Exists(controller.SavePath), Is.False);
        }

        private T Find<T>(string name) where T : Component
        {
            var result = ContentReady.PresentationRoot(controller).GetComponentsInChildren<T>(true).FirstOrDefault(value => value.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }

        private void Click(string name)
        {
            var button = Find<Button>(name);
            Assert.That(button.gameObject.activeInHierarchy, Is.True, name);
            Assert.That(button.IsInteractable(), Is.True, name);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left,
                pointerId = -1
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, name);
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.SameAs(button.gameObject), "A graphic blocks " + name);
            pointer.pointerCurrentRaycast = hits[0];
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }

        internal static void AssertCharmPresentation(SpiritEncounterView view, EncounterJourneyBeat beat, MechanicalCatalog catalog)
        {
            var sequence = view.JourneyView;
            var body = sequence.Body;
            var detail = sequence.CharmDetail;
            Assert.That(detail.gameObject.activeInHierarchy, Is.EqualTo(beat.Charm != null));
            if (beat.Charm == null)
            {
                Assert.That(body.text, Is.EqualTo(beat.Text));
                return;
            }
            var definition = catalog.GetModifier(beat.Charm.Id);
            string name = SpiritModifierPresentation.GetName(definition);
            var theme = body.GetComponentInParent<SpiritUiContext>().Theme;
            Assert.That(body.text, Is.EqualTo(beat.Text.Replace("{charm}", "<b><color=#" +
                ColorUtility.ToHtmlStringRGB(theme.amber) + ">" + SpiritModifierFamilyRow.GetFamilyName(definition) + "</color></b>")));
            Assert.That(sequence.CharmName.text, Is.EqualTo(name));
            var effect = sequence.CharmEffect;
            Assert.That(effect.text, Does.Contain(SpiritModifierPresentation.GetEffectText(definition)));
            Assert.That(effect.color, Is.EqualTo(SpiritModifierIcons.GetEffectColor(definition)));
            Assert.That(sequence.CharmCondition.text,
                Is.EqualTo(SpiritRewardCard.GetModifierDescription(definition, theme.amber,
                    theme.GetModifierTriggerColor(definition.Phase))));
            Graphic icon = sequence.CharmIcon.gameObject.activeInHierarchy
                ? sequence.CharmIcon : sequence.CharmSigil;
            Assert.That(icon.gameObject.activeInHierarchy, Is.True, "The charm detail requires a visible icon.");
            Assert.That(icon.color, Is.EqualTo(SpiritModifierIcons.GetEffectColor(definition)));
            var bodyCorners = new Vector3[4];
            var detailCorners = new Vector3[4];
            body.rectTransform.GetWorldCorners(bodyCorners);
            detail.GetWorldCorners(detailCorners);
            Assert.That(bodyCorners[0].y, Is.GreaterThan(detailCorners[1].y), "Charm details must sit below the narration.");
            foreach (var label in detail.GetComponentsInChildren<Text>())
            {
                AssertCompleteText(label);
                var corners = new Vector3[4];
                label.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector2 local = detail.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(detail.rect.xMin - 1, detail.rect.xMax + 1), label.name);
                    Assert.That(local.y, Is.InRange(detail.rect.yMin - 1, detail.rect.yMax + 1), label.name);
                }
            }
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

        private static IEnumerator Capture(string name)
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) yield break;
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, name + ".png");
            Assert.That(File.Exists(path), Is.False, path);
            ScreenCapture.CaptureScreenshot(path);
            for (int frame = 0; frame < 120 && !File.Exists(path); ++frame) yield return null;
            Assert.That(File.Exists(path), Is.True, path);
        }
    }
}
