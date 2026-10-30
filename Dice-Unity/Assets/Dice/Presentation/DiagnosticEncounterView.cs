using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;
namespace Dice.Presentation
{
    /** Authored diagnostic controls display mechanical state and score resolution. */
    public sealed class DiagnosticEncounterView : MonoBehaviour
    {
        private const int VisibleEventLimit = 60;
        private readonly List<CardText> rewardCards = new List<CardText>();
        private readonly List<CardText> inventoryCards = new List<CardText>();
        private readonly List<string> upgradeRows = new List<string>();
        private readonly List<string> eventRows = new List<string>();
        private readonly string[] dieLabels = new string[DiceValues.Count];
        private readonly List<string> dieStates = new List<string>();
        private IDiagnosticEncounterControls controls;
        private ulong lastControlsSeed;
        private string stageTitle;
        private string stageSummary;
        private string runSummary;
        private string runDetail;
        private string previewCategory;
        private string previewScore;
        private string previewBreakdown;
        private string previewResources;
        private string rewardTitle;
        private string rewardDetail;
        private string refreshLabel;
        private string selectionLabel;
        private string inventoryTitle;
        private string logTitle;

        private sealed class CardText
        {
            public string Title;
            public string Body;
            public string Detail;
            public string Status;
            public string Contributions;
            public long OfferId;
            public long OptionId;
            public bool Active;
        }

        [SerializeField] private InputField seedInput;
        [SerializeField] private Button startButton, saveButton, loadButton, abandonButton;
        [SerializeField] private Button selectAllButton, clearSelectionButton, rerollButton, playButton, refreshButton;
        [SerializeField] private Button[] dieButtons;
        [SerializeField] private Text[] dieTexts;
        [SerializeField] private Image[] dieSelection;
        [SerializeField] private Text statusText, savePathText, runText, selectionText, previewText;
        [SerializeField] private Text rewardText, upgradesText, inventoryText, eventText;
        [SerializeField] private RectTransform rewardsContent, inventoryContent;
        [SerializeField] private DiagnosticEncounterCard cardPrefab;
        private readonly List<DiagnosticEncounterCard> rewardViews = new List<DiagnosticEncounterCard>();
        private readonly List<DiagnosticEncounterCard> inventoryViews = new List<DiagnosticEncounterCard>();
        private bool bound;

        public void Bind(IDiagnosticEncounterControls encounterControls)
        {
            var next = encounterControls ?? throw new ArgumentNullException(nameof(encounterControls));
            if (controls != null) controls.Changed -= Render;
            controls = next;
            controls.Changed += Render;
            lastControlsSeed = controls.Seed;
            seedInput.SetTextWithoutNotify(SeedText(lastControlsSeed));
            if (!bound) BindButtons();
            Render();
        }

        private void BindButtons()
        {
            startButton.onClick.AddListener(() => controls.StartNew(seedInput.text));
            saveButton.onClick.AddListener(() => controls.Save());
            loadButton.onClick.AddListener(() => controls.Load());
            abandonButton.onClick.AddListener(() => controls.Abandon());
            selectAllButton.onClick.AddListener(() => controls.SelectAllDice());
            clearSelectionButton.onClick.AddListener(() => controls.ClearDiceSelection());
            rerollButton.onClick.AddListener(() => controls.Reroll());
            playButton.onClick.AddListener(() => controls.PlayHand());
            refreshButton.onClick.AddListener(() => controls.RefreshRewards());
            for (int i = 0; i < dieButtons.Length; ++i)
            {
                int index = i;
                dieButtons[i].onClick.AddListener(() => controls.ToggleDie(index));
            }
            bound = true;
        }

        private void OnEnable() => Render();

        private void OnDestroy()
        {
            if (controls != null) controls.Changed -= Render;
        }

