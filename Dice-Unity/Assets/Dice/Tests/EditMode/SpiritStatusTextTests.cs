using System;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;

namespace Dice.Tests
{
    /** The player's wording of typed statuses and profile states, over fabricated statuses. */
    public sealed class SpiritStatusTextTests
    {
        private static readonly PlayerProfileStatus ProfileLoadFailed = new PlayerProfileStatus(PlayerProfileStatusKind.LoadFailed, "bad profile");
        private static readonly PlayerProfileStatus ProfilePending = new PlayerProfileStatus(PlayerProfileStatusKind.ProfileSavePending, "locked");

        /** Checks each profile state is worded with its detail / fails if a state loses its wording or detail. */
        [TestCase(PlayerProfileStatusKind.None, "")]
        [TestCase(PlayerProfileStatusKind.LoadFailed, "Cannot load player profile: detail")]
        [TestCase(PlayerProfileStatusKind.EncounterSavePending, "Progress is pending: cannot save the encounter. detail")]
        [TestCase(PlayerProfileStatusKind.ProfileSavePending, "Progress is pending: cannot save player profile. detail")]
        [TestCase(PlayerProfileStatusKind.RewardFailed, "Cannot award story progress: detail")]
        [TestCase(PlayerProfileStatusKind.StoryFailed, "Cannot finish the story: detail")]
        [TestCase(PlayerProfileStatusKind.VisitFailed, "Cannot accept the invitation: cannot save player profile. detail")]
        public void EachProfileStateIsWordedWithItsDetail(PlayerProfileStatusKind kind, string expected)
        {
            var profile = kind == PlayerProfileStatusKind.None ? PlayerProfileStatus.None : new PlayerProfileStatus(kind, "detail");
            Assert.That(SpiritStatusText.Describe(profile), Is.EqualTo(expected));
        }

        /** Checks only unsaved progress counts as pending / fails if a load or operation failure reads as pending progress. */
        [TestCase(PlayerProfileStatusKind.None, false)]
        [TestCase(PlayerProfileStatusKind.LoadFailed, false)]
        [TestCase(PlayerProfileStatusKind.EncounterSavePending, true)]
        [TestCase(PlayerProfileStatusKind.ProfileSavePending, true)]
        [TestCase(PlayerProfileStatusKind.RewardFailed, false)]
        [TestCase(PlayerProfileStatusKind.StoryFailed, false)]
        [TestCase(PlayerProfileStatusKind.VisitFailed, false)]
        public void OnlyUnsavedProgressIsPending(PlayerProfileStatusKind kind, bool pending)
        {
            Assert.That(new PlayerProfileStatus(kind, "detail").IsPending, Is.EqualTo(pending));
        }

