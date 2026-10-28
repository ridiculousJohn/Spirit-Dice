using System;
using System.Collections.Generic;
using System.Globalization;

namespace Dice.Presentation
{
    /** The player's wording of reported statuses and profile states, composed from the UI wording's status templates. */
    public static class SpiritStatusText
    {
        /** The full wording of a status; a run state adds its warnings and any profile problem. */
        public static string Describe(SpiritStatus status, SpiritUiWording.StatusText wording)
        {
            if (status == null) throw new ArgumentNullException(nameof(status));
            if (wording == null) throw new ArgumentNullException(nameof(wording));
            if (!status.IsRunState) return Base(status, wording);
            var parts = new List<string> { Base(status, wording) };
            AddWarnings(status, wording, parts);
            AddProfile(status.Profile, wording, parts);
            return string.Join(wording.separator, parts);
        }

        /**
         * The wording the gameplay HUD shows: pending progress alone when the status reports it; for a started run, an
         * accepted action or a ready encounter only its warnings, if any; otherwise the full wording.
         */
        public static string Gameplay(SpiritStatus status, SpiritUiWording.StatusText wording)
        {
            if (status == null) throw new ArgumentNullException(nameof(status));
            if (wording == null) throw new ArgumentNullException(nameof(wording));
            if (status.Profile.IsPending) return Describe(status.Profile, wording);
            if (status.Kind == SpiritStatusKind.Started || status.Kind == SpiritStatusKind.ActionAccepted ||
                status.Kind == SpiritStatusKind.Ready)
            {
                var parts = new List<string>();
                AddWarnings(status, wording, parts);
                if (parts.Count == 0) return "";
                AddProfile(status.Profile, wording, parts);
                return string.Join(wording.separator, parts).Trim();
            }
            return Describe(status, wording);
        }

        public static string Describe(PlayerProfileStatus profile, SpiritUiWording.StatusText wording)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (wording == null) throw new ArgumentNullException(nameof(wording));
            switch (profile.Kind)
            {
                case PlayerProfileStatusKind.None: return "";
                case PlayerProfileStatusKind.LoadFailed: return Detail(wording.profileLoadFailed, profile.Detail);
                case PlayerProfileStatusKind.EncounterSavePending: return Detail(wording.encounterSavePending, profile.Detail);
                case PlayerProfileStatusKind.ProfileSavePending: return Detail(wording.profileSavePending, profile.Detail);
                case PlayerProfileStatusKind.RewardFailed: return Detail(wording.rewardFailed, profile.Detail);
                case PlayerProfileStatusKind.StoryFailed: return Detail(wording.storyFailed, profile.Detail);
                case PlayerProfileStatusKind.VisitFailed: return Detail(wording.visitFailed, profile.Detail);
                default: throw new ArgumentOutOfRangeException(nameof(profile), profile.Kind, "The profile state has no wording.");
            }
        }

        private static void AddWarnings(SpiritStatus status, SpiritUiWording.StatusText wording, List<string> parts)
        {
            if (status.HandPreviewFailure != null) parts.Add(Detail(wording.handPreviewUnavailable, status.HandPreviewFailure));
            if (status.ModifierInspectionFailure != null)
                parts.Add(Detail(wording.modifierInspectionUnavailable, status.ModifierInspectionFailure));
        }

        private static void AddProfile(PlayerProfileStatus profile, SpiritUiWording.StatusText wording, List<string> parts)
        {
            if (profile.Kind != PlayerProfileStatusKind.None) parts.Add(Describe(profile, wording));
        }

        private static string Detail(string template, string detail) => SpiritWordingTemplate.Fill(template, "detail", detail);

