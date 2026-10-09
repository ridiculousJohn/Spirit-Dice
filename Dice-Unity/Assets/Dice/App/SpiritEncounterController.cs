using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using UnityEngine;
using Resolution = Dice.Engine.Resolution;

namespace Dice.App
{
    [RequireComponent(typeof(SpiritEncounterView))]
    public sealed class SpiritEncounterController : MonoBehaviour, ISpiritEncounterControls, IEncounterPresenter
    {
        [SerializeField] private MechanicalCatalogAsset catalogAsset;
        [SerializeField] private string modifierPresentationLabel = ModifierPresentationLoader.Label;
        [SerializeField] private string encounterId = "encounter.standard";
        [SerializeField, Tooltip("Use Initial Seed for repeatable debug runs. Otherwise every new run uses a fresh seed.")]
        private bool useFixedSeed;
        [SerializeField] private string initialSeed = "60";
        private EncounterApplication application;
        private ulong seed = 60;
        private string cacheStatus = string.Empty;
        private ModifierAssetLoader modifierAssets;
        private ModifierPresentationLoader presentationAssets;
        private IDisposable presentationTheme;

        public EncounterSnapshot Snapshot { get; private set; }
        public MechanicalCatalog Catalog { get; private set; }
        public HandPreview Preview { get; private set; }
        public IReadOnlyList<ModifierInspection> ModifierInspections { get; private set; } = Array.Empty<ModifierInspection>();
        public int SelectedMask { get; private set; }
        public bool IsBusy { get; private set; }
        public string StatusMessage { get; private set; } = "Assign a mechanical catalog to start the encounter.";
        public string SavePath { get; private set; }

        public event Action StateChanged;
        public event Action<Resolution> Resolved;

        private void Awake()
        {
            GetComponent<SpiritEncounterView>().Bind(this);
            SavePath = Path.Combine(UnityEngine.Application.persistentDataPath, "spirit-encounter.json");
            if (catalogAsset == null)
            {
                StateChanged?.Invoke();
                return;
            }
        }

        private IEnumerator Start()
        {
            if (catalogAsset == null || Catalog != null) yield break;
            if (string.IsNullOrWhiteSpace(modifierPresentationLabel))
            {
                SetStatus("Cannot load charm presentation: A theme label is required.");
                yield break;
            }
            SetStatus("Loading modifiers...");
            modifierAssets = new ModifierAssetLoader();
            yield return modifierAssets.Load();
            if (Catalog != null) { ReleaseContent(); yield break; }
            if (modifierAssets.Error != null)
            {
                SetStatus("Cannot load modifiers: " + modifierAssets.Error.Message);
                ReleaseContent();
                yield break;
            }
            SetStatus("Loading charm presentation...");
            presentationAssets = new ModifierPresentationLoader(modifierPresentationLabel);
            yield return presentationAssets.Load();
            if (Catalog != null) { ReleaseContent(); yield break; }
            if (presentationAssets.Error != null)
            {
                SetStatus("Cannot load charm presentation: " + presentationAssets.Error.Message);
                ReleaseContent();
                yield break;
            }
            try
            {
                var catalog = catalogAsset.Build(modifierAssets.Assets);
                presentationTheme = SpiritModifierPresentation.InstallTheme(presentationAssets.Assets, catalog);
                Initialize(catalog, encounterId, SelectSeed(), SavePath);
            }
            catch (Exception exception)
            {
                SetStatus("Cannot initialize the encounter: " + exception.Message);
                ReleaseContent();
            }
        }

        private void OnDestroy()
        {
            application?.AttachPresenter(null);
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
        }

        public void Initialize(MechanicalCatalog catalog, string encounterId, ulong seed, string savePath)
        {
            if (IsBusy) return;
            var next = new EncounterApplication(catalog, encounterId, seed);
            Catalog = catalog;
            this.encounterId = encounterId;
            this.seed = seed;
            SavePath = savePath;
            Install(next, "Started encounter with seed " + seed.ToString(CultureInfo.InvariantCulture));
        }

        public void StartNew()
        {
            if (IsBusy) return;
            if (Catalog == null)
            {
                SetStatus("Assign a mechanical catalog to start the encounter.");
                return;
            }
            try { Initialize(Catalog, encounterId, SelectSeed(), SavePath); }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                SetStatus("Cannot start the encounter: " + exception.Message);
            }
        }

        private ulong SelectSeed()
        {
            if (useFixedSeed)
            {
                if (!ulong.TryParse(initialSeed, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value))
                    throw new FormatException("Seed must be an unsigned 64-bit integer.");
                return value;
            }

            var bytes = new byte[sizeof(ulong)];
            using (var random = RandomNumberGenerator.Create())
            {
                ulong value;
                do
                {
                    random.GetBytes(bytes);
                    value = BitConverter.ToUInt64(bytes, 0);
                } while (value == seed);
                return value;
            }
        }

