using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Pages through preserved campaigns awaiting a destination save slot. */
    public sealed class SpiritCampaignRecoveryPicker : MonoBehaviour
    {
        /** Characters of a campaign id shown to tell candidates apart. */
        private const int IdentifierExcerptLength = 10;
        private ISpiritMainMenuControls controls;
        private Action<Guid> selectCampaign;
        private Action cancel;
        [SerializeField] private Button[] candidateButtons = Array.Empty<Button>();
        [SerializeField] private Text[] summaries = Array.Empty<Text>();
        [SerializeField] private Text[] details = Array.Empty<Text>();
        [SerializeField] private Text[] identifiers = Array.Empty<Text>();
        private Guid?[] campaignIds = Array.Empty<Guid?>();
        private UnityAction[] candidateSelections;

        [field: SerializeField] public RectTransform Root { get; private set; }
        [field: SerializeField] public Button CloseButton { get; private set; }
        [field: SerializeField] public Button PreviousPageButton { get; private set; }
        [field: SerializeField] public Button NextPageButton { get; private set; }
        [field: SerializeField] public Text PageLabel { get; private set; }
        public IReadOnlyList<Button> CandidateButtons => candidateButtons;
        public int PageIndex { get; private set; }
        public bool IsOpen => Root != null && Root.gameObject.activeSelf;

        public void Build()
        {
            if (Root == null || CloseButton == null || PreviousPageButton == null ||
                NextPageButton == null || PageLabel == null || candidateButtons == null ||
                summaries == null || details == null || identifiers == null || candidateButtons.Length == 0 ||
                summaries.Length != candidateButtons.Length || details.Length != candidateButtons.Length ||
                identifiers.Length != candidateButtons.Length)
                throw new InvalidOperationException("The recovery picker requires authored UI references.");
            for (int i = 0; i < candidateButtons.Length; ++i)
                if (candidateButtons[i] == null || summaries[i] == null || details[i] == null || identifiers[i] == null)
                    throw new InvalidOperationException("The recovery picker requires a summary, detail and identifier for every authored candidate.");
            SpiritUi.RequireWording(transform);
            if (candidateSelections == null)
            {
                campaignIds = new Guid?[candidateButtons.Length];
                candidateSelections = new UnityAction[candidateButtons.Length];
                for (int i = 0; i < candidateSelections.Length; ++i)
                {
                    int index = i;
                    candidateSelections[i] = () => SelectCandidate(index);
                }
            }
            for (int i = 0; i < candidateButtons.Length; ++i)
            {
                candidateButtons[i].onClick.RemoveListener(candidateSelections[i]);
                candidateButtons[i].onClick.AddListener(candidateSelections[i]);
            }
            PreviousPageButton.onClick.RemoveListener(PreviousPage);
            PreviousPageButton.onClick.AddListener(PreviousPage);
            NextPageButton.onClick.RemoveListener(NextPage);
            NextPageButton.onClick.AddListener(NextPage);
            CloseButton.onClick.RemoveListener(Cancel);
            CloseButton.onClick.AddListener(Cancel);
            Close();
        }

        public void Bind(ISpiritMainMenuControls menuControls, Action<Guid> onSelectCampaign, Action onCancel)
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
                campaignIds[i] = visible ? campaigns[index].CampaignId : (Guid?)null;
                if (!visible) continue;
                var info = campaigns[index];
                summaries[i].text = wording.saveSlots.Progress(info.CompletedEncounters, info.CollectedCharms);
                details[i].text = wording.recovery.SavedAt(info.LastSaved);
                identifiers[i].text = wording.recovery.Identifier(info.SuggestedSlot,
                    info.CampaignId.ToString("N").Substring(0, IdentifierExcerptLength));
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
            if (!IsOpen || controls == null || !controls.CanSelectSaveSlot || campaignIds[index] == null) return;
            Guid campaignId = campaignIds[index].Value;
            Close();
            selectCampaign?.Invoke(campaignId);
        }

        private void PreviousPage() => ChangePage(-1);
        private void NextPage() => ChangePage(1);

        private void OnDisable()
        {
            Close();
        }
    }
}
