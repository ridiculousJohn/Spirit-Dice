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
        public IEnumerator SceneButtonsPresentEngineDiceRewardsCharmsAndSaveResume()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            Assert.That(controller, Is.Not.Null);
            var view = controller.GetComponent<SpiritEncounterView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.enabled, Is.True);
            Assert.That(controller.Snapshot, Is.Not.Null);
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo("encounter.standard"));
            Assert.That(controller.Snapshot.StageCount, Is.EqualTo(25));
            string artifactRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                "spirit-file-tests", Guid.NewGuid().ToString("N")));
            string savePath = Path.Combine(artifactRoot, "playmode-encounter.json");
            controller.Initialize(controller.Catalog, "encounter.standard", 60, savePath);
            var expected = EncounterSession.CreateEncounter(controller.Catalog, "encounter.standard", 60);
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
            var theme = Resources.Load<SpiritUiTheme>("SpiritHouseTheme");
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
            Assert.That(view.RerollButton.GetComponentInChildren<Text>().text, Does.Contain("2"),
                "The reroll action must show the number of selected dice.");
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
            Assert.That(view.GetComponentInChildren<SpiritFoxPortrait>().IsReacting, Is.True,
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
            yield return WaitForPresentation(controller, view, "modifier-reward");
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingModifier));
            Assert.That(controller.Preview, Is.Null);
            yield return AssertCardTextVisible(view, "RewardCard", "Effects", "Status");

            var offer = controller.Snapshot.PendingReward;
            Click(view, "RefreshRewardsButton");
            AssertSnapshot(expected.Execute(new RefreshModifierOfferAction(offer.OfferId), expected.Snapshot.Revision).Snapshot,
                controller.Snapshot);
            yield return WaitForPresentation(controller, view, "modifier-refreshed");
            yield return AssertCardTextVisible(view, "RewardCard", "Effects", "Status");
            offer = controller.Snapshot.PendingReward;
            Click(view, "RewardCard1");
            AssertSnapshot(expected.Execute(new SelectModifierAction(offer.OfferId, offer.Options[0].OptionId),
                expected.Snapshot.Revision).Snapshot, controller.Snapshot);
            yield return WaitForPresentation(controller, view, "hand-upgrade");
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.ChoosingHandUpgrade));
            Assert.That(controller.Snapshot.Modifiers.Count, Is.EqualTo(1));
            yield return AssertCardTextVisible(view, "RewardCard", "Effects", "Status");

            offer = controller.Snapshot.PendingReward;
            var upgradedCategory = offer.Options[0].Category.Value;
            int previousUpgradeLevel = controller.Snapshot.HandUpgrades[(int)upgradedCategory];
            Click(view, "RewardCard1");
            AssertSnapshot(expected.Execute(new SelectHandUpgradeAction(offer.OfferId, offer.Options[0].OptionId),
                expected.Snapshot.Revision).Snapshot, controller.Snapshot);
            yield return WaitForPresentation(controller, view, "next-stage");
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
            yield return AssertCardTextVisible(view, "OwnedCharm", "Status");
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
            Assert.That(view.GetComponentInChildren<SpiritFoxPortrait>().IsReacting, Is.False,
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
            Assert.That(reroll[2].x - reroll[0].x, Is.GreaterThan(parentWidth * .9f),
                "Reroll must span the playing area.");
            Assert.That(play[2].x - play[0].x, Is.GreaterThan(parentWidth * .9f),
                "Play hand must span the playing area.");
            Assert.That((reroll[0].x + reroll[2].x) * .5f,
                Is.EqualTo((play[0].x + play[2].x) * .5f).Within(1f), "Action centers must align.");
            Assert.That(reroll[0].y, Is.GreaterThanOrEqualTo(play[2].y),
                "Reroll must sit above Play hand without overlapping it.");
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
            for (int index = 0; index < DiceValues.Count; ++index)
                Assert.That(view.DiceTray.GetVisibleFace(index), Is.EqualTo(values[index]), "Die " + index);
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
