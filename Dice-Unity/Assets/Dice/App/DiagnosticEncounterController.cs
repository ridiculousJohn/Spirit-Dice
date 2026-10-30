using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using UnityEngine;
using Resolution = Dice.Engine.Resolution;

namespace Dice.App
{
    public sealed class DiagnosticEncounterController : MonoBehaviour, IDiagnosticEncounterControls, IEncounterPresenter
    {
        private const int MaximumEventLogEntries = 160;
        [SerializeField] private DiagnosticEncounterView view;
        [SerializeField] private MechanicalCatalogAsset catalogAsset;
        [SerializeField] private ModifierSkinAsset modifierSkin;
        [SerializeField, Tooltip("The encounter to play; it need not be on the catalog's encounter list yet.")]
        private EncounterAsset encounter;
        [SerializeField] private ulong initialSeed = EncounterApplication.DefaultSeed;
        private readonly List<string> eventLog = new List<string>();
        private EncounterApplication application;
        private IReadOnlyList<string> readOnlyEventLog;
        private string cacheStatus = string.Empty;
        private EncounterDefinition.Id encounterId;

        public EncounterSnapshot Snapshot { get; private set; }
        public MechanicalCatalog Catalog { get; private set; }
        public ModifierPresentationCatalog ModifierPresentation { get; private set; }
        public HandPreview Preview { get; private set; }
        public IReadOnlyList<ModifierInspection> ModifierInspections { get; private set; } = Array.Empty<ModifierInspection>();
        public IReadOnlyList<string> EventLog => readOnlyEventLog ?? (readOnlyEventLog = eventLog.AsReadOnly());
        public int SelectedMask { get; private set; }
        public string StatusMessage { get; private set; } = "Assign a mechanical catalog to start the diagnostic encounter.";
        public string SavePath { get; private set; }
        public ulong Seed { get; private set; } = EncounterApplication.DefaultSeed;
        public event Action Changed;

        private void Awake()
        {
            SavePath = Path.Combine(UnityEngine.Application.persistentDataPath, "diagnostic-encounter.json");
            Seed = initialSeed;
            BindView();
        }

        /**
         * Loads the catalog and skin the scene references, the same content and skin the application loads, with the
         * scene's encounter added to the content when the catalog does not list it.
         */
        private void Start()
        {
            if (Catalog != null) return;
            if (catalogAsset == null) return;
            try
            {
                if (encounter == null) throw new InvalidOperationException("An encounter is required.");
                MechanicalContent content = catalogAsset.Build(encounter);
                if (modifierSkin == null) throw new InvalidOperationException("A modifier skin is required.");
                ModifierPresentation = new ModifierPresentationCatalog(modifierSkin, content);
                Catalog = content.Catalog;
                encounterId = encounter.Identity;
            }
            catch (Exception exception)
            {
                SetStatus("Cannot initialize the encounter: " + exception.Message);
                return;
            }
            StartNew(initialSeed);
        }

        private void OnDestroy()
        {
            application?.AttachPresenter(null);
            ModifierPresentation = null;
        }

        /** Uses the charm presentation, which presents the catalog's charms. */
        public void Initialize(MechanicalCatalog catalog, ModifierPresentationCatalog presentation, EncounterDefinition.Id encounterId,
            ulong seed, string savePath)
        {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            var next = new EncounterApplication(catalog, encounterId, seed);
            ModifierPresentation = presentation;
            Catalog = catalog;
            this.encounterId = encounterId;
            SavePath = savePath;
            BindView();
            InstallNew(next, seed);
        }

        /** Starts a run from the seed typed into the diagnostic view; text is parsed only here, at that input. */
        public void StartNew(string seedText)
        {
            if (!ulong.TryParse(seedText, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value))
            {
                SetStatus("Seed must be an unsigned 64-bit integer.");
                return;
            }
            StartNew(value);
        }

        public void StartNew(ulong value)
        {
            if (Catalog == null)
            {
                SetStatus("Assign a mechanical catalog to start the diagnostic encounter.");
                return;
            }
            try { InstallNew(new EncounterApplication(Catalog, encounterId, value), value); }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                SetStatus("Cannot start the encounter: " + exception.Message);
            }
        }

        public void ToggleDie(int index)
        {
            if (Snapshot == null || Snapshot.Phase != EncounterPhase.Playing || index < 0 || index >= DiceValues.Count ||
                (Snapshot.LockedMask & (1 << index)) != 0) return;
            SelectedMask ^= 1 << index;
            Changed?.Invoke();
        }

        public void SelectAllDice()
        {
            if (Snapshot == null || Snapshot.Phase != EncounterPhase.Playing) return;
            SelectedMask = DiceValues.AllMask & ~Snapshot.LockedMask;
            Changed?.Invoke();
        }

