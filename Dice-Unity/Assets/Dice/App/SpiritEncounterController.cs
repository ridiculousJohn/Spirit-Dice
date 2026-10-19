using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
    public sealed class SpiritEncounterController : MonoBehaviour, ISpiritEncounterControls, ISpiritProgressionControls, IEncounterJourneyControls, IEncounterPresenter
    {
        private string encounterId;
        private bool useFixedSeed;
        private string initialSeed = "60";
        private EncounterApplication application;
        private ulong seed = 60;
        private string cacheStatus = string.Empty;
        private readonly PlayerProgression progression = new PlayerProgression();
        private readonly EncounterJourneyNavigation navigation = new EncounterJourneyNavigation();
        private bool directGameplay;

        public EncounterSnapshot Snapshot { get; private set; }
        public MechanicalCatalog Catalog { get; private set; }
        public HandPreview Preview { get; private set; }
        public IReadOnlyList<ModifierInspection> ModifierInspections { get; private set; } = Array.Empty<ModifierInspection>();
        public int SelectedMask { get; private set; }
        public bool IsBusy { get; private set; }
        public string StatusMessage { get; private set; } = "Waiting for application startup.";
        public string SavePath { get; private set; }
        public string ProfilePath => progression.ProfilePath;
        public IReadOnlyList<string> ProfileUnlockedModifierIds => progression.ProfileUnlockedModifierIds;
        public IReadOnlyList<string> ProfileDiscoveredFamilyIds => progression.KnownDiscoveredFamilies(Catalog);
        public IReadOnlyList<string> CompletedEncounterIds => progression.CompletedEncounterIds;
        public IReadOnlyList<string> CompletedStoryIds => progression.CompletedStoryIds;
        public string ProfileStatusMessage => progression.ProfileStatusMessage;
        public IReadOnlyList<string> VisitedEncounterIds => progression.VisitedEncounterIds;
        public IReadOnlyList<string> HighlightedEncounterIds => navigation.HighlightedEncounterIds;
        public EncounterJourneyPhase JourneyPhase => navigation.JourneyPhase;
        public EncounterJourneyCatalogAsset JourneyCatalog => navigation.JourneyCatalog;
        public EncounterJourneyAsset SelectedJourney => navigation.SelectedJourney;
        public int BeatIndex => navigation.BeatIndex;
        public string JourneyStatus { get; private set; } = string.Empty;
        public bool HasActiveEncounter => application != null && Snapshot != null && Snapshot.Outcome == EncounterOutcome.None;
        public bool CanResume => !IsBusy && navigation.CanResume(HasActiveEncounter);
        public bool CanSkipIntro => navigation.CanSkipIntro(JourneyProgress);
        private EncounterJourneyNavigation.Progress JourneyProgress => new EncounterJourneyNavigation.Progress(
            progression.HasProfile, VisitedEncounterIds, CompletedEncounterIds, CompletedStoryIds);

        public event Action StateChanged;
        public event Action<Resolution> Resolved;
        public event Action JourneyChanged;

        private void Awake()
        {
            GetComponent<SpiritEncounterView>().Bind(this);
            GetComponent<SpiritEncounterView>().BindJourney(this);
            SavePath = Path.Combine(UnityEngine.Application.persistentDataPath, "spirit-encounter.json");
        }

        public void ConfigureSeedPolicy(bool useFixedSeed, string initialSeed)
        {
            this.useFixedSeed = useFixedSeed;
            this.initialSeed = initialSeed;
        }

        internal bool Initialize(MechanicalCatalog catalog, EncounterJourneyCatalogAsset journeys, string encounterId)
        {
            if (Catalog != null || IsBusy) return false;
            if (journeys != null) return InitializeJourney(catalog, journeys, SavePath);
            Initialize(catalog, encounterId, SelectSeed(), SavePath);
            return application != null;
        }

        private void OnDestroy()
        {
            application?.AttachPresenter(null);
        }

        public void Initialize(MechanicalCatalog catalog, string encounterId, ulong seed, string savePath)
        {
            if (IsBusy) return;
            if (!PrepareProfile(savePath)) return;
            var next = new EncounterApplication(catalog, encounterId, seed, progression.KnownUnlockedModifiers(catalog),
                progression.KnownDiscoveredFamilies(catalog));
            Catalog = catalog;
            this.encounterId = encounterId;
            this.seed = seed;
            SavePath = savePath;
            directGameplay = true;
            navigation.Initialize(JourneyCatalog, encounterId);
            JourneyStatus = string.Empty;
            Install(next, "Started encounter with seed " + seed.ToString(CultureInfo.InvariantCulture));
            JourneyChanged?.Invoke();
        }

        public bool InitializeJourney(MechanicalCatalog catalog, EncounterJourneyCatalogAsset journeys, string savePath)
        {
            if (IsBusy) return false;
            try
            {
                if (journeys == null) throw new ArgumentNullException(nameof(journeys));
                journeys.Validate(catalog);
                if (application != null && Catalog != catalog)
                    throw new InvalidOperationException("An active encounter requires its original mechanical catalog.");
                if (!PrepareProfile(savePath)) { SetJourneyStatus(ProfileStatusMessage); return false; }
                Catalog = catalog;
                SavePath = savePath;
                directGameplay = false;
                navigation.InitializeJourney(journeys);
                JourneyStatus = string.Empty;
                StatusMessage = "Investigation ready.";
                JourneyChanged?.Invoke();
                StateChanged?.Invoke();
                return true;
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                SetJourneyStatus("Cannot initialize investigation: " + exception.Message);
                return false;
            }
        }

        public void SelectEncounter(string id)
        {
            if (IsBusy || Catalog == null) return;
            if (!navigation.SelectEncounter(id, JourneyProgress, out string error))
            {
                if (error != null) SetJourneyStatus(error);
                return;
            }
            SetJourneyStatus(string.Empty);
        }

        public void AdvanceJourney()
        {
            if (IsBusy || !navigation.AdvanceJourney(out var transition)) return;
            ApplyJourneyTransition(transition);
        }

        public void SkipIntro()
        {
            if (IsBusy || !navigation.SkipIntro(JourneyProgress, out var transition)) return;
            ApplyJourneyTransition(transition);
        }

        public void AcceptEncounter()
        {
            if (IsBusy || !navigation.AcceptEncounter(out var transition)) return;
            try
            {
                transition.Journey.Validate(Catalog);
                if (useFixedSeed) ParseFixedSeed();
                if (!progression.PersistProfile()) { SetJourneyStatus(ProfileStatusMessage); return; }
                if (!ApplyJourneyRewards(transition)) return;
                if (!progression.PersistVisit(transition.Journey.EncounterId)) { SetJourneyStatus(ProfileStatusMessage); return; }
                ulong nextSeed = SelectSeed();
                var next = new EncounterApplication(Catalog, transition.Journey.EncounterId, nextSeed, progression.KnownUnlockedModifiers(Catalog),
                    ProfileDiscoveredFamilyIds);
                encounterId = transition.Journey.EncounterId;
                seed = nextSeed;
                navigation.Commit(transition);
                JourneyStatus = string.Empty;
                Install(next, "Started encounter with seed " + seed.ToString(CultureInfo.InvariantCulture));
                JourneyChanged?.Invoke();
                Present(next.InitialResolution);
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                SetJourneyStatus("Cannot accept the invitation: " + exception.Message);
            }
        }

        public void ReturnToInvestigation()
        {
            if (IsBusy || !navigation.ReturnToInvestigation()) return;
            SetJourneyStatus(string.Empty);
        }

        public bool IsJourneyAvailable(string id) => navigation.IsJourneyAvailable(id, JourneyProgress);

        private void ApplyJourneyTransition(EncounterJourneyNavigation.Transition transition)
        {
            if (transition.CompletesStory)
            {
                if (!progression.CompleteStoryJourney(transition.Journey.StoryId,
                    transition.Rewards.Select(reward => reward.Build(Catalog)), Catalog))
                {
                    SetJourneyStatus(ProfileStatusMessage);
                    return;
                }
                MergeProgression();
            }
            else if (!ApplyJourneyRewards(transition)) return;
            navigation.Commit(transition);
            SetJourneyStatus(string.Empty);
            if (transition.CompletesStory) StateChanged?.Invoke();
        }

        public void ResumeEncounter()
        {
            if (IsBusy) return;
            if (!navigation.ResumeEncounter(Snapshot?.EncounterId, HasActiveEncounter, out string error))
            {
                if (error != null) SetJourneyStatus(error);
                return;
            }
            SetJourneyStatus(string.Empty);
            StateChanged?.Invoke();
        }

        public void RetryEncounter()
        {
            if (IsBusy || Snapshot == null || !navigation.RetryEncounter(Snapshot.EncounterId, Snapshot.Outcome)) return;
            SetJourneyStatus(string.Empty);
        }

        private void SetJourneyStatus(string message)
        {
            JourneyStatus = message;
            if (!string.IsNullOrEmpty(message))
            {
                StatusMessage = message;
                StateChanged?.Invoke();
            }
            JourneyChanged?.Invoke();
        }

        public void StartNew()
        {
            if (IsBusy) return;
            if (JourneyCatalog != null && !directGameplay) { ReturnToInvestigation(); return; }
            if (Catalog == null)
            {
                SetStatus("Application content is not initialized.");
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
            if (useFixedSeed) return ParseFixedSeed();

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

        private ulong ParseFixedSeed()
        {
            if (!ulong.TryParse(initialSeed, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value))
                throw new FormatException("Seed must be an unsigned 64-bit integer.");
            return value;
        }

        public void ToggleDie(int index)
        {
            if (IsBusy || JourneyPhase != EncounterJourneyPhase.Playing || Snapshot == null || Snapshot.Phase != EncounterPhase.Playing || index < 0 || index >= DiceValues.Count) return;
            SelectedMask ^= 1 << index;
            StateChanged?.Invoke();
        }

        public void Reroll() => Submit(new RerollDiceAction(SelectedMask));
        public void PlayHand() => Submit(PlayHandAction.Instance);
        public void RefreshRewards() => Submit(new RefreshModifierOfferAction(Snapshot?.PendingReward?.OfferId ?? 0));
        public void Abandon() => Submit(AbandonEncounterAction.Instance);

        public void ChooseReward(long offerId, long optionId)
        {
            EncounterAction action = Snapshot?.Phase == EncounterPhase.ChoosingUnlock
                ? (Snapshot.PendingReward.Options.Count == 0
                    ? (EncounterAction)new AcknowledgeModifierUnlockAction(offerId)
                    : new SelectModifierUnlockAction(offerId, optionId))
                : Snapshot?.Phase == EncounterPhase.ChoosingHandUpgrade
                ? (EncounterAction)new SelectHandUpgradeAction(offerId, optionId)
                : new SelectModifierAction(offerId, optionId);
            Submit(action);
        }

        public void Save()
        {
            if (!RequireApplication()) return;
            if (!progression.PersistProfile()) { SetStatus(ProfileStatusMessage); return; }
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
                if (!PrepareProfile(SavePath)) return;
                var save = EncounterSaveFileStore.Load(SavePath);
                if (!EncounterApplication.TryRestore(Catalog, save, out var next, out var result))
                {
                    SetStatus("Cannot load the encounter: " + result.Error + ". " + result.Detail);
                    return;
                }
                next.MergeUnlockedModifiers(progression.KnownUnlockedModifiers(Catalog));
                next.MergeDiscoveredFamilies(ProfileDiscoveredFamilyIds);
                if (!navigation.Load(next.Snapshot.EncounterId, JourneyCatalog != null && !directGameplay, out var transition, out string error))
                {
                    SetStatus(error);
                    return;
                }
                encounterId = next.Snapshot.EncounterId;
                navigation.Commit(transition);
                JourneyStatus = string.Empty;
                Install(next, "Loaded encounter.");
                JourneyChanged?.Invoke();
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
            bool wasChoosingUnlock = Snapshot?.Phase == EncounterPhase.ChoosingUnlock;
            IsBusy = true;
            Snapshot = resolution.Snapshot;
            var unlockSave = wasChoosingUnlock || Snapshot.Phase == EncounterPhase.ChoosingUnlock
                ? application.CaptureSave() : null;
            RecordProgression(Snapshot, true, unlockSave);
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
            RecordProgression(next.Snapshot);
            application?.AttachPresenter(null);
            application = next;
            SelectedMask = 0;
            application.AttachPresenter(this);
            SetStatus(WithCacheStatus(message));
        }

        private void Submit(EncounterAction action)
        {
            if (JourneyPhase != EncounterJourneyPhase.Playing) return;
            if (!RequireApplication()) return;
            if (!progression.PersistProfile()) { SetStatus(ProfileStatusMessage); return; }
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

        private string WithCacheStatus(string message) => message + cacheStatus +
            (string.IsNullOrEmpty(ProfileStatusMessage) ? string.Empty : " " + ProfileStatusMessage);

        private bool PrepareProfile(string savePath)
        {
            if (progression.PrepareProfile(savePath)) return true;
            SetStatus(ProfileStatusMessage);
            return false;
        }

        private void RecordProgression(EncounterSnapshot snapshot, bool highlightFollowUps = false, EncounterSaveData unlockSave = null)
        {
            if (!progression.HasProfile) return;
            var journey = JourneyCatalog?.Find(snapshot.EncounterId);
            if (snapshot.Outcome == EncounterOutcome.Won && highlightFollowUps)
                navigation.RecordVictory(snapshot.EncounterId, JourneyProgress);
            var completionReward = snapshot.Outcome == EncounterOutcome.Won ? journey?.CompletionReward?.Build(Catalog) : null;
            progression.RecordProgression(snapshot, Catalog, completionReward, unlockSave, SavePath);
        }

        public bool ApplyProgressionReward(ProgressionRewardDefinition reward)
        {
            if (IsBusy || Catalog == null || !progression.HasProfile) return false;
            if (!progression.ApplyProgressionReward(reward, Catalog, out bool changed))
            {
                SetStatus(ProfileStatusMessage);
                return false;
            }
            if (!changed) return true;
            MergeProgression();
            StateChanged?.Invoke();
            return true;
        }

        private void MergeProgression()
        {
            application?.MergeUnlockedModifiers(progression.KnownUnlockedModifiers(Catalog));
            application?.MergeDiscoveredFamilies(ProfileDiscoveredFamilyIds);
            if (application != null)
            {
                Snapshot = application.Snapshot;
                CachePreview();
            }
        }

        private bool ApplyJourneyRewards(EncounterJourneyNavigation.Transition transition)
        {
            try
            {
                foreach (var reward in transition.Rewards)
                {
                    if (!progression.ApplyProgressionReward(reward.Build(Catalog), Catalog, out bool changed))
                    {
                        SetJourneyStatus(ProfileStatusMessage);
                        return false;
                    }
                    if (changed) MergeProgression();
                }
                return true;
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                SetJourneyStatus("Cannot award story progress: " + exception.Message);
                return false;
            }
        }

        internal void SetStatus(string message)
        {
            StatusMessage = message;
            if (JourneyPhase != EncounterJourneyPhase.Playing)
            {
                JourneyStatus = message;
                JourneyChanged?.Invoke();
            }
            StateChanged?.Invoke();
        }

        private static bool IsExpectedFailure(Exception exception)
            => exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException
                || exception is CryptographicException
                || exception is FormatException || exception is NotSupportedException || exception is ArgumentException
                || exception is InvalidOperationException || exception is OverflowException || exception is KeyNotFoundException;
    }
}