        private static string Base(SpiritStatus status, SpiritUiWording.StatusText wording)
        {
            string detail = status.Detail;
            switch (status.Kind)
            {
                case SpiritStatusKind.None: return "";
                case SpiritStatusKind.WaitingForStartup: return wording.waitingForStartup;
                case SpiritStatusKind.LoadingConfiguration: return wording.loadingConfiguration;
                case SpiritStatusKind.ConfigurationFailed: return Detail(wording.configurationFailed, detail);
                case SpiritStatusKind.ApplicationFailed: return Detail(wording.applicationFailed, detail);
                case SpiritStatusKind.ContentFailed: return Detail(wording.contentFailed, detail);
                case SpiritStatusKind.EncounterInitializationFailed: return Detail(wording.encounterInitializationFailed, detail);
                case SpiritStatusKind.PresentationSceneMissing: return wording.presentationSceneMissing;
                case SpiritStatusKind.LoadingPresentation: return wording.loadingPresentation;
                case SpiritStatusKind.PresentationOpenFailed: return Detail(wording.presentationOpenFailed, detail);
                case SpiritStatusKind.PresentationInitializationFailed: return Detail(wording.presentationInitializationFailed, detail);
                case SpiritStatusKind.MainMenuReady: return wording.mainMenuReady;
                case SpiritStatusKind.MainMenuFailed: return Detail(wording.mainMenuFailed, detail);
                case SpiritStatusKind.SaveSlotsRecovered: return wording.saveSlotsRecovered;
                case SpiritStatusKind.UnassignedSavesRecovered: return wording.unassignedSavesRecovered;
                case SpiritStatusKind.SaveSlotsFailed: return Detail(wording.saveSlotsFailed, detail);
                case SpiritStatusKind.SaveSlotSelectionFailed: return Detail(wording.saveSlotSelectionFailed, detail);
                case SpiritStatusKind.NewGameFailed: return Detail(wording.newGameFailed, detail);
                case SpiritStatusKind.PreviousCampaignNotRemoved: return Detail(wording.previousCampaignNotRemoved, detail);
                case SpiritStatusKind.CampaignRecovered: return SpiritWordingTemplate.Fill(wording.campaignRecovered, "slot", status.Slot);
                case SpiritStatusKind.CampaignRecoveryFailed: return Detail(wording.campaignRecoveryFailed, detail);
                case SpiritStatusKind.ProfileProblem: return Describe(status.Profile, wording);
                case SpiritStatusKind.InvestigationReady: return wording.investigationReady;
                case SpiritStatusKind.InvestigationFailed: return Detail(wording.investigationFailed, detail);
                case SpiritStatusKind.ChapterNotFound: return wording.chapterNotFound;
                case SpiritStatusKind.ChapterLocked: return wording.chapterLocked;
                case SpiritStatusKind.LeadNotFound: return wording.leadNotFound;
                case SpiritStatusKind.LocationLocked: return wording.locationLocked;
                case SpiritStatusKind.SavedEncounterHasNoLead: return wording.savedEncounterHasNoLead;
                case SpiritStatusKind.ActiveEncounterHasNoLead: return wording.activeEncounterHasNoLead;
                case SpiritStatusKind.InvitationFailed: return Detail(wording.invitationFailed, detail);
                case SpiritStatusKind.RewardFailed: return Detail(wording.rewardFailed, detail);
                case SpiritStatusKind.ContentNotInitialized: return wording.contentNotInitialized;
                case SpiritStatusKind.StartFailed: return Detail(wording.startFailed, detail);
                case SpiritStatusKind.Started:
                    return SpiritWordingTemplate.Fill(wording.started, "seed", status.Seed.ToString(CultureInfo.InvariantCulture));
                case SpiritStatusKind.Saved: return wording.saved;
                case SpiritStatusKind.SaveFailed: return Detail(wording.saveFailed, detail);
                case SpiritStatusKind.CatalogRequired: return wording.catalogRequired;
                case SpiritStatusKind.Loaded: return wording.loaded;
                case SpiritStatusKind.LoadFailed: return Detail(wording.loadFailed, detail);
                case SpiritStatusKind.Ready: return wording.ready;
                case SpiritStatusKind.ActionAccepted: return SpiritWordingTemplate.Fill(wording.actionAccepted, "revision", status.Revision);
                case SpiritStatusKind.ActionRejected:
                    return SpiritWordingTemplate.Fill(wording.actionRejected, "rejection", status.Rejection.ToString());
                case SpiritStatusKind.ActionFailed: return Detail(wording.actionFailed, detail);
                case SpiritStatusKind.NoEncounter: return wording.noEncounter;
                default: throw new ArgumentOutOfRangeException(nameof(status), status.Kind, "The status has no wording.");
            }
        }
    }
}
