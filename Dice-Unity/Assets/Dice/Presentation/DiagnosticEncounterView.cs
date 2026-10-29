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
        private EncounterSnapshot cachedSnapshot;
        private MechanicalCatalog cachedCatalog;
        private HandPreview cachedPreview;
        private IReadOnlyList<ModifierInspection> cachedInspections;
        private IReadOnlyList<string> cachedEvents;
        private int cachedEventCount = -1;
        private string cachedLastEvent;
        private int cachedMask = -1;
        private string cachedSavePath;
        private string savePathLabel = "Save file: unavailable";
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
            controls = encounterControls ?? throw new ArgumentNullException(nameof(encounterControls));
            cachedSnapshot = null; cachedCatalog = null; cachedEvents = null; cachedMask = -1;
            lastControlsSeed = controls.Seed;
            seedInput.SetTextWithoutNotify(SeedText(lastControlsSeed));
            if (bound) return;
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

        private void Update()
        {
            if (controls == null) return;
            ulong incoming = controls.Seed;
            if (incoming != lastControlsSeed && !seedInput.isFocused)
            {
                lastControlsSeed = incoming;
                seedInput.SetTextWithoutNotify(SeedText(incoming));
            }
            RefreshText();
            statusText.text = controls.StatusMessage ?? "";
            savePathText.text = savePathLabel;
            bool exists = cachedSnapshot != null;
            bool playing = exists && cachedSnapshot.Phase == EncounterPhase.Playing;
            saveButton.interactable = exists;
            abandonButton.interactable = exists && cachedSnapshot.Phase != EncounterPhase.Completed;
            rerollButton.interactable = exists && cachedSnapshot.CanReroll && controls.SelectedMask != 0;
            playButton.interactable = playing && cachedSnapshot.HandsRemaining > 0;
            selectAllButton.interactable = clearSelectionButton.interactable = exists && cachedSnapshot.CanReroll;
            for (int i = 0; i < dieButtons.Length; ++i)
            {
                dieButtons[i].interactable = exists && cachedSnapshot.CanReroll;
                dieTexts[i].text = exists ? dieLabels[i] : "DIE " + (i + 1);
                dieSelection[i].enabled = exists && (controls.SelectedMask & (1 << i)) != 0;
            }
            runText.text = exists ? stageTitle + "\n" + stageSummary + "\n" + runSummary + "\n" + runDetail
                : "Start a run or load a saved encounter.";
            selectionText.text = exists ? selectionLabel : "";
            previewText.text = cachedPreview == null ? "Available while the encounter is playing." :
                previewCategory + "\n" + previewScore + "\n" + previewBreakdown + "\n" + previewResources;
            rewardText.text = exists ? rewardTitle + "\n" + rewardDetail : "REWARDS";
            upgradesText.text = "HAND UPGRADES / CATEGORY VALUES\nCategory base excludes the dice sum and modifier contributions.\n" + string.Join("\n", upgradeRows);
            inventoryText.text = exists ? inventoryTitle : "OWNED MODIFIERS";
            eventText.text = logTitle + "\nNewest first. Scored hands retain their complete resolution breakdown.\n\n" + string.Join("\n\n", eventRows);
            refreshButton.gameObject.SetActive(exists && cachedSnapshot.PendingReward?.Kind == RewardKind.Modifier);
            refreshButton.interactable = exists && cachedSnapshot.Phase == EncounterPhase.ChoosingModifier
                && cachedSnapshot.RewardRefreshesAvailable > 0;
            refreshButton.GetComponentInChildren<Text>(true).text = refreshLabel;
            RenderCards(rewardCards, rewardViews, rewardsContent, true);
            RenderCards(inventoryCards, inventoryViews, inventoryContent, false);
        }

        private void RenderCards(List<CardText> cards, List<DiagnosticEncounterCard> views, RectTransform parent, bool rewards)
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
                view.Choose.interactable = rewards && cachedSnapshot != null &&
                    (cachedSnapshot.Phase == EncounterPhase.ChoosingModifier || cachedSnapshot.Phase == EncounterPhase.ChoosingHandUpgrade);
            }
        }
        private static string SeedText(ulong seed) => seed.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private void RefreshText()
        {
            if (cachedSavePath != controls.SavePath)
            {
                cachedSavePath = controls.SavePath;
                savePathLabel = "Save file: " + (cachedSavePath ?? "unavailable");
            }
            EncounterSnapshot snapshot = controls.Snapshot;
            MechanicalCatalog catalog = controls.Catalog;
            HandPreview preview = controls.Preview;
            IReadOnlyList<ModifierInspection> inspections = controls.ModifierInspections;
            bool stateChanged = !ReferenceEquals(snapshot, cachedSnapshot) || !ReferenceEquals(catalog, cachedCatalog) ||
                !ReferenceEquals(preview, cachedPreview) || !ReferenceEquals(inspections, cachedInspections);
            cachedSnapshot = snapshot;
            cachedCatalog = catalog;
            cachedPreview = preview;
            cachedInspections = inspections;
            if (stateChanged && snapshot != null && catalog != null) BuildStateText();
            if (snapshot != null && (stateChanged || cachedMask != controls.SelectedMask))
            {
                cachedMask = controls.SelectedMask;
                int selectedCount = 0;
                for (int index = 0; index < DiceValues.Count; ++index)
                {
                    bool selected = (cachedMask & (1 << index)) != 0;
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
                selectionLabel = "Select dice FOR REROLL. " + selectedCount + " selected | mask " + cachedMask +
                    " | rerolls spent on this hand " + snapshot.RerollsSpentOnHand;
            }
            IReadOnlyList<string> events = controls.EventLog;
            int count = events?.Count ?? 0;
            string latest = count > 0 ? events[count - 1] : null;
            if (!ReferenceEquals(events, cachedEvents) || count != cachedEventCount || latest != cachedLastEvent)
            {
                cachedEvents = events;
                cachedEventCount = count;
                cachedLastEvent = latest;
                eventRows.Clear();
                for (int index = count - 1; index >= Math.Max(0, count - VisibleEventLimit); --index)
                    eventRows.Add(events[index]);
                logTitle = "RECENT EVENTS / " + eventRows.Count + " SHOWN";
            }
        }

        private void BuildStateText()
        {
            EncounterSnapshot snapshot = cachedSnapshot;
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
            if (cachedPreview != null)
            {
                ScoreBreakdown score = cachedPreview.Score;
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
                    cachedPreview.RerollsRemaining + " rerolls (before any stage transition).";
            }
            BuildRewardText();
            upgradeRows.Clear();
            foreach (HandDefinition hand in cachedCatalog.Hands)
            {
                int level = snapshot.HandUpgrades[(int)hand.Category];
                upgradeRows.Add(DiagnosticText.Humanize(hand.Category.ToString()) + " | level " + level + " / " +
                    hand.MaximumUpgrades + " | base " + DiagnosticText.Number(hand.BaseScoreAt(level)) +
                    " | multiplier " + DiagnosticText.Number(hand.MultiplierAt(level)));
            }
            BuildInventoryText();
        }

        private void BuildRewardText()
        {
            RewardOffer offer = cachedSnapshot.PendingReward;
            rewardCards.Clear();
            rewardTitle = offer == null ? "REWARDS" : "CHOOSE " + DiagnosticText.Humanize(offer.Kind.ToString()).ToUpperInvariant();
            rewardDetail = offer == null ? "" : "For stage " + (offer.TargetStageIndex + 1) + " | offer " + offer.OfferId;
            refreshLabel = "Refresh modifiers - cost " + cachedSnapshot.RewardRefreshCost + " (available " +
                cachedSnapshot.RewardRefreshes + ")";
            if (offer == null) return;
            foreach (RewardOption option in offer.Options)
            {
                var card = new CardText { OfferId = offer.OfferId, OptionId = option.OptionId };
                if (offer.Kind == RewardKind.Modifier)
                {
                    ModifierDefinition modifier = cachedCatalog.GetModifier(option.DefinitionId.Value);
                    ModifierPresentationCatalog charms = controls.ModifierPresentation;
                    card.Title = charms.GetName(modifier);
                    card.Body = charms.GetConditionText(modifier) + "\n" + charms.GetEffectText(modifier);
                    card.Detail = charms.GetTimingText(modifier) + "\n" + charms.GetName(modifier) + " | option " + option.OptionId;
                }
                else if (option.Category.HasValue)
                {
                    HandDefinition hand = cachedCatalog.GetHand(option.Category.Value);
                    int level = cachedSnapshot.HandUpgrades[(int)hand.Category];
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

        private void BuildInventoryText()
        {
            inventoryCards.Clear();
            inventoryTitle = "OWNED MODIFIERS / " + cachedSnapshot.Modifiers.Count + " INSTANCES";
            foreach (ModifierInstanceSnapshot instance in cachedSnapshot.Modifiers)
            {
                ModifierDefinition definition = cachedCatalog.GetModifier(instance.DefinitionId);
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
                if (cachedInspections != null)
                    foreach (ModifierInspection inspection in cachedInspections)
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
                card.Contributions = "This hand so far: " + DescribeContributions(cachedSnapshot.ModifierContributions, instance.InstanceId);
                if (cachedPreview != null && definition.Phase == ModifierPhase.PlayHand)
                    card.Contributions += "\nIf played now, hand total: " +
                        DescribeContributions(cachedPreview.Score.ModifierContributions, instance.InstanceId);
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
