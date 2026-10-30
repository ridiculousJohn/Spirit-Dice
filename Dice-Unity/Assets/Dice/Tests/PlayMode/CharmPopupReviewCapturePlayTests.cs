using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dice.Tests
{
    /**
     * Review capture of the charm popup over the encounter table at a wide and a narrow portrait screen. The game view is
     * set to each size so the encounter screen fits its portrait column to that screen as on a device, the popup is opened
     * on an equipped charm as tapping its tile does, and the whole screen is written as a PNG under DICE_SPIRIT_CAPTURE_ROOT
     * with the portrait width, the popup panel width and their ratio beside it. It is ignored without that variable; it is
     * a capture for review, not a layout test.
     */
    [Explicit("Review capture; run it explicitly with DICE_SPIRIT_CAPTURE_ROOT set.")]
    [Category("Capture")]
    public sealed class CharmPopupReviewCapturePlayTests
    {
        // A wide portrait screen keeps the full 540-unit column; a narrow one narrows it to fit the screen at its height.
        private static readonly Vector2Int[] ScreenSizes = { new Vector2Int(1440, 1920), new Vector2Int(1080, 2400) };
        private const string GameViewSizeName = "Charm Popup Review";
        private const string MeasurementsFile = "charm-popup-measurements.txt";

        [UnityTest]
        public IEnumerator CharmPopupWidthReviewCapture()
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Review capture only; set DICE_SPIRIT_CAPTURE_ROOT to write it.");
            root = Path.Combine(root, "charm-popup-review");
            Assert.That(Directory.Exists(root), Is.False, "Capture output must be fresh.");
            Directory.CreateDirectory(root);

            yield return ContentReady.LoadApplication();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            // No authored encounter starts with a charm, so the first journey's encounter starts with the first starter
            // charm in a copy of the catalog; the popup inspects it as it would a charm taken from an offer.
            var journey = controller.JourneyCatalog.Entries.First(entry => entry != null && entry.Encounters.Any());
            var starter = controller.Catalog.Modifiers.FirstOrDefault(modifier => modifier.Availability.Access == ModifierAccess.Starter);
            Assert.That(starter, Is.Not.Null, "The authored catalog has no starter charm to equip.");
            Assert.That(controller.InitializeJourney(WithStartingCharm(controller.Catalog, journey.Encounters.First().Identity,
                starter.Identity), controller.JourneyCatalog, controller.SavePath), Is.True, controller.JourneyStatus.ToString());
            yield return ContentReady.EnterEncounter(controller, journey, ContentReady.Seed);
            var view = ContentReady.EncounterView(controller);
            Assert.That(controller.Snapshot.Modifiers, Is.Not.Empty, journey.name + " has no equipped charm to inspect.");
            var charm = controller.Snapshot.Modifiers[0];

            var report = new System.Text.StringBuilder();
            foreach (var size in ScreenSizes)
            {
                view.CloseCharmPopup();
                yield return ResizeScreen(size);
                // The view fits its portrait to the screen in Update; the popup sizes its panel from it when shown.
                yield return null;
                view.ShowCharmPopup(charm.InstanceId);
                Assert.That(view.IsCharmPopupOpen, Is.True, "The charm popup did not open on " + charm.DefinitionId);
                Assert.That(view.CharmPopup.InstanceId, Is.EqualTo(charm.InstanceId));
                yield return null;
                yield return new WaitForEndOfFrame();

                string name = "charm-popup-" + size.x + "x" + size.y;
                float portraitWidth = view.Portrait.rect.width, panelWidth = view.CharmPopup.Panel.rect.width;
                string line = string.Format(CultureInfo.InvariantCulture,
                    "{0}: screen {1}x{2}, portrait width {3:F2}, panel width {4:F2}, ratio {5:F4}",
                    name, Screen.width, Screen.height, portraitWidth, panelWidth, panelWidth / portraitWidth);
                Debug.Log("Charm popup review capture " + line);
                report.AppendLine(line);
                var pixels = ScreenCapture.CaptureScreenshotAsTexture();
                try { Write(root, name + ".png", pixels.EncodeToPNG()); }
                finally { UnityEngine.Object.Destroy(pixels); }
            }
            view.CloseCharmPopup();
            Write(root, MeasurementsFile, System.Text.Encoding.ASCII.GetBytes(report.ToString()));
        }

        /** A copy of the catalog in which the encounter starts with the given charm and is otherwise as authored. */
        private static MechanicalCatalog WithStartingCharm(MechanicalCatalog authored, EncounterDefinition.Id encounterId,
            ModifierDefinition.Id charmId)
        {
            var definition = authored.GetEncounter(encounterId);
            var equipped = new EncounterDefinition(definition.Identity, definition.Stages, definition.StartingUpgrades,
                new[] { charmId }, definition.RewardPolicy, definition.Rules, definition.UnlockMilestones,
                definition.EncounterModifierIds, definition.UnlockRewardModifierIds, definition.Pieces,
                definition.EncounterModifiersFirstClearedStageNumber);
            return new MechanicalCatalog(authored.Version + ".charm-popup-review", authored.Hands,
                authored.Encounters.Select(value => value.Identity == definition.Identity ? equipped : value), authored.Modifiers);
        }

        /** Sets the game view to render at the given size and waits until the player screen reports it. */
        private static IEnumerator ResizeScreen(Vector2Int size)
        {
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)size.x, (uint)size.y, GameViewSizeName);
#endif
            yield return ContentReady.WaitUntil(() => Screen.width == size.x && Screen.height == size.y,
                "The game view did not take the size " + size.x + "x" + size.y + ".");
        }

        private static void Write(string root, string filename, byte[] bytes)
        {
            string path = Path.Combine(root, filename);
            Assert.That(File.Exists(path), Is.False, "Capture output must be fresh.");
            File.WriteAllBytes(path, bytes);
            TestContext.WriteLine("Charm popup review capture: " + path);
        }
    }
}