        /** Checks each status without typed data is worded with its detail / fails if a status loses its wording or detail. */
        [TestCase(SpiritStatusKind.None, "")]
        [TestCase(SpiritStatusKind.WaitingForStartup, "Waiting for application startup.")]
        [TestCase(SpiritStatusKind.LoadingConfiguration, "Loading application configuration...")]
        [TestCase(SpiritStatusKind.ConfigurationFailed, "Cannot load application configuration: detail")]
        [TestCase(SpiritStatusKind.ApplicationFailed, "Cannot initialize the application: detail")]
        [TestCase(SpiritStatusKind.ContentFailed, "Cannot load the game content: detail")]
        [TestCase(SpiritStatusKind.EncounterInitializationFailed, "Cannot initialize the encounter: detail")]
        [TestCase(SpiritStatusKind.MainMenuReady, "Main menu ready.")]
        [TestCase(SpiritStatusKind.MainMenuFailed, "Cannot initialize main menu: detail")]
        [TestCase(SpiritStatusKind.SaveSlotsRecovered, "Save-slot list recovered.")]
        [TestCase(SpiritStatusKind.UnassignedSavesRecovered, "Save-slot list recovered. Open Load Game to recover unassigned saves.")]
        [TestCase(SpiritStatusKind.SaveSlotsFailed, "Cannot open save slots: detail")]
        [TestCase(SpiritStatusKind.SaveSlotSelectionFailed, "Cannot select save slot: detail")]
        [TestCase(SpiritStatusKind.NewGameFailed, "Cannot start a new game: detail")]
        [TestCase(SpiritStatusKind.PreviousCampaignNotRemoved, "The new campaign was saved, but the previous campaign could not be removed: detail")]
        [TestCase(SpiritStatusKind.CampaignRecoveryFailed, "Cannot recover campaign: detail")]
        [TestCase(SpiritStatusKind.InvestigationReady, "Investigation ready.")]
        [TestCase(SpiritStatusKind.InvestigationFailed, "Cannot initialize investigation: detail")]
        [TestCase(SpiritStatusKind.ChapterNotFound, "The chapter does not exist in the investigation.")]
        [TestCase(SpiritStatusKind.ChapterLocked, "Follow the current lead before opening this chapter.")]
        [TestCase(SpiritStatusKind.LeadNotFound, "The encounter has no investigation lead.")]
        [TestCase(SpiritStatusKind.LocationLocked, "Follow the current lead before visiting this location.")]
        [TestCase(SpiritStatusKind.SavedEncounterHasNoLead, "Cannot load the encounter: no investigation lead exists for the saved encounter.")]
        [TestCase(SpiritStatusKind.ActiveEncounterHasNoLead, "The active encounter has no investigation lead.")]
        [TestCase(SpiritStatusKind.InvitationFailed, "Cannot accept the invitation: detail")]
        [TestCase(SpiritStatusKind.RewardFailed, "Cannot award story progress: detail")]
        [TestCase(SpiritStatusKind.ContentNotInitialized, "Application content is not initialized.")]
        [TestCase(SpiritStatusKind.StartFailed, "Cannot start the encounter: detail")]
        [TestCase(SpiritStatusKind.SaveFailed, "Cannot save the encounter: detail")]
        [TestCase(SpiritStatusKind.CatalogRequired, "A mechanical catalog is required to load an encounter.")]
        [TestCase(SpiritStatusKind.LoadFailed, "Cannot load the encounter: detail")]
        [TestCase(SpiritStatusKind.ActionFailed, "Cannot resolve the action: detail")]
        [TestCase(SpiritStatusKind.NoEncounter, "Start an encounter first.")]
        public void EachStatusIsWordedWithItsDetailOnScreenAndInTheHud(SpiritStatusKind kind, string expected)
        {
            var status = SpiritStatus.Of(kind, "detail");
            Assert.That(SpiritStatusText.Describe(status), Is.EqualTo(expected));
            Assert.That(SpiritStatusText.Gameplay(status), Is.EqualTo(expected));
        }

        /** Checks statuses with typed data word that data / fails if a rejection, slot or profile problem is lost. */
        [Test]
        public void StatusesWithTypedDataWordTheirData()
        {
            Assert.That(SpiritStatusText.Describe(SpiritStatus.Rejected(CommandRejection.NoRerollsRemaining)),
                Is.EqualTo("Action rejected: NoRerollsRemaining"));
            Assert.That(SpiritStatusText.Describe(SpiritStatus.Recovered(3)), Is.EqualTo("Campaign recovered to slot 3."));
            Assert.That(SpiritStatusText.Describe(SpiritStatus.ForProfile(ProfileLoadFailed)), Is.EqualTo("Cannot load player profile: bad profile"));
            Assert.That(SpiritStatus.ForProfile(PlayerProfileStatus.None), Is.SameAs(SpiritStatus.None));
        }

        /** Checks a status that needs typed data cannot be made without it / fails if a run state could be reported without its warnings. */
        [TestCase(SpiritStatusKind.Started)]
        [TestCase(SpiritStatusKind.ActionAccepted)]
        [TestCase(SpiritStatusKind.ActionRejected)]
        [TestCase(SpiritStatusKind.CampaignRecovered)]
        [TestCase(SpiritStatusKind.ProfileProblem)]
        public void AStatusThatNeedsTypedDataRequiresIt(SpiritStatusKind kind)
        {
            Assert.Throws<ArgumentException>(() => SpiritStatus.Of(kind));
        }

