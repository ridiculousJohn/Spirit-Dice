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
    public sealed class SpiritEncounterController : MonoBehaviour, ISpiritEncounterControls, ISpiritProgressionControls, IEncounterJourneyControls, IEncounterPresenter
    {
        [SerializeField] private MechanicalCatalogAsset catalogAsset;
        [SerializeField] private string modifierPresentationLabel = ModifierPresentationLoader.Label;
        [SerializeField] private string encounterId;
        [SerializeField] private EncounterJourneyCatalogAsset journeyCatalog;
        [SerializeField, Tooltip("Use Initial Seed for repeatable debug runs. Otherwise every new run uses a fresh seed.")]
        private bool useFixedSeed;
        [SerializeField] private string initialSeed = "60";
        private EncounterApplication application;
        private ulong seed = 60;
        private string cacheStatus = string.Empty;
        private ModifierAssetLoader modifierAssets;
        private ModifierPresentationLoader presentationAssets;
        private IDisposable presentationTheme;
        private PlayerProfile profile;
        private bool profileDirty;
        private bool unlockSavePending;
        private bool directGameplay;
        private readonly List<string> highlightedEncounterIds = new List<string>();

        public EncounterSnapshot Snapshot { get; private set; }
        public MechanicalCatalog Catalog { get; private set; }
        public HandPreview Preview { get; private set; }
        public IReadOnlyList<ModifierInspection> ModifierInspections { get; private set; } = Array.Empty<ModifierInspection>();
        public int SelectedMask { get; private set; }
        public bool IsBusy { get; private set; }
        public string StatusMessage { get; private set; } = "Assign a mechanical catalog to start the encounter.";
        public string SavePath { get; private set; }
        public string ProfilePath { get; private set; }
        public IReadOnlyList<string> ProfileUnlockedModifierIds => profile?.UnlockedModifierIds ?? (IReadOnlyList<string>)Array.Empty<string>();
        public IReadOnlyList<string> ProfileDiscoveredFamilyIds => profile != null && Catalog != null
            ? KnownDiscoveredFamilies(Catalog) : (IReadOnlyList<string>)Array.Empty<string>();
        public IReadOnlyList<string> CompletedEncounterIds => profile?.CompletedEncounterIds ?? (IReadOnlyList<string>)Array.Empty<string>();
        public IReadOnlyList<string> CompletedStoryIds => profile?.CompletedStoryIds ?? (IReadOnlyList<string>)Array.Empty<string>();
        public string ProfileStatusMessage { get; private set; } = string.Empty;
        public IReadOnlyList<string> VisitedEncounterIds => profile?.VisitedEncounterIds ?? (IReadOnlyList<string>)Array.Empty<string>();
        public IReadOnlyList<string> HighlightedEncounterIds => highlightedEncounterIds;
        public EncounterJourneyPhase JourneyPhase { get; private set; } = EncounterJourneyPhase.Playing;
        public EncounterJourneyCatalogAsset JourneyCatalog => journeyCatalog;
        public EncounterJourneyAsset SelectedJourney { get; private set; }
        public int BeatIndex { get; private set; } = -1;
        public string JourneyStatus { get; private set; } = string.Empty;
        public bool HasActiveEncounter => application != null && Snapshot != null && Snapshot.Outcome == EncounterOutcome.None;
        public bool CanResume => HasActiveEncounter && !IsBusy && JourneyPhase == EncounterJourneyPhase.Investigation;
        public bool CanSkipIntro => JourneyPhase == EncounterJourneyPhase.Sequence && SelectedJourney != null &&
            !SelectedJourney.IsStoryOnly && BeatIndex < SelectedJourney.Beats.Count - 1 && profile != null &&
            profile.VisitedEncounterIds.Contains(SelectedJourney.EncounterId);

        public event Action StateChanged;
        public event Action<Resolution> Resolved;
        public event Action JourneyChanged;

        private void Awake()
        {
            if (journeyCatalog != null) JourneyPhase = EncounterJourneyPhase.Investigation;
            GetComponent<SpiritEncounterView>().Bind(this);
            GetComponent<SpiritEncounterView>().BindJourney(this);
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
                if (journeyCatalog != null) InitializeJourney(catalog, journeyCatalog, SavePath);
                else Initialize(catalog, encounterId, SelectSeed(), SavePath);
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
            if (!PrepareProfile(savePath)) return;
            var next = new EncounterApplication(catalog, encounterId, seed, KnownUnlockedModifiers(catalog),
                KnownDiscoveredFamilies(catalog));
            Catalog = catalog;
            this.encounterId = encounterId;
            this.seed = seed;
            SavePath = savePath;
            directGameplay = true;
            SelectedJourney = journeyCatalog?.Find(encounterId);
            JourneyPhase = EncounterJourneyPhase.Playing;
            BeatIndex = -1;
            JourneyChanged?.Invoke();
            Install(next, "Started encounter with seed " + seed.ToString(CultureInfo.InvariantCulture));
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
                journeyCatalog = journeys;
                SavePath = savePath;
                directGameplay = false;
                SetStatus("Investigation ready.");
                ReturnToInvestigation();
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
            if (IsBusy || Catalog == null || journeyCatalog == null || JourneyPhase != EncounterJourneyPhase.Investigation) return;
            var selected = journeyCatalog.Find(id);
            if (selected == null) { SetJourneyStatus("The encounter has no investigation lead."); return; }
            if (!IsJourneyAvailable(id)) { SetJourneyStatus("Follow the current lead before visiting this location."); return; }
            SelectedJourney = selected;
            highlightedEncounterIds.Remove(id);
            BeatIndex = 0;
            JourneyPhase = EncounterJourneyPhase.Sequence;
            SetJourneyStatus(string.Empty);
        }

        public void AdvanceJourney()
        {
            if (IsBusy || JourneyPhase != EncounterJourneyPhase.Sequence || SelectedJourney == null) return;
            if (BeatIndex == SelectedJourney.Beats.Count - 1)
            {
                if (SelectedJourney.IsStoryOnly) CompleteStoryJourney();
                return;
            }
            if (!ApplyJourneyRewards(BeatIndex)) return;
            ++BeatIndex;
            SetJourneyStatus(string.Empty);
        }

        public void SkipIntro()
        {
            if (IsBusy || !CanSkipIntro) return;
            if (!ApplyJourneyRewards(SelectedJourney.Beats.Count - 2)) return;
            BeatIndex = SelectedJourney.Beats.Count - 1;
            SetJourneyStatus(string.Empty);
        }

        public void AcceptEncounter()
        {
            if (IsBusy || JourneyPhase != EncounterJourneyPhase.Sequence || SelectedJourney == null || SelectedJourney.IsStoryOnly ||
                SelectedJourney.Beats[BeatIndex].Kind != EncounterJourneyBeatKind.Invitation) return;
            try
            {
                SelectedJourney.Validate(Catalog);
                if (useFixedSeed) ParseFixedSeed();
                if (!PersistProfile()) { SetJourneyStatus(ProfileStatusMessage); return; }
                if (!ApplyJourneyRewards(BeatIndex)) return;
                if (!PersistVisit(SelectedJourney.EncounterId)) { SetJourneyStatus(ProfileStatusMessage); return; }
                ulong nextSeed = SelectSeed();
                var next = new EncounterApplication(Catalog, SelectedJourney.EncounterId, nextSeed, KnownUnlockedModifiers(Catalog),
                    ProfileDiscoveredFamilyIds);
                encounterId = SelectedJourney.EncounterId;
                seed = nextSeed;
                JourneyPhase = EncounterJourneyPhase.Playing;
                SetJourneyStatus(string.Empty);
                Install(next, "Started encounter with seed " + seed.ToString(CultureInfo.InvariantCulture));
                Present(next.InitialResolution);
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                SetJourneyStatus("Cannot accept the invitation: " + exception.Message);
            }
        }

        public void ReturnToInvestigation()
        {
            if (IsBusy || journeyCatalog == null) return;
            JourneyPhase = EncounterJourneyPhase.Investigation;
            SelectedJourney = null;
            BeatIndex = -1;
            SetJourneyStatus(string.Empty);
        }

        public bool IsJourneyAvailable(string id)
        {
            var journey = journeyCatalog?.Find(id);
            if (journey == null || profile == null) return false;
            if (journey.IsStoryOnly ? profile.CompletedStoryIds.Contains(journey.StoryId) :
                profile.CompletedEncounterIds.Contains(journey.EncounterId) || profile.VisitedEncounterIds.Contains(journey.EncounterId)) return true;
            foreach (string required in journey.RequiredCompletedEncounterIds)
                if (!profile.CompletedEncounterIds.Contains(required)) return false;
            foreach (string required in journey.RequiredCompletedStoryIds)
                if (!profile.CompletedStoryIds.Contains(required)) return false;
            return true;
        }

        private void CompleteStoryJourney()
        {
            if (!PersistProfile()) { SetJourneyStatus(ProfileStatusMessage); return; }
            try
            {
                var nextProfile = new PlayerProfile();
                nextProfile.Merge(profile);
                foreach (var beat in SelectedJourney.Beats)
                    if (beat.ProgressionReward != null) nextProfile.ApplyReward(beat.ProgressionReward.Build(Catalog), Catalog);
                if (SelectedJourney.CompletionReward != null)
                    nextProfile.ApplyReward(SelectedJourney.CompletionReward.Build(Catalog), Catalog);
                nextProfile.CompleteStory(SelectedJourney.StoryId);
                PlayerProfileFileStore.Save(ProfilePath, nextProfile);
                profile = nextProfile;
                ProfileStatusMessage = string.Empty;
                application?.MergeUnlockedModifiers(KnownUnlockedModifiers(Catalog));
                application?.MergeDiscoveredFamilies(ProfileDiscoveredFamilyIds);
                if (application != null) Snapshot = application.Snapshot;
                ReturnToInvestigation();
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                ProfileStatusMessage = "Cannot finish the story: " + exception.Message;
                SetJourneyStatus(ProfileStatusMessage);
            }
        }

        public void ResumeEncounter()
        {
            if (!CanResume) return;
            SelectedJourney = journeyCatalog.Find(Snapshot.EncounterId);
            if (SelectedJourney == null) { SetJourneyStatus("The active encounter has no investigation lead."); return; }
            JourneyPhase = EncounterJourneyPhase.Playing;
            BeatIndex = -1;
            SetJourneyStatus(string.Empty);
            StateChanged?.Invoke();
        }

        public void RetryEncounter()
        {
            if (IsBusy || JourneyPhase != EncounterJourneyPhase.Playing || Snapshot == null ||
                Snapshot.Outcome == EncounterOutcome.None || journeyCatalog == null) return;
            var selected = journeyCatalog.Find(Snapshot.EncounterId);
            if (selected == null) return;
            SelectedJourney = selected;
            BeatIndex = selected.Beats.Count - 1;
            JourneyPhase = EncounterJourneyPhase.Sequence;
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
            if (journeyCatalog != null && !directGameplay) { ReturnToInvestigation(); return; }
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
            if (!PersistProfile()) { SetStatus(ProfileStatusMessage); return; }
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
                next.MergeUnlockedModifiers(KnownUnlockedModifiers(Catalog));
                next.MergeDiscoveredFamilies(ProfileDiscoveredFamilyIds);
                var restoredJourney = journeyCatalog?.Find(next.Snapshot.EncounterId);
                if (journeyCatalog != null && !directGameplay && restoredJourney == null)
                {
                    SetStatus("Cannot load the encounter: no investigation lead exists for the saved encounter.");
                    return;
                }
                encounterId = next.Snapshot.EncounterId;
                SelectedJourney = restoredJourney;
                JourneyPhase = EncounterJourneyPhase.Playing;
                BeatIndex = -1;
                SetJourneyStatus(string.Empty);
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
            bool wasChoosingUnlock = Snapshot?.Phase == EncounterPhase.ChoosingUnlock;
            IsBusy = true;
            Snapshot = resolution.Snapshot;
            if (wasChoosingUnlock || Snapshot.Phase == EncounterPhase.ChoosingUnlock) unlockSavePending = true;
            RecordProgression(Snapshot, true);
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
            if (!PersistProfile()) { SetStatus(ProfileStatusMessage); return; }
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
            if (!PersistProfile()) { SetStatus(ProfileStatusMessage); return false; }
            try
            {
                string path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(savePath)), "spirit-profile.json");
                var loaded = PlayerProfileFileStore.LoadOrCreate(path);
                if (profile != null && path == ProfilePath) profile.Merge(loaded);
                else profile = loaded;
                ProfilePath = path;
                ProfileStatusMessage = string.Empty;
                return true;
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                ProfileStatusMessage = "Cannot load player profile: " + exception.Message;
                SetStatus(ProfileStatusMessage);
                return false;
            }
        }

        private IEnumerable<string> KnownUnlockedModifiers(MechanicalCatalog catalog)
        {
            var unlocked = new HashSet<string>(ProfileUnlockedModifierIds, StringComparer.Ordinal);
            foreach (var modifier in catalog.Modifiers)
                if (modifier.Availability.Access == ModifierAccess.PermanentUnlock && unlocked.Contains(modifier.Id))
                    yield return modifier.Id;
        }

        private IReadOnlyList<string> KnownDiscoveredFamilies(MechanicalCatalog catalog)
        {
            var discovered = new HashSet<string>(profile.GetDiscoveredFamilyIds(catalog), StringComparer.Ordinal);
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var modifier in catalog.Modifiers)
                if (discovered.Contains(modifier.FamilyId)) known.Add(modifier.FamilyId);
            var result = new List<string>(known);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private void RecordProgression(EncounterSnapshot snapshot, bool highlightFollowUps = false)
        {
            if (profile == null) return;
            foreach (string id in snapshot.UnlockedModifierIds) profileDirty |= profile.UnlockModifier(id);
            if (snapshot.Outcome == EncounterOutcome.Won)
            {
                bool firstVictory = !profile.CompletedEncounterIds.Contains(snapshot.EncounterId);
                if (firstVictory && highlightFollowUps)
                {
                    var journey = journeyCatalog?.Find(snapshot.EncounterId);
                    if (journey != null)
                        foreach (string id in journey.FollowUpEncounterIds)
                            if (!profile.VisitedEncounterIds.Contains(id) && !profile.CompletedEncounterIds.Contains(id) &&
                                !highlightedEncounterIds.Contains(id)) highlightedEncounterIds.Add(id);
                }
                profileDirty |= profile.CompleteEncounter(snapshot.EncounterId);
                var completionReward = journeyCatalog?.Find(snapshot.EncounterId)?.CompletionReward;
                if (completionReward != null)
                    profileDirty |= profile.ApplyReward(completionReward.Build(Catalog), Catalog);
            }
            PersistProfile();
        }

        public bool ApplyProgressionReward(ProgressionRewardDefinition reward)
        {
            if (IsBusy || Catalog == null || profile == null) return false;
            if (!PersistProfile()) { SetStatus(ProfileStatusMessage); return false; }
            try
            {
                var nextProfile = new PlayerProfile();
                nextProfile.Merge(profile);
                if (!nextProfile.ApplyReward(reward, Catalog)) return true;
                PlayerProfileFileStore.Save(ProfilePath, nextProfile);
                profile = nextProfile;
                application?.MergeUnlockedModifiers(KnownUnlockedModifiers(Catalog));
                application?.MergeDiscoveredFamilies(ProfileDiscoveredFamilyIds);
                if (application != null)
                {
                    Snapshot = application.Snapshot;
                    CachePreview();
                }
                ProfileStatusMessage = string.Empty;
                StateChanged?.Invoke();
                return true;
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                ProfileStatusMessage = "Cannot award story progress: " + exception.Message;
                SetStatus(ProfileStatusMessage);
                return false;
            }
        }

        private bool ApplyJourneyRewards(int throughBeat)
        {
            try
            {
                // Include skipped beats and retries; discovery and ownership are idempotent grants.
                for (int index = 0; index <= throughBeat; ++index)
                {
                    var reward = SelectedJourney.Beats[index].ProgressionReward;
                    if (reward != null && !ApplyProgressionReward(reward.Build(Catalog))) return false;
                }
                return true;
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                SetJourneyStatus("Cannot award story progress: " + exception.Message);
                return false;
            }
        }

        private bool PersistProfile()
        {
            if (unlockSavePending)
            {
                // Commit the consumed offer before profile progress so a retry cannot grant two choices.
                try
                {
                    EncounterSaveFileStore.Save(SavePath, application.CaptureSave());
                    unlockSavePending = false;
                    ProfileStatusMessage = string.Empty;
                }
                catch (Exception exception) when (IsExpectedFailure(exception))
                {
                    ProfileStatusMessage = "Progress is pending: cannot save the unlock choice. " + exception.Message;
                    return false;
                }
            }
            if (!profileDirty) return true;
            try
            {
                PlayerProfileFileStore.Save(ProfilePath, profile);
                profileDirty = false;
                ProfileStatusMessage = string.Empty;
                return true;
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                ProfileStatusMessage = "Progress is pending: cannot save player profile. " + exception.Message;
                return false;
            }
        }

        private bool PersistVisit(string id)
        {
            var nextProfile = new PlayerProfile();
            nextProfile.Merge(profile);
            nextProfile.VisitEncounter(id);
            try
            {
                PlayerProfileFileStore.Save(ProfilePath, nextProfile);
                profile = nextProfile;
                ProfileStatusMessage = string.Empty;
                return true;
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                ProfileStatusMessage = "Cannot accept the invitation: cannot save player profile. " + exception.Message;
                return false;
            }
        }

        private void SetStatus(string message)
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
