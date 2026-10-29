using System;
using System.Collections.Generic;
using System.IO;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    /** Main menu campaign actions, run on test-only content in an isolated campaign directory. */
    public sealed class CampaignMenuActionsTests
    {
        private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        private readonly SyntheticCharmAssets content = new SyntheticCharmAssets();
        private GameObject host;
        private SpiritEncounterController controller;
        private SpiritCampaignController campaign;
        private MechanicalCatalog catalog;
        private EncounterJourneyCatalogAsset journeys;
        private string root;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Campaign menu test");
            host.SetActive(false);
            controller = host.AddComponent<SpiritEncounterController>();
            campaign = host.AddComponent<SpiritCampaignController>();
            catalog = TestContent.Catalog();
            var journey = TestJourneys.Identify(Create<EncounterJourneyAsset>(), "journey.test");
            SyntheticUi.Set(journey, "encounter", content.Encounter("encounter.test"));
            SyntheticUi.Set(journey, "locationTitle", "Test location");
            SyntheticUi.Set(journey, "spiritName", "Test host");
            SyntheticUi.Set(journey, "leadText", "A test lead.");
            SyntheticUi.Set(journey, "tablePresentation", Create<EncounterTablePresentationAsset>());
            TestJourneys.SetBeats(journey, TestJourneys.Beat("invitation", EncounterJourneyBeatKind.Invitation));
            journeys = TestJourneys.Catalog(Create<EncounterJourneyCatalogAsset>(),
                TestJourneys.Chapter(Create<EncounterChapterAsset>(), "chapter.test", journey));
            root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                "campaign-menu-tests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            TestContext.WriteLine("Retained campaign files: " + root);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(host);
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();
            content.Dispose();
        }

        /** Checks Continue needs a run save and Chapter needs a campaign in the active slot; fails if either is offered without them. */
        [Test]
        public void ContinueNeedsARunSaveAndChapterNeedsACampaign()
        {
            Assert.That(campaign.Initialize(catalog, journeys, root), Is.True);
            Assert.That(campaign.CanStartNewGame, Is.True);
            Assert.That(campaign.CanOpenSavedChapter, Is.False);
            Assert.That(campaign.CanContinueGame, Is.False);

            var slots = Slots();
            SeedCampaign(slots, 1);
            Assert.That(campaign.Initialize(catalog, journeys, root), Is.True);
            Assert.That(campaign.CanOpenSavedChapter, Is.True);
            Assert.That(campaign.CanContinueGame, Is.False);

            SaveRun(slots.GetSavePath(1));
            Assert.That(campaign.CanContinueGame, Is.True);
        }

        /** Checks Load Game resumes a slot's run without resolving, opens a run-less slot's chapter and ignores an empty slot; fails if any slot opens the wrong way. */
        [Test]
        public void SelectingASlotResumesItsRunOrOpensItsChapterAndIgnoresAnEmptySlot()
        {
            var slots = Slots();
            SeedCampaign(slots, 2);
            SeedCampaign(slots, 1);
            var saved = SaveRun(slots.GetSavePath(1));
            Assert.That(campaign.Initialize(catalog, journeys, root), Is.True);
            Assert.That(campaign.ActiveSaveSlot, Is.EqualTo(1));
            int resolutions = 0;
            controller.Resolved += _ => ++resolutions;

            campaign.SelectSaveSlot(3);
            Assert.That(campaign.ActiveSaveSlot, Is.EqualTo(1));
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));

            campaign.SelectSaveSlot(1);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(saved.Revision));
            Assert.That(controller.Snapshot.CurrentDice.ToArray(), Is.EqualTo(saved.CurrentDice.ToArray()));
            Assert.That(resolutions, Is.Zero);

            campaign.ShowMainMenu();
            campaign.SelectSaveSlot(2);
            Assert.That(campaign.ActiveSaveSlot, Is.EqualTo(2));
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.Snapshot, Is.Null);
        }

        /** Checks New Game starts the chosen slot without progress or run and leaves other slots intact; fails if progress carries over or another campaign changes. */
        [Test]
        public void NewGameStartsTheChosenSlotEmptyAndKeepsOtherCampaigns()
        {
            var slots = Slots();
            SeedCampaign(slots, 2);
            SaveRun(slots.GetSavePath(2));
            SeedCampaign(slots, 1);
            SaveRun(slots.GetSavePath(1));
            byte[] otherRun = File.ReadAllBytes(slots.GetSavePath(2));
            byte[] otherProfile = File.ReadAllBytes(slots.GetProfilePath(2));
            Assert.That(campaign.Initialize(catalog, journeys, root), Is.True);

            campaign.NewGame(1);
            Assert.That(campaign.ActiveSaveSlot, Is.EqualTo(1));
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.CompletedEncounterIds, Is.Empty);
            Assert.That(File.Exists(controller.SavePath), Is.False);
            slots.Load();
            Assert.That(File.ReadAllBytes(slots.GetSavePath(2)), Is.EqualTo(otherRun));
            Assert.That(File.ReadAllBytes(slots.GetProfilePath(2)), Is.EqualTo(otherProfile));
        }

        /** Checks an unreadable run returns to the menu with its load error and keeps its file; fails if it leaves the menu, hides the error or rewrites the file. */
        [Test]
        public void UnreadableRunReturnsToTheMenuWithItsErrorAndKeepsItsFile()
        {
            var slots = Slots();
            SeedCampaign(slots, 1);
            const string unreadable = "{ broken run";
            File.WriteAllText(slots.GetSavePath(1), unreadable);
            Assert.That(campaign.Initialize(catalog, journeys, root), Is.True);
            Assert.That(campaign.CanContinueGame, Is.True);

            campaign.ContinueGame();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.Status.Kind, Is.EqualTo(SpiritStatusKind.LoadFailed));
            Assert.That(campaign.CanStartNewGame, Is.True);
            Assert.That(File.ReadAllText(slots.GetSavePath(1)), Is.EqualTo(unreadable));
        }

        /** Checks a rejected menu initialization keeps the previous campaign mapping and live run; fails if either is replaced or lost. */
        [Test]
        public void RejectedInitializationKeepsThePreviousCampaignAndRun()
        {
            var slots = Slots();
            SeedCampaign(slots, 1);
            SaveRun(slots.GetSavePath(1));
            Assert.That(campaign.Initialize(catalog, journeys, root), Is.True);
            campaign.ContinueGame();
            var snapshot = controller.Snapshot;
            Assert.That(snapshot, Is.Not.Null, controller.Status.ToString());
            string savePath = controller.SavePath;

            Assert.That(campaign.Initialize(catalog, null, Path.Combine(root, "rejected-campaigns")), Is.False);
            Assert.That(campaign.RootDirectory, Is.EqualTo(root));
            Assert.That(campaign.ActiveSaveSlot, Is.EqualTo(1));
            Assert.That(controller.SavePath, Is.EqualTo(savePath));
            Assert.That(controller.Snapshot, Is.SameAs(snapshot));
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
        }

        /** Checks a corrupt slot list opens the menu with a recovery notice and its campaign still available; fails if the rebuild is silent or the campaign is lost. */
        [Test]
        public void CorruptSlotListOpensTheMenuWithARecoveryNotice()
        {
            SeedCampaign(Slots(), 1);
            Assert.That(campaign.Initialize(catalog, journeys, root), Is.True);
            Assert.That(controller.Status.Kind, Is.EqualTo(SpiritStatusKind.MainMenuReady));

            File.WriteAllText(Path.Combine(root, "spirit-slots.json"), "{ broken index");
            Assert.That(campaign.Initialize(catalog, journeys, root), Is.True);
            Assert.That(controller.Status.Kind, Is.EqualTo(SpiritStatusKind.SaveSlotsRecovered).Or.EqualTo(SpiritStatusKind.UnassignedSavesRecovered));
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));
            Assert.That(campaign.CanOpenSavedChapter, Is.True);
        }

        private CampaignSaveSlots Slots()
        {
            var slots = new CampaignSaveSlots(root, catalog);
            slots.Load();
            return slots;
        }

        // Starts a campaign in the slot, making it the active slot, with progress in its profile.
        private static void SeedCampaign(CampaignSaveSlots slots, int slot)
        {
            slots.StartNewGame(slot);
            var profile = new PlayerProfile();
            profile.CompleteEncounter(TestContent.EncounterId);
            PlayerProfileFileStore.Save(slots.GetProfilePath(slot), profile);
        }

        // Saves a run one reroll past its opening roll, so a fresh opening roll would not match it.
        private EncounterSnapshot SaveRun(string path)
        {
            var run = new EncounterApplication(catalog, TestContent.EncounterId, 17);
            Assert.That(run.Submit(new RerollDiceAction(1), run.Snapshot.Revision).Accepted, Is.True);
            EncounterSaveFileStore.Save(path, run.CaptureSave());
            return run.Snapshot;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            assets.Add(asset);
            return asset;
        }
    }
}
