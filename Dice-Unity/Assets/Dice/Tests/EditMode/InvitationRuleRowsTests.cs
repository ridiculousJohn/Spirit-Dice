using System.Linq;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    /** The encounter rule rows a synthetic journey view shows on an invitation, driven by a synthetic journey. */
    public sealed class InvitationRuleRowsTests
    {
        private SyntheticUi ui;
        private SyntheticCharmAssets content;

        [SetUp]
        public void SetUp()
        {
            ui = new SyntheticUi();
            content = new SyntheticCharmAssets();
        }

        [TearDown]
        public void TearDown()
        {
            ui.Dispose();
            content.Dispose();
        }

        /**
         * Checks that the invitation beat shows one row per rule presentation of the journey's table, in table order,
         * with each rule's name and description, and that a beat other than the invitation shows no rule row. Fails if a
         * rule is missing, out of order or shows another rule's text, or if rule rows remain on a non-invitation beat.
         */
        [Test]
        public void InvitationShowsOneRowPerTableRuleAndOtherBeatsShowNone()
        {
            var table = Create<EncounterTablePresentationAsset>();
            SyntheticUi.Set(table, "rulePresentations", new[] { Rule("first", "First rule", "First rule text"),
                Rule("second", "Second rule", "Second rule text") });
            var journey = Create<EncounterJourneyAsset>();
            TestJourneys.SetEncounterBeats(journey, table, content.Encounter("encounter.rules"), new DialogueBeat());
            var controls = new TestJourneyControls
            {
                SelectedJourney = journey, JourneyPhase = EncounterJourneyPhase.Sequence, BeatIndex = 1
            };
            var view = JourneyView();
            view.Build(null);
            view.Bind(controls, null);

            view.Refresh();
            Assert.That(ShownRows(view), Is.EqualTo(new[] { "First rule / First rule text", "Second rule / Second rule text" }));

            controls.BeatIndex = 0;
            view.Refresh();
            Assert.That(ShownRows(view), Is.Empty, "Only the invitation shows the encounter rules.");
        }

        private static string[] ShownRows(EncounterJourneyView view)
            => view.GetComponentsInChildren<SpiritInvitationRuleView>(true).Where(row => row.gameObject.activeSelf)
                .Select(row => row.Title.text + " / " + row.Description.text).ToArray();

        /** A journey view holding only what showing a beat of a table scene and its invitation rule rows reads. */
        private EncounterJourneyView JourneyView()
        {
            var view = ui.Add<EncounterJourneyView>(ui.Root, "Journey");
            var artwork = ui.Rect(view.transform, "Artwork");
            SyntheticUi.Set(view, "artwork", artwork);
            SyntheticUi.Set(view, "background", ui.Add<RawImage>(artwork, "Background"));
            var foreground = ui.Add<RawImage>(ui.Rect(artwork, "Foreground Clip"), "Foreground");
            foreground.gameObject.AddComponent<AspectRatioFitter>();
            SyntheticUi.Set(view, "foreground", foreground);
            SyntheticUi.Set(view, "leftPanel", ui.Add<RawImage>(artwork, "Left Panel"));
            SyntheticUi.Set(view, "rightPanel", ui.Add<RawImage>(artwork, "Right Panel"));
            SyntheticUi.Set(view, "illustrationGroup", artwork.gameObject.AddComponent<CanvasGroup>());
            var narrative = ui.Rect(view.transform, "Narrative");
            SyntheticUi.Set(view, "narrative", narrative);
            SyntheticUi.Set(view, "narrativeGroup", narrative.gameObject.AddComponent<CanvasGroup>());
            var scroll = ui.Add<ScrollRect>(narrative, "Narrative Scroll");
            scroll.content = ui.Rect(scroll.transform, "Content");
            SyntheticUi.Set(view, "narrativeScroll", scroll);
            SyntheticUi.Set(view, "title", ui.Label(narrative, "Title"));
            SyntheticUi.Set(view, "body", ui.Label(narrative, "Body"));
            SyntheticUi.Set(view, "ruleHeading", ui.Label(narrative, "Rule Heading"));
            var invitation = ui.Rect(narrative, "Invitation");
            SyntheticUi.Set(view, "invitationSection", invitation.gameObject);
            SyntheticUi.Set(view, "invitationRulesRoot", ui.Rect(invitation, "Rules"));
            SyntheticUi.Set(view, "invitationRulePrefab", RuleRow());
            SyntheticUi.Set(view, "charmDetail", ui.Rect(narrative, "Charm Detail"));
            SyntheticUi.Set(view, "journeyStatus", ui.Label(view.transform, "Status"));
            SyntheticUi.Set(view, "advanceButton", ui.NewButton(view.transform, "Advance"));
            SyntheticUi.Set(view, "acceptButton", ui.NewButton(view.transform, "Accept"));
            SyntheticUi.Set(view, "skipButton", ui.NewButton(view.transform, "Skip"));
            SyntheticUi.Set(view, "returnButton", ui.NewButton(view.transform, "Return"));
            SyntheticUi.Set(view, "<EncounterCharms>k__BackingField", ui.CharmsScreen(view.transform));
            return view;
        }

        /** An invitation rule row template, inactive outside the view, with its texts and icon. */
        private SpiritInvitationRuleView RuleRow()
        {
            var row = ui.Add<SpiritInvitationRuleView>(ui.Root, "Rule Row Template");
            var iconRoot = ui.Rect(row.transform, "Icon Root");
            SyntheticUi.Set(row, "title", ui.Label(row.transform, "Title"));
            SyntheticUi.Set(row, "description", ui.Label(row.transform, "Description"));
            SyntheticUi.Set(row, "iconRoot", iconRoot);
            SyntheticUi.Set(row, "icon", ui.Add<Image>(iconRoot, "Icon"));
            row.gameObject.SetActive(false);
            return row;
        }

        private EncounterRulePresentationAsset Rule(string id, string name, string description)
        {
            var rule = Create<EncounterRulePresentationAsset>();
            SyntheticUi.Set(rule, "rule", content.Own(TestRules.Asset(TestRules.IdOf(id))));
            SyntheticUi.Set(rule, "displayName", name);
            SyntheticUi.Set(rule, "description", description);
            return rule;
        }

        private T Create<T>() where T : ScriptableObject => ui.Own(ScriptableObject.CreateInstance<T>());
    }
}
