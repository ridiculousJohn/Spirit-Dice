using System.Collections.Generic;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    /**
     * What the gameplay HUD shows when it is told what happened, on a synthetic HUD with every element it fills and the
     * fabricated wording, so each label is checked against the wording composed for it.
     */
    public sealed class SpiritGameplayHudTests
    {
        private SyntheticUi ui;
        private SyntheticCharms charms;
        private SpiritGameplayHud hud;
        // The synthetic HUD's elements by the HUD property each stands in for.
        private readonly Dictionary<string, Text> labels = new Dictionary<string, Text>();
        private readonly Dictionary<string, RectTransform> panels = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Image> highlightIcons = new Dictionary<string, Image>();

        private SpiritUiWording.Hud Wording => ui.Wording.hud;

        [SetUp]
        public void SetUp()
        {
            ui = new SyntheticUi();
            charms = new SyntheticCharms();
            hud = Hud();
        }

        [TearDown]
        public void TearDown()
        {
            ui.Dispose();
            charms.Dispose();
        }

        /** Checks a stage start shows its line, threshold and counters and prepares its intro hidden, which shows only when asked / fails if the stage start leaves the playing area, the resources line or the intro shown. */
        [Test]
        public void StageStartShowsTheStageAndPreparesItsIntroHidden()
        {
            panels["PlayingPanel"].gameObject.SetActive(true);
            hud.ShowStageStart(new StageStartedEvent(1, new StageDefinition(240, 4, 2)), 3);
            Assert.That(labels["StageLabel"].text, Is.EqualTo(Wording.Stage(2, 3)));
            Assert.That(labels["TargetLabel"].text, Is.EqualTo("240"));
            Assert.That(labels["TargetCaption"].text, Is.EqualTo(Wording.toClear));
            Assert.That(labels["HandsLabel"].text, Is.EqualTo(Wording.Hands(4)));
            Assert.That(labels["RerollsLabel"].text, Is.EqualTo(Wording.Rerolls(2)));
            Assert.That(panels["HandsPanel"].gameObject.activeSelf && panels["RerollsPanel"].gameObject.activeSelf, Is.True);
            Assert.That(labels["ResourcesLabel"].gameObject.activeSelf, Is.False);
            Assert.That(panels["PlayingPanel"].gameObject.activeSelf, Is.False, "The playing area waits for the stage's first roll.");
            Assert.That(labels["StageIntroTitle"].text, Is.EqualTo(Wording.Stage(2, 3)));
            Assert.That(labels["StageIntroDetail"].text, Is.EqualTo(Wording.StageIntro(240, 4, 2)));
            Assert.That(panels["StageIntroPanel"].gameObject.activeSelf, Is.False);
            hud.ShowStageIntro();
            Assert.That(panels["StageIntroPanel"].gameObject.activeSelf, Is.True);
            hud.HideStageIntro();
            Assert.That(panels["StageIntroPanel"].gameObject.activeSelf, Is.False);
        }

        /** Checks a hand start clears the score counters, rank, total and step lines and shows the new hand line muted / fails if a previous hand's score or rule selection remains. */
        [Test]
        public void HandStartClearsTheScoreAndShowsTheNewHandLine()
        {
            hud.ShowRuleSelection("rule");
            hud.ShowHandTotal(90);
            hud.ShowHandStart(new HandStartedEvent(new EncounterState { HandsRemaining = 2, RerollsRemaining = 1 }), null, null);
            Assert.That(labels["HandsLabel"].text, Is.EqualTo(Wording.Hands(2)));
            Assert.That(labels["RerollsLabel"].text, Is.EqualTo(Wording.Rerolls(1)));
            Assert.That(labels["BaseScoreLabel"].text, Is.EqualTo("0"));
            Assert.That(labels["MultiplierLabel"].text, Is.EqualTo("0"));
            Assert.That(labels["HandScoreLabel"].text, Is.EqualTo("0"));
            Assert.That(labels["CategoryLabel"].text, Is.Empty);
            Assert.That(labels["ScoreDetail"].text, Is.Empty);
            Assert.That(labels["SelectionLabel"].text, Is.EqualTo(Wording.newHand));
            Assert.That(labels["SelectionLabel"].color, Is.EqualTo(SpiritUi.GetTheme(hud.transform).muted));
            Assert.That(hud.DisplayedBaseScore == 0 && hud.DisplayedMultiplier == 0, Is.True);
        }

        /** Checks a rule presenter's selection line shows marked as the rule's / fails if its text or mark is missing. */
        [Test]
        public void RuleSelectionShowsItsTextMarkedAsTheRules()
        {
            hud.ShowRuleSelection("rule selection");
            Assert.That(labels["SelectionLabel"].text, Is.EqualTo("rule selection"));
            Assert.That(labels["SelectionLabel"].color, Is.EqualTo(SpiritUi.GetTheme(hud.transform).amber));
        }

        /** Checks the target counts to a changed threshold and shows zero with the cleared caption when the stage clears / fails if the counter or its caption is not updated. */
        [Test]
        public void TargetFollowsThresholdChangesAndTheStageClear()
        {
            labels["TargetCaption"].text = "";
            var counting = hud.ShowThresholdChange(300, 120, 0);
            Assert.That(labels["TargetCaption"].text, Is.EqualTo(Wording.toClear), "The caption shows as the count starts.");
            while (counting.MoveNext()) { }
            Assert.That(labels["TargetLabel"].text, Is.EqualTo("120"));
            hud.ShowStageCleared();
            Assert.That(labels["TargetLabel"].text, Is.EqualTo("0"));
            Assert.That(labels["TargetCaption"].text, Is.EqualTo(Wording.stageCleared));
        }

        /** Checks a reroll change shows its count and the rerolls remaining line while a hand change shows only its count / fails if a hand change replaces the selection line or a count is not shown. */
        [Test]
        public void ResourceChangesShowTheirCounts()
        {
            var state = new EncounterState();
            hud.ShowRuleSelection("before");
            hud.ShowResourceChanged(new ResourceChangedEvent(state, ResourceKind.Hands, ResourceChangeCause.PlayHand, 3, 2));
            Assert.That(labels["HandsLabel"].text, Is.EqualTo(Wording.Hands(2)));
            Assert.That(labels["SelectionLabel"].text, Is.EqualTo("before"));
            hud.ShowResourceChanged(new ResourceChangedEvent(state, ResourceKind.Rerolls, ResourceChangeCause.RerollDice, 2, 1));
            Assert.That(labels["RerollsLabel"].text, Is.EqualTo(Wording.Rerolls(1)));
            Assert.That(labels["SelectionLabel"].text, Is.EqualTo(Wording.RerollsRemaining(1)));
        }

        /** Checks a screen without a run hides the playing area and shows the run status / fails if the playing area remains or the status is not described. */
        [Test]
        public void NoEncounterHidesThePlayingAreaAndShowsTheStatus()
        {
            panels["PlayingPanel"].gameObject.SetActive(true);
            hud.ShowNoEncounter(null, SpiritStatus.None);
            Assert.That(panels["PlayingPanel"].gameObject.activeSelf, Is.False);
            Assert.That(labels["StatusLabel"].text, Is.EqualTo(SpiritStatusText.Describe(SpiritStatus.None, ui.Wording.status)));
        }

        /**
         * Checks a playing snapshot shows its stage, threshold, counters and an idle hand at its charm scores with reroll
         * allowed only for a selection, and a completed one shows its total score in place of the threshold and counters /
         * fails if a phase shows the other phase's target, counters or actions.
         */
        [Test]
        public void SnapshotShowsThePlayingAndCompletedPhases()
        {
            var encounter = new EncounterDefinition("encounter.test.hud", new[] { new StageDefinition(1000000, 1, 2) });
            var catalog = new MechanicalCatalog("test.hud", TestContent.Hands(), new[] { encounter });
            var session = EncounterSession.CreateEncounter(catalog, encounter.Id, 3);
            var playing = session.Snapshot;
            hud.ShowSnapshot(playing, null, SpiritStatus.None, null, 0);
            Assert.That(panels["PlayingPanel"].gameObject.activeSelf, Is.True);
            Assert.That(labels["StageLabel"].text, Is.EqualTo(Wording.Stage(1, 1)));
            Assert.That(labels["TargetLabel"].text, Is.EqualTo(DiagnosticText.Number(1000000)));
            Assert.That(labels["TargetCaption"].text, Is.EqualTo(Wording.toClear));
            Assert.That(panels["HandsPanel"].gameObject.activeSelf && !labels["ResourcesLabel"].gameObject.activeSelf, Is.True);
            Assert.That(labels["CharmCount"].text, Is.EqualTo(Wording.CharmCount(0)));
            Assert.That(labels["CharmsButtonLabel"].text, Is.EqualTo(Wording.CharmsButton(0)));
            Assert.That(labels["StatusLabel"].text, Is.EqualTo(SpiritStatusText.Gameplay(SpiritStatus.None, ui.Wording.status)));
            Assert.That(labels["BaseScoreLabel"].text, Is.EqualTo(DiagnosticText.Number(playing.ModifierBaseScore)));
            Assert.That(labels["HandScoreLabel"].text, Is.EqualTo("0"));
            Assert.That(labels["SelectionLabel"].text, Is.Empty);
            Assert.That(buttons["PlayButton"].interactable, Is.True);
            Assert.That(buttons["RerollButton"].interactable, Is.False, "Reroll waits for a selection.");
            hud.ShowSnapshot(playing, null, SpiritStatus.None, null, 0b00001);
            Assert.That(buttons["RerollButton"].interactable, Is.True);

            var played = session.Execute(PlayHandAction.Instance, playing.Revision);
            Assert.That(played.Snapshot.Phase, Is.EqualTo(EncounterPhase.Completed), "The stage's only hand cannot clear it.");
            hud.ShowSnapshot(played.Snapshot, null, SpiritStatus.None, null, 0);
            Assert.That(panels["PlayingPanel"].gameObject.activeSelf, Is.False);
            Assert.That(labels["TargetLabel"].text, Is.EqualTo(DiagnosticText.Number(played.Snapshot.TotalScore)));
            Assert.That(labels["TargetCaption"].text, Is.EqualTo(Wording.totalScore));
            Assert.That(panels["HandsPanel"].gameObject.activeSelf, Is.False);
            Assert.That(labels["ResourcesLabel"].gameObject.activeSelf, Is.True);
            Assert.That(labels["ResourcesLabel"].text, Is.EqualTo(Wording.TotalScoreLine(played.Snapshot.TotalScore)));
        }

        /**
         * Checks a scoring step shows its lines, highlights and grants, and its animation leaves the counters at the step's
         * values with the highlights reset / fails if a line, highlight or grant is missing or a highlight outlasts the step.
         */
        [Test]
        public void ScoringStepShowsItsLinesAndCountsToItsValues()
        {
            var charm = new ModifierDefinition("test.hud.grant", TestFamilies.Of("test.hud.grant"), ModifierPhase.PlayerReroll,
                new AlwaysCondition(), new ModifierEffect[] { new AddRerollsEffect(1) });
            var state = new EncounterState();
            var step = new SpiritScoringStep
            {
                BaseScore = 12, Multiplier = 3, CategoryText = "category", SelectionText = "selection", SelectionColor = Color.red,
                StepLabel = "step", Charm = charm, HighlightsRerolls = true,
                Grants = new[] { new ResourceChangedEvent(state, ResourceKind.Rerolls, ResourceChangeCause.ModifierEffect, 1, 2) }
            };
            // A reset hides every counter highlight, as between the authored HUD's steps.
            hud.ResetTransientVisuals();
            hud.ShowScoringStep(step, charms.For(charm));
            Assert.That(labels["CategoryLabel"].text, Is.EqualTo("category"));
            Assert.That(labels["SelectionLabel"].text, Is.EqualTo("selection"));
            Assert.That(labels["SelectionLabel"].color, Is.EqualTo(Color.red));
            Assert.That(labels["ScoreDetail"].text, Is.EqualTo("step"));
            Assert.That(labels["RerollsLabel"].text, Is.EqualTo(Wording.Rerolls(2)));
            Assert.That(highlightIcons["RerollsHighlight"].gameObject.activeSelf, Is.True);
            Assert.That(highlightIcons["BaseScoreHighlight"].gameObject.activeSelf, Is.False);
            var animation = hud.AnimateScoringStep(step, 0);
            while (animation.MoveNext()) { }
            Assert.That(labels["BaseScoreLabel"].text, Is.EqualTo("12"));
            Assert.That(labels["MultiplierLabel"].text, Is.EqualTo("3"));
            Assert.That(hud.DisplayedBaseScore == 12 && hud.DisplayedMultiplier == 3, Is.True);
            Assert.That(highlightIcons["RerollsHighlight"].gameObject.activeSelf, Is.False);
        }

        /** A HUD with every element its methods fill. */
        private SpiritGameplayHud Hud()
        {
            var created = ui.Add<SpiritGameplayHud>(ui.Root, "Gameplay HUD");
            Transform parent = created.transform;
            foreach (string panel in new[] { "PlayingPanel", "TargetPanel", "HandsPanel", "RerollsPanel", "StageIntroPanel" })
                SyntheticUi.Set(created, "<" + panel + ">k__BackingField", panels[panel] = ui.Rect(parent, panel));
            foreach (string label in new[] { "EncounterTitle", "StageLabel", "TargetLabel", "TargetCaption", "HandsLabel",
                "RerollsLabel", "ResourcesLabel", "SelectionLabel", "CategoryLabel", "ScoreDetail", "BaseScoreLabel",
                "MultiplierLabel", "HandScoreLabel", "StageIntroTitle", "StageIntroDetail", "CharmCount", "StatusLabel" })
                SyntheticUi.Set(created, "<" + label + ">k__BackingField", labels[label] = ui.Add<Text>(parent, label));
            SyntheticUi.Set(created, "<ModifierEmblem>k__BackingField", ui.Add<Image>(parent, "Emblem"));
            SyntheticUi.Set(created, "<ModifierActivationIcon>k__BackingField", ui.Add<Image>(parent, "Activation Icon"));
            SyntheticUi.Set(created, "modifierActivationCategory", ui.CategoryBadge(parent));
            foreach (string highlight in new[] { "BaseScoreHighlight", "MultiplierHighlight", "RerollsHighlight", "HandsHighlight" })
            {
                string path = "<" + highlight + ">k__BackingField.";
                SyntheticUi.Set(created, path + "Label", ui.Add<Text>(parent, highlight + " Label"));
                SyntheticUi.Set(created, path + "Caption", ui.Add<Text>(parent, highlight + " Caption"));
                SyntheticUi.Set(created, path + "Icon", highlightIcons[highlight] = ui.Add<Image>(parent, highlight + " Icon"));
            }
            foreach (string action in new[] { "reroll", "play" })
            {
                var badge = ui.Add<CanvasGroup>(parent, action + " rule badge");
                SyntheticUi.Set(created, action + "EncounterRuleGroup", badge);
                SyntheticUi.Set(created, action + "EncounterRuleLabel", ui.Add<Text>(badge.transform, "Label"));
                SyntheticUi.Set(created, action + "EncounterRuleIcon", ui.Add<Image>(badge.transform, "Icon"));
                string name = action == "reroll" ? "RerollButton" : "PlayButton";
                var button = ui.Add<SpiritUiButtonVisual>(parent, name);
                button.targetGraphic = button.gameObject.AddComponent<Image>();
                SyntheticUi.Set(created, "<" + name + ">k__BackingField", buttons[name] = button);
            }
            var charmsButton = ui.NewButton(parent, "CharmsButton");
            labels["CharmsButtonLabel"] = ui.Add<Text>(charmsButton.transform, "Label");
            SyntheticUi.Set(created, "<CharmsButton>k__BackingField", charmsButton);
            created.BuildPlayingHud(() => { }, () => { });
            return created;
        }
    }
}
