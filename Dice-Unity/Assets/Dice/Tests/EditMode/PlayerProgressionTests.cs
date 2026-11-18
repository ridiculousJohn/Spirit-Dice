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
        private static readonly ModifierFamilyDefinition.Id GiftFamilyId = TestFamilies.IdOf(GiftFamily);
        private static readonly EncounterJourneyAsset.Id ReturnJourneyId = TestJourneys.IdOf("journey.return");
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
            Assert.That(progression.PersistVisit(TestEncounters.IdOf("encounter.previous")), Is.True);
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
            Assert.That(progression.VisitedEncounterIds, Is.EqualTo(new[] { TestEncounters.IdOf("encounter.previous") }));
        }

        [Test]
        public void PreparationMergesTheSameProfileAndReplacesItWhenTheDirectoryChanges()
        {
            Assert.That(progression.HasProfile, Is.False);
            Assert.That(progression.PrepareProfile(savePath), Is.True);
            Assert.That(progression.HasProfile, Is.True);
            Assert.That(progression.PersistVisit(TestEncounters.IdOf("encounter.first")), Is.True);

            var external = new PlayerProfile();
            external.CompleteJourney(ReturnJourneyId);
            PlayerProfileFileStore.Save(progression.ProfilePath, external);
            string anotherRun = Path.Combine(Path.GetDirectoryName(savePath), "another-run.json");
            Assert.That(progression.PrepareProfile(anotherRun), Is.True);
            Assert.That(progression.VisitedEncounterIds, Is.EqualTo(new[] { TestEncounters.IdOf("encounter.first") }));
            Assert.That(progression.CompletedJourneyIds, Is.EqualTo(new[] { ReturnJourneyId }));

            string differentRun = Path.Combine(Path.GetDirectoryName(savePath), "other-profile", "encounter.json");
            string differentProfile = ProfilePathFor(differentRun);
            var other = new PlayerProfile();
            other.CompleteEncounter(TestEncounters.IdOf("encounter.other"));
            PlayerProfileFileStore.Save(differentProfile, other);
            Assert.That(progression.PrepareProfile(differentRun), Is.True);
            Assert.That(progression.ProfilePath, Is.EqualTo(differentProfile));
            Assert.That(progression.VisitedEncounterIds, Is.Empty);
            Assert.That(progression.CompletedJourneyIds, Is.Empty);
            Assert.That(progression.CompletedEncounterIds, Is.EqualTo(new[] { TestEncounters.IdOf("encounter.other") }));
        }

        [Test]
        public void FailedPreparationRetainsTheProfileAndPath()
        {
            Assert.That(progression.PrepareProfile(savePath), Is.True);
            Assert.That(progression.PersistVisit(TestEncounters.IdOf("encounter.first")), Is.True);
            string originalProfile = progression.ProfilePath;
            string differentRun = Path.Combine(Path.GetDirectoryName(savePath), "corrupt-profile", "encounter.json");
            string differentProfile = ProfilePathFor(differentRun);
            Directory.CreateDirectory(Path.GetDirectoryName(differentProfile));
            const string corrupt = "{ broken profile";
            File.WriteAllText(differentProfile, corrupt);

            Assert.That(progression.PrepareProfile(differentRun), Is.False);
            Assert.That(progression.HasProfile, Is.True);
            Assert.That(progression.ProfilePath, Is.EqualTo(originalProfile));
            Assert.That(progression.VisitedEncounterIds, Is.EqualTo(new[] { TestEncounters.IdOf("encounter.first") }));
            Assert.That(progression.Status.Kind, Is.EqualTo(PlayerProfileStatusKind.LoadFailed));
            Assert.That(File.ReadAllText(differentProfile), Is.EqualTo(corrupt));
        }

        [TestCase("reward")]
        [TestCase("journey")]
        [TestCase("visit")]
        public void TransactionalWriteFailurePreservesProgressUntilTheOperationIsRetried(string operation)
        {
            var catalog = Catalog();
            Assert.That(progression.PrepareProfile(savePath), Is.True);
            Assert.That(progression.PersistVisit(TestEncounters.IdOf("encounter.previous")), Is.True);
            byte[] before = File.ReadAllBytes(progression.ProfilePath);

            using (var lockedProfile = new FileStream(progression.ProfilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.That(ApplyOperation(operation, catalog), Is.False);
                Assert.That(progression.ProfileUnlockedModifierIds, Is.Empty);
                Assert.That(progression.CompletedJourneyIds, Is.Empty);
                Assert.That(progression.CompletedEncounterIds, Is.Empty);
                Assert.That(progression.VisitedEncounterIds, Is.EqualTo(new[] { TestEncounters.IdOf("encounter.previous") }));
                Assert.That(progression.KnownDiscoveredFamilies(catalog), Has.No.Member(GiftFamilyId));
                Assert.That(progression.Status.Kind, Is.EqualTo(FailureOf(operation)));
                Assert.That(progression.Status.Detail, Is.Not.Empty);
            }
            Assert.That(File.ReadAllBytes(progression.ProfilePath), Is.EqualTo(before));
            Assert.That(ApplyOperation(operation, catalog), Is.True);
            Assert.That(progression.Status.Kind, Is.EqualTo(PlayerProfileStatusKind.None));
            var saved = PlayerProfileFileStore.Load(progression.ProfilePath);
            if (operation == "visit")
            {
                Assert.That(saved.VisitedEncounterIds, Is.EqualTo(new[] { TestEncounters.IdOf("encounter.previous"), TestContent.EncounterId }));
                Assert.That(saved.UnlockedModifierIds, Is.Empty);
            }
            else
            {
                Assert.That(saved.UnlockedModifierIds, Is.EqualTo(TestModifiers.IdsOf(GiftId)));
                Assert.That(saved.DiscoveredFamilyIds, Does.Contain(GiftFamilyId));
                if (operation == "journey") Assert.That(saved.CompletedJourneyIds, Is.EqualTo(new[] { ReturnJourneyId }));
                else Assert.That(saved.CompletedJourneyIds, Is.Empty);
            }
        }

        [Test]
        public void RepeatedRewardSucceedsWithoutWritingOrReportingAChange()
        {
            var catalog = Catalog();
            var reward = new ProgressionRewardDefinition(new[] { GiftFamilyId }, TestModifiers.IdsOf(GiftId),
                Array.Empty<ItemDefinition.Id>());
            Assert.That(progression.PrepareProfile(savePath), Is.True);
            Assert.That(progression.ApplyProgressionReward(reward, catalog, out bool changed), Is.True);
            Assert.That(changed, Is.True);
            using (var lockedProfile = new FileStream(progression.ProfilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.That(progression.ApplyProgressionReward(reward, catalog, out changed), Is.True);
                Assert.That(changed, Is.False);
            }
            Assert.That(progression.ProfileUnlockedModifierIds, Is.EqualTo(TestModifiers.IdsOf(GiftId)));
        }

        [Test]
        public void CatalogProjectionsFilterStoredIdsAndRecomputeStarterFamilies()
        {
            var profile = new PlayerProfile();
            profile.UnlockModifier(TestModifiers.IdOf(GiftId));
            profile.UnlockModifier(TestModifiers.IdOf("modifier.future"));
            profile.DiscoverFamily(TestFamilies.IdOf("family.future"));
            PlayerProfileFileStore.Save(ProfilePathFor(savePath), profile);
            Assert.That(progression.PrepareProfile(savePath), Is.True);

            var first = Catalog(Modifier(GiftId, GiftFamily), Modifier("modifier.starter", "family.starter", requiresUnlock: false));
            Assert.That(progression.KnownUnlockedModifiers(first), Is.EqualTo(TestModifiers.IdsOf(GiftId)));
            Assert.That(progression.KnownDiscoveredFamilies(first), Is.EquivalentTo(new[] { GiftFamilyId, TestFamilies.IdOf("family.starter") }));

            var second = Catalog(Modifier("modifier.future", "family.future"),
                Modifier("modifier.other-starter", "family.other-starter", requiresUnlock: false));
            Assert.That(progression.KnownUnlockedModifiers(second), Is.EqualTo(TestModifiers.IdsOf("modifier.future")));
            Assert.That(progression.KnownDiscoveredFamilies(second), Is.EquivalentTo(new[] { TestFamilies.IdOf("family.future"), TestFamilies.IdOf("family.other-starter") }));
            Assert.That(progression.ProfileUnlockedModifierIds, Is.EqualTo(TestModifiers.IdsOf(GiftId, "modifier.future")));
            Assert.That(PlayerProfileFileStore.Load(progression.ProfilePath).DiscoveredFamilyIds, Is.EqualTo(new[] { TestFamilies.IdOf("family.future") }));
        }

        /** Checks the known owned items are the profile's items the catalog has, in catalog order / fails if an unknown item or the profile's order appears. */
        [Test]
        public void KnownOwnedItemsAreTheCatalogsItemsTheProfileOwnsInCatalogOrder()
        {
            var first = new ItemDefinition.Id(new Guid("5a0e3c7b1d2f4e6a8b9c0d1e2f3a4b5c"));
            var second = new ItemDefinition.Id(new Guid("6b1f4d8c2e3a4f7b9c0d1e2f3a4b5c6d"));
            var unknown = new ItemDefinition.Id(new Guid("7c2a5e9d3f4b4a8c0d1e2f3a4b5c6d7e"));
            var profile = new PlayerProfile();
            profile.OwnItem(second);
            profile.OwnItem(unknown);
            profile.OwnItem(first);
            PlayerProfileFileStore.Save(ProfilePathFor(savePath), profile);
            Assert.That(progression.PrepareProfile(savePath), Is.True);
            var catalog = new MechanicalCatalog("test.player-progression", TestContent.Hands(),
                new[] { new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1, 1, 0) }) },
                items: new[] { Item(first, "piece.first"), Item(second, "piece.second") });
            Assert.That(progression.KnownOwnedItems(catalog), Is.EqualTo(new[] { first, second }));
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
                new EncounterApplication(catalog, TestContent.EncounterId, 17, TestModifiers.IdsOf(GiftId)).CaptureSave());
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
            Assert.That(savedProfile.UnlockedModifierIds, Is.EqualTo(TestModifiers.IdsOf(GiftId)));
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
                Assert.That(progression.ProfileUnlockedModifierIds, Is.EqualTo(TestModifiers.IdsOf(GiftId)));
            }

            using (var lockedRun = new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.That(progression.PersistProfile(), Is.True);

            Assert.That(progression.Status.Kind, Is.EqualTo(PlayerProfileStatusKind.None));
            var saved = PlayerProfileFileStore.Load(progression.ProfilePath);
            Assert.That(saved.UnlockedModifierIds, Is.EqualTo(TestModifiers.IdsOf(GiftId)));
            Assert.That(saved.CompletedEncounterIds, Is.EqualTo(new[] { TestContent.EncounterId }));
        }

        private static PlayerProfileStatusKind FailureOf(string operation) =>
            operation == "journey" ? PlayerProfileStatusKind.StoryFailed :
            operation == "visit" ? PlayerProfileStatusKind.VisitFailed : PlayerProfileStatusKind.RewardFailed;

        private bool ApplyOperation(string operation, MechanicalCatalog catalog)
        {
            var reward = new ProgressionRewardDefinition(new[] { GiftFamilyId }, TestModifiers.IdsOf(GiftId),
                Array.Empty<ItemDefinition.Id>());
            if (operation == "journey") return progression.CompleteJourney(ReturnJourneyId, new[] { reward }, catalog);
            if (operation == "visit") return progression.PersistVisit(TestContent.EncounterId);
            bool result = progression.ApplyProgressionReward(reward, catalog, out bool changed);
            Assert.That(changed, Is.EqualTo(result));
            return result;
        }

        private static EncounterApplication WinningApplication(MechanicalCatalog catalog)
        {
            var application = new EncounterApplication(catalog, TestContent.EncounterId, 17, TestModifiers.IdsOf(GiftId));
            Assert.That(application.Submit(PlayHandAction.Instance, application.Snapshot.Revision).Accepted, Is.True);
            Assert.That(application.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Won));
            return application;
        }

        private static string ProfilePathFor(string runPath)
            => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(runPath)), "spirit-profile.json");

        /** A one-encounter catalog of the charms, or of the gift without them, whose one reward pool holds them all. */
        private static MechanicalCatalog Catalog(params ModifierDefinition[] modifiers)
        {
            var charms = modifiers.Length == 0 ? new[] { Modifier(GiftId, GiftFamily) } : modifiers;
            return new MechanicalCatalog("test.player-progression", TestContent.Hands(), new[]
            {
                new EncounterDefinition(TestContent.EncounterId, new[] { new StageDefinition(1, 1, 0) },
                    rewardPolicy: RewardTestContent.Policy(new[] { new ModifierRewardLayout(new[] { RewardTestContent.Pool(charms) }) }))
            }, charms);
        }

        private static ModifierDefinition Modifier(string id, string family, bool requiresUnlock = true)
            => new ModifierDefinition(TestModifiers.IdOf(id), TestFamilies.Of(family), ModifierPhase.PlayHand, new AlwaysCondition(),
                new ModifierEffect[] { new AddBaseScoreEffect(1) },
                availability: new ModifierAvailabilityDefinition(requiresUnlock: requiresUnlock));

        /** An item that places one piece at neutral. */
        private static ItemDefinition Item(ItemDefinition.Id id, string pieceId)
            => new ItemDefinition(id, piece: new TablePieceDefinition(TestPieces.IdOf(pieceId),
                new[] { new PositionInteractionDefinition(PiecePosition.Neutral, PieceMovementTiming.DuringHand) }));
    }
}
