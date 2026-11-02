using System;
using System.Collections;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dice.Tests
{
    /**
     * Review capture of the current rank at a phone-sized portrait screen: the encounter table during a hand, with the
     * HUD naming the current rank of the dice as they lie, and the hand ranks panel opened over it as its footer button
     * does, marking that rank's row. Each is written as a PNG under DICE_SPIRIT_CAPTURE_ROOT. It is ignored without that
     * variable; it is a capture for review, not a layout test.
     */
    [Explicit("Review capture; run it explicitly with DICE_SPIRIT_CAPTURE_ROOT set.")]
    [Category("Capture")]
    public sealed class HandRanksReviewCapturePlayTests
    {
        private static readonly Vector2Int PhoneSize = new Vector2Int(1080, 2400);
        private const string GameViewSizeName = "Hand Ranks Review";

        [UnityTest]
        public IEnumerator CurrentRankReviewCapture()
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Review capture only; set DICE_SPIRIT_CAPTURE_ROOT to write it.");
            root = Path.Combine(root, "hand-ranks-review");
            Assert.That(Directory.Exists(root), Is.False, "Capture output must be fresh.");
            Directory.CreateDirectory(root);

            // The screen takes the phone size before the encounter screen is built, as on a device.
            yield return ResizeScreen(PhoneSize);
            yield return ContentReady.LoadApplication();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            // Any encounter journey shows the HUD and the panel; this is the first found.
            var journey = controller.JourneyCatalog.Entries.First(entry => entry != null && entry.Encounters.Any());
            yield return ContentReady.EnterEncounter(controller, journey, ContentReady.Seed);
            var view = ContentReady.EncounterView(controller);
            Assert.That(controller.Preview, Is.Not.Null, journey.name + " has no hand preview after its opening roll.");
            Debug.Log("Hand ranks review capture: current rank " + controller.Preview.Score.Category);

            yield return Capture(root, "a-hud-current-rank-" + PhoneSize.x + "x" + PhoneSize.y + ".png");

            view.ShowHandRanks();
            Assert.That(view.IsHandRanksOpen, Is.True, "The hand ranks panel did not open.");
            yield return Capture(root, "b-hand-ranks-current-row-" + PhoneSize.x + "x" + PhoneSize.y + ".png");
            view.ShowTable();
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

        private static IEnumerator Capture(string root, string filename)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(root, filename);
            Assert.That(File.Exists(path), Is.False, "Capture output must be fresh.");
            var pixels = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(path, pixels.EncodeToPNG()); }
            finally { UnityEngine.Object.Destroy(pixels); }
            TestContext.WriteLine("Hand ranks review capture: " + path);
        }
    }
}
