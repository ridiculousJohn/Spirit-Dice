using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using UnityEngine;
using Resolution = Dice.Engine.Resolution;

namespace Dice.App
{
    public sealed class SpiritEncounterController : MonoBehaviour, ISpiritEncounterControls, ISpiritProgressionControls, IEncounterJourneyControls, IEncounterPresenter
    {
        private EncounterDefinition.Id encounterId;
        private bool useFixedSeed;
        private ulong initialSeed = EncounterApplication.DefaultSeed;
        private EncounterApplication application;
        private ulong seed = EncounterApplication.DefaultSeed;
        private string handPreviewFailure;
        private string modifierInspectionFailure;
        private readonly PlayerProgression progression = new PlayerProgression();
        private readonly EncounterJourneyNavigation navigation = new EncounterJourneyNavigation();
        private bool directGameplay;

        public EncounterSnapshot Snapshot { get; private set; }
        public MechanicalCatalog Catalog { get; private set; }
        public ModifierPresentationCatalog ModifierPresentation { get; private set; }
        public bool PresentationReady { get; private set; }
        public HandPreview Preview { get; private set; }
        public IReadOnlyList<ModifierInspection> ModifierInspections { get; private set; } = Array.Empty<ModifierInspection>();
        public int SelectedMask { get; private set; }
        public bool IsBusy { get; private set; }
        public SpiritStatus Status { get; private set; } = SpiritStatus.Of(SpiritStatusKind.WaitingForStartup);
        // The setter is internal so PlayMode tests isolate the save and profile before Start loads them.
        public string SavePath { get; internal set; }
        public string ProfilePath => progression.ProfilePath;
        public IReadOnlyList<string> ProfileUnlockedModifierIds => progression.ProfileUnlockedModifierIds;
        public IReadOnlyList<ModifierFamilyDefinition.Id> ProfileDiscoveredFamilyIds => progression.KnownDiscoveredFamilies(Catalog);
        public IReadOnlyList<EncounterDefinition.Id> CompletedEncounterIds => progression.CompletedEncounterIds;
        public IReadOnlyList<string> CompletedStoryIds => progression.CompletedStoryIds;
        public PlayerProfileStatus ProfileStatus => progression.Status;
        public IReadOnlyList<EncounterDefinition.Id> VisitedEncounterIds => progression.VisitedEncounterIds;
        public IReadOnlyList<EncounterJourneyAsset> HighlightedJourneys => navigation.HighlightedJourneys;
        public EncounterJourneyPhase JourneyPhase => navigation.JourneyPhase;
        public EncounterJourneyCatalogAsset JourneyCatalog => navigation.JourneyCatalog;
        public EncounterChapterAsset SelectedChapter => navigation.SelectedChapter;
        public EncounterJourneyAsset SelectedJourney => navigation.SelectedJourney;
        public int BeatIndex => navigation.BeatIndex;
        public SpiritStatus JourneyStatus { get; private set; } = SpiritStatus.None;
        public bool HasActiveEncounter => application != null && Snapshot != null && Snapshot.Outcome == EncounterOutcome.None;
        // Read-only test seam: lets EditMode tests check that rejected requests leave the engine and its seed untouched.
        internal EncounterApplication Application => application;
        internal ulong Seed => seed;
        public bool CanResume => !IsBusy && navigation.CanResume(HasActiveEncounter);
        public bool CanSkipIntro => navigation.CanSkipIntro(JourneyProgress);
        private EncounterJourneyNavigation.Progress JourneyProgress => new EncounterJourneyNavigation.Progress(
            progression.HasProfile, VisitedEncounterIds, CompletedEncounterIds, CompletedStoryIds);

        public event Action StateChanged;
        public event Action<Resolution> Resolved;
        public event Action JourneyChanged;
        public event Action PresentationInitialized;

        private void Awake()
        {
            if (SavePath == null)
                SavePath = Path.Combine(UnityEngine.Application.persistentDataPath, EncounterSaveFileStore.FileName);
        }

