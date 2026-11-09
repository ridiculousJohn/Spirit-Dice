using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dice.Tests
{
    /**
     * Review capture of how every scene frames its art at the 540 x 960 authoring width and the narrow 443 x 960 width.
     * For each journey of the catalog, before its first invitation, it shows each picture scene (at its first beat that
     * does not advance itself) and the table beat that follows a picture, and captures each at both widths; then it plays
     * each journey's encounter to its opening roll and captures the table with its host at both widths. It writes PNGs
     * under DICE_SPIRIT_CAPTURE_ROOT/pictures and DICE_SPIRIT_CAPTURE_ROOT/tables and is ignored without it; it is a
     * capture, not a regression test. A second capture shows each journey's beats up to its invitation at both widths, for
     * the narration panel over each scene, under DICE_SPIRIT_CAPTURE_ROOT/beats. The fixture is explicit: it runs only when a run selects it by name, never in a run
     * of the whole suite.
     */
    [Explicit("Review capture tool; select it by name with DICE_SPIRIT_CAPTURE_ROOT set.")]
    [Category("Capture")]
    public sealed class NarrowCropReviewCapturePlayTests
    {
        internal const int Width = PortraitCapture.Width, NarrowWidth = 443, Height = PortraitCapture.Height;
        // Real seconds the table rests after its opening roll before it is captured, so the host has settled into idle.
        private const float TableSettleSeconds = 1f;

        [UnityTest]
        public IEnumerator PicturesAndTablesAtBothWidthsCapture()
        {
            string pictures = Fresh("pictures");
            string tables = Fresh("tables");
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.JourneyAtBoard();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journeys = controller.JourneyCatalog.Entries.Where(journey => journey != null).ToArray();

            foreach (var journey in journeys)
            {
                var beats = PictureAndFollowingTableBeats(journey);
                if (beats.Count == 0) continue;
                controller.ReturnToInvestigation();
                yield return ContentReady.Routed(controller);
                ContentReady.SeedJourneyPrerequisites(controller, journey);
                controller.SelectEncounter(journey);
                yield return ContentReady.Routed(controller);
                foreach (int index in beats)
                {
                    var view = ContentReady.EncounterView(controller);
                    yield return ShowBeat(controller, view, index);
                    string kind = journey.SceneAt(index) is PictureSceneAsset picture ? picture.name : journey.TableSceneAt(index).name;
                    string name = journey.name + "-beat" + index + "-" + kind;
                    yield return Capture(pictures, name + "-" + Width + "x" + Height + ".png");
                    yield return Capture(pictures, name + "-" + NarrowWidth + "x" + Height + ".png", NarrowWidth);
                }
            }

            foreach (var journey in journeys.Where(journey => journey.Encounters.Any()))
            {
                yield return ContentReady.EnterEncounter(controller, journey, ContentReady.Seed);
                yield return new WaitForSecondsRealtime(TableSettleSeconds);
                string name = journey.name + "-" + journey.TableSceneAt(journey.IndexOfEncounter(journey.Encounters.First().Identity)).name;
                yield return Capture(tables, name + "-" + Width + "x" + Height + ".png");
                yield return Capture(tables, name + "-" + NarrowWidth + "x" + Height + ".png", NarrowWidth);
            }
            LogAssert.NoUnexpectedReceived();
        }

        /**
         * Review capture of the journey screen's narration at both widths: for each journey of the catalog, every beat up to
         * and including its first invitation (or every beat of a journey without one) that does not advance itself, shown
         * with its motion complete. It writes PNGs under DICE_SPIRIT_CAPTURE_ROOT/beats.
         */
        [UnityTest]
        public IEnumerator JourneyBeatsToInvitationAtBothWidthsCapture()
        {
            string beats = Fresh("beats");
            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.JourneyAtBoard();
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journeys = controller.JourneyCatalog.Entries.Where(journey => journey != null).ToArray();

            foreach (var journey in journeys)
            {
                int end = 0;
                while (end < journey.Beats.Count - 1 && !(journey.Beats[end] is InvitationBeat)) ++end;
                controller.ReturnToInvestigation();
                yield return ContentReady.Routed(controller);
                ContentReady.SeedJourneyPrerequisites(controller, journey);
                controller.SelectEncounter(journey);
                yield return ContentReady.Routed(controller);
                for (int index = 0; index <= end; ++index)
                {
                    if (journey.Beats[index].AdvancesItself) continue;
                    yield return ShowBeat(controller, ContentReady.EncounterView(controller), index);
                    string name = journey.name + "-beat" + index.ToString("00") + "-" + journey.Beats[index].GetType().Name +
                        "-" + journey.SceneAt(index).name;
                    yield return Capture(beats, name + "-" + Width + "x" + Height + ".png");
                    yield return Capture(beats, name + "-" + NarrowWidth + "x" + Height + ".png", NarrowWidth);
                }
            }
            LogAssert.NoUnexpectedReceived();
        }

        /**
         * Before the journey's first invitation: for each run of beats in one picture scene, its first beat that does not
         * advance itself, and when a table scene follows the run, that run's first such beat.
         */
        private static List<int> PictureAndFollowingTableBeats(EncounterJourneyAsset journey)
        {
            var beats = new List<int>();
            int end = 0;
            while (end < journey.Beats.Count && !(journey.Beats[end] is InvitationBeat)) ++end;
            for (int index = 0; index < end; ++index)
            {
                var scene = journey.SceneAt(index);
                bool entersScene = index == 0 || journey.SceneAt(index - 1) != scene;
                if (!entersScene) continue;
                bool afterPicture = index > 0 && journey.SceneAt(index - 1) is PictureSceneAsset;
                if (!(scene is PictureSceneAsset) && !afterPicture) continue;
                for (int shown = index; shown < end && journey.SceneAt(shown) == scene; ++shown)
                {
                    if (journey.Beats[shown].AdvancesItself) continue;
                    beats.Add(shown);
                    break;
                }
            }
            return beats;
        }

        internal static string Fresh(string folder)
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Review capture only; set DICE_SPIRIT_CAPTURE_ROOT to write it.");
            root = Path.Combine(root, folder);
            Assert.That(Directory.Exists(root), Is.False, "Capture output must be fresh.");
            Directory.CreateDirectory(root);
            return root;
        }

        /**
         * Advances the journey to the beat at the index and waits until the view shows it with its motion complete and its
         * narration panel at its fitted rectangle.
         */
        private static IEnumerator ShowBeat(SpiritEncounterController controller, SpiritEncounterView view, int index)
        {
            Assert.That(index, Is.InRange(0, controller.SelectedJourney.Beats.Count - 1));
            string id = controller.SelectedJourney.name + " beat " + index;
            float deadline = Time.realtimeSinceStartup + 15;
            while (controller.BeatIndex != index && Time.realtimeSinceStartup < deadline)
            {
                controller.AdvanceJourney();
                yield return null;
            }
            Assert.That(controller.BeatIndex, Is.EqualTo(index), id);
            yield return ContentReady.Routed(controller);
            view = ContentReady.EncounterView(controller);
            deadline = Time.realtimeSinceStartup + 15;
            while ((view.JourneyView.VisibleBeatIndex != controller.BeatIndex || view.JourneyView.IsAnimating ||
                view.JourneyView.IsEasingNarrative) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(view.JourneyView.VisibleBeatIndex, Is.EqualTo(controller.BeatIndex), id);
            Assert.That(view.JourneyView.IsAnimating, Is.False, id);
            Assert.That(view.JourneyView.IsEasingNarrative, Is.False, id);
            yield return null;
        }

        /** Renders the portrait canvas at the given width (540 x 960 by default) through a capture camera and writes it as a PNG. */
        internal static IEnumerator Capture(string root, string filename, int width = Width)
        {
            string path = Path.Combine(root, filename);
            PortraitCapture.FitPortrait(width);
            yield return PortraitCapture.Still(path, width, 1);
            TestContext.WriteLine("Narrow crop review capture: " + path);
        }
    }
}
