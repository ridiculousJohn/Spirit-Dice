using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Presents the pending charm, hand-upgrade, or unlock offer; a choice is selected, then confirmed. */
    public sealed class SpiritRewardsPanel : MonoBehaviour
    {
        private readonly List<SpiritRewardCard> cards = new List<SpiritRewardCard>();
        private ISpiritEncounterControls controls;
        private Func<bool> isBusy;
        [SerializeField] private SpiritRewardCard cardPrefab;
        private long renderedOffer = -1;
        private long selectedOption = -1;

        public RectTransform Root => (RectTransform)transform;
        [field: SerializeField] public Text Title { get; private set; }
        [field: SerializeField] public Text Instruction { get; private set; }
        [field: SerializeField] public ScrollRect Scroll { get; private set; }
        [field: SerializeField] public RectTransform Content { get; private set; }
        [field: SerializeField] public Button RefreshButton { get; private set; }
        [field: SerializeField] public Button ConfirmButton { get; private set; }
        public IReadOnlyList<SpiritRewardCard> Cards => cards;

        public void Build(Func<bool> busy)
        {
            isBusy = busy ?? throw new ArgumentNullException(nameof(busy));
            SpiritUi.RequireWording(transform);
            RefreshButton.onClick.RemoveAllListeners();
            RefreshButton.onClick.AddListener(() => { if (!isBusy()) { ClearSelection(); controls.RefreshRewards(); } });
            ConfirmButton.onClick.RemoveAllListeners();
            ConfirmButton.onClick.AddListener(Confirm);
        }

        public void Bind(ISpiritEncounterControls encounterControls)
        {
            controls = encounterControls ?? throw new ArgumentNullException(nameof(encounterControls));
        }

        /** Forces the next render to rebuild its cards, as when a different snapshot is presented. */
        public void Invalidate() => renderedOffer = -2;

        public void Render(EncounterSnapshot snapshot)
        {
            var offer = snapshot.PendingReward;
            // An offer without options, made only of a kind offered when nothing is eligible, is acknowledged.
            bool empty = offer != null && offer.Options.Count == 0;
            var wording = SpiritUi.GetWording(transform).rewards;
            var kindWording = offer == null ? null : wording.For(offer.Kind);
            string instruction = kindWording == null ? null : empty ? kindWording.emptyInstruction : kindWording.instruction;
            Title.text = kindWording == null ? string.Empty : empty ? kindWording.emptyTitle : kindWording.title;
            Instruction.gameObject.SetActive(!string.IsNullOrWhiteSpace(instruction));
            Instruction.text = instruction ?? string.Empty;
            ConfirmButton.gameObject.SetActive(offer != null);
            if (kindWording != null)
                SpiritUi.SetButtonLabel(ConfirmButton, empty ? kindWording.emptyConfirmButton : kindWording.confirmButton);
            bool canRefresh = offer != null && offer.Kind.Refreshable;
            RefreshButton.gameObject.SetActive(canRefresh);
            RefreshButton.interactable = canRefresh && snapshot.RewardRefreshesAvailable > 0;
            SpiritUi.SetButtonLabel(RefreshButton, wording.RefreshButton(snapshot.RewardRefreshesAvailable));
            long offerId = offer?.OfferId ?? -1;
            if (renderedOffer == offerId) return;
            renderedOffer = offerId;
            ClearSelection();
            ConfirmButton.interactable = empty;
            int count = offer?.Options.Count ?? 0;
            while (cards.Count < count)
                cards.Add(SpiritRewardCard.Create(cardPrefab, "RewardCard" + (cards.Count + 1), Content,
                    Choose));
            for (int i = 0; i < cards.Count; ++i)
            {
                var card = cards[i];
                card.Button.gameObject.SetActive(i < count);
                if (i >= count) continue;
                var option = offer.Options[i];
                if (option.Category.HasValue)
                {
                    var hand = controls.Catalog.GetHand(option.Category.Value);
                    int level = snapshot.HandUpgrades[(int)option.Category.Value];
                    card.RenderHandUpgrade(hand, level, offer.OfferId, option.OptionId);
                }
                else
                {
                    var definition = controls.Catalog.GetModifier(option.DefinitionId.Value);
                    card.RenderModifier(controls.ModifierPresentation, definition, offer.OfferId, option.OptionId);
                }
            }
            Content.anchoredPosition = Vector2.zero;
            UpdateLayout();
        }

        public void UpdateLayout() => LayoutRebuilder.MarkLayoutForRebuild(Content);

        private void Choose(long offerId, long optionId)
        {
            if (isBusy()) return;
            var offer = controls.Snapshot?.PendingReward;
            if (offer == null || offer.OfferId != offerId || renderedOffer != offerId) return;
            foreach (var option in offer.Options)
            {
                if (option.OptionId != optionId) continue;
                selectedOption = optionId;
                for (int i = 0; i < offer.Options.Count; ++i)
                {
                    bool selected = offer.Options[i].OptionId == optionId;
                    if (offer.Options[i].Category.HasValue) cards[i].SetHandUpgradeSelected(selected);
                    else cards[i].SetModifierSelected(selected);
                }
                ConfirmButton.interactable = true;
                return;
            }
        }

        private void Confirm()
        {
            if (isBusy()) return;
            var offer = controls.Snapshot?.PendingReward;
            if (offer == null || offer.OfferId != renderedOffer) return;
            if (offer.Options.Count == 0)
            {
                controls.ChooseReward(offer.OfferId, 0);
                return;
            }
            if (selectedOption < 0) return;
            foreach (var option in offer.Options)
            {
                if (option.OptionId != selectedOption) continue;
                long optionId = selectedOption;
                ClearSelection();
                controls.ChooseReward(offer.OfferId, optionId);
                return;
            }
        }

        private void ClearSelection()
        {
            selectedOption = -1;
            ConfirmButton.interactable = false;
            foreach (var card in cards)
            {
                card.SetModifierSelected(false);
                card.SetHandUpgradeSelected(false);
            }
        }
    }
}
