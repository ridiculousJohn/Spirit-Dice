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
        [field: SerializeField] public Text Step { get; private set; }
        [field: SerializeField] public Text Instruction { get; private set; }
        [field: SerializeField] public Text UpcomingStage { get; private set; }
        [field: SerializeField] public ScrollRect Scroll { get; private set; }
        [field: SerializeField] public RectTransform Content { get; private set; }
        [field: SerializeField] public Button RefreshButton { get; private set; }
        [field: SerializeField] public Button ConfirmButton { get; private set; }
        public IReadOnlyList<SpiritRewardCard> Cards => cards;

        public void Build(RectTransform parent, Func<bool> busy)
        {
            isBusy = busy ?? throw new ArgumentNullException(nameof(busy));
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
            bool handUpgrade = offer != null && offer.Kind == RewardKind.HandUpgrade;
            bool unlock = offer != null && offer.Kind == RewardKind.ModifierUnlock;
            bool exhausted = unlock && offer.Options.Count == 0;
            Title.text = handUpgrade ? "Choose a hand to upgrade" : exhausted ? "All charms collected" :
                unlock ? "Unlock a charm" : "Choose a charm";
            Step.gameObject.SetActive(false);
            Instruction.gameObject.SetActive(unlock);
            UpcomingStage.gameObject.SetActive(false);
            ConfirmButton.gameObject.SetActive(offer != null);
            SpiritUi.SetButtonLabel(ConfirmButton, handUpgrade ? "Upgrade" : exhausted ? "Continue" : unlock ? "Unlock" : "Select");
            Step.text = offer == null ? "REWARDS COMPLETE" : offer.Kind == RewardKind.Modifier ?
                "CHARM CHOICE" : "HAND UPGRADE CHOICE";
            Instruction.text = offer == null ? "Preparing the next stage" : offer.Kind == RewardKind.Modifier ?
                "Choose one free charm to keep for this run" : "Choose one hand upgrade\nScroll to see all choices";
            if (unlock) Instruction.text = exhausted ? "You own every charm this encounter awards." :
                "Choose one to add permanently to your collection.";
            var encounter = controls.Catalog.GetEncounter(snapshot.EncounterId);
            int nextStageIndex = offer?.TargetStageIndex ?? snapshot.StageIndex + 1;
            if (nextStageIndex < encounter.Stages.Count)
            {
                var nextStage = encounter.Stages[nextStageIndex];
                UpcomingStage.text = "NEXT STAGE " + (nextStageIndex + 1) + " / " + snapshot.StageCount +
                    " - CLEAR " + DiagnosticText.Number(nextStage.Threshold);
            }
            else UpcomingStage.text = "";
            bool canRefresh = offer != null && offer.Kind == RewardKind.Modifier;
            RefreshButton.gameObject.SetActive(canRefresh);
            RefreshButton.interactable = canRefresh && snapshot.RewardRefreshesAvailable > 0;
            SpiritUi.SetButtonLabel(RefreshButton, "Refresh (" + snapshot.RewardRefreshesAvailable + ")");
            long offerId = offer?.OfferId ?? -1;
            if (renderedOffer == offerId) return;
            renderedOffer = offerId;
            ClearSelection();
            ConfirmButton.interactable = exhausted;
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
                if (offer.Kind == RewardKind.Modifier || unlock)
                {
                    var definition = controls.Catalog.GetModifier(option.DefinitionId);
                    card.RenderModifier(controls.ModifierPresentation, definition, offer.OfferId, option.OptionId);
                }
                else if (option.Category.HasValue)
                {
                    var hand = controls.Catalog.GetHand(option.Category.Value);
                    int level = snapshot.HandUpgrades[(int)option.Category.Value];
                    card.RenderHandUpgrade(hand, level, offer.OfferId, option.OptionId);
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
                    if (offer.Kind == RewardKind.HandUpgrade) cards[i].SetHandUpgradeSelected(selected);
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
            if (offer.Kind == RewardKind.ModifierUnlock && offer.Options.Count == 0)
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
