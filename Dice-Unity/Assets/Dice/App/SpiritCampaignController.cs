using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using Dice.Engine;
using Dice.Presentation;
using UnityEngine;

namespace Dice.App
{
    /** Owns campaign slot selection and main menu actions; encounter state stays with the encounter controller. */
    [RequireComponent(typeof(SpiritEncounterController))]
    public sealed class SpiritCampaignController : MonoBehaviour, ISpiritMainMenuControls
    {
        private SpiritEncounterController controller;
        private CampaignSaveSlots slots;
        private MechanicalCatalog catalog;
        private EncounterJourneyCatalogAsset journeys;
        private readonly List<SpiritSaveSlotInfo> saveSlots = new List<SpiritSaveSlotInfo>();
        private readonly List<SpiritRecoveryCampaignInfo> recoveryCampaigns = new List<SpiritRecoveryCampaignInfo>();

        public string RootDirectory { get; private set; }
        public bool IsInitialized { get; private set; }
        public int ActiveSaveSlot => IsInitialized ? slots.ActiveSlot : 1;
        public IReadOnlyList<SpiritSaveSlotInfo> SaveSlots => saveSlots;
        public IReadOnlyList<SpiritRecoveryCampaignInfo> RecoveryCampaigns => recoveryCampaigns;
        private bool AtMenu => IsInitialized && !controller.IsBusy && controller.JourneyPhase == EncounterJourneyPhase.MainMenu;
        public bool CanContinueGame => CanOpenSavedChapter && File.Exists(slots.GetSavePath(ActiveSaveSlot));
        public bool CanOpenSavedChapter => AtMenu && slots.HasGame(ActiveSaveSlot);
        public bool CanStartNewGame => AtMenu;
        public bool CanSelectSaveSlot => AtMenu;

        public bool Initialize(MechanicalCatalog mechanicalCatalog, EncounterJourneyCatalogAsset journeyCatalog, string rootDirectory)
        {
            controller = GetComponent<SpiritEncounterController>();
            if (controller.IsBusy) return false;
            try
            {
                var nextSlots = new CampaignSaveSlots(rootDirectory, mechanicalCatalog);
                string recoveredIndex = nextSlots.Load();
                string nextRoot = Path.GetFullPath(rootDirectory);
                string nextSavePath = SavePath(nextSlots, nextRoot, nextSlots.ActiveSlot);
                if (!controller.InitializeMenu(mechanicalCatalog, journeyCatalog, nextSavePath)) return false;
                catalog = mechanicalCatalog;
                journeys = journeyCatalog;
                RootDirectory = nextRoot;
                slots = nextSlots;
                IsInitialized = true;
                RefreshSlots();
                controller.SetStatus(SpiritStatus.Of(recoveredIndex == null ? SpiritStatusKind.MainMenuReady :
                    recoveryCampaigns.Count == 0 ? SpiritStatusKind.SaveSlotsRecovered : SpiritStatusKind.UnassignedSavesRecovered));
                return true;
            }
            catch (Exception exception) when (Expected(exception))
            {
                controller.SetStatus(SpiritStatus.Of(SpiritStatusKind.SaveSlotsFailed, exception.Message));
                return false;
            }
        }

        public void SelectSaveSlot(int slot)
        {
            if (!CanSelectSaveSlot || !slots.HasGame(slot)) return;
            try
            {
                slots.SelectSlot(slot);
                RefreshSlots();
                if (!controller.InitializeMenu(catalog, journeys, SelectedSavePath())) return;
                if (CanContinueGame) ContinueGame();
                else OpenSavedChapter();
            }
            catch (Exception exception) when (Expected(exception))
            {
                controller.SetStatus(SpiritStatus.Of(SpiritStatusKind.SaveSlotSelectionFailed, exception.Message));
            }
        }

        public void NewGame(int slot)
        {
            if (!CanStartNewGame) return;
            try
            {
                string cleanupFailure = slots.StartNewGame(slot);
                RefreshSlots();
                if (!controller.InitializeMenu(catalog, journeys, SelectedSavePath())) return;
                if (!controller.InitializeJourney(catalog, journeys, SelectedSavePath())) ReturnWithError();
                else if (cleanupFailure != null) controller.SetStatus(SpiritStatus.Of(SpiritStatusKind.PreviousCampaignNotRemoved, cleanupFailure));
            }
            catch (Exception exception) when (Expected(exception))
            {
                controller.SetStatus(SpiritStatus.Of(SpiritStatusKind.NewGameFailed, exception.Message));
            }
        }

