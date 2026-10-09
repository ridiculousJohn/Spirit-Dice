using System;
using System.Collections;
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
        internal static IEnumerator Spirit(bool enterEncounter = true)
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
            string savePath = SpiritSavePath();
            Assert.That(controller.InitializeJourney(controller.Catalog, controller.JourneyCatalog, savePath), Is.True,
                controller.StatusMessage);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.Snapshot, Is.Null);
            if (!enterEncounter) yield break;
            controller.SelectEncounter(controller.JourneyCatalog.Entries[0].EncounterId);
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                controller.AdvanceJourney();
            controller.AcceptEncounter();
            deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False, "Opening encounter presentation did not complete.");
            Assert.That(controller.Snapshot, Is.Not.Null, controller.StatusMessage);
        }

        internal static string SpiritSavePath() => Path.GetFullPath(Path.Combine(Application.dataPath,
            "..", "..", "Docs", "work", "journey-file-tests", Guid.NewGuid().ToString("N"), "encounter.json"));

        internal static IEnumerator Diagnostic()
        {
            var controller = UnityEngine.Object.FindAnyObjectByType<DiagnosticEncounterController>();
            Assert.That(controller, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.Snapshot == null && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(controller.StatusMessage, Does.Not.StartWith("Cannot"));
                yield return null;
            }
            Assert.That(controller.Snapshot, Is.Not.Null, controller.StatusMessage);
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
            if (scene.name != "SpiritHousePrototype") return;
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            if (controller == null) return;
            // sceneLoaded runs after Awake and before Start loads the persistent profile.
            var setter = typeof(SpiritEncounterController).GetProperty(nameof(SpiritEncounterController.SavePath))
                .GetSetMethod(true);
            setter.Invoke(controller, new object[] { ContentReady.SpiritSavePath() });
        }
    }
}
