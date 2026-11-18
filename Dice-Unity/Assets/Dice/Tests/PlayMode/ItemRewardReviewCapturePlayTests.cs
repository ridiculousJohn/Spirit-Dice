using System.Collections;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Dice.Tests
{
    /**
     * Review capture of an item a reward gives, shown before it is given: on the journey screen, on a win's ending and on
     * the investigation board. No authored reward grants an item, so it plays copies of the first journey with an
     * encounter, its beats ending at the Encounter beat and its encounter one one-point stage, each part with a fresh
     * profile. A beat's reward: the first beat shows the carry-over item, Continue gives it, and the beat shown again shows
     * none. A completion reward: the win's ending shows the item, the profile does not own it until the ending's Continue,
     * and a second win shows none. The app closed on a won ending: a fresh controller on the same profile arrives at the
     * board the way the main menu prepares a campaign, and the board's panel shows the item and gives it on its button. A
     * skip: the invitation it lands on shows the skipped beat's item. It captures at 540 and 443 wide the beat, the ending,
     * the board panel and the skip's landing beat showing the item, the beat, the ending and the landing beat also
     * scrolled to their end. No content asset changes. It writes PNGs under
     * DICE_SPIRIT_CAPTURE_ROOT/item-rewards and is ignored without that variable; it is a capture, not a regression test.
     * The fixture is explicit: it runs only when a run selects it by name.
     */
    [Explicit("Review capture tool; select it by name with DICE_SPIRIT_CAPTURE_ROOT set.")]
    [Category("Capture")]
    public sealed class ItemRewardReviewCapturePlayTests
    {
        // Real seconds a beat, the ending or the board rests before it is captured, so its motion and layout settle.
        private const float SettleSeconds = 1.5f;
        // The skipped beat that carries the reward in the skip part; the first journey's invitation follows it.
        private const int SkippedBeat = 2;

        [UnityTest]
        public IEnumerator ItemRewardOnBeatEndingAndBoardCapture()
        {
            string root = NarrowCropReviewCapturePlayTests.Fresh("item-rewards");

            // A beat's reward: shown with the beat, given on Continue, not shown again.
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.JourneyAtBoard();
            var controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            var carryOver = CarryOver(controller);
            string itemName = DisplayName(controller, carryOver);
            var authored = controller.JourneyCatalog.Entries.First(entry => entry != null && entry.Encounters.Any());
            var journey = Copy(authored, ItemAssetOf(controller, carryOver), 0);
            Install(controller, authored, journey, controller.SavePath);
            yield return OpenAtFirstBeat(controller, journey);
            var view = ContentReady.EncounterView(controller);
            Assert.That(ShownItems(view.JourneyView), Is.EqualTo(new[] { itemName }), "The first beat shows its item.");
            yield return new WaitForSecondsRealtime(SettleSeconds);
            yield return CaptureBothWidths(root, authored.name + "-beat-reward");
            yield return CaptureScrolledToEnd(root, authored.name + "-beat-reward", view.JourneyView);
            Assert.That(Owns(controller, carryOver), Is.False, "The item is given on continuing, not on showing the beat.");
            controller.AdvanceJourney();
            yield return ContentReady.Routed(controller);
            Assert.That(Owns(controller, carryOver), Is.True, "Continuing from the beat gives its item.");
            yield return OpenAtFirstBeat(controller, journey);
            Assert.That(ShownItems(ContentReady.EncounterView(controller).JourneyView), Is.Empty,
                "The beat shown again shows no item the player owns.");

            // A completion reward: shown on the win's ending, given on its Continue, not shown on a second win.
            yield return ContentReady.LoadApplication();
            controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            journey = Copy(authored, ItemAssetOf(controller, carryOver), -1);
            Install(controller, authored, journey, controller.SavePath);
            yield return Win(controller, journey);
            view = ContentReady.EncounterView(controller);
            Assert.That(controller.CompletedJourneyIds, Has.Member(journey.Identity), "The win records the journey as completed.");
            Assert.That(ShownItems(EndingItems(view)), Is.EqualTo(new[] { itemName }), "The ending shows the item its Continue gives.");
            Assert.That(Owns(controller, carryOver), Is.False, "The win gives nothing.");
            yield return new WaitForSecondsRealtime(SettleSeconds);
            yield return CaptureBothWidths(root, authored.name + "-ending-reward");
            yield return CaptureScrolledToEnd(root, authored.name + "-ending-reward", EndingItems(view));
            ContentReady.Click(ContentReady.Find<Button>(controller, "EndingNewRunButton"));
            yield return ContentReady.Routed(controller);
            Assert.That(Owns(controller, carryOver), Is.True, "The ending's Continue gives the item.");
            yield return Win(controller, journey);
            Assert.That(ShownItems(EndingItems(ContentReady.EncounterView(controller))), Is.Empty,
                "A second win shows no item the player owns.");

            // The app closed on a won ending: a fresh controller on the same profile shows the item on the board.
            yield return ContentReady.LoadApplication();
            controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            journey = Copy(authored, ItemAssetOf(controller, carryOver), -1);
            Install(controller, authored, journey, controller.SavePath);
            yield return Win(controller, journey);
            Assert.That(ShownItems(EndingItems(ContentReady.EncounterView(controller))), Is.EqualTo(new[] { itemName }));
            string savePath = controller.SavePath;
            string profilePath = controller.ProfilePath;
            yield return ContentReady.LoadApplication();
            controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            journey = Copy(authored, ItemAssetOf(controller, carryOver), -1);
            Install(controller, authored, journey, savePath);
            yield return ContentReady.Routed(controller);
            Assert.That(controller.ProfilePath, Is.EqualTo(profilePath), "The fresh controller reads the same profile.");
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            var panel = ContentReady.FrontendView(controller).BoardRewards;
            Assert.That(panel.IsOpen, Is.True, "Arriving at the board shows the completed journey's item the player lacks.");
            Assert.That(ShownItems(panel.ItemDetails), Is.EqualTo(new[] { itemName }));
            Assert.That(Owns(controller, carryOver), Is.False, "The board gives nothing before its button.");
            yield return new WaitForSecondsRealtime(SettleSeconds);
            yield return CaptureBothWidths(root, authored.name + "-board-reward");
            ContentReady.Click(panel.ContinueButton);
            yield return null;
            Assert.That(panel.IsOpen, Is.False);
            Assert.That(Owns(controller, carryOver), Is.True, "The board's button gives the item.");

            // A skip: the invitation it lands on shows the skipped beat's item.
            yield return ContentReady.LoadApplication();
            controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            journey = Copy(authored, ItemAssetOf(controller, carryOver), SkippedBeat);
            var profile = PlayerProfileFileStore.LoadOrCreate(controller.ProfilePath);
            profile.VisitEncounter(journey.Encounters.First().Identity);
            PlayerProfileFileStore.Save(controller.ProfilePath, profile);
            Install(controller, authored, journey, controller.SavePath);
            yield return OpenAtFirstBeat(controller, journey);
            int invitation = journey.IndexOfEncounter(journey.Encounters.First().Identity) - 1;
            Assert.That(invitation, Is.GreaterThan(SkippedBeat), "The skipped beat lies before the invitation.");
            Assert.That(ShownItems(ContentReady.EncounterView(controller).JourneyView), Is.Empty, "The first beat gives no item.");
            controller.SkipIntro();
            yield return ContentReady.Routed(controller);
            Assert.That(controller.BeatIndex, Is.EqualTo(invitation));
            Assert.That(Owns(controller, carryOver), Is.False, "The skip gives nothing by itself.");
            var journeyView = ContentReady.EncounterView(controller).JourneyView;
            yield return ContentReady.WaitUntil(() => !journeyView.IsAnimating && !journeyView.IsEasingNarrative, "the invitation",
                ContentReady.SequenceTimeout);
            Assert.That(ShownItems(journeyView), Is.EqualTo(new[] { itemName }), "The invitation shows the skipped beat's item.");
            yield return new WaitForSecondsRealtime(SettleSeconds);
            yield return CaptureBothWidths(root, authored.name + "-skip-landing");
            yield return CaptureScrolledToEnd(root, authored.name + "-skip-landing", journeyView);
            LogAssert.NoUnexpectedReceived();
        }

        private static ItemDefinition CarryOver(SpiritEncounterController controller)
        {
            var item = controller.Catalog.Items.FirstOrDefault(candidate => candidate.Rules.Any(rule => rule is DieCarryOverRuleDefinition));
            Assert.That(item, Is.Not.Null, "The catalog has no carry-over item.");
            return item;
        }

        /** The item asset of the item, through the item presentation that references it. */
        private static ItemAsset ItemAssetOf(SpiritEncounterController controller, ItemDefinition item)
        {
            var asset = controller.ItemPresentation.PresentationOf(item).Item;
            Assert.That(asset, Is.Not.Null);
            return asset;
        }

        private static string DisplayName(SpiritEncounterController controller, ItemDefinition item)
            => controller.ItemPresentation.PresentationOf(item).DisplayName;

        /** Whether the saved profile owns the item. */
        private static bool Owns(SpiritEncounterController controller, ItemDefinition item)
            => PlayerProfileFileStore.Load(controller.ProfilePath).OwnedItemIds.Contains(item.Identity);

        /**
         * A copy of the journey whose beats end at its Encounter beat, beats taking no time, and whose beat at rewardBeat,
         * or its completion reward when rewardBeat is negative, grants the item.
         */
        private static EncounterJourneyAsset Copy(EncounterJourneyAsset authored, ItemAsset item, int rewardBeat)
        {
            var journey = Object.Instantiate(authored);
            int encounter = journey.IndexOfEncounter(journey.Encounters.First().Identity);
            var beats = journey.Beats.Take(encounter + 1).ToArray();
            foreach (var beat in beats)
            {
                ContentReady.SetField(beat, "duration", 0f);
                if (beat is ArrivalBeat) ContentReady.SetField(beat, "autoAdvance", false);
            }
            ContentReady.SetField(journey, "beats", beats);
            ContentReady.SetField(journey, "clueBeat", null);
            if (rewardBeat >= 0) ContentReady.SetField(beats[rewardBeat], "progressionReward", Reward(item));
            else ContentReady.SetField(journey, "completionReward", Reward(item));
            Assert.That(journey.WinCompletes(journey.Encounters.First().Identity), Is.True);
            return journey;
        }

        private static ProgressionRewardAsset Reward(ItemAsset item)
        {
            var reward = ScriptableObject.CreateInstance<ProgressionRewardAsset>();
            ContentReady.SetField(reward, "grantedItems", new[] { item });
            return reward;
        }

        /**
         * Installs a catalog in which the journey's encounter has one one-point stage and a journey catalog holding the
         * copy in place of the authored journey, prepared on the save path's campaign as the main menu prepares one (the
         * menu, then the board), with the journey's prerequisites completed.
         */
        private static void Install(SpiritEncounterController controller, EncounterJourneyAsset authored, EncounterJourneyAsset journey,
            string savePath)
        {
            var original = controller.Catalog;
            var encounterId = journey.Encounters.First().Identity;
            var definition = original.GetEncounter(encounterId);
            var shortEncounter = new EncounterDefinition(definition.Identity, new[] { new StageDefinition(1, 1, 0) },
                rules: definition.OwnRules, encounterModifierIds: definition.OwnEncounterModifierIds, pieces: definition.OwnPieces,
                encounterModifiersFirstClearedStageNumber: 1, items: definition.Items);
            var mechanical = new MechanicalCatalog(original.Version + ".item-reward-review", original.Hands,
                original.Encounters.Select(encounter => encounter.Identity == encounterId ? shortEncounter : encounter),
                original.Modifiers, original.Items);
            var catalog = Object.Instantiate(controller.JourneyCatalog);
            ContentReady.SetField(catalog, "chapters", controller.JourneyCatalog.Chapters.Select(chapter =>
            {
                var copy = Object.Instantiate(chapter);
                ContentReady.SetField(copy, "entries", chapter.Entries.Select(value => value == authored ? journey : value).ToArray());
                return copy;
            }).ToArray());
            Assert.That(controller.InitializeMenu(mechanical, catalog, savePath), Is.True, controller.JourneyStatus.ToString());
            Assert.That(controller.InitializeJourney(mechanical, catalog, savePath), Is.True, controller.JourneyStatus.ToString());
            ContentReady.SeedJourneyPrerequisites(controller, journey);
        }

        /** Opens the journey from the board at its first beat and waits for the beat to settle. */
        private static IEnumerator OpenAtFirstBeat(SpiritEncounterController controller, EncounterJourneyAsset journey)
        {
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            controller.SelectEncounter(journey);
            yield return ContentReady.Routed(controller);
            Assert.That(controller.BeatIndex, Is.Zero);
            var view = ContentReady.EncounterView(controller).JourneyView;
            yield return ContentReady.WaitUntil(() => !view.IsAnimating && !view.IsEasingNarrative, "the first beat", ContentReady.SequenceTimeout);
        }

        /** Plays the journey from the board to a won encounter and waits for the ending's summary. */
        private static IEnumerator Win(SpiritEncounterController controller, EncounterJourneyAsset journey)
        {
            yield return OpenAtFirstBeat(controller, journey);
            var view = ContentReady.EncounterView(controller);
            while (!(journey.Beats[controller.BeatIndex] is InvitationBeat)) controller.AdvanceJourney();
            yield return ContentReady.WaitUntil(() => !view.JourneyView.IsAnimating, "the invitation", ContentReady.SequenceTimeout);
            yield return ContentReady.AcceptEncounter(controller, ContentReady.Seed);
            yield return ContentReady.Idle(controller, ContentReady.SequenceTimeout);
            controller.PlayHand();
            yield return ContentReady.WaitUntil(() => controller.Snapshot != null && controller.Snapshot.Outcome == EncounterOutcome.Won,
                "the win", ContentReady.SequenceTimeout);
            var dialogue = ContentReady.Find<Button>(controller, "EndingDialogueContinueButton");
            var newRun = ContentReady.Find<Button>(controller, "EndingNewRunButton");
            yield return ContentReady.WaitUntil(() => dialogue.gameObject.activeInHierarchy || newRun.gameObject.activeInHierarchy,
                "the ending", ContentReady.SequenceTimeout);
            yield return ContentReady.Idle(controller, ContentReady.SequenceTimeout);
            if (dialogue.gameObject.activeInHierarchy)
            {
                ContentReady.Click(dialogue);
                yield return null;
            }
            Assert.That(newRun.gameObject.activeInHierarchy, Is.True, "The ending's summary shows.");
        }

        private static SpiritItemDetailList EndingItems(SpiritEncounterView view)
            => view.GetComponentsInChildren<SpiritItemDetailList>(true).Single(list => list.GetComponentInParent<SpiritEncounterEndingView>(true) != null);

        private static string[] ShownItems(Component holder)
        {
            var list = holder as SpiritItemDetailList ?? holder.GetComponentInChildren<SpiritItemDetailList>(true);
            return !list.gameObject.activeInHierarchy ? new string[0]
                : list.Details.Where(detail => detail.gameObject.activeInHierarchy).Select(detail => detail.ItemName.text).ToArray();
        }

        /** Scrolls the scroll view holding the item details to its end, so their charms show, and captures both widths. */
        private static IEnumerator CaptureScrolledToEnd(string root, string name, Component holder)
        {
            var list = holder as SpiritItemDetailList ?? holder.GetComponentInChildren<SpiritItemDetailList>(true);
            var scroll = list.GetComponentInParent<ScrollRect>(true);
            Assert.That(scroll, Is.Not.Null, "The item details sit in a scroll view.");
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0;
            yield return new WaitForSecondsRealtime(SettleSeconds);
            yield return CaptureBothWidths(root, name + "-scrolled");
        }

        private static IEnumerator CaptureBothWidths(string root, string name)
        {
            foreach (int width in new[] { NarrowCropReviewCapturePlayTests.Width, NarrowCropReviewCapturePlayTests.NarrowWidth })
                yield return NarrowCropReviewCapturePlayTests.Capture(root,
                    name + "-" + width + "x" + NarrowCropReviewCapturePlayTests.Height + ".png", width);
        }
    }
}
