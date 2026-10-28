using System;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    /** The player's wording of typed statuses and profile states, over fabricated statuses and fabricated wording. */
    public sealed class SpiritStatusTextTests
    {
        private static readonly PlayerProfileStatus ProfileLoadFailed = new PlayerProfileStatus(PlayerProfileStatusKind.LoadFailed, "bad profile");
        private static readonly PlayerProfileStatus ProfilePending = new PlayerProfileStatus(PlayerProfileStatusKind.ProfileSavePending, "locked");
        private const string Separator = " |separator| ";
        private const string LoadFailedWording = "<status.profileLoadFailed>bad profile";
        private SpiritUiWording asset;
        private SpiritUiWording.StatusText wording;

        [SetUp]
        public void SetUp()
        {
            asset = SyntheticWording.Create();
            wording = asset.status;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(asset);

        /** Checks each profile state is worded by its template with its detail / fails if a state loses its wording or detail. */
        [TestCase(PlayerProfileStatusKind.None, "")]
        [TestCase(PlayerProfileStatusKind.LoadFailed, "<status.profileLoadFailed>detail")]
        [TestCase(PlayerProfileStatusKind.EncounterSavePending, "<status.encounterSavePending>detail")]
        [TestCase(PlayerProfileStatusKind.ProfileSavePending, "<status.profileSavePending>detail")]
        [TestCase(PlayerProfileStatusKind.RewardFailed, "<status.rewardFailed>detail")]
        [TestCase(PlayerProfileStatusKind.StoryFailed, "<status.storyFailed>detail")]
        [TestCase(PlayerProfileStatusKind.VisitFailed, "<status.visitFailed>detail")]
        public void EachProfileStateIsWordedWithItsDetail(PlayerProfileStatusKind kind, string expected)
        {
            var profile = kind == PlayerProfileStatusKind.None ? PlayerProfileStatus.None : new PlayerProfileStatus(kind, "detail");
            Assert.That(SpiritStatusText.Describe(profile, wording), Is.EqualTo(expected));
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

        /** Checks each status without typed data is worded by its template with its detail / fails if a status loses its wording or detail. */
        [TestCase(SpiritStatusKind.None, "")]
        [TestCase(SpiritStatusKind.WaitingForStartup, "<status.waitingForStartup>")]
        [TestCase(SpiritStatusKind.LoadingConfiguration, "<status.loadingConfiguration>")]
        [TestCase(SpiritStatusKind.ConfigurationFailed, "<status.configurationFailed>detail")]
        [TestCase(SpiritStatusKind.ApplicationFailed, "<status.applicationFailed>detail")]
        [TestCase(SpiritStatusKind.ContentFailed, "<status.contentFailed>detail")]
        [TestCase(SpiritStatusKind.EncounterInitializationFailed, "<status.encounterInitializationFailed>detail")]
        [TestCase(SpiritStatusKind.PresentationSceneMissing, "<status.presentationSceneMissing>")]
        [TestCase(SpiritStatusKind.LoadingPresentation, "<status.loadingPresentation>")]
        [TestCase(SpiritStatusKind.PresentationOpenFailed, "<status.presentationOpenFailed>detail")]
        [TestCase(SpiritStatusKind.PresentationInitializationFailed, "<status.presentationInitializationFailed>detail")]
        [TestCase(SpiritStatusKind.MainMenuReady, "<status.mainMenuReady>")]
        [TestCase(SpiritStatusKind.MainMenuFailed, "<status.mainMenuFailed>detail")]
        [TestCase(SpiritStatusKind.SaveSlotsRecovered, "<status.saveSlotsRecovered>")]
        [TestCase(SpiritStatusKind.UnassignedSavesRecovered, "<status.unassignedSavesRecovered>")]
        [TestCase(SpiritStatusKind.SaveSlotsFailed, "<status.saveSlotsFailed>detail")]
        [TestCase(SpiritStatusKind.SaveSlotSelectionFailed, "<status.saveSlotSelectionFailed>detail")]
        [TestCase(SpiritStatusKind.NewGameFailed, "<status.newGameFailed>detail")]
        [TestCase(SpiritStatusKind.PreviousCampaignNotRemoved, "<status.previousCampaignNotRemoved>detail")]
        [TestCase(SpiritStatusKind.CampaignRecoveryFailed, "<status.campaignRecoveryFailed>detail")]
        [TestCase(SpiritStatusKind.InvestigationReady, "<status.investigationReady>")]
        [TestCase(SpiritStatusKind.InvestigationFailed, "<status.investigationFailed>detail")]
        [TestCase(SpiritStatusKind.ChapterNotFound, "<status.chapterNotFound>")]
        [TestCase(SpiritStatusKind.ChapterLocked, "<status.chapterLocked>")]
        [TestCase(SpiritStatusKind.LeadNotFound, "<status.leadNotFound>")]
        [TestCase(SpiritStatusKind.LocationLocked, "<status.locationLocked>")]
        [TestCase(SpiritStatusKind.SavedEncounterHasNoLead, "<status.savedEncounterHasNoLead>")]
        [TestCase(SpiritStatusKind.ActiveEncounterHasNoLead, "<status.activeEncounterHasNoLead>")]
        [TestCase(SpiritStatusKind.InvitationFailed, "<status.invitationFailed>detail")]
        [TestCase(SpiritStatusKind.RewardFailed, "<status.rewardFailed>detail")]
        [TestCase(SpiritStatusKind.ContentNotInitialized, "<status.contentNotInitialized>")]
        [TestCase(SpiritStatusKind.StartFailed, "<status.startFailed>detail")]
        [TestCase(SpiritStatusKind.SaveFailed, "<status.saveFailed>detail")]
        [TestCase(SpiritStatusKind.CatalogRequired, "<status.catalogRequired>")]
        [TestCase(SpiritStatusKind.LoadFailed, "<status.loadFailed>detail")]
        [TestCase(SpiritStatusKind.ActionFailed, "<status.actionFailed>detail")]
        [TestCase(SpiritStatusKind.NoEncounter, "<status.noEncounter>")]
        public void EachStatusIsWordedWithItsDetailOnScreenAndInTheHud(SpiritStatusKind kind, string expected)
        {
            var status = SpiritStatus.Of(kind, "detail");
            Assert.That(SpiritStatusText.Describe(status, wording), Is.EqualTo(expected));
            Assert.That(SpiritStatusText.Gameplay(status, wording), Is.EqualTo(expected));
        }

        /** Checks statuses with typed data fill their templates with that data / fails if a rejection, slot or profile problem is lost. */
        [Test]
        public void StatusesWithTypedDataWordTheirData()
        {
            Assert.That(SpiritStatusText.Describe(SpiritStatus.Rejected(CommandRejection.NoRerollsRemaining), wording),
                Is.EqualTo("<status.actionRejected>NoRerollsRemaining"));
            Assert.That(SpiritStatusText.Describe(SpiritStatus.Recovered(3), wording), Is.EqualTo("<status.campaignRecovered>3"));
            Assert.That(SpiritStatusText.Describe(SpiritStatus.ForProfile(ProfileLoadFailed), wording), Is.EqualTo(LoadFailedWording));
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

        /** Checks a run state's full wording joins its warnings and profile problem with the separator / fails if either is dropped or reordered. */
        [TestCase(SpiritStatusKind.Started, "<status.started>42")]
        [TestCase(SpiritStatusKind.Saved, "<status.saved>")]
        [TestCase(SpiritStatusKind.Loaded, "<status.loaded>")]
        [TestCase(SpiritStatusKind.Ready, "<status.ready>")]
        [TestCase(SpiritStatusKind.ActionAccepted, "<status.actionAccepted>7")]
        public void ARunStateIsWordedWithItsWarningsAndProfileProblem(SpiritStatusKind kind, string expected)
        {
            Assert.That(SpiritStatusText.Describe(Run(kind), wording), Is.EqualTo(expected));
            Assert.That(SpiritStatusText.Describe(Run(kind, "p", "i", ProfileLoadFailed), wording), Is.EqualTo(expected + Separator +
                "<status.handPreviewUnavailable>p" + Separator + "<status.modifierInspectionUnavailable>i" + Separator + LoadFailedWording));
        }

        /** Checks the HUD shows a started run, accepted action or ready encounter only by its warnings / fails if their routine wording shows. */
        [TestCase(SpiritStatusKind.Started)]
        [TestCase(SpiritStatusKind.ActionAccepted)]
        [TestCase(SpiritStatusKind.Ready)]
        public void TheHudShowsRoutineRunStatesOnlyByTheirWarnings(SpiritStatusKind kind)
        {
            Assert.That(SpiritStatusText.Gameplay(Run(kind), wording), Is.Empty);
            Assert.That(SpiritStatusText.Gameplay(Run(kind, profile: ProfileLoadFailed), wording), Is.Empty);
            Assert.That(SpiritStatusText.Gameplay(Run(kind, inspection: "i"), wording), Is.EqualTo("<status.modifierInspectionUnavailable>i"));
            Assert.That(SpiritStatusText.Gameplay(Run(kind, "p", "i", ProfileLoadFailed), wording),
                Is.EqualTo("<status.handPreviewUnavailable>p" + Separator + "<status.modifierInspectionUnavailable>i" + Separator +
                    LoadFailedWording));
        }

        /** Checks the HUD shows a saved or loaded run in full / fails if their wording or warnings are hidden. */
        [TestCase(SpiritStatusKind.Saved, "<status.saved>")]
        [TestCase(SpiritStatusKind.Loaded, "<status.loaded>")]
        public void TheHudShowsSavedAndLoadedRunsInFull(SpiritStatusKind kind, string expected)
        {
            Assert.That(SpiritStatusText.Gameplay(Run(kind), wording), Is.EqualTo(expected));
            Assert.That(SpiritStatusText.Gameplay(Run(kind, "p"), wording), Is.EqualTo(expected + Separator + "<status.handPreviewUnavailable>p"));
        }

        /** Checks pending progress is all the HUD shows when a status reports it / fails if run wording or warnings show beside it. */
        [Test]
        public void TheHudShowsPendingProgressAlone()
        {
            const string pending = "<status.profileSavePending>locked";
            foreach (var kind in new[] { SpiritStatusKind.Started, SpiritStatusKind.Saved, SpiritStatusKind.Loaded, SpiritStatusKind.Ready,
                SpiritStatusKind.ActionAccepted })
                Assert.That(SpiritStatusText.Gameplay(Run(kind, "p", "i", ProfilePending), wording), Is.EqualTo(pending), kind.ToString());
            Assert.That(SpiritStatusText.Gameplay(SpiritStatus.ForProfile(ProfilePending), wording), Is.EqualTo(pending));
            Assert.That(SpiritStatusText.Gameplay(SpiritStatus.ForProfile(ProfileLoadFailed), wording), Is.EqualTo(LoadFailedWording));
        }

        private static SpiritStatus Run(SpiritStatusKind kind, string preview = null, string inspection = null,
            PlayerProfileStatus profile = null) =>
            SpiritStatus.ForRun(kind, 42, 7, preview, inspection, profile ?? PlayerProfileStatus.None);
    }
}
