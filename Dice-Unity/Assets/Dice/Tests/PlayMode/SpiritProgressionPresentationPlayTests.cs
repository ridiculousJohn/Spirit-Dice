using System;
using System.Collections;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class SpiritProgressionPresentationPlayTests
    {
        [UnityTest]
        public IEnumerator ExhaustedUnlockListShowsContinueAndCompletesTheEncounter()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit(false);
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = controller.GetComponent<SpiritEncounterView>();
            var definition = controller.Catalog.Modifiers.First(value => value.Availability.Access == ModifierAccess.PermanentUnlock);
            var encounter = new EncounterDefinition("encounter.collected", new[] { new StageDefinition(1, 1, 0) },
                unlockMilestones: new[] { new ModifierUnlockMilestone(1, Array.Empty<string>()) },
                unlockRewardModifierIds: new[] { definition.Id });
            var catalog = new MechanicalCatalog("collected.test", controller.Catalog.Hands, new[] { encounter }, new[] { definition });
            string path = ContentReady.SpiritSavePath();
            var profile = new PlayerProfile();
            profile.UnlockModifier(definition.Id);
            profile.DiscoverFamily(definition.FamilyId);
            PlayerProfileFileStore.Save(Path.Combine(Path.GetDirectoryName(path), "spirit-profile.json"), profile);
            controller.Initialize(catalog, encounter.Id, 60, path);
            var portrait = (RectTransform)view.transform.Find("SpiritCanvas/SafeArea/Portrait");
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(443, 912);
            controller.PlayHand();
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingUnlock));
            Assert.That(Find<Text>(view, "RewardTitle").text, Is.EqualTo("All charms collected"));
            var confirm = Find<Button>(view, "UpgradeHandButton");
            Assert.That(confirm.interactable, Is.True);
            Assert.That(confirm.GetComponentInChildren<Text>().text, Is.EqualTo("Continue"));
            yield return Capture("progression-collected-443");
            confirm.onClick.Invoke();
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(controller.Snapshot.NewlyUnlockedModifierIds, Is.Empty);
            deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MilestonePopupResultsAndMenuShowPermanentProgressAtNarrowPortraitSize()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = controller.GetComponent<SpiritEncounterView>();
            var definitions = controller.Catalog.Modifiers.Where(value =>
                value.Availability.Access == ModifierAccess.PermanentUnlock &&
                value.Availability.Scope == ModifierAvailabilityScope.Global).Take(6).ToArray();
            Assert.That(definitions.Length, Is.GreaterThanOrEqualTo(4));
            var encounter = new EncounterDefinition("encounter.progression-presentation",
                new[] { new StageDefinition(1, 1, 0) }, unlockMilestones:
                new[] { new ModifierUnlockMilestone(1, definitions.Select(value => value.Id)) },
                unlockRewardModifierIds: definitions.Select(value => value.Id));
            var catalog = new MechanicalCatalog("progression-presentation.v1", controller.Catalog.Hands,
                new[] { encounter }, definitions);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                "spirit-progression-presentation-tests", Guid.NewGuid().ToString("N"), "encounter.json"));
            controller.Initialize(catalog, encounter.Id, 60, path);
            Assert.That(controller.ApplyProgressionReward(new ProgressionRewardDefinition(
                definitions.Select(value => value.FamilyId).Distinct(), Array.Empty<string>())), Is.True);
            var portrait = view.transform.Find("SpiritCanvas/SafeArea/Portrait") as RectTransform;
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(443, 912);
            portrait.anchoredPosition = Vector2.zero;
            yield return null;
            controller.PlayHand();
            float deadline = Time.realtimeSinceStartup + 40;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingUnlock));
            var offer = controller.Snapshot.PendingReward;
            Assert.That(offer.Options.Count, Is.EqualTo(3));
            Assert.That(controller.ProfileUnlockedModifierIds, Is.Empty);
            var choice = catalog.GetModifier(offer.Options[0].DefinitionId);
            yield return Capture("progression-offer-443");
            controller.ChooseReward(offer.OfferId, offer.Options[0].OptionId);
            var popupTitle = Find<Text>(view, "StageIntroTitle");
            deadline = Time.realtimeSinceStartup + 40;
            while ((!popupTitle.gameObject.activeInHierarchy || popupTitle.text != "CHARM UNLOCKED") &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(popupTitle.gameObject.activeInHierarchy, Is.True);
            Assert.That(popupTitle.text, Is.EqualTo("CHARM UNLOCKED"));
            Assert.That(controller.IsBusy, Is.True);
            Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).UnlockedModifierIds.Count,
                Is.EqualTo(1), "The selected unlock must be committed before the popup plays.");
            var popupDetail = Find<Text>(view, "StageIntroDetail");
            Assert.That(popupDetail.text, Does.Contain(SpiritModifierPresentation.GetName(choice)));
            Assert.That(popupDetail.text, Does.Contain("Permanently unlocked / All encounters"));
            Canvas.ForceUpdateCanvases();
            AssertCompleteText(popupTitle);
            AssertCompleteText(popupDetail);
            yield return Capture("progression-unlock-443");
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            yield return null;
            var endingUnlocks = Find<Text>(view, "EndingUnlocks");
            Assert.That(endingUnlocks.gameObject.activeInHierarchy, Is.True);
            Assert.That(endingUnlocks.text, Does.Contain(SpiritModifierPresentation.GetName(choice)));
            Canvas.ForceUpdateCanvases();
            AssertCompleteText(endingUnlocks);
            var endingScroll = Find<ScrollRect>(view, "EndingUnlockScroll");
            Assert.That(endingScroll.content.rect.height, Is.GreaterThan(0));
            yield return Capture("progression-results-443");
            endingScroll.verticalNormalizedPosition = 0;
            yield return null;
            yield return Capture("progression-results-scrolled-443");
            foreach (var size in new[] { new Vector2(443, 912), new Vector2(540, 960) })
            {
                portrait.sizeDelta = size;
                yield return null;
                Canvas.ForceUpdateCanvases();
                var title = Find<Text>(view, "EndingTitle");
                var detail = Find<Text>(view, "EndingDetail");
                Assert.That(detail.text, Is.EqualTo("Total score  " + DiagnosticText.Number(controller.Snapshot.TotalScore)));
                AssertCompleteText(title);
                AssertCompleteText(detail);
                Assert.That(title.cachedTextGenerator.lineCount, Is.EqualTo(1));
                Assert.That(detail.cachedTextGenerator.lineCount, Is.EqualTo(1));
                AssertVerticalSeparation(title.rectTransform, (RectTransform)endingScroll.transform);
                AssertVerticalSeparation((RectTransform)endingScroll.transform,
                    Find<Button>(view, "EndingNewRunButton").GetComponent<RectTransform>());
            }
            portrait.sizeDelta = new Vector2(443, 912);
            endingScroll.verticalNormalizedPosition = 1;
            yield return null;
            Find<Button>(view, "MenuButton").onClick.Invoke();
            yield return null;
            var summary = Find<Text>(view, "ProfileSummary");
            Assert.That(summary.gameObject.activeInHierarchy, Is.True);
            Assert.That(summary.text, Is.EqualTo("1 charm permanently unlocked\n1 encounter completed"));
            AssertCompleteText(summary);
            var abandon = Find<Button>(view, "AbandonRunButton");
            var back = Find<Button>(view, "CloseMenuButton");
            var abandonCorners = new Vector3[4];
            var backCorners = new Vector3[4];
            ((RectTransform)abandon.transform).GetWorldCorners(abandonCorners);
            ((RectTransform)back.transform).GetWorldCorners(backCorners);
            Assert.That(abandonCorners[0].y, Is.GreaterThan(backCorners[2].y));
            yield return Capture("progression-menu-443");
            LogAssert.NoUnexpectedReceived();
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

        private static void AssertVerticalSeparation(RectTransform upper, RectTransform lower)
        {
            var upperCorners = new Vector3[4];
            var lowerCorners = new Vector3[4];
            upper.GetWorldCorners(upperCorners);
            lower.GetWorldCorners(lowerCorners);
            Assert.That(upperCorners[0].y, Is.GreaterThanOrEqualTo(lowerCorners[2].y),
                upper.name + " overlaps " + lower.name);
        }

        private static T Find<T>(Component root, string name) where T : Component
        {
            var component = root.GetComponentsInChildren<T>(true).FirstOrDefault(value => value.name == name);
            Assert.That(component, Is.Not.Null, name);
            return component;
        }

        private static IEnumerator Capture(string name)
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) yield break;
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, name + ".png");
            Assert.That(File.Exists(path), Is.False);
            ScreenCapture.CaptureScreenshot(path);
            for (int frame = 0; frame < 120 && !File.Exists(path); ++frame) yield return null;
            Assert.That(File.Exists(path), Is.True);
        }
    }
}
