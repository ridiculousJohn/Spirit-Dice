using System;
using System.Globalization;

namespace Dice.Presentation
{
    /** The player's wording of reported statuses and profile states. */
    public static class SpiritStatusText
    {
        private const string RewardFailed = "Cannot award story progress: ";

        /** The full wording of a status; a run state adds its warnings and any profile problem. */
        public static string Describe(SpiritStatus status)
        {
            if (status == null) throw new ArgumentNullException(nameof(status));
            string text = Base(status);
            return status.IsRunState ? text + Warnings(status) + ProfileSuffix(status.Profile) : text;
        }

        /**
         * The wording the gameplay HUD shows: pending progress alone when the status reports it; for a started run, an
         * accepted action or a ready encounter only its warnings, if any; otherwise the full wording.
         */
        public static string Gameplay(SpiritStatus status)
        {
            if (status == null) throw new ArgumentNullException(nameof(status));
            if (status.Profile.IsPending) return Describe(status.Profile);
            if (status.Kind == SpiritStatusKind.Started || status.Kind == SpiritStatusKind.ActionAccepted ||
                status.Kind == SpiritStatusKind.Ready)
            {
                string warnings = Warnings(status);
                return warnings.Length == 0 ? "" : (warnings + ProfileSuffix(status.Profile)).Trim();
            }
            return Describe(status);
        }

        public static string Describe(PlayerProfileStatus profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            switch (profile.Kind)
            {
                case PlayerProfileStatusKind.None: return "";
                case PlayerProfileStatusKind.LoadFailed: return "Cannot load player profile: " + profile.Detail;
                case PlayerProfileStatusKind.EncounterSavePending: return "Progress is pending: cannot save the encounter. " + profile.Detail;
                case PlayerProfileStatusKind.ProfileSavePending: return "Progress is pending: cannot save player profile. " + profile.Detail;
                case PlayerProfileStatusKind.RewardFailed: return RewardFailed + profile.Detail;
                case PlayerProfileStatusKind.StoryFailed: return "Cannot finish the story: " + profile.Detail;
                case PlayerProfileStatusKind.VisitFailed: return "Cannot accept the invitation: cannot save player profile. " + profile.Detail;
                default: throw new ArgumentOutOfRangeException(nameof(profile), profile.Kind, "The profile state has no wording.");
            }
        }

        private static string Warnings(SpiritStatus status) =>
            (status.HandPreviewFailure == null ? "" : " Hand preview unavailable: " + status.HandPreviewFailure) +
            (status.ModifierInspectionFailure == null ? "" : " Modifier inspection unavailable: " + status.ModifierInspectionFailure);

        private static string ProfileSuffix(PlayerProfileStatus profile) =>
            profile.Kind == PlayerProfileStatusKind.None ? "" : " " + Describe(profile);

        private static string Base(SpiritStatus status)
        {
            string detail = status.Detail;
            switch (status.Kind)
            {
                case SpiritStatusKind.None: return "";
                case SpiritStatusKind.WaitingForStartup: return "Waiting for application startup.";
                case SpiritStatusKind.LoadingConfiguration: return "Loading application configuration...";
                case SpiritStatusKind.ConfigurationFailed: return "Cannot load application configuration: " + detail;
                case SpiritStatusKind.ApplicationFailed: return "Cannot initialize the application: " + detail;
                case SpiritStatusKind.ContentFailed: return "Cannot load the game content: " + detail;
                case SpiritStatusKind.EncounterInitializationFailed: return "Cannot initialize the encounter: " + detail;
                case SpiritStatusKind.MainMenuReady: return "Main menu ready.";
                case SpiritStatusKind.MainMenuFailed: return "Cannot initialize main menu: " + detail;
                case SpiritStatusKind.SaveSlotsRecovered: return "Save-slot list recovered.";
                case SpiritStatusKind.UnassignedSavesRecovered: return "Save-slot list recovered. Open Load Game to recover unassigned saves.";
                case SpiritStatusKind.SaveSlotsFailed: return "Cannot open save slots: " + detail;
                case SpiritStatusKind.SaveSlotSelectionFailed: return "Cannot select save slot: " + detail;
                case SpiritStatusKind.NewGameFailed: return "Cannot start a new game: " + detail;
                case SpiritStatusKind.PreviousCampaignNotRemoved:
                    return "The new campaign was saved, but the previous campaign could not be removed: " + detail;
                case SpiritStatusKind.CampaignRecovered: return "Campaign recovered to slot " + status.Slot + ".";
                case SpiritStatusKind.CampaignRecoveryFailed: return "Cannot recover campaign: " + detail;
                case SpiritStatusKind.ProfileProblem: return Describe(status.Profile);
                case SpiritStatusKind.InvestigationReady: return "Investigation ready.";
                case SpiritStatusKind.InvestigationFailed: return "Cannot initialize investigation: " + detail;
                case SpiritStatusKind.ChapterNotFound: return "The chapter does not exist in the investigation.";
                case SpiritStatusKind.ChapterLocked: return "Follow the current lead before opening this chapter.";
                case SpiritStatusKind.LeadNotFound: return "The encounter has no investigation lead.";
                case SpiritStatusKind.LocationLocked: return "Follow the current lead before visiting this location.";
                case SpiritStatusKind.SavedEncounterHasNoLead:
                    return "Cannot load the encounter: no investigation lead exists for the saved encounter.";
                case SpiritStatusKind.ActiveEncounterHasNoLead: return "The active encounter has no investigation lead.";
                case SpiritStatusKind.InvitationFailed: return "Cannot accept the invitation: " + detail;
                case SpiritStatusKind.RewardFailed: return RewardFailed + detail;
                case SpiritStatusKind.ContentNotInitialized: return "Application content is not initialized.";
                case SpiritStatusKind.StartFailed: return "Cannot start the encounter: " + detail;
                case SpiritStatusKind.Started: return "Started encounter with seed " + status.Seed.ToString(CultureInfo.InvariantCulture);
                case SpiritStatusKind.Saved: return "Saved encounter.";
                case SpiritStatusKind.SaveFailed: return "Cannot save the encounter: " + detail;
                case SpiritStatusKind.CatalogRequired: return "A mechanical catalog is required to load an encounter.";
                case SpiritStatusKind.Loaded: return "Loaded encounter.";
                case SpiritStatusKind.LoadFailed: return "Cannot load the encounter: " + detail;
                case SpiritStatusKind.Ready: return "Encounter ready.";
                case SpiritStatusKind.ActionAccepted:
                    return "Accepted action at revision " + status.Revision.ToString(CultureInfo.InvariantCulture);
                case SpiritStatusKind.ActionRejected: return "Action rejected: " + status.Rejection;
                case SpiritStatusKind.ActionFailed: return "Cannot resolve the action: " + detail;
                case SpiritStatusKind.NoEncounter: return "Start an encounter first.";
                default: throw new ArgumentOutOfRangeException(nameof(status), status.Kind, "The status has no wording.");
            }
        }
    }
}
