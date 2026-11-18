using System;
using System.Collections.Generic;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    /**
     * How the items a reward grants are shown before it is given: the rewards continuing from a beat gives (every beat's
     * through it, and the completion reward from the last beat), the item detail that shows one item, and the journey
     * screen, the ending and the board panel, which each show the items of what they give that the player lacks.
     */
    public sealed class ItemRewardDetailTests
    {
        private static readonly ItemDefinition.Id ItemId = new ItemDefinition.Id(new Guid("5e6f7a8b9c0d41e2a3b4c5d6e7f80912"));
        private static readonly EncounterRuleDefinition.Id RuleId = TestRules.IdOf("item_detail.rule");
        private static readonly TablePieceDefinition.Id PieceId = TestPieces.IdOf("piece.item_detail");
        private const string WonEncounter = "encounter.item_detail.won";
        private const string LostEncounter = "encounter.item_detail.lost";
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
         * Checks continuing from a beat gives the rewards of every beat through it, the last beat adding the completion
         * reward, and an Encounter beat that is last giving its own reward and the completion reward / fails if a beat drops
         * an earlier beat's reward or the last beat misses the completion reward.
         */
        [Test]
        public void TheRewardsThroughABeatAreEveryBeatsSoFarAndTheLastAddsTheCompletionReward()
        {
            var first = Reward();
            var last = Reward();
            var completion = Reward();
            var narrated = Create<EncounterJourneyAsset>();
            TestJourneys.SetBeats(narrated, null, new DialogueBeat(), new DialogueBeat(), new DialogueBeat());
            TestJourneys.SetBeat(narrated, 0, "progressionReward", first);
            TestJourneys.SetBeat(narrated, 2, "progressionReward", last);
            SyntheticUi.Set(narrated, "completionReward", completion);
            Assert.That(narrated.RewardsThrough(0), Is.EqualTo(new[] { first }));
            Assert.That(narrated.RewardsThrough(1), Is.EqualTo(new[] { first }), "A beat without a reward still gives the earlier beats'.");
            Assert.That(narrated.RewardsThrough(2), Is.EqualTo(new[] { first, last, completion }), "The last beat adds the completion reward.");

            var encounter = content.Encounter(WonEncounter);
            var encounterReward = Reward();
            var played = Create<EncounterJourneyAsset>();
            TestJourneys.SetEncounterBeats(played, null, encounter, new DialogueBeat());
            TestJourneys.SetBeat(played, 0, "progressionReward", first);
            TestJourneys.SetEncounterBeat(played, encounter, "progressionReward", encounterReward);
            SyntheticUi.Set(played, "completionReward", completion);
            Assert.That(played.RewardsThrough(played.IndexOfEncounter(encounter.Identity)), Is.EqualTo(new[] { first, encounterReward, completion }),
                "Continuing from a won Encounter beat that is last gives every beat's reward and the completion reward.");
        }

        /**
         * Checks the item detail shows the item presentation's name, one row per rule card with the card's name and
         * description, and one family row per family of the charms the item's rules add / fails if any of them is missing
         * or shows another's text.
         */
        [Test]
        public void TheItemDetailShowsTheNameRuleCardsAndCharms()
        {
            var setup = Setup();
            var list = ui.ItemDetailList(ui.Root);
            list.Show(new[] { ItemId }, () => setup.Sources);
            var detail = list.Details.Single();
            Assert.That(detail.ItemName.text, Is.EqualTo("Test Lantern"));
            Assert.That(detail.GetComponentsInChildren<SpiritInvitationRuleView>(false).Select(row => row.Title.text + " / " + row.Description.text),
                Is.EqualTo(new[] { "Carry / Carries the die" }));
            var families = detail.GetComponentsInChildren<SpiritModifierFamilyRow>(false);
            Assert.That(families.Select(row => row.Family), Is.EqualTo(new[] { setup.Charm.Family }));
        }

        /**
         * Checks the journey screen shows the items continuing from the shown beat gives that the player lacks, an earlier
         * beat's item included, and none once the player owns them / fails if a lacked item is missing or an owned one shows.
         */
        [Test]
        public void TheJourneyScreenShowsTheItemsTheShownBeatGivesThatThePlayerLacks()
        {
            var setup = Setup();
            var journey = Create<EncounterJourneyAsset>();
            var table = Create<EncounterTablePresentationAsset>();
            TestJourneys.SetEncounterBeats(journey, table, content.Encounter(WonEncounter), new DialogueBeat(), new DialogueBeat());
            TestJourneys.SetBeat(journey, 0, "progressionReward", Reward(setup.Item));
            var controls = new TestJourneyControls
            {
                SelectedJourney = journey, JourneyPhase = EncounterJourneyPhase.Sequence, RewardCatalog = setup.Catalog
            };
            var view = JourneyView(out SpiritItemDetailList items);
            view.Build(null);
            view.Bind(controls, new Controls(setup));

            view.Refresh();
            Assert.That(ShownNames(items), Is.EqualTo(new[] { "Test Lantern" }));

            controls.BeatIndex = 1;
            view.Refresh();
            Assert.That(ShownNames(items), Is.EqualTo(new[] { "Test Lantern" }), "A later beat shows an earlier beat's item the player lacks.");

            // Giving happens on continuing, which shows another beat; the beat shown next asks again.
            controls.GiveRewards(journey.RewardsThrough(1));
            controls.BeatIndex = 0;
            view.Refresh();
            Assert.That(items.gameObject.activeSelf, Is.False, "An item the player owns is not shown.");
        }

        /**
         * Checks a win's ending shows the items continuing from its Encounter beat gives that the player lacks, none once the
         * player owns them, and none after a loss / fails if a lacked item is missing, an owned one shows, or a loss shows one.
         */
        [Test]
        public void TheEndingOfAWinShowsTheItemsItsContinueGivesThatThePlayerLacks()
        {
            var setup = Setup();
            var journey = Create<EncounterJourneyAsset>();
            TestJourneys.SetEncounterBeats(journey, Create<EncounterTablePresentationAsset>(), content.Encounter(WonEncounter),
                new DialogueBeat());
            TestJourneys.SetBeat(journey, 0, "progressionReward", Reward(setup.Item));
            var controls = new TestJourneyControls { SelectedJourney = journey, RewardCatalog = setup.Catalog };
            var ending = Ending(out SpiritItemDetailList items);
            ending.Build(() => { }, () => { });
            var won = Play(setup.Catalog, TestEncounters.IdOf(WonEncounter));

            ending.Render(won, journey, "", controls, () => setup.Sources);
            Assert.That(ShownNames(items), Is.EqualTo(new[] { "Test Lantern" }));

            var lost = Play(setup.Catalog, TestEncounters.IdOf(LostEncounter));
            Assert.That(lost.Outcome, Is.EqualTo(EncounterOutcome.Lost));
            ending.Render(lost, journey, "", controls, () => setup.Sources);
            Assert.That(items.gameObject.activeSelf, Is.False, "A loss shows nothing.");

            controls.GiveRewards(journey.RewardsThrough(journey.Beats.Count - 1));
            ending.Render(won, journey, "", controls, () => setup.Sources);
            Assert.That(items.gameObject.activeSelf, Is.False, "A win whose items the player owns shows none.");
        }

        /**
         * Checks arriving at the board shows the items a completed journey's rewards hold that the player lacks, its button
         * gives those rewards and closes the panel, and arriving shows nothing when nothing is lacking or no journey is
         * completed / fails if the panel misses a lacked item, gives without the button, or shows when nothing is lacking.
         */
        [Test]
        public void TheBoardShowsACompletedJourneysLackedItemsAndGivesThemOnItsButton()
        {
            var setup = Setup();
            var journey = TestJourneys.Identify(Create<EncounterJourneyAsset>(), "journey.item_detail");
            TestJourneys.SetEncounterBeats(journey, null, content.Encounter(WonEncounter), new DialogueBeat());
            SyntheticUi.Set(journey, "completionReward", Reward(setup.Item));
            var controls = new TestJourneyControls
            {
                JourneyCatalog = TestJourneys.Catalog(Create<EncounterJourneyCatalogAsset>(),
                    TestJourneys.Chapter(Create<EncounterChapterAsset>(), "Chapter", journey)),
                RewardCatalog = setup.Catalog
            };
            var panel = BoardRewards(out SpiritItemDetailList items, out Button button);
            panel.Build();

            panel.Arrive(controls, () => setup.Sources);
            Assert.That(panel.IsOpen, Is.False, "A journey not completed gives nothing on the board.");

            controls.CompletedJourneyIds = new[] { journey.Identity };
            panel.Arrive(controls, () => setup.Sources);
            Assert.That(panel.IsOpen, Is.True);
            Assert.That(ShownNames(items), Is.EqualTo(new[] { "Test Lantern" }));
            Assert.That(controls.Given, Is.Empty, "Nothing is given before the button.");

            button.onClick.Invoke();
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(controls.Given.Single(), Is.EqualTo(journey.AllRewards.ToArray()));
            Assert.That(controls.Profile.OwnedItemIds, Has.Member(ItemId));

            panel.Arrive(controls, () => setup.Sources);
            Assert.That(panel.IsOpen, Is.False, "Nothing lacking shows nothing.");
            Assert.That(controls.Given.Count, Is.EqualTo(1));
        }

        private sealed class Fabricated
        {
            public ItemAsset Item;
            public ModifierDefinition Charm;
            public MechanicalCatalog Catalog;
            public ItemDetailSources Sources;
        }

        /**
         * A catalog of a won and a lost one-hand encounter and one item (a carry-over rule adding one charm, and its piece),
         * with the item's presentation: "Test Lantern", an icon and one rule card "Carry".
         */
        private Fabricated Setup()
        {
            var charm = RewardTestContent.Modifier("item_detail.charm", "family.item_detail");
            var piece = new TablePieceDefinition(PieceId, new[] { new PositionInteractionDefinition(PiecePosition.Neutral, PieceMovementTiming.DuringHand) });
            var definition = new ItemDefinition(ItemId, new[] { new DieCarryOverRuleDefinition(RuleId, PieceId, new[] { charm.Identity }) }, piece);
            var catalog = new MechanicalCatalog("test.item_detail", TestContent.Hands(), new[]
            {
                new EncounterDefinition(TestEncounters.IdOf(WonEncounter), new[] { new StageDefinition(1, 1, 0) }),
                new EncounterDefinition(TestEncounters.IdOf(LostEncounter), new[] { new StageDefinition(1000000, 1, 0) })
            }, new[] { charm }, new[] { definition });
            var item = Item("Test Lantern Item");
            SyntheticUi.Set(item, ContentAsset.AssetGuidField, ItemId.Value.ToString("N"));
            var mechanical = content.Content(catalog, new[] { new KeyValuePair<ItemAsset, ItemDefinition>(item, definition) },
                out ModifierPresentationCatalog charms);

            var card = Create<EncounterRulePresentationAsset>();
            SyntheticUi.Set(card, "rule", content.Own(TestRules.Asset(RuleId)));
            SyntheticUi.Set(card, "displayName", "Carry");
            SyntheticUi.Set(card, "description", "Carries the die");
            var presentation = Create<ItemPresentationAsset>();
            SyntheticUi.Set(presentation, "item", item);
            SyntheticUi.Set(presentation, "displayName", "Test Lantern");
            SyntheticUi.Set(presentation, "icon", Icon());
            SyntheticUi.Set(presentation, "ruleCards", new[] { card });
            var presenter = Create<ItemPositionPresenterAsset>();
            SyntheticUi.Set(presenter, "indicatorPrefab", IndicatorPrefab());
            SyntheticUi.Set(presentation, "piecePresenter", presenter);
            var skin = Create<ItemSkinAsset>();
            SyntheticUi.Set(skin, "items", new[] { presentation });
            return new Fabricated
            {
                Item = item, Charm = charm, Catalog = catalog,
                Sources = new ItemDetailSources(catalog, charms, new ItemPresentationCatalog(skin, mechanical))
            };
        }

        /** The completed snapshot of playing the encounter's one hand. */
        private static EncounterSnapshot Play(MechanicalCatalog catalog, EncounterDefinition.Id encounterId)
        {
            var application = new EncounterApplication(catalog, encounterId, 7);
            application.Submit(PlayHandAction.Instance, application.Snapshot.Revision);
            Assert.That(application.Snapshot.Phase, Is.EqualTo(EncounterPhase.Completed));
            return application.Snapshot;
        }

        /** The names of the item details the list shows. */
        private static string[] ShownNames(SpiritItemDetailList items)
        {
            Assert.That(items.gameObject.activeSelf, Is.True, "The item details show.");
            return items.Details.Where(detail => detail.gameObject.activeSelf).Select(detail => detail.ItemName.text).ToArray();
        }

        private ItemAsset Item(string name)
        {
            var item = Create<ItemAsset>();
            item.name = name;
            return item;
        }

        private ProgressionRewardAsset Reward(params ItemAsset[] items)
        {
            var reward = Create<ProgressionRewardAsset>();
            SyntheticUi.Set(reward, "grantedItems", items);
            return reward;
        }

        private Sprite Icon()
        {
            var texture = ui.Own(new Texture2D(2, 2));
            return ui.Own(Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f)));
        }

        /** A bay indicator with its glow and emblem on the tray's render layer, as an item position presenter requires. */
        private SpiritBayIndicator IndicatorPrefab()
        {
            var indicator = ui.Add<SpiritBayIndicator>(ui.Root, "Bay Indicator");
            indicator.gameObject.layer = SpiritDiceTray.RenderLayer;
            var glow = ui.Add<SpriteRenderer>(indicator.transform, "Glow");
            var emblem = ui.Add<SpriteRenderer>(indicator.transform, "Emblem");
            glow.gameObject.layer = emblem.gameObject.layer = SpiritDiceTray.RenderLayer;
            SyntheticUi.Set(indicator, "glow", glow);
            SyntheticUi.Set(indicator, "emblem", emblem);
            return indicator;
        }

        /** A journey view holding what showing a beat of a table scene reads, with its item details. */
        private EncounterJourneyView JourneyView(out SpiritItemDetailList items)
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
            items = ui.ItemDetailList(narrative);
            SyntheticUi.Set(view, "itemDetails", items);
            SyntheticUi.Set(view, "journeyStatus", ui.Label(view.transform, "Status"));
            SyntheticUi.Set(view, "advanceButton", ui.LabeledButton(view.transform, "Advance"));
            SyntheticUi.Set(view, "acceptButton", ui.LabeledButton(view.transform, "Accept"));
            SyntheticUi.Set(view, "skipButton", ui.NewButton(view.transform, "Skip"));
            SyntheticUi.Set(view, "returnButton", ui.NewButton(view.transform, "Return"));
            SyntheticUi.Set(view, "<EncounterCharms>k__BackingField", ui.CharmsScreen(view.transform));
            return view;
        }

        /** An ending view with every authored reference it requires, its item details in its scrolling content. */
        private SpiritEncounterEndingView Ending(out SpiritItemDetailList items)
        {
            var ending = ui.Add<SpiritEncounterEndingView>(ui.Root, "Ending");
            SyntheticUi.Set(ending, "<Root>k__BackingField", (RectTransform)ending.transform);
            var panel = ui.Rect(ending.transform, "Panel");
            SyntheticUi.Set(ending, "panel", panel);
            var scroll = ui.Add<ScrollRect>(panel, "Scroll");
            var scrollContent = ui.Rect(scroll.transform, "Content");
            scroll.content = scrollContent;
            SyntheticUi.Set(ending, "content", scrollContent);
            SyntheticUi.Set(ending, "scroll", scroll);
            SyntheticUi.Set(ending, "title", ui.Label(scrollContent, "Title"));
            SyntheticUi.Set(ending, "detail", ui.Label(scrollContent, "Detail"));
            SyntheticUi.Set(ending, "unlockTitle", ui.Label(scrollContent, "Unlock Title"));
            SyntheticUi.Set(ending, "unlocks", ui.Label(scrollContent, "Unlocks"));
            items = ui.ItemDetailList(scrollContent);
            SyntheticUi.Set(ending, "itemDetails", items);
            var frame = ui.Rect(scrollContent, "Artwork Frame");
            SyntheticUi.Set(ending, "artworkFrame", frame);
            var artwork = ui.Add<RawImage>(frame, "Artwork");
            SyntheticUi.Set(ending, "artwork", artwork);
            SyntheticUi.Set(ending, "artworkAspect", artwork.gameObject.AddComponent<AspectRatioFitter>());
            SyntheticUi.Set(ending, "returnButton", ui.LabeledButton(panel, "Return"));
            SyntheticUi.Set(ending, "retryButton", ui.LabeledButton(panel, "Retry"));
            SyntheticUi.Set(ending, "dialogueContinueButton", ui.LabeledButton(panel, "Continue"));
            SyntheticUi.Set(ending, "skipButton", ui.LabeledButton(ending.transform, "Skip"));
            return ending;
        }

        /** A board rewards panel with its item details and Continue button. */
        private SpiritBoardRewardsPanel BoardRewards(out SpiritItemDetailList items, out Button button)
        {
            var panel = ui.Add<SpiritBoardRewardsPanel>(ui.Root, "Board Rewards");
            items = ui.ItemDetailList(panel.transform);
            button = ui.LabeledButton(panel.transform, "Continue");
            SyntheticUi.Set(panel, "itemDetails", items);
            SyntheticUi.Set(panel, "continueButton", button);
            return panel;
        }

        private T Create<T>() where T : ScriptableObject => ui.Own(ScriptableObject.CreateInstance<T>());

        /** Controls that hold the fabricated catalog and presentations, for a journey screen to read its items from. */
        private sealed class Controls : ISpiritEncounterControls
        {
            private readonly ItemDetailSources sources;

            public Controls(Fabricated setup) => sources = setup.Sources;

            public EncounterSnapshot Snapshot => null;
            public MechanicalCatalog Catalog => sources.Catalog;
            public EncounterInPlay InPlay => null;
            public IReadOnlyList<EncounterRuleDefinition> InstalledRules => null;
            public IReadOnlyList<TablePieceDefinition> InstalledPieces => null;
            public ModifierPresentationCatalog ModifierPresentation => sources.Charms;
            public ItemPresentationCatalog ItemPresentation => sources.Items;
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
