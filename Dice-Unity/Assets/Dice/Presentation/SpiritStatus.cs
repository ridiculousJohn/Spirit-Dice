using System;
using Dice.Engine;

namespace Dice.Presentation
{
    /** What the spirit application last reported: startup, menu, campaign, journey or run state, or a failure. */
    public enum SpiritStatusKind
    {
        None,
        WaitingForStartup,
        LoadingConfiguration,
        ConfigurationFailed,
        ApplicationFailed,
        ContentFailed,
        EncounterInitializationFailed,
        /** The application's state selects a presentation scene that is not authored. */
        PresentationSceneMissing,
        LoadingPresentation,
        PresentationOpenFailed,
        PresentationInitializationFailed,
        MainMenuReady,
        MainMenuFailed,
        SaveSlotsRecovered,
        UnassignedSavesRecovered,
        SaveSlotsFailed,
        SaveSlotSelectionFailed,
        NewGameFailed,
        PreviousCampaignNotRemoved,
        CampaignRecovered,
        CampaignRecoveryFailed,
        /** The player profile needs attention; Profile states why. */
        ProfileProblem,
        InvestigationReady,
        InvestigationFailed,
        ChapterNotFound,
        ChapterLocked,
        LeadNotFound,
        LocationLocked,
        SavedEncounterHasNoLead,
        ActiveEncounterHasNoLead,
        InvitationFailed,
        RewardFailed,
        ContentNotInitialized,
        StartFailed,
        Started,
        Saved,
        SaveFailed,
        CatalogRequired,
        Loaded,
        LoadFailed,
        Ready,
        ActionAccepted,
        ActionRejected,
        ActionFailed,
        NoEncounter
    }

    /**
     * A reported status with its typed data. Detail is developer text, such as an exception message; the wording the
     * player sees is composed from the kind by presentation. A run state also records the run's display warnings and
     * the profile state as they were when it was reported.
     */
    public sealed class SpiritStatus
    {
        public static readonly SpiritStatus None = new SpiritStatus(SpiritStatusKind.None);

        public SpiritStatusKind Kind { get; }
        /** Developer text describing a failure; null when the status carries none. */
        public string Detail { get; }
        /** Why the engine rejected an action, for ActionRejected. */
        public CommandRejection Rejection { get; }
        /** The slot a campaign was recovered to, for CampaignRecovered. */
        public int Slot { get; }
        /** The seed of the run, for a run state. */
        public ulong Seed { get; }
        /** The run's revision, for a run state. */
        public long Revision { get; }
        /** Developer text of the failed hand preview, for a run state; null when the preview did not fail. */
        public string HandPreviewFailure { get; }
        /** Developer text of the failed modifier inspection, for a run state; null when the inspection did not fail. */
        public string ModifierInspectionFailure { get; }
        /** The profile state, for a run state or ProfileProblem; None otherwise. */
        public PlayerProfileStatus Profile { get; }

        /** A state of the live run, reported with its warnings and profile state. */
        public bool IsRunState => IsRunKind(Kind);

        private SpiritStatus(SpiritStatusKind kind, string detail = null, CommandRejection rejection = CommandRejection.None,
            int slot = 0, ulong seed = 0, long revision = 0, string handPreviewFailure = null, string modifierInspectionFailure = null,
            PlayerProfileStatus profile = null)
        {
            Kind = kind;
            Detail = detail;
            Rejection = rejection;
            Slot = slot;
            Seed = seed;
            Revision = revision;
            HandPreviewFailure = handPreviewFailure;
            ModifierInspectionFailure = modifierInspectionFailure;
            Profile = profile ?? PlayerProfileStatus.None;
        }

        /** A status that carries at most developer detail. */
        public static SpiritStatus Of(SpiritStatusKind kind, string detail = null)
        {
            if (kind == SpiritStatusKind.None) return None;
            if (IsRunKind(kind) || kind == SpiritStatusKind.ProfileProblem || kind == SpiritStatusKind.ActionRejected ||
                kind == SpiritStatusKind.CampaignRecovered)
                throw new ArgumentException("The status " + kind + " requires its own data.", nameof(kind));
            return new SpiritStatus(kind, detail);
        }

        public static SpiritStatus Rejected(CommandRejection rejection) =>
            new SpiritStatus(SpiritStatusKind.ActionRejected, rejection: rejection);

        public static SpiritStatus Recovered(int slot) => new SpiritStatus(SpiritStatusKind.CampaignRecovered, slot: slot);

        /** The profile problem as a status; a profile without a problem reports nothing. */
        public static SpiritStatus ForProfile(PlayerProfileStatus profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            return profile.Kind == PlayerProfileStatusKind.None ? None : new SpiritStatus(SpiritStatusKind.ProfileProblem, profile: profile);
        }

        public static SpiritStatus ForRun(SpiritStatusKind kind, ulong seed, long revision, string handPreviewFailure,
            string modifierInspectionFailure, PlayerProfileStatus profile)
        {
            if (!IsRunKind(kind)) throw new ArgumentException("The status " + kind + " is not a run state.", nameof(kind));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            return new SpiritStatus(kind, seed: seed, revision: revision, handPreviewFailure: handPreviewFailure,
                modifierInspectionFailure: modifierInspectionFailure, profile: profile);
        }

        private static bool IsRunKind(SpiritStatusKind kind) => kind == SpiritStatusKind.Started || kind == SpiritStatusKind.Saved ||
            kind == SpiritStatusKind.Loaded || kind == SpiritStatusKind.Ready || kind == SpiritStatusKind.ActionAccepted;

        /** Developer text for diagnostics and test messages; the player's wording comes from presentation. */
        public override string ToString()
        {
            string text = Kind.ToString();
            if (Kind == SpiritStatusKind.ActionRejected) text += ": " + Rejection;
            if (Detail != null) text += ": " + Detail;
            if (HandPreviewFailure != null) text += "; hand preview: " + HandPreviewFailure;
            if (ModifierInspectionFailure != null) text += "; modifier inspection: " + ModifierInspectionFailure;
            if (Profile.Kind != PlayerProfileStatusKind.None) text += "; profile: " + Profile;
            return text;
        }
    }
}
