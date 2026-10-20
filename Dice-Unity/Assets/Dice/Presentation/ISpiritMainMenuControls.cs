using System.Collections.Generic;

namespace Dice.Presentation
{
    /** Read-only presentation of one campaign save slot. */
    public readonly struct SpiritSaveSlotInfo
    {
        public int SlotNumber { get; }
        public bool HasSavedGame { get; }
        public string Summary { get; }

        public SpiritSaveSlotInfo(int slotNumber, bool hasSavedGame, string summary)
        {
            SlotNumber = slotNumber;
            HasSavedGame = hasSavedGame;
            Summary = summary;
        }
    }

    /** Read-only presentation of a campaign awaiting a destination save slot. */
    public readonly struct SpiritRecoveryCampaignInfo
    {
        public string CampaignId { get; }
        public string Summary { get; }
        public string LastSaved { get; }
        public int SuggestedSlot { get; }

        public SpiritRecoveryCampaignInfo(string campaignId, string summary, string lastSaved, int suggestedSlot)
        {
            CampaignId = campaignId;
            Summary = summary;
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
        void RecoverCampaign(string campaignId, int slot);
        void ShowMainMenu();
    }
}