        /** Shows the controls' state; a disabled view shows it when it is enabled. */
        private void Render()
        {
            if (controls == null || !isActiveAndEnabled) return;
            ulong incoming = controls.Seed;
            // A seed being typed is kept; the field takes the controls' seed at the first render after it loses focus.
            if (incoming != lastControlsSeed && !seedInput.isFocused)
            {
                lastControlsSeed = incoming;
                seedInput.SetTextWithoutNotify(SeedText(incoming));
            }
            EncounterSnapshot snapshot = controls.Snapshot;
            HandPreview preview = controls.Preview;
            BuildText(snapshot, controls.Catalog, preview, controls.ModifierInspections);
            statusText.text = controls.StatusMessage ?? "";
            savePathText.text = "Save file: " + (controls.SavePath ?? "unavailable");
            bool exists = snapshot != null;
            bool playing = exists && snapshot.Phase == EncounterPhase.Playing;
            saveButton.interactable = exists;
            abandonButton.interactable = exists && snapshot.Phase != EncounterPhase.Completed;
            rerollButton.interactable = exists && snapshot.CanReroll && controls.SelectedMask != 0;
            playButton.interactable = playing && snapshot.HandsRemaining > 0;
            selectAllButton.interactable = clearSelectionButton.interactable = exists && snapshot.CanReroll;
            for (int i = 0; i < dieButtons.Length; ++i)
            {
                dieButtons[i].interactable = exists && snapshot.CanReroll;
                dieTexts[i].text = exists ? dieLabels[i] : "DIE " + (i + 1);
                dieSelection[i].enabled = exists && (controls.SelectedMask & (1 << i)) != 0;
            }
            runText.text = exists ? stageTitle + "\n" + stageSummary + "\n" + runSummary + "\n" + runDetail
                : "Start a run or load a saved encounter.";
            selectionText.text = exists ? selectionLabel : "";
            previewText.text = preview == null ? "Available while the encounter is playing." :
                previewCategory + "\n" + previewScore + "\n" + previewBreakdown + "\n" + previewResources;
            rewardText.text = exists ? rewardTitle + "\n" + rewardDetail : "REWARDS";
            upgradesText.text = "HAND UPGRADES / CATEGORY VALUES\nCategory base excludes the dice sum and modifier contributions.\n" + string.Join("\n", upgradeRows);
            inventoryText.text = exists ? inventoryTitle : "OWNED MODIFIERS";
            eventText.text = logTitle + "\nNewest first. Scored hands retain their complete resolution breakdown.\n\n" + string.Join("\n\n", eventRows);
            bool refreshable = exists && snapshot.PendingReward != null && snapshot.PendingReward.Kind.Refreshable;
            refreshButton.gameObject.SetActive(refreshable);
            refreshButton.interactable = refreshable && snapshot.RewardRefreshesAvailable > 0;
            refreshButton.GetComponentInChildren<Text>(true).text = refreshLabel;
            RenderCards(rewardCards, rewardViews, rewardsContent, true, snapshot);
            RenderCards(inventoryCards, inventoryViews, inventoryContent, false, snapshot);
        }

        private void RenderCards(List<CardText> cards, List<DiagnosticEncounterCard> views, RectTransform parent, bool rewards,
            EncounterSnapshot snapshot)
        {
            while (views.Count < cards.Count)
            {
                DiagnosticEncounterCard view = Instantiate(cardPrefab, parent, false);
                int index = views.Count;
                if (rewards) view.Choose.onClick.AddListener(() => controls.ChooseReward(rewardCards[index].OfferId, rewardCards[index].OptionId));
                views.Add(view);
            }
            for (int i = 0; i < views.Count; ++i)
            {
                var view = views[i];
                view.gameObject.SetActive(i < cards.Count);
                if (i >= cards.Count) continue;
                CardText card = cards[i];
                view.Title.text = card.Title;
                view.Body.text = string.Join("\n", new[] { card.Status, card.Body, card.Detail, card.Contributions });
                view.Choose.gameObject.SetActive(rewards);
                view.Choose.interactable = rewards && snapshot?.PendingReward != null;
            }
        }
        private static string SeedText(ulong seed) => seed.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private void BuildText(EncounterSnapshot snapshot, MechanicalCatalog catalog, HandPreview preview,
            IReadOnlyList<ModifierInspection> inspections)
        {
            if (snapshot != null && catalog != null) BuildStateText(snapshot, catalog, preview, inspections);
            if (snapshot != null)
            {
                int mask = controls.SelectedMask;
                int selectedCount = 0;
                for (int index = 0; index < DiceValues.Count; ++index)
                {
                    bool selected = (mask & (1 << index)) != 0;
                    if (selected) ++selectedCount;
                    // A locked die cannot be rerolled, so only the rules' labels describe it.
                    dieStates.Clear();
                    if ((snapshot.LockedMask & (1 << index)) == 0) dieStates.Add(selected ? "REROLL" : "KEEP");
                    foreach (EncounterRuleSnapshot rule in snapshot.EncounterRules)
                    {
                        string label = rule.DiagnosticDieLabel(index);
                        if (!string.IsNullOrEmpty(label)) dieStates.Add(label);
                    }
                    dieLabels[index] = "DIE " + (index + 1) + "\n" + snapshot.CurrentDice[index] + "\n" + string.Join(" / ", dieStates);
                }
                selectionLabel = "Select dice FOR REROLL. " + selectedCount + " selected | mask " + mask +
                    " | rerolls spent on this hand " + snapshot.RerollsSpentOnHand;
            }
            IReadOnlyList<string> events = controls.EventLog;
            int count = events?.Count ?? 0;
            eventRows.Clear();
            for (int index = count - 1; index >= Math.Max(0, count - VisibleEventLimit); --index)
                eventRows.Add(events[index]);
            logTitle = "RECENT EVENTS / " + eventRows.Count + " SHOWN";
        }

