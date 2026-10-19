using System;
using System.Collections;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Dice.App
{
    [RequireComponent(typeof(SpiritEncounterController))]
    public sealed class DiceBootstrap : MonoBehaviour
    {
        public const string ConfigurationAddress = "dice.application";

        private SpiritEncounterController controller;
        private AsyncOperationHandle<DiceApplicationConfig> configurationHandle;
        private bool hasConfigurationHandle;
        private ModifierAssetLoader modifierAssets;
        private ModifierPresentationLoader presentationAssets;
        private IDisposable presentationTheme;
        private bool retainContent;

        private bool StartupObsolete => !isActiveAndEnabled || controller == null || controller.Catalog != null;

        private IEnumerator Start()
        {
            controller = GetComponent<SpiritEncounterController>();
            if (StartupObsolete) yield break;
            try
            {
                controller.SetStatus("Loading application configuration...");
                if (StartupObsolete || !BeginConfigurationLoad()) yield break;

                // Addressables shutdown can invalidate operations while startup is waiting.
                while (!StartupObsolete && configurationHandle.IsValid() && !configurationHandle.IsDone) yield return null;
                if (StartupObsolete) yield break;
                if (!configurationHandle.IsValid())
                {
                    controller.SetStatus("Cannot load application configuration: The load operation was invalidated.");
                    yield break;
                }
                if (configurationHandle.Status != AsyncOperationStatus.Succeeded || configurationHandle.Result == null)
                {
                    controller.SetStatus("Cannot load application configuration: " +
                        (configurationHandle.OperationException?.Message ?? "The load returned no configuration."));
                    yield break;
                }
                DiceApplicationConfig configuration = configurationHandle.Result;
                if (!ValidateConfiguration(configuration)) yield break;

                controller.SetStatus("Loading modifiers...");
                if (StartupObsolete) yield break;
                modifierAssets = new ModifierAssetLoader(configuration.ModifierLabel);
                IEnumerator modifierLoad = modifierAssets.Load();
                while (!StartupObsolete && configurationHandle.IsValid() && modifierLoad.MoveNext()) yield return modifierLoad.Current;
                if (StartupObsolete) yield break;
                if (!ConfigurationStillLoaded()) yield break;
                if (modifierAssets.Error != null)
                {
                    controller.SetStatus("Cannot load modifiers: " + modifierAssets.Error.Message);
                    yield break;
                }

                controller.SetStatus("Loading charm presentation...");
                if (StartupObsolete) yield break;
                presentationAssets = new ModifierPresentationLoader(configuration.ModifierPresentationLabel);
                IEnumerator presentationLoad = presentationAssets.Load();
                while (!StartupObsolete && configurationHandle.IsValid() && presentationLoad.MoveNext()) yield return presentationLoad.Current;
                if (StartupObsolete) yield break;
                if (!ConfigurationStillLoaded()) yield break;
                if (presentationAssets.Error != null)
                {
                    controller.SetStatus("Cannot load charm presentation: " + presentationAssets.Error.Message);
                    yield break;
                }
                Initialize(configuration);
            }
            finally
            {
                if (!retainContent) ReleaseContent();
            }
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
                controller.SetStatus("Cannot load application configuration: " + exception.Message);
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
                controller.SetStatus("Cannot initialize the application: " + exception.Message);
                return false;
            }
        }

        private bool ConfigurationStillLoaded()
        {
            if (configurationHandle.IsValid()) return true;
            controller.SetStatus("Cannot initialize the application: The configuration load operation was invalidated.");
            return false;
        }

        private void Initialize(DiceApplicationConfig configuration)
        {
            MechanicalCatalog catalog = null;
            try
            {
                catalog = configuration.CatalogAsset.Build(modifierAssets.Assets);
                presentationTheme = SpiritModifierPresentation.InstallTheme(presentationAssets.Assets, catalog);
                // Controller events may disable this component while initialization is on the stack.
                retainContent = true;
                controller.ConfigureSeedPolicy(configuration.UseFixedSeed, configuration.InitialSeed);
                controller.Initialize(catalog, configuration.JourneyCatalog, configuration.EncounterId);
            }
            catch (Exception exception)
            {
                if (controller != null) controller.SetStatus("Cannot initialize the encounter: " + exception.Message);
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
            presentationTheme?.Dispose();
            presentationTheme = null;
            presentationAssets?.Dispose();
            presentationAssets = null;
            modifierAssets?.Dispose();
            modifierAssets = null;
            if (!hasConfigurationHandle) return;
            hasConfigurationHandle = false;
            if (configurationHandle.IsValid()) Addressables.Release(configurationHandle);
        }
    }
}
