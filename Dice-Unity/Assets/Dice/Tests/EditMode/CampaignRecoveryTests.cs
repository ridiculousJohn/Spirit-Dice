using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class CampaignRecoveryTests
    {
        private string root;
        private string IndexPath => Path.Combine(root, "spirit-slots.json");

        [SetUp]
        public void SetUp()
        {
            root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                "campaign-recovery-tests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown() => TestContext.WriteLine("Retained campaign files: " + root);

        [TestCase(false)]
        [TestCase(true)]
        public void MissingOrCorruptIndexRestoresUniqueMetadataClaims(bool corrupt)
        {
            string first = CreateCampaign(1);
            string third = CreateCampaign(3);
            if (corrupt) File.WriteAllText(IndexPath, "invalid index");
            var slots = new CampaignSaveSlots(root, TestContent.Catalog());
            Assert.That(slots.Load() != null, Is.EqualTo(corrupt));
            Assert.That(slots.GetProfilePath(1), Is.EqualTo(ProfilePath(first)));
            Assert.That(slots.HasGame(2), Is.False);
            Assert.That(slots.GetProfilePath(3), Is.EqualTo(ProfilePath(third)));
            Assert.That(slots.RecoveryCampaigns, Is.Empty);
            Assert.That(Load().GetProfilePath(3), Is.EqualTo(ProfilePath(third)));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RecoverySelectsTheFirstOccupiedSlot(bool corrupt)
        {
            string third = CreateCampaign(3);
            if (corrupt) File.WriteAllText(IndexPath, "invalid index");
            var slots = Load();
            Assert.That(slots.GetProfilePath(3), Is.EqualTo(ProfilePath(third)));
            Assert.That(slots.ActiveSlot, Is.EqualTo(3));
            Assert.That(Load().ActiveSlot, Is.EqualTo(3));
        }

        [Test]
        public void PublishedRunOnlyCampaignWithoutMetadataRemainsSelectable()
        {
            string id = Guid.NewGuid().ToString("N");
            string directory = Path.Combine(root, id);
            Directory.CreateDirectory(directory);
            string run = Path.Combine(directory, "spirit-encounter.json");
            EncounterSaveFileStore.Save(run,
                EncounterSession.CreateEncounter(TestContent.Catalog(), TestContent.EncounterId, 42).CaptureSave());
            var slots = Load();
            Assert.That(slots.HasGame(1) || slots.HasGame(2) || slots.HasGame(3), Is.False);
            var candidate = slots.RecoveryCampaigns.Single();
            Assert.That(candidate.CampaignId, Is.EqualTo(id));
            Assert.That(candidate.SuggestedSlot, Is.Zero);
            Assert.That(candidate.LastWriteTimeUtc, Is.EqualTo(File.GetLastWriteTimeUtc(run)));
            slots.RecoverCampaign(id, 2);
            Assert.That(slots.GetSavePath(2), Is.EqualTo(run));
            Assert.That(Load().GetSavePath(2), Is.EqualTo(run));
            Assert.That(File.Exists(ProfilePath(id)), Is.False);
        }

        [Test]
        public void ReadableRunDoesNotMakeAPresentCorruptProfileRecoverable()
        {
            string id = CreateCampaign(1);
            EncounterSaveFileStore.Save(Path.Combine(root, id, "spirit-encounter.json"),
                EncounterSession.CreateEncounter(TestContent.Catalog(), TestContent.EncounterId, 42).CaptureSave());
            File.WriteAllText(ProfilePath(id), "invalid profile");
            var slots = Load();
            Assert.That(slots.RecoveryCampaigns, Is.Empty);
            Assert.That(slots.HasGame(1), Is.False);
            Assert.That(File.ReadAllText(ProfilePath(id)), Is.EqualTo("invalid profile"));
        }

        [Test]
        public void ConflictingClaimsAndUnmarkedCampaignsRemainSelectableAcrossRestart()
        {
            string first = CreateCampaign(2);
            string second = CreateCampaign(2);
            string unmarked = CreateCampaign();
            File.SetLastWriteTimeUtc(ProfilePath(first), DateTime.UtcNow.AddDays(-5));
            var slots = Load();
            Assert.That(slots.HasGame(1) || slots.HasGame(2) || slots.HasGame(3), Is.False);
            Assert.That(slots.RecoveryCampaigns.Select(value => value.CampaignId), Is.EquivalentTo(new[] { first, second, unmarked }));
            Assert.That(slots.RecoveryCampaigns.Single(value => value.CampaignId == unmarked).SuggestedSlot, Is.Zero);
            Assert.That(Load().RecoveryCampaigns.Select(value => value.CampaignId), Is.EquivalentTo(new[] { first, second, unmarked }));
        }

        [Test]
        public void ValidIndexBackfillsMissingCampaignMetadata()
        {
            string id = CreateCampaign();
            WriteIndex(null, id, null);
            var slots = Load();
            Assert.That(slots.RecoveryCampaigns, Is.Empty);
            Assert.That(JObject.Parse(File.ReadAllText(Path.Combine(root, id, "spirit-campaign.json")))["slot"].Value<int>(), Is.EqualTo(2));
            File.WriteAllText(IndexPath, "invalid index");
            slots.Load();
            Assert.That(slots.GetProfilePath(2), Is.EqualTo(ProfilePath(id)));
        }

        [Test]
        public void UnreadableAuxiliaryMetadataDoesNotBlockAValidAssignedIndex()
        {
            string id = CreateCampaign(1);
            WriteIndex(id, null, null);
            string metadata = Path.Combine(root, id, "spirit-campaign.json");
            using (var locked = new FileStream(metadata, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var slots = Load();
                Assert.That(slots.GetProfilePath(1), Is.EqualTo(ProfilePath(id)));
                Assert.That(slots.RecoveryCampaigns, Is.Empty);
            }
        }

        [Test]
        public void ManualAssignmentPersistsAndKeepsTheDisplacedCampaignRecoverable()
        {
            string occupied = CreateCampaign(1);
            string candidate = CreateCampaign();
            WriteIndex(occupied, null, null);
            var slots = Load();
            var info = slots.RecoveryCampaigns.Single();
            Assert.That(info.CampaignId, Is.EqualTo(candidate));
            Assert.That(info.ProfilePath, Is.EqualTo(ProfilePath(candidate)));
            Assert.That(info.SavePath, Is.EqualTo(Path.Combine(root, candidate, "spirit-encounter.json")));
            Assert.That(info.LastWriteTimeUtc, Is.EqualTo(File.GetLastWriteTimeUtc(info.ProfilePath)));
            slots.RecoverCampaign(candidate, 1);
            Assert.That(slots.GetProfilePath(1), Is.EqualTo(ProfilePath(candidate)));
            Assert.That(slots.RecoveryCampaigns.Single().CampaignId, Is.EqualTo(occupied));
            Assert.That(File.Exists(ProfilePath(occupied)), Is.True);
            slots = Load();
            Assert.That(slots.GetProfilePath(1), Is.EqualTo(ProfilePath(candidate)));
            Assert.That(slots.RecoveryCampaigns.Single().CampaignId, Is.EqualTo(occupied));
            Assert.That(JObject.Parse(File.ReadAllText(Path.Combine(root, candidate, "spirit-campaign.json")))["slot"].Value<int>(), Is.EqualTo(1));
        }

        [Test]
        public void FailedManualAssignmentKeepsTheCommittedMappingAndAllCampaignFiles()
        {
            string occupied = CreateCampaign(1);
            string candidate = CreateCampaign();
            WriteIndex(occupied, null, null);
            var slots = Load();
            byte[] index = File.ReadAllBytes(IndexPath);
            Directory.CreateDirectory(IndexPath + ".bak");
            Assert.Catch<Exception>(() => slots.RecoverCampaign(candidate, 1));
            Assert.That(File.ReadAllBytes(IndexPath), Is.EqualTo(index));
            Assert.That(slots.GetProfilePath(1), Is.EqualTo(ProfilePath(occupied)));
            Assert.That(slots.RecoveryCampaigns.Single().CampaignId, Is.EqualTo(candidate));
            Assert.That(File.Exists(ProfilePath(occupied)), Is.True);
            Assert.That(File.Exists(ProfilePath(candidate)), Is.True);
        }

        [Test]
        public void RecoveryRevalidatesTheChosenCampaignAndRejectsUnknownIdentifiers()
        {
            string candidate = CreateCampaign();
            var slots = Load();
            Assert.Throws<ArgumentException>(() => slots.RecoverCampaign("../outside", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => slots.RecoverCampaign(candidate, 4));
            File.WriteAllText(ProfilePath(candidate), "invalid profile");
            Assert.Throws<FormatException>(() => slots.RecoverCampaign(candidate, 1));
            Assert.That(slots.HasGame(1), Is.False);
        }

        [Test]
        public void InvalidProfileOrPresentInvalidRunIsPreservedWithoutAssignment()
        {
            string invalidProfile = CreateCampaign(1);
            string invalidRun = CreateCampaign(2);
            string valid = CreateCampaign(3);
            File.WriteAllText(ProfilePath(invalidProfile), "invalid profile");
            string runPath = Path.Combine(root, invalidRun, "spirit-encounter.json");
            File.WriteAllText(runPath, "invalid run");
            var slots = Load();
            Assert.That(slots.HasGame(1), Is.False);
            Assert.That(slots.HasGame(2), Is.False);
            Assert.That(slots.GetProfilePath(3), Is.EqualTo(ProfilePath(valid)));
            Assert.That(slots.RecoveryCampaigns, Is.Empty);
            Assert.That(File.ReadAllText(ProfilePath(invalidProfile)), Is.EqualTo("invalid profile"));
            Assert.That(File.ReadAllText(runPath), Is.EqualTo("invalid run"));
        }

        [Test]
        public void UnreadableRecoverySaveIsAnIoErrorWithoutReplacingTheIndex()
        {
            var slots = Load();
            slots.StartNewGame(2);
            string candidate = CreateCampaign();
            byte[] index = File.ReadAllBytes(IndexPath);
            using (var locked = new FileStream(ProfilePath(candidate), FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.Throws<IOException>(() => slots.Load());
            Assert.That(slots.ActiveSlot, Is.EqualTo(2));
            Assert.That(File.ReadAllBytes(IndexPath), Is.EqualTo(index));
            Assert.That(Directory.GetFiles(root, "spirit-slots.corrupt-*.json"), Is.Empty);
        }

        [Test]
        public void NewGameCleanupFailureReturnsAWarningAfterTheNewMappingIsCommitted()
        {
            var slots = Load();
            slots.StartNewGame(1);
            string previous = slots.GetProfilePath(1);
            string warning;
            using (var locked = new FileStream(previous, FileMode.Open, FileAccess.Read, FileShare.None))
                warning = slots.StartNewGame(1);
            Assert.That(warning, Is.Not.Null.And.Not.Empty);
            Assert.That(slots.GetProfilePath(1), Is.Not.EqualTo(previous));
            Assert.That(Load().GetProfilePath(1), Is.EqualTo(slots.GetProfilePath(1)));
        }

        [Test]
        public void RecoveryAndNewGameLeaveUnresolvedAndForeignDirectoriesUntouched()
        {
            string unresolved = CreateCampaign();
            string foreign = Path.Combine(root, "unrelated-data");
            Directory.CreateDirectory(foreign);
            File.WriteAllText(Path.Combine(foreign, "keep.txt"), "keep");
            var slots = Load();
            slots.StartNewGame(1);
            slots.StartNewGame(1);
            Assert.That(File.Exists(ProfilePath(unresolved)), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(foreign, "keep.txt")), Is.EqualTo("keep"));
            Assert.That(Load().RecoveryCampaigns.Single().CampaignId, Is.EqualTo(unresolved));
        }

        [Test]
        public void PruningBackupsPreservesFilesOutsideTheExactGeneratedNamingPattern()
        {
            string unknown = Path.Combine(root, "spirit-slots.corrupt-user-copy.json");
            File.WriteAllText(unknown, "keep");
            var slots = Load();
            File.WriteAllText(IndexPath, "first invalid index");
            string first = slots.Load();
            File.WriteAllText(IndexPath, "second invalid index");
            string second = slots.Load();
            Assert.That(File.Exists(first), Is.False);
            Assert.That(File.ReadAllText(second), Is.EqualTo("second invalid index"));
            Assert.That(File.ReadAllText(unknown), Is.EqualTo("keep"));
        }

        [Test]
        public void InterruptedRecoveryRetainsEveryBackupUntilARecoveryCommitSucceeds()
        {
            var slots = Load();
            File.WriteAllText(IndexPath, "first invalid index");
            string first = slots.Load();
            File.WriteAllText(IndexPath, "second invalid index");
            using (var locked = new FileStream(IndexPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.Throws<IOException>(() => slots.Load());
            string[] interrupted = Directory.GetFiles(root, "spirit-slots.corrupt-*.json");
            Assert.That(interrupted.Length, Is.EqualTo(2));
            Assert.That(interrupted, Does.Contain(first));
            Assert.That(File.ReadAllText(first), Is.EqualTo("first invalid index"));
            Assert.That(File.ReadAllText(interrupted.Single(path => path != first)), Is.EqualTo("second invalid index"));
            string recovered = slots.Load();
            Assert.That(Directory.GetFiles(root, "spirit-slots.corrupt-*.json"), Is.EqualTo(new[] { recovered }));
            Assert.That(File.ReadAllText(recovered), Is.EqualTo("second invalid index"));
        }

        [Test]
        public void ReparseCampaignDirectoriesAreExcludedFromRecovery()
        {
            string outside = Path.Combine(root, "outside");
            Directory.CreateDirectory(outside);
            File.WriteAllBytes(Path.Combine(outside, "spirit-profile.json"), PlayerProfileCodec.Serialize(new PlayerProfile()));
            string linked = Path.Combine(root, Guid.NewGuid().ToString("N"));
            CreateJunction(linked, outside);
            var slots = Load();
            Assert.That(slots.RecoveryCampaigns, Is.Empty);
            Assert.That(slots.HasGame(1) || slots.HasGame(2) || slots.HasGame(3), Is.False);
            Assert.That(File.Exists(Path.Combine(outside, "spirit-profile.json")), Is.True);
        }

        [Test]
        public void ReparseDescendantPreventsDeletionAfterTheReplacementIsCommitted()
        {
            var slots = Load();
            slots.StartNewGame(1);
            string previous = Path.GetDirectoryName(slots.GetProfilePath(1));
            string outside = Path.Combine(root, "outside");
            Directory.CreateDirectory(outside);
            string sentinel = Path.Combine(outside, "keep.txt");
            File.WriteAllText(sentinel, "keep");
            CreateJunction(Path.Combine(previous, "linked"), outside);
            Assert.That(slots.StartNewGame(1), Is.Not.Null);
            Assert.That(Path.GetDirectoryName(slots.GetProfilePath(1)), Is.Not.EqualTo(previous));
            Assert.That(Directory.Exists(previous), Is.True);
            Assert.That(slots.RecoveryCampaigns.Single().CampaignId, Is.EqualTo(Path.GetFileName(previous)));
            Assert.That(File.ReadAllText(sentinel), Is.EqualTo("keep"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CampaignStorageWorksThroughRedirectedRootOrParent(bool redirectedParent)
        {
            string target = Path.Combine(root, "target");
            Directory.CreateDirectory(target);
            string linked = Path.Combine(root, "linked-root");
            CreateJunction(linked, target);
            string saveRoot = redirectedParent ? Path.Combine(linked, "saves") : linked;
            Directory.CreateDirectory(saveRoot);
            string sentinel = Path.Combine(target, "keep.txt");
            File.WriteAllText(sentinel, "keep");

            var slots = new CampaignSaveSlots(saveRoot, TestContent.Catalog());
            Assert.That(slots.Load(), Is.Null);
            Assert.That(slots.StartNewGame(1), Is.Null);
            Assert.That(slots.StartNewGame(2), Is.Null);
            string profilePath = slots.GetProfilePath(2);
            PlayerProfileFileStore.Save(profilePath, PlayerProfileFileStore.Load(profilePath));
            var save = EncounterSession.CreateEncounter(TestContent.Catalog(), TestContent.EncounterId, 42).CaptureSave();
            EncounterSaveFileStore.Save(slots.GetSavePath(2), save);

            slots = new CampaignSaveSlots(saveRoot, TestContent.Catalog());
            Assert.That(slots.Load(), Is.Null);
            Assert.That(slots.ActiveSlot, Is.EqualTo(2));
            Assert.That(EncounterSaveCodec.Serialize(EncounterSaveFileStore.Load(slots.GetSavePath(2), TestContent.Catalog())),
                Is.EqualTo(EncounterSaveCodec.Serialize(save)));

            string index = Path.Combine(saveRoot, "spirit-slots.json");
            File.WriteAllText(index, "invalid index");
            Assert.That(slots.Load(), Is.Not.Null);
            Assert.That(slots.GetProfilePath(2), Is.EqualTo(profilePath));
            File.WriteAllText(index, "another invalid index");
            string retainedBackup = slots.Load();
            Assert.That(Directory.GetFiles(saveRoot, "spirit-slots.corrupt-*.json"), Is.EqualTo(new[] { retainedBackup }));

            string previous = Path.GetDirectoryName(profilePath);
            Assert.That(slots.StartNewGame(2), Is.Null);
            Assert.That(Directory.Exists(previous), Is.False);
            Assert.That(File.Exists(slots.GetProfilePath(1)), Is.True);
            Assert.That(File.Exists(slots.GetProfilePath(2)), Is.True);
            Assert.That(File.ReadAllText(sentinel), Is.EqualTo("keep"));
        }

        private static void CreateJunction(string path, string target)
        {
            if (Application.platform != RuntimePlatform.WindowsEditor) Assert.Ignore("Directory junction coverage requires Windows.");
            var start = new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + path + "\" \"" + target + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var process = Process.Start(start))
            {
                process.WaitForExit();
                Assert.That(process.ExitCode, Is.Zero, process.StandardError.ReadToEnd());
            }
        }

        private string CreateCampaign(int slot = 0)
        {
            string id = Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(Path.Combine(root, id));
            File.WriteAllBytes(ProfilePath(id), PlayerProfileCodec.Serialize(new PlayerProfile()));
            if (slot != 0)
                File.WriteAllText(Path.Combine(root, id, "spirit-campaign.json"),
                    new JObject { ["formatVersion"] = 1, ["slot"] = slot }.ToString());
            return id;
        }

        private string ProfilePath(string id) => Path.Combine(root, id, "spirit-profile.json");

        private void WriteIndex(params string[] campaigns) => File.WriteAllText(IndexPath,
            new JObject { ["formatVersion"] = 1, ["activeSlot"] = 1, ["campaigns"] = new JArray(campaigns) }.ToString());

        private CampaignSaveSlots Load()
        {
            var slots = new CampaignSaveSlots(root, TestContent.Catalog());
            slots.Load();
            return slots;
        }
    }
}
