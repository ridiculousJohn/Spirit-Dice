using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /** Runtime inspection surface; encounter commands and persistence remain with the bound controls. */
    [DisallowMultipleComponent]
    public sealed class DiagnosticEncounterView : MonoBehaviour
    {
        private const int VisibleEventLimit = 60;
        private const string SeedControlName = "DiagnosticNewRunSeed";
        private static readonly GUILayoutOption[] Expand = { GUILayout.ExpandWidth(true) };
        private static readonly GUILayoutOption[] ButtonHeight = { GUILayout.Height(38), GUILayout.ExpandWidth(true) };
        private static readonly GUILayoutOption[] DieHeight = { GUILayout.Height(116), GUILayout.ExpandWidth(true) };
        private static readonly GUILayoutOption[] ToolbarButton = { GUILayout.Height(38), GUILayout.Width(108) };
        private static readonly GUILayoutOption[] SeedWidth = { GUILayout.Height(38), GUILayout.MinWidth(100), GUILayout.MaxWidth(260) };
        private readonly List<Texture2D> ownedTextures = new List<Texture2D>();
        private readonly List<CardText> rewardCards = new List<CardText>();
        private readonly List<CardText> inventoryCards = new List<CardText>();
        private readonly List<string> upgradeRows = new List<string>();
        private readonly List<string> eventRows = new List<string>();
        private readonly string[] dieLabels = new string[DiceValues.Count];
        private IDiagnosticEncounterControls controls;
        private EncounterSnapshot cachedSnapshot;
        private MechanicalCatalog cachedCatalog;
        private HandPreview cachedPreview;
        private IReadOnlyList<ModifierInspection> cachedInspections;
        private IReadOnlyList<string> cachedEvents;
        private int cachedEventCount = -1;
        private string cachedLastEvent;
        private int cachedMask = -1;
        private float cachedColumnWidth;
        private GUILayoutOption[] columnWidth;
        private string cachedSavePath;
        private string savePathLabel = "Save file: unavailable";
        private string seedText = "1";
        private string lastControlsSeed;
        private Vector2 scroll;
        private GUISkin skin;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle textStyle;
        private GUIStyle smallStyle;
        private GUIStyle accentStyle;
        private GUIStyle amberStyle;
        private GUIStyle scoreStyle;
        private GUIStyle cardStyle;
        private GUIStyle dieStyle;
        private GUIStyle selectedDieStyle;
        private Texture2D background;
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
        private int refreshCost;

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

        public void Bind(IDiagnosticEncounterControls encounterControls)
        {
            controls = encounterControls ?? throw new ArgumentNullException(nameof(encounterControls));
            cachedSnapshot = null;
            cachedCatalog = null;
            cachedEvents = null;
            cachedMask = -1;
            lastControlsSeed = controls.SeedText;
            seedText = lastControlsSeed ?? "1";
            scroll = Vector2.zero;
        }

        private void OnGUI()
        {
            EnsureStyles();
            GUISkin previousSkin = GUI.skin;
            bool previousEnabled = GUI.enabled;
            Color previousColor = GUI.color;
            GUI.skin = skin;
            GUI.color = Color.white;
            try
            {
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), background);
                float width = Mathf.Min(1560, Mathf.Max(1, Screen.width - 40));
                GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, 16, width, Mathf.Max(1, Screen.height - 32)));
                if (controls == null)
                {
                    GUILayout.Label("DICE / MECHANICAL DIAGNOSTIC", titleStyle);
                    GUILayout.Label("Waiting for encounter controls.", textStyle);
                }
                else
                {
                    SynchronizeSeedText();
                    RefreshText();
                    GUILayout.Label("DICE / MECHANICAL DIAGNOSTIC", titleStyle);
                    GUILayout.Label("Deterministic encounter, score resolution, and modifier inspection", smallStyle);
                    DrawToolbar(width);
                    GUILayout.Label(controls.StatusMessage ?? "", amberStyle);
                    scroll = GUILayout.BeginScrollView(scroll, false, false, Expand);
                    if (cachedSnapshot == null)
                        GUILayout.Label("Start a run or load a saved encounter.", textStyle);
                    else if (width >= 1040)
                    {
                        float nextColumnWidth = (width - 40) * 0.5f;
                        if (columnWidth == null || cachedColumnWidth != nextColumnWidth)
                        {
                            cachedColumnWidth = nextColumnWidth;
                            columnWidth = new[] { GUILayout.Width(nextColumnWidth) };
                        }
                        GUILayout.BeginHorizontal();
                        GUILayout.BeginVertical(columnWidth);
                        DrawRun();
                        DrawDice(cachedColumnWidth);
                        DrawPreview();
                        DrawRewards();
                        DrawUpgrades();
                        GUILayout.EndVertical();
                        GUILayout.Space(18);
                        GUILayout.BeginVertical(columnWidth);
                        DrawInventory();
                        DrawEventLog();
                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        DrawRun();
                        DrawDice(width);
                        DrawPreview();
                        DrawRewards();
                        DrawUpgrades();
                        DrawInventory();
                        DrawEventLog();
                    }
                    GUILayout.EndScrollView();
                }
                GUILayout.EndArea();
            }
            finally
            {
                GUI.skin = previousSkin;
                GUI.enabled = previousEnabled;
                GUI.color = previousColor;
            }
        }

        private void SynchronizeSeedText()
        {
            string incoming = controls.SeedText ?? "1";
            if (incoming == lastControlsSeed || GUI.GetNameOfFocusedControl() == SeedControlName) return;
            lastControlsSeed = incoming;
            seedText = incoming;
        }

        private void DrawToolbar(float width)
        {
            GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            GUILayout.Label("New run seed", textStyle);
            GUI.SetNextControlName(SeedControlName);
            seedText = GUILayout.TextField(seedText, 32, SeedWidth);
            if (GUILayout.Button("Start new", ToolbarButton))
            {
                GUI.FocusControl(null);
                controls.StartNew(seedText);
                GUIUtility.ExitGUI();
            }
            if (width >= 680) DrawPersistenceButtons();
            GUILayout.EndHorizontal();
            if (width < 680)
            {
                GUILayout.BeginHorizontal();
                DrawPersistenceButtons();
                GUILayout.EndHorizontal();
            }
            GUILayout.Label(savePathLabel, smallStyle);
        }

        private void DrawPersistenceButtons()
        {
            GUI.enabled = cachedSnapshot != null;
            if (GUILayout.Button("Save", ToolbarButton)) { controls.Save(); GUIUtility.ExitGUI(); }
            GUI.enabled = true;
            if (GUILayout.Button("Load", ToolbarButton)) { controls.Load(); GUIUtility.ExitGUI(); }
            GUI.enabled = cachedSnapshot != null && cachedSnapshot.Phase != EncounterPhase.Completed;
            if (GUILayout.Button("Abandon", ToolbarButton)) { controls.Abandon(); GUIUtility.ExitGUI(); }
            GUI.enabled = true;
        }

        private void DrawRun()
        {
            BeginCard(stageTitle);
            GUILayout.Label(stageSummary, scoreStyle);
            GUILayout.Label(runSummary, textStyle);
            GUILayout.Label(runDetail, smallStyle);
            EndCard();
        }

        private void DrawDice(float width)
        {
            BeginCard("CURRENT HAND");
            GUILayout.Label(selectionLabel, smallStyle);
            bool playing = cachedSnapshot.Phase == EncounterPhase.Playing;
            GUI.enabled = playing && cachedSnapshot.RerollsRemaining > 0;
            int dicePerRow = Mathf.Clamp(Mathf.FloorToInt((width - 32) / 98), 1, DiceValues.Count);
            for (int first = 0; first < DiceValues.Count; first += dicePerRow)
            {
                GUILayout.BeginHorizontal();
                for (int index = first; index < Math.Min(first + dicePerRow, DiceValues.Count); ++index)
                {
                    bool selected = (controls.SelectedMask & (1 << index)) != 0;
                    if (!GUILayout.Button(dieLabels[index], selected ? selectedDieStyle : dieStyle, DieHeight)) continue;
                    controls.ToggleDie(index);
                    GUIUtility.ExitGUI();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Select all", ButtonHeight)) { controls.SelectAllDice(); GUIUtility.ExitGUI(); }
            if (GUILayout.Button("Clear selection", ButtonHeight)) { controls.ClearDiceSelection(); GUIUtility.ExitGUI(); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUI.enabled = playing && cachedSnapshot.RerollsRemaining > 0 && controls.SelectedMask != 0;
            if (GUILayout.Button("Reroll selected", ButtonHeight)) { controls.Reroll(); GUIUtility.ExitGUI(); }
            GUI.enabled = playing && cachedSnapshot.HandsRemaining > 0;
            if (GUILayout.Button("Play hand", ButtonHeight)) { controls.PlayHand(); GUIUtility.ExitGUI(); }
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            EndCard();
        }

        private void DrawPreview()
        {
            BeginCard("IF PLAYED NOW / SCORE PREVIEW");
            if (cachedPreview == null)
                GUILayout.Label("Available while the encounter is playing.", smallStyle);
            else
            {
                GUILayout.Label(previewCategory, accentStyle);
                GUILayout.Label(previewScore, scoreStyle);
                GUILayout.Label(previewBreakdown, textStyle);
                GUILayout.Label(previewResources, smallStyle);
                GUILayout.Label("Play-hand conditions use the context after spending one hand. Modifier inspection below uses that same context.", smallStyle);
            }
            EndCard();
        }

        private void DrawRewards()
        {
            BeginCard(rewardTitle);
            GUILayout.Label(rewardDetail, smallStyle);
            if (cachedSnapshot.PendingReward == null)
                GUILayout.Label(cachedSnapshot.Phase == EncounterPhase.Completed
                    ? "Run finished. Start a new run to continue." : "Choices appear after a stage clear.", textStyle);
            else
            {
                foreach (CardText card in rewardCards)
                {
                    GUILayout.BeginVertical(cardStyle, Expand);
                    GUILayout.Label(card.Title, accentStyle);
                    GUILayout.Label(card.Body, textStyle);
                    GUILayout.Label(card.Detail, smallStyle);
                    GUI.enabled = cachedSnapshot.Phase == EncounterPhase.ChoosingModifier ||
                        cachedSnapshot.Phase == EncounterPhase.ChoosingHandUpgrade;
                    if (GUILayout.Button("Choose", ButtonHeight))
                    {
                        controls.ChooseReward(card.OfferId, card.OptionId);
                        GUIUtility.ExitGUI();
                    }
                    GUI.enabled = true;
                    GUILayout.EndVertical();
                    GUILayout.Space(6);
                }
                if (cachedSnapshot.PendingReward.Kind == RewardKind.Modifier)
                {
                    GUI.enabled = cachedSnapshot.Phase == EncounterPhase.ChoosingModifier && refreshCost > 0 &&
                        cachedSnapshot.RewardRefreshes >= refreshCost;
                    if (GUILayout.Button(refreshLabel, ButtonHeight)) { controls.RefreshRewards(); GUIUtility.ExitGUI(); }
                    GUI.enabled = true;
                }
            }
            EndCard();
        }

        private void DrawUpgrades()
        {
            BeginCard("HAND UPGRADES / CATEGORY VALUES");
            GUILayout.Label("Category base excludes the dice sum and modifier contributions.", smallStyle);
            foreach (string row in upgradeRows) GUILayout.Label(row, textStyle);
            EndCard();
        }

        private void DrawInventory()
        {
            BeginCard(inventoryTitle);
            GUILayout.Label("If played now: Play Hand modifiers. Roll modifiers are evaluated on their roll; their current-hand contributions are shown separately.", smallStyle);
            if (inventoryCards.Count == 0) GUILayout.Label("No owned modifiers yet.", textStyle);
            foreach (CardText card in inventoryCards)
            {
                GUILayout.BeginVertical(cardStyle, Expand);
                GUILayout.Label(card.Title, accentStyle);
                GUILayout.Label(card.Status, card.Active ? accentStyle : amberStyle);
                GUILayout.Label(card.Body, textStyle);
                GUILayout.Label(card.Detail, smallStyle);
                GUILayout.Label(card.Contributions, smallStyle);
                GUILayout.EndVertical();
                GUILayout.Space(8);
            }
            EndCard();
        }

        private void DrawEventLog()
        {
            BeginCard(logTitle);
            GUILayout.Label("Newest first. Scored hands retain their complete resolution breakdown.", smallStyle);
            if (eventRows.Count == 0) GUILayout.Label("No events recorded.", textStyle);
            foreach (string row in eventRows)
            {
                GUILayout.Label(row, textStyle);
                GUILayout.Space(9);
            }
            EndCard();
        }

        private void BeginCard(string heading)
        {
            GUILayout.BeginVertical(cardStyle, Expand);
            GUILayout.Label(heading ?? "", headingStyle);
            GUILayout.Space(6);
        }

        private static void EndCard()
        {
            GUILayout.EndVertical();
            GUILayout.Space(14);
        }

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
                    dieLabels[index] = "DIE " + (index + 1) + "\n" + snapshot.CurrentDice[index] + "\n" +
                        (selected ? "REROLL" : "KEEP");
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
                    hand.MaximumUpgrades + " | base " + DiagnosticText.Number(hand.BaseScore + hand.BaseScorePerUpgrade * level) +
                    " | multiplier " + DiagnosticText.Number(hand.Multiplier + hand.MultiplierPerUpgrade * level));
            }
            BuildInventoryText();
        }

        private void BuildRewardText()
        {
            RewardOffer offer = cachedSnapshot.PendingReward;
            rewardCards.Clear();
            rewardTitle = offer == null ? "REWARDS" : "CHOOSE " + DiagnosticText.Humanize(offer.Kind.ToString()).ToUpperInvariant();
            rewardDetail = offer == null ? "" : "For stage " + (offer.TargetStageIndex + 1) + " | offer " + offer.OfferId;
            RewardPolicyDefinition policy = cachedCatalog.GetEncounter(cachedSnapshot.EncounterId).RewardPolicy;
            refreshCost = policy?.RefreshCost ?? 0;
            refreshLabel = "Refresh modifiers - cost " + refreshCost + " (available " + cachedSnapshot.RewardRefreshes + ")";
            if (offer == null) return;
            foreach (RewardOption option in offer.Options)
            {
                var card = new CardText { OfferId = offer.OfferId, OptionId = option.OptionId };
                if (offer.Kind == RewardKind.Modifier)
                {
                    ModifierDefinition modifier = cachedCatalog.GetModifier(option.DefinitionId);
                    card.Title = DiagnosticText.Humanize(modifier.Id);
                    card.Body = DiagnosticText.DescribeCondition(modifier.Condition) + "\n" + DiagnosticText.DescribeEffects(modifier);
                    card.Detail = DiagnosticText.Humanize(modifier.Phase.ToString()) + " | " +
                        DiagnosticText.Humanize(modifier.ActivationLimit.ToString()) + "\n" + modifier.Id + " | option " + option.OptionId;
                }
                else if (option.Category.HasValue)
                {
                    HandDefinition hand = cachedCatalog.GetHand(option.Category.Value);
                    int level = cachedSnapshot.HandUpgrades[(int)hand.Category];
                    card.Title = DiagnosticText.Humanize(hand.Category.ToString()) + " | level " + level + " -> " + (level + 1);
                    card.Body = "Category base " + DiagnosticText.Number(hand.BaseScore + hand.BaseScorePerUpgrade * level) +
                        " -> " + DiagnosticText.Number(hand.BaseScore + hand.BaseScorePerUpgrade * (level + 1)) +
                        "\nMultiplier " + DiagnosticText.Number(hand.Multiplier + hand.MultiplierPerUpgrade * level) +
                        " -> " + DiagnosticText.Number(hand.Multiplier + hand.MultiplierPerUpgrade * (level + 1));
                    card.Detail = hand.Id + " | option " + option.OptionId;
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
                var card = new CardText
                {
                    Title = DiagnosticText.Humanize(definition.Id) + " #" + instance.InstanceId,
                    Body = DiagnosticText.DescribeCondition(definition.Condition) + "\n" + DiagnosticText.DescribeEffects(definition),
                    Detail = DiagnosticText.Humanize(definition.Phase.ToString()) + " | " +
                        DiagnosticText.Humanize(definition.ActivationLimit.ToString()) +
                        "\nActivations: hand " + instance.ActivationsThisHand + ", stage " + instance.ActivationsThisStage +
                        ", total " + instance.TotalActivations + "\n" + definition.Id,
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
                long currentBase = 0, currentMultiplier = 0;
                int currentRerolls = 0;
                foreach (ModifierContribution contribution in cachedSnapshot.ModifierContributions)
                {
                    if (contribution.InstanceId != instance.InstanceId) continue;
                    currentBase += contribution.BaseScore;
                    currentMultiplier += contribution.Multiplier;
                    currentRerolls += contribution.Rerolls;
                }
                card.Contributions = "This hand so far: +" + DiagnosticText.Number(currentBase) + " base, +" +
                    DiagnosticText.Number(currentMultiplier) + " multiplier, +" + currentRerolls + " rerolls.";
                if (cachedPreview != null && definition.Phase == ModifierPhase.PlayHand)
                {
                    long projectedBase = 0, projectedMultiplier = 0;
                    int projectedRerolls = 0;
                    foreach (ModifierContribution contribution in cachedPreview.Score.ModifierContributions)
                    {
                        if (contribution.InstanceId != instance.InstanceId) continue;
                        projectedBase += contribution.BaseScore;
                        projectedMultiplier += contribution.Multiplier;
                        projectedRerolls += contribution.Rerolls;
                    }
                    card.Contributions += "\nIf played now, hand total: +" + DiagnosticText.Number(projectedBase) +
                        " base, +" + DiagnosticText.Number(projectedMultiplier) + " multiplier, +" + projectedRerolls + " rerolls.";
                }
                inventoryCards.Add(card);
            }
        }

        private void EnsureStyles()
        {
            if (skin != null) return;
            skin = Instantiate(GUI.skin);
            background = MakeTexture(new Color(0.055f, 0.068f, 0.086f));
            Texture2D panel = MakeTexture(new Color(0.092f, 0.112f, 0.139f));
            Texture2D button = MakeTexture(new Color(0.145f, 0.180f, 0.220f));
            Texture2D hover = MakeTexture(new Color(0.210f, 0.275f, 0.320f));
            Texture2D selected = MakeTexture(new Color(0.090f, 0.365f, 0.410f));
            Texture2D active = MakeTexture(new Color(0.130f, 0.465f, 0.500f));
            Color text = new Color(0.91f, 0.94f, 0.97f);
            Color muted = new Color(0.66f, 0.73f, 0.80f);
            Color cyan = new Color(0.37f, 0.87f, 0.93f);
            Color amber = new Color(1f, 0.76f, 0.37f);
            textStyle = LabelStyle(16, text);
            smallStyle = LabelStyle(13, muted);
            titleStyle = LabelStyle(25, text, FontStyle.Bold);
            headingStyle = LabelStyle(18, text, FontStyle.Bold);
            accentStyle = LabelStyle(16, cyan, FontStyle.Bold);
            amberStyle = LabelStyle(14, amber);
            scoreStyle = LabelStyle(23, cyan, FontStyle.Bold);
            cardStyle = new GUIStyle(skin.box)
            {
                padding = new RectOffset(16, 16, 14, 14),
                margin = new RectOffset(0, 0, 0, 0),
                stretchWidth = true
            };
            SetStates(cardStyle, panel, panel, panel, text);
            skin.label = textStyle;
            skin.button.fontSize = 16;
            skin.button.wordWrap = true;
            skin.button.padding = new RectOffset(8, 8, 6, 6);
            skin.button.margin = new RectOffset(3, 3, 3, 3);
            SetStates(skin.button, button, hover, active, text);
            skin.textField.fontSize = 17;
            skin.textField.padding = new RectOffset(10, 10, 8, 6);
            SetStates(skin.textField, button, hover, active, text);
            dieStyle = new GUIStyle(skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
            selectedDieStyle = new GUIStyle(dieStyle);
            SetStates(selectedDieStyle, selected, active, active, Color.white);
            skin.verticalScrollbar.fixedWidth = 16;
            skin.verticalScrollbarThumb.fixedWidth = 16;
            SetStates(skin.verticalScrollbar, background, background, background, text);
            SetStates(skin.verticalScrollbarThumb, selected, active, active, text);
            skin.horizontalScrollbar.fixedHeight = 16;
            skin.horizontalScrollbarThumb.fixedHeight = 16;
            SetStates(skin.horizontalScrollbar, background, background, background, text);
            SetStates(skin.horizontalScrollbarThumb, selected, active, active, text);
        }

        private GUIStyle LabelStyle(int size, Color color, FontStyle fontStyle = FontStyle.Normal)
        {
            var style = new GUIStyle(skin.label)
            {
                fontSize = size,
                fontStyle = fontStyle,
                wordWrap = true,
                padding = new RectOffset(0, 0, 3, 3),
                margin = new RectOffset(0, 0, 0, 0),
                stretchWidth = true
            };
            style.normal.textColor = color;
            return style;
        }

        private static void SetStates(GUIStyle style, Texture2D normal, Texture2D hover, Texture2D active, Color color)
        {
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = active;
            style.focused.background = hover;
            style.onNormal.background = normal;
            style.onHover.background = hover;
            style.onActive.background = active;
            style.onFocused.background = hover;
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        private Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            ownedTextures.Add(texture);
            return texture;
        }

        private void OnDestroy()
        {
            foreach (Texture2D texture in ownedTextures) if (texture != null) Destroy(texture);
            if (skin != null) Destroy(skin);
        }
    }
}
