using System.Collections;
using System.Linq;
using System.Reflection;
using Dice.App;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dice.Tests
{
    public sealed class SpiritApplicationPresentationPlayTests
    {
        [UnityTest]
        public IEnumerator SingleSceneReplacementReleasesTheRoutedSceneOnce()
        {
            yield return SceneManager.LoadSceneAsync("Application", LoadSceneMode.Single);
            yield return ContentReady.Spirit(false);
            var controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            var router = ContentReady.Router(controller);
            var frontendHandle = SceneHandle(router);
            Assert.That(frontendHandle.IsValid(), Is.True);
            var journey = controller.JourneyCatalog.Entries.Single(entry =>
                entry.EncounterId == "encounter.owl.watchtower");
            ContentReady.SeedJourneyPrerequisites(controller, journey.Id);
            controller.SelectEncounter(journey.Id);
            yield return ContentReady.Routed(controller);
            Assert.That(frontendHandle.IsValid(), Is.False,
                "A routed transition must release its previous presentation handle.");

            var encounterHandle = SceneHandle(router);
            var encounterScene = encounterHandle.Result.Scene;
            Assert.That(encounterScene.isLoaded, Is.True);
            Assert.That(encounterScene.name, Is.EqualTo("WatchtowerEncounter"));
            yield return SceneManager.LoadSceneAsync("Application", LoadSceneMode.Single);
            yield return ContentReady.Spirit(false);
            float deadline = Time.realtimeSinceStartup + 10;
            while (encounterHandle.IsValid() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(router == null, Is.True, "Single scene loading must destroy the previous router.");
            Assert.That(encounterScene.isLoaded, Is.False);
            Assert.That(encounterHandle.IsValid(), Is.False,
                "Application teardown must release its presentation handle after Unity unloads the scene.");
            Assert.That(ContentReady.Router().IsReady, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        private static AsyncOperationHandle<SceneInstance> SceneHandle(SpiritApplicationPresentation router)
        {
            return (AsyncOperationHandle<SceneInstance>)typeof(SpiritApplicationPresentation)
                .GetField("scene", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(router);
        }
    }
}
