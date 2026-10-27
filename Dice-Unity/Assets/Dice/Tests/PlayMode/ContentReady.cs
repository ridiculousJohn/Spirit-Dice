using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Dice.App;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dice.Tests
{
    internal static class ContentReady
    {
        /**
         * Loads the authored content and, when entering an encounter, opens the first encounter journey in catalog order
         * that a fresh profile can play without prerequisites and that has the property the caller requires.
         */
        internal static IEnumerator Spirit(bool enterEncounter = true, Func<EncounterJourneyAsset, bool> requirement = null)
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            Assert.That(controller, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 30;
            while ((controller.Catalog == null || controller.JourneyCatalog == null) &&
                Time.realtimeSinceStartup < deadline)
            {
                Assert.That(controller.StatusMessage, Does.Not.StartWith("Cannot"));
                yield return null;
            }
            Assert.That(controller.Catalog, Is.Not.Null, controller.StatusMessage);
            Assert.That(controller.JourneyCatalog, Is.Not.Null, controller.StatusMessage);
            yield return Routed(controller);
            string savePath = SpiritSavePath();
            Assert.That(controller.InitializeJourney(controller.Catalog, controller.JourneyCatalog, savePath), Is.True,
                controller.StatusMessage);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.Snapshot, Is.Null);
            if (!enterEncounter) { yield return Routed(controller); yield break; }
            EncounterJourneyAsset journey = null;
            foreach (var entry in controller.JourneyCatalog.Entries)
                if (entry != null && !entry.IsStoryOnly && controller.IsJourneyAvailable(entry.Id)
                    && (requirement == null || requirement(entry)))
                {
                    journey = entry;
                    break;
                }
            Assert.That(journey, Is.Not.Null, requirement == null
                ? "The authored journey catalog has no encounter journey available without prerequisites."
                : "The authored journey catalog has no encounter journey available without prerequisites with the required property.");
            controller.SelectEncounter(journey.EncounterId);
            yield return Routed(controller);
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                controller.AdvanceJourney();
            controller.AcceptEncounter();
            yield return Routed(controller);
            deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False, "Opening encounter presentation did not complete.");
            Assert.That(controller.Snapshot, Is.Not.Null, controller.StatusMessage);
        }

        /**
         * The application's charm presentation: the authored skin's, loaded with its content, or the test skin a test
         * installed.
         */
        internal static ModifierPresentationCatalog Charms
        {
            get
            {
                var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.ModifierPresentation, Is.Not.Null, "The application has not loaded its charm presentation.");
                return controller.ModifierPresentation;
            }
        }

        internal static SpiritApplicationPresentation Router(SpiritEncounterController controller = null)
        {
            if (controller == null) controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            Assert.That(controller, Is.Not.Null);
            var router = controller.GetComponent<SpiritApplicationPresentation>();
            Assert.That(router, Is.Not.Null, "Application requires its authored presentation router.");
            return router;
        }

        internal static SpiritEncounterView EncounterView(SpiritEncounterController controller = null) => Router(controller).EncounterView;
        internal static SpiritFrontendView FrontendView(SpiritEncounterController controller = null) => Router(controller).FrontendView;

        internal static Component PresentationRoot(SpiritEncounterController controller = null)
        {
            var router = Router(controller);
            Assert.That(router.PresentationRoot, Is.Not.Null);
            return router.PresentationRoot.transform;
        }

        internal static RectTransform Portrait(SpiritEncounterController controller = null)
        {
            var router = Router(controller);
            var portrait = router.EncounterView != null ? router.EncounterView.Portrait : router.FrontendView.Portrait;
            Assert.That(portrait, Is.Not.Null);
            return portrait;
        }

        internal static IEnumerator Routed(SpiritEncounterController controller = null)
        {
            var router = Router(controller);
            float deadline = Time.realtimeSinceStartup + 30;
            while (!router.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(router.IsReady, Is.True, "The authored presentation scene did not become ready.");
        }

        internal static Transform Die(SpiritDiceTray tray, int index)
        {
            var field = typeof(SpiritDiceTray).GetField("dice",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var dice = (Transform[])field.GetValue(tray);
            Assert.That(dice, Has.Length.EqualTo(Dice.Engine.DiceValues.Count));
            Assert.That(dice[index], Is.Not.Null, "Authored die " + index);
            return dice[index];
        }

        internal static string SpiritSavePath() => Path.GetFullPath(Path.Combine(Application.dataPath,
            "..", "..", "Docs", "work", "journey-file-tests", Guid.NewGuid().ToString("N"), "encounter.json"));

        internal static void SeedJourneyPrerequisites(SpiritEncounterController controller, string journeyId)
        {
            string workRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work")) +
                Path.DirectorySeparatorChar;
            Assert.That(Path.GetFullPath(controller.ProfilePath).StartsWith(workRoot, StringComparison.OrdinalIgnoreCase),
                Is.True, "Journey prerequisites require an isolated test profile.");
            var journey = controller.JourneyCatalog.Find(journeyId);
            Assert.That(journey, Is.Not.Null, journeyId);
            var profile = PlayerProfileFileStore.LoadOrCreate(controller.ProfilePath);
            SeedPrerequisites(profile, controller.JourneyCatalog, journey, new HashSet<string>(StringComparer.Ordinal));
            PlayerProfileFileStore.Save(controller.ProfilePath, profile);
            Assert.That(controller.InitializeJourney(controller.Catalog, controller.JourneyCatalog, controller.SavePath),
                Is.True, controller.JourneyStatus);
            Assert.That(controller.IsJourneyAvailable(journeyId), Is.True, journeyId);
        }

        private static void SeedPrerequisites(PlayerProfile profile, EncounterJourneyCatalogAsset journeys,
            EncounterJourneyAsset journey, HashSet<string> visited)
        {
            if (!visited.Add(journey.Id)) return;
            foreach (string id in journey.RequiredCompletedEncounterIds)
            {
                var required = journeys.Find(id);
                if (required != null) SeedPrerequisites(profile, journeys, required, visited);
                profile.CompleteEncounter(id);
            }
            foreach (string id in journey.RequiredCompletedStoryIds)
            {
                var required = journeys.Find(id);
                Assert.That(required, Is.Not.Null, id);
                SeedPrerequisites(profile, journeys, required, visited);
                profile.CompleteStory(id);
            }
        }
    }

    [SetUpFixture]
    public sealed class SpiritTestProfileIsolation
    {
        [OneTimeSetUp]
        public void IsolateSceneProfiles() => SceneManager.sceneLoaded += IsolateProfile;

        [OneTimeTearDown]
        public void StopIsolatingSceneProfiles() => SceneManager.sceneLoaded -= IsolateProfile;

        private static void IsolateProfile(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Application") return;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            if (controller == null) return;
            // sceneLoaded runs after Awake and before Start loads the persistent profile.
            var setter = typeof(SpiritEncounterController).GetProperty(nameof(SpiritEncounterController.SavePath))
                .GetSetMethod(true);
            setter.Invoke(controller, new object[] { ContentReady.SpiritSavePath() });
        }
    }
}
