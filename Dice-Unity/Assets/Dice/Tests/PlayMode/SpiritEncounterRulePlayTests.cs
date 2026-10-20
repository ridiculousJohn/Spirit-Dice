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
    public sealed class SpiritEncounterRulePlayTests
    {
        [UnityTest]
        public IEnumerator FoxFavorPresentsFreeRerollsAndRestoresAvailabilityFromSavesAndNewHands()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit();
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = ContentReady.EncounterView(controller);
            var presentation = view.EncounterRulePresentation;
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.RuleId, Is.EqualTo(FirstRerollFreeRuleDefinition.StableId));
            var encounter = new EncounterDefinition("encounter.fox-favor-test",
                new[] { new StageDefinition(1000000, 2, 1) },
                rules: new[] { new FirstRerollFreeRuleDefinition() });
            var catalog = new MechanicalCatalog("fox-favor-test.v1", controller.Catalog.Hands,
                new[] { encounter }, Array.Empty<ModifierDefinition>());
            string savePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                "spirit-file-tests", Guid.NewGuid().ToString("N"), "fox-favor.json"));
            controller.Initialize(catalog, encounter.Id, 60, savePath);
            yield return ContentReady.Routed(controller);
            view = ContentReady.EncounterView(controller);
            yield return null;
            var cue = view.GetComponent<SpiritActionModifierCue>();
            var fox = view.GetComponentInChildren<SpiritHostPortrait>();
            int gestures = 0;
            fox.SpiritGesture += () => ++gestures;
            AssertFavor(view, presentation, true);
            var portrait = ContentReady.Portrait();
            foreach (var size in new[] { new Vector2(540, 960), new Vector2(443, 912) })
            {
                portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                portrait.sizeDelta = size;
                portrait.anchoredPosition = Vector2.zero;
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertFullText(Find<Text>(view, "EncounterRuleLabel"));
                AssertFullText(view.RerollButton.GetComponentInChildren<Text>());
                var favor = Find<Text>(view, "EncounterRuleLabel").rectTransform;
                var action = view.RerollButton.GetComponentInChildren<Text>().rectTransform;
                var favorCorners = new Vector3[4];
                var actionCorners = new Vector3[4];
                favor.GetWorldCorners(favorCorners);
                action.GetWorldCorners(actionCorners);
                Assert.That(favorCorners[2].y, Is.LessThanOrEqualTo(actionCorners[0].y),
                    "The favor label must fit below the reroll action label.");
            }

            var unchanged = controller.Snapshot;
            controller.Reroll();
            Assert.That(controller.Snapshot, Is.SameAs(unchanged));
            Assert.That(gestures, Is.Zero, "An invalid selection must not present or consume the host's favor.");
            AssertFavor(view, presentation, true);
            controller.Save();
            controller.ToggleDie(0);
            Assert.That(view.RerollButton.IsInteractable(), Is.True);
            view.RerollButton.onClick.Invoke();
            yield return ContentReady.Routed();
            view = ContentReady.EncounterView(controller);
            Assert.That(controller.IsBusy, Is.True);
            Assert.That(controller.Snapshot.RerollsRemaining, Is.EqualTo(1));
            Assert.That(Find<Text>(view, "RerollsRemainingLabel").text, Is.EqualTo("REROLLS  1"));
            Assert.That(gestures, Is.EqualTo(1));
            Assert.That(fox.IsReacting, Is.True);
            Assert.That(cue.IsVisible, Is.True);
            Assert.That(cue.ActivePhase, Is.EqualTo(ModifierPhase.PlayerReroll));
            Assert.That(cue.ActiveSprite, Is.SameAs(presentation.Icon));
            Assert.That(ContentReady.ActionCuePart<SpiritHostSigilGraphic>(view, false, "HostSigil")
                .gameObject.activeInHierarchy, Is.EqualTo(presentation.Icon == null));
            AssertFavor(view, presentation, false);
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(Find<CanvasGroup>(view, "RuleSpiritSweep").gameObject.activeInHierarchy, Is.True);
            Canvas.ForceUpdateCanvases();
            AssertFullText(Find<Text>(view, "SelectionLabel"));
            yield return Capture("fox-courtesy-rule-cue");
            yield return Wait(controller);
            Assert.That(view.ModifierHistory.Count, Is.Zero, "A host rule must not create a charm history token.");
            Assert.That(controller.Snapshot.RerollsSpentOnHand, Is.EqualTo(1));
            Assert.That(controller.Snapshot.RerollsRemaining, Is.EqualTo(1));

            controller.Load();
            yield return ContentReady.Routed(controller);
            Assert.That(controller.Snapshot.Revision, Is.Zero);
            AssertFavor(view, presentation, true);
            Assert.That(cue.IsVisible, Is.False);
            Assert.That(Find<CanvasGroup>(view, "RuleSpiritSweep").gameObject.activeInHierarchy, Is.False);
            controller.ToggleDie(0);
            controller.Reroll();
            yield return Wait(controller);
            controller.Save();
            controller.Load();
            yield return ContentReady.Routed(controller);
            AssertFavor(view, presentation, false);
            Assert.That(cue.IsVisible, Is.False);
            int beforePaidGesture = gestures;
            controller.ToggleDie(0);
            controller.Reroll();
            Assert.That(gestures, Is.EqualTo(beforePaidGesture), "A paid reroll must not replay the free favor gesture.");
            Assert.That(cue.IsVisible, Is.False);
            yield return Wait(controller);
            Assert.That(controller.Snapshot.RerollsRemaining, Is.Zero);
            Assert.That(controller.Snapshot.CanReroll, Is.False);
            controller.ToggleDie(0);
            Assert.That(view.RerollButton.IsInteractable(), Is.False);

            controller.PlayHand();
            yield return Wait(controller);
            Assert.That(controller.Snapshot.HandIndex, Is.EqualTo(1));
            Assert.That(controller.Snapshot.RerollsRemaining, Is.Zero);
            Assert.That(controller.Snapshot.CanReroll, Is.True);
            AssertFavor(view, presentation, true);
            controller.ToggleDie(0);
            Assert.That(view.RerollButton.IsInteractable(), Is.True, "The free reroll must remain available with no paid rerolls.");
            view.RerollButton.onClick.Invoke();
            yield return ContentReady.Routed();
            view = ContentReady.EncounterView(controller);
            Assert.That(controller.IsBusy, Is.True);
            yield return Wait(controller);
            Assert.That(controller.Snapshot.RerollsRemaining, Is.Zero);
            Assert.That(controller.Snapshot.CanReroll, Is.False);
            AssertFavor(view, presentation, false);
            controller.Save();
            controller.Load();
            yield return ContentReady.Routed(controller);
            AssertFavor(view, presentation, false);
            controller.ToggleDie(0);
            Assert.That(view.RerollButton.IsInteractable(), Is.False);
            Assert.That(view.ModifierHistory.Count, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        private static void AssertFavor(SpiritEncounterView view, EncounterRulePresentationAsset presentation, bool available)
        {
            var label = Find<Text>(view, "EncounterRuleLabel");
            Assert.That(label.gameObject.activeInHierarchy, Is.True);
            Assert.That(label.text, Is.EqualTo(available ? presentation.AvailableText : presentation.UsedText));
            Assert.That(Find<CanvasGroup>(view, "EncounterRuleFavor").alpha, Is.EqualTo(available ? 1 : .4f));
            Assert.That(Find<SpiritHostSigilGraphic>(view, "EncounterRuleSigil").gameObject.activeInHierarchy,
                Is.EqualTo(presentation.Icon == null));
        }

        private static void AssertFullText(Text label)
        {
            var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            var generated = label.cachedTextGenerator;
            Assert.That(generated.Populate(label.text, settings), Is.True, label.name);
            Assert.That(generated.vertexCount, Is.GreaterThan(4), label.name);
            settings.fontSize = label.resizeTextForBestFit ? generated.fontSizeUsedForBestFit : label.fontSize;
            settings.resizeTextForBestFit = false;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            using (var complete = new TextGenerator())
            {
                Assert.That(complete.Populate(label.text, settings), Is.True, label.name);
                Assert.That(generated.vertexCount, Is.EqualTo(complete.vertexCount), label.name + " truncates glyphs.");
            }
            var bounds = label.rectTransform.rect;
            for (int index = 0; index < generated.verts.Count - 4; ++index)
            {
                Vector2 position = generated.verts[index].position / label.pixelsPerUnit;
                Assert.That(position.x, Is.InRange(bounds.xMin - 2, bounds.xMax + 2), label.name);
                Assert.That(position.y, Is.InRange(bounds.yMin - 2, bounds.yMax + 2), label.name);
            }
        }

        private static T Find<T>(Component root, string name) where T : Component
        {
            var result = root.GetComponentsInChildren<T>(true).FirstOrDefault(value => value.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }

        private static IEnumerator Wait(SpiritEncounterController controller)
        {
            float deadline = Time.realtimeSinceStartup + 25;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
        }

        private static IEnumerator Capture(string name)
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) yield break;
            string path = Path.Combine(root, name + ".png");
            Assert.That(File.Exists(path), Is.False);
            Directory.CreateDirectory(root);
            ScreenCapture.CaptureScreenshot(path);
            for (int frame = 0; frame < 120 && !File.Exists(path); ++frame) yield return null;
            Assert.That(File.Exists(path), Is.True);
        }
    }
}
