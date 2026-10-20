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
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = ContentReady.EncounterView(controller);
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
            yield return ContentReady.Routed(controller);
            view = ContentReady.EncounterView(controller);
            var portrait = ContentReady.Portrait();
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
            yield return ContentReady.Routed();
            view = ContentReady.EncounterView(controller);
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
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = ContentReady.EncounterView(controller);
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
            yield return ContentReady.Routed(controller);
            view = ContentReady.EncounterView(controller);
            Assert.That(controller.ApplyProgressionReward(new ProgressionRewardDefinition(
                definitions.Select(value => value.FamilyId).Distinct(), Array.Empty<string>())), Is.True);
            var portrait = ContentReady.Portrait();
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
            Assert.That(view.GameplayHud.StageIntroPanel.gameObject.activeSelf, Is.False);
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            Assert.That(controller.Snapshot.NewlyUnlockedModifierIds, Is.EqualTo(new[] { choice.Id }));
            Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).UnlockedModifierIds.Count,
                Is.EqualTo(1), "The selected unlock must be committed before the results are presented.");
            deadline = Time.realtimeSinceStartup + 40;
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
            yield return ContentReady.Routed();
            view = ContentReady.EncounterView(controller);
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

        [UnityTest]
        public IEnumerator NonfinalMilestoneUnlockImmediatelyShowsTheCharmOffer()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var standard = controller.Catalog;
            var unlock = standard.Modifiers.First(value => value.RewardEligible &&
                value.Availability.Access == ModifierAccess.PermanentUnlock &&
                value.Availability.Scope == ModifierAvailabilityScope.Global);
            var definitions = standard.Modifiers.Where(value => value.RewardEligible &&
                    value.Availability.Access == ModifierAccess.Starter &&
                    value.Availability.Scope == ModifierAvailabilityScope.Global)
                .GroupBy(value => value.FamilyId).Select(family => family.First()).Take(3).ToArray();
            Assert.That(definitions.Length, Is.EqualTo(3));
            var pool = new ModifierRewardPoolDefinition("pool.unlock-transition", definitions.Select(value => value.Id));
            var policy = new RewardPolicyDefinition("rewards.unlock-transition",
                new[] { new ModifierRewardLayout(new[] { pool.Id, pool.Id, pool.Id }) }, 1, 1, 1, 3,
                new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) });
            var encounter = new EncounterDefinition("encounter.unlock-transition",
                new[] { new StageDefinition(1, 1, 0, StageExitBehavior.AwaitRewards), new StageDefinition(1000000, 1, 0) },
                rewardPolicy: policy, unlockMilestones: new[] { new ModifierUnlockMilestone(1, new[] { unlock.Id }) },
                unlockRewardModifierIds: new[] { unlock.Id });
            var catalog = new MechanicalCatalog("unlock-transition.v1", standard.Hands, new[] { encounter },
                definitions.Concat(new[] { unlock }), new[] { pool });
            controller.Initialize(catalog, encounter.Id, 60, ContentReady.SpiritSavePath());
            yield return ContentReady.Routed(controller);
            var view = ContentReady.EncounterView(controller);
            Assert.That(controller.ApplyProgressionReward(new ProgressionRewardDefinition(
                new[] { unlock.FamilyId }, Array.Empty<string>())), Is.True);
            controller.PlayHand();
            float deadline = Time.realtimeSinceStartup + 40;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingUnlock));
            var offer = controller.Snapshot.PendingReward;
            Assert.That(offer.Kind, Is.EqualTo(RewardKind.ModifierUnlock));
            Assert.That(offer.Options.Select(value => value.DefinitionId), Is.EqualTo(new[] { unlock.Id }));
            var panel = view.RewardsPanel;
            Assert.That(panel.Root.gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.ConfirmButton.IsInteractable(), Is.False);
            panel.Cards[0].Button.onClick.Invoke();
            Assert.That(controller.Snapshot.PendingReward.OfferId, Is.EqualTo(offer.OfferId));
            Assert.That(controller.ProfileUnlockedModifierIds, Is.Empty);
            Assert.That(panel.ConfirmButton.IsInteractable(), Is.True);
            panel.ConfirmButton.onClick.Invoke();
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingModifier));
            var nextOffer = controller.Snapshot.PendingReward;
            Assert.That(nextOffer.Kind, Is.EqualTo(RewardKind.Modifier));
            Assert.That(nextOffer.OfferId, Is.Not.EqualTo(offer.OfferId));
            Assert.That(nextOffer.Options.Count, Is.EqualTo(3));
            Assert.That(panel.Root.gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.Title.text, Is.EqualTo("Choose a charm"));
            Assert.That(panel.Cards.Count(value => value.Button.gameObject.activeInHierarchy), Is.EqualTo(3));
            Assert.That(panel.ConfirmButton.IsInteractable(), Is.False);
            Assert.That(view.GameplayHud.StageIntroPanel.gameObject.activeSelf, Is.False);
            Assert.That(controller.ProfileUnlockedModifierIds, Is.EqualTo(new[] { unlock.Id }));
            Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).UnlockedModifierIds,
                Is.EqualTo(new[] { unlock.Id }));
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
