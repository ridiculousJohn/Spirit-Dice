using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Pages through preserved campaigns awaiting a destination save slot. */
    public sealed class SpiritCampaignRecoveryPicker : MonoBehaviour
    {
        private ISpiritMainMenuControls controls;
        private Action<string> selectCampaign;
        private Action cancel;
        private readonly Button[] candidateButtons = new Button[3];
        private readonly Text[] summaries = new Text[3];
        private readonly Text[] details = new Text[3];
        private readonly Text[] identifiers = new Text[3];
        private readonly string[] campaignIds = new string[3];

        public RectTransform Root { get; private set; }
        public Button CloseButton { get; private set; }
        public Button PreviousPageButton { get; private set; }
        public Button NextPageButton { get; private set; }
        public Text PageLabel { get; private set; }
        public IReadOnlyList<Button> CandidateButtons => candidateButtons;
        public int PageIndex { get; private set; }
        public bool IsOpen => Root != null && Root.gameObject.activeSelf;

        public void Build(RectTransform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (Root != null) throw new InvalidOperationException("The campaign recovery picker has already been built.");

            var theme = SpiritUi.GetTheme(transform);
            Root = SpiritUi.Panel("CampaignRecoveryPicker", parent, Vector2.zero, Vector2.one,
                new Color(0, 0, 0, .78f)).rectTransform;
            var panel = SpiritUi.Panel("CampaignRecoveryPickerPanel", Root,
                new Vector2(.065f, .22f), new Vector2(.935f, .90f), theme.panelColor, 0, true).rectTransform;
            SpiritUi.Heading("CampaignRecoveryPickerTitle", panel, "Recover saves", 28,
                new Vector2(.04f, .88f), new Vector2(.96f, .98f), TextAnchor.MiddleCenter, theme.amber);
            SpiritUi.Label("CampaignRecoveryPickerSubtitle", panel, "Choose a campaign to recover.", 18,
                new Vector2(.04f, .78f), new Vector2(.96f, .88f), TextAnchor.MiddleCenter, theme.muted);
            for (int i = 0; i < candidateButtons.Length; ++i)
            {
                int index = i;
                float top = .77f - i * .19f;
                var button = SpiritUi.Button("RecoveryCampaignButton" + (i + 1), panel, "",
                    new Vector2(.055f, top - .18f), new Vector2(.945f, top), () => SelectCandidate(index), compact: true);
                candidateButtons[i] = button;
                var summary = button.GetComponentInChildren<Text>();
                summary.rectTransform.anchorMin = new Vector2(.07f, .54f);
                summary.rectTransform.anchorMax = new Vector2(.93f, .92f);
                summary.horizontalOverflow = HorizontalWrapMode.Wrap;
                summary.resizeTextMinSize = 14;
                summary.resizeTextMaxSize = 18;
                summaries[i] = summary;
                details[i] = SpiritUi.Label("RecoveryCampaignDetails" + (i + 1), button.transform, "", 15,
                    new Vector2(.07f, .29f), new Vector2(.93f, .54f), TextAnchor.MiddleCenter, theme.muted);
                details[i].rectTransform.offsetMin = details[i].rectTransform.offsetMax = Vector2.zero;
                identifiers[i] = SpiritUi.Label("RecoveryCampaignIdentifier" + (i + 1), button.transform, "", 14,
                    new Vector2(.07f, .06f), new Vector2(.93f, .29f), TextAnchor.MiddleCenter, theme.muted);
                identifiers[i].rectTransform.offsetMin = identifiers[i].rectTransform.offsetMax = Vector2.zero;
                button.GetComponent<SpiritUiButtonVisual>().CaptureContent();
            }
            PreviousPageButton = SpiritUi.Button("PreviousRecoveryPageButton", panel, "Prev",
                new Vector2(.045f, .115f), new Vector2(.34f, .205f), () => ChangePage(-1), compact: true);
            PageLabel = SpiritUi.Label("RecoveryPageLabel", panel, "", 16,
                new Vector2(.34f, .115f), new Vector2(.66f, .205f), TextAnchor.MiddleCenter, theme.muted);
            NextPageButton = SpiritUi.Button("NextRecoveryPageButton", panel, "Next",
                new Vector2(.66f, .115f), new Vector2(.955f, .205f), () => ChangePage(1), compact: true);
            CloseButton = SpiritUi.Button("CloseCampaignRecoveryPickerButton", panel, "Cancel",
                new Vector2(.22f, .015f), new Vector2(.78f, .115f), Cancel, compact: true);
            Close();
        }

        public void Bind(ISpiritMainMenuControls menuControls, Action<string> onSelectCampaign, Action onCancel)
        {
            Close();
            controls = menuControls;
            selectCampaign = onSelectCampaign;
            cancel = onCancel;
        }

        public void Show()
        {
            if (controls == null || !controls.CanSelectSaveSlot || controls.RecoveryCampaigns == null ||
                controls.RecoveryCampaigns.Count == 0) return;
            PageIndex = 0;
            Root.SetAsLastSibling();
            Root.gameObject.SetActive(true);
            Refresh();
        }

        public void Refresh()
        {
            if (!IsOpen) return;
            var campaigns = controls?.RecoveryCampaigns;
            if (controls == null || !controls.CanSelectSaveSlot || campaigns == null || campaigns.Count == 0)
            {
                Cancel();
                return;
            }
            int pageCount = (campaigns.Count + candidateButtons.Length - 1) / candidateButtons.Length;
            PageIndex = Mathf.Clamp(PageIndex, 0, pageCount - 1);
            for (int i = 0; i < candidateButtons.Length; ++i)
            {
                int index = PageIndex * candidateButtons.Length + i;
                bool visible = index < campaigns.Count;
                candidateButtons[i].gameObject.SetActive(visible);
                campaignIds[i] = visible ? campaigns[index].CampaignId : null;
                if (!visible) continue;
                var info = campaigns[index];
                summaries[i].text = string.IsNullOrWhiteSpace(info.Summary) ? "Saved campaign" : info.Summary;
                string saved = string.IsNullOrWhiteSpace(info.LastSaved) ? "Unknown" : info.LastSaved;
                details[i].text = "Saved " + saved;
                string id = info.CampaignId ?? "";
                identifiers[i].text = "Slot " + (info.SuggestedSlot > 0 ? info.SuggestedSlot.ToString() : "?") +
                    " / Save " + (id.Length > 10 ? id.Substring(0, 10) : id);
                candidateButtons[i].interactable = !string.IsNullOrEmpty(info.CampaignId);
            }
            PageLabel.text = (PageIndex + 1) + " / " + pageCount;
            PreviousPageButton.interactable = PageIndex > 0;
            NextPageButton.interactable = PageIndex + 1 < pageCount;
        }

        public void Close()
        {
            if (Root != null) Root.gameObject.SetActive(false);
        }

        private void Cancel()
        {
            Close();
            cancel?.Invoke();
        }

        private void ChangePage(int direction)
        {
            if (!IsOpen) return;
            PageIndex += direction;
            Refresh();
        }

        private void SelectCandidate(int index)
        {
            if (!IsOpen || controls == null || !controls.CanSelectSaveSlot || string.IsNullOrEmpty(campaignIds[index])) return;
            string campaignId = campaignIds[index];
            Close();
            selectCampaign?.Invoke(campaignId);
        }

        private void OnDisable()
        {
            Close();
        }
    }
}
