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
        private RectTransform portrait;
        private long renderedOffer = -1;
        private long selectedOption = -1;

        public RectTransform Root { get; private set; }
        public Text Title { get; private set; }
        public Text Step { get; private set; }
        public Text Instruction { get; private set; }
        public Text UpcomingStage { get; private set; }
        public ScrollRect Scroll { get; private set; }
        public RectTransform Content { get; private set; }
        public Button RefreshButton { get; private set; }
        public Button ConfirmButton { get; private set; }
        public IReadOnlyList<SpiritRewardCard> Cards => cards;

        public void Build(RectTransform parent, Func<bool> busy)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (busy == null) throw new ArgumentNullException(nameof(busy));
            if (Root != null) throw new InvalidOperationException("The rewards panel has already been built.");

            portrait = parent;
            isBusy = busy;
            Root = SpiritUi.Panel("RewardsPanel", parent, new Vector2(.025f, .065f), new Vector2(.975f, .80f),
                new Color32(20, 32, 27, 255), 0, true).rectTransform;
            Title = SpiritUi.Heading("RewardTitle", Root, "STAGE CLEARED", 25, new Vector2(0, .94f), Vector2.one,
                TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).amber);
            Step = SpiritUi.Label("RewardStep", Root, "", 14, new Vector2(.04f, .885f), new Vector2(.96f, .94f),
                TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).amber);
            Instruction = SpiritUi.Label("RewardInstruction", Root, "", 17, new Vector2(.04f, .80f), new Vector2(.96f, .885f),
                TextAnchor.MiddleCenter);
            Scroll = SpiritUi.Scroll("RewardScroll", Root, new Vector2(.015f, .15f), new Vector2(.985f, .80f), out var content);
            Content = content;
            var layout = Content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UpcomingStage = SpiritUi.Label("UpcomingStage", Root, "", 14, new Vector2(.04f, .08f), new Vector2(.96f, .15f),
                TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).muted);
            RefreshButton = SpiritUi.Button("RefreshRewardsButton", Root, "Refresh choices", new Vector2(.035f, 0), new Vector2(.965f, .075f),
                () => { if (!isBusy()) { ClearSelection(); controls.RefreshRewards(); } });
            var refreshLabel = RefreshButton.GetComponentInChildren<Text>();
            refreshLabel.rectTransform.anchorMin = new Vector2(.19f, .08f);
            refreshLabel.rectTransform.anchorMax = new Vector2(.81f, .92f);
            refreshLabel.resizeTextMinSize = 12;
            refreshLabel.resizeTextMaxSize = 18;
            ConfirmButton = SpiritUi.Button("UpgradeHandButton", Root, "Upgrade",
                Vector2.zero, Vector2.one, Confirm);
            ConfirmButton.gameObject.SetActive(false);
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
            int cost = encounter.RewardPolicy?.RefreshCost ?? 1;
            RefreshButton.interactable = canRefresh && snapshot.RewardRefreshes >= cost;
            SpiritUi.SetButtonLabel(RefreshButton, cost > 0
                ? "Refresh (" + (snapshot.RewardRefreshes / cost) + ")" : "Refresh");
            long offerId = offer?.OfferId ?? -1;
            if (renderedOffer == offerId) return;
            renderedOffer = offerId;
            ClearSelection();
            ConfirmButton.interactable = exhausted;
            int count = offer?.Options.Count ?? 0;
            while (cards.Count < count)
                cards.Add(SpiritRewardCard.Create("RewardCard" + (cards.Count + 1), Content,
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
                    card.RenderModifier(definition, offer.OfferId, option.OptionId);
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

        public void UpdateLayout()
        {
            var offer = controls.Snapshot?.PendingReward;
            bool handUpgrade = offer != null && offer.Kind == RewardKind.HandUpgrade;
            bool unlock = offer != null && offer.Kind == RewardKind.ModifierUnlock;
            var scrollRect = (RectTransform)Scroll.transform;
            var layout = Content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            Scroll.vertical = handUpgrade;
            Root.anchorMin = new Vector2(.025f, .065f);
            Root.anchorMax = new Vector2(.975f, unlock ? .85f : .80f);
            Root.offsetMin = Root.offsetMax = Vector2.zero;
            if (handUpgrade)
            {
                // Keep the panel fitted to its choices, with scrolling available for larger authored offers.
                float contentHeight = layout.padding.vertical + Mathf.Max(0, offer.Options.Count - 1) * layout.spacing;
                for (int i = 0; i < offer.Options.Count; ++i)
                    contentHeight += cards[i].GetComponent<LayoutElement>().preferredHeight;
                Root.anchorMax = new Vector2(.975f, .065f);
                Root.offsetMax = new Vector2(0, Mathf.Min(contentHeight + 148, portrait.rect.height * .735f));
                Title.rectTransform.anchorMin = new Vector2(0, 1);
                Title.rectTransform.anchorMax = Vector2.one;
                Title.rectTransform.offsetMin = new Vector2(20, -64);
                Title.rectTransform.offsetMax = new Vector2(-20, -12);
                scrollRect.anchorMin = Vector2.zero;
                scrollRect.anchorMax = Vector2.one;
                scrollRect.offsetMin = new Vector2(20, 76);
                scrollRect.offsetMax = new Vector2(-20, -72);
                var confirmRect = (RectTransform)ConfirmButton.transform;
                confirmRect.anchorMin = confirmRect.anchorMax = new Vector2(.5f, 0);
                confirmRect.sizeDelta = new Vector2(220, 52);
                confirmRect.anchoredPosition = new Vector2(0, 40);
            }
            else
            {
                if (unlock && offer.Options.Count == 0)
                {
                    Root.anchorMin = new Vector2(.025f, .28f);
                    Root.anchorMax = new Vector2(.975f, .65f);
                }
                Title.rectTransform.anchorMin = new Vector2(0, 1);
                Title.rectTransform.anchorMax = Vector2.one;
                Title.rectTransform.offsetMin = new Vector2(20, -64);
                Title.rectTransform.offsetMax = new Vector2(-20, -12);
                scrollRect.anchorMin = Vector2.zero;
                scrollRect.anchorMax = Vector2.one;
                scrollRect.offsetMin = new Vector2(20, 76);
                scrollRect.offsetMax = new Vector2(-20, unlock ? -110 : -72);
                if (unlock)
                {
                    Instruction.rectTransform.anchorMin = new Vector2(0, 1);
                    Instruction.rectTransform.anchorMax = Vector2.one;
                    Instruction.rectTransform.offsetMin = new Vector2(24, -106);
                    Instruction.rectTransform.offsetMax = new Vector2(-24, -64);
                }
                var refreshRect = (RectTransform)RefreshButton.transform;
                refreshRect.anchorMin = Vector2.zero;
                refreshRect.anchorMax = new Vector2(.5f, 0);
                refreshRect.offsetMin = new Vector2(20, 14);
                refreshRect.offsetMax = new Vector2(-6, 66);
                var confirmRect = (RectTransform)ConfirmButton.transform;
                confirmRect.anchorMin = new Vector2(.5f, 0);
                confirmRect.anchorMax = new Vector2(1, 0);
                confirmRect.offsetMin = new Vector2(6, 14);
                confirmRect.offsetMax = new Vector2(-20, 66);
                if (unlock)
                {
                    confirmRect.anchorMin = confirmRect.anchorMax = new Vector2(.5f, 0);
                    confirmRect.sizeDelta = new Vector2(220, 52);
                    confirmRect.anchoredPosition = new Vector2(0, 40);
                }
                if (offer != null)
                {
                    float availableHeight = Root.rect.height - (unlock ? 186 : 148) - layout.padding.vertical -
                        Mathf.Max(0, offer.Options.Count - 1) * layout.spacing;
                    float cardHeight = offer.Options.Count > 3 ? 180 :
                        Mathf.Clamp(availableHeight / Mathf.Max(1, offer.Options.Count), 160, 180);
                    for (int i = 0; i < offer.Options.Count; ++i)
                        cards[i].GetComponent<LayoutElement>().preferredHeight = cardHeight;
                    Scroll.vertical = cardHeight * offer.Options.Count > availableHeight + .5f;
                }
            }
        }

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
