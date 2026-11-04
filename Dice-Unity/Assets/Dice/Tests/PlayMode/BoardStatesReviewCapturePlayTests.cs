using System.Collections;
using System.Linq;
using System.Text;
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
    /**
     * Review capture of every encounter table's board, the dice tray and the HUD around it, at the 540 x 960 authoring
     * width and the narrow 443 x 960 width: after the opening roll, with two dice selected, and with ready Play Hand charms
     * in the tray's charm row. No authored encounter starts with a charm, so the charm row is
     * captured in a second pass over a copy of the catalog in which every encounter starts with every starter Play Hand
     * charm; the opening dice decide which of them are ready. It writes PNGs under DICE_SPIRIT_CAPTURE_ROOT/board-states,
     * with each capture's dice touch targets and dice bays in portrait pixels in the test log, and is ignored without that
     * variable; it is a capture, not a regression test. The fixture is explicit: it runs only when a run selects it by name.
     */
    [Explicit("Review capture tool; select it by name with DICE_SPIRIT_CAPTURE_ROOT set.")]
    [Category("Capture")]
    public sealed class BoardStatesReviewCapturePlayTests
    {
        // Real seconds the table rests after its opening roll before it is captured, so the host has settled into idle.
        private const float TableSettleSeconds = 1f;
        // The two dice the selection capture selects.
        private static readonly int[] SelectedBays = { 1, 2 };

        [UnityTest]
        public IEnumerator BoardStatesAtBothWidthsCapture()
        {
            string root = NarrowCropReviewCapturePlayTests.Fresh("board-states");
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.JourneyAtBoard();
            var controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            var journeys = controller.JourneyCatalog.Entries.Where(journey => journey != null && journey.Encounters.Any()).ToArray();

            foreach (var journey in journeys)
            {
                yield return ContentReady.EnterEncounter(controller, journey, ContentReady.Seed);
                yield return new WaitForSecondsRealtime(TableSettleSeconds);
                string name = TableName(journey);
                yield return CaptureBothWidths(root, name + "-opening");
                foreach (int bay in SelectedBays) controller.ToggleDie(bay);
                yield return ContentReady.Idle(controller);
                Assert.That(controller.SelectedMask, Is.EqualTo(SelectedBays.Sum(bay => 1 << bay)), name + " selection");
                yield return CaptureBothWidths(root, name + "-selected");
            }

            // A run keeps the catalog it started with, so the copy starts in a fresh application with a fresh save.
            yield return ContentReady.LoadApplication();
            controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            var authored = controller.Catalog;
            var starters = authored.Modifiers.Where(modifier => modifier.Availability.Access == ModifierAccess.Starter &&
                modifier.Phase == ModifierPhase.PlayHand).Select(modifier => modifier.Identity).ToArray();
            Assert.That(starters, Is.Not.Empty, "The authored catalog has no starter Play Hand charm.");
            var equipped = new MechanicalCatalog(authored.Version + ".board-states-review", authored.Hands,
                authored.Encounters.Select(encounter => WithStartingCharms(encounter, starters)), authored.Modifiers);
            Assert.That(controller.InitializeJourney(equipped, controller.JourneyCatalog, ContentReady.SpiritSavePath()), Is.True,
                controller.JourneyStatus.ToString());
            foreach (var journey in journeys)
            {
                yield return ContentReady.EnterEncounter(controller, journey, ContentReady.Seed);
                yield return new WaitForSecondsRealtime(TableSettleSeconds);
                var view = ContentReady.EncounterView(controller);
                int ready = view.TrayScene.HistoryRenderer.Count;
                TestContext.WriteLine(TableName(journey) + ": " + ready + " ready Play Hand charms in the charm row.");
                yield return CaptureBothWidths(root, TableName(journey) + "-charm-row");
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static string TableName(EncounterJourneyAsset journey)
            => journey.TableSceneAt(journey.IndexOfEncounter(journey.Encounters.First().Identity)).name;

        private static IEnumerator CaptureBothWidths(string root, string name)
        {
            foreach (int width in new[] { NarrowCropReviewCapturePlayTests.Width, NarrowCropReviewCapturePlayTests.NarrowWidth })
            {
                string file = name + "-" + width + "x" + NarrowCropReviewCapturePlayTests.Height + ".png";
                yield return NarrowCropReviewCapturePlayTests.Capture(root, file, width);
                LogBoard(file, width);
            }
        }

        /**
         * Logs, in portrait pixels from the top at the captured width, each die's touch target and the tray's projection of
         * its die, which the touch target follows, and the HUD's tray frame.
         */
        private static void LogBoard(string file, int width)
        {
            var view = ContentReady.EncounterView();
            var portrait = view.Portrait;
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(width, NarrowCropReviewCapturePlayTests.Height);
            Canvas.ForceUpdateCanvases();
            var report = new StringBuilder(file + ":");
            var image = view.GameplayHud.DiceTrayImage.rectTransform;
            Rect frame = InPortrait(portrait, image);
            report.Append(" tray image " + Describe(frame));
            for (int bay = 0; bay < DiceValues.Count; ++bay)
            {
                var button = (RectTransform)ContentReady.Find<Button>(null, "DieButton" + (bay + 1)).transform;
                Rect slot = view.TrayScene.DiceTray.GetSlotViewportRect(bay);
                Rect projected = Rect.MinMaxRect(frame.xMin + slot.xMin * frame.width, frame.yMin + slot.yMin * frame.height,
                    frame.xMin + slot.xMax * frame.width, frame.yMin + slot.yMax * frame.height);
                report.Append("; die " + bay + " target " + Describe(InPortrait(portrait, button)) + " die " + Describe(projected));
            }
            TestContext.WriteLine(report.ToString());
        }

        private static Rect InPortrait(RectTransform portrait, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
            foreach (var corner in corners)
            {
                Vector2 local = (Vector2)portrait.InverseTransformPoint(corner) - portrait.rect.min;
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static string Describe(Rect rect)
        {
            float height = NarrowCropReviewCapturePlayTests.Height;
            return string.Format("x {0:F1}-{1:F1} y {2:F1}-{3:F1}", rect.xMin, rect.xMax, height - rect.yMax, height - rect.yMin);
        }

        /** The encounter as authored, starting with the given charms. */
        private static EncounterDefinition WithStartingCharms(EncounterDefinition definition, ModifierDefinition.Id[] charms)
            => new EncounterDefinition(definition.Identity, definition.Stages, definition.StartingUpgrades, charms,
                definition.RewardPolicy, definition.Rules, definition.UnlockMilestones, definition.EncounterModifierIds,
                definition.UnlockRewardModifierIds, definition.Pieces, definition.EncounterModifiersFirstClearedStageNumber);
    }
}
