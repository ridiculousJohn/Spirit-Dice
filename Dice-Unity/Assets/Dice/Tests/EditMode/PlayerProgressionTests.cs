using System;
using System.IO;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;

namespace Dice.Tests
{
    public sealed class PlayerProgressionTests
    {
        private const string GiftId = "modifier.gift";
        private const string GiftFamily = "family.gift";
        private const string StoryId = "story.return";
        private PlayerProgression progression;
        private string savePath;

        [SetUp]
        public void SetUp()
        {
            progression = new PlayerProgression();
            string directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..",
                "Docs", "work", "player-progression-tests", Guid.NewGuid().ToString("N")));
            savePath = Path.Combine(directory, "encounter.json");
            TestContext.WriteLine("Retained player progression test files: " + directory);
        }

        [Test]
        public void ClearReleasesProfileAndPendingWritesWithoutTouchingFiles()
        {
            var catalog = Catalog();
            Assert.That(progression.PrepareProfile(savePath), Is.True);
            Assert.That(progression.PersistVisit("encounter.previous"), Is.True);
            byte[] profileBytes = File.ReadAllBytes(progression.ProfilePath);
            var application = WinningApplication(catalog);
            EncounterSaveFileStore.Save(savePath, new EncounterApplication(catalog, TestContent.EncounterId, 31).CaptureSave());
            byte[] runBytes = File.ReadAllBytes(savePath);
            using (var lockedRun = new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.That(progression.RecordProgression(application.Snapshot, catalog, null, application.CaptureSave(), savePath), Is.False);
                progression.Clear();
                Assert.That(progression.PersistProfile(), Is.True);
                Assert.That(progression.HasProfile, Is.False);
                Assert.That(progression.ProfilePath, Is.Null);
                Assert.That(progression.Status.Kind, Is.EqualTo(PlayerProfileStatusKind.None));
                Assert.That(progression.ProfileUnlockedModifierIds, Is.Empty);
                Assert.That(progression.VisitedEncounterIds, Is.Empty);
            }
            Assert.That(File.ReadAllBytes(savePath), Is.EqualTo(runBytes));
            Assert.That(File.ReadAllBytes(ProfilePathFor(savePath)), Is.EqualTo(profileBytes));
            Assert.That(progression.PrepareProfile(savePath), Is.True);
            Assert.That(progression.ProfileUnlockedModifierIds, Is.Empty);
            Assert.That(progression.VisitedEncounterIds, Is.EqualTo(new[] { "encounter.previous" }));
        }

        [Test]
        public void PreparationMergesTheSameProfileAndReplacesItWhenTheDirectoryChanges()
        {
            Assert.That(progression.HasProfile, Is.False);
            Assert.That(progression.PrepareProfile(savePath), Is.True);
            Assert.That(progression.HasProfile, Is.True);
            Assert.That(progression.PersistVisit("encounter.first"), Is.True);

            var external = new PlayerProfile();
            external.CompleteStory(StoryId);
            PlayerProfileFileStore.Save(progression.ProfilePath, external);
            string anotherRun = Path.Combine(Path.GetDirectoryName(savePath), "another-run.json");
            Assert.That(progression.PrepareProfile(anotherRun), Is.True);
            Assert.That(progression.VisitedEncounterIds, Is.EqualTo(new[] { "encounter.first" }));
            Assert.That(progression.CompletedStoryIds, Is.EqualTo(new[] { StoryId }));

            string differentRun = Path.Combine(Path.GetDirectoryName(savePath), "other-profile", "encounter.json");
            string differentProfile = ProfilePathFor(differentRun);
            var other = new PlayerProfile();
            other.CompleteEncounter("encounter.other");
            PlayerProfileFileStore.Save(differentProfile, other);
            Assert.That(progression.PrepareProfile(differentRun), Is.True);
            Assert.That(progression.ProfilePath, Is.EqualTo(differentProfile));
            Assert.That(progression.VisitedEncounterIds, Is.Empty);
            Assert.That(progression.CompletedStoryIds, Is.Empty);
            Assert.That(progression.CompletedEncounterIds, Is.EqualTo(new[] { "encounter.other" }));
        }

        [Test]
        public void FailedPreparationRetainsTheProfileAndPath()
        {
            Assert.That(progression.PrepareProfile(savePath), Is.True);
            Assert.That(progression.PersistVisit("encounter.first"), Is.True);
            string originalProfile = progression.ProfilePath;
            string differentRun = Path.Combine(Path.GetDirectoryName(savePath), "corrupt-profile", "encounter.json");
            string differentProfile = ProfilePathFor(differentRun);
            Directory.CreateDirectory(Path.GetDirectoryName(differentProfile));
            const string corrupt = "{ broken profile";
            File.WriteAllText(differentProfile, corrupt);

            Assert.That(progression.PrepareProfile(differentRun), Is.False);
            Assert.That(progression.HasProfile, Is.True);
            Assert.That(progression.ProfilePath, Is.EqualTo(originalProfile));
            Assert.That(progression.VisitedEncounterIds, Is.EqualTo(new[] { "encounter.first" }));
            Assert.That(progression.Status.Kind, Is.EqualTo(PlayerProfileStatusKind.LoadFailed));
            Assert.That(File.ReadAllText(differentProfile), Is.EqualTo(corrupt));
        }

        [TestCase("reward")]
        [TestCase("story")]
        [TestCase("visit")]
        public void TransactionalWriteFailurePreservesProgressUntilTheOperationIsRetried(string operation)
        {
            var catalog = Catalog();
            Assert.That(progression.PrepareProfile(savePath), Is.True);
            Assert.That(progression.PersistVisit("encounter.previous"), Is.True);
            byte[] before = File.ReadAllBytes(progression.ProfilePath);

            using (var lockedProfile = new FileStream(progression.ProfilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.That(ApplyOperation(operation, catalog), Is.False);
                Assert.That(progression.ProfileUnlockedModifierIds, Is.Empty);
                Assert.That(progression.CompletedStoryIds, Is.Empty);
                Assert.That(progression.CompletedEncounterIds, Is.Empty);
                Assert.That(progression.VisitedEncounterIds, Is.EqualTo(new[] { "encounter.previous" }));
                Assert.That(progression.KnownDiscoveredFamilies(catalog), Does.Not.Contain(GiftFamily));
                Assert.That(progression.Status.Kind, Is.EqualTo(FailureOf(operation)));
                Assert.That(progression.Status.Detail, Is.Not.Empty);
            }
            Assert.That(File.ReadAllBytes(progression.ProfilePath), Is.EqualTo(before));
            Assert.That(ApplyOperation(operation, catalog), Is.True);
            Assert.That(progression.Status.Kind, Is.EqualTo(PlayerProfileStatusKind.None));
            var saved = PlayerProfileFileStore.Load(progression.ProfilePath);
            if (operation == "visit")
            {
                Assert.That(saved.VisitedEncounterIds, Is.EqualTo(new[] { "encounter.previous", TestContent.EncounterId }));
                Assert.That(saved.UnlockedModifierIds, Is.Empty);
            }
            else
            {
                Assert.That(saved.UnlockedModifierIds, Is.EqualTo(new[] { GiftId }));
                Assert.That(saved.DiscoveredFamilyIds, Does.Contain(GiftFamily));
                if (operation == "story") Assert.That(saved.CompletedStoryIds, Is.EqualTo(new[] { StoryId }));
                else Assert.That(saved.CompletedStoryIds, Is.Empty);
            }
        }

        [Test]
        public void RepeatedRewardSucceedsWithoutWritingOrReportingAChange()
        {
            var catalog = Catalog();
            var reward = new ProgressionRewardDefinition(new[] { GiftFamily }, new[] { GiftId });
            Assert.That(progression.PrepareProfile(savePath), Is.True);
            Assert.That(progression.ApplyProgressionReward(reward, catalog, out bool changed), Is.True);
            Assert.That(changed, Is.True);
            using (var lockedProfile = new FileStream(progression.ProfilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.That(progression.ApplyProgressionReward(reward, catalog, out changed), Is.True);
                Assert.That(changed, Is.False);
            }
            Assert.That(progression.ProfileUnlockedModifierIds, Is.EqualTo(new[] { GiftId }));
        }

        [Test]
        public void CatalogProjectionsFilterStoredIdsAndRecomputeStarterFamilies()
        {
            var profile = new PlayerProfile();
            profile.UnlockModifier(GiftId);
            profile.UnlockModifier("modifier.future");
            profile.DiscoverFamily("family.future");
            PlayerProfileFileStore.Save(ProfilePathFor(savePath), profile);
            Assert.That(progression.PrepareProfile(savePath), Is.True);

            var first = Catalog(Modifier(GiftId, GiftFamily), Modifier("modifier.starter", "family.starter", ModifierAccess.Starter));
            Assert.That(progression.KnownUnlockedModifiers(first), Is.EqualTo(new[] { GiftId }));
            Assert.That(progression.KnownDiscoveredFamilies(first), Is.EquivalentTo(new[] { GiftFamily, "family.starter" }));

            var second = Catalog(Modifier("modifier.future", "family.future"),
                Modifier("modifier.other-starter", "family.other-starter", ModifierAccess.Starter));
            Assert.That(progression.KnownUnlockedModifiers(second), Is.EqualTo(new[] { "modifier.future" }));
            Assert.That(progression.KnownDiscoveredFamilies(second), Is.EquivalentTo(new[] { "family.future", "family.other-starter" }));
            Assert.That(progression.ProfileUnlockedModifierIds, Is.EqualTo(new[] { GiftId, "modifier.future" }));
            Assert.That(PlayerProfileFileStore.Load(progression.ProfilePath).DiscoveredFamilyIds, Is.EqualTo(new[] { "family.future" }));
        }

        [Test]
        public void PendingRunWriteBlocksProfileReplacementUntilTheOriginalRunAndProfileAreSaved()
        {
            var catalog = Catalog();
            Assert.That(progression.PrepareProfile(savePath), Is.True);
            PlayerProfileFileStore.Save(progression.ProfilePath, new PlayerProfile());
            string originalProfile = progression.ProfilePath;
            var application = WinningApplication(catalog);
            var savedRun = application.CaptureSave();
            EncounterSaveFileStore.Save(savePath,
                new EncounterApplication(catalog, TestContent.EncounterId, 17, new[] { GiftId }).CaptureSave());
            string otherRun = Path.Combine(Path.GetDirectoryName(savePath), "other-profile", "encounter.json");

            using (var lockedRun = new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.That(progression.RecordProgression(application.Snapshot, catalog, null, savedRun, savePath), Is.False);
                Assert.That(PlayerProfileFileStore.Load(originalProfile).UnlockedModifierIds, Is.Empty);
                Assert.That(progression.PrepareProfile(otherRun), Is.False);
                Assert.That(progression.ProfilePath, Is.EqualTo(originalProfile));
                Assert.That(progression.Status.Kind, Is.EqualTo(PlayerProfileStatusKind.EncounterSavePending));
            }

            Assert.That(progression.PrepareProfile(otherRun), Is.True);
            Assert.That(EncounterSaveFileStore.Load(savePath, catalog).Outcome, Is.EqualTo(EncounterOutcome.Won));
            var savedProfile = PlayerProfileFileStore.Load(originalProfile);
            Assert.That(savedProfile.UnlockedModifierIds, Is.EqualTo(new[] { GiftId }));
            Assert.That(savedProfile.CompletedEncounterIds, Is.EqualTo(new[] { TestContent.EncounterId }));
            Assert.That(progression.ProfilePath, Is.EqualTo(ProfilePathFor(otherRun)));
            Assert.That(progression.ProfileUnlockedModifierIds, Is.Empty);
            Assert.That(progression.CompletedEncounterIds, Is.Empty);
            Assert.That(File.Exists(otherRun), Is.False);
        }

        [Test]
        public void ProfileRetryDoesNotRewriteAnAlreadySavedUnlockRun()
        {
            var catalog = Catalog();
            Assert.That(progression.PrepareProfile(savePath), Is.True);
            PlayerProfileFileStore.Save(progression.ProfilePath, new PlayerProfile());
            var application = WinningApplication(catalog);

            using (var lockedProfile = new FileStream(progression.ProfilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.That(progression.RecordProgression(application.Snapshot, catalog, null, application.CaptureSave(), savePath), Is.False);
                Assert.That(EncounterSaveFileStore.Load(savePath, catalog).Outcome, Is.EqualTo(EncounterOutcome.Won));
                Assert.That(progression.Status.Kind, Is.EqualTo(PlayerProfileStatusKind.ProfileSavePending));
                Assert.That(progression.ProfileUnlockedModifierIds, Is.EqualTo(new[] { GiftId }));
            }

            using (var lockedRun = new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.That(progression.PersistProfile(), Is.True);

            Assert.That(progression.Status.Kind, Is.EqualTo(PlayerProfileStatusKind.None));
            var saved = PlayerProfileFileStore.Load(progression.ProfilePath);
            Assert.That(saved.UnlockedModifierIds, Is.EqualTo(new[] { GiftId }));
            Assert.That(saved.CompletedEncounterIds, Is.EqualTo(new[] { TestContent.EncounterId }));
        }

        private static PlayerProfileStatusKind FailureOf(string operation) =>
            operation == "story" ? PlayerProfileStatusKind.StoryFailed :
            operation == "visit" ? PlayerProfileStatusKind.VisitFailed : PlayerProfileStatusKind.RewardFailed;

        private bool ApplyOperation(string operation, MechanicalCatalog catalog)
        {
            var reward = new ProgressionRewardDefinition(new[] { GiftFamily }, new[] { GiftId });
            if (operation == "story") return progression.CompleteStoryJourney(StoryId, new[] { reward }, catalog);
            if (operation == "visit") return progression.PersistVisit(TestContent.EncounterId);
            bool result = progression.ApplyProgressionReward(reward, catalog, out bool changed);
            Assert.That(changed, Is.EqualTo(result));
            return result;
        }

        private static EncounterApplication WinningApplication(MechanicalCatalog catalog)
        {
            var application = new EncounterApplication(catalog, TestContent.EncounterId, 17, new[] { GiftId });
            Assert.That(application.Submit(PlayHandAction.Instance, application.Snapshot.Revision).Accepted, Is.True);
            Assert.That(application.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            return application;
        }

        private static string ProfilePathFor(string runPath)
            => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(runPath)), "spirit-profile.json");

        private static MechanicalCatalog Catalog(params ModifierDefinition[] modifiers)
            => new MechanicalCatalog("test.player-progression", TestContent.Hands(), new[]
            {
                new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1, 1, 0) })
            }, modifiers.Length == 0 ? new[] { Modifier(GiftId, GiftFamily) } : modifiers);

        private static ModifierDefinition Modifier(string id, string family, ModifierAccess access = ModifierAccess.PermanentUnlock)
            => new ModifierDefinition(id, family, ModifierPhase.PlayHand, new AlwaysCondition(),
                new ModifierEffect[] { new AddBaseScoreEffect(1) },
                availability: new ModifierAvailabilityDefinition(access: access));
    }
}