        public void ContinueGame()
        {
            if (!CanContinueGame) return;
            if (!PrepareSelectedCampaign()) return;
            if (File.Exists(controller.SavePath) && !controller.TryLoad()) ReturnWithError();
        }

        public void RecoverCampaign(Guid campaignId, int slot)
        {
            if (!CanSelectSaveSlot) return;
            try
            {
                slots.RecoverCampaign(campaignId, slot);
                RefreshSlots();
                if (!controller.InitializeMenu(catalog, journeys, SelectedSavePath())) return;
                controller.SetStatus(SpiritStatus.Recovered(slot));
            }
            catch (Exception exception) when (Expected(exception))
            {
                controller.SetStatus(SpiritStatus.Of(SpiritStatusKind.CampaignRecoveryFailed, exception.Message));
            }
        }

        public void OpenSavedChapter()
        {
            if (!CanOpenSavedChapter) return;
            if (!PrepareSelectedCampaign()) return;
            if (!controller.OpenSavedChapter()) ReturnWithError();
        }

        public void ShowMainMenu()
        {
            if (!IsInitialized || controller.IsBusy) return;
            if (controller.Snapshot != null && !controller.TrySave()) return;
            RefreshSlots();
            controller.ShowMainMenu();
        }

        private bool PrepareSelectedCampaign()
        {
            // Clear retained profile/run state before loading so campaigns never merge across slots.
            if (!controller.InitializeMenu(catalog, journeys, SelectedSavePath())) return false;
            if (controller.InitializeJourney(catalog, journeys, SelectedSavePath())) return true;
            ReturnWithError();
            return false;
        }

        private void ReturnWithError()
        {
            var status = controller.Status;
            controller.ShowMainMenu();
            controller.SetStatus(status);
        }

        private string SelectedSavePath() => SavePath(slots, RootDirectory, ActiveSaveSlot);

        /** The slot's run save, or for an empty slot a path in a directory named for the slot. */
        private static string SavePath(CampaignSaveSlots slots, string root, int slot)
            => slots.GetSavePath(slot) ?? Path.Combine(root, "empty-slot-" + slot, EncounterSaveFileStore.FileName);

        /** The slot and file failures the main menu reports as a status; lookup and seed failures still throw. */
        private static bool Expected(Exception exception) => exception is IOException || exception is UnauthorizedAccessException ||
            exception is SecurityException || exception is FormatException || exception is ArgumentException ||
            exception is InvalidOperationException || exception is NotSupportedException || exception is OverflowException;

        private void RefreshSlots()
        {
            saveSlots.Clear();
            for (int slot = 1; slot <= CampaignSaveSlots.SlotCount; ++slot)
            {
                if (!slots.HasGame(slot))
                {
                    saveSlots.Add(new SpiritSaveSlotInfo(slot, false, false, 0, 0));
                    continue;
                }
                PlayerProfile profile;
                try
                {
                    profile = PlayerProfileFileStore.LoadOrCreate(slots.GetProfilePath(slot));
                }
                catch (Exception exception) when (Expected(exception))
                {
                    saveSlots.Add(new SpiritSaveSlotInfo(slot, true, false, 0, 0));
                    continue;
                }
                saveSlots.Add(new SpiritSaveSlotInfo(slot, true, true, profile.CompletedEncounterIds.Count,
                    profile.UnlockedModifierIds.Count));
            }
            recoveryCampaigns.Clear();
            foreach (var candidate in slots.RecoveryCampaigns)
            {
                var profile = PlayerProfileFileStore.LoadOrCreate(candidate.ProfilePath);
                recoveryCampaigns.Add(new SpiritRecoveryCampaignInfo(candidate.CampaignId, profile.CompletedEncounterIds.Count,
                    profile.UnlockedModifierIds.Count, candidate.LastWriteTimeUtc.ToLocalTime(), candidate.SuggestedSlot));
            }
        }

    }
}