        public void PresentationDidInitialize()
        {
            if (PresentationReady) return;
            PresentationReady = true;
            PresentationInitialized?.Invoke();
        }
        public void ConfigureSeedPolicy(bool useFixedSeed, ulong initialSeed)
        {
            this.useFixedSeed = useFixedSeed;
            this.initialSeed = initialSeed;
        }

        /** Supplies the charm presentation the views read; its owner keeps it alive while the content is loaded. */
        public void ConfigureModifierPresentation(ModifierPresentationCatalog presentation)
        {
            ModifierPresentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        }

        internal bool Initialize(MechanicalCatalog catalog, EncounterDefinition.Id encounterId)
        {
            if (Catalog != null || IsBusy) return false;
            Initialize(catalog, encounterId, SelectSeed(), SavePath);
            return application != null;
        }

        private void OnDestroy()
        {
            application?.AttachPresenter(null);
        }

        public void Initialize(MechanicalCatalog catalog, EncounterDefinition.Id encounterId, ulong seed, string savePath)
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
            JourneyStatus = SpiritStatus.None;
            Install(next, SpiritStatusKind.Started);
            JourneyChanged?.Invoke();
        }

        public bool InitializeMenu(MechanicalCatalog catalog, EncounterJourneyCatalogAsset journeys, string savePath)
        {
            if (IsBusy) return false;
            try
            {
                if (journeys == null) throw new ArgumentNullException(nameof(journeys));
                journeys.Validate(catalog);
                if (string.IsNullOrEmpty(savePath)) throw new ArgumentException("A campaign save path is required.", nameof(savePath));
                Path.GetFullPath(savePath);
                application?.AttachPresenter(null);
                application = null;
                Snapshot = null;
                Preview = null;
                ModifierInspections = Array.Empty<ModifierInspection>();
                SelectedMask = 0;
                handPreviewFailure = null;
                modifierInspectionFailure = null;
                progression.Clear();
                Catalog = catalog;
                SavePath = savePath;
                directGameplay = false;
                navigation.InitializeJourney(journeys);
                navigation.ShowMainMenu();
                JourneyStatus = SpiritStatus.None;
                Status = SpiritStatus.Of(SpiritStatusKind.MainMenuReady);
                JourneyChanged?.Invoke();
                StateChanged?.Invoke();
                return true;
            }
            catch (Exception exception) when (ExpectedFailures.Includes(exception))
            {
                SetJourneyStatus(SpiritStatus.Of(SpiritStatusKind.MainMenuFailed, exception.Message));
                return false;
            }
        }

