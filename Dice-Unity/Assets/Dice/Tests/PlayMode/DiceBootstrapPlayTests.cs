using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class DiceBootstrapPlayTests
    {
        private AsyncOperationHandle<DiceApplicationConfig> authoredHandle;
        private IResourceLocator[] originalLocators;
        private ResourceLocationMap testLocator;
        private PendingConfigurationProvider provider;
        private GameObject host;
        private SpiritEncounterController controller;
        private DiceBootstrap bootstrap;
        private Action<AsyncOperationHandle, Exception> previousExceptionHandler;
        private readonly List<Exception> expectedFailures = new List<Exception>();
        private string failureKey;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            authoredHandle = Addressables.LoadAssetAsync<DiceApplicationConfig>(DiceBootstrap.ConfigurationAddress);
            yield return ContentReady.WaitUntil(() => authoredHandle.IsDone, "Authored configuration did not load.");
            Assert.That(authoredHandle.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
            Assert.That(authoredHandle.Result, Is.Not.Null);
            previousExceptionHandler = ResourceManager.ExceptionHandler;
            expectedFailures.Clear();
            failureKey = "dice.tests.bootstrap." + Guid.NewGuid().ToString("N");
            ResourceManager.ExceptionHandler = (operation, exception) =>
            {
                if (exception.ToString().Contains(failureKey)) expectedFailures.Add(exception);
                else previousExceptionHandler?.Invoke(operation, exception);
            };
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var presentationScene = default(Scene);
            if (host != null)
            {
                var router = host.GetComponent<SpiritApplicationPresentation>();
                if (router != null && router.PresentationRoot != null)
                    presentationScene = router.PresentationRoot.scene;
            }
            if (host != null) UnityEngine.Object.Destroy(host);
            yield return null;
            if (presentationScene.IsValid())
                yield return ContentReady.WaitUntil(() => !presentationScene.IsValid() || !presentationScene.isLoaded,
                    "Destroying the application did not unload its owned presentation scene.");
            if (provider != null && provider.IsPending)
            {
                provider.Complete(authoredHandle.Result);
                yield return null;
            }
            if (testLocator != null) Addressables.RemoveResourceLocator(testLocator);
            RestoreOriginalLocators();
            if (provider != null) Addressables.ResourceManager.ResourceProviders.Remove(provider);
            if (authoredHandle.IsValid()) Addressables.Release(authoredHandle);
            ResourceManager.ExceptionHandler = previousExceptionHandler;
            host = null;
            controller = null;
            bootstrap = null;
            provider = null;
            testLocator = null;
            originalLocators = null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PendingStartupDefersPresentationUntilContentIsReady()
        {
            InstallPendingConfiguration();
            bool observedSceneLoad = false;
            bool firstStartupVisible = false;
            bool firstPresentationReady = true;
            bool firstGameHierarchyExists = true;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> onLoaded = (scene, mode) =>
            {
                var sceneController = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
                var sceneBootstrap = sceneController.GetComponent<DiceBootstrap>();
                observedSceneLoad = true;
                firstStartupVisible = sceneBootstrap.StartupView.Root.gameObject.activeInHierarchy;
                firstPresentationReady = sceneController.PresentationReady;
                firstGameHierarchyExists = sceneController.GetComponent<SpiritApplicationPresentation>().EncounterView != null;
            };
            SceneManager.sceneLoaded += onLoaded;
            try { yield return CreateHost(); }
            finally { SceneManager.sceneLoaded -= onLoaded; }
            Assert.That(observedSceneLoad, Is.True);
            Assert.That(firstStartupVisible, Is.True, "Startup must have its own surface before content loads.");
            Assert.That(firstPresentationReady, Is.False);
            Assert.That(firstGameHierarchyExists, Is.False, "Awake must not construct gameplay presentation.");
            yield return ContentReady.WaitUntil(() => provider.IsPending, "Bootstrap did not request configuration.");
            AssertStartupSurface();
            yield return new WaitForEndOfFrame();
            AssertStartupSurface();
            RestoreOriginalLocators();
            provider.Complete(authoredHandle.Result);
            yield return ContentReady.WaitUntil(() => host.GetComponent<SpiritCampaignController>()?.IsInitialized == true,
                "Startup did not initialize the main menu.");
            yield return ContentReady.Routed(controller);
            var frontend = ContentReady.FrontendView(controller);
            var menu = ContentReady.FrontendView(controller).MainMenu;
            Assert.That(controller.PresentationReady, Is.True);
            Assert.That(bootstrap.StartupView.Root.gameObject.activeInHierarchy, Is.False);
            Assert.That(menu.Root.gameObject.activeInHierarchy, Is.True);
            Assert.That(menu.NewGameButton.interactable, Is.True);
            Assert.That(ContentReady.EncounterView(controller), Is.Null);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<SpiritDiceTray>(), Is.Null);
            var canvas = frontend.GetComponentInChildren<Canvas>(true);
            var board = frontend.Board;
            for (int i = 0; i < 2; ++i)
                Assert.That(controller.InitializeMenu(controller.Catalog, controller.JourneyCatalog, controller.SavePath), Is.True);
            yield return ContentReady.Routed(controller);
            Assert.That(ContentReady.FrontendView(controller), Is.SameAs(frontend));
            Assert.That(frontend.GetComponentInChildren<Canvas>(true), Is.SameAs(canvas));
            Assert.That(frontend.MainMenu, Is.SameAs(menu));
            Assert.That(frontend.Board, Is.SameAs(board));
            Assert.That(frontend.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(1));
        }

        private void AssertStartupSurface()
        {
            var view = ContentReady.EncounterView(controller);
            Assert.That(controller.PresentationReady, Is.False);
            Assert.That(bootstrap.StartupView.Root.gameObject.activeInHierarchy, Is.True);
            var wording = bootstrap.StartupView.GetComponentInParent<SpiritUiContext>(true).Wording;
            Assert.That(bootstrap.StartupView.Status.text, Is.EqualTo(SpiritStatusText.Describe(controller.Status, wording.status)));
            Assert.That(host.transform.Find("SpiritCanvas"), Is.Null);
            Assert.That(view, Is.Null);
            Assert.That(ContentReady.FrontendView(controller), Is.Null);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<SpiritDiceTray>(), Is.Null);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<SpiritModifierHistoryTray>(), Is.Null);
            Assert.That(host.GetComponent<SpiritEncounterAudio>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator FailedConfigurationLoadLeavesControllerUsable()
        {
            InstallPendingConfiguration();
            yield return CreateHost();
            yield return ContentReady.WaitUntil(() => provider.IsPending, "Bootstrap did not request configuration.");
            provider.Complete(null, new InvalidOperationException(failureKey));
            yield return ContentReady.WaitUntil(() => controller.Status.Kind == SpiritStatusKind.ConfigurationFailed, "Startup did not report its failure.");
            Assert.That(controller.Status.Detail, Does.Contain(failureKey));
            Assert.That(controller.Catalog, Is.Null);
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(expectedFailures, Is.Not.Empty);
            AssertStartupSurface();
            Assert.DoesNotThrow(controller.StartNew);

            const string encounterId = "encounter.bootstrap.recovery";
            var catalog = DirectCatalog("bootstrap.recovery", encounterId, out var table);
            controller.Initialize(catalog, encounterId, 42, ContentReady.SpiritSavePath());
            RestoreOriginalLocators();
            ContentReady.Router(controller).Initialize(authoredHandle.Result.FrontendScene);
            yield return ContentReady.Routed(controller);
            Assert.That(ContentReady.EncounterView(controller).TablePresentation, Is.SameAs(table));
            Assert.That(controller.PresentationReady, Is.True);
            Assert.That(bootstrap.StartupView.Root.gameObject.activeInHierarchy, Is.False,
                "Successful initialization dismisses the error surface when its authored scene is ready.");
        }

        [UnityTest]
        public IEnumerator DestroyingBootstrapDuringPendingConfigurationNeverInitializesController()
        {
            InstallPendingConfiguration();
            yield return CreateHost();
            yield return ContentReady.WaitUntil(() => provider.IsPending, "Bootstrap did not request configuration.");
            UnityEngine.Object.Destroy(bootstrap);
            yield return null;
            RestoreOriginalLocators();
            provider.Complete(authoredHandle.Result);
            yield return ContentReady.WaitUntil(() => provider.ReleaseCount == 1, "Destroyed bootstrap retained configuration.");
            yield return null;
            Assert.That(controller.Catalog, Is.Null);
            Assert.That(controller.JourneyCatalog, Is.Null);
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.IsBusy, Is.False);
        }

        [UnityTest]
        public IEnumerator PendingStartupCannotReplaceExplicitControllerInitialization()
        {
            InstallPendingConfiguration();
            yield return CreateHost();
            yield return ContentReady.WaitUntil(() => provider.IsPending, "Bootstrap did not request configuration.");
            const string encounterId = "encounter.bootstrap.explicit";
            var catalog = DirectCatalog("bootstrap.explicit", encounterId, out var table);
            string savePath = ContentReady.SpiritSavePath();
            controller.ConfigureSeedPolicy(true, 17);
            controller.Initialize(catalog, encounterId, 42, savePath);
            var snapshot = controller.Snapshot;
            var preview = controller.Preview;
            var status = controller.Status;
            RestoreOriginalLocators();
            ContentReady.Router(controller).Initialize(authoredHandle.Result.FrontendScene);
            yield return ContentReady.Routed(controller);
            Assert.That(controller.PresentationReady, Is.True);
            Assert.That(ContentReady.EncounterView(controller), Is.Not.Null);
            Assert.That(ContentReady.EncounterView(controller).TablePresentation, Is.SameAs(table));
            provider.Complete(authoredHandle.Result);
            yield return ContentReady.WaitUntil(() => provider.ReleaseCount == 1, "Superseded startup retained configuration.");
            yield return null;
            Assert.That(controller.Catalog, Is.SameAs(catalog));
            Assert.That(controller.Snapshot, Is.SameAs(snapshot));
            Assert.That(controller.Preview, Is.SameAs(preview));
            Assert.That(controller.SavePath, Is.EqualTo(savePath));
            Assert.That(controller.JourneyCatalog, Is.Null);
            Assert.That(controller.Status, Is.SameAs(status));
            Assert.That(bootstrap.StartupView.Root.gameObject.activeInHierarchy, Is.False);
            controller.StartNew();
            var expected = EncounterSession.CreateEncounter(catalog, encounterId, 17).Snapshot;
            Assert.That(controller.Snapshot.CurrentDice, Is.EqualTo(expected.CurrentDice));
        }

        [UnityTest]
        public IEnumerator DisablingCompletedBootstrapKeepsItsContentAndInstalledThemeAlive()
        {
            yield return ContentReady.LoadApplication();
            var sceneController = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var sceneBootstrap = sceneController.GetComponent<DiceBootstrap>();
            Assert.That(sceneBootstrap, Is.Not.Null);
            var catalog = sceneController.Catalog;
            var journeyCatalog = sceneController.JourneyCatalog;
            // Any modifier: the installed theme must keep presenting it exactly as it did before the bootstrap was disabled.
            var definition = catalog.Modifiers.First();
            string name = ContentReady.Charms.GetName(definition);
            var sprite = ContentReady.Charms.GetIcon(definition);
            Assert.That(sprite, Is.Not.Null, definition.Id);
            sceneBootstrap.enabled = false;
            yield return null;
            Assert.That(sceneController.Catalog, Is.SameAs(catalog));
            Assert.That(sceneController.JourneyCatalog, Is.SameAs(journeyCatalog));
            Assert.That(ContentReady.Charms.GetName(definition), Is.EqualTo(name));
            Assert.That(ContentReady.Charms.GetIcon(definition), Is.SameAs(sprite));
            var journey = journeyCatalog.Entries.FirstOrDefault(value => value != null && !value.IsStoryOnly);
            Assert.That(journey, Is.Not.Null, "No encounter journey is authored.");
            ContentReady.SeedJourneyPrerequisites(sceneController, journey.Id);
            sceneController.SelectEncounter(journey.EncounterId);
            while (sceneController.SelectedJourney.Beats[sceneController.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                sceneController.AdvanceJourney();
            sceneController.AcceptEncounter();
            yield return ContentReady.WaitUntil(() => !sceneController.IsBusy, "Opening presentation did not complete.");
            Assert.That(sceneController.Snapshot, Is.Not.Null);
            Assert.That(sceneController.Snapshot.EncounterId, Is.EqualTo(journey.EncounterId));
        }

        /**
         * A one-encounter catalog, of the given version and every hand category, whose encounter carries the rules the
         * direct table's cards present, so a run of it opens on that table.
         */
        private MechanicalCatalog DirectCatalog(string version, string encounterId, out EncounterTablePresentationAsset table)
        {
            var hands = Enum.GetValues(typeof(HandCategory)).Cast<HandCategory>()
                .Select(category => new HandDefinition("hand." + version + "." + (int)category, category, 5, 1, 5, 1, 6));
            table = DirectTable(out var rules);
            return new MechanicalCatalog(version + ".v1", hands,
                new[] { new EncounterDefinition(encounterId, new[] { new StageDefinition(1000000, 3, 3) }, rules: rules) });
        }

        /** The authored table a run without a journey opens on, and the rules its cards present. */
        private EncounterTablePresentationAsset DirectTable(out EncounterRuleDefinition[] rules)
            => ContentReady.DirectTable(authoredHandle.Result, authoredHandle.Result.CatalogAsset.Build().Catalog, out rules);

        private void InstallPendingConfiguration()
        {
            // Use a real Addressables provider so cancellation exercises operation ownership and release.
            originalLocators = Addressables.ResourceLocators.ToArray();
            foreach (var locator in originalLocators) Addressables.RemoveResourceLocator(locator);
            provider = new PendingConfigurationProvider();
            provider.Initialize(failureKey, null);
            Addressables.ResourceManager.ResourceProviders.Add(provider);
            testLocator = new ResourceLocationMap(failureKey);
            testLocator.Add(DiceBootstrap.ConfigurationAddress, new ResourceLocationBase(DiceBootstrap.ConfigurationAddress,
                failureKey, provider.ProviderId, typeof(DiceApplicationConfig)));
            Addressables.AddResourceLocator(testLocator);
        }

        private void RestoreOriginalLocators()
        {
            if (originalLocators == null) return;
            foreach (var locator in originalLocators) Addressables.AddResourceLocator(locator);
            originalLocators = null;
        }

        private IEnumerator CreateHost()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            Assert.That(controller, Is.Not.Null);
            host = controller.gameObject;
            bootstrap = host.GetComponent<DiceBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
        }

        private sealed class PendingConfigurationProvider : ResourceProviderBase
        {
            private ProvideHandle pending;
            public bool IsPending { get; private set; }
            public int ReleaseCount { get; private set; }
            public override Type GetDefaultType(IResourceLocation location) => typeof(DiceApplicationConfig);
            public override void Provide(ProvideHandle handle)
            {
                pending = handle;
                IsPending = true;
            }
            public void Complete(DiceApplicationConfig result, Exception error = null)
            {
                Assert.That(IsPending, Is.True);
                IsPending = false;
                pending.Complete(result, error == null, error);
            }
            public override void Release(IResourceLocation location, object asset) => ++ReleaseCount;
        }
    }
}
