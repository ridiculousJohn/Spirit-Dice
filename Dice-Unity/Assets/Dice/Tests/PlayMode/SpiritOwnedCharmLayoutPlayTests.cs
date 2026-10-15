using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public sealed class SpiritOwnedCharmLayoutPlayTests
    {
        [UnityTest]
        public IEnumerator OwnedVariantsAndInspectionsKeepEveryGlyphAtBothPortraitSizes()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit();
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = controller.GetComponent<SpiritEncounterView>();
            const string family = "modifier.play_hand.edge_sum_equals_either";
            var definitions = controller.Catalog.Modifiers.OrderBy(definition =>
                definition.Id == family + ".multiplier" ? 0 : definition.Id == family + ".combined" ? 1 : 2).ToArray();
            Assert.That(definitions.Length, Is.EqualTo(296));
            Assert.That(definitions.All(definition => SpiritModifierIcons.GetSprite(definition) != null), Is.True,
                "Every standard family supplies authored artwork.");
            var encounter = new EncounterDefinition("encounter.owned-charm-layout",
                new[] { new StageDefinition(1000000000, 3, 3) },
                startingModifiers: definitions.Select(definition => definition.Id));
            var catalog = new MechanicalCatalog("owned-charm-layout.v1", controller.Catalog.Hands,
                new[] { encounter }, definitions);
            string savePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                "spirit-file-tests", Guid.NewGuid().ToString("N"), "owned-charms.json"));
            controller.Initialize(catalog, encounter.Id, 60, savePath);
            view.ShowCharms();
            yield return null;
            var snapshot = controller.Snapshot;
            var cards = view.GetComponentsInChildren<Button>().Where(button =>
                button.name.StartsWith("OwnedCharm", StringComparison.Ordinal)).ToArray();
            Assert.That(cards.Length, Is.EqualTo(definitions.Length));
            var scroll = Find<ScrollRect>(view, "OwnedCharmScroll");
            var grid = scroll.content.GetComponent<GridLayoutGroup>();
            Assert.That(grid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
            Assert.That(grid.constraintCount, Is.EqualTo(2));
            var portrait = view.transform.Find("SpiritCanvas/SafeArea/Portrait") as RectTransform;
            var detailTitle = Find<Text>(view, "CharmDetailTitle");
            var detailBody = Find<Text>(view, "CharmDetailBody");
            var reward = SpiritRewardCard.Create("CharmIconComparisonReward", portrait, (offerId, optionId) => { });
            reward.gameObject.SetActive(false);
            var comparedGraphics = new HashSet<bool>();
            foreach (var size in new[] { new Vector2(540, 960), new Vector2(443, 912) })
            {
                portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                portrait.sizeDelta = size;
                portrait.anchoredPosition = Vector2.zero;
                scroll.verticalNormalizedPosition = 1;
                yield return null;
                Canvas.ForceUpdateCanvases();
                float expectedWidth = (scroll.content.rect.width - grid.padding.horizontal - grid.spacing.x) / 2;
                Assert.That(grid.cellSize.x, Is.EqualTo(expectedWidth).Within(.01f));
                Assert.That(((RectTransform)cards[0].transform).rect.width, Is.EqualTo(expectedWidth).Within(.01f));
                for (int index = 0; index < definitions.Length; ++index)
                {
                    var definition = definitions[index];
                    var card = cards[index];
                    var title = CardText(card, "Title");
                    var condition = CardText(card, "Condition");
                    var effects = CardText(card, "Effects");
                    var badge = card.GetComponentInChildren<SpiritCharmStatusBadge>();
                    Assert.That(badge, Is.Not.Null, definition.Id);
                    Assert.That(title.text, Is.EqualTo(SpiritModifierPresentation.GetName(definition)), definition.Id);
                    Assert.That(condition.text, Is.EqualTo(SpiritModifierPresentation.GetConditionText(definition)), definition.Id);
                    Assert.That(effects.text, Is.EqualTo(SpiritModifierPresentation.GetEffectText(definition)), definition.Id);
                    var labels = new[] { title, condition, effects };
                    AssertSections((RectTransform)card.transform, labels, definition.Id);
                    foreach (var label in labels) AssertCompleteText(label, definition.Id + "/" + label.name);
                    AssertBadge(card, badge, definition.Id);
                    AssertOwnedGraphic(card, definition, badge);

                    card.onClick.Invoke();
                    Canvas.ForceUpdateCanvases();
                    Assert.That(detailTitle.text, Is.EqualTo(title.text));
                    string effectColor = ColorUtility.ToHtmlStringRGB(SpiritModifierIcons.GetEffectColor(definition));
                    Assert.That(detailBody.text, Is.EqualTo(condition.text + "\n<color=#" + effectColor + ">" +
                        effects.text + "</color>\n" +
                        SpiritModifierPresentation.GetTimingText(definition) + "\n" +
                        AvailabilityText(definition, controller.Snapshot)));
                    AssertDetailStatus(view, controller, snapshot.Modifiers[index], definition, badge);
                    AssertDetailGraphic(view, definition);
                    if (comparedGraphics.Add(SpiritModifierIcons.GetSprite(definition) != null))
                        AssertRewardGraphic(reward, card, view, definition);
                    AssertSections((RectTransform)detailTitle.transform.parent,
                        new[] { detailTitle, detailBody }, definition.Id + "/inspection");
                    AssertCompleteText(detailTitle, definition.Id + "/inspection title");
                    AssertCompleteText(detailBody, definition.Id + "/inspection body");
                    Assert.That(scroll.viewport.rect.height, Is.GreaterThan(100), "Inspection must leave a usable inventory viewport.");
                }
                cards[0].onClick.Invoke();
                scroll.verticalNormalizedPosition = 1;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(CardText(cards[0], "Title").text, Is.EqualTo("Two Gateposts / Mult"));
                Assert.That(CardText(cards[1], "Title").text, Is.EqualTo("Two Gateposts / Base + Mult"));
                Assert.That(CardText(cards[0], "Effects").text, Is.EqualTo("+3 multiplier"));
                Assert.That(CardText(cards[1], "Effects").text, Is.EqualTo("+10 base score, +1 multiplier"));
                var first = new Vector3[4];
                var second = new Vector3[4];
                ((RectTransform)cards[0].transform).GetWorldCorners(first);
                ((RectTransform)cards[1].transform).GetWorldCorners(second);
                Assert.That(second[0].x, Is.GreaterThan(first[2].x));
                Assert.That(second[2].y, Is.EqualTo(first[2].y).Within(.01f));
                yield return Capture("owned-variants-" + (int)size.x);

                scroll.verticalNormalizedPosition = 0;
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(scroll.content.anchoredPosition.y, Is.GreaterThan(0), "The complete inventory must remain scrollable.");
                var last = (RectTransform)cards[cards.Length - 1].transform;
                var center = scroll.viewport.InverseTransformPoint(last.TransformPoint(last.rect.center));
                Assert.That(center.y, Is.InRange(scroll.viewport.rect.yMin, scroll.viewport.rect.yMax));
                cards[cards.Length - 1].onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                Assert.That(detailTitle.text, Is.EqualTo(CardText(cards[cards.Length - 1], "Title").text));
                yield return Capture("owned-variants-scrolled-" + (int)size.x);
                Assert.That(controller.Snapshot, Is.SameAs(snapshot), "Inspecting and scrolling must preserve the run.");
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MixedCharmStatesAndUseCountsStayReadableAndClickableAtBothPortraitSizes()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            yield return ContentReady.Spirit();
            yield return null;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var view = controller.GetComponent<SpiritEncounterView>();
            var definitions = new[]
            {
                Modifier("ready", ModifierPhase.PlayHand, new AlwaysCondition()),
                Modifier("waiting", ModifierPhase.PlayHand, new NeverCondition()),
                Modifier("initial.used", ModifierPhase.InitialRoll, new AlwaysCondition()),
                Modifier("initial.waiting", ModifierPhase.InitialRoll, new NeverCondition()),
                Modifier("stage.used", ModifierPhase.PlayerReroll, new AlwaysCondition(), ModifierActivationLimit.OncePerStage),
                Modifier("unlimited", ModifierPhase.PlayerReroll, new AlwaysCondition(), ModifierActivationLimit.Unlimited)
            };
            var encounter = new EncounterDefinition("encounter.owned-charm-status",
                new[] { new StageDefinition(1000000000, 3, 3) },
                startingModifiers: definitions.Select(value => value.Id));
            var catalog = new MechanicalCatalog("owned-charm-status.v1", controller.Catalog.Hands,
                new[] { encounter }, definitions);
            string savePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                "spirit-file-tests", Guid.NewGuid().ToString("N"), "owned-charm-status.json"));
            controller.Initialize(catalog, encounter.Id, 60, savePath);
            controller.ToggleDie(0);
            controller.Reroll();
            yield return Wait(controller);
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(1));
            Assert.That(controller.Snapshot.Modifiers.Select(value => value.TotalActivations),
                Is.EqualTo(new long[] { 0, 0, 1, 0, 1, 1 }));
            view.ShowCharms();
            yield return null;
            var cards = view.GetComponentsInChildren<Button>().Where(button =>
                button.name.StartsWith("OwnedCharm", StringComparison.Ordinal)).ToArray();
            Assert.That(cards.Length, Is.EqualTo(definitions.Length));
            var states = new[] { SpiritCharmStatusState.Ready, SpiritCharmStatusState.WaitingCondition,
                SpiritCharmStatusState.Used, SpiritCharmStatusState.OnRoll,
                SpiritCharmStatusState.Used, SpiritCharmStatusState.OnRoll };
            var details = new[] { "Ready", "Waiting", "Activated this hand", "On the initial roll",
                "Activated this stage", "After a reroll" };
            var snapshot = controller.Snapshot;
            var scroll = Find<ScrollRect>(view, "OwnedCharmScroll");
            var portrait = view.transform.Find("SpiritCanvas/SafeArea/Portrait") as RectTransform;
            var symbols = new Dictionary<SpiritCharmStatusState, Vector3[]>();
            foreach (var size in new[] { new Vector2(443, 912), new Vector2(540, 960) })
            {
                portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                portrait.sizeDelta = size;
                portrait.anchoredPosition = Vector2.zero;
                scroll.verticalNormalizedPosition = 1;
                yield return null;
                Canvas.ForceUpdateCanvases();
                for (int index = 0; index < cards.Length; ++index)
                {
                    var card = cards[index];
                    CenterCard(scroll, (RectTransform)card.transform);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    var badge = card.GetComponentInChildren<SpiritCharmStatusBadge>();
                    if (index == cards.Length - 1)
                        yield return Capture("owned-status-unlimited-before-check-" + (int)size.x);
                    Assert.That(badge, Is.Not.Null, definitions[index].Id);
                    Assert.That(badge.Presentation.State, Is.EqualTo(states[index]), definitions[index].Id);
                    Assert.That(badge.Icon.State, Is.EqualTo(states[index]), definitions[index].Id);
                    AssertBadge(card, badge, definitions[index].Id);
                    AssertSymbol(badge.Icon, symbols, definitions[index].Id);
                    Assert.That(SpiritModifierIcons.GetSprite(definitions[index]), Is.Null, definitions[index].Id);
                    AssertOwnedGraphic(card, definitions[index], badge);
                    var sigil = Find<SpiritModifierSigilGraphic>(card, "CharmSigil");
                    AssertSigilMesh(sigil, definitions[index].Id);
                    ClickAt(card, sigil.rectTransform);
                    AssertDetail(view, controller, snapshot.Modifiers[index], definitions[index]);
                    ClickAt(card, badge.Icon.rectTransform);
                    AssertDetail(view, controller, snapshot.Modifiers[index], definitions[index]);
                    AssertDetailStatus(view, controller, snapshot.Modifiers[index], definitions[index], badge, details[index]);
                    if (index % 2 == 1)
                        yield return Capture("owned-status-row-" + (index / 2 + 1) + "-" + (int)size.x);
                }
                Assert.That(controller.Snapshot, Is.SameAs(snapshot), "Charm badge inspection must preserve the run.");
            }
            controller.Abandon();
            yield return Wait(controller);
            Assert.That(controller.Snapshot.Phase, Is.EqualTo(EncounterPhase.Completed));
            view.ShowCharms();
            yield return null;
            var waitingBadge = cards[1].GetComponentInChildren<SpiritCharmStatusBadge>();
            Assert.That(waitingBadge.Presentation.State, Is.EqualTo(SpiritCharmStatusState.Inactive));
            Assert.That(cards[2].GetComponentInChildren<SpiritCharmStatusBadge>().Presentation.State,
                Is.EqualTo(SpiritCharmStatusState.Used));
            CenterCard(scroll, (RectTransform)cards[1].transform);
            yield return null;
            Canvas.ForceUpdateCanvases();
            ClickAt(cards[1], waitingBadge.Icon.rectTransform);
            AssertDetail(view, controller, controller.Snapshot.Modifiers[1], definitions[1]);
            AssertDetailStatus(view, controller, controller.Snapshot.Modifiers[1], definitions[1], waitingBadge, "Encounter ended");
            AssertSymbol(waitingBadge.Icon, symbols, definitions[1].Id);
            yield return Capture("owned-status-inactive-540");
            var emptyEncounter = new EncounterDefinition("encounter.owned-charm-empty",
                new[] { new StageDefinition(1000000000, 3, 3) });
            var emptyCatalog = new MechanicalCatalog("owned-charm-empty.v1", controller.Catalog.Hands,
                new[] { emptyEncounter }, definitions);
            controller.Initialize(emptyCatalog, emptyEncounter.Id, 60, Path.Combine(Path.GetDirectoryName(savePath), "empty-charms.json"));
            view.ShowCharms();
            yield return null;
            Assert.That(Find<RectTransform>(view, "CharmDetailStatusRow").gameObject.activeSelf, Is.False);
            Assert.That(Find<Image>(view, "CharmDetailIcon").gameObject.activeSelf, Is.False);
            Assert.That(Find<SpiritModifierSigilGraphic>(view, "CharmDetailSigil").gameObject.activeSelf, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        private static ModifierDefinition Modifier(string id, ModifierPhase phase, IModifierCondition condition,
            ModifierActivationLimit limit = ModifierActivationLimit.OncePerHand)
            => new ModifierDefinition("modifier.owned-status." + id, "family.owned-status." + id, phase, condition,
                new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 7) }, limit);

        private static void AssertDetail(SpiritEncounterView view, SpiritEncounterController controller,
            ModifierInstanceSnapshot instance, ModifierDefinition definition)
        {
            Canvas.ForceUpdateCanvases();
            var title = Find<Text>(view, "CharmDetailTitle");
            var body = Find<Text>(view, "CharmDetailBody");
            Assert.That(title.text, Is.EqualTo(SpiritModifierPresentation.GetName(definition)));
            Assert.That(body.text, Is.EqualTo(SpiritModifierPresentation.GetConditionText(definition) + "\n" +
                "<color=#" + ColorUtility.ToHtmlStringRGB(SpiritModifierIcons.GetEffectColor(definition)) + ">" +
                SpiritModifierPresentation.GetEffectText(definition) + "</color>\n" +
                SpiritModifierPresentation.GetTimingText(definition) + "\n" +
                AvailabilityText(definition, controller.Snapshot)));
            AssertCompleteText(title, definition.Id + "/detail title");
            AssertCompleteText(body, definition.Id + "/detail body");
            AssertDetailGraphic(view, definition);
        }

        private static void AssertOwnedGraphic(Button card, ModifierDefinition definition, SpiritCharmStatusBadge badge)
        {
            AssertCharmGraphic(card, "CharmIcon", "CharmSigil", definition, 36,
                new[] { CardText(card, "Title").rectTransform, CardText(card, "Condition").rectTransform,
                    CardText(card, "Effects").rectTransform, (RectTransform)badge.transform });
        }

        private static void AssertDetailGraphic(SpiritEncounterView view, ModifierDefinition definition)
        {
            var title = Find<Text>(view, "CharmDetailTitle");
            AssertCharmGraphic(title.transform.parent, "CharmDetailIcon", "CharmDetailSigil", definition, 44,
                new[] { title.rectTransform, Find<Text>(view, "CharmDetailBody").rectTransform,
                    Find<RectTransform>(view, "CharmDetailStatusRow") });
        }

        private static void AssertCharmGraphic(Component root, string imageName, string sigilName,
            ModifierDefinition definition, float size, RectTransform[] sections)
        {
            var image = Find<Image>(root, imageName);
            var sigil = Find<SpiritModifierSigilGraphic>(root, sigilName);
            var sprite = SpiritModifierIcons.GetSprite(definition);
            Assert.That(image.gameObject.activeSelf, Is.EqualTo(sprite != null), definition.Id + "/" + imageName);
            Assert.That(sigil.gameObject.activeSelf, Is.EqualTo(sprite == null), definition.Id + "/" + sigilName);
            Assert.That(image.sprite, Is.SameAs(sprite), definition.Id + "/sprite");
            Assert.That(sigil.Seed, Is.EqualTo(SpiritModifierIcons.GetSigilSeed(definition)), definition.Id + "/sigil seed");
            Assert.That(image.raycastTarget, Is.False, definition.Id + " image must permit card inspection.");
            Assert.That(sigil.raycastTarget, Is.False, definition.Id + " sigil must permit card inspection.");
            Assert.That(image.preserveAspect, Is.True, definition.Id + "/sprite aspect");
            var graphic = sprite != null ? image.rectTransform : sigil.rectTransform;
            var panel = (RectTransform)graphic.parent;
            var bounds = BoundsIn(panel, graphic);
            Assert.That(graphic.rect.width, Is.EqualTo(size).Within(.01f), definition.Id + "/graphic width");
            Assert.That(graphic.rect.height, Is.EqualTo(size).Within(.01f), definition.Id + "/graphic height");
            AssertInside(panel.rect, bounds, definition.Id + "/graphic");
            foreach (var section in sections)
                Assert.That(bounds.Overlaps(BoundsIn(panel, section)), Is.False,
                    definition.Id + " graphic overlaps " + section.name + ".");
        }

        private static void AssertRewardGraphic(SpiritRewardCard reward, Button card, SpiritEncounterView view,
            ModifierDefinition definition)
        {
            reward.RenderModifier(definition, 1, 1);
            var rewardImage = Find<Image>(reward, "CharmIcon");
            var rewardSigil = Find<SpiritModifierSigilGraphic>(reward, "CharmSigil");
            var image = Find<Image>(card, "CharmIcon");
            var sigil = Find<SpiritModifierSigilGraphic>(card, "CharmSigil");
            var detailImage = Find<Image>(view, "CharmDetailIcon");
            var detailSigil = Find<SpiritModifierSigilGraphic>(view, "CharmDetailSigil");
            Assert.That(image.gameObject.activeSelf, Is.EqualTo(rewardImage.gameObject.activeSelf), definition.Id);
            Assert.That(sigil.gameObject.activeSelf, Is.EqualTo(rewardSigil.gameObject.activeSelf), definition.Id);
            Assert.That(image.sprite, Is.SameAs(rewardImage.sprite), definition.Id + "/reward sprite");
            Assert.That(detailImage.sprite, Is.SameAs(rewardImage.sprite), definition.Id + "/reward detail sprite");
            Assert.That(sigil.Seed, Is.EqualTo(rewardSigil.Seed), definition.Id + "/reward sigil seed");
            Assert.That(detailSigil.Seed, Is.EqualTo(rewardSigil.Seed), definition.Id + "/reward detail sigil seed");
            Assert.That(sigil.color, Is.EqualTo(rewardSigil.color), definition.Id + "/reward sigil color");
            Assert.That(detailSigil.color, Is.EqualTo(rewardSigil.color), definition.Id + "/reward detail sigil color");
        }

        private static void AssertSigilMesh(SpiritModifierSigilGraphic sigil, string context)
        {
            var mesh = sigil.canvasRenderer.GetMesh();
            Assert.That(mesh, Is.Not.Null, context + " sigil must provide a rendered mesh.");
            Assert.That(mesh.vertexCount, Is.GreaterThan(0), context + " sigil must draw visible geometry.");
            var bounds = sigil.rectTransform.rect;
            // Family marks extend beyond the central sigil's rectangle by less than a tenth of its size.
            float margin = Mathf.Min(bounds.width, bounds.height) * .1f;
            foreach (var vertex in mesh.vertices)
            {
                Assert.That(vertex.x, Is.InRange(bounds.xMin - margin, bounds.xMax + margin), context + "/sigil x");
                Assert.That(vertex.y, Is.InRange(bounds.yMin - margin, bounds.yMax + margin), context + "/sigil y");
            }
        }

        private static void AssertDetailStatus(SpiritEncounterView view, SpiritEncounterController controller,
            ModifierInstanceSnapshot instance, ModifierDefinition definition, SpiritCharmStatusBadge cardBadge,
            string expectedText = null)
        {
            var row = Find<RectTransform>(view, "CharmDetailStatusRow");
            var label = Find<Text>(view, "CharmDetailStatus");
            var badge = row.GetComponentInChildren<SpiritCharmStatusBadge>();
            var inspection = controller.ModifierInspections.SingleOrDefault(value => value.InstanceId == instance.InstanceId);
            var status = SpiritCharmStatusPresentation.Create(instance, definition, controller.Snapshot.Phase, inspection);
            Assert.That(row.gameObject.activeSelf, Is.True, definition.Id);
            Assert.That(badge, Is.Not.Null, definition.Id);
            Assert.That(label.text, Is.EqualTo(expectedText ?? status.GetDetailText(definition.Phase, controller.Snapshot.Phase)),
                definition.Id);
            Assert.That(badge.Presentation.State, Is.EqualTo(cardBadge.Presentation.State), definition.Id);
            Assert.That(badge.Icon.State, Is.EqualTo(cardBadge.Icon.State), definition.Id);
            Assert.That(badge.GetComponentsInChildren<Text>(true), Is.Empty, definition.Id + "/detail badge text");
            Assert.That(view.GetComponentsInChildren<Transform>(true).Any(value => value.name == "CharmDetailUseCount"),
                Is.False, definition.Id + "/detail use count");
            var panel = (RectTransform)row.parent;
            var bounds = BoundsIn(panel, row);
            var body = BoundsIn(panel, Find<Text>(view, "CharmDetailBody").rectTransform);
            AssertInside(panel.rect, bounds, definition.Id + "/detail status row");
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(body.yMin + .01f), definition.Id + " status row overlaps description.");
            var labelBounds = BoundsIn(row, label.rectTransform);
            var badgeBounds = BoundsIn(row, (RectTransform)badge.transform);
            AssertInside(row.rect, labelBounds, definition.Id + "/detail status label");
            AssertInside(row.rect, badgeBounds, definition.Id + "/detail status badge");
            Assert.That(badgeBounds.xMin, Is.GreaterThan(labelBounds.xMax), definition.Id + " status label overlaps badge.");
            var iconBounds = BoundsIn(row, badge.Icon.rectTransform);
            AssertInside(badgeBounds, iconBounds, definition.Id + "/detail status icon");
            if (!string.IsNullOrEmpty(label.text)) AssertCompleteText(label, definition.Id + "/detail status");
        }

        private static void AssertBadge(Button card, SpiritCharmStatusBadge badge, string context)
        {
            var panel = (RectTransform)card.transform;
            var bounds = BoundsIn(panel, (RectTransform)badge.transform);
            AssertInside(panel.rect, bounds, context + "/badge");
            Assert.That(bounds.center.x, Is.GreaterThan(panel.rect.center.x), context + " badge must occupy the right corner.");
            Assert.That(bounds.center.y, Is.LessThan(panel.rect.center.y), context + " badge must occupy the bottom corner.");
            var icon = BoundsIn(panel, badge.Icon.rectTransform);
            AssertInside(bounds, icon, context + "/icon");
            Assert.That(badge.GetComponentsInChildren<Text>(true), Is.Empty, context + "/badge text");
            Assert.That(badge.Icon.raycastTarget, Is.False, context + " icon must permit card inspection.");
            foreach (string name in new[] { "Title", "Condition", "Effects", "CharmIcon", "CharmSigil" })
            {
                var section = card.transform.Find(name) as RectTransform;
                if (section == null || !section.gameObject.activeSelf) continue;
                Assert.That(bounds.Overlaps(BoundsIn(panel, section)), Is.False, context + "/" + name + " overlaps badge.");
            }
        }

        private static Rect BoundsIn(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            var bottom = parent.InverseTransformPoint(corners[0]);
            var top = parent.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(bottom.x, bottom.y, top.x, top.y);
        }

        private static void AssertInside(Rect outer, Rect inner, string context)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - .01f), context);
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - .01f), context);
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + .01f), context);
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + .01f), context);
        }

        private static void AssertSymbol(SpiritCharmStatusGraphic icon,
            Dictionary<SpiritCharmStatusState, Vector3[]> symbols, string context)
        {
            var mesh = icon.canvasRenderer.GetMesh();
            Assert.That(mesh, Is.Not.Null, context + " state symbol must provide a rendered mesh.");
            Assert.That(mesh.vertexCount, Is.GreaterThan(0), context + " state symbol must draw visible geometry.");
            var bounds = icon.rectTransform.rect;
            foreach (var vertex in mesh.vertices)
            {
                Assert.That(vertex.x, Is.InRange(bounds.xMin, bounds.xMax), context + "/symbol x");
                Assert.That(vertex.y, Is.InRange(bounds.yMin, bounds.yMax), context + "/symbol y");
            }
            if (icon.State == SpiritCharmStatusState.Used)
            {
                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                float radius = Mathf.Min(bounds.width, bounds.height) * .3f;
                for (int sample = 0; sample <= 16; ++sample)
                {
                    float angle = sample * Mathf.PI * 2 / 16;
                    Vector3 point = bounds.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) *
                        (sample == 16 ? 0 : radius);
                    bool covered = false;
                    for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                    {
                        Vector3 a = vertices[triangles[triangle]];
                        Vector3 b = vertices[triangles[triangle + 1]];
                        Vector3 c = vertices[triangles[triangle + 2]];
                        if (Mathf.Abs(Vector3.Cross(b - a, c - a).z) < .0001f) continue;
                        float first = Vector3.Cross(b - a, point - a).z;
                        float second = Vector3.Cross(c - b, point - b).z;
                        float third = Vector3.Cross(a - c, point - c).z;
                        if ((first >= -.0001f && second >= -.0001f && third >= -.0001f) ||
                            (first <= .0001f && second <= .0001f && third <= .0001f))
                        {
                            covered = true;
                            break;
                        }
                    }
                    Assert.That(covered, Is.True, context + "/used circle interior " + sample);
                }
            }
            if (!symbols.ContainsKey(icon.State))
            {
                var vertices = mesh.vertices;
                foreach (var symbol in symbols)
                    Assert.That(vertices.SequenceEqual(symbol.Value), Is.False,
                        icon.State + " must have a distinct shape from " + symbol.Key + ".");
                symbols.Add(icon.State, vertices);
            }
        }

        private static void CenterCard(ScrollRect scroll, RectTransform card)
        {
            Vector2 center = scroll.viewport.InverseTransformPoint(card.TransformPoint(card.rect.center));
            scroll.content.anchoredPosition += new Vector2(0, scroll.viewport.rect.center.y - center.y);
            scroll.velocity = Vector2.zero;
        }

        private static void ClickAt(Button card, RectTransform target)
        {
            Assert.That(EventSystem.current, Is.Not.Null);
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center)),
                button = PointerEventData.InputButton.Left,
                pointerId = -1
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, card.name);
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.SameAs(card.gameObject), card.name + "/" + target.name + " must reach the card click handler.");
            pointer.pointerCurrentRaycast = hits[0];
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static IEnumerator Wait(SpiritEncounterController controller)
        {
            float deadline = Time.realtimeSinceStartup + 25;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
        }

        private static void AssertSections(RectTransform panel, Text[] labels, string context)
        {
            float previousBottom = float.PositiveInfinity;
            var corners = new Vector3[4];
            foreach (var label in labels)
            {
                label.rectTransform.GetWorldCorners(corners);
                var bottom = panel.InverseTransformPoint(corners[0]);
                var top = panel.InverseTransformPoint(corners[2]);
                Assert.That(top.y, Is.LessThanOrEqualTo(previousBottom + .01f), context + " sections overlap.");
                Assert.That(bottom.x, Is.GreaterThanOrEqualTo(panel.rect.xMin - .01f), context);
                Assert.That(bottom.y, Is.GreaterThanOrEqualTo(panel.rect.yMin - .01f), context);
                Assert.That(top.x, Is.LessThanOrEqualTo(panel.rect.xMax + .01f), context);
                Assert.That(top.y, Is.LessThanOrEqualTo(panel.rect.yMax + .01f), context);
                previousBottom = bottom.y;
            }
        }

        private static void AssertCompleteText(Text label, string context)
        {
            var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            var generated = label.cachedTextGenerator;
            Assert.That(generated.Populate(label.text, settings), Is.True, context);
            Assert.That(generated.vertexCount, Is.GreaterThanOrEqualTo(4), context);
            settings.fontSize = label.resizeTextForBestFit ? generated.fontSizeUsedForBestFit : label.fontSize;
            settings.resizeTextForBestFit = false;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            using (var complete = new TextGenerator())
            {
                Assert.That(complete.Populate(label.text, settings), Is.True, context);
                Assert.That(generated.vertexCount, Is.EqualTo(complete.vertexCount), context + " truncates glyphs or whole lines.");
            }
            var bounds = label.rectTransform.rect;
            // Decorative heading glyphs can overhang their advance within the card's padded margin.
            for (int index = 0; index < generated.verts.Count; ++index)
            {
                Vector2 position = generated.verts[index].position / label.pixelsPerUnit;
                Assert.That(position.x, Is.InRange(bounds.xMin - 3, bounds.xMax + 3), context);
                Assert.That(position.y, Is.InRange(bounds.yMin - 2, bounds.yMax + 2), context);
            }
        }

        private static Text CardText(Button card, string name) => card.transform.Find(name).GetComponent<Text>();

        private static string AvailabilityText(ModifierDefinition definition, EncounterSnapshot snapshot)
        {
            string scope = definition.Availability.Scope == ModifierAvailabilityScope.Global ? "All encounters" :
                definition.Availability.Scope == ModifierAvailabilityScope.Spirit ? "This spirit's encounters" : "This encounter";
            string access = definition.Availability.Access == ModifierAccess.PermanentUnlock ?
                (snapshot.UnlockedModifierIds.Contains(definition.Id) ? "Permanently unlocked" : "Requires permanent unlock") :
                definition.Availability.Access == ModifierAccess.EncounterExclusive ? "Encounter exclusive" : "Available from the start";
            return access + " / " + scope + " / Tier " + definition.Availability.PowerTier;
        }

        private static T Find<T>(Component root, string name) where T : Component
        {
            var result = root.GetComponentsInChildren<T>(true).FirstOrDefault(value => value.name == name);
            Assert.That(result, Is.Not.Null, name);
            return result;
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
