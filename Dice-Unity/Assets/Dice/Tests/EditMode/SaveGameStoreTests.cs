using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Editor;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class SaveGameStoreTests
    {
        private string folder;
        private string SaveRoot => Path.Combine(folder, "saves");
        private string BackupRoot => Path.Combine(folder, "backups");

        [SetUp]
        public void SetUp()
        {
            folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work", "save-game-tests",
                Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(SaveRoot);
        }

        [TearDown]
        public void TearDown() => TestContext.WriteLine("Retained save game files: " + folder);

        [Test]
        public void EverythingBeforeAJourneyCompletesEveryEarlierJourneyAndLeavesItAvailableAndNotStarted()
        {
            var content = SaveGameContent.Load();
            var store = new SaveGameStore(SaveRoot, BackupRoot, content.Catalog);
            Assert.That(store.StartNewGame(1), Is.Null);
            var journeys = content.Journeys.Entries;
            var chosen = journeys[journeys.Count - 1];

            store.WriteEverythingBefore(1, content.Journeys, chosen);

            var slots = LoadSlots(content.Catalog);
            var profile = PlayerProfileFileStore.Load(slots.GetProfilePath(1));
            var progress = new EncounterJourneyNavigation.Progress(true, profile.VisitedEncounterIds, profile.CompletedJourneyIds,
                profile.CompletedEncounterIds);
            var navigation = new EncounterJourneyNavigation();
            navigation.InitializeJourney(content.Journeys, progress);
            foreach (var journey in journeys.Take(journeys.Count - 1))
            {
                Assert.That(profile.CompletedJourneyIds, Has.Member(journey.Identity), journey.name);
                foreach (var encounter in journey.Encounters)
                {
                    Assert.That(profile.CompletedEncounterIds, Has.Member(encounter.Identity), encounter.name);
                    Assert.That(profile.UnlockedModifierIds,
                        Is.SupersetOf(content.Catalog.GetEncounter(encounter.Identity).UnlockRewardModifierIds), encounter.name);
                }
                var rewards = journey.Beats.Select(beat => beat.ProgressionReward).Append(journey.CompletionReward)
                    .Where(reward => reward != null);
                foreach (var reward in rewards.Select(reward => reward.Build(content.Catalog)))
                {
                    Assert.That(profile.DiscoveredFamilyIds, Is.SupersetOf(reward.DiscoveredFamilyIds), journey.name);
                    Assert.That(profile.UnlockedModifierIds, Is.SupersetOf(reward.GrantedModifierIds), journey.name);
                }
            }
            Assert.That(navigation.IsJourneyAvailable(chosen, progress), Is.True);
            Assert.That(profile.CompletedJourneyIds, Has.No.Member(chosen.Identity));
            foreach (var encounter in chosen.Encounters)
            {
                Assert.That(profile.VisitedEncounterIds, Has.No.Member(encounter.Identity), encounter.name);
                Assert.That(profile.CompletedEncounterIds, Has.No.Member(encounter.Identity), encounter.name);
            }
            Assert.That(File.Exists(slots.GetSavePath(1)), Is.False);
        }

        [Test]
        public void AProfileEditRoundTripsThroughTheGameStore()
        {
            var catalog = TestContent.Catalog();
            var store = new SaveGameStore(SaveRoot, BackupRoot, catalog);
            Assert.That(store.StartNewGame(1), Is.Null);
            var journey = new EncounterJourneyAsset.Id(Guid.NewGuid());
            var family = new ModifierFamilyDefinition.Id(Guid.NewGuid());
            var charm = new ModifierDefinition.Id(Guid.NewGuid());

            var read = store.ReadProfile(1);
            var edit = new SaveGameProfileEdit(read.Profile);
            edit.CompletedJourneys.Set(journey, true);
            edit.CompletedEncounters.Set(TestContent.EncounterId, true);
            edit.VisitedEncounters.Set(TestContent.EncounterId, true);
            edit.DiscoveredFamilies.Set(family, true);
            edit.UnlockedCharms.Set(charm, true);
            store.SaveProfile(read, edit.Build());

            var saved = PlayerProfileFileStore.Load(LoadSlots(catalog).GetProfilePath(1));
            Assert.That(saved.CompletedJourneyIds, Is.EqualTo(new[] { journey }));
            Assert.That(saved.CompletedEncounterIds, Is.EqualTo(new[] { TestContent.EncounterId }));
            Assert.That(saved.VisitedEncounterIds, Is.EqualTo(new[] { TestContent.EncounterId }));
            Assert.That(saved.DiscoveredFamilyIds, Is.EqualTo(new[] { family }));
            Assert.That(saved.UnlockedModifierIds, Is.EqualTo(new[] { charm }));

            read = store.ReadProfile(1);
            edit = new SaveGameProfileEdit(read.Profile);
            edit.CompletedJourneys.Set(journey, false);
            edit.UnlockedCharms.Set(charm, false);
            store.SaveProfile(read, edit.Build());

            var trimmed = PlayerProfileFileStore.Load(LoadSlots(catalog).GetProfilePath(1));
            Assert.That(trimmed.CompletedJourneyIds, Is.Empty);
            Assert.That(trimmed.UnlockedModifierIds, Is.Empty);
            Assert.That(trimmed.CompletedEncounterIds, Is.EqualTo(new[] { TestContent.EncounterId }));
            Assert.That(trimmed.VisitedEncounterIds, Is.EqualTo(new[] { TestContent.EncounterId }));
            Assert.That(trimmed.DiscoveredFamilyIds, Is.EqualTo(new[] { family }));
        }

        [Test]
        public void ClearMovesTheSavesIntoTheBackupAndLeavesTheRootWithoutSaves()
        {
            var catalog = TestContent.Catalog();
            var store = new SaveGameStore(SaveRoot, BackupRoot, catalog);
            Assert.That(store.StartNewGame(1), Is.Null);
            Assert.That(store.StartNewGame(2), Is.Null);
            EncounterSaveFileStore.Save(LoadSlots(catalog).GetSavePath(1), TestContent.Session().CaptureSave());
            string unrelated = Path.Combine(SaveRoot, "unrelated.txt");
            File.WriteAllText(unrelated, "not a save");
            var saves = Files(SaveRoot).Where(file => !file.StartsWith(Path.GetFileName(unrelated) + "|", StringComparison.Ordinal)).ToList();
            Assert.That(saves, Is.Not.Empty);

            string backup = store.Clear();

            Assert.That(store.SaveEntries(), Is.Empty);
            Assert.That(Directory.GetFileSystemEntries(SaveRoot), Is.EqualTo(new[] { unrelated }));
            string moved = Path.Combine(backup, SaveGameStore.MovedFolderName);
            Assert.That(Files(moved), Is.EqualTo(saves));
            string movedPrefix = SaveGameStore.MovedFolderName + Path.DirectorySeparatorChar;
            Assert.That(Files(backup).Where(file => !file.StartsWith(movedPrefix, StringComparison.Ordinal)), Is.EqualTo(saves));
        }

        private CampaignSaveSlots LoadSlots(MechanicalCatalog catalog)
        {
            var slots = new CampaignSaveSlots(SaveRoot, catalog);
            Assert.That(slots.Load(), Is.Null);
            return slots;
        }

        /** Every file under the folder as its relative path and its contents, in path order. */
        private static List<string> Files(string directory)
            => Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .Select(path => path.Substring(directory.Length + 1) + "|" + Convert.ToBase64String(File.ReadAllBytes(path)))
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToList();
    }
}