        public void ToggleDie(int index)
        {
            if (IsBusy || Snapshot == null || Snapshot.Phase != EncounterPhase.Playing || index < 0 || index >= DiceValues.Count) return;
            SelectedMask ^= 1 << index;
            StateChanged?.Invoke();
        }

        public void Reroll() => Submit(new RerollDiceAction(SelectedMask));
        public void PlayHand() => Submit(PlayHandAction.Instance);
        public void RefreshRewards() => Submit(new RefreshModifierOfferAction(Snapshot?.PendingReward?.OfferId ?? 0));
        public void Abandon() => Submit(AbandonEncounterAction.Instance);

        public void ChooseReward(long offerId, long optionId)
        {
            EncounterAction action = Snapshot?.Phase == EncounterPhase.ChoosingHandUpgrade
                ? (EncounterAction)new SelectHandUpgradeAction(offerId, optionId)
                : new SelectModifierAction(offerId, optionId);
            Submit(action);
        }

        public void Save()
        {
            if (!RequireApplication()) return;
            try
            {
                EncounterSaveFileStore.Save(SavePath, application.CaptureSave());
                SetStatus(WithCacheStatus("Saved encounter."));
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                SetStatus("Cannot save the encounter: " + exception.Message);
            }
        }

        public void Load()
        {
            if (IsBusy) return;
            if (Catalog == null)
            {
                SetStatus("A mechanical catalog is required to load an encounter.");
                return;
            }
            try
            {
                var save = EncounterSaveFileStore.Load(SavePath);
                if (!EncounterApplication.TryRestore(Catalog, save, out var next, out var result))
                {
                    SetStatus("Cannot load the encounter: " + result.Error + ". " + result.Detail);
                    return;
                }
                encounterId = next.Snapshot.EncounterId;
                Install(next, "Loaded encounter.");
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                SetStatus("Cannot load the encounter: " + exception.Message);
            }
        }

        public void Synchronize(EncounterSnapshot snapshot)
        {
            Snapshot = snapshot;
            CachePreview();
            SetStatus(WithCacheStatus("Encounter ready."));
        }

        public void Present(Resolution resolution)
        {
            if (!resolution.Accepted) return;
            IsBusy = true;
            Snapshot = resolution.Snapshot;
            foreach (var ruleEvent in resolution.Events)
            {
                if (ruleEvent is DiceRolledEvent || ruleEvent is HandScoredEvent) SelectedMask = 0;
            }
            CachePreview();
            StatusMessage = WithCacheStatus("Accepted action at revision " + Snapshot.Revision.ToString(CultureInfo.InvariantCulture));
            StateChanged?.Invoke();
            var handler = Resolved;
            if (handler == null) CompletePresentation();
            else handler(resolution);
        }

        public void CompletePresentation()
        {
            if (!IsBusy) return;
            IsBusy = false;
            StateChanged?.Invoke();
        }

        private void Install(EncounterApplication next, string message)
        {
            application = next;
            SelectedMask = 0;
            application.AttachPresenter(this);
            SetStatus(WithCacheStatus(message));
        }

        private void Submit(EncounterAction action)
        {
            if (!RequireApplication()) return;
            try
            {
                var result = application.Submit(action, Snapshot.Revision);
                if (!result.Accepted) SetStatus("Action rejected: " + result.Rejection);
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                SetStatus("Cannot resolve the action: " + exception.Message);
            }
        }

        private bool RequireApplication()
        {
            if (IsBusy) return false;
            if (application != null) return true;
            SetStatus("Start an encounter first.");
            return false;
        }

        private void CachePreview()
        {
            Preview = null;
            ModifierInspections = Array.Empty<ModifierInspection>();
            cacheStatus = string.Empty;
            // Display evaluation cannot invalidate a successfully synchronized engine state.
            try
            {
                if (Snapshot.Phase == EncounterPhase.Playing) Preview = application.PreviewHandResolution();
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                cacheStatus = " Hand preview unavailable: " + exception.Message;
            }
            try { ModifierInspections = application.InspectModifiersForPlayHand(); }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                cacheStatus += " Modifier inspection unavailable: " + exception.Message;
            }
        }

        private string WithCacheStatus(string message) => message + cacheStatus;

        private void SetStatus(string message)
        {
            StatusMessage = message;
            StateChanged?.Invoke();
        }

        private static bool IsExpectedFailure(Exception exception)
            => exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException
                || exception is CryptographicException
                || exception is FormatException || exception is NotSupportedException || exception is ArgumentException
                || exception is InvalidOperationException || exception is OverflowException || exception is KeyNotFoundException;
    }
}
