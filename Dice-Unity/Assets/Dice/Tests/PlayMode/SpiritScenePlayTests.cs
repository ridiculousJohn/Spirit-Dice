using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    public sealed class SpiritScenePlayTests
    {
        [UnityTest]
        public IEnumerator ReusableRewardCardComparesUpgradedValuesAndRebindsItsChoice()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit();
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = controller.GetComponent<SpiritEncounterView>();
            long chosenOffer = 0;
            long chosenOption = 0;
            int choices = 0;
            var portrait = view.transform.Find("SpiritCanvas/SafeArea/Portrait") as RectTransform;
            var card = SpiritRewardCard.Create("TestRewardCard", portrait,
                (offer, option) => { chosenOffer = offer; chosenOption = option; ++choices; });
            var cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(.5f, .5f);
            cardRect.sizeDelta = new Vector2(376, 140);
            var hand = new HandDefinition("hand.reward-test", HandCategory.Pair, 17, 3, 7, 2, 6);
            card.RenderHandUpgrade(hand, 3, 11, 21);
            cardRect.sizeDelta = new Vector2(376, card.GetComponent<LayoutElement>().preferredHeight);
            Canvas.ForceUpdateCanvases();
            foreach (var label in card.GetComponentsInChildren<Text>()) AssertCompleteLabelInsideBounds(label);
            Assert.That(CardLabel(card.Button, "Condition").text, Is.EqualTo("Level 3"));
            Assert.That(CardLabel(card.Button, "Effects").text, Is.EqualTo("BASE 38\nMULT 9"));
            var pattern = card.GetComponentInChildren<SpiritHandPatternGraphic>();
            Assert.That(pattern, Is.Not.Null);
            Assert.That(pattern.Category, Is.EqualTo(HandCategory.Pair));
            Assert.That(pattern.raycastTarget, Is.False);
            card.SetHandUpgradeSelected(true);
            Assert.That(CardLabel(card.Button, "Condition").text, Is.EqualTo("Level 3 +1"));
            Assert.That(CardLabel(card.Button, "Effects").text, Is.EqualTo("BASE 38 +7\nMULT 9 +2"));
            card.SetHandUpgradeSelected(false);
            Assert.That(CardLabel(card.Button, "Condition").text, Is.EqualTo("Level 3"));
            Assert.That(CardLabel(card.Button, "Effects").text, Is.EqualTo("BASE 38\nMULT 9"));
            card.Button.onClick.Invoke();
            Assert.That(chosenOffer, Is.EqualTo(11));
            Assert.That(chosenOption, Is.EqualTo(21));

            card.RenderModifier(controller.Catalog.GetModifier("modifier.play_hand.no_extreme_faces.base_score"), 12, 22);
            Assert.That(CardLabel(card.Button, "Title").text, Is.EqualTo("Quiet Grove / Base"));
            Assert.That(CardLabel(card.Button, "Condition").text, Does.Contain("When playing a hand").And.Contain("No die shows"));
            Assert.That(CardLabel(card.Button, "Condition").text, Does.Not.Contain("Once per hand"));
            Assert.That(CardLabel(card.Button, "Effects").text, Is.EqualTo("<b>+15 base score</b>"));
            Assert.That(CardLabel(card.Button, "Status").gameObject.activeSelf, Is.False);
            card.SetModifierSelected(true);
            Assert.That(card.transform.Find("UpgradeSelectionMarker").gameObject.activeSelf, Is.True);
            card.SetModifierSelected(false);
            Assert.That(card.transform.Find("UpgradeSelectionMarker").gameObject.activeSelf, Is.False);
            Assert.That(pattern.gameObject.activeSelf, Is.False);
            foreach (string name in new[] { "CurrentHeader", "UpgradedHeader", "UpgradedValues" })
                Assert.That(CardLabel(card.Button, name).gameObject.activeSelf, Is.False, name);
            card.Button.onClick.Invoke();
            Assert.That(chosenOffer, Is.EqualTo(12));
            Assert.That(chosenOption, Is.EqualTo(22));
            Assert.That(choices, Is.EqualTo(2), "Rendering must replace the choice identity without adding click listeners.");
            foreach (var definition in controller.Catalog.Modifiers)
            {
                card.RenderModifier(definition, 13, 23);
                yield return null;
                cardRect.sizeDelta = new Vector2(376, card.GetComponent<LayoutElement>().preferredHeight);
                Canvas.ForceUpdateCanvases();
                foreach (string name in new[] { "Title", "Condition", "Effects" })
                {
                    var label = CardLabel(card.Button, name);
                    try { AssertCompleteLabelInsideBounds(label); }
                    catch (AssertionException exception)
                    {
                        throw new AssertionException(definition.Id + "/" + name + ": " + exception.Message);
                    }
                    var corners = new Vector3[4];
                    label.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                        Assert.That(cardRect.rect.Contains(cardRect.InverseTransformPoint(corner)), Is.True,
                            definition.Id + "/" + name + " extends outside the reward card.");
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PortraitSizesKeepScoreCaptionsAndActionLabelsVisible()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit();
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            Assert.That(controller, Is.Not.Null);
            var view = controller.GetComponent<SpiritEncounterView>();
            Assert.That(view, Is.Not.Null);
            string savePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                "spirit-file-tests", Guid.NewGuid().ToString("N"), "portrait-labels.json"));
            controller.Initialize(controller.Catalog, "encounter.standard", 60, savePath);
            yield return null;
            var portrait = view.transform.Find("SpiritCanvas/SafeArea/Portrait") as RectTransform;
            Assert.That(portrait, Is.Not.Null);
            foreach (var size in new[] { new Vector2(540, 960), new Vector2(443, 912) })
            {
                portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                portrait.sizeDelta = size;
                portrait.anchoredPosition = Vector2.zero;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(portrait.rect.width, Is.EqualTo(size.x).Within(.01f));
                Assert.That(portrait.rect.height, Is.EqualTo(size.y).Within(.01f));
                AssertPresentationLabelsVisible(view);
                AssertActionButtonsStacked(view);
                foreach (var counter in new[] { view.BaseScoreLabel, view.MultiplierLabel, view.HandScoreLabel })
                {
                    var caption = counter.transform.parent.Find(counter.name + "Caption").GetComponent<Text>();
                    AssertLabelGlyphsInsideBounds(caption);
                }
                AssertLabelGlyphsInsideBounds(view.RerollButton.GetComponentInChildren<Text>());
                AssertLabelGlyphsInsideBounds(view.PlayButton.GetComponentInChildren<Text>());
                var menu = FindButton(view, "MenuButton").GetComponentInChildren<Text>();
                AssertLabelGlyphsInsideBounds(menu);
                Assert.That(menu.cachedTextGenerator.lineCount, Is.EqualTo(1),
                    "The menu label must remain on one line at portrait size " + size);
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SceneButtonsPresentEngineDiceRewardsCharmsAndSaveResume()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit();
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            Assert.That(controller, Is.Not.Null);
            var view = controller.GetComponent<SpiritEncounterView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.enabled, Is.True);
            Assert.That(controller.Snapshot, Is.Not.Null);
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo("encounter.fox.introduction"));
            Assert.That(controller.Snapshot.StageCount, Is.EqualTo(10));
            string artifactRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                "spirit-file-tests", Guid.NewGuid().ToString("N")));
            string savePath = Path.Combine(artifactRoot, "playmode-encounter.json");
            controller.Initialize(controller.Catalog, "encounter.standard", 60, savePath);
            var expected = EncounterSession.CreateEncounter(controller.Catalog, "encounter.standard", 60,
                discoveredFamilyIds: controller.ProfileDiscoveredFamilyIds);
            int resolutions = 0;
            controller.Resolved += resolution => ++resolutions;
            yield return null;
            AssertSnapshot(expected.Snapshot, controller.Snapshot);
            AssertVisibleDice(view, expected.Snapshot.CurrentDice);
            AssertRerollPlates(view, 0);
            for (int frame = 0; frame < 3; ++frame) yield return null;
            AssertTrayRendered(view);
            foreach (var image in view.GetComponentsInChildren<Image>())
            {
                if (image.name != "TargetPanel" && image.name != "PlayButton") continue;
                TestContext.WriteLine(image.name + " image rect: " + image.rectTransform.rect);
                TestContext.WriteLine(image.name + " pixelsPerUnitMultiplier: " + image.pixelsPerUnitMultiplier);
                if (image.sprite == null) continue;
                TestContext.WriteLine(image.name + " sprite rect: " + image.sprite.rect);
                TestContext.WriteLine(image.name + " sprite border: " + image.sprite.border);
                TestContext.WriteLine(image.name + " sprite texture: " + image.sprite.texture.width + " x " +
                    image.sprite.texture.height);
            }
            var theme = view.UiTheme;
            if (theme != null)
                TestContext.WriteLine("Theme greenFrame spriteRect: " + theme.greenFrame.spriteRect);
            yield return CaptureIfRequested("initial");
            AssertPresentationLabelsVisible(view);
            AssertActionButtonsStacked(view);
            AssertHandRanksAndCharmsButtonsSeparated(view);
            AssertButtonDecorationsDoNotBlockPointer(view.RerollButton);
            AssertButtonDecorationsDoNotBlockPointer(view.PlayButton);

            ClickThroughEventSystem(view, "MenuButton");
            yield return CaptureIfRequested("menu");
            Assert.That(FindButton(view, "CloseMenuButton").gameObject.activeInHierarchy, Is.True);
            AssertPresentationLabelsVisible(view);
            ClickThroughEventSystem(view, "CloseMenuButton");
            yield return CaptureIfRequested("menu-closed");
            Assert.That(FindButton(view, "CloseMenuButton").gameObject.activeInHierarchy, Is.False);
            AssertSnapshot(expected.Snapshot, controller.Snapshot);

            ClickThroughEventSystem(view, "DieButton1");
            ClickThroughEventSystem(view, "DieButton4");
            yield return CaptureIfRequested("selected-slots");
            Assert.That(controller.SelectedMask, Is.EqualTo(9));
            AssertRerollPlates(view, 9);
            Assert.That(view.RerollButton.GetComponentInChildren<Text>().text, Is.EqualTo("Reroll"),
                "Selecting dice must preserve the reroll action label.");
            Assert.That(view.PlayButton.GetComponentInChildren<Text>().text, Is.EqualTo("Play Hand"));
            AssertPresentationLabelsVisible(view);
            var beforeRanks = controller.Snapshot;
            int beforeRanksResolutions = resolutions;
            ClickThroughEventSystem(view, "HandRanksButton");
            Assert.That(view.IsHandRanksOpen, Is.True);
            Assert.That(view.IsCharmsOpen, Is.False);
            AssertHandRanks(view, controller);
            yield return CaptureIfRequested("hand-ranks");
            AssertPresentationLabelsVisible(view);
            Assert.That(controller.Snapshot, Is.SameAs(beforeRanks));
            Assert.That(controller.SelectedMask, Is.EqualTo(9));
            Assert.That(resolutions, Is.EqualTo(beforeRanksResolutions));
            ClickThroughEventSystem(view, "CloseHandRanksButton");
            Assert.That(view.IsHandRanksOpen, Is.False);
            Assert.That(controller.Snapshot, Is.SameAs(beforeRanks));
            Assert.That(controller.SelectedMask, Is.EqualTo(9));
            string previousScore = view.HandScoreLabel.text;
            ClickThroughEventSystem(view, "RerollButton");
            Assert.That(controller.IsBusy, Is.True);
            Assert.That(view.DiceTray.IsRolling, Is.True);
            Assert.That(controller.SelectedMask, Is.Zero);
            Assert.That(view.HandScoreLabel.text, Is.EqualTo(previousScore));
            AssertSnapshot(expected.Execute(new RerollDiceAction(9), 0).Snapshot, controller.Snapshot);
            var committed = controller.Snapshot;

            FindButton(view, "DieButton2").onClick.Invoke();
            view.PlayButton.onClick.Invoke();
            view.RerollButton.onClick.Invoke();
            view.CharmsButton.onClick.Invoke();
            view.HandRanksButton.onClick.Invoke();
            controller.ToggleDie(1);
            controller.PlayHand();
            controller.StartNew();
            controller.Save();
            controller.Load();
            controller.Abandon();
            Assert.That(controller.Snapshot, Is.SameAs(committed));
            Assert.That(controller.SelectedMask, Is.Zero);
            Assert.That(view.IsCharmsOpen, Is.False);
            Assert.That(view.IsHandRanksOpen, Is.False);
            Assert.That(view.PlayButton.IsInteractable(), Is.False);
            Assert.That(File.Exists(savePath), Is.False);
            yield return CaptureIfRequested("rolling");
            yield return WaitForPresentation(controller, view, "rerolled");
            AssertVisibleDice(view, expected.Snapshot.CurrentDice);
            AssertRerollPlates(view, 0);
            Assert.That(controller.Preview.Score.HandScore, Is.EqualTo(152));
            Assert.That(view.BaseScoreLabel.text, Is.EqualTo("0"));
            Assert.That(view.MultiplierLabel.text, Is.EqualTo("0"));
            Assert.That(view.HandScoreLabel.text, Is.EqualTo("0"));

            ClickThroughEventSystem(view, "PlayButton");
            Assert.That(view.GetComponentInChildren<SpiritHostPortrait>().IsReacting, Is.True,
                "Scoring must trigger the host's physical and spirit response.");
            AssertSnapshot(expected.Execute(PlayHandAction.Instance, 1).Snapshot, controller.Snapshot);
            Assert.That(controller.IsBusy, Is.True);
            Assert.That(controller.Preview.Score.HandScore, Is.EqualTo(81));
            Assert.That(view.HandScoreLabel.text, Is.EqualTo("0"));
            yield return CaptureIfRequested("fox-reaction");
            yield return WaitForPresentation(controller, view, "hand-played");
            Assert.That(controller.Snapshot.RemainingThreshold, Is.EqualTo(8));
            Assert.That(view.BaseScoreLabel.text, Is.EqualTo("0"));
            Assert.That(view.MultiplierLabel.text, Is.EqualTo("0"));
            Assert.That(view.HandScoreLabel.text, Is.EqualTo("0"));
            AssertVisibleDice(view, expected.Snapshot.CurrentDice);

            Click(view, "PlayButton");
            AssertSnapshot(expected.Execute(PlayHandAction.Instance, 2).Snapshot, controller.Snapshot);
            yield return AssertStageClearReaction(controller, view);
            yield return WaitForPresentation(controller, view, "modifier-reward");
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingModifier));
            Assert.That(controller.Preview, Is.Null);
            yield return AssertCardTextVisible(view, "RewardCard", "Effects", "Condition");
            AssertModifierRewards(controller, view);
            yield return AssertRewardPortraitBounds(view, "charm-choice");

            var offer = controller.Snapshot.PendingReward;
            var firstRewardCard = FindButton(view, "RewardCard1");
            long previousOfferId = offer.OfferId;
            var beforeCharmSelection = controller.Snapshot;
            ClickThroughEventSystem(view, "RewardCard1");
            Assert.That(controller.Snapshot, Is.SameAs(beforeCharmSelection));
            AssertModifierRewards(controller, view, 0);
            Click(view, "RefreshRewardsButton");
            AssertSnapshot(expected.Execute(new RefreshModifierOfferAction(offer.OfferId), expected.Snapshot.Revision).Snapshot,
                controller.Snapshot);
            yield return WaitForPresentation(controller, view, "modifier-refreshed");
            yield return AssertCardTextVisible(view, "RewardCard", "Effects", "Condition");
            offer = controller.Snapshot.PendingReward;
            Assert.That(offer.OfferId, Is.Not.EqualTo(previousOfferId));
            Assert.That(FindButton(view, "RewardCard1"), Is.SameAs(firstRewardCard));
            AssertModifierRewards(controller, view);
            var selectCharmButton = FindButton(view, "UpgradeHandButton");
            var refreshedOfferSnapshot = controller.Snapshot;
            selectCharmButton.onClick.Invoke();
            Assert.That(controller.Snapshot, Is.SameAs(refreshedOfferSnapshot));
            ClickThroughEventSystem(view, "RewardCard2");
            AssertModifierRewards(controller, view, 1);
            Assert.That(controller.Snapshot, Is.SameAs(refreshedOfferSnapshot));
            controller.Save();
            controller.Load();
            yield return null;
            AssertModifierRewards(controller, view);
            AssertSnapshot(expected.Snapshot, controller.Snapshot);
            var loadedOfferSnapshot = controller.Snapshot;
            selectCharmButton.onClick.Invoke();
            Assert.That(controller.Snapshot, Is.SameAs(loadedOfferSnapshot));
            Click(view, "RewardCard1");
            Assert.That(controller.Snapshot, Is.SameAs(loadedOfferSnapshot));
            AssertModifierRewards(controller, view, 0);
            ClickThroughEventSystem(view, "UpgradeHandButton");
            AssertSnapshot(expected.Execute(new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId),
                expected.Snapshot.Revision).Snapshot, controller.Snapshot);
            yield return WaitForPresentation(controller, view, "hand-upgrade");
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingHandUpgrade));
            Assert.That(controller.Snapshot.Modifiers.Count, Is.EqualTo(1));
            yield return AssertCardTextVisible(view, "RewardCard", "Title", "Condition", "Effects");
            Assert.That(FindButton(view, "RewardCard1"), Is.SameAs(firstRewardCard));
            AssertHandUpgradeRewards(controller, view);
            yield return AssertRewardPortraitBounds(view, "hand-upgrade-unselected");

            var beforeRewardSave = controller.Snapshot;
            ClickThroughEventSystem(view, "RewardCard1");
            Assert.That(controller.Snapshot, Is.SameAs(beforeRewardSave));
            AssertHandUpgradeRewards(controller, view, 0);
            controller.Save();
            Assert.That(File.Exists(savePath), Is.True, controller.StatusMessage);
            var rewardSave = EncounterSaveFileStore.Load(savePath);
            Assert.That(EncounterSaveCodec.Serialize(rewardSave),
                Is.EqualTo(EncounterSaveCodec.Serialize(expected.CaptureSave())));
            int beforeRewardLoadResolutions = resolutions;
            controller.Load();
            yield return null;
            AssertSnapshot(expected.Snapshot, controller.Snapshot);
            Assert.That(resolutions, Is.EqualTo(beforeRewardLoadResolutions));
            Assert.That(controller.IsBusy, Is.False);
            AssertHandUpgradeRewards(controller, view);

            offer = controller.Snapshot.PendingReward;
            Assert.That(offer.Options.Count, Is.GreaterThan(1));
            var upgradeButton = FindButton(view, "UpgradeHandButton");
            var beforeUpgradeSelection = controller.Snapshot;
            int beforeUpgradeResolutions = resolutions;
            Assert.That(upgradeButton.IsInteractable(), Is.False);
            upgradeButton.onClick.Invoke();
            Assert.That(controller.Snapshot, Is.SameAs(beforeUpgradeSelection));
            ClickThroughEventSystem(view, "RewardCard1");
            Assert.That(controller.Snapshot, Is.SameAs(beforeUpgradeSelection));
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(resolutions, Is.EqualTo(beforeUpgradeResolutions));
            Assert.That(upgradeButton.IsInteractable(), Is.True);
            AssertHandUpgradeRewards(controller, view, 0);
            yield return CaptureIfRequested("hand-upgrade-selected");
            yield return AssertRewardPortraitBounds(view, "hand-upgrade-selected");
            ClickThroughEventSystem(view, "RewardCard2");
            Assert.That(controller.Snapshot, Is.SameAs(beforeUpgradeSelection));
            Assert.That(resolutions, Is.EqualTo(beforeUpgradeResolutions));
            AssertHandUpgradeRewards(controller, view, 1);
            yield return CaptureIfRequested("hand-upgrade-selection-changed");
            var upgradedCategory = offer.Options[1].Category.Value;
            int previousUpgradeLevel = controller.Snapshot.HandUpgrades[(int)upgradedCategory];
            ClickThroughEventSystem(view, "UpgradeHandButton");
            AssertSnapshot(expected.Execute(new SelectHandUpgradeAction(offer.OfferId, offer.Options[1].OptionId),
                expected.Snapshot.Revision).Snapshot, controller.Snapshot);
            Assert.That(resolutions, Is.EqualTo(beforeUpgradeResolutions + 1));
            yield return AssertStageIntroBlocksInput(controller, view, offer, savePath);
            yield return WaitForPresentation(controller, view, "next-stage");
            var afterUpgrade = controller.Snapshot;
            upgradeButton.onClick.Invoke();
            firstRewardCard.onClick.Invoke();
            Assert.That(controller.Snapshot, Is.SameAs(afterUpgrade), "Stale reward controls must not apply another upgrade.");
            Assert.That(resolutions, Is.EqualTo(beforeUpgradeResolutions + 1));
            Assert.That(controller.Snapshot.StageIndex, Is.EqualTo(1));
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.Playing));
            Assert.That(controller.ModifierInspections.Count, Is.EqualTo(1));
            AssertVisibleDice(view, expected.Snapshot.CurrentDice);
            Assert.That(controller.Snapshot.HandUpgrades[(int)upgradedCategory], Is.EqualTo(previousUpgradeLevel + 1));
            beforeRanks = controller.Snapshot;
            int beforeRanksSelection = controller.SelectedMask;
            beforeRanksResolutions = resolutions;
            ClickThroughEventSystem(view, "HandRanksButton");
            Assert.That(view.IsHandRanksOpen, Is.True);
            AssertHandRanks(view, controller);
            yield return CaptureIfRequested("hand-ranks-upgraded");
            AssertPresentationLabelsVisible(view);
            ClickThroughEventSystem(view, "CloseHandRanksButton");
            Assert.That(view.IsHandRanksOpen, Is.False);
            Assert.That(controller.Snapshot, Is.SameAs(beforeRanks));
            Assert.That(controller.SelectedMask, Is.EqualTo(beforeRanksSelection));
            Assert.That(resolutions, Is.EqualTo(beforeRanksResolutions));
            Click(view, "CharmsButton");
            Assert.That(view.IsCharmsOpen, Is.True);
            Click(view, "OwnedCharm1");
            yield return CaptureIfRequested("charms");
            yield return AssertCardTextVisible(view, "OwnedCharm", "Title", "Condition", "Effects");
            var badges = view.GetComponentsInChildren<SpiritCharmStatusBadge>();
            Assert.That(badges, Is.Not.Empty);
            foreach (var badge in badges)
            {
                Assert.That(badge.Icon, Is.Not.Null);
                Assert.That(badge.Icon.IsActive(), Is.True);
                Assert.That(badge.Icon.State, Is.EqualTo(badge.Presentation.State));
                Assert.That(badge.Icon.color.a, Is.GreaterThan(0));
                Assert.That(badge.Icon.rectTransform.rect.width, Is.GreaterThan(0));
                Assert.That(badge.Icon.rectTransform.rect.height, Is.GreaterThan(0));
            }
            AssertPresentationLabelsVisible(view);
            Click(view, "CloseCharmsButton");
            Assert.That(view.IsCharmsOpen, Is.False);

            Click(view, "MenuButton");
            Click(view, "SaveRunButton");
            Assert.That(File.Exists(savePath), Is.True, controller.StatusMessage);
            var saved = EncounterSaveFileStore.Load(savePath);
            Assert.That(EncounterSaveCodec.Serialize(saved), Is.EqualTo(EncounterSaveCodec.Serialize(expected.CaptureSave())));
            Click(view, "DieButton2");
            Click(view, "RerollButton");
            yield return WaitForPresentation(controller, view, "saved-run-rerolled");
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(saved.Revision + 1));
            Click(view, "DieButton3");
            int beforeLoadResolutions = resolutions;
            Click(view, "MenuButton");
            Click(view, "LoadRunButton");
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(view.DiceTray.IsRolling, Is.False);
            Assert.That(view.GetComponentInChildren<SpiritHostPortrait>().IsReacting, Is.False,
                "Loading a run must clear the previous host reaction.");
            Assert.That(resolutions, Is.EqualTo(beforeLoadResolutions));
            Assert.That(controller.SelectedMask, Is.Zero);
            AssertSnapshot(expected.Snapshot, controller.Snapshot);
            AssertVisibleDice(view, expected.Snapshot.CurrentDice);
            yield return CaptureIfRequested("loaded");
            AssertPresentationLabelsVisible(view);

            Click(view, "MenuButton");
            Click(view, "AbandonRunButton");
            yield return WaitForPresentation(controller, view, "completed");
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Abandoned));
            Assert.That(controller.Preview, Is.Null);
            int beforeNewRunResolutions = resolutions;
            Click(view, "EndingNewRunButton");
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            controller.StartNew();
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(view.DiceTray.IsRolling, Is.False);
            Assert.That(resolutions, Is.EqualTo(beforeNewRunResolutions));
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.Playing));
            Assert.That(controller.Snapshot.StageIndex, Is.Zero);
            Assert.That(controller.Snapshot.Revision, Is.Zero);
            Assert.That(controller.Snapshot.TotalScore, Is.Zero);
            Assert.That(controller.Snapshot.Modifiers.Count, Is.Zero);
            controller.Save();
            var newRunSave = EncounterSaveFileStore.Load(savePath);
            var originalRunSave = EncounterSession.CreateEncounter(controller.Catalog, "encounter.standard", 60).CaptureSave();
            Assert.That(newRunSave.RewardRandomState, Is.Not.EqualTo(originalRunSave.RewardRandomState),
                "New run must obtain a fresh seed instead of repeating the explicitly initialized run.");
            AssertVisibleDice(view, controller.Snapshot.CurrentDice);
            yield return CaptureIfRequested("new-run");
            AssertPresentationLabelsVisible(view);
            LogAssert.NoUnexpectedReceived();
            TestContext.WriteLine("Retained spirit save: " + savePath);
        }

        private static IEnumerator AssertStageClearReaction(SpiritEncounterController controller, SpiritEncounterView view)
        {
            var caption = FindLabel(view, "ToClearLabel");
            float deadline = Time.realtimeSinceStartup + 20f;
            while (controller.IsBusy && caption.text != "STAGE CLEARED" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(caption.text, Is.EqualTo("STAGE CLEARED"));
            Assert.That(controller.IsBusy, Is.True);
            var fox = view.GetComponentInChildren<SpiritHostPortrait>();
            Assert.That(fox.IsReacting, Is.True, "Clearing the stage must trigger its own host reaction.");
            Assert.That(FindButton(view, "RefreshRewardsButton").gameObject.activeInHierarchy, Is.False);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(controller.IsBusy, Is.True);
            Assert.That(fox.IsReacting, Is.True, "The reward panel must wait while the host reacts.");
            Assert.That(FindButton(view, "RefreshRewardsButton").gameObject.activeInHierarchy, Is.False);
        }

        private static IEnumerator AssertStageIntroBlocksInput(SpiritEncounterController controller, SpiritEncounterView view,
            RewardOffer offer, string savePath)
        {
            var intro = FindLabel(view, "StageIntroTitle");
            float deadline = Time.realtimeSinceStartup + 5f;
            while (controller.IsBusy && !intro.gameObject.activeInHierarchy && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(intro.gameObject.activeInHierarchy, Is.True, "A new stage must be introduced before rolling its dice.");
            Assert.That(controller.IsBusy, Is.True);
            Assert.That(view.DiceTray.IsRolling, Is.False);
            Assert.That(intro.text, Is.EqualTo("STAGE 2 / 25"));
            var stage = controller.Catalog.GetEncounter(controller.Snapshot.EncounterId).Stages[1];
            var detail = FindLabel(view, "StageIntroDetail");
            Assert.That(detail.text, Is.EqualTo("CLEAR " + DiagnosticText.Number(stage.Threshold) + " POINTS\n" +
                stage.StartingHands + " hands / " + stage.StartingRerolls + " rerolls"));
            Canvas.ForceUpdateCanvases();
            AssertCompleteLabelInsideBounds(intro);
            AssertCompleteLabelInsideBounds(detail);

            var committed = controller.Snapshot;
            string saved = File.ReadAllText(savePath);
            FindButton(view, "DieButton1").onClick.Invoke();
            view.PlayButton.onClick.Invoke();
            view.RerollButton.onClick.Invoke();
            FindButton(view, "RewardCard1").onClick.Invoke();
            FindButton(view, "UpgradeHandButton").onClick.Invoke();
            FindButton(view, "MenuButton").onClick.Invoke();
            view.CharmsButton.onClick.Invoke();
            view.HandRanksButton.onClick.Invoke();
            controller.ToggleDie(0);
            controller.PlayHand();
            controller.Reroll();
            controller.RefreshRewards();
            controller.ChooseReward(offer.OfferId, offer.Options[0].OptionId);
            controller.StartNew();
            controller.Save();
            controller.Load();
            controller.Abandon();
            Assert.That(controller.Snapshot, Is.SameAs(committed));
            Assert.That(controller.SelectedMask, Is.Zero);
            Assert.That(view.IsCharmsOpen, Is.False);
            Assert.That(view.IsHandRanksOpen, Is.False);
            Assert.That(FindButton(view, "CloseMenuButton").gameObject.activeInHierarchy, Is.False);
            Assert.That(view.PlayButton.IsInteractable(), Is.False);
            Assert.That(File.ReadAllText(savePath), Is.EqualTo(saved));
        }

        private static void AssertModifierRewards(SpiritEncounterController controller, SpiritEncounterView view,
            int selectedIndex = -1)
        {
            var offer = controller.Snapshot.PendingReward;
            Assert.That(FindLabel(view, "RewardTitle").text, Is.EqualTo("Choose a charm"));
            foreach (string name in new[] { "RewardStep", "RewardInstruction", "UpcomingStage" })
                Assert.That(FindLabel(view, name).gameObject.activeInHierarchy, Is.False, name);
            var selectButton = FindButton(view, "UpgradeHandButton");
            Assert.That(selectButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(selectButton.GetComponentInChildren<Text>().text, Is.EqualTo("Select"));
            Assert.That(selectButton.IsInteractable(), Is.EqualTo(selectedIndex >= 0));
            for (int index = 0; index < offer.Options.Count; ++index)
            {
                var card = FindButton(view, "RewardCard" + (index + 1));
                Assert.That(card.GetComponent<SpiritRewardCard>(), Is.Not.Null);
                var definition = controller.Catalog.GetModifier(offer.Options[index].DefinitionId);
                Assert.That(CardLabel(card, "Title").text, Is.EqualTo(SpiritModifierPresentation.GetName(definition)));
                var theme = card.GetComponentInParent<SpiritUiContext>().Theme;
                Assert.That(CardLabel(card, "Condition").text,
                    Is.EqualTo(SpiritRewardCard.GetModifierDescription(definition, theme.amber,
                        theme.GetModifierTriggerColor(definition.Phase))));
                Assert.That(CardLabel(card, "Effects").text, Is.EqualTo("<b>" + SpiritModifierPresentation.GetEffectText(definition) + "</b>"));
                Assert.That(CardLabel(card, "Status").gameObject.activeSelf, Is.False);
                Assert.That(card.transform.Find("UpgradeSelectionMarker").gameObject.activeSelf, Is.EqualTo(index == selectedIndex));
                foreach (string name in new[] { "CurrentHeader", "UpgradedHeader", "UpgradedValues" })
                    Assert.That(CardLabel(card, name).gameObject.activeSelf, Is.False, card.name + "/" + name);
            }
        }

        private static void AssertHandUpgradeRewards(SpiritEncounterController controller, SpiritEncounterView view,
            int selectedIndex = -1)
        {
            var offer = controller.Snapshot.PendingReward;
            Assert.That(offer.Kind, Is.EqualTo(RewardKind.HandUpgrade));
            Assert.That(FindLabel(view, "TargetLabel").gameObject.activeInHierarchy, Is.False);
            Assert.That(FindLabel(view, "ResourcesLabel").gameObject.activeInHierarchy, Is.False);
            Assert.That(FindLabel(view, "RewardTitle").text, Is.EqualTo("Choose a hand to upgrade"));
            foreach (string name in new[] { "RewardStep", "RewardInstruction", "UpcomingStage" })
                Assert.That(FindLabel(view, name).gameObject.activeInHierarchy, Is.False, name);
            var upgradeButton = FindButton(view, "UpgradeHandButton");
            Assert.That(upgradeButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(upgradeButton.IsInteractable(), Is.EqualTo(selectedIndex >= 0));
            Assert.That(view.RefreshButton.gameObject.activeInHierarchy, Is.False);
            int visibleCards = 0;
            foreach (var button in view.GetComponentsInChildren<Button>())
                if (button.name.StartsWith("RewardCard", StringComparison.Ordinal)) ++visibleCards;
            Assert.That(visibleCards, Is.EqualTo(offer.Options.Count));
            for (int index = 0; index < offer.Options.Count; ++index)
            {
                var card = FindButton(view, "RewardCard" + (index + 1));
                var category = offer.Options[index].Category.Value;
                var hand = controller.Catalog.GetHand(category);
                int level = controller.Snapshot.HandUpgrades[(int)category];
                bool selected = index == selectedIndex;
                Assert.That(CardLabel(card, "Title").text, Is.EqualTo(DiagnosticText.Humanize(category.ToString())));
                Assert.That(CardLabel(card, "Condition").text, Is.EqualTo("Level " + level + (selected ? " +1" : "")));
                Assert.That(CardLabel(card, "Effects").text,
                    Is.EqualTo("BASE " + DiagnosticText.Number(hand.BaseScore + level * hand.BaseScorePerUpgrade) +
                        (selected ? " +" + DiagnosticText.Number(hand.BaseScorePerUpgrade) : "") +
                        "\nMULT " + DiagnosticText.Number(hand.Multiplier + level * hand.MultiplierPerUpgrade) +
                        (selected ? " +" + DiagnosticText.Number(hand.MultiplierPerUpgrade) : "")));
                foreach (string name in new[] { "CurrentHeader", "UpgradedHeader", "UpgradedValues", "Status" })
                    Assert.That(CardLabel(card, name).gameObject.activeSelf, Is.False, card.name + "/" + name);
                var pattern = card.GetComponentInChildren<SpiritHandPatternGraphic>();
                Assert.That(pattern, Is.Not.Null, card.name);
                Assert.That(pattern.Category, Is.EqualTo(category));
                Assert.That(pattern.raycastTarget, Is.False);
                Assert.That(card.transform.Find("UpgradeSelectionMarker").gameObject.activeSelf, Is.EqualTo(selected));
            }
        }

        private static IEnumerator AssertRewardPortraitBounds(SpiritEncounterView view, string captureName = null)
        {
            var portrait = view.transform.Find("SpiritCanvas/SafeArea/Portrait") as RectTransform;
            var anchorMin = portrait.anchorMin;
            var anchorMax = portrait.anchorMax;
            var sizeDelta = portrait.sizeDelta;
            var position = portrait.anchoredPosition;
            foreach (var size in new[] { new Vector2(540, 960), new Vector2(443, 912), new Vector2(443, 960) })
            {
                portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                portrait.sizeDelta = size;
                portrait.anchoredPosition = Vector2.zero;
                yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (var label in view.GetComponentsInChildren<Text>())
                {
                    if (label.transform.parent.GetComponent<SpiritRewardCard>() != null && !string.IsNullOrEmpty(label.text))
                        AssertCompleteLabelInsideBounds(label);
                }
                AssertCompleteLabelInsideBounds(FindLabel(view, "RewardTitle"));
                foreach (string name in new[] { "RewardInstruction", "RewardStep", "UpcomingStage" })
                {
                    var label = FindLabel(view, name);
                    if (label.isActiveAndEnabled) AssertCompleteLabelInsideBounds(label);
                }
                var upgradeButton = FindButton(view, "UpgradeHandButton");
                if (upgradeButton.gameObject.activeInHierarchy)
                {
                    ScrollRect rewardScroll = null;
                    foreach (var scroll in view.GetComponentsInChildren<ScrollRect>())
                        if (scroll.name == "RewardScroll") { rewardScroll = scroll; break; }
                    Assert.That(rewardScroll, Is.Not.Null);
                    Assert.That(rewardScroll.content.rect.height,
                        Is.LessThanOrEqualTo(rewardScroll.viewport.rect.height + 1f),
                        "All offered rewards must fit without scrolling at portrait size " + size);
                    bool modifierOffer = view.RefreshButton.gameObject.activeInHierarchy;
                    Assert.That(rewardScroll.vertical, Is.EqualTo(!modifierOffer));
                    foreach (var card in view.GetComponentsInChildren<SpiritRewardCard>())
                    {
                        var rect = (RectTransform)card.transform;
                        Assert.That(rect.rect.height, Is.LessThanOrEqualTo(modifierOffer ? 180f : 110f),
                            card.name + " must remain compact.");
                        if (modifierOffer) Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(160f));
                        AssertRectInsideBounds(rect, rewardScroll.viewport);
                        foreach (var graphic in card.GetComponentsInChildren<Graphic>())
                            AssertRectInsideBounds(graphic.rectTransform, rect);
                    }
                    AssertRectInsideBounds((RectTransform)upgradeButton.transform, portrait);
                    AssertCompleteLabelInsideBounds(upgradeButton.GetComponentInChildren<Text>());
                    if (modifierOffer)
                    {
                        var refreshRect = (RectTransform)view.RefreshButton.transform;
                        var confirmRect = (RectTransform)upgradeButton.transform;
                        Assert.That(refreshRect.rect.height, Is.EqualTo(52f).Within(.1f));
                        Assert.That(refreshRect.position.y, Is.EqualTo(confirmRect.position.y).Within(.1f));
                        AssertRectInsideBounds(refreshRect, portrait);
                        AssertCompleteLabelInsideBounds(view.RefreshButton.GetComponentInChildren<Text>());
                    }
                    if (captureName != null)
                        yield return CaptureIfRequested(captureName + "-" + (int)size.x + "x" + (int)size.y);
                }
            }
            portrait.anchorMin = anchorMin;
            portrait.anchorMax = anchorMax;
            portrait.sizeDelta = sizeDelta;
            portrait.anchoredPosition = position;
            yield return null;
        }

        private static void AssertRectInsideBounds(RectTransform child, RectTransform parent)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            var bounds = parent.rect;
            foreach (var corner in corners)
            {
                var local = parent.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(bounds.xMin - 1f, bounds.xMax + 1f), child.name + " exceeds " + parent.name);
                Assert.That(local.y, Is.InRange(bounds.yMin - 1f, bounds.yMax + 1f), child.name + " exceeds " + parent.name);
            }
        }

        private static void AssertCompleteLabelInsideBounds(Text label)
        {
            // Decorative heading glyphs can overhang their text advance without clipping the padded card.
            AssertLabelGlyphsInsideBounds(label, 3f);
            var generated = label.cachedTextGenerator;
            var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.fontSize = label.resizeTextForBestFit ? generated.fontSizeUsedForBestFit : label.fontSize;
            settings.resizeTextForBestFit = false;
            using (var complete = new TextGenerator())
            {
                Assert.That(complete.Populate(label.text, settings), Is.True, label.name);
                Assert.That(generated.vertexCount, Is.EqualTo(complete.vertexCount),
                    label.name + " truncates glyphs or whole lines: " + label.text);
            }
        }

        private static Text CardLabel(Button card, string name)
        {
            var child = card.transform.Find(name);
            Assert.That(child, Is.Not.Null, card.name + "/" + name);
            var label = child.GetComponent<Text>();
            Assert.That(label, Is.Not.Null, card.name + "/" + name);
            return label;
        }

        private static Text FindLabel(SpiritEncounterView view, string name)
        {
            foreach (var label in view.GetComponentsInChildren<Text>(true))
                if (label.name == name) return label;
            Assert.Fail("Scene label was not found: " + name);
            return null;
        }

        private static IEnumerator WaitForPresentation(SpiritEncounterController controller, SpiritEncounterView view,
            string captureName)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False, "Encounter presentation did not complete.");
            Assert.That(view.DiceTray.IsRolling, Is.False);
            yield return null;
            yield return CaptureIfRequested(captureName);
            AssertPresentationLabelsVisible(view);
        }

        private static void AssertPresentationLabelsVisible(SpiritEncounterView view)
        {
            Canvas.ForceUpdateCanvases();
            int visibleLabels = 0;
            foreach (var label in view.GetComponentsInChildren<Text>())
            {
                if (!label.isActiveAndEnabled || string.IsNullOrWhiteSpace(label.text)) continue;
                ++visibleLabels;
                var rect = label.rectTransform.rect;
                Assert.That(rect.width, Is.GreaterThan(0f), label.name + " has no width.");
                Assert.That(rect.height, Is.GreaterThan(0f), label.name + " has no height.");
                var generator = label.cachedTextGenerator;
                Assert.That(generator.Populate(label.text, label.GetGenerationSettings(rect.size)),
                    Is.True, label.name);
                Assert.That(generator.vertexCount, Is.GreaterThanOrEqualTo(4),
                    label.name + " generated no visible glyphs within its rect.");
                // Text generation also works for cards culled by the scroll viewport.
                var vertices = generator.verts;
                float unitsPerPixel = 1f / label.pixelsPerUnit;
                var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                for (int index = 0; index < vertices.Count; ++index)
                {
                    Vector2 position = vertices[index].position * unitsPerPixel;
                    minimum = Vector2.Min(minimum, position);
                    maximum = Vector2.Max(maximum, position);
                }
                const float glyphTolerance = 2f;
                Assert.That(maximum.x - minimum.x, Is.LessThanOrEqualTo(rect.width + glyphTolerance),
                    label.name + " glyphs exceed its width: " + label.text);
                Assert.That(maximum.y - minimum.y, Is.LessThanOrEqualTo(rect.height + glyphTolerance),
                    label.name + " glyphs exceed its height: " + label.text);
            }
            Assert.That(visibleLabels, Is.GreaterThan(0), "No active presentation labels were found.");
        }

        private static void AssertLabelGlyphsInsideBounds(Text label, float glyphTolerance = 2f)
        {
            Assert.That(label, Is.Not.Null);
            Assert.That(label.isActiveAndEnabled, Is.True, label.name);
            Assert.That(label.text, Is.Not.Empty, label.name);
            var rect = label.rectTransform.rect;
            var generator = label.cachedTextGenerator;
            Assert.That(generator.Populate(label.text, label.GetGenerationSettings(rect.size)), Is.True, label.name);
            Assert.That(generator.vertexCount, Is.GreaterThan(4), label.name + " has no visible glyphs.");
            float unitsPerPixel = 1f / label.pixelsPerUnit;
            // TextGenerator appends a dummy quad after the rendered glyph vertices.
            for (int index = 0; index < generator.verts.Count - 4; ++index)
            {
                Vector2 position = generator.verts[index].position * unitsPerPixel;
                Assert.That(position.x, Is.InRange(rect.xMin - glyphTolerance, rect.xMax + glyphTolerance),
                    label.name + " glyphs exceed its horizontal bounds.");
                Assert.That(position.y, Is.InRange(rect.yMin - glyphTolerance, rect.yMax + glyphTolerance),
                    label.name + " glyphs exceed its vertical bounds.");
            }
        }

        private static void AssertActionButtonsStacked(SpiritEncounterView view)
        {
            Canvas.ForceUpdateCanvases();
            var reroll = new Vector3[4];
            var play = new Vector3[4];
            view.RerollButton.GetComponent<RectTransform>().GetWorldCorners(reroll);
            view.PlayButton.GetComponent<RectTransform>().GetWorldCorners(play);
            var parent = new Vector3[4];
            view.PlayButton.transform.parent.GetComponent<RectTransform>().GetWorldCorners(parent);
            float parentWidth = parent[2].x - parent[0].x;
            float parentHeight = parent[2].y - parent[0].y;
            float rerollWidth = reroll[2].x - reroll[0].x;
            float playWidth = play[2].x - play[0].x;
            var rerollRect = (RectTransform)view.RerollButton.transform;
            var playRect = (RectTransform)view.PlayButton.transform;
            var parentRect = (RectTransform)view.PlayButton.transform.parent;
            Vector3 rerollInset = parentRect.TransformVector((Vector3)(rerollRect.offsetMax - rerollRect.offsetMin));
            Vector3 rowInset = parentRect.TransformVector(new Vector3(playRect.offsetMax.x - rerollRect.offsetMin.x,
                playRect.offsetMax.y - playRect.offsetMin.y, 0));
            Assert.That(rerollWidth, Is.EqualTo(parentWidth * .455f + rerollInset.x).Within(1f));
            Assert.That(playWidth, Is.EqualTo(rerollWidth).Within(1f), "Action widths must match.");
            Assert.That(play[0].x, Is.GreaterThanOrEqualTo(reroll[2].x),
                "Reroll must sit left of Play Hand without overlapping it.");
            Assert.That(reroll[0].y, Is.EqualTo(play[0].y).Within(1f));
            Assert.That(reroll[2].y, Is.EqualTo(play[2].y).Within(1f));
            Assert.That(play[2].x - reroll[0].x, Is.EqualTo(parentWidth * .93f + rowInset.x).Within(1f),
                "The action row must fill the reserved width.");
            Assert.That(play[2].y - play[0].y, Is.EqualTo(parentHeight * .188f + rowInset.y).Within(1f),
                "The action row must fill the reserved height.");
            Assert.That(view.RerollButton.GetComponentInChildren<Text>().text, Is.EqualTo("Reroll"));
            Assert.That(view.PlayButton.GetComponentInChildren<Text>().text, Is.EqualTo("Play Hand"));
        }

        private static void AssertButtonDecorationsDoNotBlockPointer(Button button)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var graphic in button.GetComponentsInChildren<Graphic>())
            {
                if (graphic == button.targetGraphic) continue;
                Assert.That(graphic.raycastTarget, Is.False,
                    button.name + "/" + graphic.name + " blocks the action's pointer target.");
            }
            foreach (var ornament in button.GetComponentsInChildren<SpiritUiOrnament>())
            {
                var renderer = ornament.GetComponent<CanvasRenderer>();
                Assert.That(renderer, Is.Not.Null, button.name + "/" + ornament.name + " has no canvas renderer.");
                var mesh = renderer.GetMesh();
                Assert.That(mesh, Is.Not.Null, button.name + "/" + ornament.name + " has no rendered mesh.");
                Assert.That(mesh.vertexCount, Is.GreaterThan(0),
                    button.name + "/" + ornament.name + " produced no visible icon geometry.");
            }
        }

        private static void AssertHandRanksAndCharmsButtonsSeparated(SpiritEncounterView view)
        {
            Canvas.ForceUpdateCanvases();
            var charms = new Vector3[4];
            var ranks = new Vector3[4];
            view.CharmsButton.GetComponent<RectTransform>().GetWorldCorners(charms);
            view.HandRanksButton.GetComponent<RectTransform>().GetWorldCorners(ranks);
            Assert.That(ranks[0].x, Is.GreaterThanOrEqualTo(charms[2].x),
                "Hand ranks must sit beside Charms without overlapping it.");
            Assert.That(ranks[0].y, Is.EqualTo(charms[0].y).Within(1f));
            Assert.That(ranks[2].y, Is.EqualTo(charms[2].y).Within(1f));
        }

        private static void AssertHandRanks(SpiritEncounterView view, SpiritEncounterController controller)
        {
            var panel = view.GetComponent<SpiritHandRanksPanel>();
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.Root.gameObject.activeInHierarchy, Is.True);
            Assert.That(panel.Rows.Count, Is.EqualTo(HandDefinition.CategoryCount));
            var currentCategory = controller.Preview.Score.Category;
            Assert.That(panel.CurrentCategory, Is.EqualTo(currentCategory));
            int currentRows = 0;
            for (int index = 0; index < panel.Rows.Count; ++index)
            {
                var row = panel.Rows[index];
                var category = (HandCategory)index;
                var hand = controller.Catalog.GetHand(category);
                int level = controller.Snapshot.HandUpgrades[index];
                Assert.That(row.Category, Is.EqualTo(category));
                Assert.That(row.Background.gameObject.activeInHierarchy, Is.True, category.ToString());
                Assert.That(row.CategoryLabel.text, Is.EqualTo(DiagnosticText.Humanize(category.ToString())));
                Assert.That(row.LevelLabel.text, Is.EqualTo("Level " + level));
                Assert.That(row.BaseLabel.text,
                    Is.EqualTo("+Base " + DiagnosticText.Number(hand.BaseScore + level * hand.BaseScorePerUpgrade)));
                Assert.That(row.MultiplierLabel.text,
                    Is.EqualTo("+Mult " + DiagnosticText.Number(hand.Multiplier + level * hand.MultiplierPerUpgrade)));
                Assert.That(row.CurrentLabel.text, Is.EqualTo(category == currentCategory ? "CURRENT" : ""));
                if (category == currentCategory)
                {
                    ++currentRows;
                    var other = panel.Rows[(index + 1) % panel.Rows.Count];
                    Assert.That(row.Background.color, Is.Not.EqualTo(other.Background.color));
                    Assert.That(row.CategoryLabel.color, Is.Not.EqualTo(other.CategoryLabel.color));
                }
            }
            Assert.That(currentRows, Is.EqualTo(1));
        }

        private static Button FindButton(SpiritEncounterView view, string name)
        {
            foreach (var button in view.GetComponentsInChildren<Button>(true))
                if (button.name == name) return button;
            Assert.Fail("Scene button was not found: " + name);
            return null;
        }

        private static void Click(SpiritEncounterView view, string name)
        {
            var button = FindButton(view, name);
            Assert.That(button.gameObject.activeInHierarchy, Is.True, name);
            Assert.That(button.IsInteractable(), Is.True, name);
            button.onClick.Invoke();
        }

        private static void ClickThroughEventSystem(SpiritEncounterView view, string name)
        {
            var button = FindButton(view, name);
            Assert.That(button.gameObject.activeInHierarchy, Is.True, name);
            Assert.That(button.IsInteractable(), Is.True, name);
            Assert.That(EventSystem.current, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            var rect = button.GetComponent<RectTransform>();
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left,
                pointerId = -1
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), "No graphic received the pointer at " + name);
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.SameAs(button.gameObject), "The first graphic hit does not reach " + name);
            pointer.pointerCurrentRaycast = hits[0];
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void AssertVisibleDice(SpiritEncounterView view, DiceValues values)
        {
            var faceNormals = new[] { Vector3.forward, Vector3.up, Vector3.right,
                Vector3.left, Vector3.down, Vector3.back };
            for (int index = 0; index < DiceValues.Count; ++index)
            {
                var slot = view.TrayScene.Geometry.DiceSlots[index];
                var die = slot.Find("Spirit Dice Slot Content " + index + "/Spirit Die " + index);
                Assert.That(die, Is.Not.Null, "Die " + index);
                Vector3 resultNormal = die.TransformDirection(faceNormals[values[index] - 1]);
                Assert.That(Vector3.Dot(resultNormal, slot.up), Is.GreaterThan(.999f), "Die " + index + " result face");
                var camera = view.DiceTray.RenderCamera;
                Vector3 towardCamera = camera.orthographic ? -camera.transform.forward
                    : (camera.transform.position - die.position).normalized;
                Assert.That(Vector3.Dot(resultNormal, towardCamera), Is.GreaterThan(0f), "Die " + index + " result visibility");
            }
        }

        private static void AssertRerollPlates(SpiritEncounterView view, int selectedMask)
        {
            Canvas.ForceUpdateCanvases();
            var transforms = view.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < DiceValues.Count; ++index)
            {
                string plateName = "DieRerollPlate" + (index + 1);
                Transform plate = null;
                foreach (var candidate in transforms)
                    if (candidate.name == plateName) { plate = candidate; break; }
                Assert.That(plate, Is.Not.Null, plateName);
                bool selected = (selectedMask & (1 << index)) != 0;
                Assert.That(plate.gameObject.activeSelf, Is.EqualTo(selected), plateName);
                if (!selected) continue;
                Assert.That(plate.gameObject.activeInHierarchy, Is.True, plateName);
                string labelName = "DieRerollLabel" + (index + 1);
                var child = plate.Find(labelName);
                Assert.That(child, Is.Not.Null, labelName);
                var label = child.GetComponent<Text>();
                Assert.That(label, Is.Not.Null, labelName);
                Assert.That(label.text, Is.EqualTo("REROLL"), labelName);
                Assert.That(label.gameObject.activeInHierarchy, Is.True, labelName);
                var generator = label.cachedTextGenerator;
                Assert.That(generator.Populate(label.text, label.GetGenerationSettings(label.rectTransform.rect.size)),
                    Is.True, labelName);
                Assert.That(generator.vertexCount, Is.GreaterThan(4), labelName + " generated no visible glyphs within its rect.");
            }
        }

        private static IEnumerator AssertCardTextVisible(SpiritEncounterView view, string prefix, params string[] childNames)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            int cards = 0;
            foreach (var button in view.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy || !button.name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                ++cards;
                foreach (string childName in childNames)
                {
                    var child = button.transform.Find(childName);
                    Assert.That(child, Is.Not.Null, button.name + "/" + childName);
                    var label = child.GetComponent<Text>();
                    Assert.That(label, Is.Not.Null, button.name + "/" + childName);
                    Assert.That(label.text, Is.Not.Empty, button.name + "/" + childName);
                    Assert.That(label.gameObject.activeInHierarchy, Is.True, button.name + "/" + childName);
                    // Generate within the final rect even when a scroll viewport culls the card's mesh.
                    var generator = label.cachedTextGenerator;
                    Assert.That(generator.Populate(label.text, label.GetGenerationSettings(label.rectTransform.rect.size)),
                        Is.True, button.name + "/" + childName);
                    Assert.That(generator.vertexCount, Is.GreaterThan(4),
                        button.name + "/" + childName + " generated no visible glyphs within its rect.");
                }
            }
            Assert.That(cards, Is.GreaterThan(0), "No active cards matched " + prefix);
        }

        private static void AssertSnapshot(EncounterSnapshot expected, EncounterSnapshot actual)
        {
            Assert.That(actual.Revision, Is.EqualTo(expected.Revision));
            Assert.That(actual.Phase, Is.EqualTo(expected.Phase));
            Assert.That(actual.Outcome, Is.EqualTo(expected.Outcome));
            Assert.That(actual.StageIndex, Is.EqualTo(expected.StageIndex));
            Assert.That(actual.HandIndex, Is.EqualTo(expected.HandIndex));
            Assert.That(actual.RemainingThreshold, Is.EqualTo(expected.RemainingThreshold));
            Assert.That(actual.HandsRemaining, Is.EqualTo(expected.HandsRemaining));
            Assert.That(actual.RerollsRemaining, Is.EqualTo(expected.RerollsRemaining));
            Assert.That(actual.RewardRefreshes, Is.EqualTo(expected.RewardRefreshes));
            Assert.That(actual.TotalScore, Is.EqualTo(expected.TotalScore));
            Assert.That(actual.CurrentDice.ToArray(), Is.EqualTo(expected.CurrentDice.ToArray()));
            Assert.That(actual.PendingReward, Is.EqualTo(expected.PendingReward));
            Assert.That(actual.HandUpgrades, Is.EqualTo(expected.HandUpgrades));
            Assert.That(actual.Modifiers.Count, Is.EqualTo(expected.Modifiers.Count));
            for (int index = 0; index < expected.Modifiers.Count; ++index)
            {
                Assert.That(actual.Modifiers[index].InstanceId, Is.EqualTo(expected.Modifiers[index].InstanceId));
                Assert.That(actual.Modifiers[index].DefinitionId, Is.EqualTo(expected.Modifiers[index].DefinitionId));
            }
        }

        private static void AssertTrayRendered(SpiritEncounterView view)
        {
            var target = view.DiceTray.Texture;
            var previous = RenderTexture.active;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                string captureRoot = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
                if (!string.IsNullOrEmpty(captureRoot))
                {
                    Directory.CreateDirectory(captureRoot);
                    string capturePath = Path.Combine(captureRoot, "tray-detail.png");
                    Assert.That(File.Exists(capturePath), Is.False, "Each validation run must use a fresh capture directory.");
                    File.WriteAllBytes(capturePath, pixels.EncodeToPNG());
                }
                int opaquePixels = 0;
                int bonePixels = 0;
                foreach (var pixel in pixels.GetPixels32())
                {
                    if (pixel.a > 200) ++opaquePixels;
                    if (pixel.a > 200 && pixel.r > 140 && pixel.g > 120 && pixel.b > 85) ++bonePixels;
                }
                int pixelCount = target.width * target.height;
                Assert.That(opaquePixels, Is.GreaterThan(pixelCount / 10), "The dice camera did not render the tray.");
                Assert.That(bonePixels, Is.GreaterThan(pixelCount / 100), "The dice camera did not render the bone dice.");
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(pixels);
            }
        }

        private static IEnumerator CaptureIfRequested(string name)
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) yield break;
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, name + ".png");
            Assert.That(File.Exists(path), Is.False, "Each validation run must use a fresh capture directory.");
            if (name == "fox-reaction") yield return new WaitForSecondsRealtime(.18f);
            for (int frame = 0; frame < 3; ++frame) yield return null;
            ScreenCapture.CaptureScreenshot(path);
            for (int frame = 0; frame < 240 && !File.Exists(path); ++frame) yield return null;
            Assert.That(File.Exists(path), Is.True, "Screenshot was not produced: " + path);
            TestContext.WriteLine("Spirit screenshot: " + path);
        }
    }
}
