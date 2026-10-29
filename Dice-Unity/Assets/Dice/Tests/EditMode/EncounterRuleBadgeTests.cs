using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    /**
     * The gameplay HUD's encounter rule badge, the table card it shows and the rule wording, driven by synthetic rule
     * encounters on a synthetic HUD holding only the two rule badges and the action buttons they sit beside.
     */
    public sealed class EncounterRuleBadgeTests
    {
        private static readonly EncounterRuleDefinition.Id FreeId = TestRules.IdOf("first_reroll_free");
        private static readonly EncounterDefinition.Id EncounterId = TestEncounters.IdOf("encounter.rule-badge");
        private readonly List<Object> created = new List<Object>();
        private SyntheticUi ui;
        private SpiritGameplayHud hud;
        // The synthetic badges, labels and icons, by the action whose button each sits beside.
        private readonly Dictionary<string, CanvasGroup> badges = new Dictionary<string, CanvasGroup>();
        private readonly Dictionary<string, Text> badgeLabels = new Dictionary<string, Text>();
        private readonly Dictionary<string, Image> badgeIcons = new Dictionary<string, Image>();

        [SetUp]
        public void SetUp()
        {
            ui = new SyntheticUi();
            hud = ui.Add<SpiritGameplayHud>(ui.Root, "Gameplay HUD");
            foreach (string action in new[] { "reroll", "play" })
            {
                var badge = badges[action] = ui.Add<CanvasGroup>(hud.transform, action + " rule badge");
                SyntheticUi.Set(hud, action + "EncounterRuleGroup", badge);
                SyntheticUi.Set(hud, action + "EncounterRuleLabel", badgeLabels[action] = ui.Add<Text>(badge.transform, "Label"));
                SyntheticUi.Set(hud, action + "EncounterRuleIcon", badgeIcons[action] = ui.Add<Image>(badge.transform, "Icon"));
            }
            SyntheticUi.Set(hud, "<RerollButton>k__BackingField", ActionButton("Reroll"));
            SyntheticUi.Set(hud, "<PlayButton>k__BackingField", ActionButton("Play"));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created)
                if (item != null) Object.DestroyImmediate(item);
            created.Clear();
            ui.Dispose();
        }

        private SpiritUiButtonVisual ActionButton(string name)
        {
            var button = ui.Add<SpiritUiButtonVisual>(hud.transform, name);
            button.targetGraphic = button.gameObject.AddComponent<Image>();
            return button;
        }

        /**
         * Checks that the badge shows a rule's available wording at full opacity while the rule is available and its used
         * wording dimmed once the rule is spent, and that it hides a rule the encounter does not have. Fails if the badge
         * ignores the rule's state or shows a rule absent from the encounter.
         */
        [Test]
        public void BadgeShowsTheRuleStateAndHidesARuleTheEncounterLacks()
        {
            var session = Session(new FirstRerollFreeRuleDefinition(FreeId));
            var rule = Presentation(FreeId, ModifierPhase.PlayerReroll, "favor ready", "favor spent");
            hud.RenderEncounterRule(session.Snapshot, rule);
            Assert.That(badges["reroll"].gameObject.activeSelf, Is.True);
            Assert.That(badgeLabels["reroll"].text, Is.EqualTo("favor ready"));
            Assert.That(badges["reroll"].alpha, Is.EqualTo(1f));

            var spent = session.Execute(new RerollDiceAction(1), session.Snapshot.Revision);
            Assert.That(spent.Accepted, Is.True);
            Assert.That(spent.Snapshot.EncounterRules.Single().IsAvailable, Is.False);
            hud.RenderEncounterRule(spent.Snapshot, rule);
            Assert.That(badges["reroll"].gameObject.activeSelf, Is.True);
            Assert.That(badgeLabels["reroll"].text, Is.EqualTo("favor spent"));
            Assert.That(badges["reroll"].alpha, Is.LessThan(1f), "A spent rule is dimmed.");

            hud.RenderEncounterRule(session.Snapshot,
                Presentation(TestRules.IdOf("absent"), ModifierPhase.PlayerReroll, "absent", "absent"));
            Assert.That(badges["reroll"].gameObject.activeSelf || badges["play"].gameObject.activeSelf, Is.False,
                "A rule the encounter lacks is not shown.");
        }

        /**
         * Checks that a rule acting on Play Hand is shown in a different badge from a rule acting on a reroll, with only
         * the shown badge active. Fails if both phases share one badge or the other phase's badge stays visible.
         */
        [Test]
        public void BadgeFollowsTheActionOfTheRulesActivationPhase()
        {
            var session = Session(new FirstRerollFreeRuleDefinition(FreeId));
            hud.RenderEncounterRule(session.Snapshot,
                Presentation(FreeId, ModifierPhase.PlayerReroll, "reroll", "reroll"));
            Assert.That(badges["reroll"].gameObject.activeSelf, Is.True);
            Assert.That(badges["play"].gameObject.activeSelf, Is.False);
            Assert.That(badgeLabels["reroll"].text, Is.EqualTo("reroll"));
            hud.RenderEncounterRule(session.Snapshot,
                Presentation(FreeId, ModifierPhase.PlayHand, "play", "play"));
            Assert.That(badges["play"].gameObject.activeSelf, Is.True);
            Assert.That(badges["reroll"].gameObject.activeSelf, Is.False);
            Assert.That(badgeLabels["play"].text, Is.EqualTo("play"));
        }

        /**
         * Checks that the badge shows a rule's authored icon and disables the icon for a rule without one. Fails if an
         * authored icon is missing or an empty icon is shown.
         */
        [Test]
        public void BadgeShowsTheRuleIconOnlyWhenOneIsAuthored()
        {
            var session = Session(new FirstRerollFreeRuleDefinition(FreeId));
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f));
            created.Add(sprite);
            hud.RenderEncounterRule(session.Snapshot,
                Presentation(FreeId, ModifierPhase.PlayerReroll, "icon", "icon", sprite));
            Assert.That(badgeIcons["reroll"].sprite, Is.SameAs(sprite));
            Assert.That(badgeIcons["reroll"].enabled, Is.True);

            hud.RenderEncounterRule(session.Snapshot,
                Presentation(FreeId, ModifierPhase.PlayerReroll, "none", "none"));
            Assert.That(badgeIcons["reroll"].sprite == null, Is.True);
            Assert.That(badgeIcons["reroll"].enabled, Is.False);
        }

        /**
         * Checks that a table's HUD card is the card flagged as shown on the HUD wherever it sits among the table's cards,
         * and that a table flagging none has no HUD card. Fails if an unflagged card is shown or the flagged card is not.
         */
        [Test]
        public void TheTableShowsItsFlaggedCardOnTheHud()
        {
            var unflagged = Presentation(TestRules.IdOf("unflagged"), ModifierPhase.PlayerReroll, "unflagged", "unflagged",
                shownOnHud: false);
            var flagged = Presentation(TestRules.IdOf("flagged"), ModifierPhase.PlayHand, "flagged", "flagged", shownOnHud: true);
            Assert.That(Table("table.flagged", unflagged, flagged).HudRuleCard, Is.SameAs(flagged));
            Assert.That(Table("table.unflagged", unflagged).HudRuleCard, Is.Null);
        }

        /**
         * Checks that a table flagging more than one card as shown on the HUD fails with a message naming the table. Fails if
         * such a table yields a card or its failure does not name it.
         */
        [Test]
        public void ATableFlaggingMoreThanOneHudCardFails()
        {
            var table = Table("table.two-flagged",
                Presentation(TestRules.IdOf("first"), ModifierPhase.PlayerReroll, "first", "first", shownOnHud: true),
                Presentation(TestRules.IdOf("second"), ModifierPhase.PlayHand, "second", "second", shownOnHud: true));
            var failure = Assert.Throws<InvalidOperationException>(() => _ = table.HudRuleCard);
            Assert.That(failure.Message, Does.Contain("table.two-flagged"));
        }

        /**
         * Checks that rule wording fills each placeholder the rule's snapshot supplies, for its availability, and keeps any
         * other placeholder as authored. Fails if a supplied value is not filled or an unsupplied placeholder is replaced.
         */
        [Test]
        public void RuleWordingFillsTheValuesTheRuleSnapshotSupplies()
        {
            var favored = TestRules.IdOf("favored");
            var presentation = Presentation(favored, ModifierPhase.PlayHand, "Favored {face} of {count}", "Spent {face}");
            Assert.That(presentation.TextFor(new FavoredFaceRuleSnapshot(favored, true, 4)), Is.EqualTo("Favored 4 of {count}"));
            Assert.That(presentation.TextFor(new FavoredFaceRuleSnapshot(favored, false, 6)), Is.EqualTo("Spent 6"));
            Assert.That(presentation.TextFor(new EncounterRuleSnapshot(favored, true)), Is.EqualTo("Favored {face} of {count}"));
        }

        /** A one-stage test encounter with the given rule. */
        private static EncounterSession Session(EncounterRuleDefinition rule)
        {
            var encounter = new EncounterDefinition(EncounterId, new[] { new StageDefinition(1000000, 3, 3) },
                rules: new[] { rule });
            return EncounterSession.CreateEncounter(
                new MechanicalCatalog("rule-badge.v1", TestContent.Hands(), new[] { encounter }), EncounterId, 1);
        }

        private EncounterRulePresentationAsset Presentation(EncounterRuleDefinition.Id ruleId, ModifierPhase phase, string available,
            string used, Sprite icon = null, bool shownOnHud = false)
        {
            var rule = TestRules.Asset(ruleId);
            created.Add(rule);
            var asset = ScriptableObject.CreateInstance<EncounterRulePresentationAsset>();
            created.Add(asset);
            asset.name = "Card " + ruleId;
            using var serialized = new SerializedObject(asset);
            serialized.FindProperty("rule").objectReferenceValue = rule;
            serialized.FindProperty("displayName").stringValue = asset.name;
            serialized.FindProperty("availableText").stringValue = available;
            serialized.FindProperty("usedText").stringValue = used;
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("activationPhase").intValue = (int)phase;
            serialized.FindProperty("shownOnHud").boolValue = shownOnHud;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private EncounterTablePresentationAsset Table(string name, params EncounterRulePresentationAsset[] cards)
        {
            var table = ScriptableObject.CreateInstance<EncounterTablePresentationAsset>();
            created.Add(table);
            table.name = name;
            using var serialized = new SerializedObject(table);
            var list = serialized.FindProperty("rulePresentations");
            list.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }
    }
}
