using System;
using System.IO;
using System.Linq;
using System.Text;
using Dice.App;
using Dice.Engine;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class CampaignSaveSlotsTests
    {
        private string root;
        private string IndexPath => Path.Combine(root, "spirit-slots.json");

        [SetUp]
        public void SetUp()
        {
            root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                "campaign-slot-tests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown() => TestContext.WriteLine("Retained campaign files: " + root);

        [Test]
        public void FreshIndexHasThreeEmptySlotsAndSelectionDoesNotCreateACampaign()
        {
            var slots = Load();
            Assert.That(slots.ActiveSlot, Is.EqualTo(1));
            for (int slot = 1; slot <= CampaignSaveSlots.SlotCount; ++slot)
            {
                Assert.That(slots.HasGame(slot), Is.False);
                Assert.That(slots.GetSavePath(slot), Is.Null);
                Assert.That(slots.GetProfilePath(slot), Is.Null);
            }
            slots.SelectSlot(3);
            Assert.That(Load().ActiveSlot, Is.EqualTo(3));
            Assert.That(Directory.GetDirectories(root), Is.Empty);
            Assert.That(Load().HasGame(3), Is.False);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(4)]
        public void EverySlotOperationRejectsOutOfRangeSlots(int slot)
        {
            var slots = Load();
            byte[] before = File.ReadAllBytes(IndexPath);
            Assert.Throws<ArgumentOutOfRangeException>(() => slots.HasGame(slot));
            Assert.Throws<ArgumentOutOfRangeException>(() => slots.GetSavePath(slot));
            Assert.Throws<ArgumentOutOfRangeException>(() => slots.GetProfilePath(slot));
            Assert.Throws<ArgumentOutOfRangeException>(() => slots.SelectSlot(slot));
            Assert.Throws<ArgumentOutOfRangeException>(() => slots.StartNewGame(slot));
            Assert.That(File.ReadAllBytes(IndexPath), Is.EqualTo(before));
            Assert.That(Directory.GetDirectories(root), Is.Empty);
        }

        [Test]
        public void OperationsRequireALoadedIndex()
        {
            var slots = new CampaignSaveSlots(root, TestContent.Catalog());
            Assert.Throws<InvalidOperationException>(() => { _ = slots.ActiveSlot; });
            Assert.Throws<InvalidOperationException>(() => slots.HasGame(1));
            Assert.Throws<InvalidOperationException>(() => slots.GetSavePath(1));
            Assert.Throws<InvalidOperationException>(() => slots.SelectSlot(1));
            Assert.Throws<InvalidOperationException>(() => slots.StartNewGame(1));
        }

        [Test]
        public void ThreeCampaignsKeepIndependentRunAndProfileProgressAfterReload()
        {
            var slots = Load();
            var paths = new string[CampaignSaveSlots.SlotCount];
            var saves = new byte[CampaignSaveSlots.SlotCount][];
            for (int slot = 1; slot <= CampaignSaveSlots.SlotCount; ++slot)
            {
                slots.StartNewGame(slot);
                Assert.That(slots.ActiveSlot, Is.EqualTo(slot));
                Assert.That(slots.HasGame(slot), Is.True);
                paths[slot - 1] = slots.GetSavePath(slot);
                var profile = new PlayerProfile();
                profile.UnlockModifier("modifier." + slot);
                profile.DiscoverFamily("family." + slot);
                profile.CompleteEncounter("encounter." + slot);
                profile.CompleteStory("story." + slot);
                profile.VisitEncounter("visited." + slot);
                PlayerProfileFileStore.Save(slots.GetProfilePath(slot), profile);
                var session = EncounterSession.CreateEncounter(TestContent.Catalog(), TestContent.EncounterId, (ulong)slot);
                Assert.That(session.Execute(new RerollDiceAction(3), 0).Accepted, Is.True);
                var save = session.CaptureSave();
                EncounterSaveFileStore.Save(paths[slot - 1], save);
                saves[slot - 1] = EncounterSaveCodec.Serialize(save);
            }
            Assert.That(paths.Distinct().Count(), Is.EqualTo(CampaignSaveSlots.SlotCount));
            slots = Load();
            Assert.That(slots.ActiveSlot, Is.EqualTo(3));
            for (int slot = 1; slot <= CampaignSaveSlots.SlotCount; ++slot)
            {
                slots.SelectSlot(slot);
                Assert.That(slots.GetSavePath(slot), Is.EqualTo(paths[slot - 1]));
                Assert.That(EncounterSaveCodec.Serialize(EncounterSaveFileStore.Load(paths[slot - 1], TestContent.Catalog())), Is.EqualTo(saves[slot - 1]));
                var profile = PlayerProfileFileStore.Load(slots.GetProfilePath(slot));
                Assert.That(profile.UnlockedModifierIds, Is.EqualTo(new[] { "modifier." + slot }));
                Assert.That(profile.DiscoveredFamilyIds, Is.EqualTo(new[] { "family." + slot }));
                Assert.That(profile.CompletedEncounterIds, Is.EqualTo(new[] { "encounter." + slot }));
                Assert.That(profile.CompletedStoryIds, Is.EqualTo(new[] { "story." + slot }));
                Assert.That(profile.VisitedEncounterIds, Is.EqualTo(new[] { "visited." + slot }));
            }
        }

        [Test]
        public void ReplacingCampaignStartsWithEmptyProfileAndNoRunAndRemovesOnlyTheReplacedCampaign()
        {
            var slots = Load();
            slots.StartNewGame(1);
            slots.StartNewGame(2);
            string previousRun = slots.GetSavePath(1);
            string previousProfile = slots.GetProfilePath(1);
            var profile = new PlayerProfile();
            profile.UnlockModifier("modifier.previous");
            profile.CompleteStory("story.previous");
            PlayerProfileFileStore.Save(previousProfile, profile);
            var save = EncounterSession.CreateEncounter(TestContent.Catalog(), TestContent.EncounterId, 17).CaptureSave();
            EncounterSaveFileStore.Save(previousRun, save);
            string otherProfile = slots.GetProfilePath(2);
            byte[] otherBytes = File.ReadAllBytes(otherProfile);

            Assert.That(slots.StartNewGame(1), Is.Null);
            Assert.That(slots.ActiveSlot, Is.EqualTo(1));
            Assert.That(slots.HasGame(1), Is.True);
            Assert.That(slots.GetSavePath(1), Is.Not.EqualTo(previousRun));
            Assert.That(File.Exists(slots.GetSavePath(1)), Is.False);
            AssertEmptyProfile(PlayerProfileFileStore.Load(slots.GetProfilePath(1)));
            Assert.That(Directory.Exists(Path.GetDirectoryName(previousProfile)), Is.False);
            Assert.That(slots.GetProfilePath(2), Is.EqualTo(otherProfile));
            Assert.That(File.ReadAllBytes(otherProfile), Is.EqualTo(otherBytes));
            Assert.That(slots.HasGame(3), Is.False);
            var restored = Load();
            Assert.That(restored.GetSavePath(1), Is.EqualTo(slots.GetSavePath(1)));
            Assert.That(File.Exists(restored.GetSavePath(1)), Is.False);
            AssertEmptyProfile(PlayerProfileFileStore.Load(restored.GetProfilePath(1)));
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void LegacyFilesAreCopiedOnceToSlotOneWithoutModifyingTheirBytes(bool hasRun, bool hasProfile)
        {
            string legacyRun = Path.Combine(root, "spirit-encounter.json");
            string legacyProfile = Path.Combine(root, "spirit-profile.json");
            byte[] runBytes = EncounterSaveCodec.Serialize(
                EncounterSession.CreateEncounter(TestContent.Catalog(), TestContent.EncounterId, 42).CaptureSave());
            var profile = new PlayerProfile();
            profile.CompleteStory("story.legacy");
            byte[] profileBytes = PlayerProfileCodec.Serialize(profile);
            if (hasRun) File.WriteAllBytes(legacyRun, runBytes);
            if (hasProfile) File.WriteAllBytes(legacyProfile, profileBytes);
            var slots = Load();
            Assert.That(slots.ActiveSlot, Is.EqualTo(1));
            Assert.That(slots.HasGame(1), Is.True);
            Assert.That(slots.HasGame(2), Is.False);
            Assert.That(slots.HasGame(3), Is.False);
            Assert.That(File.Exists(slots.GetSavePath(1)), Is.EqualTo(hasRun));
            Assert.That(File.Exists(slots.GetProfilePath(1)), Is.EqualTo(hasProfile));
            if (hasRun)
            {
                Assert.That(File.ReadAllBytes(legacyRun), Is.EqualTo(runBytes));
                Assert.That(File.ReadAllBytes(slots.GetSavePath(1)), Is.EqualTo(runBytes));
            }
            if (hasProfile)
            {
                Assert.That(File.ReadAllBytes(legacyProfile), Is.EqualTo(profileBytes));
                Assert.That(File.ReadAllBytes(slots.GetProfilePath(1)), Is.EqualTo(profileBytes));
            }
            string firstRun = slots.GetSavePath(1);
            slots.StartNewGame(1);
            string replacementRun = slots.GetSavePath(1);
            var restored = Load();
            Assert.That(restored.GetSavePath(1), Is.EqualTo(replacementRun).And.Not.EqualTo(firstRun));
            Assert.That(File.Exists(restored.GetSavePath(1)), Is.False);
            AssertEmptyProfile(PlayerProfileFileStore.Load(restored.GetProfilePath(1)));
            Assert.That(Directory.GetDirectories(root).Length, Is.EqualTo(1));
        }

        [Test]
        public void CorruptLegacyProfileIsPreservedAndReportedByTheProfileLoader()
        {
            string legacyProfile = Path.Combine(root, "spirit-profile.json");
            byte[] corrupt = Encoding.UTF8.GetBytes("invalid profile");
            File.WriteAllBytes(legacyProfile, corrupt);
            var slots = Load();
            string migratedProfile = slots.GetProfilePath(1);
            Assert.That(File.ReadAllBytes(migratedProfile), Is.EqualTo(corrupt));
            Assert.Throws<FormatException>(() => PlayerProfileFileStore.Load(migratedProfile));
            Assert.That(Load().GetProfilePath(1), Is.EqualTo(migratedProfile));
            slots.StartNewGame(1);
            AssertEmptyProfile(PlayerProfileFileStore.Load(slots.GetProfilePath(1)));
            Assert.That(File.Exists(migratedProfile), Is.False);
            Assert.That(File.ReadAllBytes(legacyProfile), Is.EqualTo(corrupt));
        }

        [TestCase("version")]
        [TestCase("active-zero")]
        [TestCase("active-four")]
        [TestCase("active-string")]
        [TestCase("count")]
        [TestCase("missing")]
        [TestCase("unknown")]
        [TestCase("absolute")]
        [TestCase("relative")]
        [TestCase("empty")]
        [TestCase("number")]
        [TestCase("duplicate-directory")]
        [TestCase("duplicate-field")]
        [TestCase("trailing")]
        [TestCase("oversized")]
        [TestCase("malformed")]
        [TestCase("root-array")]
        [TestCase("blank")]
        [TestCase("invalid-utf8")]
        [TestCase("integer-overflow")]
        public void InvalidIndexIsPreservedBeforeRecoveringEmptySlots(string invalid)
        {
            var slots = Load();
            slots.StartNewGame(2);
            string originalRun = slots.GetSavePath(2);
            string originalProfile = slots.GetProfilePath(2);
            byte[] runBytes = { 0, 1, 2, 255 };
            File.WriteAllBytes(originalRun, runBytes);
            byte[] profileBytes = File.ReadAllBytes(originalProfile);
            string[] originalDirectories = Directory.GetDirectories(root);
            var document = JObject.Parse(File.ReadAllText(IndexPath));
            if (invalid == "version") document["formatVersion"] = 2;
            if (invalid == "active-zero") document["activeSlot"] = 0;
            if (invalid == "active-four") document["activeSlot"] = 4;
            if (invalid == "active-string") document["activeSlot"] = "2";
            if (invalid == "count") document["campaigns"] = new JArray();
            if (invalid == "missing") document.Remove("campaigns");
            if (invalid == "unknown") document["external"] = "path";
            if (invalid == "absolute") document["campaigns"][0] = "C:/saves";
            if (invalid == "relative") document["campaigns"][0] = "../saves";
            if (invalid == "empty") document["campaigns"][0] = "";
            if (invalid == "number") document["campaigns"][0] = 12;
            if (invalid == "duplicate-directory") document["campaigns"][0] = document["campaigns"][1].Value<string>().ToUpperInvariant();
            string json = document.ToString();
            if (invalid == "duplicate-field") json = json.Replace("\"activeSlot\": 2", "\"activeSlot\": 2, \"activeSlot\": 1");
            if (invalid == "trailing") json += " {}";
            if (invalid == "oversized") json += new string(' ', 4096);
            if (invalid == "malformed") json = "{invalid";
            if (invalid == "root-array") json = "[]";
            if (invalid == "blank") json = "";
            if (invalid == "integer-overflow") json = json.Replace("\"activeSlot\": 2", "\"activeSlot\": 9223372036854775808");
            File.WriteAllText(IndexPath, json);
            if (invalid == "invalid-utf8") File.WriteAllBytes(IndexPath, new byte[] { 0xef, 0xbb, 0xbf, 0xff });
            byte[] invalidBytes = File.ReadAllBytes(IndexPath);

            string backupPath = slots.Load();
            Assert.That(Path.GetDirectoryName(backupPath), Is.EqualTo(root));
            Assert.That(Path.GetFileName(backupPath), Does.StartWith("spirit-slots.corrupt-").And.EndWith(".json"));
            Assert.That(File.ReadAllBytes(backupPath), Is.EqualTo(invalidBytes));
            AssertEmptySlots(slots);
            Assert.That(File.ReadAllBytes(originalRun), Is.EqualTo(runBytes));
            Assert.That(File.ReadAllBytes(originalProfile), Is.EqualTo(profileBytes));
            Assert.That(Directory.GetDirectories(root), Is.EquivalentTo(originalDirectories));
            Assert.That(slots.Load(), Is.Null);
            AssertEmptySlots(Load());
            Assert.That(Directory.GetFiles(root, "spirit-slots.corrupt-*.json"), Is.EqualTo(new[] { backupPath }));
        }

        [Test]
        public void RecoveryKeepsLegacyFilesAndFreshSlotsRemainUsableAfterReload()
        {
            byte[] legacyRun = Encoding.UTF8.GetBytes("legacy run");
            byte[] legacyProfile = Encoding.UTF8.GetBytes("legacy profile");
            string legacyRunPath = Path.Combine(root, "spirit-encounter.json");
            string legacyProfilePath = Path.Combine(root, "spirit-profile.json");
            File.WriteAllBytes(legacyRunPath, legacyRun);
            File.WriteAllBytes(legacyProfilePath, legacyProfile);
            File.WriteAllText(IndexPath, "invalid index");
            var slots = new CampaignSaveSlots(root, TestContent.Catalog());
            Assert.That(slots.Load(), Is.Not.Null);
            AssertEmptySlots(slots);
            Assert.That(Directory.GetDirectories(root), Is.Empty);
            Assert.That(File.ReadAllBytes(legacyRunPath), Is.EqualTo(legacyRun));
            Assert.That(File.ReadAllBytes(legacyProfilePath), Is.EqualTo(legacyProfile));

            slots.StartNewGame(3);
            string profilePath = slots.GetProfilePath(3);
            AssertEmptyProfile(PlayerProfileFileStore.Load(profilePath));
            slots = Load();
            Assert.That(slots.ActiveSlot, Is.EqualTo(3));
            Assert.That(slots.HasGame(1), Is.False);
            Assert.That(slots.HasGame(2), Is.False);
            Assert.That(slots.GetProfilePath(3), Is.EqualTo(profilePath));
            Assert.That(File.Exists(slots.GetSavePath(3)), Is.False);
            Assert.That(Directory.GetDirectories(root).Length, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedCorruptionRetainsOnlyTheLatestGeneratedBackup()
        {
            var slots = Load();
            byte[] first = Encoding.UTF8.GetBytes("first corrupt index");
            File.WriteAllBytes(IndexPath, first);
            string firstBackup = slots.Load();
            byte[] second = Encoding.UTF8.GetBytes("second corrupt index");
            File.WriteAllBytes(IndexPath, second);
            string secondBackup = slots.Load();
            Assert.That(secondBackup, Is.Not.EqualTo(firstBackup));
            Assert.That(File.Exists(firstBackup), Is.False);
            Assert.That(File.ReadAllBytes(secondBackup), Is.EqualTo(second));
            Assert.That(Directory.GetFiles(root, "spirit-slots.corrupt-*.json"), Is.EqualTo(new[] { secondBackup }));
            AssertEmptySlots(Load());
        }

        [Test]
        public void FailedRecoveryCommitRetainsCorruptIndexBackupAndLoadedMapping()
        {
            var slots = Load();
            slots.StartNewGame(2);
            string originalRun = slots.GetSavePath(2);
            string originalProfile = slots.GetProfilePath(2);
            byte[] profile = File.ReadAllBytes(originalProfile);
            byte[] corrupt = Encoding.UTF8.GetBytes("invalid index");
            File.WriteAllBytes(IndexPath, corrupt);

            using (var locked = new FileStream(IndexPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.Throws<IOException>(() => slots.Load());
            Assert.That(slots.ActiveSlot, Is.EqualTo(2));
            Assert.That(slots.GetSavePath(2), Is.EqualTo(originalRun));
            Assert.That(slots.GetProfilePath(2), Is.EqualTo(originalProfile));
            Assert.That(File.ReadAllBytes(originalProfile), Is.EqualTo(profile));
            Assert.That(File.ReadAllBytes(IndexPath), Is.EqualTo(corrupt));
            string[] backups = Directory.GetFiles(root, "spirit-slots.corrupt-*.json");
            Assert.That(backups.Length, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(backups[0]), Is.EqualTo(corrupt));
        }

        [Test]
        public void LockedIndexRemainsAnAccessErrorWithoutRecovery()
        {
            var slots = Load();
            slots.StartNewGame(2);
            string originalRun = slots.GetSavePath(2);
            byte[] originalIndex = File.ReadAllBytes(IndexPath);
            using (var locked = new FileStream(IndexPath, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.Throws<IOException>(() => slots.Load());
            Assert.That(slots.ActiveSlot, Is.EqualTo(2));
            Assert.That(slots.GetSavePath(2), Is.EqualTo(originalRun));
            Assert.That(File.ReadAllBytes(IndexPath), Is.EqualTo(originalIndex));
            Assert.That(Directory.GetFiles(root, "spirit-slots.corrupt-*.json"), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedIndexCommitRetainsSelectionMappingAndPreparedFiles(bool startNewGame)
        {
            File.WriteAllBytes(Path.Combine(root, "spirit-profile.json"), PlayerProfileCodec.Serialize(new PlayerProfile()));
            var slots = Load();
            string originalRun = slots.GetSavePath(1);
            string originalProfile = slots.GetProfilePath(1);
            byte[] originalIndex = File.ReadAllBytes(IndexPath);
            Directory.CreateDirectory(IndexPath + ".bak");
            Assert.Catch<Exception>(() =>
            {
                if (startNewGame) slots.StartNewGame(1);
                else slots.SelectSlot(2);
            });
            Assert.That(slots.ActiveSlot, Is.EqualTo(1));
            Assert.That(slots.GetSavePath(1), Is.EqualTo(originalRun));
            Assert.That(slots.GetProfilePath(1), Is.EqualTo(originalProfile));
            Assert.That(slots.HasGame(2), Is.False);
            Assert.That(File.ReadAllBytes(IndexPath), Is.EqualTo(originalIndex));
            Assert.That(Load().HasGame(2), Is.False);
            Assert.That(Load().ActiveSlot, Is.EqualTo(1));
            Assert.That(Load().GetSavePath(1), Is.EqualTo(originalRun));
            Assert.That(Directory.GetFiles(root, "spirit-slots.json.tmp-*").Length, Is.EqualTo(1));
            string[] preparedProfiles = Directory.GetFiles(root, "spirit-profile.json", SearchOption.AllDirectories);
            Assert.That(preparedProfiles.Length, Is.EqualTo(startNewGame ? 3 : 2));
            foreach (string path in preparedProfiles) AssertEmptyProfile(PlayerProfileFileStore.Load(path));
        }

        [Test]
        public void IndexDirectoryIsAnAccessErrorAndDoesNotStartMigration()
        {
            string legacyProfile = Path.Combine(root, "spirit-profile.json");
            byte[] profile = PlayerProfileCodec.Serialize(new PlayerProfile());
            File.WriteAllBytes(legacyProfile, profile);
            Directory.CreateDirectory(IndexPath);
            var slots = new CampaignSaveSlots(root, TestContent.Catalog());
            Assert.Catch<Exception>(() => slots.Load());
            Assert.Throws<InvalidOperationException>(() => { _ = slots.ActiveSlot; });
            Assert.That(File.ReadAllBytes(legacyProfile), Is.EqualTo(profile));
            string[] prepared = Directory.GetFiles(root, "spirit-profile.json", SearchOption.AllDirectories);
            Assert.That(prepared.Length, Is.EqualTo(1));
            foreach (string path in prepared) Assert.That(File.ReadAllBytes(path), Is.EqualTo(profile));
            Assert.That(Directory.GetFiles(root, "spirit-slots.json.tmp-*").Length, Is.EqualTo(0));
        }

        private CampaignSaveSlots Load()
        {
            var slots = new CampaignSaveSlots(root, TestContent.Catalog());
            slots.Load();
            return slots;
        }

        private static void AssertEmptySlots(CampaignSaveSlots slots)
        {
            Assert.That(slots.ActiveSlot, Is.EqualTo(1));
            for (int slot = 1; slot <= CampaignSaveSlots.SlotCount; ++slot)
            {
                Assert.That(slots.HasGame(slot), Is.False);
                Assert.That(slots.GetSavePath(slot), Is.Null);
                Assert.That(slots.GetProfilePath(slot), Is.Null);
            }
        }

        private static void AssertEmptyProfile(PlayerProfile profile)
        {
            Assert.That(profile.UnlockedModifierIds, Is.Empty);
            Assert.That(profile.DiscoveredFamilyIds, Is.Empty);
            Assert.That(profile.CompletedEncounterIds, Is.Empty);
            Assert.That(profile.CompletedStoryIds, Is.Empty);
            Assert.That(profile.VisitedEncounterIds, Is.Empty);
        }
    }
}