        public bool ShowMainMenu()
        {
            if (IsBusy || JourneyCatalog == null) return false;
            navigation.ShowMainMenu();
            SetJourneyStatus(SpiritStatus.None);
            StateChanged?.Invoke();
            return true;
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
                if (!PrepareProfile(savePath)) { SetJourneyStatus(ProfileProblem); return false; }
                Catalog = catalog;
                SavePath = savePath;
                directGameplay = false;
                navigation.InitializeJourney(journeys, JourneyProgress);
                JourneyStatus = SpiritStatus.None;
                Status = SpiritStatus.Of(SpiritStatusKind.InvestigationReady);
                JourneyChanged?.Invoke();
                StateChanged?.Invoke();
                return true;
            }
            catch (Exception exception) when (ExpectedFailures.Includes(exception))
            {
                SetJourneyStatus(SpiritStatus.Of(SpiritStatusKind.InvestigationFailed, exception.Message));
                return false;
            }
        }

        public void SelectChapter(string id)
        {
            if (IsBusy || Catalog == null) return;
            if (!navigation.SelectChapter(id, JourneyProgress, out SpiritStatusKind error))
            {
                if (error != SpiritStatusKind.None) SetJourneyStatus(SpiritStatus.Of(error));
                return;
            }
            SetJourneyStatus(SpiritStatus.None);
        }

        public void SelectEncounter(EncounterJourneyAsset journey)
        {
            if (IsBusy || Catalog == null) return;
            if (!navigation.SelectEncounter(journey, JourneyProgress, out SpiritStatusKind error))
            {
                if (error != SpiritStatusKind.None) SetJourneyStatus(SpiritStatus.Of(error));
                return;
            }
            SetJourneyStatus(SpiritStatus.None);
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
                if (!progression.PersistProfile()) { SetJourneyStatus(ProfileProblem); return; }
                if (!ApplyJourneyRewards(transition)) return;
                EncounterDefinition.Id selected = transition.Journey.Encounter.Identity;
                if (!progression.PersistVisit(selected)) { SetJourneyStatus(ProfileProblem); return; }
                ulong nextSeed = SelectSeed();
                var next = new EncounterApplication(Catalog, selected, nextSeed, progression.KnownUnlockedModifiers(Catalog),
                    ProfileDiscoveredFamilyIds);
                encounterId = selected;
                seed = nextSeed;
                navigation.Commit(transition);
                JourneyStatus = SpiritStatus.None;
                Install(next, SpiritStatusKind.Started);
                JourneyChanged?.Invoke();
                Present(next.InitialResolution);
            }
            catch (Exception exception) when (ExpectedFailures.Includes(exception))
            {
                SetJourneyStatus(SpiritStatus.Of(SpiritStatusKind.InvitationFailed, exception.Message));
            }
        }

        public void ReturnToInvestigation()
        {
            if (IsBusy || !navigation.ReturnToInvestigation(JourneyProgress)) return;
            SetJourneyStatus(SpiritStatus.None);
        }

        public bool IsJourneyAvailable(EncounterJourneyAsset journey) => navigation.IsJourneyAvailable(journey, JourneyProgress);
        public bool IsChapterAvailable(string id) => navigation.IsChapterAvailable(id, JourneyProgress);
        public bool IsChapterCompleted(string id) => navigation.IsChapterCompleted(id, JourneyProgress);

        private void ApplyJourneyTransition(EncounterJourneyNavigation.Transition transition)
        {
            if (transition.CompletesStory)
            {
                if (!progression.CompleteStoryJourney(transition.Journey.StoryId,
                    transition.Rewards.Select(reward => reward.Build(Catalog)), Catalog))
                {
                    SetJourneyStatus(ProfileProblem);
                    return;
                }
                MergeProgression();
            }
            else if (!ApplyJourneyRewards(transition)) return;
            navigation.Commit(transition, JourneyProgress);
            SetJourneyStatus(SpiritStatus.None);
            if (transition.CompletesStory) StateChanged?.Invoke();
        }

        public void ResumeEncounter()
        {
            if (IsBusy) return;
            if (!navigation.ResumeEncounter(Snapshot?.EncounterId, HasActiveEncounter, out SpiritStatusKind error))
            {
                if (error != SpiritStatusKind.None) SetJourneyStatus(SpiritStatus.Of(error));
                return;
            }
            SetJourneyStatus(SpiritStatus.None);
            StateChanged?.Invoke();
        }

        public void RetryEncounter()
        {
            if (IsBusy || Snapshot == null || !navigation.RetryEncounter(Snapshot.EncounterId, Snapshot.Outcome)) return;
            SetJourneyStatus(SpiritStatus.None);
        }

        private void SetJourneyStatus(SpiritStatus status)
        {
            JourneyStatus = status;
            if (status.Kind != SpiritStatusKind.None)
            {
                Status = status;
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
                SetStatus(SpiritStatus.Of(SpiritStatusKind.ContentNotInitialized));
                return;
            }
            try { Initialize(Catalog, encounterId, SelectSeed(), SavePath); }
            catch (Exception exception) when (ExpectedFailures.Includes(exception))
            {
                SetStatus(SpiritStatus.Of(SpiritStatusKind.StartFailed, exception.Message));
            }
        }

        private ulong SelectSeed()
        {
            if (useFixedSeed) return initialSeed;

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
            if (IsBusy || JourneyPhase != EncounterJourneyPhase.Playing || Snapshot == null || Snapshot.Phase != EncounterPhase.Playing || index < 0 || index >= DiceValues.Count) return;
            // A locked die cannot be rerolled, so tapping it changes nothing.
            if ((Snapshot.LockedMask & (1 << index)) != 0) return;
            SelectedMask ^= 1 << index;
            StateChanged?.Invoke();
        }

        public void Reroll() => Submit(new RerollDiceAction(SelectedMask));
        public void PlayHand() => Submit(PlayHandAction.Instance);
        public void InteractWithPiece(string pieceId, string interactionId, PieceInteractionInput input)
            => Submit(new InteractWithPieceAction(pieceId, interactionId, input));
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

        public void Save() => TrySave();

        public bool TrySave()
        {
            if (!RequireApplication()) return false;
            if (!progression.PersistProfile()) { SetStatus(ProfileProblem); return false; }
            try
            {
                EncounterSaveFileStore.Save(SavePath, application.CaptureSave());
                SetStatus(RunStatus(SpiritStatusKind.Saved));
                return true;
            }
            catch (Exception exception) when (ExpectedFailures.Includes(exception))
            {
                SetStatus(SpiritStatus.Of(SpiritStatusKind.SaveFailed, exception.Message));
                return false;
            }
        }

        public void Load() => TryLoad();

        public bool TryLoad()
        {
            if (IsBusy) return false;
            if (Catalog == null)
            {
                SetStatus(SpiritStatus.Of(SpiritStatusKind.CatalogRequired));
                return false;
            }
            try
            {
                if (!PrepareProfile(SavePath)) return false;
                var save = EncounterSaveFileStore.Load(SavePath, Catalog);
                if (!EncounterApplication.TryRestore(Catalog, save, out var next, out var result))
                {
                    SetStatus(SpiritStatus.Of(SpiritStatusKind.LoadFailed, result.Error + ". " + result.Detail));
                    return false;
                }
                next.MergeUnlockedModifiers(progression.KnownUnlockedModifiers(Catalog));
                next.MergeDiscoveredFamilies(ProfileDiscoveredFamilyIds);
                if (!navigation.Load(next.Snapshot.EncounterId, JourneyCatalog != null && !directGameplay, out var transition, out SpiritStatusKind error))
                {
                    SetStatus(SpiritStatus.Of(error));
                    return false;
                }
                encounterId = next.Snapshot.EncounterId;
                navigation.Commit(transition);
                JourneyStatus = SpiritStatus.None;
                Install(next, SpiritStatusKind.Loaded);
                JourneyChanged?.Invoke();
                return true;
            }
            catch (Exception exception) when (ExpectedFailures.Includes(exception))
            {
                SetStatus(SpiritStatus.Of(SpiritStatusKind.LoadFailed, exception.Message));
                return false;
            }
        }

        public bool OpenSavedChapter()
        {
            if (IsBusy || Catalog == null || JourneyCatalog == null || !progression.HasProfile) return false;
            try
            {
                EncounterChapterAsset chapter = null;
                EncounterApplication restored = null;
                if (File.Exists(SavePath))
                {
                    var save = EncounterSaveFileStore.Load(SavePath, Catalog);
                    if (!EncounterApplication.TryRestore(Catalog, save, out restored, out var result))
                    {
                        SetJourneyStatus(SpiritStatus.Of(SpiritStatusKind.LoadFailed, result.Error + ". " + result.Detail));
                        return false;
                    }
                    var journey = JourneyCatalog.Find(restored.Snapshot.EncounterId);
                    if (journey == null)
                    {
                        SetJourneyStatus(SpiritStatus.Of(SpiritStatusKind.SavedEncounterHasNoLead));
                        return false;
                    }
                    chapter = JourneyCatalog.Chapters?.FirstOrDefault(candidate => candidate != null && candidate.Entries.Contains(journey));
                    if (chapter != null && !navigation.IsChapterAvailable(chapter.Id, JourneyProgress))
                    {
                        SetJourneyStatus(SpiritStatus.Of(SpiritStatusKind.ChapterLocked));
                        return false;
                    }
                    restored.MergeUnlockedModifiers(progression.KnownUnlockedModifiers(Catalog));
                    restored.MergeDiscoveredFamilies(ProfileDiscoveredFamilyIds);
                }
                navigation.ReturnToInvestigation(JourneyProgress);
                if (chapter != null && !navigation.SelectChapter(chapter.Id, JourneyProgress, out SpiritStatusKind error))
                {
                    SetJourneyStatus(SpiritStatus.Of(error));
                    return false;
                }
                if (restored != null)
                {
                    encounterId = restored.Snapshot.EncounterId;
                    Install(restored, SpiritStatusKind.Loaded);
                }
                SetJourneyStatus(SpiritStatus.None);
                StateChanged?.Invoke();
                return true;
            }
            catch (Exception exception) when (ExpectedFailures.Includes(exception))
            {
                SetJourneyStatus(SpiritStatus.Of(SpiritStatusKind.LoadFailed, exception.Message));
                return false;
            }
        }

        public void Synchronize(EncounterSnapshot snapshot)
        {
            Snapshot = snapshot;
            CachePreview();
            SetStatus(RunStatus(SpiritStatusKind.Ready));
        }

        public void Present(Resolution resolution)
        {
            if (!resolution.Accepted) return;
            bool wasChoosingUnlock = Snapshot?.Phase == EncounterPhase.ChoosingUnlock;
            IsBusy = true;
            Snapshot = resolution.Snapshot;
            var unlockSave = !directGameplay || wasChoosingUnlock || Snapshot.Phase == EncounterPhase.ChoosingUnlock
                ? application.CaptureSave() : null;
            RecordProgression(Snapshot, true, unlockSave);
            foreach (var ruleEvent in resolution.Events)
            {
                if (ruleEvent.EndsDiceSelection) SelectedMask = 0;
            }
            CachePreview();
            Status = RunStatus(SpiritStatusKind.ActionAccepted);
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

        private void Install(EncounterApplication next, SpiritStatusKind kind)
        {
            RecordProgression(next.Snapshot);
            application?.AttachPresenter(null);
            application = next;
            SelectedMask = 0;
            application.AttachPresenter(this);
            SetStatus(RunStatus(kind));
        }

        private void Submit(EncounterAction action)
        {
            if (JourneyPhase != EncounterJourneyPhase.Playing) return;
            if (!RequireApplication()) return;
            if (!progression.PersistProfile()) { SetStatus(ProfileProblem); return; }
            try
            {
                var result = application.Submit(action, Snapshot.Revision);
                if (!result.Accepted) SetStatus(SpiritStatus.Rejected(result.Rejection));
            }
            catch (Exception exception) when (ExpectedFailures.Includes(exception))
            {
                SetStatus(SpiritStatus.Of(SpiritStatusKind.ActionFailed, exception.Message));
            }
        }

        private bool RequireApplication()
        {
            if (IsBusy) return false;
            if (application != null) return true;
            SetStatus(SpiritStatus.Of(SpiritStatusKind.NoEncounter));
            return false;
        }

        private void CachePreview()
        {
            var display = application.PreviewForDisplay(ExpectedFailures.Includes);
            Preview = display.HandPreview;
            ModifierInspections = display.ModifierInspections;
            handPreviewFailure = display.HandPreviewFailure;
            modifierInspectionFailure = display.ModifierInspectionFailure;
        }

        /** A run state records the display warnings and profile state it is reported with. */
        private SpiritStatus RunStatus(SpiritStatusKind kind) => SpiritStatus.ForRun(kind, seed, Snapshot?.Revision ?? 0,
            handPreviewFailure, modifierInspectionFailure, progression.Status);

        private SpiritStatus ProfileProblem => SpiritStatus.ForProfile(progression.Status);

        private bool PrepareProfile(string savePath)
        {
            if (progression.PrepareProfile(savePath)) return true;
            SetStatus(ProfileProblem);
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
                SetStatus(ProfileProblem);
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
                        SetJourneyStatus(ProfileProblem);
                        return false;
                    }
                    if (changed) MergeProgression();
                }
                return true;
            }
            catch (Exception exception) when (ExpectedFailures.Includes(exception))
            {
                SetJourneyStatus(SpiritStatus.Of(SpiritStatusKind.RewardFailed, exception.Message));
                return false;
            }
        }

        internal void SetStatus(SpiritStatus status)
        {
            Status = status ?? throw new ArgumentNullException(nameof(status));
            if (JourneyPhase != EncounterJourneyPhase.Playing)
            {
                JourneyStatus = status;
                JourneyChanged?.Invoke();
            }
            StateChanged?.Invoke();
        }
    }
}