        private void BuildStateText(EncounterSnapshot snapshot, MechanicalCatalog catalog, HandPreview preview,
            IReadOnlyList<ModifierInspection> inspections)
        {
            stageTitle = "RUN / STAGE " + (snapshot.StageIndex + 1) + " OF " + snapshot.StageCount +
                " / " + DiagnosticText.Humanize(snapshot.Phase.ToString()).ToUpperInvariant();
            if (snapshot.Phase == EncounterPhase.Completed) stageTitle += " / " + snapshot.Outcome.ToString().ToUpperInvariant();
            stageSummary = DiagnosticText.Number(snapshot.RemainingThreshold) + " remaining / " +
                DiagnosticText.Number(snapshot.StartingThreshold) + " target";
            runSummary = "Hands " + snapshot.HandsRemaining + " / " + snapshot.StartingHands +
                "    Rerolls " + snapshot.RerollsRemaining + " / " + snapshot.StartingRerolls +
                "    Refreshes " + snapshot.RewardRefreshes + "\nStage score " +
                DiagnosticText.Number(snapshot.StartingThreshold - snapshot.RemainingThreshold) +
                "    Run score " + DiagnosticText.Number(snapshot.TotalScore);
            runDetail = "Hand " + (snapshot.HandIndex + 1) + " | stage hands played " + snapshot.HandsPlayedInStage +
                " | run hands " + snapshot.TotalHandsPlayed + " | run rerolls " + snapshot.TotalRerollsSpent +
                "\n" + snapshot.EncounterId + " | catalog " + snapshot.CatalogVersion + " | revision " + snapshot.Revision;
            if (preview != null)
            {
                ScoreBreakdown score = preview.Score;
                previewCategory = DiagnosticText.Humanize(score.Category.ToString()) + " | upgrade level " + score.UpgradeCount;
                previewScore = DiagnosticText.Number(score.BaseScore) + " x " + DiagnosticText.Number(score.Multiplier) +
                    " = " + DiagnosticText.Number(score.HandScore);
                previewBreakdown = "Base score\n  Dice sum " + DiagnosticText.Number(score.DiceSum) +
                    " + category " + DiagnosticText.Number(score.CategoryBaseScore) +
                    "\n  Upgrades " + DiagnosticText.Number(score.UpgradeBaseScore) +
                    " + modifiers " + DiagnosticText.Number(score.ModifierBaseScore) +
                    "\nMultiplier\n  Category " + DiagnosticText.Number(score.CategoryMultiplier) +
                    " + upgrades " + DiagnosticText.Number(score.UpgradeMultiplier) +
                    " + modifiers " + DiagnosticText.Number(score.ModifierMultiplier);
                previewResources = "After playing: " + (snapshot.HandsRemaining - 1) + " hands, " +
                    preview.RerollsRemaining + " rerolls (before any stage transition).";
            }
            BuildRewardText(snapshot, catalog);
            upgradeRows.Clear();
            foreach (HandDefinition hand in catalog.Hands)
            {
                int level = snapshot.HandUpgrades[(int)hand.Category];
                upgradeRows.Add(DiagnosticText.Humanize(hand.Category.ToString()) + " | level " + level + " / " +
                    hand.MaximumUpgrades + " | base " + DiagnosticText.Number(hand.BaseScoreAt(level)) +
                    " | multiplier " + DiagnosticText.Number(hand.MultiplierAt(level)));
            }
            BuildInventoryText(snapshot, catalog, preview, inspections);
        }

