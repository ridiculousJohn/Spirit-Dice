using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Dice.Presentation
{
    /** Holds the active theme's Addressables references until its owner releases them. */
    public sealed class ModifierPresentationLoader : IDisposable
    {
        public const string Label = "dice.theme.spirit-house.modifiers";

        private readonly string label;
        private AsyncOperationHandle<IList<ModifierPresentationAsset>> handle;
        private bool hasHandle;
        private bool loadStarted;
        private bool disposed;

        public IReadOnlyList<ModifierPresentationAsset> Assets { get; private set; } = Array.Empty<ModifierPresentationAsset>();
        public Exception Error { get; private set; }

        public ModifierPresentationLoader(string label = Label)
        {
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A theme label is required.", nameof(label));
            this.label = label;
        }

        public IEnumerator Load()
        {
            if (disposed) throw new ObjectDisposedException(nameof(ModifierPresentationLoader));
            if (loadStarted) throw new InvalidOperationException("Presentation assets can only be loaded once per loader.");
            loadStarted = true;
            try
            {
                handle = Addressables.LoadAssetsAsync<ModifierPresentationAsset>(label, null, false);
                hasHandle = true;
            }
            catch (Exception exception)
            {
                Error = exception;
            }
            if (Error != null) yield break;

            // A cancelled owner may release the operation between coroutine frames.
            while (!disposed && !handle.IsDone) yield return null;
            if (disposed) yield break;
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Fail(handle.OperationException ?? new InvalidOperationException("Modifier presentation assets failed to load."));
                yield break;
            }
            IList<ModifierPresentationAsset> result = handle.Result;
            if (result == null || result.Count == 0)
            {
                Fail(new InvalidOperationException("The theme load returned no presentation assets."));
                yield break;
            }
            var loaded = new List<ModifierPresentationAsset>(result);
            if (loaded.Exists(asset => asset == null))
            {
                Fail(new InvalidOperationException("The theme load returned a missing presentation asset."));
                yield break;
            }
            Assets = loaded.AsReadOnly();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Assets = Array.Empty<ModifierPresentationAsset>();
            Release();
        }

        private void Fail(Exception exception)
        {
            Error = exception;
            Release();
        }

        private void Release()
        {
            if (!hasHandle) return;
            hasHandle = false;
            if (handle.IsValid()) Addressables.Release(handle);
        }
    }
}
