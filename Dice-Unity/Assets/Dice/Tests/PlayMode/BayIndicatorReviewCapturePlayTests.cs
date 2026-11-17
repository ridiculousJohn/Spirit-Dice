using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dice.Tests
{
    /**
     * Review capture of the bay indicator, the mark an item puts on a dice bay, on every tray: at one table of each tray the
     * player carries the carry-over item, as a run carries owned items, and its piece is moved onto a middle bay through
     * the screen's controls; the first table is also captured with the die in that bay selected, with the piece at neutral
     * and on an end bay. The player then also carries the merchant's candle: a table that does not lend it is captured with
     * both pieces on one bay, their emblems side by side, and the merchant's table with both on one bay, where the candle
     * shows its scene prop and only carry-over is marked.
     * It writes 540 x 960 PNGs under DICE_SPIRIT_CAPTURE_ROOT/bay-indicators and is ignored without that variable; it is a
     * capture, not a regression test. The fixture is explicit: it runs only when a run selects it by name.
     */
    [Explicit("Review capture tool; select it by name with DICE_SPIRIT_CAPTURE_ROOT set.")]
    [Category("Capture")]
    public sealed class BayIndicatorReviewCapturePlayTests
    {
        // Real seconds the table rests after its opening roll or a piece move before it is captured, so props settle.
        private const float SettleSeconds = 1f;
        private const int MiddleBay = 2;
        private const int EndBay = 4;

        [UnityTest]
        public IEnumerator BayIndicatorsOnEveryTrayCapture()
        {
            string root = NarrowCropReviewCapturePlayTests.Fresh("bay-indicators");
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.JourneyAtBoard();
            var controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            var carryOver = ItemWithRule<DieCarryOverRuleDefinition>(controller.Catalog);
            var candle = ItemWithRule<BayWardRuleDefinition>(controller.Catalog);
            Own(controller, carryOver);

            var journeys = controller.JourneyCatalog.Entries.Where(journey => journey != null && journey.Encounters.Any()).ToArray();
            var perTray = new List<EncounterJourneyAsset>();
            var trays = new HashSet<SpiritTrayPresentationAsset>();
            foreach (var journey in journeys)
                if (trays.Add(Table(journey).TrayPresentation)) perTray.Add(journey);
            TestContext.WriteLine("Tables, one per tray: " + string.Join(", ", perTray.Select(journey => Table(journey).name)));

            bool first = true;
            foreach (var journey in perTray)
            {
                yield return Enter(controller, journey);
                string name = Table(journey).name;
                yield return Move(controller, carryOver, PiecePosition.Bay(MiddleBay));
                Assert.That(ShownIndicators(controller).Count, Is.EqualTo(1), name);
                yield return Capture(root, name + "-carry-over-bay-" + MiddleBay);
                if (!first) continue;
                first = false;
                controller.ToggleDie(MiddleBay);
                yield return ContentReady.Idle(controller);
                Assert.That(controller.SelectedMask, Is.EqualTo(1 << MiddleBay), name + " selection");
                yield return Capture(root, name + "-carry-over-bay-" + MiddleBay + "-selected");
                controller.ToggleDie(MiddleBay);
                yield return ContentReady.Idle(controller);
                yield return Move(controller, carryOver, PiecePosition.Neutral);
                Assert.That(ShownIndicators(controller), Is.Empty, name + " at neutral");
                yield return Capture(root, name + "-carry-over-neutral");
                yield return Move(controller, carryOver, PiecePosition.Bay(EndBay));
                yield return Capture(root, name + "-carry-over-bay-" + EndBay);
            }

            Own(controller, candle);
            var other = perTray.First(journey => !Lends(controller, journey, candle));
            var lender = perTray.First(journey => Lends(controller, journey, candle));
            foreach (var journey in new[] { other, lender })
            {
                yield return Enter(controller, journey);
                string name = Table(journey).name;
                yield return Move(controller, carryOver, PiecePosition.Bay(MiddleBay));
                yield return Move(controller, candle, PiecePosition.Bay(MiddleBay));
                // The merchant's table shows the candle as its scene prop, so only carry-over is marked there.
                int marks = journey == lender ? 1 : 2;
                Assert.That(ShownIndicators(controller).Count, Is.EqualTo(marks), name + " marks with both items on one bay");
                yield return Capture(root, name + "-carry-over-and-candle-bay-" + MiddleBay);
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static ItemDefinition ItemWithRule<TRule>(MechanicalCatalog catalog) where TRule : EncounterRuleDefinition
        {
            var item = catalog.Items.FirstOrDefault(candidate => candidate.Piece != null && candidate.Rules.Any(rule => rule is TRule));
            Assert.That(item, Is.Not.Null, "The catalog has no item with a piece and a " + typeof(TRule).Name + ".");
            return item;
        }

        /** Records the item as owned on the test profile, so every run started afterwards carries it. */
        private static void Own(SpiritEncounterController controller, ItemDefinition item)
        {
            var profile = PlayerProfileFileStore.LoadOrCreate(controller.ProfilePath);
            profile.OwnItem(item.Identity);
            PlayerProfileFileStore.Save(controller.ProfilePath, profile);
            Assert.That(controller.InitializeJourney(controller.Catalog, controller.JourneyCatalog, controller.SavePath), Is.True,
                controller.JourneyStatus.ToString());
        }

        private static EncounterTablePresentationAsset Table(EncounterJourneyAsset journey)
            => journey.TableSceneAt(journey.IndexOfEncounter(journey.Encounters.First().Identity));

        private static bool Lends(SpiritEncounterController controller, EncounterJourneyAsset journey, ItemDefinition item)
            => controller.Catalog.GetEncounter(journey.Encounters.First().Identity).Items.Any(lent => lent.Identity == item.Identity);

        private static IEnumerator Enter(SpiritEncounterController controller, EncounterJourneyAsset journey)
        {
            yield return ContentReady.EnterEncounter(controller, journey, ContentReady.Seed);
            yield return new WaitForSecondsRealtime(SettleSeconds);
        }

        /** Moves the item's piece through the screen's controls, as the player's handling of it does. */
        private static IEnumerator Move(SpiritEncounterController controller, ItemDefinition item, PiecePosition position)
        {
            var piece = item.Piece;
            controller.InteractWithPiece(piece.Identity, piece.IndexOf<PositionInteractionDefinition>(), new MovePieceInput(position));
            yield return ContentReady.Idle(controller);
            var state = controller.Snapshot.PieceInteractions.Single(interaction => interaction.PieceId == piece.Identity).State;
            Assert.That(((PiecePositionState)state).Position, Is.EqualTo(position), "The piece did not move.");
            yield return new WaitForSecondsRealtime(SettleSeconds);
        }

        private static List<SpiritBayIndicator> ShownIndicators(SpiritEncounterController controller)
            => ContentReady.EncounterView(controller).TrayScene.GetComponentsInChildren<SpiritBayIndicator>(false).ToList();

        private static IEnumerator Capture(string root, string name)
            => NarrowCropReviewCapturePlayTests.Capture(root, name + "-" + NarrowCropReviewCapturePlayTests.Width + "x" +
                NarrowCropReviewCapturePlayTests.Height + ".png");
    }
}
