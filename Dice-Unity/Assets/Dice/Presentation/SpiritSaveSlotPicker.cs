using System;
using UnityEngine;
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
        private string recoveryCampaignId;
        private readonly Button[] slotButtons = new Button[3];
        private readonly Text[] slotSummaries = new Text[3];

        public RectTransform Root { get; private set; }
        public Text Title { get; private set; }
        public Text Subtitle { get; private set; }
        public Button CloseButton { get; private set; }
        public Button RecoverSavesButton { get; private set; }
        public SpiritCampaignRecoveryPicker RecoveryPicker { get; private set; }
        public PickerMode Mode => mode;
        public RectTransform Confirmation { get; private set; }
        public Text ConfirmationTitle { get; private set; }
        public Text ConfirmationText { get; private set; }
        public Button ConfirmReplaceSaveButton { get; private set; }
        public Button CancelReplaceSaveButton { get; private set; }
        public System.Collections.Generic.IReadOnlyList<Button> SlotButtons => slotButtons;
        public bool IsOpen => Root != null && Root.gameObject.activeSelf;
        public bool IsConfirmingReplacement => Confirmation != null && Confirmation.gameObject.activeSelf;

        public void Build(RectTransform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (Root != null) throw new InvalidOperationException("The save slot picker has already been built.");

            var theme = SpiritUi.GetTheme(transform);
            Root = SpiritUi.Panel("SaveSlotPicker", parent, Vector2.zero, Vector2.one,
                new Color(0, 0, 0, .78f)).rectTransform;
            var panel = SpiritUi.Panel("SaveSlotPickerPanel", Root,
                new Vector2(.065f, .22f), new Vector2(.935f, .78f), theme.panelColor, 0, true).rectTransform;
            Title = SpiritUi.Heading("SaveSlotPickerTitle", panel, "Save Slots", 28,
                new Vector2(.04f, .83f), new Vector2(.96f, .97f), TextAnchor.MiddleCenter, theme.amber);
            Subtitle = SpiritUi.Label("SaveSlotPickerSubtitle", panel, "Choose a save slot.", 18,
                new Vector2(.04f, .74f), new Vector2(.96f, .84f), TextAnchor.MiddleCenter, theme.muted);
            for (int i = 0; i < slotButtons.Length; ++i)
            {
                int slot = i + 1;
                float top = .73f - i * .19f;
                var button = SpiritUi.Button("SaveSlotButton" + slot, panel, "Slot " + slot,
                    new Vector2(.055f, top - .18f), new Vector2(.945f, top), () => SelectSlot(slot), compact: true);
                slotButtons[i] = button;
                var label = button.GetComponentInChildren<Text>();
                label.rectTransform.anchorMin = new Vector2(.09f, .46f);
                label.rectTransform.anchorMax = new Vector2(.91f, .89f);
                var summary = SpiritUi.Label("SaveSlotSummary" + slot, button.transform, "", 16,
                    new Vector2(.09f, .08f), new Vector2(.91f, .48f), TextAnchor.MiddleCenter, theme.muted);
                summary.rectTransform.offsetMin = summary.rectTransform.offsetMax = Vector2.zero;
                summary.resizeTextForBestFit = true;
                summary.resizeTextMinSize = 12;
                summary.resizeTextMaxSize = 16;
                slotSummaries[i] = summary;
                button.GetComponent<SpiritUiButtonVisual>().CaptureContent();
            }
            CloseButton = SpiritUi.Button("CloseSaveSlotPickerButton", panel, "Cancel",
                new Vector2(.22f, .025f), new Vector2(.78f, .15f), Cancel, compact: true);
            RecoverSavesButton = SpiritUi.Button("RecoverSavesButton", panel, "Recover saves",
                new Vector2(.12f, .155f), new Vector2(.88f, .28f), () => RecoveryPicker.Show(), compact: true);

            Confirmation = SpiritUi.Panel("ReplaceSaveConfirmation", Root, Vector2.zero, Vector2.one,
                new Color(0, 0, 0, .78f)).rectTransform;
            var confirmationPanel = SpiritUi.Panel("ReplaceSaveConfirmationPanel", Confirmation,
                new Vector2(.065f, .3f), new Vector2(.935f, .68f), theme.panelColor, 0, true).rectTransform;
            ConfirmationTitle = SpiritUi.Heading("ReplaceSaveConfirmationTitle", confirmationPanel, "", 28,
                new Vector2(.04f, .75f), new Vector2(.96f, .96f), TextAnchor.MiddleCenter, theme.amber);
            ConfirmationText = SpiritUi.Label("ReplaceSaveConfirmationText", confirmationPanel,
                "This will reset its chapter progress, collected charms, and current encounter.", 20,
                new Vector2(.055f, .31f), new Vector2(.945f, .76f), TextAnchor.MiddleCenter);
            ConfirmReplaceSaveButton = SpiritUi.Button("ConfirmReplaceSaveButton", confirmationPanel, "New Game",
                new Vector2(.04f, .065f), new Vector2(.5f, .28f), ConfirmReplacement, compact: true);
            CancelReplaceSaveButton = SpiritUi.Button("CancelReplaceSaveButton", confirmationPanel, "Cancel",
                new Vector2(.5f, .065f), new Vector2(.96f, .28f), CloseConfirmation, compact: true);
            RecoveryPicker = gameObject.AddComponent<SpiritCampaignRecoveryPicker>();
            RecoveryPicker.Build(parent);
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
            Title.text = newGame ? "New Game" : "Load Game";
            Subtitle.text = "Choose a save slot.";
            ConfirmationText.text = "This will reset its chapter progress, collected charms, and current encounter.";
            SpiritUi.SetButtonLabel(ConfirmReplaceSaveButton, "New Game");
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
            SpiritUi.SetButtonLabel(RecoverSavesButton, "Recover saves (" + recoveryCount + ")");
            for (int i = 0; i < slotButtons.Length; ++i)
            {
                float top = .73f - i * (showRecovery ? .15f : .19f);
                var rect = (RectTransform)slotButtons[i].transform;
                rect.anchorMin = new Vector2(.055f, top - (showRecovery ? .14f : .18f));
                rect.anchorMax = new Vector2(.945f, top);
                int slot = i + 1;
                var info = GetSlot(slot);
                SpiritUi.SetButtonLabel(slotButtons[i], "Slot " + slot + (controls.ActiveSaveSlot == slot ? " - Active" : ""));
                slotSummaries[i].text = !string.IsNullOrWhiteSpace(info.Summary) ? info.Summary :
                    info.HasSavedGame ? "Saved game" : "Empty";
                slotButtons[i].interactable = mode != PickerMode.Select || info.HasSavedGame;
            }
            ConfirmReplaceSaveButton.interactable = mode == PickerMode.Recover ?
                controls.CanSelectSaveSlot : controls.CanStartNewGame;
            RecoveryPicker.Refresh();
        }

        public void Close()
        {
            CloseConfirmation();
            RecoveryPicker?.Close();
            if (Root != null) Root.gameObject.SetActive(false);
        }

        public void CloseConfirmation()
        {
            replacementSlot = 0;
            if (Confirmation != null) Confirmation.gameObject.SetActive(false);
        }

        private SpiritSaveSlotInfo GetSlot(int slot)
        {
            if (controls?.SaveSlots != null)
                foreach (var info in controls.SaveSlots)
                    if (info.SlotNumber == slot) return info;
            return new SpiritSaveSlotInfo(slot, false, "Empty");
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
                if (!controls.CanSelectSaveSlot || string.IsNullOrEmpty(recoveryCampaignId)) return;
                if (!GetSlot(slot).HasSavedGame)
                {
                    RecoverCampaign(slot);
                    return;
                }
                replacementSlot = slot;
                ConfirmationTitle.text = "Use save slot " + slot + "?";
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
            ConfirmationTitle.text = "Replace save slot " + slot + "?";
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

        private void ShowRecovery(string campaignId)
        {
            if (controls == null || !controls.CanSelectSaveSlot || string.IsNullOrEmpty(campaignId)) return;
            mode = PickerMode.Recover;
            recoveryCampaignId = campaignId;
            CloseConfirmation();
            Title.text = "Recover save";
            Subtitle.text = "Choose a destination slot.";
            ConfirmationText.text = "The existing campaign will remain available under Recover saves.";
            SpiritUi.SetButtonLabel(ConfirmReplaceSaveButton, "Recover");
            Root.SetAsLastSibling();
            Root.gameObject.SetActive(true);
            Refresh();
        }

        private void RecoverCampaign(int slot)
        {
            string campaignId = recoveryCampaignId;
            Close();
            controls.RecoverCampaign(campaignId, slot);
            Show(false);
        }

        private void Cancel()
        {
            if (mode == PickerMode.Recover) Show(false);
            else Close();
        }

        private void OnDisable()
        {
            Close();
        }
    }
}
