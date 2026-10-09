using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Dice.UnityContent
{
    public sealed class ModifierAssetLoader : IDisposable
    {
        public const string Label = "dice.modifiers";

        private readonly string label;
        private AsyncOperationHandle<IList<ModifierAsset>> handle;
        private bool hasHandle;
        private bool loadStarted;
        private bool disposed;

        public IReadOnlyList<ModifierAsset> Assets { get; private set; } = Array.Empty<ModifierAsset>();
        public Exception Error { get; private set; }

        public ModifierAssetLoader(string label = Label)
        {
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A modifier label is required.", nameof(label));
            this.label = label;
        }

        public IEnumerator Load()
        {
            if (disposed) throw new ObjectDisposedException(nameof(ModifierAssetLoader));
            if (loadStarted) throw new InvalidOperationException("Modifier assets can only be loaded once per loader.");
            loadStarted = true;
            try
            {
                handle = Addressables.LoadAssetsAsync<ModifierAsset>(label, null, false);
                hasHandle = true;
            }
            catch (Exception exception)
            {
                Error = exception;
            }
            if (Error != null) yield break;

            // Dispose can release an in-flight handle, so check ownership before reading it each frame.
            while (!disposed && !handle.IsDone) yield return null;
            if (disposed) yield break;
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Error = handle.OperationException ?? new InvalidOperationException("Modifier assets failed to load.");
                yield break;
            }
            var result = handle.Result;
            if (result == null)
            {
                Error = new InvalidOperationException("The modifier load returned no asset collection.");
                yield break;
            }
            var loaded = new List<ModifierAsset>(result);
            if (loaded.Exists(asset => asset == null))
            {
                Error = new InvalidOperationException("The modifier load returned a missing asset.");
                yield break;
            }
            Assets = loaded.AsReadOnly();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Assets = Array.Empty<ModifierAsset>();
            if (!hasHandle) return;
            Addressables.Release(handle);
            hasHandle = false;
        }
    }
}
