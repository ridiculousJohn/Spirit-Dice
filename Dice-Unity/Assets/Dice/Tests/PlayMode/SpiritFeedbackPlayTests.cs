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
    public sealed class SpiritFeedbackPlayTests
    {
        [UnityTest]
        public IEnumerator ScoringPresentsContributionsInOrderAndRetainsRerollBonuses()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit();
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = controller.GetComponent<SpiritEncounterView>();
            var modifiers = new[]
            {
                Modifier("bank", ModifierPhase.InitialRoll, new AlwaysCondition(), ModifierEffectKind.AddBaseScore, 7),
                Modifier("mercy", ModifierPhase.PlayerReroll, new LastRerollOnLastHandCondition(), ModifierEffectKind.AddBaseScore, 15),
                Modifier("base", ModifierPhase.PlayHand, new AlwaysCondition(), ModifierEffectKind.AddBaseScore, 10),
                Modifier("multiplier", ModifierPhase.PlayHand, new AlwaysCondition(), ModifierEffectKind.AddMultiplier, 2),
                Modifier("rerolls", ModifierPhase.PlayHand, new AlwaysCondition(), ModifierEffectKind.AddRerolls, 1)
            };
            var encounter = new EncounterDefinition("encounter.feedback",
                new[] { new StageDefinition(1000000, 2, 1) },
                startingModifiers: Array.ConvertAll(modifiers, modifier => modifier.Id));
            var catalog = new MechanicalCatalog("feedback.v1", controller.Catalog.Hands, new[] { encounter }, modifiers);
            string save = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                "spirit-file-tests", Guid.NewGuid().ToString("N"), "feedback.json"));
            controller.Initialize(catalog, encounter.Id, 60, save);
            yield return null;
            var expected = EncounterSession.CreateEncounter(catalog, encounter.Id, 60);
            AssertCounters(view, 7, 0, 0);
            controller.ToggleDie(0);
            AssertCounters(view, 7, 0, 0);
            controller.ToggleDie(0);
            yield return PlayAndInspect(controller, view, expected, "first-hand");

            Assert.That(controller.Snapshot.HandsRemaining, Is.EqualTo(1));
            Assert.That(controller.Snapshot.RerollsRemaining, Is.EqualTo(2));
            for (int reroll = 0; reroll < 2; ++reroll)
            {
                controller.ToggleDie(0);
                controller.Reroll();
                expected.Execute(new RerollDiceAction(1), expected.Snapshot.Revision);
                yield return WaitForPresentation(controller);
                AssertCounters(view, reroll == 0 ? 7 : 22, 0, 0);
            }
            Assert.That(controller.Snapshot.ModifierContributions.Count, Is.EqualTo(2),
                "Initial-roll and final-reroll bonuses should both be banked on the last hand.");
            Assert.That(controller.Snapshot.ModifierContributions[1].DefinitionId, Is.EqualTo("modifier.mercy"));
            Assert.That(controller.Snapshot.ModifierContributions[1].BaseScore, Is.EqualTo(15));
            controller.Save();
            view.Bind(controller);
            AssertCounters(view, 22, 0, 0);
            controller.Load();
            Assert.That(controller.IsBusy, Is.False);
            AssertCounters(view, 22, 0, 0);
            yield return PlayAndInspect(controller, view, expected, "last-hand");
            Assert.That(controller.Snapshot.TotalScore, Is.EqualTo(expected.Snapshot.TotalScore));
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(expected.Snapshot.Outcome));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InitialBaseAndMultiplierBanksSurviveSelectionLoadAndRebindWithoutPreview()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit();
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = controller.GetComponent<SpiritEncounterView>();
            var modifiers = new[]
            {
                Modifier("bank-base", ModifierPhase.InitialRoll, new AlwaysCondition(), ModifierEffectKind.AddBaseScore, 10),
                Modifier("bank-mult", ModifierPhase.InitialRoll, new AlwaysCondition(), ModifierEffectKind.AddMultiplier, 2),
                new ModifierDefinition("modifier.mixed-play", "family.mixed-play", ModifierPhase.PlayHand,
                    new AlwaysCondition(), new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 4),
                        new ModifierEffect(ModifierEffectKind.AddRerolls, 1) })
            };
            var encounter = new EncounterDefinition("encounter.banked-feedback",
                new[] { new StageDefinition(1000000, 2, 1) },
                startingUpgrades: new[] { 1, 1, 1, 1, 1, 1, 1, 1 },
                startingModifiers: Array.ConvertAll(modifiers, modifier => modifier.Id));
            var catalog = new MechanicalCatalog("banked-feedback.v1", controller.Catalog.Hands,
                new[] { encounter }, modifiers);
            string save = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                "spirit-file-tests", Guid.NewGuid().ToString("N"), "banked-feedback.json"));
            controller.Initialize(catalog, encounter.Id, 60, save);
            yield return null;
            var expected = EncounterSession.CreateEncounter(catalog, encounter.Id, 60);
            Assert.That(controller.Preview.Score.HandScore, Is.GreaterThan(0));
            AssertCounters(view, 10, 2, 0);
            yield return Capture("initial-base-and-multiplier-bank");
            controller.ToggleDie(0);
            controller.ToggleDie(3);
            AssertCounters(view, 10, 2, 0);
            controller.Save();
            controller.Reroll();
            expected.Execute(new RerollDiceAction(9), expected.Snapshot.Revision);
            yield return WaitForPresentation(controller);
            AssertCounters(view, 10, 2, 0);
            controller.Load();
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(controller.SelectedMask, Is.Zero);
            Assert.That(controller.Snapshot.Revision, Is.Zero);
            AssertCounters(view, 10, 2, 0);
            view.Bind(controller);
            AssertCounters(view, 10, 2, 0);
            expected = EncounterSession.CreateEncounter(catalog, encounter.Id, 60);
            yield return PlayAndInspect(controller, view, expected, "base-and-multiplier-bank");
            AssertCounters(view, 10, 2, 0);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ReadyModifiersRetainDuplicateInstancesAndScrollWithoutRollActivations()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit();
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = controller.GetComponent<SpiritEncounterView>();
            var geometry = view.TrayScene.Geometry;
            // Endpoint dice must fully leave the authored bay at opposite ends of the scroll.
            int readyCount = Mathf.CeilToInt((geometry.ModifierWidth * .5f + geometry.ModifierDieSize
                + (geometry.ModifierVisibleCapacity - 1) * geometry.ModifierSlotSpacing * .5f)
                / geometry.ModifierSlotSpacing) + 2;
            int lastIndex = readyCount - 1;
            var modifiers = new List<ModifierDefinition>();
            modifiers.Add(Modifier("history-ready", ModifierPhase.PlayHand,
                new AlwaysCondition(), ModifierEffectKind.AddMultiplier, 1));
            modifiers.Add(Modifier("history-bank", ModifierPhase.InitialRoll,
                new AlwaysCondition(), ModifierEffectKind.AddBaseScore, 2));
            modifiers.Add(new ModifierDefinition("modifier.history-repeat", "family.history-repeat",
                ModifierPhase.PlayerReroll, new AlwaysCondition(),
                new[] { new ModifierEffect(ModifierEffectKind.AddMultiplier, 1) }, ModifierActivationLimit.Unlimited));
            var startingModifiers = modifiers.ConvertAll(modifier => modifier.Id);
            for (int index = 1; index < readyCount; ++index) startingModifiers.Add(modifiers[0].Id);
            var encounter = new EncounterDefinition("encounter.history", new[] { new StageDefinition(1000000, 2, 3) },
                startingModifiers: startingModifiers);
            var catalog = new MechanicalCatalog("history.v1", controller.Catalog.Hands, new[] { encounter }, modifiers);
            string save = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                "spirit-file-tests", Guid.NewGuid().ToString("N"), "history.json"));
            controller.Initialize(catalog, encounter.Id, 60, save);
            yield return null;
            AssertHistory(view, controller.Preview.Score.ModifierContributions);
            Assert.That(view.ModifierHistory.Count, Is.EqualTo(readyCount));
            var renderer = view.ModifierHistory.Renderer;
            AssertHistoryGeometry(renderer);
            AssertHistoryDieOnFloor(renderer, 0);
            Canvas.ForceUpdateCanvases();
            var scroll = view.ModifierHistory.GetComponentInChildren<ScrollRect>();
            Assert.That(scroll, Is.Not.Null);
            Assert.That(scroll.horizontal, Is.True);
            Assert.That(scroll.content.rect.width, Is.GreaterThan(scroll.viewport.rect.width));
            scroll.horizontalNormalizedPosition = 0;
            yield return null;
            Vector3 framePosition = renderer.TrayRoot.position;
            Quaternion frameRotation = renderer.TrayRoot.rotation;
            Vector3 firstDiePosition = renderer.GetDieTransform(0).position;
            var clipping = renderer.GetDieRenderer(0).sharedMaterial.GetVector("_ClipLocalX");
            Assert.That(clipping.z, Is.EqualTo(1f), "Overflow pieces must be clipped to the tray interior.");
            Assert.That(renderer.TrayRoot, Is.SameAs(geometry.ModifierRoot));
            Assert.That(clipping.x, Is.EqualTo(-geometry.ModifierWidth * .5f));
            Assert.That(clipping.y, Is.EqualTo(geometry.ModifierWidth * .5f));
            Vector3 bayPoint = new Vector3(.23f, .4f, -.1f);
            Vector3 shaderPoint = renderer.GetDieRenderer(0).sharedMaterial.GetMatrix("_ClipWorldToLocal")
                .MultiplyPoint3x4(geometry.ModifierRoot.TransformPoint(bayPoint));
            Assert.That(Vector3.Distance(shaderPoint, bayPoint), Is.LessThan(.003f));
            Assert.That(HistoryDieXBounds(renderer, lastIndex).x, Is.GreaterThan(clipping.y),
                "The final history die must overflow the interior at the start of the scroll.");
            Assert.That(renderer.GetDieViewportRect(lastIndex).width, Is.Zero,
                "A clipped history die must expose no pointer target.");
            yield return CaptureHistoryTexture(renderer, "history-3d-start-detail");
            yield return Capture("history-overflow-start");

            scroll.horizontalNormalizedPosition = 1;
            yield return null;
            Assert.That(renderer.TrayRoot.position, Is.EqualTo(framePosition), "Scrolling must leave the wooden tray fixed.");
            Assert.That(renderer.TrayRoot.rotation, Is.EqualTo(frameRotation));
            Assert.That(renderer.TrayRoot.InverseTransformPoint(renderer.GetDieTransform(0).position).x,
                Is.LessThan(renderer.TrayRoot.InverseTransformPoint(firstDiePosition).x - .1f),
                "Scrolling must move the physical dice along the fixed tray.");
            Assert.That(HistoryDieXBounds(renderer, 0).y, Is.LessThan(clipping.x),
                "The first history die must be clipped beyond the opposite end after scrolling.");
            Assert.That(renderer.GetDieViewportRect(0).width, Is.Zero);
            AssertHistory(view, controller.Preview.Score.ModifierContributions);
            yield return CaptureHistoryTexture(renderer, "history-3d-scrolled-detail");

            for (int index = 0; index < 2; ++index)
            {
                controller.ToggleDie(0);
                controller.Reroll();
                float deadline = Time.realtimeSinceStartup + 10f;
                while (controller.IsBusy && Time.realtimeSinceStartup < deadline)
                {
                    Assert.That(view.ModifierHistory.Count, Is.Zero,
                        "Ready PlayHand activations wait until the roll and its bonuses finish.");
                    yield return null;
                }
                Assert.That(controller.IsBusy, Is.False);
                AssertHistory(view, controller.Preview.Score.ModifierContributions);
                Assert.That(renderer.GetArrivalProgress(lastIndex), Is.EqualTo(1f));
                AssertHistoryDieOnFloor(renderer, lastIndex);
            }
            Assert.That(view.ModifierHistory.Contributions[0].DefinitionId,
                Is.EqualTo(view.ModifierHistory.Contributions[lastIndex].DefinitionId));
            Assert.That(view.ModifierHistory.Contributions[0].InstanceId,
                Is.Not.EqualTo(view.ModifierHistory.Contributions[lastIndex].InstanceId));
            controller.ToggleDie(2);
            controller.ToggleDie(2);
            controller.Save();
            view.Bind(controller);
            AssertHistory(view, controller.Preview.Score.ModifierContributions);
            controller.Load();
            AssertHistory(view, controller.Preview.Score.ModifierContributions);
            Assert.That(view.ModifierHistory.Count, Is.EqualTo(readyCount));
            Assert.That(renderer.Count, Is.EqualTo(readyCount));
            for (int index = 0; index < renderer.Count; ++index)
            {
                Assert.That(renderer.GetArrivalProgress(index), Is.EqualTo(1f),
                    "Restoring history must not replay arrival animation.");
                AssertHistoryDieOnFloor(renderer, index);
            }
            scroll.horizontalNormalizedPosition = 1;
            yield return null;
            var selectedContribution = view.ModifierHistory.Contributions[lastIndex];
            var selectedDefinition = controller.Catalog.GetModifier(selectedContribution.DefinitionId);
            ClickHistoryDie(view.ModifierHistory, lastIndex);
            Assert.That(view.IsCharmsOpen, Is.True);
            Assert.That(FindLabel(view, "CharmDetailTitle").text,
                Is.EqualTo(SpiritModifierPresentation.GetName(selectedDefinition)));
            Assert.That(FindLabel(view, "CharmDetailBody").text,
                Does.Contain(SpiritModifierPresentation.GetEffectText(selectedDefinition)));
            int selectedOwnedIndex = -1;
            for (int index = 0; index < controller.Snapshot.Modifiers.Count; ++index)
                if (controller.Snapshot.Modifiers[index].InstanceId == selectedContribution.InstanceId)
                    selectedOwnedIndex = index;
            Assert.That(selectedOwnedIndex, Is.GreaterThanOrEqualTo(0));
            Button selectedOwnedCard = null;
            foreach (var button in view.GetComponentsInChildren<Button>(true))
                if (button.name == "OwnedCharm" + (selectedOwnedIndex + 1)) selectedOwnedCard = button;
            Assert.That(selectedOwnedCard, Is.Not.Null);
            Assert.That(selectedOwnedCard.GetComponent<Image>().color,
                Is.EqualTo((Color)new Color32(255, 232, 179, 255)), "The tapped duplicate instance must be selected.");
            var selectedOutline = selectedOwnedCard.transform.Find("SelectionOutline")
                .GetComponent<SpiritCharmSelectionGraphic>();
            Assert.That(selectedOutline, Is.Not.Null);
            Assert.That(selectedOutline.gameObject.activeSelf, Is.True);
            Assert.That(selectedOutline.IsPulsing, Is.True,
                "Opening a ready modifier must immediately pulse its exact owned instance.");
            var ownedCards = selectedOwnedCard.transform.parent.GetComponentsInChildren<Button>(true);
            foreach (var card in ownedCards)
            {
                var outline = card.transform.Find("SelectionOutline").GetComponent<SpiritCharmSelectionGraphic>();
                Assert.That(outline.gameObject.activeSelf, Is.EqualTo(card == selectedOwnedCard),
                    "Only the selected owned instance may display a selection outline.");
                if (card != selectedOwnedCard) Assert.That(outline.IsPulsing, Is.False);
            }
            var ownedScroll = selectedOwnedCard.GetComponentInParent<ScrollRect>();
            Assert.That(ownedScroll, Is.Not.Null);
            var selectedCorners = new Vector3[4];
            selectedOwnedCard.GetComponent<RectTransform>().GetWorldCorners(selectedCorners);
            for (int index = 0; index < selectedCorners.Length; ++index)
                selectedCorners[index] = ownedScroll.viewport.InverseTransformPoint(selectedCorners[index]);
            var ownedViewport = ownedScroll.viewport.rect;
            Assert.That(selectedCorners[0].x, Is.GreaterThanOrEqualTo(ownedViewport.xMin - .5f));
            Assert.That(selectedCorners[2].x, Is.LessThanOrEqualTo(ownedViewport.xMax + .5f));
            if (selectedCorners[1].y - selectedCorners[0].y <= ownedViewport.height + .5f)
            {
                Assert.That(selectedCorners[0].y, Is.GreaterThanOrEqualTo(ownedViewport.yMin - .5f),
                    "Opening a ready modifier must synchronously scroll its full card into view.");
                Assert.That(selectedCorners[1].y, Is.LessThanOrEqualTo(ownedViewport.yMax + .5f));
            }
            else
            {
                Assert.That(selectedCorners[1].y, Is.EqualTo(ownedViewport.yMax).Within(.5f),
                    "A card taller than the viewport must align its top with the viewport.");
            }
            yield return Capture("history-selected-charm");
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(selectedOutline.IsPulsing, Is.False);
            Assert.That(selectedOutline.gameObject.activeSelf, Is.True,
                "The selected outline must remain visible after its brief pulse settles.");
            Assert.That(selectedOutline.color, Is.EqualTo((Color)new Color32(255, 215, 119, 255)));
            Assert.That(selectedOwnedCard.GetComponent<Image>().color,
                Is.EqualTo((Color)new Color32(255, 232, 179, 255)));

            view.ShowTable();
            ClickHistoryDie(view.ModifierHistory, lastIndex);
            Assert.That(selectedOutline.IsPulsing, Is.True);
            Button otherOwnedCard = null;
            foreach (var card in ownedCards)
                if (card.name == "OwnedCharm1") otherOwnedCard = card;
            Assert.That(otherOwnedCard, Is.Not.Null);
            Assert.That(otherOwnedCard, Is.Not.SameAs(selectedOwnedCard));
            otherOwnedCard.onClick.Invoke();
            Assert.That(selectedOutline.gameObject.activeSelf, Is.False);
            Assert.That(selectedOutline.IsPulsing, Is.False,
                "Selecting another owned instance must stop the previous pulse immediately.");
            foreach (var card in ownedCards)
                Assert.That(card.transform.Find("SelectionOutline").gameObject.activeSelf,
                    Is.EqualTo(card == otherOwnedCard));
            Assert.That(otherOwnedCard.GetComponent<Image>().color,
                Is.EqualTo((Color)new Color32(255, 232, 179, 255)));

            view.ShowTable();
            ClickHistoryDie(view.ModifierHistory, lastIndex);
            Assert.That(selectedOutline.IsPulsing, Is.True);
            view.ShowTable();
            Assert.That(selectedOutline.IsPulsing, Is.False,
                "Closing Charms must stop an in-progress selection pulse.");
            view.ShowCharms();
            Assert.That(selectedOutline.IsPulsing, Is.False,
                "Reopening Charms must not resume or replay a stale selection pulse.");
            Assert.That(selectedOutline.gameObject.activeSelf, Is.True);
            Assert.That(view.ModifierHistory.Count, Is.EqualTo(readyCount), "Inspecting a token must not replay its activation.");
            view.ShowTable();
            Assert.That(view.IsCharmsOpen, Is.False);
            Canvas.ForceUpdateCanvases();
            var statusRect = FindLabel(view, "StatusMessage").rectTransform;
            var statusCorners = new Vector3[4];
            var historyCorners = new Vector3[4];
            statusRect.GetWorldCorners(statusCorners);
            scroll.viewport.GetWorldCorners(historyCorners);
            Assert.That(statusCorners[0].y, Is.GreaterThan(historyCorners[2].y),
                "Save/load status must not overlap the modifier history.");
            yield return CaptureHistoryTexture(renderer, "history-3d-restored-detail");
            yield return Capture("history-overflow-latest");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SoundThemeLoadsAndMuteStopsOnlyEncounterAudio()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit();
            yield return null;
            var sound = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterAudio>();
            Assert.That(sound, Is.Not.Null);
            Assert.That(sound.Theme, Is.Not.Null);
            Assert.That(sound.Theme.rollClips, Has.Length.EqualTo(DiceValues.Count));
            Assert.That(sound.Theme.rollContactTracks, Has.Length.EqualTo(DiceValues.Count));
            Assert.That(sound.Theme.ambience.name, Is.EqualTo("inspectorj_window_rain_loop"));
            Assert.That(sound.Theme.credits.text, Does.Contain("InspectorJ"));
            foreach (var clip in sound.Theme.rollClips) Assert.That(clip, Is.Not.Null);
            foreach (var clip in new[] { sound.Theme.ambience, sound.Theme.spirit, sound.Theme.score,
                sound.Theme.stageClear, sound.Theme.creak }) Assert.That(clip, Is.Not.Null);
            float listenerVolume = AudioListener.volume;
            sound.SendMessage("OnApplicationFocus", true);
            for (int count = 1; count <= DiceValues.Count; count++)
            {
                var clip = sound.Theme.rollClips[count - 1];
                var timing = sound.BeginRoll(count);
                Assert.That(timing.Duration, Is.LessThan(1.2f), "Preparation audio must not prolong the toss.");
                yield return new WaitForSecondsRealtime(timing.AudioDelay + .06f);
                Assert.That(Array.Exists(sound.GetComponentsInChildren<AudioSource>(),
                    source => source.clip == clip && source.isPlaying && source.time >= timing.RecordingStartTime), Is.True,
                    "The matching recording must play from its configured impact sequence.");
                sound.Muted = true;
                var mutedTiming = sound.BeginRoll(count);
                Assert.That(mutedTiming.Duration, Is.EqualTo(timing.Duration));
                Assert.That(mutedTiming.RecordingStartTime, Is.EqualTo(timing.RecordingStartTime));
                for (int contact = 0; contact < timing.ContactCount; contact++)
                    Assert.That(mutedTiming.GetContactTime(contact), Is.EqualTo(timing.GetContactTime(contact)));
                foreach (var source in sound.GetComponentsInChildren<AudioSource>())
                    Assert.That(source.isPlaying, Is.False);
                sound.Muted = false;
            }
            sound.Play(SpiritAudioCue.StageClear);
            yield return null;
            bool cuePlaying = false;
            foreach (var source in sound.GetComponentsInChildren<AudioSource>())
                if (source.clip == sound.Theme.stageClear && source.isPlaying) cuePlaying = true;
            Assert.That(cuePlaying, Is.True, "The requested cue must reach an encounter audio source.");
            var view = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterView>();
            Button toggle = null;
            foreach (var button in view.GetComponentsInChildren<Button>(true))
                if (button.name == "SoundButton") toggle = button;
            Assert.That(toggle, Is.Not.Null);
            toggle.onClick.Invoke();
            Assert.That(sound.Muted, Is.True);
            sound.BeginRoll(2);
            yield return null;
            foreach (var source in sound.GetComponentsInChildren<AudioSource>())
                Assert.That(source.isPlaying, Is.False);
            Assert.That(AudioListener.volume, Is.EqualTo(listenerVolume));
            Text credits = null;
            foreach (var label in view.GetComponentsInChildren<Text>(true))
                if (label.name == "AudioCredits") credits = label;
            Assert.That(credits, Is.Not.Null);
            Assert.That(credits.text, Does.Contain("creativecommons.org/licenses/by/4.0"));
            credits.transform.parent.gameObject.SetActive(true);
            yield return Capture("rain-credits");
            credits.transform.parent.gameObject.SetActive(false);
            toggle.onClick.Invoke();
            Assert.That(sound.Muted, Is.False);
            sound.SendMessage("OnApplicationFocus", false);
            foreach (var source in sound.GetComponentsInChildren<AudioSource>()) Assert.That(source.isPlaying, Is.False);
            sound.SendMessage("OnApplicationFocus", true);
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            controller.ToggleDie(0);
            controller.ToggleDie(3);
            controller.Reroll();
            float rollDeadline = Time.realtimeSinceStartup + 5;
            while (!view.DiceTray.IsRolling && controller.IsBusy && Time.realtimeSinceStartup < rollDeadline)
                yield return null;
            Assert.That(view.DiceTray.IsRolling, Is.True, "The rule cue must lead into the dice roll.");
            bool selectedRecordingPlayed = false;
            while (controller.IsBusy && Time.realtimeSinceStartup < rollDeadline)
            {
                selectedRecordingPlayed = Array.Exists(sound.GetComponentsInChildren<AudioSource>(),
                    source => source.clip == sound.Theme.rollClips[1] && source.isPlaying);
                if (selectedRecordingPlayed) break;
                yield return null;
            }
            TestContext.WriteLine("roll mask=" + controller.Snapshot.LatestRerollMask + " muted=" + sound.Muted +
                " focused=" + Application.isFocused + " audioFocus=" + typeof(SpiritEncounterAudio).GetField("hasFocus",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(sound));
            foreach (var source in sound.GetComponentsInChildren<AudioSource>())
                TestContext.WriteLine("clip=" + source.clip?.name + " playing=" + source.isPlaying +
                    " time=" + (source.clip != null ? source.time : 0));
            Assert.That(selectedRecordingPlayed, Is.True,
                "Rerolling two selected dice must use the two-dice recording.");
            yield return WaitForPresentation(controller);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RecordedRollsAnimateSelectedDiceAndRetainTheResults()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit();
            yield return null;
            var sound = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterAudio>();
            var tray = UnityEngine.Object.FindAnyObjectByType<SpiritDiceTray>();
            sound.SendMessage("OnApplicationFocus", true);
            string root = Environment.GetEnvironmentVariable("DICE_ROLL_CAPTURE_ROOT");
            if (!string.IsNullOrEmpty(root)) Directory.CreateDirectory(root);
            var before = new DiceValues(1, 2, 3, 4, 5);
            var after = new DiceValues(6, 5, 4, 3, 2);
            foreach (int mask in new[] { 1, 21, DiceValues.AllMask })
            {
                tray.SetSelection(mask);
                tray.SetValues(before);
                int count = 0;
                for (int die = 0; die < DiceValues.Count; die++) if ((mask & (1 << die)) != 0) count++;
                var timing = sound.BeginRoll(count);
                var animation = tray.Roll(after, mask, timing);
                float nextCapture = 0;
                int frame = 0;
                try
                {
                    while (animation.MoveNext())
                    {
                        Assert.That(timing.Elapsed, Is.LessThan(2f), "A roll must complete without an extended airborne wait.");
                        for (int die = 0; die < DiceValues.Count; die++)
                            if ((mask & (1 << die)) == 0)
                                AssertResultFaceUp(tray, die, before[die]);
                        if (!string.IsNullOrEmpty(root) && timing.Elapsed >= nextCapture)
                        {
                            nextCapture += .05f;
                            yield return new WaitForEndOfFrame();
                            var previous = RenderTexture.active;
                            var pixels = new Texture2D(tray.Texture.width, tray.Texture.height, TextureFormat.RGB24, false);
                            try
                            {
                                RenderTexture.active = tray.Texture;
                                pixels.ReadPixels(new Rect(0, 0, pixels.width, pixels.height), 0, 0);
                                pixels.Apply();
                                File.WriteAllBytes(Path.Combine(root, "roll-" + count + "-" + frame.ToString("D3") + ".png"),
                                    pixels.EncodeToPNG());
                                frame++;
                            }
                            finally
                            {
                                RenderTexture.active = previous;
                                UnityEngine.Object.Destroy(pixels);
                            }
                        }
                        else yield return null;
                    }
                }
                finally { ((IDisposable)animation).Dispose(); }
                for (int die = 0; die < DiceValues.Count; die++)
                    AssertResultFaceUp(tray, die, (mask & (1 << die)) != 0 ? after[die] : before[die]);
                Assert.That(tray.IsRolling, Is.False);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static ModifierDefinition Modifier(string id, ModifierPhase phase, IModifierCondition condition,
            ModifierEffectKind effect, long amount)
            => new ModifierDefinition("modifier." + id, "family." + id, phase, condition,
                new[] { new ModifierEffect(effect, amount) });

        private static IEnumerator PlayAndInspect(SpiritEncounterController controller, SpiritEncounterView view,
            EncounterSession expected, string captureName)
        {
            var played = controller.Snapshot.CurrentDice;
            var score = controller.Preview.Score;
            long baseScore = controller.Snapshot.ModifierBaseScore;
            long multiplier = controller.Snapshot.ModifierMultiplier;
            long beforeTarget = controller.Snapshot.RemainingThreshold;
            int beforeRerolls = controller.Snapshot.RerollsRemaining;
            var target = FindLabel(view, "TargetLabel");
            var selection = FindLabel(view, "SelectionLabel");
            var rerolls = FindLabel(view, "RerollsRemainingLabel");
            var actionCue = view.GetComponent<SpiritActionModifierCue>();
            var ready = new List<ModifierContribution>();
            foreach (var contribution in score.ModifierContributions)
                if (contribution.Phase == ModifierPhase.PlayHand) ready.Add(contribution);
            var stages = new List<CounterStage>();
            stages.Add(new CounterStage(baseScore + score.DiceSum, multiplier, "dice", "STEP 1 OF 4 - DICE",
                DiceValues.AllMask));
            baseScore += score.DiceSum + score.CategoryBaseScore + score.UpgradeBaseScore;
            multiplier += score.CategoryMultiplier + score.UpgradeMultiplier;
            stages.Add(new CounterStage(baseScore, multiplier, "category", "STEP 2 OF 4 - HAND RANK",
                SpiritHandScoringHighlights.GetMask(played, score.Category)));
            for (int readyIndex = 0; readyIndex < ready.Count; ++readyIndex)
            {
                var contribution = ready[readyIndex];
                baseScore += contribution.BaseScore;
                multiplier += contribution.Multiplier;
                string cue = contribution.BaseScore != 0 ? "+" + DiagnosticText.Number(contribution.BaseScore) + " base" :
                    contribution.Multiplier != 0 ? "+" + DiagnosticText.Number(contribution.Multiplier) + " mult" :
                    "+" + contribution.Rerolls + " reroll";
                stages.Add(new CounterStage(baseScore, multiplier, "modifier-" + stages.Count,
                    "STEP 3 OF 4 - CHARMS", 0, cue, readyIndex, contribution.Rerolls != 0,
                    contribution.BaseScore != 0 || contribution.Multiplier != 0));
            }
            AssertCounters(view, controller.Snapshot.ModifierBaseScore, controller.Snapshot.ModifierMultiplier, 0);
            AssertHistory(view, score.ModifierContributions);
            controller.PlayHand();
            expected.Execute(PlayHandAction.Instance, expected.Snapshot.Revision);
            float deadline = Time.realtimeSinceStartup + 20f;
            float endpointStarted = -1;
            int observed = 0;
            bool revealedTotal = false;
            bool reducedTarget = false;
            bool resetNextHand = false;
            bool grantedReroll = false;
            bool capturedReduction = false;
            var captures = new List<string>();
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(FindLabel(view, "ScoreBreakdown").text, Is.Not.EqualTo("ADDING HAND UPGRADE"),
                    "The upgraded category must be presented as a single contribution.");
                long total = ReadCounter(view.HandScoreLabel);
                if (observed < stages.Count && stages[observed].Matches(view, selection.text))
                {
                    var stage = stages[observed];
                    Assert.That(view.DiceTray.ScoringHighlightMask, Is.EqualTo(stage.HighlightMask), stage.Name);
                    Assert.That(view.ModifierHistory.ResolvingIndex, Is.EqualTo(stage.ResolvingIndex), stage.Name);
                    AssertHistory(view, score.ModifierContributions);
                    if (stage.ResolvingIndex >= 0)
                    {
                        Assert.That(view.ModifierHistory.Contributions[stage.ResolvingIndex],
                            Is.EqualTo(ready[stage.ResolvingIndex]), "The resolving token must be the current contribution.");
                        Assert.That(actionCue.IsRerollVisible, Is.EqualTo(stage.RerollCue), stage.Name);
                        Assert.That(actionCue.IsPlayVisible, Is.EqualTo(stage.PlayCue), stage.Name);
                    }
                    if (endpointStarted < 0)
                    {
                        endpointStarted = Time.realtimeSinceStartup;
                        RequestCapture(captureName + "-" + stages[observed].Name, captures);
                    }
                    if (Time.realtimeSinceStartup - endpointStarted >= .08f)
                    {
                        ++observed;
                        endpointStarted = -1;
                    }
                }
                else endpointStarted = -1;
                if (total != 0)
                {
                    Assert.That(observed, Is.EqualTo(stages.Count),
                        "Total must stay zero until every contribution endpoint has been held for the player.");
                    if (total == score.HandScore && !revealedTotal)
                    {
                        AssertHistory(view, score.ModifierContributions);
                        Assert.That(view.DiceTray.ScoringHighlightMask, Is.Zero);
                        Assert.That(view.ModifierHistory.ResolvingIndex, Is.EqualTo(-1));
                        Assert.That(FindLabel(view, "ScoreBreakdown").text, Does.StartWith("STEP 4 OF 4 - "));
                        revealedTotal = true;
                        RequestCapture(captureName + "-total", captures);
                    }
                }
                if (!revealedTotal)
                    Assert.That(target.text, Is.EqualTo(DiagnosticText.Number(beforeTarget)),
                        "The target must remain unchanged until the final Total is revealed.");
                if (revealedTotal && !capturedReduction && target.text != DiagnosticText.Number(beforeTarget))
                {
                    capturedReduction = true;
                    RequestCapture(captureName + "-goal-reduction", captures);
                }
                if (target.text == DiagnosticText.Number(expected.Snapshot.RemainingThreshold)) reducedTarget = true;
                if (!reducedTarget)
                    for (int die = 0; die < DiceValues.Count; ++die)
                        AssertResultFaceUp(view.DiceTray, die, played[die]);
                foreach (var contribution in score.ModifierContributions)
                {
                    if (contribution.Phase != ModifierPhase.PlayHand || contribution.Rerolls == 0) continue;
                    if (selection.text.Contains("+" + contribution.Rerolls + " reroll"))
                    {
                        Assert.That(rerolls.text, Is.EqualTo("REROLLS  " + (beforeRerolls + contribution.Rerolls)),
                            "A resource grant must appear with its modifier activation.");
                        grantedReroll = true;
                    }
                    else if (!grantedReroll)
                        Assert.That(rerolls.text, Is.EqualTo("REROLLS  " + beforeRerolls),
                            "A resource grant must not appear before its modifier activation.");
                }
                if (revealedTotal && reducedTarget && total == 0 && expected.Snapshot.Phase == EncounterPhase.Playing)
                {
                    if (!resetNextHand)
                    {
                        AssertCounters(view, 0, 0, 0);
                        Assert.That(view.ModifierHistory.Count, Is.Zero, "Ready activations clear before the next roll.");
                        resetNextHand = true;
                        RequestCapture(captureName + "-next-hand-reset", captures);
                    }
                }
                yield return null;
            }
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(observed, Is.EqualTo(stages.Count), "The ordered score contribution endpoints were not all held.");
            Assert.That(revealedTotal, Is.True);
            reducedTarget |= target.text == DiagnosticText.Number(expected.Snapshot.RemainingThreshold);
            Assert.That(reducedTarget, Is.True);
            if (expected.Snapshot.Phase == EncounterPhase.Playing)
            {
                Assert.That(resetNextHand, Is.True, "The next hand must visibly reset all counters before initial bonuses.");
                AssertCounters(view, expected.Snapshot.ModifierBaseScore, expected.Snapshot.ModifierMultiplier, 0);
            }
            Assert.That(controller.Snapshot.TotalScore, Is.EqualTo(expected.Snapshot.TotalScore));
            AssertHistory(view, expected.Snapshot.Phase == EncounterPhase.Playing
                ? controller.Preview.Score.ModifierContributions : Array.Empty<ModifierContribution>());
            for (int frame = 0; frame < 120 && captures.Exists(path => !File.Exists(path)); ++frame) yield return null;
            foreach (string path in captures) Assert.That(File.Exists(path), Is.True, path);
            yield return Capture(captureName + "-complete");
        }

        private sealed class CounterStage
        {
            public readonly long BaseScore;
            public readonly long Multiplier;
            public readonly string Name;
            public readonly int HighlightMask;
            public readonly int ResolvingIndex;
            public readonly bool RerollCue;
            public readonly bool PlayCue;
            private readonly string cue;
            private readonly string step;

            public CounterStage(long baseScore, long multiplier, string name, string step, int highlightMask,
                string cue = null, int resolvingIndex = -1, bool rerollCue = false, bool playCue = false)
            {
                BaseScore = baseScore;
                Multiplier = multiplier;
                Name = name;
                HighlightMask = highlightMask;
                ResolvingIndex = resolvingIndex;
                RerollCue = rerollCue;
                PlayCue = playCue;
                this.cue = cue;
                this.step = step;
            }

            public bool Matches(SpiritEncounterView view, string selection)
                => ReadCounter(view.BaseScoreLabel) == BaseScore && ReadCounter(view.MultiplierLabel) == Multiplier &&
                    ReadCounter(view.HandScoreLabel) == 0 && FindLabel(view, "ScoreBreakdown").text == step &&
                    (cue == null || selection.Contains(cue));
        }

        private static long ReadCounter(Text label)
        {
            Assert.That(label, Is.Not.Null);
            Assert.That(long.TryParse(label.text, System.Globalization.NumberStyles.AllowThousands,
                System.Globalization.CultureInfo.InvariantCulture, out long value), Is.True, label.name + ": " + label.text);
            return value;
        }

        private static void AssertCounters(SpiritEncounterView view, long baseScore, long multiplier, long total)
        {
            Assert.That(ReadCounter(view.BaseScoreLabel), Is.EqualTo(baseScore), "Base counter");
            Assert.That(ReadCounter(view.MultiplierLabel), Is.EqualTo(multiplier), "Multiplier counter");
            Assert.That(ReadCounter(view.HandScoreLabel), Is.EqualTo(total), "Total counter");
        }

        private static void AssertHistory(SpiritEncounterView view, IReadOnlyList<ModifierContribution> expected)
        {
            var ready = new List<ModifierContribution>();
            foreach (var contribution in expected)
                if (contribution.Phase == ModifierPhase.PlayHand) ready.Add(contribution);
            Assert.That(view.ModifierHistory.Count, Is.EqualTo(ready.Count));
            Assert.That(view.ModifierHistory.Renderer.Count, Is.EqualTo(ready.Count));
            for (int index = 0; index < ready.Count; ++index)
                Assert.That(view.ModifierHistory.Contributions[index], Is.EqualTo(ready[index]),
                    "The ready tray must preserve PlayHand activation order and repeated instances.");
        }

        private static void AssertResultFaceUp(SpiritDiceTray tray, int index, int face)
        {
            var normals = new[] { Vector3.forward, Vector3.up, Vector3.right,
                Vector3.left, Vector3.down, Vector3.back };
            Transform die = null;
            foreach (var candidate in tray.RenderRoot.GetComponentsInChildren<Transform>(true))
                if (candidate.name == "Spirit Die " + index) { die = candidate; break; }
            Assert.That(die, Is.Not.Null, "Die " + index);
            Assert.That(Vector3.Dot(die.TransformDirection(normals[face - 1]), Vector3.up), Is.GreaterThan(.95f),
                "The settled result face must stay upward in slot " + index);
        }

        private static void AssertHistoryGeometry(SpiritModifierHistoryRenderer renderer)
        {
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.RenderCamera, Is.Not.Null);
            var view = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterView>();
            Assert.That(renderer.RenderCamera, Is.SameAs(view.TrayScene.RenderCamera));
            Assert.That(renderer.RenderCamera.orthographic, Is.EqualTo(view.TrayScene.Presentation.Orthographic));
            Assert.That(renderer.RenderCamera.targetTexture, Is.SameAs(renderer.Texture));
            Assert.That(renderer.Texture.width, Is.GreaterThan(100));
            Assert.That(renderer.Texture.height, Is.GreaterThan(50));
            var body = renderer.GetDieRenderer(0);
            Assert.That(body, Is.TypeOf<MeshRenderer>());
            var filter = body.GetComponent<MeshFilter>();
            Assert.That(filter, Is.Not.Null);
            var mesh = filter.sharedMesh;
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.vertexCount, Is.GreaterThan(24), "The body must contain rounded geometry.");
            Assert.That(Array.Exists(mesh.normals, normal => Mathf.Abs(normal.x) > .1f && Mathf.Abs(normal.y) > .1f),
                Is.True, "Rounded edges must have normals between the cube face normals.");
            Assert.That(mesh.bounds.size.x, Is.GreaterThan(.5f));
            Assert.That(mesh.bounds.size.y, Is.GreaterThan(.5f));
            Assert.That(mesh.bounds.size.z, Is.GreaterThan(.5f));
            var bone = body.sharedMaterial;
            var boneTexture = view.TrayScene.Presentation.BoneTexture;
            var woodTexture = view.TrayScene.Presentation.WoodTexture;
            Assert.That(boneTexture, Is.Not.Null);
            Assert.That(woodTexture, Is.Not.Null);
            Assert.That(bone.GetTexture("_SurfaceTexture"), Is.SameAs(boneTexture));
            Assert.That(bone.GetFloat("_ShowPips"), Is.Zero, "History faces must show their modifier icon instead of pips.");
            Assert.That(bone.GetFloat("_ShowFaceIcon"), Is.EqualTo(1f));
            Assert.That(bone.GetTexture("_FaceIcon"), Is.Not.Null, "The modifier icon must be mapped onto the die face.");
            var firstDefinition = view.ModifierHistory.Contributions[0].DefinitionId;
            var secondDefinition = view.ModifierHistory.Contributions[1].DefinitionId;
            if (firstDefinition == secondDefinition)
                Assert.That(bone.GetTexture("_FaceIcon"),
                    Is.SameAs(renderer.GetDieRenderer(1).sharedMaterial.GetTexture("_FaceIcon")),
                    "Duplicate ready instances must retain their modifier's mapped sigil.");
            else
                Assert.That(bone.GetTexture("_FaceIcon"),
                    Is.Not.SameAs(renderer.GetDieRenderer(1).sharedMaterial.GetTexture("_FaceIcon")),
                    "Distinct modifier families must retain distinct mapped sigils.");
            Vector4 iconRect = bone.GetVector("_FaceIconRect");
            Assert.That(iconRect.x, Is.InRange(0f, 1f));
            Assert.That(iconRect.y, Is.InRange(0f, 1f));
            Assert.That(iconRect.z, Is.GreaterThan(0f));
            Assert.That(iconRect.w, Is.GreaterThan(0f));
            Assert.That(Vector3.Dot(renderer.GetDieTransform(0).up, Vector3.up), Is.GreaterThan(.95f),
                "The icon-bearing top face must settle facing upward.");
            Assert.That(renderer.GetShadowRenderer(0).sharedMaterial, Is.Not.Null);
            bool texturedWood = false;
            foreach (var part in view.TrayScene.Geometry.GetComponentsInChildren<MeshRenderer>())
            {
                var material = part.sharedMaterial;
                if (material != null && material.HasProperty("_SurfaceTexture") &&
                    material.GetTexture("_SurfaceTexture") == woodTexture)
                    texturedWood = true;
            }
            Assert.That(texturedWood, Is.True, "The fixed physical tray must use textured wooden mesh surfaces.");
        }

        private static float HistoryDieMinimumHeight(SpiritModifierHistoryRenderer renderer, int index)
        {
            var body = renderer.GetDieRenderer(index);
            var mesh = body.GetComponent<MeshFilter>().sharedMesh;
            float minimum = float.PositiveInfinity;
            foreach (var vertex in mesh.vertices)
                minimum = Mathf.Min(minimum, body.transform.TransformPoint(vertex).y);
            return minimum;
        }

        private static void AssertHistoryDieOnFloor(SpiritModifierHistoryRenderer renderer, int index)
        {
            Assert.That(HistoryDieMinimumHeight(renderer, index),
                Is.EqualTo(renderer.TrayRoot.position.y).Within(.003f),
                "The settled rounded body must contact the tray floor without floating or sinking.");
            AssertHistoryDieWithinFrame(renderer, index);
        }

        private static Vector2 HistoryDieXBounds(SpiritModifierHistoryRenderer renderer, int index)
        {
            var body = renderer.GetDieRenderer(index);
            Vector2 bounds = new Vector2(float.PositiveInfinity, float.NegativeInfinity);
            foreach (Vector3 vertex in body.GetComponent<MeshFilter>().sharedMesh.vertices)
            {
                float x = renderer.TrayRoot.InverseTransformPoint(body.transform.TransformPoint(vertex)).x;
                bounds.x = Mathf.Min(bounds.x, x); bounds.y = Mathf.Max(bounds.y, x);
            }
            return bounds;
        }

        private static void AssertHistoryDieWithinFrame(SpiritModifierHistoryRenderer renderer, int index)
        {
            var body = renderer.GetDieRenderer(index);
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            foreach (var vertex in body.GetComponent<MeshFilter>().sharedMesh.vertices)
            {
                float y = renderer.RenderCamera.WorldToViewportPoint(body.transform.TransformPoint(vertex)).y;
                minimum = Mathf.Min(minimum, y);
                maximum = Mathf.Max(maximum, y);
            }
            Assert.That(minimum, Is.GreaterThan(0f), "The full die must fit above the render's bottom edge.");
            Assert.That(maximum, Is.LessThan(1f), "The die top must stay within the render during arrival and at rest.");
        }

        private static void ClickHistoryDie(SpiritModifierHistoryTray tray, int index)
        {
            Assert.That(EventSystem.current, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            var imageRect = tray.GetComponent<RectTransform>();
            Rect projection = tray.Renderer.GetDieViewportRect(index);
            Vector2 localPoint = imageRect.rect.min + Vector2.Scale(projection.center, imageRect.rect.size);
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, imageRect.TransformPoint(localPoint)),
                button = PointerEventData.InputButton.Left,
                pointerId = -1
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), "The projected history die must receive a pointer hit.");
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.SameAs(tray.Content.GetChild(index).gameObject),
                "The pointer target must follow the die's position in the render texture.");
            pointer.pointerCurrentRaycast = hits[0];
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static IEnumerator CaptureHistoryTexture(SpiritModifierHistoryRenderer renderer, string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = renderer.Texture;
            var previous = RenderTexture.active;
            var pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, pixels.width, pixels.height), 0, 0);
                pixels.Apply();
                int opaquePixels = 0;
                int bonePixels = 0;
                int woodPixels = 0;
                foreach (var pixel in pixels.GetPixels32())
                {
                    if (pixel.a < 200) continue;
                    ++opaquePixels;
                    if (pixel.r > 140 && pixel.g > 120 && pixel.b > 85) ++bonePixels;
                    if (pixel.r > 30 && pixel.r > pixel.g * 1.3f && pixel.g > pixel.b * 1.1f) ++woodPixels;
                }
                Assert.That(opaquePixels, Is.GreaterThan(texture.width * texture.height / 20),
                    "The history camera must render the physical tray.");
                Assert.That(bonePixels, Is.GreaterThan(20), "The history camera must render the bone dice.");
                Assert.That(woodPixels, Is.GreaterThan(20), "The history camera must render the wooden tray.");
                string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
                if (!string.IsNullOrEmpty(root))
                {
                    Directory.CreateDirectory(root);
                    string path = Path.Combine(root, name + ".png");
                    Assert.That(File.Exists(path), Is.False, "Each validation run must use a fresh capture directory.");
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                    TestContext.WriteLine("History texture screenshot: " + path);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(pixels);
            }
        }

        private static Text FindLabel(SpiritEncounterView view, string name)
        {
            foreach (var label in view.GetComponentsInChildren<Text>(true))
                if (label.name == name) return label;
            Assert.Fail("Presentation label was not found: " + name);
            return null;
        }

        private static void RequestCapture(string name, List<string> captures)
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) return;
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "feedback-" + name + ".png");
            Assert.That(File.Exists(path), Is.False);
            captures.Add(path);
            ScreenCapture.CaptureScreenshot(path);
        }

        private static IEnumerator WaitForPresentation(SpiritEncounterController controller)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
        }

        private static IEnumerator Capture(string name)
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) yield break;
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "feedback-" + name + ".png");
            Assert.That(File.Exists(path), Is.False);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            for (int frame = 0; frame < 120 && !File.Exists(path); ++frame) yield return null;
            Assert.That(File.Exists(path), Is.True);
        }
    }
}
