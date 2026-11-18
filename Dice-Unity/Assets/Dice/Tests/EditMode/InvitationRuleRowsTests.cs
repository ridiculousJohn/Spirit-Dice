using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
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
            view.Bind(controls, EncounterControls(TestEncounters.IdOf("encounter.rules")));

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
            SyntheticUi.Set(view, "invitationRulePrefab", ui.RuleRow());
            SyntheticUi.Set(view, "charmDetail", ui.Rect(narrative, "Charm Detail"));
            SyntheticUi.Set(view, "itemDetails", ui.ItemDetailList(narrative));
            SyntheticUi.Set(view, "journeyStatus", ui.Label(view.transform, "Status"));
            SyntheticUi.Set(view, "advanceButton", ui.NewButton(view.transform, "Advance"));
            SyntheticUi.Set(view, "acceptButton", ui.NewButton(view.transform, "Accept"));
            SyntheticUi.Set(view, "skipButton", ui.NewButton(view.transform, "Skip"));
            SyntheticUi.Set(view, "returnButton", ui.NewButton(view.transform, "Return"));
            SyntheticUi.Set(view, "<EncounterCharms>k__BackingField", ui.CharmsScreen(view.transform));
            return view;
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

        /** Controls whose catalog holds a one-stage encounter of the id with no rules or items, and an empty item skin's presentation. */
        private ISpiritEncounterControls EncounterControls(EncounterDefinition.Id encounterId)
        {
            var encounter = new EncounterDefinition(encounterId, new[] { new StageDefinition(100, 3, 0) });
            var catalog = new MechanicalCatalog("invitation-rules.v1", TestContent.Hands(), new[] { encounter });
            var content = new MechanicalContent(catalog, Array.Empty<KeyValuePair<ModifierAsset, ModifierDefinition>>());
            return new Controls(catalog, new ItemPresentationCatalog(Create<ItemSkinAsset>(), content));
        }

        private sealed class Controls : ISpiritEncounterControls
        {
            public Controls(MechanicalCatalog catalog, ItemPresentationCatalog items)
            {
                Catalog = catalog;
                ItemPresentation = items;
            }

            public EncounterSnapshot Snapshot => null;
            public MechanicalCatalog Catalog { get; }
            public EncounterInPlay InPlay => null;
            public IReadOnlyList<EncounterRuleDefinition> InstalledRules => null;
            public IReadOnlyList<TablePieceDefinition> InstalledPieces => null;
            public ModifierPresentationCatalog ModifierPresentation => null;
            public ItemPresentationCatalog ItemPresentation { get; }
            public HandPreview Preview => null;
            public IReadOnlyList<ModifierInspection> ModifierInspections => Array.Empty<ModifierInspection>();
            public int SelectedMask => 0;
            public bool IsBusy => false;
            public SpiritStatus Status => SpiritStatus.None;
            public event Action StateChanged { add { } remove { } }
            public event Action<Dice.Engine.Resolution> Resolved { add { } remove { } }
            public void ToggleDie(int index) { }
            public void Reroll() { }
            public void PlayHand() { }
            public void InteractWithPiece(TablePieceDefinition.Id pieceId, int interactionIndex, PieceInteractionInput input) { }
            public void RefreshRewards() { }
            public void ChooseReward(long offerId, long optionId) { }
            public void StartNew() { }
            public void Save() { }
            public void Load() { }
            public void Abandon() { }
            public void CompletePresentation() { }
        }
    }
}
