using System;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    /**
     * The Spirit House wording asset over fabricated wording: composed text fills the asset's templates with its values,
     * a count chooses between singular and plural wording, and missing wording fails naming its field. Status, scoring,
     * collection, reward card, hand ranks and charm description wording are checked where those texts are composed.
     */
    public sealed class SpiritUiWordingTests
    {
        private SpiritUiWording wording;

        [SetUp]
        public void SetUp() => wording = SyntheticWording.Create();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(wording);

        /** Checks a count of one chooses the singular wording and any other count the plural / fails if zero or many read as one. */
        [TestCase(0, "plural")]
        [TestCase(1, "singular")]
        [TestCase(2, "plural")]
        public void ACountChoosesBetweenSingularAndPluralWording(long count, string expected)
        {
            var counted = new SpiritCountedWording { singular = "singular", plural = "plural" };
            Assert.That(counted.For(count), Is.EqualTo(expected));
        }

        /** Checks save slot and recovery text fill their templates with their values / fails if a value, form or template is not the asset's. */
        [Test]
        public void SaveSlotAndRecoveryTextUseTheirTemplates()
        {
            var slots = wording.saveSlots;
            Assert.That(slots.RecoverSavesButton(2), Is.EqualTo("<saveSlots.recoverSavesButton>2"));
            Assert.That(slots.Slot(2, false), Is.EqualTo("<saveSlots.slot>2"));
            Assert.That(slots.Slot(2, true), Is.EqualTo("<saveSlots.activeSlot>2"));
            Assert.That(slots.Progress(1, 3),
                Is.EqualTo("<saveSlots.progress><saveSlots.encountersCompleted.one>1|<saveSlots.charmsCollected.many>3"));
            Assert.That(slots.UseSlotConfirmation(3), Is.EqualTo("<saveSlots.useSlotConfirmation>3"));
            Assert.That(slots.ReplaceSlotConfirmation(3), Is.EqualTo("<saveSlots.replaceSlotConfirmation>3"));
            var recovery = wording.recovery;
            Assert.That(recovery.SavedAt(new DateTime(2026, 1, 2, 3, 4, 0)), Is.EqualTo("<recovery.savedAt>20260102"));
            Assert.That(recovery.Identifier(2, "abc"), Is.EqualTo("<recovery.identifier>2|abc"));
            Assert.That(recovery.Identifier(0, "abc"), Is.EqualTo("<recovery.identifier><recovery.unknownSlot>|abc"));
            Assert.That(recovery.Page(1, 3), Is.EqualTo("<recovery.page>1|3"));
        }

        /** Checks collection text fills its templates, words each filter and joins sources with its separators / fails if a filter shares another's wording or a source is joined another way. */
        [Test]
        public void CollectionTextUsesItsTemplates()
        {
            var collection = wording.collection;
            Assert.That(collection.Summary(1, 4, 2), Is.EqualTo("<collection.summary>1|4|2"));
            Assert.That(collection.FilterName(SpiritCollectionFilter.Owned), Is.EqualTo("<collection.ownedFilter>"));
            Assert.That(collection.FilterName(SpiritCollectionFilter.Discovered), Is.EqualTo("<collection.discoveredFilter>"));
            Assert.That(collection.FilterName(SpiritCollectionFilter.All), Is.EqualTo("<collection.allFilter>"));
            Assert.That(collection.NoFamilies(SpiritCollectionFilter.Owned), Is.EqualTo("<collection.noOwnedFamilies>"));
            Assert.That(collection.NoFamilies(SpiritCollectionFilter.Discovered), Is.EqualTo("<collection.noDiscoveredFamilies>"));
            Assert.That(collection.NoFamilies(SpiritCollectionFilter.All), Is.EqualTo("<collection.noFamilies>"));
            Assert.That(collection.DetailTitle("Name", "State"), Is.EqualTo("<collection.detailTitle>Name|State"));
            Assert.That(collection.UnlockSource("Place", new[] { 3 }), Is.EqualTo("<collection.unlockSource.one>Place|3"));
            Assert.That(collection.UnlockSource("Place", new[] { 1, 3 }),
                Is.EqualTo("<collection.unlockSource.many>Place|1 |stageSeparator| 3"));
            Assert.That(collection.StoryGift("Place"), Is.EqualTo("<collection.storyGift>Place"));
            Assert.That(collection.CompletionGift("Place"), Is.EqualTo("<collection.completionGift>Place"));
            Assert.That(collection.UnlockChoices(new[] { "a", "b" }), Is.EqualTo("<collection.unlockChoices>a |sourceSeparator| b"));
            Assert.That(collection.Gifts(new[] { "a", "b" }), Is.EqualTo("<collection.gifts>a |sourceSeparator| b"));
            Assert.That(collection.Rewards(new string[0]), Is.EqualTo("<collection.noRunRewards>"));
            Assert.That(collection.Rewards(new[] { "a", "b" }), Is.EqualTo("a |rewardSeparator| b"));
            Assert.That(collection.EncounterOnlySource("r"), Is.EqualTo("<collection.encounterOnlySource>r"));
            Assert.That(collection.StarterSource("r"), Is.EqualTo("<collection.starterSource>r"));
            Assert.That(collection.UnlockableSource("s", "r"), Is.EqualTo("<collection.unlockableSource>s|r"));
        }

        /** Checks rewards panel and HUD text fill their templates and choose each counted phrase by its own count / fails if a count, score format or form is wrong. */
        [Test]
        public void RewardsAndHudTextUseTheirTemplates()
        {
            Assert.That(wording.rewards.UpcomingStage(2, 4, 1200), Is.EqualTo("<rewards.upcomingStage>2|4|1,200"));
            Assert.That(wording.rewards.RefreshButton(2), Is.EqualTo("<rewards.refreshButton>2"));
            var hud = wording.hud;
            Assert.That(hud.Stage(2, 5), Is.EqualTo("<hud.stage>2|5"));
            Assert.That(hud.Hands(3), Is.EqualTo("<hud.hands>3"));
            Assert.That(hud.Rerolls(1), Is.EqualTo("<hud.rerolls>1"));
            Assert.That(hud.Resources(1, 2), Is.EqualTo("<hud.resources><hud.handsLeft.one>1|<hud.rerollsLeft.many>2"));
            Assert.That(hud.LastHand(1), Is.EqualTo("<hud.lastHand.one>1"));
            Assert.That(hud.LastHand(1500), Is.EqualTo("<hud.lastHand.many>1,500"));
            Assert.That(hud.TotalScoreLine(1500), Is.EqualTo("<hud.totalScoreLine>1,500"));
            Assert.That(hud.CharmCount(2), Is.EqualTo("<hud.charmCount>2"));
            Assert.That(hud.CharmsButton(2), Is.EqualTo("<hud.charmsButton>2"));
            Assert.That(hud.RerollsRemaining(1), Is.EqualTo("<hud.rerollsRemaining.one>1"));
            Assert.That(hud.StageIntro(1300, 1, 2),
                Is.EqualTo("<hud.stageIntro.many>1,300|<hud.stageHands.one>1|<hud.stageRerolls.many>2"));
        }

        /** Checks pause menu, owned charm, availability and ending text fill their templates / fails if a choice picks the other wording or a value is dropped. */
        [Test]
        public void MenuCharmAndEndingTextUseTheirTemplates()
        {
            var pause = wording.pauseMenu;
            Assert.That(pause.Sound(true), Is.EqualTo("<pauseMenu.soundOff>"));
            Assert.That(pause.Sound(false), Is.EqualTo("<pauseMenu.soundOn>"));
            Assert.That(pause.ProfileSummary(1, 2),
                Is.EqualTo("<pauseMenu.profileSummary><pauseMenu.charmsUnlocked.one>1|<pauseMenu.encountersCompleted.many>2"));
            Assert.That(pause.ProfileAttention(true), Is.EqualTo("<pauseMenu.profileAttention><pauseMenu.progressPending>"));
            Assert.That(pause.ProfileAttention(false), Is.EqualTo("<pauseMenu.profileAttention><pauseMenu.profileUnreadable>"));
            Assert.That(wording.charmAvailability.Availability("a", "s"), Is.EqualTo("<charmAvailability.availability>a|s"));
            Assert.That(wording.charmAvailability.ScopeTier("s", 2), Is.EqualTo("<charmAvailability.scopeTier>s|2"));
            Assert.That(wording.ownedCharms.DetailBody("c", "e", "t", "a"), Is.EqualTo("<ownedCharms.detailBody>c|e|t|a"));
            var ending = wording.ending;
            Assert.That(ending.Summary(null, 1500), Is.EqualTo("<ending.scoreSummary>1,500"));
            Assert.That(ending.Summary("Farewell", 7), Is.EqualTo("<ending.farewellScoreSummary>Farewell|7"));
            Assert.That(ending.UnlockedCharm("n", "s"), Is.EqualTo("<ending.unlockedCharm>n|s"));
            Assert.That(ending.Unlocks(new[] { "a", "b" }, true), Is.EqualTo("<ending.pendingUnlocks>a |unlockedCharmSeparator| b"));
            Assert.That(ending.Unlocks(new[] { "a" }, false), Is.EqualTo("<ending.permanentUnlocks>a"));
        }

        /** Checks journey, investigation, chapter and narration text fill their templates / fails if a count or value is ignored. */
        [Test]
        public void JourneyAndInvestigationTextUseTheirTemplates()
        {
            Assert.That(wording.journey.ruleHeading.For(1), Is.EqualTo("<journey.ruleHeading.one>"));
            Assert.That(wording.journey.ruleHeading.For(2), Is.EqualTo("<journey.ruleHeading.many>"));
            Assert.That(wording.investigation.ChapterProgress(2), Is.EqualTo("<investigation.chapterProgress>2"));
            Assert.That(wording.chapters.Number(3), Is.EqualTo("<chapters.number>3"));
            Assert.That(wording.encounterCharms.Narration("Words"), Is.EqualTo("<encounterCharms.narration>Words"));
        }

        /** Checks every hand category is named by its own wording / fails if a category shares another's name. */
        [TestCase(HandCategory.HighDie, "<handNames.highDie>")]
        [TestCase(HandCategory.Pair, "<handNames.pair>")]
        [TestCase(HandCategory.TwoPair, "<handNames.twoPair>")]
        [TestCase(HandCategory.ThreeOfAKind, "<handNames.threeOfAKind>")]
        [TestCase(HandCategory.FullHouse, "<handNames.fullHouse>")]
        [TestCase(HandCategory.FourOfAKind, "<handNames.fourOfAKind>")]
        [TestCase(HandCategory.Straight, "<handNames.straight>")]
        [TestCase(HandCategory.FiveOfAKind, "<handNames.fiveOfAKind>")]
        public void EveryHandCategoryIsNamedByItsWording(HandCategory category, string expected)
        {
            Assert.That(wording.handNames.Name(category), Is.EqualTo(expected));
        }

        /** Checks missing wording fails validation naming its asset, section and field, while spaces suffice for a separator / fails if missing wording passes or the message names another field. */
        [Test]
        public void MissingWordingFailsNamingItsField()
        {
            Assert.DoesNotThrow(() => wording.Validate());
            wording.status.separator = " ";
            Assert.DoesNotThrow(() => wording.Validate(), "A separator may be spaces.");
            wording.handNames.straight = " ";
            Assert.That(Assert.Throws<InvalidOperationException>(() => wording.Validate()).Message,
                Does.Contain("SyntheticWording").And.Contain("handNames.straight"));
            wording.handNames.straight = "<handNames.straight>";
            wording.hud.lastHand.plural = null;
            Assert.That(Assert.Throws<InvalidOperationException>(() => wording.Validate()).Message, Does.Contain("hud.lastHand.plural"));
            wording.hud.lastHand.plural = "<hud.lastHand.many>{score}";
            wording.collection.sourceSeparator = "";
            Assert.That(Assert.Throws<InvalidOperationException>(() => wording.Validate()).Message,
                Does.Contain("collection.sourceSeparator"));
            wording.collection.sourceSeparator = "; ";
            wording.charmText.highlightedTerms = new string[0];
            Assert.That(Assert.Throws<InvalidOperationException>(() => wording.Validate()).Message, Does.Contain("charmText.highlightedTerms"));
            wording.charmText.highlightedTerms = new[] { "Pair" };
            wording.ending = null;
            Assert.That(Assert.Throws<InvalidOperationException>(() => wording.Validate()).Message, Does.Contain("ending"));
        }

        /** Checks configuring a screen whose wording lacks a field it uses fails naming the field / fails if the screen configures and fails later. */
        [Test]
        public void ConfiguringAScreenWithMissingWordingFailsNamingTheField()
        {
            using var ui = new SyntheticUi();
            ui.Wording.handRanks.level = null;
            var panel = ui.Add<SpiritHandRanksPanel>(ui.Root, "Hand Ranks");
            Assert.That(Assert.Throws<InvalidOperationException>(() => panel.Build(ui.Root, () => { })).Message,
                Does.Contain("handRanks.level"));
        }
    }
}
