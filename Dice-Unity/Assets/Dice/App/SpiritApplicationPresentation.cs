using System;
using System.Collections;
using Dice.Presentation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Dice.App
{
    /** Owns presentation scene lifetime while campaign and encounter state remain in the application scene. */
    public sealed class SpiritApplicationPresentation : MonoBehaviour
    {
        [SerializeField] private SpiritEncounterController controller;
        [SerializeField] private SpiritCampaignController campaign;
        [SerializeField] private SpiritStartupView startup;
        [SerializeField] private AssetReference defaultEncounterScene;
        private AssetReference frontend;
        private AssetReference requested;
        private string current;
        private Coroutine transition;
        private AsyncOperationHandle<SceneInstance> scene;
        public SpiritEncounterView EncounterView { get; private set; }
        public SpiritFrontendView FrontendView { get; private set; }
        public GameObject PresentationRoot { get; private set; }
        public bool IsReady => transition == null && PresentationRoot != null && requested != null && current == requested.AssetGUID;

        public void Initialize(AssetReference frontendScene)
        {
            if (frontend != null) throw new InvalidOperationException("Application presentation is already initialized.");
            frontend = frontendScene ?? throw new ArgumentNullException(nameof(frontendScene));
            controller.JourneyChanged += RequestScene;
            controller.StateChanged += RequestScene;
            RequestScene();
        }

        private void RequestScene()
        {
            bool atFrontend = controller.JourneyPhase == EncounterJourneyPhase.MainMenu ||
                controller.JourneyPhase == EncounterJourneyPhase.Investigation;
            var selectedTable = controller.SelectedJourney?.TablePresentation;
            requested = atFrontend ? frontend : selectedTable != null ? selectedTable.Scene : defaultEncounterScene;
            if (requested == null || !requested.RuntimeKeyIsValid())
            {
                startup.ShowStatus(SpiritStatus.Of(SpiritStatusKind.PresentationSceneMissing));
                return;
            }
            if ((current == requested.AssetGUID && PresentationRoot != null) || transition != null) return;
            transition = StartCoroutine(ChangeScene());
        }

        private IEnumerator ChangeScene()
        {
            while (requested != null && (current != requested.AssetGUID || PresentationRoot == null))
            {
                startup.ShowStatus(SpiritStatus.Of(SpiritStatusKind.LoadingPresentation));
                if (PresentationRoot != null) PresentationRoot.SetActive(false);
                EncounterView = null;
                FrontendView = null;
                PresentationRoot = null;
                current = null;
                // Transfer ownership before yielding so teardown cannot request the same unload again.
                var previousScene = scene;
                scene = default;
                if (previousScene.IsValid())
                {
                    if (previousScene.Status == AsyncOperationStatus.Succeeded)
                        yield return Addressables.UnloadSceneAsync(previousScene);
                    else Addressables.Release(previousScene);
                }
                string loading = requested.AssetGUID;
                // The router releases its handle during transitions and teardown, including Unity Single loads.
                scene = Addressables.LoadSceneAsync(requested.RuntimeKey, LoadSceneMode.Additive,
                    SceneReleaseMode.OnlyReleaseSceneOnHandleRelease);
                yield return scene;
                if (scene.Status != AsyncOperationStatus.Succeeded)
                {
                    startup.ShowStatus(SpiritStatus.Of(SpiritStatusKind.PresentationOpenFailed, scene.OperationException?.Message));
                    transition = null;
                    yield break;
                }
                foreach (var root in scene.Result.Scene.GetRootGameObjects())
                {
                    var encounterView = root.GetComponentInChildren<SpiritEncounterView>(true);
                    var frontendView = root.GetComponentInChildren<SpiritFrontendView>(true);
                    if (encounterView == null && frontendView == null) continue;
                    PresentationRoot = root;
                    EncounterView = encounterView;
                    FrontendView = frontendView;
                    break;
                }
                if (loading != requested.AssetGUID) continue;
                try
                {
                    if (FrontendView != null) FrontendView.Bind(controller, controller, campaign);
                    else if (EncounterView != null)
                    {
                        EncounterView.BindMainMenu(campaign.IsInitialized ? campaign : null);
                        if (controller.JourneyCatalog != null) EncounterView.Bind(controller, controller);
                        else EncounterView.Bind(controller);
                    }
                    else throw new InvalidOperationException("The scene requires an authored presentation root.");
                    current = loading;
                    controller.PresentationDidInitialize();
                    startup.Hide();
                }
                catch (Exception exception)
                {
                    startup.ShowStatus(SpiritStatus.Of(SpiritStatusKind.PresentationInitializationFailed, exception.Message));
                    transition = null;
                    yield break;
                }
            }
            transition = null;
        }

        private void OnDestroy()
        {
            if (PresentationRoot != null) PresentationRoot.SetActive(false);
            if (controller != null)
            {
                controller.JourneyChanged -= RequestScene;
                controller.StateChanged -= RequestScene;
            }
            StopAllCoroutines();
            var ownedScene = scene;
            scene = default;
            if (!ownedScene.IsValid()) return;
            if (!ownedScene.IsDone) ownedScene.Completed += ReleaseScene;
            else ReleaseScene(ownedScene);
        }

        private static void ReleaseScene(AsyncOperationHandle<SceneInstance> ownedScene)
        {
            if (!ownedScene.IsValid()) return;
            if (ownedScene.Status == AsyncOperationStatus.Succeeded) Addressables.UnloadSceneAsync(ownedScene);
            else Addressables.Release(ownedScene);
        }
    }
}
