using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dice.App;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dice.Tests
{
    public sealed class ModifierAddressablesPlayTests
    {
        private const int ModifierCount = 300;

        [Test]
        public void DisposingOwnersAfterAddressablesInvalidatesTheirOperationsDoesNotReleaseAgain()
        {
            using var resourceManager = new ResourceManager();
            using var mechanics = new ModifierAssetLoader();
            using var presentation = new ModifierPresentationLoader();
            var mechanicsHandle = resourceManager.CreateCompletedOperation<IList<ModifierAsset>>(
                Array.Empty<ModifierAsset>(), null);
            var presentationHandle = resourceManager.CreateCompletedOperation<IList<ModifierPresentationAsset>>(
                Array.Empty<ModifierPresentationAsset>(), null);
            InstallHandle(mechanics, mechanicsHandle);
            InstallHandle(presentation, presentationHandle);

            // Release only these test operations to reproduce Addressables shutdown invalidating copied handles.
            resourceManager.Release(mechanicsHandle);
            resourceManager.Release(presentationHandle);
            Assert.That(mechanicsHandle.IsValid(), Is.False);
            Assert.That(presentationHandle.IsValid(), Is.False);
            Assert.DoesNotThrow(mechanics.Dispose);
            Assert.DoesNotThrow(presentation.Dispose);
            Assert.DoesNotThrow(mechanics.Dispose);
            Assert.DoesNotThrow(presentation.Dispose);
            Assert.That(mechanics.Assets, Is.Empty);
            Assert.That(presentation.Assets, Is.Empty);
            Assert.That(mechanics.Error, Is.Null);
            Assert.That(presentation.Error, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DisposingOwnersReleasesTheirValidOperationsExactlyOnce()
        {
            using var resourceManager = new ResourceManager();
            using var mechanics = new ModifierAssetLoader();
            using var presentation = new ModifierPresentationLoader();
            var mechanicsHandle = resourceManager.CreateCompletedOperation<IList<ModifierAsset>>(
                Array.Empty<ModifierAsset>(), null);
            var presentationHandle = resourceManager.CreateCompletedOperation<IList<ModifierPresentationAsset>>(
                Array.Empty<ModifierPresentationAsset>(), null);
            InstallHandle(mechanics, mechanicsHandle);
            InstallHandle(presentation, presentationHandle);
            var retainedMechanics = resourceManager.Acquire(mechanicsHandle);
            var retainedPresentation = resourceManager.Acquire(presentationHandle);
            try
            {
                Assert.That(retainedMechanics.ReferenceCount, Is.EqualTo(2));
                Assert.That(retainedPresentation.ReferenceCount, Is.EqualTo(2));
                mechanics.Dispose();
                presentation.Dispose();
                Assert.That(retainedMechanics.ReferenceCount, Is.EqualTo(1));
                Assert.That(retainedPresentation.ReferenceCount, Is.EqualTo(1));
                mechanics.Dispose();
                presentation.Dispose();
                Assert.That(retainedMechanics.ReferenceCount, Is.EqualTo(1));
                Assert.That(retainedPresentation.ReferenceCount, Is.EqualTo(1));
            }
            finally
            {
                if (retainedMechanics.IsValid()) resourceManager.Release(retainedMechanics);
                if (retainedPresentation.IsValid()) resourceManager.Release(retainedPresentation);
            }
            Assert.That(mechanicsHandle.IsValid(), Is.False);
            Assert.That(presentationHandle.IsValid(), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator LabelsLoadCompleteMechanicsAndThemeWithTheSameMechanicalReferences()
        {
            yield return SceneManager.LoadSceneAsync("SpiritHousePrototype");
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            Assert.That(controller, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 20;
            while (controller.Catalog == null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.Catalog, Is.Not.Null, controller.StatusMessage);
            var definitions = controller.Catalog.Modifiers.ToArray();

            using var mechanics = new ModifierAssetLoader();
            yield return Complete(mechanics.Load());
            Assert.That(mechanics.Error, Is.Null);
            Assert.That(mechanics.Assets.Count, Is.EqualTo(ModifierCount));
            var byId = mechanics.Assets.ToDictionary(asset => asset.Id, StringComparer.Ordinal);
            Assert.That(byId.Keys, Is.EquivalentTo(controller.Catalog.Modifiers.Select(definition => definition.Id)));

            using var presentation = new ModifierPresentationLoader();
            yield return Complete(presentation.Load());
            Assert.That(presentation.Error, Is.Null);
            Assert.That(presentation.Assets.Count, Is.EqualTo(ModifierCount));
            Assert.That(presentation.Assets.Select(asset => asset.Modifier.Id), Is.EquivalentTo(byId.Keys));
            foreach (ModifierPresentationAsset asset in presentation.Assets)
                Assert.That(asset.Modifier, Is.SameAs(byId[asset.Modifier.Id]), asset.name);

            const string knownId = "modifier.initial_roll.initial_pair.base_score";
            var known = presentation.Assets.Single(asset => asset.Modifier.Id == knownId);
            Assert.That(known.DisplayName, Is.EqualTo("Welcoming Pair / Base"));
            Assert.That(known.Artwork, Is.EqualTo(ModifierArtworkKind.WelcomingPair));
            var definition = controller.Catalog.GetModifier(knownId);
            Assert.That(SpiritModifierPresentation.GetName(definition), Is.EqualTo(known.DisplayName));
            Assert.That(SpiritModifierIcons.GetSprite(definition), Is.Not.Null);
            Assert.That(controller.Catalog.Modifiers, Is.EqualTo(definitions));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DisposedOwnersNeverPublishAfterTheirLoadResumesAndFreshOwnersCanReload()
        {
            var mechanics = new ModifierAssetLoader();
            var presentation = new ModifierPresentationLoader();
            var mechanicsLoad = mechanics.Load();
            var presentationLoad = presentation.Load();
            try
            {
                bool mechanicsPending = mechanicsLoad.MoveNext();
                bool presentationPending = presentationLoad.MoveNext();
                TestContext.WriteLine("Load suspended before disposal: mechanics=" + mechanicsPending +
                    ", presentation=" + presentationPending);
                mechanics.Dispose();
                presentation.Dispose();
                Assert.DoesNotThrow(mechanics.Dispose);
                Assert.DoesNotThrow(presentation.Dispose);
                Assert.That(mechanicsLoad.MoveNext(), Is.False);
                Assert.That(presentationLoad.MoveNext(), Is.False);
                yield return null;
                Assert.That(mechanics.Assets, Is.Empty);
                Assert.That(presentation.Assets, Is.Empty);
                Assert.That(mechanics.Error, Is.Null);
                Assert.That(presentation.Error, Is.Null);
            }
            finally
            {
                (mechanicsLoad as IDisposable)?.Dispose();
                (presentationLoad as IDisposable)?.Dispose();
                mechanics.Dispose();
                presentation.Dispose();
            }

            using var reloadedMechanics = new ModifierAssetLoader();
            yield return Complete(reloadedMechanics.Load());
            Assert.That(reloadedMechanics.Error, Is.Null);
            Assert.That(reloadedMechanics.Assets.Count, Is.EqualTo(ModifierCount));
            using var reloadedPresentation = new ModifierPresentationLoader();
            yield return Complete(reloadedPresentation.Load());
            Assert.That(reloadedPresentation.Error, Is.Null);
            Assert.That(reloadedPresentation.Assets.Count, Is.EqualTo(ModifierCount));
            var mechanicalIds = reloadedMechanics.Assets.Select(asset => asset.Id);
            Assert.That(reloadedPresentation.Assets.Select(asset => asset.Modifier.Id), Is.EquivalentTo(mechanicalIds));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MissingLabelsReportErrorsWithoutPublishingPartialAssets()
        {
            string missingLabel = "dice.tests.missing." + Guid.NewGuid().ToString("N");
            var reported = new List<Exception>();
            var previousHandler = ResourceManager.ExceptionHandler;
            ResourceManager.ExceptionHandler = (operation, exception) =>
            {
                // Capture the expected missing-label diagnostic while preserving unrelated error handling.
                if (exception.ToString().Contains(missingLabel)) reported.Add(exception);
                else previousHandler?.Invoke(operation, exception);
            };
            try
            {
                using var mechanics = new ModifierAssetLoader(missingLabel);
                yield return Complete(mechanics.Load());
                Assert.That(mechanics.Error, Is.Not.Null);
                Assert.That(mechanics.Error.ToString(), Does.Contain(missingLabel));
                Assert.That(mechanics.Assets, Is.Empty);

                using var presentation = new ModifierPresentationLoader(missingLabel);
                yield return Complete(presentation.Load());
                Assert.That(presentation.Error, Is.Not.Null);
                Assert.That(presentation.Error.ToString(), Does.Contain(missingLabel));
                Assert.That(presentation.Assets, Is.Empty);
                Assert.That(reported, Is.Not.Empty);
            }
            finally
            {
                ResourceManager.ExceptionHandler = previousHandler;
            }
            LogAssert.NoUnexpectedReceived();
        }

        private static void InstallHandle<T>(IDisposable loader, AsyncOperationHandle<IList<T>> handle)
        {
            var handleField = loader.GetType().GetField("handle", BindingFlags.Instance | BindingFlags.NonPublic);
            var ownershipField = loader.GetType().GetField("hasHandle", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(handleField, Is.Not.Null);
            Assert.That(ownershipField, Is.Not.Null);
            handleField.SetValue(loader, handle);
            ownershipField.SetValue(loader, true);
        }

        private static IEnumerator Complete(IEnumerator operation)
        {
            float deadline = Time.realtimeSinceStartup + 20;
            try
            {
                while (operation.MoveNext())
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Addressables load timed out.");
                    yield return operation.Current;
                }
            }
            finally
            {
                (operation as IDisposable)?.Dispose();
            }
        }
    }
}
