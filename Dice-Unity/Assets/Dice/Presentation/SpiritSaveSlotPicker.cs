using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Selects a campaign slot and confirms replacement of an occupied slot. */
    public sealed class SpiritSaveSlotPicker : MonoBehaviour
    {
        public enum PickerMode { Select, NewGame, Recover }

        private ISpiritMainMenuControls controls;
        private PickerMode mode;
        private int replacementSlot;
        private Guid? recoveryCampaignId;
        [SerializeField] private Button[] slotButtons = Array.Empty<Button>();
        [SerializeField] private Text[] slotSummaries = Array.Empty<Text>();
        private UnityAction[] slotSelections;

        [field: SerializeField] public RectTransform Root { get; private set; }
        [field: SerializeField] public Text Title { get; private set; }
        [field: SerializeField] public Text Subtitle { get; private set; }
        [field: SerializeField] public Button CloseButton { get; private set; }
        [field: SerializeField] public Button RecoverSavesButton { get; private set; }
        [field: SerializeField] public SpiritCampaignRecoveryPicker RecoveryPicker { get; private set; }
        public PickerMode Mode => mode;
        [field: SerializeField] public RectTransform Confirmation { get; private set; }
        [field: SerializeField] public Text ConfirmationTitle { get; private set; }
        [field: SerializeField] public Text ConfirmationText { get; private set; }
        [field: SerializeField] public Button ConfirmReplaceSaveButton { get; private set; }
        [field: SerializeField] public Button CancelReplaceSaveButton { get; private set; }
        public System.Collections.Generic.IReadOnlyList<Button> SlotButtons => slotButtons;
        public bool IsOpen => Root.gameObject.activeSelf;
        public bool IsConfirmingReplacement => Confirmation.gameObject.activeSelf;

        public void Build()
        {
            if (Root == null || Title == null || Subtitle == null || CloseButton == null ||
                RecoverSavesButton == null || RecoveryPicker == null || Confirmation == null ||
                ConfirmationTitle == null || ConfirmationText == null || ConfirmReplaceSaveButton == null ||
                CancelReplaceSaveButton == null || slotButtons == null || slotSummaries == null ||
                slotButtons.Length == 0 || slotSummaries.Length != slotButtons.Length)
                throw new InvalidOperationException("The save slot picker requires authored UI references.");
            for (int i = 0; i < slotButtons.Length; ++i)
                if (slotButtons[i] == null || slotSummaries[i] == null)
                    throw new InvalidOperationException("The save slot picker requires a summary for every authored slot.");
            SpiritUi.RequireWording(transform);
            RecoveryPicker.Build();
            if (slotSelections == null)
            {
                slotSelections = new UnityAction[slotButtons.Length];
                for (int i = 0; i < slotSelections.Length; ++i)
                {
                    int slot = i + 1;
                    slotSelections[i] = () => SelectSlot(slot);
                }
            }
            for (int i = 0; i < slotButtons.Length; ++i)
            {
                slotButtons[i].onClick.RemoveListener(slotSelections[i]);
                slotButtons[i].onClick.AddListener(slotSelections[i]);
            }
            CloseButton.onClick.RemoveListener(Cancel);
            CloseButton.onClick.AddListener(Cancel);
            RecoverSavesButton.onClick.RemoveListener(OpenRecovery);
            RecoverSavesButton.onClick.AddListener(OpenRecovery);
            ConfirmReplaceSaveButton.onClick.RemoveListener(ConfirmReplacement);
            ConfirmReplaceSaveButton.onClick.AddListener(ConfirmReplacement);
            CancelReplaceSaveButton.onClick.RemoveListener(CloseConfirmation);
            CancelReplaceSaveButton.onClick.AddListener(CloseConfirmation);
            Close();
        }

        public void Bind(ISpiritMainMenuControls menuControls)
        {
            Close();
            controls = menuControls;
            RecoveryPicker.Bind(menuControls, ShowRecovery, () => Show(false));
        }

        public void Show(bool newGame)
        {
            if (controls == null || (newGame ? !controls.CanStartNewGame : !controls.CanSelectSaveSlot)) return;
            mode = newGame ? PickerMode.NewGame : PickerMode.Select;
            recoveryCampaignId = null;
            RecoveryPicker.Close();
            CloseConfirmation();
            var wording = Wording;
            Title.text = newGame ? wording.newGameTitle : wording.loadGameTitle;
            Subtitle.text = wording.chooseSlot;
            ConfirmationText.text = wording.replaceWarning;
            SpiritUi.SetButtonLabel(ConfirmReplaceSaveButton, wording.newGameButton);
            Root.SetAsLastSibling();
            Root.gameObject.SetActive(true);
            Refresh();
        }

        public void Refresh()
        {
            if (!IsOpen) return;
            bool available = controls != null &&
                (mode == PickerMode.NewGame ? controls.CanStartNewGame : controls.CanSelectSaveSlot);
            if (!available) { Close(); return; }
            int recoveryCount = controls.RecoveryCampaigns?.Count ?? 0;
            bool showRecovery = mode == PickerMode.Select && recoveryCount > 0;
            RecoverSavesButton.gameObject.SetActive(showRecovery);
            var wording = Wording;
            SpiritUi.SetButtonLabel(RecoverSavesButton, wording.RecoverSavesButton(recoveryCount));
            for (int i = 0; i < slotButtons.Length; ++i)
            {
                int slot = i + 1;
                var info = GetSlot(slot);
                SpiritUi.SetButtonLabel(slotButtons[i], wording.Slot(slot, controls.ActiveSaveSlot == slot));
                slotSummaries[i].text = !info.HasSavedGame ? wording.emptySlot : !info.ProgressReadable ? wording.unreadableSlot :
                    wording.Progress(info.CompletedEncounters, info.CollectedCharms);
                slotButtons[i].interactable = mode != PickerMode.Select || info.HasSavedGame;
            }
            ConfirmReplaceSaveButton.interactable = mode == PickerMode.Recover ?
                controls.CanSelectSaveSlot : controls.CanStartNewGame;
            RecoveryPicker.Refresh();
        }

        public void Close()
        {
            CloseConfirmation();
            RecoveryPicker.Close();
            Root.gameObject.SetActive(false);
        }

        public void CloseConfirmation()
        {
            replacementSlot = 0;
            Confirmation.gameObject.SetActive(false);
        }

        private SpiritSaveSlotInfo GetSlot(int slot)
        {
            if (controls?.SaveSlots != null)
                foreach (var info in controls.SaveSlots)
                    if (info.SlotNumber == slot) return info;
            return new SpiritSaveSlotInfo(slot, false, false, 0, 0);
        }

        private void SelectSlot(int slot)
        {
            if (controls == null) return;
            if (mode == PickerMode.Select)
            {
                if (!controls.CanSelectSaveSlot || !GetSlot(slot).HasSavedGame) return;
                Close();
                controls.SelectSaveSlot(slot);
                return;
            }
            if (mode == PickerMode.Recover)
            {
                if (!controls.CanSelectSaveSlot || recoveryCampaignId == null) return;
                if (!GetSlot(slot).HasSavedGame)
                {
                    RecoverCampaign(slot);
                    return;
                }
                replacementSlot = slot;
                ConfirmationTitle.text = Wording.UseSlotConfirmation(slot);
                Confirmation.SetAsLastSibling();
                Confirmation.gameObject.SetActive(true);
                return;
            }
            if (!controls.CanStartNewGame) return;
            if (!GetSlot(slot).HasSavedGame)
            {
                Close();
                controls.NewGame(slot);
                return;
            }
            replacementSlot = slot;
            ConfirmationTitle.text = Wording.ReplaceSlotConfirmation(slot);
            Confirmation.SetAsLastSibling();
            Confirmation.gameObject.SetActive(true);
        }

        private void ConfirmReplacement()
        {
            if (mode == PickerMode.Recover)
            {
                if (controls == null || !controls.CanSelectSaveSlot || replacementSlot == 0) return;
                RecoverCampaign(replacementSlot);
                return;
            }
            if (controls == null || !controls.CanStartNewGame || replacementSlot == 0) return;
            int slot = replacementSlot;
            Close();
            controls.NewGame(slot);
        }

        private void ShowRecovery(Guid campaignId)
        {
            if (controls == null || !controls.CanSelectSaveSlot) return;
            mode = PickerMode.Recover;
            recoveryCampaignId = campaignId;
            CloseConfirmation();
            var wording = Wording;
            Title.text = wording.recoverTitle;
            Subtitle.text = wording.chooseDestination;
            ConfirmationText.text = wording.recoverNote;
            SpiritUi.SetButtonLabel(ConfirmReplaceSaveButton, wording.recoverButton);
            Root.SetAsLastSibling();
            Root.gameObject.SetActive(true);
            Refresh();
        }

        private void RecoverCampaign(int slot)
        {
            Guid campaignId = recoveryCampaignId.Value;
            Close();
            controls.RecoverCampaign(campaignId, slot);
            Show(false);
        }

        private void Cancel()
        {
            if (mode == PickerMode.Recover) Show(false);
            else Close();
        }

        private SpiritUiWording.SaveSlots Wording => SpiritUi.GetWording(transform).saveSlots;

        private void OpenRecovery() => RecoveryPicker.Show();

        private void OnDisable()
        {
            Close();
        }
    }
}
