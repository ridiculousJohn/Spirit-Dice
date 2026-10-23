using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class MainMenuPlayTests
    {
        private SpiritEncounterController controller;
        private SpiritCampaignController campaign;
        private SpiritEncounterView view => ContentReady.EncounterView(controller);
        private string rootDirectory;
        private string captureRoot;
        private SpiritMainMenu Menu => ContentReady.FrontendView(controller).MainMenu;
        private SpiritSaveSlotPicker Picker => Menu.SlotPicker;

        [UnitySetUp]
        public IEnumerator MainMenuReady()
        {
            yield return SceneManager.LoadSceneAsync("Application");
            controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            Assert.That(controller, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 30;
            while (Time.realtimeSinceStartup < deadline)
            {
                campaign = controller.GetComponent<SpiritCampaignController>();
                if (controller.Catalog != null && controller.JourneyCatalog != null && campaign != null &&
                    controller.JourneyPhase == EncounterJourneyPhase.MainMenu) break;
                Assert.That(controller.StatusMessage, Does.Not.StartWith("Cannot"));
                yield return null;
            }
            Assert.That(campaign, Is.Not.Null);
            Assert.That(controller.Catalog, Is.Not.Null, controller.StatusMessage);
            Assert.That(controller.JourneyCatalog, Is.Not.Null, controller.StatusMessage);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));
            yield return ContentReady.Routed(controller);
            rootDirectory = campaign.RootDirectory;
            string workRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work")) +
                Path.DirectorySeparatorChar;
            Assert.That(Path.GetFullPath(rootDirectory).StartsWith(workRoot, StringComparison.OrdinalIgnoreCase),
                Is.True, "Main menu tests require an isolated campaign directory before bootstrap runs.");
            string captureParent = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            captureRoot = Path.Combine(string.IsNullOrEmpty(captureParent) ? workRoot : captureParent,
                "main-menu-" + Guid.NewGuid().ToString("N"));
        }

        [UnityTest]
        public IEnumerator PopupRaycastLifecycle()
        {
            SeedSlots();
            ReloadCampaign();
            for (int activation = 0; activation < 2; ++activation)
            {
                Activate(Menu.NewGameButton);
                yield return ContentReady.Routed();
                Assert.That(Picker.IsOpen, Is.True);
                Assert.That(Picker.IsConfirmingReplacement, Is.False);
                // Native canvas depth is assigned during rendering, after managed layout and graphic rebuilds.
                yield return new WaitForEndOfFrame();
                Assert.That(Picker.Root.GetComponent<Graphic>().depth, Is.GreaterThanOrEqualTo(0));
                Assert.That(Picker.SlotButtons[0].targetGraphic.depth, Is.GreaterThanOrEqualTo(0));
                foreach (var button in new[] { Menu.ContinueButton, Menu.SlotSelectorButton,
                    Menu.ChapterButton, Menu.NewGameButton, Menu.OptionsButton })
                    AssertCoveredBy(button, Picker.Root);

                Click(Picker.SlotButtons[0]);
                yield return ContentReady.Routed();
                Assert.That(Picker.IsConfirmingReplacement, Is.True);
                yield return new WaitForEndOfFrame();
                Assert.That(Picker.Confirmation.GetComponent<Graphic>().depth, Is.GreaterThanOrEqualTo(0));
                Assert.That(Picker.CancelReplaceSaveButton.targetGraphic.depth, Is.GreaterThanOrEqualTo(0));
                foreach (var button in Picker.SlotButtons.Concat(new[] { Picker.CloseButton }))
                    AssertCoveredBy(button, Picker.Confirmation);
                Click(Picker.CancelReplaceSaveButton);
                yield return ContentReady.Routed();
                Assert.That(Picker.IsConfirmingReplacement, Is.False);
                Assert.That(Picker.IsOpen, Is.True);
                yield return new WaitForEndOfFrame();
                Click(Picker.CloseButton);
                yield return ContentReady.Routed();
                Assert.That(Picker.IsOpen, Is.False);
                yield return new WaitForEndOfFrame();
            }
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void EmptyStartupShowsDisabledSavedGameActionsAndOptionsDoesNothing()
        {
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(campaign.ActiveSaveSlot, Is.EqualTo(1));
            Assert.That(campaign.SaveSlots.Select(slot => slot.SlotNumber), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(campaign.SaveSlots.All(slot => !slot.HasSavedGame), Is.True);
            Assert.That(Menu.Root.gameObject.activeInHierarchy, Is.True);
            Assert.That(Menu.Title.text, Is.EqualTo("Spirit Dice"));
            Assert.That(Menu.ContinueButton.IsInteractable(), Is.False);
            Assert.That(Menu.ChapterButton.IsInteractable(), Is.False);
            Assert.That(Menu.NewGameButton.IsInteractable(), Is.True);
            Assert.That(Menu.SlotSelectorButton.IsInteractable(), Is.True);
            Assert.That(ContentReady.FrontendView(controller).Board.Root.gameObject.activeInHierarchy, Is.False);
            string indexBefore = File.ReadAllText(Path.Combine(rootDirectory, "spirit-slots.json"));
            Activate(Menu.OptionsButton);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(Picker.IsOpen, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(rootDirectory, "spirit-slots.json")), Is.EqualTo(indexBefore));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void NewGameInEmptySlotStartsInvestigationAndRemembersItsSelection()
        {
            Activate(Menu.NewGameButton);
            Assert.That(Picker.IsOpen, Is.True);
            Activate(Picker.SlotButtons[1]);
            Assert.That(Picker.IsOpen, Is.False);
            Assert.That(Picker.IsConfirmingReplacement, Is.False);
            Assert.That(campaign.ActiveSaveSlot, Is.EqualTo(2));
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(File.Exists(controller.ProfilePath), Is.True);
            AssertEmptyProfile(PlayerProfileFileStore.Load(controller.ProfilePath));
            Assert.That(File.Exists(controller.SavePath), Is.False);
            Assert.That(ContentReady.FrontendView(controller).Board.Root.gameObject.activeInHierarchy, Is.True);
            Activate(ContentReady.FrontendView(controller).Board.LoadButton);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));
            ReloadCampaign();
            Assert.That(campaign.ActiveSaveSlot, Is.EqualTo(2));
            Assert.That(campaign.SaveSlots.Single(slot => slot.SlotNumber == 2).HasSavedGame, Is.True);
            Assert.That(campaign.SaveSlots.Count(slot => slot.HasSavedGame), Is.EqualTo(1));
            Assert.That(Menu.ContinueButton.IsInteractable(), Is.False);
            Assert.That(Menu.ChapterButton.IsInteractable(), Is.True);
            Activate(Menu.ChapterButton);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.Snapshot, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OccupiedSlotRequiresConfirmationAndReplacementPreservesOtherCampaign()
        {
            var slots = SeedSlots();
            string firstRun = File.ReadAllText(slots.GetSavePath(1));
            string firstProfile = File.ReadAllText(slots.GetProfilePath(1));
            string replacedDirectory = Path.GetDirectoryName(slots.GetProfilePath(1));
            string secondRun = File.ReadAllText(slots.GetSavePath(2));
            string secondProfile = File.ReadAllText(slots.GetProfilePath(2));
            string indexBefore = File.ReadAllText(Path.Combine(rootDirectory, "spirit-slots.json"));
            ReloadCampaign();
            Activate(Menu.NewGameButton);
            Activate(Picker.SlotButtons[0]);
            Assert.That(Picker.IsConfirmingReplacement, Is.True);
            Assert.That(Picker.ConfirmationTitle.text, Is.EqualTo("Replace save slot 1?"));
            Assert.That(Picker.ConfirmationText.text,
                Is.EqualTo("This will reset its chapter progress, collected charms, and current encounter."));
            Activate(Picker.CancelReplaceSaveButton);
            Assert.That(Picker.IsOpen, Is.True);
            Assert.That(Picker.IsConfirmingReplacement, Is.False);
            Assert.That(File.ReadAllText(slots.GetSavePath(1)), Is.EqualTo(firstRun));
            Assert.That(File.ReadAllText(slots.GetProfilePath(1)), Is.EqualTo(firstProfile));
            Assert.That(File.ReadAllText(Path.Combine(rootDirectory, "spirit-slots.json")), Is.EqualTo(indexBefore));
            Activate(Picker.SlotButtons[0]);
            Activate(Picker.ConfirmReplaceSaveButton);
            slots.Load();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(campaign.ActiveSaveSlot, Is.EqualTo(1));
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(File.Exists(slots.GetSavePath(1)), Is.False);
            Assert.That(Directory.Exists(replacedDirectory), Is.False);
            AssertEmptyProfile(PlayerProfileFileStore.Load(slots.GetProfilePath(1)));
            Assert.That(controller.CompletedEncounterIds, Is.Empty);
            Assert.That(controller.CompletedStoryIds, Is.Empty);
            Assert.That(controller.VisitedEncounterIds, Is.Empty);
            Assert.That(controller.ProfileUnlockedModifierIds, Is.Empty);
            Assert.That(File.ReadAllText(slots.GetSavePath(2)), Is.EqualTo(secondRun));
            Assert.That(File.ReadAllText(slots.GetProfilePath(2)), Is.EqualTo(secondProfile));
            campaign.ShowMainMenu();
            Assert.That(Picker.IsOpen, Is.False);
            Assert.That(Picker.IsConfirmingReplacement, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CorruptOccupiedSlotReportsItsLoadFailureAndCanBeReplaced()
        {
            foreach (bool corruptProfile in new[] { false, true })
            {
                var slots = SeedSlots();
                slots.SelectSlot(1);
                string corruptPath = corruptProfile ? slots.GetProfilePath(1) : slots.GetSavePath(1);
                File.WriteAllText(corruptPath, "{}");
                string secondRun = File.ReadAllText(slots.GetSavePath(2));
                string secondProfile = File.ReadAllText(slots.GetProfilePath(2));
                ReloadCampaign();
                Assert.That(campaign.SaveSlots.Single(slot => slot.SlotNumber == 1).HasSavedGame, Is.True);
                foreach (var button in new[] { Menu.ContinueButton, Menu.ChapterButton })
                {
                    Activate(button);
                    yield return ContentReady.Routed();
                    Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));
                    Assert.That(controller.Snapshot, Is.Null);
                    Assert.That(Menu.Root.gameObject.activeInHierarchy, Is.True);
                    Assert.That(Menu.Status.text, Does.StartWith("Cannot load"));
                    Assert.That(Menu.Status.text.ToLowerInvariant(), Does.Contain(corruptProfile ? "profile" : "encounter"));
                    Assert.That(Menu.NewGameButton.IsInteractable(), Is.True);
                    Assert.That(File.ReadAllText(corruptPath), Is.EqualTo("{}"));
                }
                yield return new WaitForEndOfFrame();
                AssertCompleteText(Menu.Status);
                yield return Capture(corruptProfile ? "corrupt-profile-menu" : "corrupt-run-menu");
                Activate(Menu.NewGameButton);
                yield return ContentReady.Routed();
                Activate(Picker.SlotButtons[0]);
                yield return ContentReady.Routed();
                Assert.That(Picker.IsConfirmingReplacement, Is.True);
                Activate(Picker.ConfirmReplaceSaveButton);
                yield return ContentReady.Routed();
                slots.Load();
                Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
                Assert.That(controller.Snapshot, Is.Null);
                AssertEmptyProfile(PlayerProfileFileStore.Load(slots.GetProfilePath(1)));
                Assert.That(File.Exists(slots.GetSavePath(1)), Is.False);
                Assert.That(File.ReadAllText(slots.GetSavePath(2)), Is.EqualTo(secondRun));
                Assert.That(File.ReadAllText(slots.GetProfilePath(2)), Is.EqualTo(secondProfile));
                campaign.ShowMainMenu();
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ContinueRestoresAutosavedHandWithoutRollingAgain()
        {
            campaign.NewGame(1);
            var journey = controller.JourneyCatalog.Entries.First(entry => !entry.IsStoryOnly);
            controller.SelectEncounter(journey.Id);
            yield return ContentReady.Routed(controller);
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                controller.AdvanceJourney();
            controller.AcceptEncounter();
            yield return ContentReady.Routed(controller);
            yield return WaitForIdle();
            controller.ToggleDie(0);
            controller.Reroll();
            yield return WaitForIdle();
            Assert.That(File.Exists(controller.SavePath), Is.True, controller.StatusMessage);
            var expected = controller.Snapshot;
            var saved = EncounterSaveFileStore.Load(controller.SavePath, controller.Catalog);
            Assert.That(saved.Revision, Is.EqualTo(expected.Revision));
            Assert.That(saved.CurrentDice, Is.EqualTo(expected.CurrentDice.ToArray()));
            string savedBytes = File.ReadAllText(controller.SavePath);
            Activate(ContentReady.PresentationRoot(controller).GetComponentsInChildren<Button>(true).Single(button => button.name == "MenuButton"));
            yield return ContentReady.Routed();
            Activate(ContentReady.PresentationRoot(controller).GetComponentsInChildren<Button>(true).Single(button => button.name == "ReturnToMainMenuButton"));
            yield return ContentReady.Routed();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));
            Assert.That(Menu.Root.gameObject.activeInHierarchy, Is.True);
            ReloadCampaign();
            Assert.That(controller.Snapshot, Is.Null);
            int resolutions = 0;
            controller.Resolved += _ => ++resolutions;
            Activate(Menu.ContinueButton);
            yield return ContentReady.Routed();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(expected.Revision));
            Assert.That(controller.Snapshot.CurrentDice, Is.EqualTo(expected.CurrentDice));
            Assert.That(controller.Snapshot.TotalRerollsSpent, Is.EqualTo(expected.TotalRerollsSpent));
            Assert.That(controller.Snapshot.RerollsRemaining, Is.EqualTo(expected.RerollsRemaining));
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(view == null || !view.DiceTray.IsRolling, Is.True);
            Assert.That(resolutions, Is.Zero);
            Assert.That(File.ReadAllText(controller.SavePath), Is.EqualTo(savedBytes));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ChapterOpensSavedEncountersChapterWithResumableRun()
        {
            yield return ContentReady.Routed(controller);
            var slots = SeedSlots();
            slots.SelectSlot(2);
            var owl = controller.JourneyCatalog.Entries.Single(entry => entry.EncounterId == "encounter.owl.watchtower");
            var expected = controller.JourneyCatalog.Chapters.Single(chapter => chapter.Entries.Contains(owl));
            string savedBytes = File.ReadAllText(slots.GetSavePath(2));
            ReloadCampaign();
            Assert.That(controller.Snapshot, Is.Null);
            int resolutions = 0;
            controller.Resolved += _ => ++resolutions;
            Activate(Menu.ChapterButton);
            yield return ContentReady.Routed(controller);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.SelectedChapter, Is.SameAs(expected));
            Assert.That(controller.SelectedJourney, Is.Null);
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo(owl.EncounterId));
            var saved = EncounterSaveFileStore.Load(slots.GetSavePath(2), controller.Catalog);
            Assert.That(controller.Snapshot.CurrentDice.ToArray(), Is.EqualTo(saved.CurrentDice));
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(saved.Revision));
            Assert.That(controller.CanResume, Is.True);
            Assert.That(ContentReady.FrontendView(controller).Board.ResumeButton.gameObject.activeInHierarchy, Is.True);
            Assert.That(controller.IsBusy, Is.False);
            Assert.That(resolutions, Is.Zero);
            Assert.That(File.ReadAllText(slots.GetSavePath(2)), Is.EqualTo(savedBytes));
            Assert.That(ContentReady.PresentationRoot(controller).GetComponentsInChildren<Text>().Where(label => label.name == "ClueTitle")
                .Select(label => label.text), Does.Contain(owl.LocationTitle));
            Activate(ContentReady.FrontendView(controller).Board.ResumeButton);
            yield return ContentReady.Routed(controller);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.Snapshot.CurrentDice.ToArray(), Is.EqualTo(saved.CurrentDice));
            Assert.That(controller.Snapshot.Revision, Is.EqualTo(saved.Revision));
            Assert.That(resolutions, Is.Zero);
            Assert.That(File.ReadAllText(slots.GetSavePath(2)), Is.EqualTo(savedBytes));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RejectedMenuInitializationKeepsPreviousCampaignMappingAndRun()
        {
            yield return ContentReady.Routed(controller);
            SeedSlots();
            ReloadCampaign();
            Activate(Menu.ContinueButton);
            yield return ContentReady.Routed(controller);
            var snapshot = controller.Snapshot;
            string savePath = controller.SavePath;
            string previousRoot = campaign.RootDirectory;
            int previousSlot = campaign.ActiveSaveSlot;
            string nextRoot = Path.Combine(rootDirectory, "rejected-campaign");
            Assert.That(campaign.Initialize(controller.Catalog, null, nextRoot), Is.False);
            Assert.That(campaign.RootDirectory, Is.EqualTo(previousRoot));
            Assert.That(campaign.ActiveSaveSlot, Is.EqualTo(previousSlot));
            Assert.That(controller.SavePath, Is.EqualTo(savePath));
            Assert.That(controller.Snapshot, Is.SameAs(snapshot));
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(campaign.SaveSlots.Count(slot => slot.HasSavedGame), Is.EqualTo(2));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CorruptSlotIndexRecoversToUsableMenuWithoutChangingPreviousCampaigns()
        {
            var slots = SeedSlots();
            string previousRun = slots.GetSavePath(2);
            string previousProfile = slots.GetProfilePath(2);
            byte[] runBytes = File.ReadAllBytes(previousRun);
            byte[] profileBytes = File.ReadAllBytes(previousProfile);
            string indexPath = Path.Combine(rootDirectory, "spirit-slots.json");
            const string corrupt = "{ broken index";
            File.WriteAllText(indexPath, corrupt);
            ReloadCampaign();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));
            Assert.That(Menu.Root.gameObject.activeInHierarchy, Is.True);
            Assert.That(Menu.NewGameButton.IsInteractable(), Is.True);
            Assert.That(Menu.ContinueButton.IsInteractable(), Is.True);
            Assert.That(Menu.ChapterButton.IsInteractable(), Is.True);
            Assert.That(Menu.Status.text, Does.Contain("Save-slot list recovered"));
            Assert.That(campaign.SaveSlots.Count(slot => slot.HasSavedGame), Is.EqualTo(2));
            string backup = Directory.GetFiles(rootDirectory, "spirit-slots.corrupt-*.json").Single();
            Assert.That(File.ReadAllText(backup), Is.EqualTo(corrupt));
            Activate(Menu.SlotSelectorButton);
            Activate(Picker.SlotButtons[1]);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.Snapshot.CurrentDice.ToArray(),
                Is.EqualTo(EncounterSaveFileStore.Load(previousRun, controller.Catalog).CurrentDice));
            Assert.That(File.ReadAllBytes(previousRun), Is.EqualTo(runBytes));
            Assert.That(File.ReadAllBytes(previousProfile), Is.EqualTo(profileBytes));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SlotSelectionLoadsCampaignAndDisablesEmptySlots()
        {
            var slots = SeedSlots();
            slots.SelectSlot(1);
            ReloadCampaign();
            Activate(Menu.SlotSelectorButton);
            Assert.That(Picker.Title.text, Is.EqualTo("Load Game"));
            Assert.That(Picker.SlotButtons[2].IsInteractable(), Is.False);
            var picker = Picker;
            Activate(picker.SlotButtons[1]);
            yield return ContentReady.Routed(controller);
            Assert.That(campaign.ActiveSaveSlot, Is.EqualTo(2));
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(picker == null || !picker.IsOpen, Is.True);
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo("encounter.owl.watchtower"));
            Assert.That(controller.Snapshot.CurrentDice.ToArray(),
                Is.EqualTo(EncounterSaveFileStore.Load(slots.GetSavePath(2), controller.Catalog).CurrentDice));
            campaign.ShowMainMenu();
            yield return ContentReady.Routed(controller);
            Activate(Menu.SlotSelectorButton);
            Assert.That(Picker.SlotButtons[2].IsInteractable(), Is.False);
            campaign.SelectSaveSlot(3);
            Assert.That(campaign.ActiveSaveSlot, Is.EqualTo(2));
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));
            Activate(Picker.CloseButton);
            Activate(Menu.ContinueButton);
            yield return ContentReady.Routed(controller);
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo("encounter.owl.watchtower"));
            slots.Load();
            Assert.That(slots.ActiveSlot, Is.EqualTo(2));
            Assert.That(File.Exists(slots.GetSavePath(1)), Is.True);
            Assert.That(File.Exists(slots.GetSavePath(2)), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void LoadGameOpensChapterForCampaignWithoutEncounterSave()
        {
            var slots = SeedSlots();
            slots.StartNewGame(3);
            slots.SelectSlot(1);
            ReloadCampaign();
            Activate(Menu.SlotSelectorButton);
            Activate(Picker.SlotButtons[2]);
            Assert.That(campaign.ActiveSaveSlot, Is.EqualTo(3));
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Investigation));
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.CompletedEncounterIds, Is.Empty);
            Assert.That(controller.ProfileUnlockedModifierIds, Is.Empty);
            Assert.That(File.Exists(controller.SavePath), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MenuSlotPickerAndConfirmationFitBothPortraitSizes()
        {
            SeedSlots();
            ReloadCampaign();
            foreach (var size in new[] { new Vector2(443, 912), new Vector2(540, 960) })
            {
                var portrait = ContentReady.Portrait();
                portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                portrait.sizeDelta = size;
                portrait.anchoredPosition = Vector2.zero;
                yield return new WaitForEndOfFrame();
                Canvas.ForceUpdateCanvases();
                AssertCompleteText(Menu.Title);
                var buttons = new[] { Menu.ContinueButton, Menu.SlotSelectorButton, Menu.ChapterButton,
                    Menu.NewGameButton, Menu.OptionsButton };
                AssertButtonsFit(buttons, portrait);
                Assert.That(Menu.SlotSelectorButton.GetComponentInChildren<Text>().text, Is.EqualTo("Load Game"));
                for (int i = 1; i < buttons.Length; ++i)
                {
                    AssertSeparate(buttons[i - 1], buttons[i]);
                    Assert.That(buttons[i - 1].transform.position.y, Is.GreaterThan(buttons[i].transform.position.y));
                }
                yield return Capture("main-menu-" + (int)size.x);
                Click(Menu.SlotSelectorButton);
                yield return ContentReady.Routed();
                yield return new WaitForEndOfFrame();
                Assert.That(Picker.Title.text, Is.EqualTo("Load Game"));
                Assert.That(Picker.SlotButtons[2].IsInteractable(), Is.False);
                AssertButtonsFit(Picker.SlotButtons.Concat(new[] { Picker.CloseButton }), portrait);
                AssertCoveredBy(Menu.ContinueButton, Picker.Root);
                yield return Capture("load-game-" + (int)size.x);
                Click(Picker.CloseButton);
                yield return ContentReady.Routed();
                yield return new WaitForEndOfFrame();
                Click(Menu.NewGameButton);
                yield return ContentReady.Routed();
                yield return new WaitForEndOfFrame();
                Canvas.ForceUpdateCanvases();
                AssertButtonsFit(Picker.SlotButtons.Concat(new[] { Picker.CloseButton }), portrait);
                for (int i = 1; i < Picker.SlotButtons.Count; ++i)
                    AssertSeparate(Picker.SlotButtons[i - 1], Picker.SlotButtons[i]);
                AssertCoveredBy(Menu.NewGameButton, Picker.Root);
                yield return Capture("save-slot-picker-" + (int)size.x);
                Click(Picker.SlotButtons[0]);
                yield return ContentReady.Routed();
                yield return new WaitForEndOfFrame();
                Canvas.ForceUpdateCanvases();
                AssertCompleteText(Picker.ConfirmationTitle);
                AssertCompleteText(Picker.ConfirmationText);
                AssertButtonsFit(new[] { Picker.ConfirmReplaceSaveButton, Picker.CancelReplaceSaveButton }, portrait);
                AssertSeparate(Picker.ConfirmReplaceSaveButton, Picker.CancelReplaceSaveButton);
                AssertCoveredBy(Picker.SlotButtons[0], Picker.Confirmation);
                yield return Capture("replace-save-confirmation-" + (int)size.x);
                Click(Picker.CancelReplaceSaveButton);
                yield return ContentReady.Routed();
                yield return new WaitForEndOfFrame();
                Click(Picker.CloseButton);
                yield return ContentReady.Routed();
                yield return new WaitForEndOfFrame();
            }
            LogAssert.NoUnexpectedReceived();
        }

        private CampaignSaveSlots SeedSlots()
        {
            var slots = new CampaignSaveSlots(rootDirectory, controller.Catalog);
            slots.Load();
            var fox = controller.JourneyCatalog.Entries.Single(entry => entry.EncounterId == "encounter.fox.introduction");
            var owl = controller.JourneyCatalog.Entries.Single(entry => entry.EncounterId == "encounter.owl.watchtower");
            var story = controller.JourneyCatalog.Entries.First(entry => entry.IsStoryOnly);
            var charm = controller.Catalog.Modifiers.First(modifier => modifier.Availability.Access == ModifierAccess.PermanentUnlock);
            for (int slot = 1; slot <= 2; ++slot)
            {
                slots.StartNewGame(slot);
                var profile = new PlayerProfile();
                profile.CompleteEncounter(fox.EncounterId);
                profile.CompleteStory(story.StoryId);
                profile.VisitEncounter(fox.EncounterId);
                profile.DiscoverFamily(charm.FamilyId);
                profile.UnlockModifier(charm.Id);
                PlayerProfileFileStore.Save(slots.GetProfilePath(slot), profile);
                var application = new EncounterApplication(controller.Catalog,
                    slot == 1 ? fox.EncounterId : owl.EncounterId, (ulong)(60 + slot),
                    profile.UnlockedModifierIds, profile.DiscoveredFamilyIds);
                EncounterSaveFileStore.Save(slots.GetSavePath(slot), application.CaptureSave());
            }
            return slots;
        }

        [Test]
        public void RecoveryConfirmationPreservesDisplacedCampaignAndReloadKeepsAssignment()
        {
            var slots = SeedSlots();
            string displacedProfile = slots.GetProfilePath(1);
            string displacedRun = slots.GetSavePath(1);
            byte[] displacedBytes = File.ReadAllBytes(displacedRun);
            string recoveryId = CopyForRecovery(slots, 2);
            string recoveryRun = Path.Combine(rootDirectory, recoveryId, "spirit-encounter.json");
            byte[] recoveredBytes = File.ReadAllBytes(recoveryRun);
            ReloadCampaign();
            Assert.That(campaign.RecoveryCampaigns.Select(item => item.CampaignId), Is.EqualTo(new[] { recoveryId }));
            Activate(Menu.SlotSelectorButton);
            Activate(Picker.RecoverSavesButton);
            Activate(Picker.RecoveryPicker.CandidateButtons[0]);
            Assert.That(Picker.Mode, Is.EqualTo(SpiritSaveSlotPicker.PickerMode.Recover));
            Activate(Picker.SlotButtons[0]);
            Assert.That(Picker.IsConfirmingReplacement, Is.True);
            Assert.That(Picker.ConfirmationText.text, Does.Contain("remain available under Recover saves"));
            Assert.That(Picker.ConfirmReplaceSaveButton.GetComponentInChildren<Text>().text, Is.EqualTo("Recover"));
            Activate(Picker.CancelReplaceSaveButton);
            Assert.That(File.ReadAllBytes(displacedRun), Is.EqualTo(displacedBytes));
            Assert.That(slots.GetProfilePath(1), Is.EqualTo(displacedProfile));
            Activate(Picker.SlotButtons[0]);
            Activate(Picker.ConfirmReplaceSaveButton);
            Assert.That(Picker.Mode, Is.EqualTo(SpiritSaveSlotPicker.PickerMode.Select));
            Assert.That(Picker.IsOpen, Is.True);
            Assert.That(controller.Snapshot, Is.Null);
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.MainMenu));
            Assert.That(campaign.RecoveryCampaigns.Select(item => item.CampaignId),
                Is.EqualTo(new[] { Path.GetFileName(Path.GetDirectoryName(displacedProfile)) }));
            Assert.That(File.ReadAllBytes(displacedRun), Is.EqualTo(displacedBytes));
            Assert.That(File.ReadAllBytes(recoveryRun), Is.EqualTo(recoveredBytes));
            ReloadCampaign();
            slots.Load();
            Assert.That(slots.GetSavePath(1), Is.EqualTo(recoveryRun));
            Assert.That(campaign.RecoveryCampaigns.Count, Is.EqualTo(1));
            Activate(Menu.ContinueButton);
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo("encounter.owl.watchtower"));
            Assert.That(controller.Snapshot.CurrentDice.ToArray(), Is.EqualTo(EncounterSaveFileStore.Load(recoveryRun, controller.Catalog).CurrentDice));
            Assert.That(File.ReadAllBytes(recoveryRun), Is.EqualTo(recoveredBytes));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RecoveryPickerPagesAndFitsPortraitsThenRestoresIntoEmptySlot()
        {
            var slots = SeedSlots();
            for (int i = 0; i < 4; ++i) CopyForRecovery(slots, 2);
            ReloadCampaign();
            var recovery = Picker.RecoveryPicker;
            foreach (var size in new[] { new Vector2(443, 912), new Vector2(540, 960) })
            {
                var portrait = ContentReady.Portrait();
                portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
                portrait.sizeDelta = size;
                portrait.anchoredPosition = Vector2.zero;
                Activate(Menu.SlotSelectorButton);
                yield return ContentReady.Routed();
                yield return new WaitForEndOfFrame();
                AssertButtonsFit(Picker.SlotButtons.Concat(new[] { Picker.CloseButton, Picker.RecoverSavesButton }), portrait);
                AssertSeparate(Picker.SlotButtons[2], Picker.RecoverSavesButton);
                AssertSeparate(Picker.RecoverSavesButton, Picker.CloseButton);
                yield return Capture("load-game-recovery-" + (int)size.x);
                Click(Picker.RecoverSavesButton);
                yield return ContentReady.Routed();
                yield return new WaitForEndOfFrame();
                AssertCoveredBy(Picker.SlotButtons[0], recovery.Root);
                Assert.That(recovery.PageLabel.text, Is.EqualTo("1 / 2"));
                yield return Capture("recover-campaigns-" + (int)size.x);
                AssertButtonsFit(recovery.CandidateButtons.Concat(new[] { recovery.PreviousPageButton,
                    recovery.NextPageButton, recovery.CloseButton }), portrait);
                foreach (var label in recovery.Root.GetComponentsInChildren<Text>()) AssertCompleteText(label);
                AssertSeparate(recovery.CandidateButtons[2], recovery.PreviousPageButton);
                Click(recovery.NextPageButton);
                yield return ContentReady.Routed();
                Assert.That(recovery.PageLabel.text, Is.EqualTo("2 / 2"));
                Assert.That(recovery.CandidateButtons.Count(button => button.gameObject.activeSelf), Is.EqualTo(1));
                Click(recovery.PreviousPageButton);
                yield return ContentReady.Routed();
                Click(recovery.CandidateButtons[0]);
                yield return ContentReady.Routed();
                yield return new WaitForEndOfFrame();
                Assert.That(Picker.Mode, Is.EqualTo(SpiritSaveSlotPicker.PickerMode.Recover));
                Click(Picker.SlotButtons[0]);
                yield return ContentReady.Routed();
                yield return new WaitForEndOfFrame();
                AssertCompleteText(Picker.ConfirmationText);
                AssertCompleteText(Picker.ConfirmationTitle);
                AssertCoveredBy(Picker.SlotButtons[1], Picker.Confirmation);
                yield return Capture("recover-replacement-" + (int)size.x);
                Click(Picker.CancelReplaceSaveButton);
                yield return ContentReady.Routed();
                yield return new WaitForEndOfFrame();
                Click(Picker.CloseButton);
                yield return ContentReady.Routed();
                Assert.That(Picker.Mode, Is.EqualTo(SpiritSaveSlotPicker.PickerMode.Select));
                Click(Picker.CloseButton);
                yield return ContentReady.Routed();
            }
            string candidateId = campaign.RecoveryCampaigns[0].CampaignId;
            Activate(Menu.SlotSelectorButton);
            yield return ContentReady.Routed();
            Activate(Picker.RecoverSavesButton);
            yield return ContentReady.Routed();
            Activate(recovery.CandidateButtons[0]);
            yield return ContentReady.Routed();
            Activate(Picker.SlotButtons[2]);
            yield return ContentReady.Routed();
            Assert.That(Picker.IsConfirmingReplacement, Is.False);
            Assert.That(Picker.Mode, Is.EqualTo(SpiritSaveSlotPicker.PickerMode.Select));
            Assert.That(campaign.ActiveSaveSlot, Is.EqualTo(3));
            Assert.That(campaign.RecoveryCampaigns.Count, Is.EqualTo(3));
            slots.Load();
            Assert.That(slots.GetSavePath(3), Is.EqualTo(Path.Combine(rootDirectory, candidateId, "spirit-encounter.json")));
            Activate(Picker.SlotButtons[2]);
            yield return ContentReady.Routed();
            Assert.That(controller.JourneyPhase, Is.EqualTo(EncounterJourneyPhase.Playing));
            Assert.That(controller.Snapshot.EncounterId, Is.EqualTo("encounter.owl.watchtower"));
            LogAssert.NoUnexpectedReceived();
        }

        private string CopyForRecovery(CampaignSaveSlots slots, int slot)
        {
            string campaignId = Guid.NewGuid().ToString("N");
            string directory = Path.Combine(rootDirectory, campaignId);
            Directory.CreateDirectory(directory);
            File.Copy(slots.GetProfilePath(slot), Path.Combine(directory, "spirit-profile.json"));
            File.Copy(slots.GetSavePath(slot), Path.Combine(directory, "spirit-encounter.json"));
            return campaignId;
        }

        private void ReloadCampaign() => campaign.Initialize(controller.Catalog, controller.JourneyCatalog, rootDirectory);

        private IEnumerator WaitForIdle()
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False, controller.StatusMessage);
            Assert.That(controller.Snapshot, Is.Not.Null, controller.StatusMessage);
        }

        private static void AssertEmptyProfile(PlayerProfile profile)
        {
            Assert.That(profile.UnlockedModifierIds, Is.Empty);
            Assert.That(profile.DiscoveredFamilyIds, Is.Empty);
            Assert.That(profile.CompletedEncounterIds, Is.Empty);
            Assert.That(profile.CompletedStoryIds, Is.Empty);
            Assert.That(profile.VisitedEncounterIds, Is.Empty);
        }

        private static void Activate(Button button)
        {
            Assert.That(button.gameObject.activeInHierarchy, Is.True, button.name);
            Assert.That(button.IsInteractable(), Is.True, button.name);
            button.onClick.Invoke();
        }

        private static PointerEventData PointerFor(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            return new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left,
                pointerId = -1
            };
        }

        private static void Click(Button button)
        {
            Assert.That(button.gameObject.activeInHierarchy, Is.True, button.name);
            Assert.That(button.IsInteractable(), Is.True, button.name);
            var pointer = PointerFor(button);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, button.name);
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(handler, Is.SameAs(button.gameObject), "A graphic blocks " + button.name);
            pointer.pointerCurrentRaycast = hits[0];
            ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void AssertCoveredBy(Button button, RectTransform overlay)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(PointerFor(button), hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.transform.IsChildOf(overlay), Is.True, button.name);
        }

        private static void AssertButtonsFit(IEnumerable<Button> buttons, RectTransform portrait)
        {
            foreach (var button in buttons)
            {
                var rect = (RectTransform)button.transform;
                AssertWithin(rect, portrait);
                foreach (var label in button.GetComponentsInChildren<Text>())
                {
                    AssertWithin(label.rectTransform, rect);
                    AssertCompleteText(label);
                }
            }
        }

        private static void AssertSeparate(Button first, Button second)
        {
            var firstRect = (RectTransform)first.transform;
            var secondRect = (RectTransform)second.transform;
            var corners = new Vector3[4];
            secondRect.GetWorldCorners(corners);
            Vector2 min = firstRect.InverseTransformPoint(corners[0]);
            Vector2 max = firstRect.InverseTransformPoint(corners[2]);
            var other = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            Assert.That(firstRect.rect.Overlaps(other), Is.False, first.name + " overlaps " + second.name);
        }

        private static void AssertWithin(RectTransform rect, RectTransform parent)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector2 local = parent.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(parent.rect.xMin - 1, parent.rect.xMax + 1), rect.name);
                Assert.That(local.y, Is.InRange(parent.rect.yMin - 1, parent.rect.yMax + 1), rect.name);
            }
        }

        private static void AssertCompleteText(Text label)
        {
            var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
            var rendered = label.cachedTextGenerator;
            Assert.That(rendered.Populate(label.text, settings), Is.True, label.name);
            settings.fontSize = label.resizeTextForBestFit ? rendered.fontSizeUsedForBestFit : label.fontSize;
            settings.resizeTextForBestFit = false;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            using (var complete = new TextGenerator())
            {
                Assert.That(complete.Populate(label.text, settings), Is.True, label.name);
                Assert.That(rendered.vertexCount, Is.EqualTo(complete.vertexCount), label.name +
                    " truncates text. Rect: " + label.rectTransform.rect + ", preferred height: " + label.preferredHeight);
            }
        }

        private IEnumerator Capture(string name)
        {
            Directory.CreateDirectory(captureRoot);
            string path = Path.Combine(captureRoot, name + ".png");
            Assert.That(File.Exists(path), Is.False, path);
            ScreenCapture.CaptureScreenshot(path);
            for (int frame = 0; frame < 120 && !File.Exists(path); ++frame) yield return null;
            Assert.That(File.Exists(path), Is.True, path);
        }
    }
}
