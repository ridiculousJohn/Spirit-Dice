using System;
using System.Collections;
using System.IO;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Dice.App
{
    public sealed class DiceBootstrap : MonoBehaviour
    {
        public const string ConfigurationAddress = "dice.application";

        [SerializeField] private SpiritEncounterController controller;
        [SerializeField] private SpiritCampaignController campaign;
        [SerializeField] private SpiritApplicationPresentation presentation;
        private AsyncOperationHandle<DiceApplicationConfig> configurationHandle;
        private bool hasConfigurationHandle;
        private bool retainContent;

        [field: SerializeField] public SpiritStartupView StartupView { get; private set; }

        private bool StartupObsolete => !isActiveAndEnabled || controller == null || controller.Catalog != null;

        private void Awake()
        {
            StartupView.Build();
            StartupView.ShowStatus(controller.Status);
        }

        private IEnumerator Start()
        {
            try
            {
                if (StartupObsolete)
                {
                    if (controller.PresentationReady) StartupView.Hide();
                    yield break;
                }
                SetStartupStatus(SpiritStatus.Of(SpiritStatusKind.LoadingConfiguration));
                if (StartupObsolete || !BeginConfigurationLoad()) yield break;

                // Addressables shutdown can invalidate operations while startup is waiting.
                while (!StartupObsolete && configurationHandle.IsValid() && !configurationHandle.IsDone) yield return null;
                if (StartupObsolete) yield break;
                if (!configurationHandle.IsValid())
                {
                    SetStartupStatus(SpiritStatus.Of(SpiritStatusKind.ConfigurationFailed, "The load operation was invalidated."));
                    yield break;
                }
                if (configurationHandle.Status != AsyncOperationStatus.Succeeded || configurationHandle.Result == null)
                {
                    SetStartupStatus(SpiritStatus.Of(SpiritStatusKind.ConfigurationFailed,
                        configurationHandle.OperationException?.Message ?? "The load returned no configuration."));
                    yield break;
                }
                DiceApplicationConfig configuration = configurationHandle.Result;
                if (!ValidateConfiguration(configuration)) yield break;
                // The catalog, the charms it references and the skin load with the configuration as its dependencies.
                Initialize(configuration);
            }
            finally
            {
                if (!retainContent) ReleaseContent();
            }
        }

        private void SetStartupStatus(SpiritStatus status)
        {
            StartupView.ShowStatus(status);
            controller.SetStatus(status);
        }

        private bool BeginConfigurationLoad()
        {
            try
            {
                configurationHandle = Addressables.LoadAssetAsync<DiceApplicationConfig>(ConfigurationAddress);
                hasConfigurationHandle = true;
                return true;
            }
            catch (Exception exception)
            {
                SetStartupStatus(SpiritStatus.Of(SpiritStatusKind.ConfigurationFailed, exception.Message));
                return false;
            }
        }

        private bool ValidateConfiguration(DiceApplicationConfig configuration)
        {
            try
            {
                configuration.Validate();
                return true;
            }
            catch (Exception exception)
            {
                SetStartupStatus(SpiritStatus.Of(SpiritStatusKind.ApplicationFailed, exception.Message));
                return false;
            }
        }

        private void Initialize(DiceApplicationConfig configuration)
        {
            MechanicalCatalog catalog = null;
            try
            {
                MechanicalContent content;
                ModifierPresentationCatalog modifierPresentation;
                try
                {
                    content = configuration.CatalogAsset.Build();
                    // The skin must present exactly the charms the content uses.
                    modifierPresentation = new ModifierPresentationCatalog(configuration.ModifierSkin, content);
                }
                catch (Exception exception)
                {
                    SetStartupStatus(SpiritStatus.Of(SpiritStatusKind.ContentFailed, exception.Message));
                    return;
                }
                catalog = content.Catalog;
                // Controller events may disable this component while initialization is on the stack.
                retainContent = true;
                controller.ConfigureSeedPolicy(configuration.UseFixedSeed, configuration.InitialSeed);
                controller.ConfigureModifierPresentation(modifierPresentation);
                bool initialized;
                if (configuration.JourneyCatalog != null)
                {
                    initialized = campaign.Initialize(catalog, configuration.JourneyCatalog, Path.GetDirectoryName(controller.SavePath));
                }
                else initialized = controller.Initialize(catalog, configuration.Encounter.Identity);
                if (initialized) presentation.Initialize(configuration.FrontendScene);
                else SetStartupStatus(controller.Status);
            }
            catch (Exception exception)
            {
                if (controller != null) SetStartupStatus(SpiritStatus.Of(SpiritStatusKind.EncounterInitializationFailed, exception.Message));
            }
            finally
            {
                retainContent = controller != null && catalog != null && ReferenceEquals(controller.Catalog, catalog);
            }
        }

        private void OnDisable()
        {
            if (retainContent) return;
            StopAllCoroutines();
            ReleaseContent();
        }

        private void OnDestroy()
        {
            ReleaseContent();
        }

        private void ReleaseContent()
        {
            if (!hasConfigurationHandle) return;
            hasConfigurationHandle = false;
            if (configurationHandle.IsValid()) Addressables.Release(configurationHandle);
        }
    }
}
