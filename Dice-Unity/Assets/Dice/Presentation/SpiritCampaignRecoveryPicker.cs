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
        [SerializeField] private Button[] candidateButtons = new Button[3];
        [SerializeField] private Text[] summaries = new Text[3];
        [SerializeField] private Text[] details = new Text[3];
        [SerializeField] private Text[] identifiers = new Text[3];
        private readonly string[] campaignIds = new string[3];

        [field: SerializeField] public RectTransform Root { get; private set; }
        [field: SerializeField] public Button CloseButton { get; private set; }
        [field: SerializeField] public Button PreviousPageButton { get; private set; }
        [field: SerializeField] public Button NextPageButton { get; private set; }
        [field: SerializeField] public Text PageLabel { get; private set; }
        public IReadOnlyList<Button> CandidateButtons => candidateButtons;
        public int PageIndex { get; private set; }
        public bool IsOpen => Root != null && Root.gameObject.activeSelf;

        public void Build(RectTransform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (Root == null || CloseButton == null || PreviousPageButton == null ||
                NextPageButton == null || PageLabel == null || candidateButtons == null ||
                summaries == null || details == null || identifiers == null || candidateButtons.Length != 3 ||
                summaries.Length != 3 || details.Length != 3 || identifiers.Length != 3)
                throw new InvalidOperationException("The recovery picker requires authored UI references.");
            for (int i = 0; i < candidateButtons.Length; ++i)
                if (candidateButtons[i] == null || summaries[i] == null || details[i] == null || identifiers[i] == null)
                    throw new InvalidOperationException("The recovery picker requires three authored candidates.");
            SpiritUi.RequireWording(transform);
            candidateButtons[0].onClick.RemoveListener(SelectFirstCandidate);
            candidateButtons[0].onClick.AddListener(SelectFirstCandidate);
            candidateButtons[1].onClick.RemoveListener(SelectSecondCandidate);
            candidateButtons[1].onClick.AddListener(SelectSecondCandidate);
            candidateButtons[2].onClick.RemoveListener(SelectThirdCandidate);
            candidateButtons[2].onClick.AddListener(SelectThirdCandidate);
            PreviousPageButton.onClick.RemoveListener(PreviousPage);
            PreviousPageButton.onClick.AddListener(PreviousPage);
            NextPageButton.onClick.RemoveListener(NextPage);
            NextPageButton.onClick.AddListener(NextPage);
            CloseButton.onClick.RemoveListener(Cancel);
            CloseButton.onClick.AddListener(Cancel);
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
            var wording = SpiritUi.GetWording(transform);
            for (int i = 0; i < candidateButtons.Length; ++i)
            {
                int index = PageIndex * candidateButtons.Length + i;
                bool visible = index < campaigns.Count;
                candidateButtons[i].gameObject.SetActive(visible);
                campaignIds[i] = visible ? campaigns[index].CampaignId : null;
                if (!visible) continue;
                var info = campaigns[index];
                summaries[i].text = wording.saveSlots.Progress(info.CompletedEncounters, info.CollectedCharms);
                details[i].text = wording.recovery.SavedAt(info.LastSaved);
                string id = info.CampaignId ?? "";
                identifiers[i].text = wording.recovery.Identifier(info.SuggestedSlot, id.Length > 10 ? id.Substring(0, 10) : id);
                candidateButtons[i].interactable = !string.IsNullOrEmpty(info.CampaignId);
            }
            PageLabel.text = wording.recovery.Page(PageIndex + 1, pageCount);
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

        private void SelectFirstCandidate() => SelectCandidate(0);
        private void SelectSecondCandidate() => SelectCandidate(1);
        private void SelectThirdCandidate() => SelectCandidate(2);
        private void PreviousPage() => ChangePage(-1);
        private void NextPage() => ChangePage(1);

        private void OnDisable()
        {
            Close();
        }
    }
}