        public void ClearDiceSelection()
        {
            SelectedMask = 0;
            Changed?.Invoke();
        }
        public void Reroll() => Submit(new RerollDiceAction(SelectedMask), true);
        public void PlayHand() => Submit(PlayHandAction.Instance, true);
        public void RefreshRewards() => Submit(new RefreshModifierOfferAction(Snapshot?.PendingReward?.OfferId ?? 0), false);
        public void Abandon() => Submit(AbandonEncounterAction.Instance, false);

        /** Answers the pending offer; without one there is nothing to choose. */
        public void ChooseReward(long offerId, long optionId)
        {
            var offer = Snapshot?.PendingReward;
            if (offer != null) Submit(offer.Answer(offerId, optionId), false);
        }

        public void Save()
        {
            if (!RequireApplication()) return;
            try
            {
                EncounterSaveFileStore.Save(SavePath, application.CaptureSave());
                SetStatus(WithCacheStatus("Saved encounter to " + SavePath));
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                SetStatus("Cannot save the encounter: " + exception.Message);
            }
        }

        public void Load()
        {
            if (Catalog == null)
            {
                SetStatus("A mechanical catalog is required to load an encounter.");
                return;
            }
            try
            {
                var save = EncounterSaveFileStore.Load(SavePath, Catalog);
                if (!EncounterApplication.TryRestore(Catalog, save, out var next, out var result))
                {
                    SetStatus("Cannot load the encounter: " + result.Error + ". " + result.Detail);
                    return;
                }
                application = next;
                encounterId = next.Snapshot.EncounterId;
                SelectedMask = 0;
                eventLog.Clear();
                application.AttachPresenter(this);
                SetStatus(WithCacheStatus("Loaded encounter from " + SavePath));
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                SetStatus("Cannot load the encounter: " + exception.Message);
            }
        }

        public void Synchronize(EncounterSnapshot snapshot)
        {
            ApplySnapshot(snapshot);
            Changed?.Invoke();
        }

        public void Present(Resolution resolution)
        {
            ApplySnapshot(resolution.Snapshot);
            AppendEvents(resolution);
            Changed?.Invoke();
        }

        private void ApplySnapshot(EncounterSnapshot snapshot)
        {
            Snapshot = snapshot;
            CachePreview();
            StatusMessage = WithCacheStatus("Synchronized encounter at revision " + Snapshot.Revision.ToString(CultureInfo.InvariantCulture));
        }

        /** Sets the status message and raises Changed. */
        private void SetStatus(string message)
        {
            StatusMessage = message;
            Changed?.Invoke();
        }

        private void BindView()
        {
            if (view == null) throw new InvalidOperationException("The diagnostic controller requires an authored view.");
            view.Bind(this);
        }

        private void InstallNew(EncounterApplication next, ulong seed)
        {
            application = next;
            Seed = seed;
            SelectedMask = 0;
            eventLog.Clear();
            application.AttachPresenter(this);
            AppendEvents(application.InitialResolution);
            SetStatus(WithCacheStatus("Started encounter with seed " + seed.ToString(CultureInfo.InvariantCulture)));
        }

        private void Submit(EncounterAction action, bool clearSelection)
        {
            if (!RequireApplication()) return;
            try
            {
                var result = application.Submit(action, Snapshot.Revision);
                if (!result.Accepted)
                {
                    SetStatus("Action rejected: " + result.Rejection);
                    return;
                }
                if (clearSelection) SelectedMask = 0;
                SetStatus(WithCacheStatus("Accepted action at revision " + Snapshot.Revision.ToString(CultureInfo.InvariantCulture)));
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                SetStatus("Cannot resolve the action: " + exception.Message);
            }
        }

        private bool RequireApplication()
        {
            if (application != null) return true;
            SetStatus("Start an encounter first.");
            return false;
        }

        private void CachePreview()
        {
            var display = application.PreviewForDisplay(IsExpectedFailure);
            Preview = display.HandPreview;
            ModifierInspections = display.ModifierInspections;
            cacheStatus = (display.HandPreviewFailure == null ? string.Empty : " Hand preview unavailable: " + display.HandPreviewFailure) +
                (display.ModifierInspectionFailure == null ? string.Empty : " Modifier inspection unavailable: " + display.ModifierInspectionFailure);
        }

        private string WithCacheStatus(string message) => message + cacheStatus;

        /** The diagnostic scene draws no cryptographic seeds, so it reports the shared failures other than cryptography. */
        private static bool IsExpectedFailure(Exception exception)
            => exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException
                || exception is FormatException || exception is NotSupportedException || exception is ArgumentException
                || exception is InvalidOperationException || exception is OverflowException || exception is KeyNotFoundException;

        private void AppendEvents(Resolution resolution)
        {
            foreach (var ruleEvent in resolution.Events)
                eventLog.Add(DiagnosticText.DescribeEvent(ruleEvent, Catalog, ModifierPresentation));
            if (eventLog.Count > MaximumEventLogEntries) eventLog.RemoveRange(0, eventLog.Count - MaximumEventLogEntries);
        }
    }
}
