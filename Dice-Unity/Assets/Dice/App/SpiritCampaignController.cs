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
                string nextSavePath = nextSlots.GetSavePath(nextSlots.ActiveSlot) ??
                    Path.Combine(nextRoot, "empty-slot-" + nextSlots.ActiveSlot, "spirit-encounter.json");
                if (!controller.InitializeMenu(mechanicalCatalog, journeyCatalog, nextSavePath)) return false;
                catalog = mechanicalCatalog;
                journeys = journeyCatalog;
                RootDirectory = nextRoot;
                slots = nextSlots;
                IsInitialized = true;
                RefreshSlots();
                controller.SetStatus(recoveredIndex == null ? "Main menu ready." :
                    recoveryCampaigns.Count == 0 ? "Save-slot list recovered." :
                    "Save-slot list recovered. Open Load Game to recover unassigned saves.");
                return true;
            }
            catch (Exception exception) when (Expected(exception))
            {
                controller.SetStatus("Cannot open save slots: " + exception.Message);
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
                controller.SetStatus("Cannot select save slot: " + exception.Message);
            }
        }

        public void NewGame(int slot)
        {
            if (!CanStartNewGame) return;
            try
            {
                string cleanupWarning = slots.StartNewGame(slot);
                RefreshSlots();
                if (!controller.InitializeMenu(catalog, journeys, SelectedSavePath())) return;
                if (!controller.InitializeJourney(catalog, journeys, SelectedSavePath())) ReturnWithError();
                else if (cleanupWarning != null) controller.SetStatus(cleanupWarning);
            }
            catch (Exception exception) when (Expected(exception))
            {
                controller.SetStatus("Cannot start a new game: " + exception.Message);
            }
        }

        public void ContinueGame()
        {
            if (!CanContinueGame) return;
            if (!PrepareSelectedCampaign()) return;
            if (File.Exists(controller.SavePath) && !controller.TryLoad()) ReturnWithError();
        }

        public void RecoverCampaign(string campaignId, int slot)
        {
            if (!CanSelectSaveSlot) return;
            try
            {
                slots.RecoverCampaign(campaignId, slot);
                RefreshSlots();
                if (!controller.InitializeMenu(catalog, journeys, SelectedSavePath())) return;
                controller.SetStatus("Campaign recovered to slot " + slot + ".");
            }
            catch (Exception exception) when (Expected(exception))
            {
                controller.SetStatus("Cannot recover campaign: " + exception.Message);
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
            string message = controller.StatusMessage;
            controller.ShowMainMenu();
            controller.SetStatus(message);
        }

        private string SelectedSavePath() => slots.GetSavePath(ActiveSaveSlot) ??
            Path.Combine(RootDirectory, "empty-slot-" + ActiveSaveSlot, "spirit-encounter.json");

        private void RefreshSlots()
        {
            saveSlots.Clear();
            for (int slot = 1; slot <= CampaignSaveSlots.SlotCount; ++slot)
            {
                bool occupied = slots.HasGame(slot);
                string summary = "Empty";
                if (occupied)
                {
                    try
                    {
                        summary = ProgressSummary(slots.GetProfilePath(slot));
                    }
                    catch (Exception exception) when (Expected(exception)) { summary = "Saved campaign - unable to read progress"; }
                }
                saveSlots.Add(new SpiritSaveSlotInfo(slot, occupied, summary));
            }
            recoveryCampaigns.Clear();
            foreach (var candidate in slots.RecoveryCampaigns)
                recoveryCampaigns.Add(new SpiritRecoveryCampaignInfo(candidate.CampaignId,
                    ProgressSummary(candidate.ProfilePath),
                    candidate.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), candidate.SuggestedSlot));
        }

        private static string ProgressSummary(string profilePath)
        {
            var profile = PlayerProfileFileStore.LoadOrCreate(profilePath);
            return profile.CompletedEncounterIds.Count + " encounters completed / " +
                profile.UnlockedModifierIds.Count + " charms collected";
        }

        private static bool Expected(Exception exception) => exception is IOException || exception is UnauthorizedAccessException ||
            exception is SecurityException || exception is FormatException || exception is ArgumentException ||
            exception is InvalidOperationException || exception is NotSupportedException || exception is OverflowException;
    }
}
