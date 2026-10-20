using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public sealed class SpiritModifierReadabilityPlayTests
    {
        private static readonly string[] Ids =
        {
            "modifier.initial_roll.initial_pair.base_score",
            "modifier.player_reroll.selected_face_unchanged.rerolls",
            "modifier.player_reroll.last_reroll_on_last_hand.combined",
            "modifier.play_hand.no_extreme_faces.base_score",
            "modifier.play_hand.contains_face.multiplier",
            "modifier.play_hand.middle_unique.combined"
        };

        [UnityTest]
        public IEnumerator RealCharmActivationsLinkTheirIconToOnlyTheAffectedCounters()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit();
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = ContentReady.EncounterView(controller);
            var standard = controller.Catalog;
            var actionCue = view.GetComponent<SpiritActionModifierCue>();
            Assert.That(actionCue, Is.Not.Null);
            Assert.That(view.GetComponentsInChildren<SpiritActionModifierCue>(true).Length, Is.EqualTo(1),
                "Both action buttons must share one modifier cue component.");
            foreach (string id in Ids)
            {
                var catalog = ReadabilityCatalog(standard, id, out var definition, out var encounter, out var action);
                ulong seed = FindActivationSeed(catalog, encounter.Id, action, id);
                var expected = EncounterSession.CreateEncounter(catalog, encounter.Id, seed);
                var resolution = expected.Execute(action, expected.Snapshot.Revision);
                var activation = resolution.Events.OfType<ModifierActivatedEvent>().Single(value => value.DefinitionId == id);
                actionCue.Show(definition, 1.07f);
                Assert.That(actionCue.IsVisible, Is.True);
                controller.Initialize(catalog, encounter.Id, seed, SavePath());
                yield return ContentReady.Routed(controller);
                view = ContentReady.EncounterView(controller);
                AssertActionCueCleared(view, actionCue);
                yield return null;
                if (action is RerollDiceAction)
                {
                    controller.ToggleDie(0);
                    controller.Reroll();
                }
                else controller.PlayHand();

                bool observed = false;
                var icon = Find<Image>(view, "ModifierActivationIcon");
                var cueGroup = ContentReady.ActionCueGroup(view, definition.Phase == ModifierPhase.PlayHand);
                float deadline = Time.realtimeSinceStartup + 25;
                while (controller.IsBusy && Time.realtimeSinceStartup < deadline)
                {
                    if (icon.gameObject.activeInHierarchy && icon.sprite == SpiritModifierIcons.GetSprite(definition))
                    {
                        Assert.That(Find<Text>(view, "SelectionLabel").text,
                            Does.Contain(SpiritModifierPresentation.GetName(definition)));
                        if (definition.Phase == ModifierPhase.PlayHand)
                        {
                            int index = view.ModifierHistory.ResolvingIndex;
                            Assert.That(index, Is.GreaterThanOrEqualTo(0));
                            Assert.That(view.ModifierHistory.Renderer.GetDieRenderer(index).sharedMaterial.GetTexture("_FaceIcon"),
                                Is.SameAs(icon.sprite.texture), "Ready tray and counter cues must use the same artwork.");
                        }
                        else Assert.That(view.ModifierHistory.Count, Is.Zero,
                            "Roll activations use transient cues instead of ready tiles.");
                        if (!observed && cueGroup.alpha > .95f)
                        {
                            AssertActionCue(view, actionCue, definition);
                            observed = true;
                            yield return Capture("activation-" + Array.IndexOf(Ids, id));
                        }
                    }
                    yield return null;
                }
                Assert.That(controller.IsBusy, Is.False);
                Assert.That(observed, Is.True, id + " must visibly identify its activation.");
                Assert.That(controller.Snapshot.TotalScore, Is.EqualTo(expected.Snapshot.TotalScore));
                Assert.That(controller.Snapshot.CurrentDice, Is.EqualTo(expected.Snapshot.CurrentDice));
                Assert.That(controller.Snapshot.RemainingThreshold, Is.EqualTo(expected.Snapshot.RemainingThreshold));
                Assert.That(controller.Snapshot.RerollsRemaining, Is.EqualTo(expected.Snapshot.RerollsRemaining));
                foreach (string counter in new[] { "BaseScoreCharmIcon", "MultiplierCharmIcon", "RerollsCharmIcon", "HandsCharmIcon" })
                    Assert.That(Find<Image>(view, counter).gameObject.activeSelf, Is.False, "Transient cues must clear: " + counter);
                AssertActionCueCleared(view, actionCue);
                controller.Save();
                actionCue.Show(definition, 1.07f);
                Assert.That(actionCue.IsVisible, Is.True);
                controller.Load();
                yield return ContentReady.Routed(controller);
                AssertActionCueCleared(view, actionCue);
                Assert.That(Find<Image>(view, "BaseScoreCharmIcon").gameObject.activeSelf, Is.False);
                Assert.That(controller.IsBusy, Is.False);
            }
            var original = standard.GetModifier(Ids[3]);
            var custom = new ModifierDefinition("modifier.readability.custom", "family.readability.custom",
                original.Phase, original.Condition, original.Effects, original.ActivationLimit);
            actionCue.Show(custom, 1.07f);
            Assert.That(actionCue.IsVisible, Is.True);
            Assert.That(actionCue.ActivePhase, Is.EqualTo(custom.Phase));
            Assert.That(actionCue.ActiveSprite, Is.Null);
            var fallback = ContentReady.ActionCueGroup(view, true);
            var sigil = Find<SpiritModifierSigilGraphic>(fallback, "Sigil");
            Assert.That(sigil.gameObject.activeInHierarchy, Is.True);
            Assert.That(sigil.Seed, Is.EqualTo(SpiritModifierIcons.GetSigilSeed(custom)));
            Assert.That(sigil.raycastTarget, Is.False);
            Assert.That(Find<Image>(fallback, "Artwork").gameObject.activeSelf, Is.False);
            actionCue.Clear();
            AssertActionCueCleared(view, actionCue);
            LogAssert.NoUnexpectedReceived();
        }

        /**
         * Steps playback's contribution animation to its first yield, where every counter highlight is already set,
         * so the actual icons are inspected without depending on rendered frames, fades, or animation duration.
         * This verifies how a contribution lights the HUD, not whether Play dispatches the contribution.
         */
        [UnityTest]
        public IEnumerator SteppedCharmActivationsLightOnlyTheAffectedCounterIcons()
        {
            var resetTransientVisuals = typeof(SpiritEncounterView).GetMethod("ResetTransientVisuals",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(resetTransientVisuals, Is.Not.Null);
            string[] counters = { "BaseScoreCharmIcon", "MultiplierCharmIcon", "RerollsCharmIcon", "HandsCharmIcon" };
            foreach (string id in Ids)
            {
                // A fresh scene per charm keeps each run's catalog from meeting the previous run's hand preview.
                yield return SceneManager.LoadSceneAsync("Application");
                yield return ContentReady.Spirit();
                yield return null;
                var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
                var view = ContentReady.EncounterView(controller);
                var playback = typeof(SpiritEncounterView).GetField("scorePlayback", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(view);
                Assert.That(playback, Is.Not.Null);
                var animateContribution = playback.GetType().GetMethod("AnimateContribution",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(animateContribution, Is.Not.Null);
                var catalog = ReadabilityCatalog(controller.Catalog, id, out var definition, out var encounter, out var action);
                ulong seed = FindActivationSeed(catalog, encounter.Id, action, id);
                var session = EncounterSession.CreateEncounter(catalog, encounter.Id, seed);
                var resolution = session.Execute(action, session.Snapshot.Revision);
                var activation = resolution.Events.OfType<ModifierActivatedEvent>().Single(value => value.DefinitionId == id);
                controller.Initialize(catalog, encounter.Id, seed, SavePath());
                yield return ContentReady.Routed(controller);
                view = ContentReady.EncounterView(controller);
                yield return Wait(controller);
                foreach (string counter in counters)
                    Assert.That(Find<Image>(view, counter).gameObject.activeSelf, Is.False, id + " starts lit: " + counter);

                bool playHand = definition.Phase == ModifierPhase.PlayHand;
                var contribution = activation.Contribution;
                int stageIndex = activation.StageIndex;
                int handIndex = activation.HandIndex;
                if (playHand)
                {
                    // Playback animates Play Hand charms from the scored hand after readying them on the tray.
                    var scored = resolution.Events.OfType<HandScoredEvent>().Single();
                    contribution = scored.Score.ModifierContributions.Single(value =>
                        value.DefinitionId == id && value.Phase == ModifierPhase.PlayHand);
                    stageIndex = scored.StageIndex;
                    handIndex = scored.HandIndex;
                    view.ModifierHistory.SynchronizeReady(scored.Score.ModifierContributions, catalog);
                }
                try
                {
                    var step = (IEnumerator)animateContribution.Invoke(playback,
                        new object[] { contribution, stageIndex, handIndex, resolution, !playHand });
                    Assert.That(step.MoveNext(), Is.True, id);
                    Assert.That(step.Current, Is.InstanceOf<IEnumerator>(), id + " must stop at its score animation.");
                    AssertCounterIcon(view, "BaseScoreCharmIcon", definition, contribution.BaseScore != 0);
                    AssertCounterIcon(view, "MultiplierCharmIcon", definition, contribution.Multiplier != 0);
                    AssertCounterIcon(view, "RerollsCharmIcon", definition, contribution.Rerolls != 0);
                    AssertCounterIcon(view, "HandsCharmIcon", definition, false);
                }
                finally
                {
                    resetTransientVisuals.Invoke(view, null);
                    view.ModifierHistory.Clear();
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ActionCueCrossfadesItsLabelAndRestoresItOnRetriggerAndDisable()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit();
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = ContentReady.EncounterView(controller);
            var cue = view.GetComponent<SpiritActionModifierCue>();
            var playGroup = ContentReady.ActionCueGroup(view, true);
            var playLabel = view.PlayButton.GetComponentInChildren<Text>().GetComponent<CanvasGroup>();
            var rerollGroup = ContentReady.ActionCueGroup(view, false);
            var rerollLabel = view.RerollButton.GetComponentInChildren<Text>().GetComponent<CanvasGroup>();
            Assert.That(playLabel, Is.Not.Null);
            Assert.That(rerollLabel, Is.Not.Null);

            cue.Show(controller.Catalog.GetModifier(Ids[3]), 1f);
            Assert.That(playGroup.alpha, Is.EqualTo(0f));
            Assert.That(playLabel.alpha, Is.EqualTo(1f));
            float deadline = Time.realtimeSinceStartup + 2;
            while (cue.IsVisible && playGroup.alpha < .95f && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(playLabel.alpha + playGroup.alpha, Is.EqualTo(1f).Within(.001f));
                yield return null;
            }
            Assert.That(cue.IsVisible, Is.True);
            Assert.That(playGroup.alpha, Is.GreaterThanOrEqualTo(.95f));
            Assert.That(playLabel.alpha, Is.LessThanOrEqualTo(.05f));
            while (cue.IsVisible && playGroup.alpha > .7f && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(playLabel.alpha + playGroup.alpha, Is.EqualTo(1f).Within(.001f));
                yield return null;
            }
            Assert.That(cue.IsVisible, Is.True, "The label must recover while the cue is still fading out.");
            Assert.That(playGroup.alpha, Is.GreaterThan(0f).And.LessThanOrEqualTo(.7f));
            Assert.That(playLabel.alpha, Is.GreaterThanOrEqualTo(.3f));
            Assert.That(playLabel.alpha + playGroup.alpha, Is.EqualTo(1f).Within(.001f));

            cue.Show(controller.Catalog.GetModifier(Ids[1]), 1f);
            Assert.That(playGroup.gameObject.activeSelf, Is.False);
            Assert.That(playLabel.alpha, Is.EqualTo(1f));
            Assert.That(rerollGroup.gameObject.activeSelf, Is.True);
            Assert.That(rerollGroup.alpha, Is.EqualTo(0f));
            Assert.That(rerollLabel.alpha, Is.EqualTo(1f));
            deadline = Time.realtimeSinceStartup + .5f;
            while (cue.IsVisible && rerollGroup.alpha == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(rerollGroup.alpha, Is.GreaterThan(0f));
            Assert.That(rerollLabel.alpha + rerollGroup.alpha, Is.EqualTo(1f).Within(.001f));
            cue.enabled = false;
            AssertActionCueCleared(view, cue);
            cue.enabled = true;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator EveryCharmRuleFitsCompactCardsAtNarrowPortraitWidth()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit();
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = ContentReady.EncounterView(controller);
            var portrait = ContentReady.Portrait();
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(443, 912);
            portrait.anchoredPosition = Vector2.zero;
            var card = SpiritRewardCard.Create(ContentReady.RewardCardPrefab(), "CompactCatalogReward", portrait, (offer, option) => { });
            var cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(.5f, .5f);
            cardRect.sizeDelta = new Vector2(443 * .95f - 48, 160);
            foreach (var definition in controller.Catalog.Modifiers)
            {
                card.RenderModifier(definition, 1, 1);
                yield return null;
                LayoutRebuilder.ForceRebuildLayoutImmediate(cardRect);
                cardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, LayoutUtility.GetPreferredHeight(cardRect));
                Canvas.ForceUpdateCanvases();
                var theme = card.GetComponentInParent<SpiritUiContext>().Theme;
                Assert.That(Find<Text>(card, "Condition").text, Does.StartWith("<b><color=#" +
                    ColorUtility.ToHtmlStringRGB(theme.GetModifierTriggerColor(definition.Phase)) + ">"));
                Assert.That(Find<Text>(card, "Effects").color, Is.EqualTo(SpiritModifierIcons.GetEffectColor(definition)));
                foreach (string name in new[] { "Title", "Effects", "Condition" })
                {
                    var label = Find<Text>(card, name);
                    AssertCompleteRewardLabelInsideBounds(label, definition.Id);
                    var corners = new Vector3[4];
                    label.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                        Assert.That(cardRect.rect.Contains(cardRect.InverseTransformPoint(corner)), Is.True,
                            definition.Id + "/" + name + " exceeds the compact card.");
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LargerCharmOffersKeepReadableRowsAndAllowScrolling()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit();
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = ContentReady.EncounterView(controller);
            var standard = controller.Catalog;
            var definitions = standard.Modifiers.Where(definition => definition.RewardEligible &&
                    definition.Availability.Access == ModifierAccess.Starter &&
                    definition.Availability.Scope == ModifierAvailabilityScope.Global)
                .GroupBy(definition => definition.FamilyId).Select(family => family.First()).Take(5).ToArray();
            Assert.That(definitions.Length, Is.EqualTo(5));
            var pool = new ModifierRewardPoolDefinition("pool.large-reward", definitions.Select(definition => definition.Id));
            var policy = new RewardPolicyDefinition("rewards.large-reward",
                new[] { new ModifierRewardLayout(Enumerable.Repeat(pool.Id, 5)) }, 1, 1, 1, 3,
                new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) });
            var encounter = new EncounterDefinition("encounter.large-reward",
                new[] { new StageDefinition(1, 3, 3, StageExitBehavior.AwaitRewards), new StageDefinition(100000, 3, 3) },
                rewardPolicy: policy);
            var catalog = new MechanicalCatalog("large-reward.v1", standard.Hands, new[] { encounter },
                definitions, new[] { pool });
            controller.Initialize(catalog, encounter.Id, 60, SavePath());
            yield return ContentReady.Routed(controller);
            view = ContentReady.EncounterView(controller);
            controller.PlayHand();
            yield return Wait(controller);
            Canvas.ForceUpdateCanvases();
            var scroll = Find<ScrollRect>(view, "RewardScroll");
            Assert.That(controller.Snapshot.PendingReward.Options.Count, Is.EqualTo(5));
            Assert.That(scroll.vertical, Is.True);
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            for (int i = 1; i <= 5; ++i)
            {
                var card = Find<Button>(view, "RewardCard" + i);
                Assert.That(((RectTransform)card.transform).rect.height, Is.GreaterThanOrEqualTo(180f - .1f));
                foreach (var label in card.GetComponentsInChildren<Text>())
                    AssertCompleteRewardLabelInsideBounds(label, "large offer/" + i);
            }
            Assert.That(Find<Button>(view, "UpgradeHandButton").IsInteractable(), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RewardAndOwnedCharmCardsShareAuthoredArtworkAndDescriptions()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit();
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = ContentReady.EncounterView(controller);
            var standard = controller.Catalog;
            var pool = new ModifierRewardPoolDefinition("pool.readability", Ids);
            var policy = new RewardPolicyDefinition("rewards.readability",
                new[] { new ModifierRewardLayout(new[] { pool.Id, pool.Id, pool.Id }) }, 1, 1, 1, 3,
                new[] { new HandUpgradeHistoryRule(int.MaxValue, 0) });
            var encounter = new EncounterDefinition("encounter.readability-reward",
                new[] { new StageDefinition(1, 3, 3, StageExitBehavior.AwaitRewards), new StageDefinition(100000, 3, 3) },
                rewardPolicy: policy);
            var catalog = new MechanicalCatalog("readability-reward.v1", standard.Hands, new[] { encounter },
                Ids.Select(standard.GetModifier), new[] { pool });
            controller.Initialize(catalog, encounter.Id, 60, SavePath());
            yield return ContentReady.Routed(controller);
            view = ContentReady.EncounterView(controller);
            controller.PlayHand();
            yield return Wait(controller);
            var offer = controller.Snapshot.PendingReward;
            Assert.That(offer, Is.Not.Null);
            Assert.That(offer.Kind, Is.EqualTo(RewardKind.Modifier));
            for (int i = 0; i < offer.Options.Count; ++i)
            {
                var definition = catalog.GetModifier(offer.Options[i].DefinitionId);
                var card = Find<Button>(view, "RewardCard" + (i + 1));
                var image = Find<Image>(card, "CharmIcon");
                Assert.That(image.gameObject.activeInHierarchy, Is.True);
                Assert.That(image.sprite, Is.SameAs(SpiritModifierIcons.GetSprite(definition)));
                Assert.That(Find<Text>(card, "Title").text, Is.EqualTo(SpiritModifierPresentation.GetName(definition)));
                var theme = card.GetComponentInParent<SpiritUiContext>().Theme;
                Assert.That(Find<Text>(card, "Condition").text,
                    Is.EqualTo(SpiritRewardCard.GetModifierDescription(definition,
                        theme.amber, theme.GetModifierTriggerColor(definition.Phase))));
            }
            yield return Capture("rewards");
            AssertCardTextVisible(view);
            var chosen = catalog.GetModifier(offer.Options[0].DefinitionId);
            var beforeCharmSelection = controller.Snapshot;
            Find<Button>(view, "RewardCard1").onClick.Invoke();
            yield return ContentReady.Routed();
            view = ContentReady.EncounterView(controller);
            Assert.That(controller.Snapshot, Is.SameAs(beforeCharmSelection));
            Assert.That(Find<Button>(view, "UpgradeHandButton").IsInteractable(), Is.True);
            Find<Button>(view, "UpgradeHandButton").onClick.Invoke();
            yield return ContentReady.Routed();
            view = ContentReady.EncounterView(controller);
            yield return Wait(controller);
            Assert.That(controller.Snapshot.PendingReward.Kind, Is.EqualTo(RewardKind.HandUpgrade));
            Assert.That(Find<Image>(Find<Button>(view, "RewardCard1"), "CharmIcon").gameObject.activeInHierarchy, Is.False);
            yield return Capture("readability-hand-upgrade-unselected");
            var beforeUpgrade = controller.Snapshot;
            var upgradeButton = Find<Button>(view, "UpgradeHandButton");
            Assert.That(upgradeButton.IsInteractable(), Is.False);
            Find<Button>(view, "RewardCard1").onClick.Invoke();
            yield return ContentReady.Routed();
            view = ContentReady.EncounterView(controller);
            Assert.That(controller.Snapshot, Is.SameAs(beforeUpgrade));
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(upgradeButton.IsInteractable(), Is.True);
            yield return Capture("readability-hand-upgrade-selected");
            upgradeButton.onClick.Invoke();
            yield return ContentReady.Routed();
            view = ContentReady.EncounterView(controller);
            yield return Wait(controller);
            view.ShowCharms();
            var owned = Find<Button>(view, "OwnedCharm1");
            Assert.That(Find<Image>(owned, "CharmIcon").sprite, Is.SameAs(SpiritModifierIcons.GetSprite(chosen)));
            Assert.That(Find<Image>(view, "CharmDetailIcon").sprite, Is.SameAs(SpiritModifierIcons.GetSprite(chosen)));
            yield return Capture("owned-charm");
            AssertCardTextVisible(view);
            view.ShowTable();
            LogAssert.NoUnexpectedReceived();
        }

        private static MechanicalCatalog ReadabilityCatalog(MechanicalCatalog standard, string id,
            out ModifierDefinition definition, out EncounterDefinition encounter, out EncounterAction action)
        {
            definition = standard.GetModifier(id);
            encounter = new EncounterDefinition("encounter.readability",
                new[] { new StageDefinition(1000000, id.Contains("last_reroll") ? 1 : 3, 1) },
                startingModifiers: new[] { id });
            action = definition.Phase == ModifierPhase.PlayerReroll
                ? (EncounterAction)new RerollDiceAction(1) : PlayHandAction.Instance;
            return new MechanicalCatalog("readability.v1", standard.Hands, new[] { encounter }, new[] { definition });
        }

        private static ulong FindActivationSeed(MechanicalCatalog catalog, string encounterId, EncounterAction action, string id)
        {
            for (ulong seed = 1; seed <= 2000; ++seed)
            {
                var session = EncounterSession.CreateEncounter(catalog, encounterId, seed);
                var resolution = session.Execute(action, session.Snapshot.Revision);
                if (resolution.Events.OfType<ModifierActivatedEvent>().Any(value => value.DefinitionId == id)) return seed;
            }
            throw new AssertionException("No legal activation sequence found for " + id);
        }

        private static void AssertCounterIcon(SpiritEncounterView view, string name, ModifierDefinition definition, bool active)
        {
            var image = Find<Image>(view, name);
            Assert.That(image.gameObject.activeInHierarchy, Is.EqualTo(active), name);
            if (active) Assert.That(image.sprite, Is.SameAs(SpiritModifierIcons.GetSprite(definition)), name);
        }

        private static void AssertActionCue(SpiritEncounterView view, SpiritActionModifierCue cue,
            ModifierDefinition definition)
        {
            bool playHand = definition.Phase == ModifierPhase.PlayHand;
            var button = playHand ? view.PlayButton : view.RerollButton;
            var active = ContentReady.ActionCueGroup(view, playHand);
            var inactive = ContentReady.ActionCueGroup(view, !playHand);
            Assert.That(cue.IsVisible, Is.True, definition.Id);
            Assert.That(cue.ActivePhase, Is.EqualTo(definition.Phase), definition.Id);
            Assert.That(cue.ActiveSprite, Is.SameAs(SpiritModifierIcons.GetSprite(definition)), definition.Id);
            Assert.That(active.gameObject.activeInHierarchy, Is.True);
            Assert.That(active.alpha, Is.GreaterThan(0f));
            Assert.That(active.transform.IsChildOf(button.transform), Is.True,
                "The activation cue must occupy its phase's action button.");
            Canvas.ForceUpdateCanvases();
            var buttonRect = (RectTransform)button.transform;
            var cueRect = (RectTransform)active.transform;
            var center = buttonRect.InverseTransformPoint(cueRect.TransformPoint(cueRect.rect.center));
            Assert.That(Vector2.Distance(center, buttonRect.rect.center), Is.LessThan(.01f),
                "The activation cue must be centered on the button face.");
            var corners = new Vector3[4];
            cueRect.GetWorldCorners(corners);
            foreach (var corner in corners)
                Assert.That(buttonRect.rect.Contains(buttonRect.InverseTransformPoint(corner)), Is.True,
                    "The activation cue must fit inside the button face.");
            var label = button.GetComponentInChildren<Text>().GetComponent<CanvasGroup>();
            Assert.That(label, Is.Not.Null);
            Assert.That(label.alpha + active.alpha, Is.EqualTo(1f).Within(.001f),
                "The button label and activation cue must crossfade together.");
            Assert.That(label.alpha, Is.LessThan(.05f));
            Assert.That(inactive.gameObject.activeSelf, Is.False);
            Assert.That(button.IsInteractable(), Is.False, "An activation cue must remain visible while the action is disabled.");
            Assert.That(active.interactable, Is.False);
            Assert.That(active.blocksRaycasts, Is.False);
            var images = active.GetComponentsInChildren<Image>();
            Assert.That(images.Any(image => image.sprite == cue.ActiveSprite), Is.True,
                "The action cue must display the same modifier artwork as the other activation cues.");
            foreach (var graphic in active.GetComponentsInChildren<Graphic>())
                Assert.That(graphic.raycastTarget, Is.False, graphic.name);
        }

        private static void AssertActionCueCleared(SpiritEncounterView view, SpiritActionModifierCue cue)
        {
            Assert.That(cue.IsVisible, Is.False);
            Assert.That(cue.ActivePhase, Is.Null);
            Assert.That(cue.ActiveSprite, Is.Null);
            Assert.That(ContentReady.ActionCueGroup(view, false).gameObject.activeSelf, Is.False);
            Assert.That(ContentReady.ActionCueGroup(view, true).gameObject.activeSelf, Is.False);
            foreach (var button in new[] { view.RerollButton, view.PlayButton })
            {
                var label = button.GetComponentInChildren<Text>().GetComponent<CanvasGroup>();
                Assert.That(label, Is.Not.Null);
                Assert.That(label.alpha, Is.EqualTo(1f), "Clearing the cue must restore its button label.");
            }
        }

        private static void AssertCardTextVisible(SpiritEncounterView view)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var label in view.GetComponentsInChildren<Text>())
            {
                if (label.name != "Status" || string.IsNullOrWhiteSpace(label.text)) continue;
                Assert.That(label.cachedTextGenerator.vertexCount, Is.GreaterThanOrEqualTo(4),
                    "Charm timing and status must fit within the card: " + label.text);
            }
        }

        private static void AssertCompleteRewardLabelInsideBounds(Text label, string definitionId)
        {
            string context = definitionId + "/" + label.name;
            var rect = label.rectTransform.rect;
            var generated = label.cachedTextGenerator;
            Assert.That(generated.Populate(label.text, label.GetGenerationSettings(rect.size)), Is.True, context);
            Assert.That(generated.vertexCount, Is.GreaterThan(4), context + " has no visible glyphs.");
            float unitsPerPixel = 1f / label.pixelsPerUnit;
            // TextGenerator appends a dummy quad after the rendered glyph vertices.
            for (int index = 0; index < generated.verts.Count - 4; ++index)
            {
                Vector2 position = generated.verts[index].position * unitsPerPixel;
                Assert.That(position.x, Is.InRange(rect.xMin - 3f, rect.xMax + 3f), context + " exceeds its horizontal bounds.");
                Assert.That(position.y, Is.InRange(rect.yMin - 3f, rect.yMax + 3f), context + " exceeds its vertical bounds.");
            }
            var settings = label.GetGenerationSettings(rect.size);
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.fontSize = label.resizeTextForBestFit ? generated.fontSizeUsedForBestFit : label.fontSize;
            settings.resizeTextForBestFit = false;
            using (var complete = new TextGenerator())
            {
                Assert.That(complete.Populate(label.text, settings), Is.True, context);
                Assert.That(generated.vertexCount, Is.EqualTo(complete.vertexCount),
                    context + " truncates glyphs or whole lines: " + label.text);
            }
        }

        private static T Find<T>(Component root, string name) where T : Component
        {
            var result = root.GetComponentsInChildren<T>(true).FirstOrDefault(value => value.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
        }

        private static string SavePath() => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
            "spirit-file-tests", Guid.NewGuid().ToString("N"), "readability.json"));

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
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "readability-" + name + ".png");
            Assert.That(File.Exists(path), Is.False);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            for (int frame = 0; frame < 120 && !File.Exists(path); ++frame) yield return null;
            Assert.That(File.Exists(path), Is.True);
        }
    }
}
