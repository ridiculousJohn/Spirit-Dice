using System.Collections;
using System.Linq;
using Dice.App;
using NUnit.Framework;
using UnityEngine;
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
            var frontendHandle = router.SceneHandle;
            Assert.That(frontendHandle.IsValid(), Is.True);
            var frontendScene = frontendHandle.Result.Scene;
            // Any encounter journey routes from the frontend scene to its own encounter scene.
            var journey = controller.JourneyCatalog.Entries.FirstOrDefault(entry => entry != null && !entry.IsStoryOnly);
            Assert.That(journey, Is.Not.Null, "No encounter journey is authored.");
            ContentReady.SeedJourneyPrerequisites(controller, journey);
            controller.SelectEncounter(journey);
            yield return ContentReady.Routed(controller);
            Assert.That(frontendHandle.IsValid(), Is.False,
                "A routed transition must release its previous presentation handle.");

            var encounterHandle = router.SceneHandle;
            var encounterScene = encounterHandle.Result.Scene;
            Assert.That(encounterScene.isLoaded, Is.True);
            Assert.That(encounterScene, Is.Not.EqualTo(frontendScene), "The journey must route to its encounter scene.");
            Assert.That(ContentReady.EncounterView(controller).gameObject.scene, Is.EqualTo(encounterScene),
                "The routed scene holds the encounter view.");
            yield return SceneManager.LoadSceneAsync("Application", LoadSceneMode.Single);
            yield return ContentReady.Spirit(false);
            yield return ContentReady.WaitUntil(() => !encounterHandle.IsValid(),
                "Application teardown must release its presentation handle after Unity unloads the scene.");
            Assert.That(router == null, Is.True, "Single scene loading must destroy the previous router.");
            Assert.That(encounterScene.isLoaded, Is.False);
            Assert.That(ContentReady.Router().IsReady, Is.True);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
