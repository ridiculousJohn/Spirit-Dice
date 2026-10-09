using System;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class SpiritProgressionTests
    {
        private const string UnlockedId = "modifier.permanent";
        private GameObject host;
        private SpiritEncounterController controller;
        private string savePath;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Spirit progression test");
            host.SetActive(false);
            controller = host.AddComponent<SpiritEncounterController>();
            host.GetComponent<SpiritEncounterView>().enabled = false;
            string directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..",
                "Docs", "work", "spirit-progression-tests", Guid.NewGuid().ToString("N")));
            savePath = Path.Combine(directory, "encounter.json");
            TestContext.WriteLine("Retained progression test files: " + directory);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [Test]
        public void MilestonePersistsBeforePlaybackAndSurvivesLossRestartAndOlderRunLoad()
        {
            var catalog = Catalog();
            controller.Initialize(catalog, TestContent.EncounterId, 17, savePath);
            controller.Save();
            int unlockEvents = 0;
            controller.Resolved += resolution =>
            {
                unlockEvents += resolution.Events.OfType<ModifierUnlockedEvent>().Count();
                if (resolution.Snapshot.NewlyUnlockedModifierIds.Count != 0)
                    Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).UnlockedModifierIds, Does.Contain(UnlockedId));
                controller.CompletePresentation();
            };
            controller.PlayHand();
            Assert.That(controller.Snapshot.UnlockedModifierIds, Does.Contain(UnlockedId));
            Assert.That(controller.Snapshot.NewlyUnlockedModifierIds, Is.EqualTo(new[] { UnlockedId }));
            controller.PlayHand();
            Assert.That(controller.Snapshot.Outcome, Is.EqualTo(EncounterOutcome.Lost));
            controller.StartNew();
            Assert.That(controller.Snapshot.UnlockedModifierIds, Does.Contain(UnlockedId));
            Assert.That(controller.Snapshot.NewlyUnlockedModifierIds, Is.Empty);
            controller.Load();
            Assert.That(controller.Snapshot.Revision, Is.Zero);
            Assert.That(controller.Snapshot.UnlockedModifierIds, Does.Contain(UnlockedId));
            controller.PlayHand();
            Assert.That(unlockEvents, Is.EqualTo(1));
            Assert.That(controller.Snapshot.NewlyUnlockedModifierIds, Is.Empty);
        }

        [Test]
        public void FinalWinImmediatelyPersistsUnlockAndUniqueCompletion()
        {
            controller.Initialize(Catalog(finalWin: true), TestContent.EncounterId, 17, savePath);
            controller.Resolved += resolution =>
            {
                var profile = PlayerProfileFileStore.Load(controller.ProfilePath);
                Assert.That(profile.CompletedEncounterIds, Is.EqualTo(new[] { TestContent.EncounterId }));
                Assert.That(profile.UnlockedModifierIds, Is.EqualTo(new[] { UnlockedId }));
                controller.CompletePresentation();
            };
            controller.PlayHand();
            Assert.That(File.Exists(savePath), Is.False);
            controller.StartNew();
            controller.PlayHand();
            Assert.That(controller.CompletedEncounterIds, Is.EqualTo(new[] { TestContent.EncounterId }));
        }

        [Test]
        public void CorruptProfileBlocksInitializationWithoutOverwritingIt()
        {
            string profilePath = Path.Combine(Path.GetDirectoryName(savePath), "spirit-profile.json");
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllText(profilePath, "{ broken profile");
            controller.Initialize(Catalog(), TestContent.EncounterId, 17, savePath);
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.StatusMessage, Does.Contain("Cannot load player profile"));
            Assert.That(File.ReadAllText(profilePath), Is.EqualTo("{ broken profile"));
        }

        [Test]
        public void FailedProfileWriteRetainsUnlockAndBlocksRunReplacementAndFurtherActions()
        {
            var catalog = Catalog();
            controller.Initialize(catalog, TestContent.EncounterId, 17, savePath);
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllText(controller.ProfilePath, "{ corrupt external profile");
            controller.PlayHand();
            var before = controller.Snapshot;
            Assert.That(controller.ProfileUnlockedModifierIds, Does.Contain(UnlockedId));
            Assert.That(controller.StatusMessage, Does.Contain("Progress is pending"));
            controller.StartNew();
            controller.Load();
            controller.Initialize(catalog, TestContent.EncounterId, 42, savePath);
            controller.PlayHand();
            Assert.That(controller.Snapshot, Is.SameAs(before));
            Assert.That(controller.ProfileUnlockedModifierIds, Does.Contain(UnlockedId));
            Assert.That(File.ReadAllText(controller.ProfilePath), Is.EqualTo("{ corrupt external profile"));
        }

        [Test]
        public void ValidSavedRunRecoversSeededPermanentUnlocksWhenProfileIsMissing()
        {
            var catalog = Catalog();
            var seeded = new EncounterApplication(catalog, TestContent.EncounterId, 17, new[] { UnlockedId });
            EncounterSaveFileStore.Save(savePath, seeded.CaptureSave());
            controller.Initialize(catalog, TestContent.EncounterId, 42, savePath);
            Assert.That(File.Exists(controller.ProfilePath), Is.False);
            Assert.That(controller.Snapshot.UnlockedModifierIds, Is.Empty);
            controller.Load();
            Assert.That(controller.Snapshot.UnlockedModifierIds, Is.EqualTo(new[] { UnlockedId }));
            Assert.That(controller.Snapshot.NewlyUnlockedModifierIds, Is.Empty);
            Assert.That(PlayerProfileFileStore.Load(controller.ProfilePath).UnlockedModifierIds, Is.EqualTo(new[] { UnlockedId }));
            controller.StartNew();
            Assert.That(controller.Snapshot.UnlockedModifierIds, Is.EqualTo(new[] { UnlockedId }));
        }

        [Test]
        public void UnknownProfileIdsRemainStoredButAreNotInjectedIntoTheCatalog()
        {
            string profilePath = Path.Combine(Path.GetDirectoryName(savePath), "spirit-profile.json");
            var profile = new PlayerProfile();
            profile.UnlockModifier("modifier.future");
            profile.CompleteEncounter("encounter.future");
            PlayerProfileFileStore.Save(profilePath, profile);
            controller.Initialize(Catalog(finalWin: true), TestContent.EncounterId, 17, savePath);
            Assert.That(controller.Snapshot.UnlockedModifierIds, Is.Empty);
            controller.PlayHand();
            profile = PlayerProfileFileStore.Load(profilePath);
            Assert.That(profile.UnlockedModifierIds, Is.EquivalentTo(new[] { "modifier.future", UnlockedId }));
            Assert.That(profile.CompletedEncounterIds, Is.EquivalentTo(new[] { "encounter.future", TestContent.EncounterId }));
        }

        private static MechanicalCatalog Catalog(bool finalWin = false)
        {
            var modifier = new ModifierDefinition(UnlockedId, "family.permanent", ModifierPhase.PlayHand,
                new AlwaysCondition(), new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 1) },
                availability: new ModifierAvailabilityDefinition(access: ModifierAccess.PermanentUnlock));
            var stages = finalWin ? new[] { new StageDefinition(1, 1, 0) } :
                new[] { new StageDefinition(1, 1, 0), new StageDefinition(1000000, 1, 0) };
            var encounter = new EncounterDefinition(TestContent.EncounterId, stages,
                unlockMilestones: new[] { new ModifierUnlockMilestone(1, new[] { UnlockedId }) });
            return new MechanicalCatalog("test.progression.v1", TestContent.Hands(), new[] { encounter }, new[] { modifier });
        }
    }
}