        /** Checks a run state's full wording adds its warnings and profile problem / fails if either is dropped or reordered. */
        [TestCase(SpiritStatusKind.Started, "Started encounter with seed 42")]
        [TestCase(SpiritStatusKind.Saved, "Saved encounter.")]
        [TestCase(SpiritStatusKind.Loaded, "Loaded encounter.")]
        [TestCase(SpiritStatusKind.Ready, "Encounter ready.")]
        [TestCase(SpiritStatusKind.ActionAccepted, "Accepted action at revision 7")]
        public void ARunStateIsWordedWithItsWarningsAndProfileProblem(SpiritStatusKind kind, string wording)
        {
            Assert.That(SpiritStatusText.Describe(Run(kind)), Is.EqualTo(wording));
            Assert.That(SpiritStatusText.Describe(Run(kind, "p", "i", ProfileLoadFailed)), Is.EqualTo(wording +
                " Hand preview unavailable: p Modifier inspection unavailable: i Cannot load player profile: bad profile"));
        }

        /** Checks the HUD shows a started run, accepted action or ready encounter only by its warnings / fails if their routine wording shows. */
        [TestCase(SpiritStatusKind.Started)]
        [TestCase(SpiritStatusKind.ActionAccepted)]
        [TestCase(SpiritStatusKind.Ready)]
        public void TheHudShowsRoutineRunStatesOnlyByTheirWarnings(SpiritStatusKind kind)
        {
            Assert.That(SpiritStatusText.Gameplay(Run(kind)), Is.Empty);
            Assert.That(SpiritStatusText.Gameplay(Run(kind, profile: ProfileLoadFailed)), Is.Empty);
            Assert.That(SpiritStatusText.Gameplay(Run(kind, inspection: "i")), Is.EqualTo("Modifier inspection unavailable: i"));
            Assert.That(SpiritStatusText.Gameplay(Run(kind, "p", "i", ProfileLoadFailed)),
                Is.EqualTo("Hand preview unavailable: p Modifier inspection unavailable: i Cannot load player profile: bad profile"));
        }

        /** Checks the HUD shows a saved or loaded run in full / fails if their wording or warnings are hidden. */
        [TestCase(SpiritStatusKind.Saved, "Saved encounter.")]
        [TestCase(SpiritStatusKind.Loaded, "Loaded encounter.")]
        public void TheHudShowsSavedAndLoadedRunsInFull(SpiritStatusKind kind, string wording)
        {
            Assert.That(SpiritStatusText.Gameplay(Run(kind)), Is.EqualTo(wording));
            Assert.That(SpiritStatusText.Gameplay(Run(kind, "p")), Is.EqualTo(wording + " Hand preview unavailable: p"));
        }

        /** Checks pending progress is all the HUD shows when a status reports it / fails if run wording or warnings show beside it. */
        [Test]
        public void TheHudShowsPendingProgressAlone()
        {
            const string pending = "Progress is pending: cannot save player profile. locked";
            foreach (var kind in new[] { SpiritStatusKind.Started, SpiritStatusKind.Saved, SpiritStatusKind.Loaded, SpiritStatusKind.Ready,
                SpiritStatusKind.ActionAccepted })
                Assert.That(SpiritStatusText.Gameplay(Run(kind, "p", "i", ProfilePending)), Is.EqualTo(pending), kind.ToString());
            Assert.That(SpiritStatusText.Gameplay(SpiritStatus.ForProfile(ProfilePending)), Is.EqualTo(pending));
            Assert.That(SpiritStatusText.Gameplay(SpiritStatus.ForProfile(ProfileLoadFailed)), Is.EqualTo("Cannot load player profile: bad profile"));
        }

        private static SpiritStatus Run(SpiritStatusKind kind, string preview = null, string inspection = null,
            PlayerProfileStatus profile = null) =>
            SpiritStatus.ForRun(kind, 42, 7, preview, inspection, profile ?? PlayerProfileStatus.None);
    }
}