        private void BuildRewardText(EncounterSnapshot snapshot, MechanicalCatalog catalog)
        {
            RewardOffer offer = snapshot.PendingReward;
            rewardCards.Clear();
            rewardTitle = offer == null ? "REWARDS" : "CHOOSE " + DiagnosticText.Humanize(offer.Kind.Id).ToUpperInvariant();
            rewardDetail = offer == null ? "" : "For stage " + (offer.TargetStageIndex + 1) + " | offer " + offer.OfferId;
            refreshLabel = "Refresh modifiers - cost " + snapshot.RewardRefreshCost + " (available " +
                snapshot.RewardRefreshes + ")";
            if (offer == null) return;
            foreach (RewardOption option in offer.Options)
            {
                var card = new CardText { OfferId = offer.OfferId, OptionId = option.OptionId };
                if (option.DefinitionId.HasValue)
                {
                    ModifierDefinition modifier = catalog.GetModifier(option.DefinitionId.Value);
                    ModifierPresentationCatalog charms = controls.ModifierPresentation;
                    card.Title = charms.GetName(modifier);
                    card.Body = charms.GetConditionText(modifier) + "\n" + charms.GetEffectText(modifier);
                    card.Detail = charms.GetTimingText(modifier) + "\n" + charms.GetName(modifier) + " | option " + option.OptionId;
                }
                else
                {
                    HandDefinition hand = catalog.GetHand(option.Category.Value);
                    int level = snapshot.HandUpgrades[(int)hand.Category];
                    card.Title = DiagnosticText.Humanize(hand.Category.ToString()) + " | level " + level + " -> " + (level + 1);
                    card.Body = "Category base " + DiagnosticText.Number(hand.BaseScoreAt(level)) +
                        " -> " + DiagnosticText.Number(hand.BaseScoreAt(level + 1)) +
                        "\nMultiplier " + DiagnosticText.Number(hand.MultiplierAt(level)) +
                        " -> " + DiagnosticText.Number(hand.MultiplierAt(level + 1));
                    card.Detail = hand.Category + " | option " + option.OptionId;
                }
                rewardCards.Add(card);
            }
        }

        private void BuildInventoryText(EncounterSnapshot snapshot, MechanicalCatalog catalog, HandPreview preview,
            IReadOnlyList<ModifierInspection> inspections)
        {
            inventoryCards.Clear();
            inventoryTitle = "OWNED MODIFIERS / " + snapshot.Modifiers.Count + " INSTANCES";
            foreach (ModifierInstanceSnapshot instance in snapshot.Modifiers)
            {
                ModifierDefinition definition = catalog.GetModifier(instance.DefinitionId);
                ModifierPresentationCatalog charms = controls.ModifierPresentation;
                var card = new CardText
                {
                    Title = charms.GetName(definition) + " #" + instance.InstanceId,
                    Body = charms.GetConditionText(definition) + "\n" + charms.GetEffectText(definition),
                    Detail = charms.GetTimingText(definition) +
                        "\nActivations: hand " + instance.ActivationsThisHand + ", stage " + instance.ActivationsThisStage +
                        ", total " + instance.TotalActivations + "\n" + charms.GetName(definition),
                    Status = definition.Phase == ModifierPhase.PlayHand ? "If played now: inspection unavailable" : "Evaluated on roll"
                };
                if (inspections != null)
                    foreach (ModifierInspection inspection in inspections)
                    {
                        if (inspection.InstanceId != instance.InstanceId) continue;
                        if (definition.Phase == ModifierPhase.PlayHand)
                        {
                            card.Active = inspection.Status == ModifierInspectionStatus.WouldActivate;
                            card.Status = "If played now: " + DiagnosticText.DescribeInspectionStatus(inspection.Status);
                            if (inspection.ConditionMet.HasValue)
                                card.Status += " | condition " + (inspection.ConditionMet.Value ? "met" : "not met");
                            if (inspection.ActivationLimitReached && inspection.Status != ModifierInspectionStatus.ActivationLimitReached)
                                card.Status += " | activation limit reached";
                        }
                        break;
                    }
                card.Contributions = "This hand so far: " + DescribeContributions(snapshot.ModifierContributions, instance.InstanceId);
                if (preview != null && definition.Phase == ModifierPhase.PlayHand)
                    card.Contributions += "\nIf played now, hand total: " +
                        DescribeContributions(preview.Score.ModifierContributions, instance.InstanceId);
                inventoryCards.Add(card);
            }
        }

        /** The summed base, multiplier and rerolls of the instance's contributions among the contributions. */
        private static string DescribeContributions(IEnumerable<ModifierContribution> contributions, long instanceId)
        {
            long baseScore = 0, multiplier = 0;
            int rerolls = 0;
            foreach (ModifierContribution contribution in contributions)
            {
                if (contribution.InstanceId != instanceId) continue;
                baseScore += contribution.BaseScore;
                multiplier += contribution.Multiplier;
                rerolls += contribution.Rerolls;
            }
            return "+" + DiagnosticText.Number(baseScore) + " base, +" + DiagnosticText.Number(multiplier) + " multiplier, +" +
                rerolls + " rerolls.";
        }

    }
}
