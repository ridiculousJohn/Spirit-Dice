using System;
using System.Collections.Generic;

namespace Dice.Presentation
{
    /**
     * Read-only presentation of one campaign save slot: whether it holds a saved game and, when its progress could be
     * read, the encounters its profile completed and the charms it collected.
     */
    public readonly struct SpiritSaveSlotInfo
    {
        public int SlotNumber { get; }
        public bool HasSavedGame { get; }
        public bool ProgressReadable { get; }
        public int CompletedEncounters { get; }
        public int CollectedCharms { get; }

        public SpiritSaveSlotInfo(int slotNumber, bool hasSavedGame, bool progressReadable, int completedEncounters,
            int collectedCharms)
        {
            SlotNumber = slotNumber;
            HasSavedGame = hasSavedGame;
            ProgressReadable = progressReadable;
            CompletedEncounters = completedEncounters;
            CollectedCharms = collectedCharms;
        }
    }

    /** Read-only presentation of a campaign awaiting a destination save slot; LastSaved is local time. */
    public readonly struct SpiritRecoveryCampaignInfo
    {
        public Guid CampaignId { get; }
        public int CompletedEncounters { get; }
        public int CollectedCharms { get; }
        public DateTime LastSaved { get; }
        public int SuggestedSlot { get; }

        public SpiritRecoveryCampaignInfo(Guid campaignId, int completedEncounters, int collectedCharms, DateTime lastSaved,
            int suggestedSlot)
        {
            CampaignId = campaignId;
            CompletedEncounters = completedEncounters;
            CollectedCharms = collectedCharms;
            LastSaved = lastSaved;
            SuggestedSlot = suggestedSlot;
        }
    }

    /** Application menu actions and their current availability. */
    public interface ISpiritMainMenuControls
    {
        bool CanContinueGame { get; }
        bool CanOpenSavedChapter { get; }
        bool CanStartNewGame { get; }
        int ActiveSaveSlot { get; }
        IReadOnlyList<SpiritSaveSlotInfo> SaveSlots { get; }
        IReadOnlyList<SpiritRecoveryCampaignInfo> RecoveryCampaigns { get; }
        bool CanSelectSaveSlot { get; }

        void ContinueGame();
        void OpenSavedChapter();
        void SelectSaveSlot(int slot);
        void NewGame(int slot);
        void RecoverCampaign(Guid campaignId, int slot);
        void ShowMainMenu();
    }
}
