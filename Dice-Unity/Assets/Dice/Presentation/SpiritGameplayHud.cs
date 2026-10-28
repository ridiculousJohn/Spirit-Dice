using System;
using System.Collections;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Counter that a charm's score or resource effect highlights while it animates. */
    [Serializable]
    internal sealed class SpiritCounterHighlight
    {
        [SerializeField] internal Text Label;
        [SerializeField] internal Text Caption;
        [SerializeField] internal Image Icon;
        internal Color Color;
        internal Vector2 OriginalInset;
        internal Vector3 LabelScale;
        internal Vector3 IconScale;
    }

    /**
     * Gameplay HUD around the dice tray: header, target and resources, score preview, actions, encounter rule,
     * footer, and stage intro. Authored controls bind to the encounter and score playback, which tell the HUD what
     * happened or what to show; the HUD fills its own elements from the wording asset.
     */
    public sealed class SpiritGameplayHud : MonoBehaviour
    {
        // Units a highlighted counter's caption moves aside for the charm icon shown beside it.
        private const float CounterIconInset = 22f;
        // Opacity of the encounter rule badge while its rule is unavailable.
        private const float UnavailableRuleAlpha = .4f;
        // Extra scale at the peak of a counting label's swell: the score counters and highlighted labels, a highlighted
        // counter's icon, the hand total and the threshold.
        private const float ScorePulse = .07f;
        private const float HighlightIconPulse = .1f;
        private const float TotalPulse = .14f;
        private const float ThresholdPulse = .06f;
        // Units the rule spirit starts left of the tray and ends right of it, and the height of its arc.
        private const float RuleSweepOverhang = 36f;
        private const float RuleSweepArc = 16f;
        [SerializeField] private RectTransform portrait;
        public RectTransform Root => portrait;
        [field: SerializeField] public RectTransform DiceTrayFrame { get; private set; }
        [field: SerializeField] public RawImage DiceTrayImage { get; private set; }
        [SerializeField] private CanvasGroup rerollEncounterRuleGroup;
        [SerializeField] private Text rerollEncounterRuleLabel;
        [SerializeField] private Image rerollEncounterRuleIcon;
        [SerializeField] private CanvasGroup playEncounterRuleGroup;
        [SerializeField] private Text playEncounterRuleLabel;
        [SerializeField] private Image playEncounterRuleIcon;
        [SerializeField] private RectTransform playEncounterRuleSigilRoot;
        private Graphic rerollEncounterRuleSigil;
        private Graphic playEncounterRuleSigil;
        private Action toggleMenu;
        private Action reroll;
        private Action play;
        private Action showCharms;
        private Action showHandRanks;
        private Action returnToInvestigation;
        private bool authoredScalesCaptured;
        private Vector3 modifierActivationIconScale;
        private Vector3 selectionLabelScale;
        private Vector3 scoreDetailScale;
        private Vector3 baseScoreLabelScale;
        private Vector3 multiplierLabelScale;
        private Vector3 handScoreLabelScale;
        private Vector3 targetLabelScale;
        [SerializeField] private RectTransform encounterRuleSigilRoot;
        [SerializeField] private Image ruleSpiritIcon;
        private Graphic ruleSpiritSigil;
        [SerializeField] private RectTransform ruleSpiritSigilRoot;
        [SerializeField] private SpiritModifierCategoryBadge modifierActivationCategory;

        [field: SerializeField] private Text EncounterTitle { get; set; }
        [field: SerializeField] private Text StageLabel { get; set; }
        [field: SerializeField] private Button MenuButton { get; set; }
        [field: SerializeField] private RectTransform TargetPanel { get; set; }
        [field: SerializeField] private Text TargetLabel { get; set; }
        [field: SerializeField] private Text TargetCaption { get; set; }
        [field: SerializeField] private RectTransform HandsPanel { get; set; }
        [field: SerializeField] private Text HandsLabel { get; set; }
        [field: SerializeField] private RectTransform RerollsPanel { get; set; }
        [field: SerializeField] private Text RerollsLabel { get; set; }
        [field: SerializeField] private Text ResourcesLabel { get; set; }
        [field: SerializeField] private RectTransform PlayingPanel { get; set; }
        [field: SerializeField] private Text SelectionLabel { get; set; }
        [field: SerializeField] private Image ModifierEmblem { get; set; }
        [field: SerializeField] private Image ModifierActivationIcon { get; set; }
        [field: SerializeField] private Text CategoryLabel { get; set; }
        [field: SerializeField] private Text ScoreDetail { get; set; }
        [field: SerializeField] private Text BaseScoreLabel { get; set; }
        [field: SerializeField] private Text MultiplierLabel { get; set; }
        [field: SerializeField] private Text HandScoreLabel { get; set; }
        [field: SerializeField] private Button RerollButton { get; set; }
        [field: SerializeField] private Button PlayButton { get; set; }
        [field: SerializeField] private CanvasGroup EncounterRuleGroup { get; set; }
        [field: SerializeField] private Text EncounterRuleLabel { get; set; }
        [field: SerializeField] private Image EncounterRuleIcon { get; set; }
        private Graphic EncounterRuleSigil { get; set; }
        [field: SerializeField] private CanvasGroup RuleSpiritSweep { get; set; }
        [field: SerializeField] private RectTransform StageIntroPanel { get; set; }
        [field: SerializeField] private Text StageIntroTitle { get; set; }
        [field: SerializeField] private Text StageIntroDetail { get; set; }
        [field: SerializeField, Min(1)] private int StageIntroDetailFontSize { get; set; } = 19;
        [field: SerializeField] private Button CharmsButton { get; set; }
        [field: SerializeField] private Button HandRanksButton { get; set; }
        [field: SerializeField] private Text CharmCount { get; set; }
        [field: SerializeField] private Text StatusLabel { get; set; }
        [field: SerializeField] private Button InvestigationButton { get; set; }
        /** The base score and multiplier the score counters show, from which a scoring step counts on. */
        public long DisplayedBaseScore { get; private set; }
        public long DisplayedMultiplier { get; private set; }

        [field: SerializeField] private SpiritCounterHighlight BaseScoreHighlight { get; set; } = new SpiritCounterHighlight();
        [field: SerializeField] private SpiritCounterHighlight MultiplierHighlight { get; set; } = new SpiritCounterHighlight();
        [field: SerializeField] private SpiritCounterHighlight RerollsHighlight { get; set; } = new SpiritCounterHighlight();
        [field: SerializeField] private SpiritCounterHighlight HandsHighlight { get; set; } = new SpiritCounterHighlight();
        // Counters the shown scoring step highlights while its animation runs.
        private List<SpiritCounterHighlight> stepHighlights = new List<SpiritCounterHighlight>();
        // Opacity groups of the HUD and its tray display in an editor preview, and their authored settings.
        private CanvasGroup previewHudVisibility;
        private CanvasGroup previewTrayVisibility;
        private float authoredPreviewHudAlpha;
        private bool authoredPreviewTrayIgnoresParentGroups;

        public void BuildHeader(RectTransform parent, Action toggleMenu)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (toggleMenu == null) throw new ArgumentNullException(nameof(toggleMenu));
            if (portrait == null || EncounterTitle == null || StageLabel == null || MenuButton == null ||
                TargetPanel == null || TargetLabel == null || TargetCaption == null || HandsPanel == null ||
                HandsLabel == null || RerollsPanel == null || RerollsLabel == null || ResourcesLabel == null)
                throw new InvalidOperationException("The gameplay HUD requires authored header references.");
            SpiritUi.RequireWording(transform);
            this.toggleMenu = toggleMenu;
            MenuButton.onClick.RemoveListener(ToggleMenu);
            MenuButton.onClick.AddListener(ToggleMenu);
        }

        /** Checks the authored playing area and tray display. */
        public void BuildPlayingPanel()
        {
            if (PlayingPanel == null || DiceTrayFrame == null || DiceTrayImage == null)
                throw new InvalidOperationException("The gameplay HUD requires an authored playing panel and tray display.");
        }

        public void BuildPlayingHud(Action reroll, Action play)
        {
            if (reroll == null) throw new ArgumentNullException(nameof(reroll));
            if (play == null) throw new ArgumentNullException(nameof(play));
            if (SelectionLabel == null || ModifierEmblem == null || ModifierActivationIcon == null ||
                modifierActivationCategory == null || CategoryLabel == null ||
                ScoreDetail == null || BaseScoreLabel == null || MultiplierLabel == null || HandScoreLabel == null ||
                RerollButton == null || PlayButton == null || !Valid(BaseScoreHighlight) || !Valid(MultiplierHighlight) ||
                !Valid(RerollsHighlight) || !Valid(HandsHighlight))
                throw new InvalidOperationException("The gameplay HUD requires authored score and action references.");
            this.reroll = reroll;
            this.play = play;
            CaptureAuthoredScales();
            RerollButton.onClick.RemoveListener(Reroll);
            RerollButton.onClick.AddListener(Reroll);
            PlayButton.onClick.RemoveListener(Play);
            PlayButton.onClick.AddListener(Play);
        }

        /** Checks the authored rule display; the spirit sweep animation crosses the tray frame. */
        public void BuildEncounterRule()
        {
            if (rerollEncounterRuleGroup == null || rerollEncounterRuleLabel == null || rerollEncounterRuleIcon == null ||
                playEncounterRuleGroup == null || playEncounterRuleLabel == null || playEncounterRuleIcon == null ||
                encounterRuleSigilRoot == null || playEncounterRuleSigilRoot == null || RuleSpiritSweep == null ||
                ruleSpiritIcon == null || ruleSpiritSigilRoot == null)
                throw new InvalidOperationException("The gameplay HUD requires authored encounter rule references.");
        }

        public void BuildStageIntro()
        {
            if (StageIntroPanel == null || StageIntroTitle == null || StageIntroDetail == null)
                throw new InvalidOperationException("The gameplay HUD requires an authored stage intro.");
        }

        public void BuildFooter(Action showCharms, Action showHandRanks, Action returnToInvestigation)
        {
            if (showCharms == null) throw new ArgumentNullException(nameof(showCharms));
            if (showHandRanks == null) throw new ArgumentNullException(nameof(showHandRanks));
            if (returnToInvestigation == null) throw new ArgumentNullException(nameof(returnToInvestigation));
            if (CharmsButton == null || HandRanksButton == null || CharmCount == null || StatusLabel == null || InvestigationButton == null)
                throw new InvalidOperationException("The gameplay HUD requires authored footer references.");
            this.showCharms = showCharms;
            this.showHandRanks = showHandRanks;
            this.returnToInvestigation = returnToInvestigation;
            CharmsButton.onClick.RemoveListener(ShowCharms);
            CharmsButton.onClick.AddListener(ShowCharms);
            HandRanksButton.onClick.RemoveListener(ShowHandRanks);
            HandRanksButton.onClick.AddListener(ShowHandRanks);
            InvestigationButton.onClick.RemoveListener(ReturnToInvestigation);
            InvestigationButton.onClick.AddListener(ReturnToInvestigation);
        }

        public void SetGameplayVisible(bool visible, bool showInvestigation)
        {
            foreach (var rect in new[] { PlayingPanel, TargetPanel, HandsPanel, RerollsPanel })
                rect.gameObject.SetActive(visible);
            StageLabel.gameObject.SetActive(visible);
            StatusLabel.gameObject.SetActive(visible);
            ResourcesLabel.gameObject.SetActive(visible);
            CharmsButton.gameObject.SetActive(visible);
            HandRanksButton.gameObject.SetActive(visible);
            MenuButton.gameObject.SetActive(visible);
            InvestigationButton.gameObject.SetActive(visible && showInvestigation);
            EncounterRuleGroup.gameObject.SetActive(visible);
        }

        public void ConfigureEncounterRuleSigil(SpiritHostPresentationAsset presentation)
        {
            rerollEncounterRuleSigil = SpiritHostPresentationAsset.ReplaceSigil(rerollEncounterRuleSigil, presentation,
                "EncounterRuleSigil", encounterRuleSigilRoot);
            playEncounterRuleSigil = SpiritHostPresentationAsset.ReplaceSigil(playEncounterRuleSigil, presentation,
                "PlayEncounterRuleSigil", playEncounterRuleSigilRoot);
            EncounterRuleSigil = EncounterRuleGroup == playEncounterRuleGroup ? playEncounterRuleSigil : rerollEncounterRuleSigil;
        }

        public void ConfigureHostSigils(SpiritHostPresentationAsset presentation)
        {
            ConfigureEncounterRuleSigil(presentation);
            ruleSpiritSigil = SpiritHostPresentationAsset.ReplaceSigil(ruleSpiritSigil, presentation, "RuleSpiritSigil",
                ruleSpiritSigilRoot);
            if (ruleSpiritSigil != null) ruleSpiritSigil.color = ruleSpiritIcon.color;
        }

        public void RenderEncounterRule(EncounterSnapshot snapshot, EncounterRulePresentationAsset presentation)
        {
            EncounterRuleSnapshot rule = null;
            if (presentation != null && snapshot?.Phase == EncounterPhase.Playing)
                foreach (var candidate in snapshot.EncounterRules)
                    if (candidate.Id == presentation.RuleId) { rule = candidate; break; }
            RenderEncounterRuleBadge(presentation, rule != null, rule != null && rule.IsAvailable,
                rule != null ? presentation.TextFor(rule) : null);
        }

        public void RenderEncounterRuleBadge(EncounterRulePresentationAsset presentation, bool visible, bool available, string text)
        {
            bool onPlay = presentation != null && presentation.ActivationPhase == ModifierPhase.PlayHand;
            rerollEncounterRuleGroup.gameObject.SetActive(visible && !onPlay);
            playEncounterRuleGroup.gameObject.SetActive(visible && onPlay);
            EncounterRuleGroup = onPlay ? playEncounterRuleGroup : rerollEncounterRuleGroup;
            EncounterRuleLabel = onPlay ? playEncounterRuleLabel : rerollEncounterRuleLabel;
            EncounterRuleIcon = onPlay ? playEncounterRuleIcon : rerollEncounterRuleIcon;
            EncounterRuleSigil = onPlay ? playEncounterRuleSigil : rerollEncounterRuleSigil;
            if (visible)
            {
                EncounterRuleLabel.text = text;
                EncounterRuleGroup.alpha = available ? 1 : UnavailableRuleAlpha;
                EncounterRuleIcon.sprite = presentation.Icon;
                EncounterRuleIcon.enabled = presentation.Icon != null;
                if (EncounterRuleSigil != null) EncounterRuleSigil.gameObject.SetActive(presentation.Icon == null);
            }
            RerollButton.GetComponent<SpiritUiButtonVisual>().CaptureContent();
            PlayButton.GetComponent<SpiritUiButtonVisual>().CaptureContent();
        }

        public void ShowEncounterTitle(string title) => EncounterTitle.text = title;

        /** Shows the stage line and the threshold left to clear. */
        public void ShowStageTarget(int stageNumber, int stageCount, long threshold)
        {
            var wording = SpiritUi.GetWording(transform).hud;
            StageLabel.text = wording.Stage(stageNumber, stageCount);
            TargetLabel.text = DiagnosticText.Number(threshold);
            TargetCaption.text = wording.toClear;
        }

        /** Shows the hands and rerolls counters, in place of the resources line, with their counts. */
        public void ShowHandCounters(int hands, int rerolls)
        {
            HandsPanel.gameObject.SetActive(true);
            RerollsPanel.gameObject.SetActive(true);
            ResourcesLabel.gameObject.SetActive(false);
            SetResources(hands, rerolls);
        }

        /** Shows a hand waiting for play: the score counters at these values and no rank, total, step or selection text. */
        public void ShowIdleScore(long baseScore, long multiplier)
        {
            ModifierEmblem.gameObject.SetActive(false);
            // The hand rank shows only while a played hand is scored.
            CategoryLabel.text = "";
            SetScoreValues(baseScore, multiplier);
            HandScoreLabel.text = "0";
            ScoreDetail.text = "";
            // Idle play shows no instruction or selection count; the tray highlight marks the reroll selection.
            SelectionLabel.text = "";
        }

        public void ShowStatus(string text) => StatusLabel.text = text;

        /** Shows that the encounter screen has no run: no playing area, the table's rule badge hidden, and the status. */
        public void ShowNoEncounter(EncounterRulePresentationAsset ruleCard, SpiritStatus status)
        {
            RenderEncounterRule(null, ruleCard);
            PlayingPanel.gameObject.SetActive(false);
            ShowStatus(SpiritStatusText.Describe(status, SpiritUi.GetWording(transform).status));
        }

        /**
         * Shows a snapshot at rest: its stage, target, resources, rule badge, charm count and status, and while it plays an
         * idle hand at its charm scores with the actions its reroll selection allows. The resources line shows the last
         * hand's score when one was scored, and the total score otherwise.
         */
        public void ShowSnapshot(EncounterSnapshot snapshot, EncounterRulePresentationAsset ruleCard, SpiritStatus status,
            ScoreBreakdown lastScore, int selectedMask)
        {
            var wording = SpiritUi.GetWording(transform);
            bool playing = snapshot.Phase == EncounterPhase.Playing;
            bool completed = snapshot.Phase == EncounterPhase.Completed;
            bool choosing = snapshot.Phase == EncounterPhase.ChoosingHandUpgrade || snapshot.Phase == EncounterPhase.ChoosingUnlock;
            TargetPanel.gameObject.SetActive(!choosing);
            StatusLabel.gameObject.SetActive(!choosing);
            PlayingPanel.gameObject.SetActive(playing);
            StageLabel.text = wording.hud.Stage(snapshot.StageIndex + 1, snapshot.StageCount);
            TargetLabel.text = DiagnosticText.Number(completed ? snapshot.TotalScore : snapshot.RemainingThreshold);
            TargetCaption.text = playing ? wording.hud.toClear : completed ? wording.hud.totalScore : wording.hud.stageCleared;
            HandsPanel.gameObject.SetActive(playing);
            RerollsPanel.gameObject.SetActive(playing);
            ResourcesLabel.gameObject.SetActive(!playing && !choosing);
            SetResources(snapshot.HandsRemaining, snapshot.RerollsRemaining);
            RenderEncounterRule(snapshot, ruleCard);
            ResourcesLabel.text = playing ? wording.hud.Resources(snapshot.HandsRemaining, snapshot.RerollsRemaining) :
                lastScore != null ? wording.hud.LastHand(lastScore.HandScore) : wording.hud.TotalScoreLine(snapshot.TotalScore);
            CharmCount.text = wording.hud.CharmCount(snapshot.Modifiers.Count);
            SpiritUi.SetButtonLabel(CharmsButton, wording.hud.CharmsButton(snapshot.Modifiers.Count));
            ShowStatus(SpiritStatusText.Gameplay(status, wording.status));
            if (!playing) return;
            ShowIdleScore(snapshot.ModifierBaseScore, snapshot.ModifierMultiplier);
            PlayButton.interactable = true;
            RerollButton.interactable = selectedMask != 0 && snapshot.CanReroll;
        }

        /** A roll shows the playing area. */
        public void ShowDiceRoll() => PlayingPanel.gameObject.SetActive(true);

        /**
         * Shows a starting stage's line, threshold and counters and prepares its intro panel, hidden; the playing area hides
         * until the stage's first roll.
         */
        public void ShowStageStart(StageStartedEvent stage, int stageCount)
        {
            ShowStageTarget(stage.StageIndex + 1, stageCount, stage.Threshold);
            ShowHandCounters(stage.Hands, stage.Rerolls);
            PlayingPanel.gameObject.SetActive(false);
            StageIntroTitle.text = StageLabel.text;
            StageIntroDetail.fontSize = StageIntroDetailFontSize;
            StageIntroDetail.text = SpiritUi.GetWording(transform).hud.StageIntro(stage.Threshold, stage.Hands, stage.Rerolls);
            StageIntroPanel.gameObject.SetActive(false);
        }

        /** Shows the intro panel the stage start prepared. */
        public void ShowStageIntro() => StageIntroPanel.gameObject.SetActive(true);

        public void HideStageIntro() => StageIntroPanel.gameObject.SetActive(false);

        /** Shows a starting hand: its rule badge and resources, cleared score counters and the new hand line. */
        public void ShowHandStart(HandStartedEvent hand, EncounterSnapshot snapshot, EncounterRulePresentationAsset ruleCard)
        {
            RenderEncounterRule(snapshot, ruleCard);
            SetResources(hand.HandsRemaining, hand.RerollsRemaining);
            ShowIdleScore(0, 0);
            SelectionLabel.color = Theme.muted;
            SelectionLabel.text = SpiritUi.GetWording(transform).hud.newHand;
        }

        /** Counts the threshold left to clear from one value to the other over the duration. */
        public IEnumerator ShowThresholdChange(long before, long after, float duration)
        {
            TargetCaption.text = SpiritUi.GetWording(transform).hud.toClear;
            CaptureAuthoredScales();
            return CountLabel(TargetLabel, targetLabelScale, before, after, duration, ThresholdPulse);
        }

        public void ShowStageCleared()
        {
            TargetLabel.text = "0";
            TargetCaption.text = SpiritUi.GetWording(transform).hud.stageCleared;
        }

        /** Shows a resource's new count; a reroll change also shows the rerolls remaining as the selection line. */
        public void ShowResourceChanged(ResourceChangedEvent resource)
        {
            ShowResource(resource);
            if (resource.Resource == ResourceKind.Rerolls)
                SelectionLabel.text = SpiritUi.GetWording(transform).hud.RerollsRemaining(resource.After);
        }

        /** Shows the played hand's running total before a step that does not reveal the final total. */
        public void ShowHandTotal(long total) => HandScoreLabel.text = DiagnosticText.Number(total);

        /** Shows a scoring step's emblem, rank, selection and step lines, its counter highlights and its granted resources. */
        public void ShowScoringStep(SpiritScoringStep step, ModifierPresentationCatalog charms)
        {
            if (step.Emblem != null) ShowActivationIcon(charms, step.Emblem);
            else ModifierEmblem.gameObject.SetActive(false);
            CategoryLabel.text = step.CategoryText;
            SelectionLabel.color = step.SelectionColor;
            SelectionLabel.text = step.SelectionText;
            ScoreDetail.text = step.StepLabel;
            stepHighlights = new List<SpiritCounterHighlight>();
            if (step.HighlightsBaseScore) AddCounterHighlight(stepHighlights, BaseScoreHighlight, charms, step.Charm);
            if (step.HighlightsMultiplier) AddCounterHighlight(stepHighlights, MultiplierHighlight, charms, step.Charm);
            if (step.HighlightsRerolls) AddCounterHighlight(stepHighlights, RerollsHighlight, charms, step.Charm);
            if (step.HighlightsHands) AddCounterHighlight(stepHighlights, HandsHighlight, charms, step.Charm);
            foreach (var grant in step.Grants) ShowResource(grant);
        }

        /** Animates the shown scoring step: its revealed total, or its score counters from those shown with its highlights. */
        public IEnumerator AnimateScoringStep(SpiritScoringStep step, float duration)
        {
            CaptureAuthoredScales();
            return step.RevealsTotal ? CountLabel(HandScoreLabel, handScoreLabelScale, 0, step.Total, duration, TotalPulse)
                : AnimateScore(DisplayedBaseScore, DisplayedMultiplier, step.BaseScore, step.Multiplier, duration, stepHighlights);
        }

        /** Shows a rule presenter's selection line, marked as the rule's. */
        public void ShowRuleSelection(string text)
        {
            SelectionLabel.text = text;
            SelectionLabel.color = Theme.amber;
        }

        private void SetScoreValues(long baseScore, long multiplier)
        {
            DisplayedBaseScore = baseScore;
            DisplayedMultiplier = multiplier;
            BaseScoreLabel.text = DiagnosticText.Number(baseScore);
            MultiplierLabel.text = DiagnosticText.Number(multiplier);
        }

        private void SetResources(int hands, int rerolls)
        {
            var wording = SpiritUi.GetWording(transform).hud;
            HandsLabel.text = wording.Hands(hands);
            RerollsLabel.text = wording.Rerolls(rerolls);
            ResourcesLabel.text = wording.Resources(hands, rerolls);
        }

        private void ShowResource(ResourceChangedEvent resource)
        {
            var wording = SpiritUi.GetWording(transform).hud;
            if (resource.Resource == ResourceKind.Rerolls) RerollsLabel.text = wording.Rerolls(resource.After);
            else if (resource.Resource == ResourceKind.Hands) HandsLabel.text = wording.Hands(resource.After);
        }

        private void ShowActivationIcon(ModifierPresentationCatalog charms, ModifierDefinition definition)
        {
            SpiritCharmVisuals.SetIcon(ModifierActivationIcon, modifierActivationCategory, charms, definition);
            ModifierEmblem.enabled = false;
            ModifierEmblem.gameObject.SetActive(true);
        }

        /**
         * Readies the HUD for a still editor preview: its tray display shows the rendered tray, and an opacity group on the
         * HUD, which passes input through, lets the preview hide the HUD while the tray display stays shown.
         */
        public void PreparePreview(Texture trayTexture)
        {
            DiceTrayImage.texture = trayTexture;
            DiceTrayImage.enabled = true;
            previewHudVisibility = GetComponent<CanvasGroup>();
            if (previewHudVisibility == null) previewHudVisibility = gameObject.AddComponent<CanvasGroup>();
            previewHudVisibility.blocksRaycasts = false;
            authoredPreviewHudAlpha = previewHudVisibility.alpha;
            previewTrayVisibility = DiceTrayFrame.GetComponent<CanvasGroup>();
            if (previewTrayVisibility == null) previewTrayVisibility = DiceTrayFrame.gameObject.AddComponent<CanvasGroup>();
            authoredPreviewTrayIgnoresParentGroups = previewTrayVisibility.ignoreParentGroups;
        }

        /** Shows or hides the prepared preview HUD at its authored opacity; the tray display stays shown either way. */
        public void ShowInPreview(bool visible)
        {
            if (previewHudVisibility == null) throw new InvalidOperationException("The HUD was not prepared for a preview.");
            previewHudVisibility.alpha = visible ? authoredPreviewHudAlpha : 0;
            previewTrayVisibility.ignoreParentGroups = !visible || authoredPreviewTrayIgnoresParentGroups;
        }

        public void ResetTransientVisuals()
        {
            if (RuleSpiritSweep != null) RuleSpiritSweep.gameObject.SetActive(false);
            if (StageIntroPanel != null) StageIntroPanel.gameObject.SetActive(false);
            if (ModifierEmblem == null) return;
            CaptureAuthoredScales();
            ModifierEmblem.gameObject.SetActive(false);
            ModifierEmblem.enabled = true;
            SpiritCharmVisuals.SetIcon(ModifierActivationIcon, modifierActivationCategory, null, null);
            ModifierActivationIcon.rectTransform.localScale = modifierActivationIconScale;
            foreach (var highlight in new[] { BaseScoreHighlight, MultiplierHighlight, RerollsHighlight, HandsHighlight })
                ResetCounterHighlight(highlight);
            var theme = Theme;
            SelectionLabel.text = "";
            SelectionLabel.color = theme.muted;
            SelectionLabel.rectTransform.localScale = selectionLabelScale;
            ScoreDetail.color = theme.muted;
            ScoreDetail.rectTransform.localScale = scoreDetailScale;
            ResetScoreLabels(theme);
            HandScoreLabel.color = theme.cream;
            HandScoreLabel.rectTransform.localScale = handScoreLabelScale;
            TargetLabel.color = theme.cream;
            TargetLabel.rectTransform.localScale = targetLabelScale;
        }

        private void ResetScoreLabels(SpiritUiTheme theme)
        {
            foreach (var label in new[] { BaseScoreLabel, MultiplierLabel })
            {
                label.color = theme.cream;
                label.rectTransform.localScale = label == BaseScoreLabel ? baseScoreLabelScale : multiplierLabelScale;
            }
        }

        private static void AddCounterHighlight(List<SpiritCounterHighlight> highlights, SpiritCounterHighlight highlight,
            ModifierPresentationCatalog charms, ModifierDefinition definition)
        {
            if (highlights.Contains(highlight)) return;
            highlight.Color = charms.GetEffectColor(definition);
            highlight.Icon.sprite = charms.GetIcon(definition);
            highlight.Icon.color = highlight.Color;
            highlight.Icon.gameObject.SetActive(true);
            highlight.Caption.rectTransform.offsetMin = highlight.OriginalInset + new Vector2(CounterIconInset, 0);
            highlights.Add(highlight);
        }

        private IEnumerator AnimateScore(long beforeBase, long beforeMultiplier, long afterBase, long afterMultiplier, float duration,
            List<SpiritCounterHighlight> highlights)
        {
            var theme = Theme;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / duration);
                SetScoreValues(Interpolate(beforeBase, afterBase, progress), Interpolate(beforeMultiplier, afterMultiplier, progress));
                float pulse = Mathf.Sin(progress * Mathf.PI);
                if (beforeBase != afterBase) Pulse(BaseScoreLabel, baseScoreLabelScale, ScorePulse * pulse, theme.amber);
                if (beforeMultiplier != afterMultiplier) Pulse(MultiplierLabel, multiplierLabelScale, ScorePulse * pulse, theme.amber);
                if (highlights != null)
                    foreach (var highlight in highlights)
                    {
                        Pulse(highlight.Label, highlight.LabelScale, ScorePulse * pulse, Color.Lerp(theme.cream, highlight.Color, pulse));
                        highlight.Icon.rectTransform.localScale = highlight.IconScale * (1 + HighlightIconPulse * pulse);
                    }
                yield return null;
            }
            SetScoreValues(afterBase, afterMultiplier);
            ResetScoreLabels(theme);
            if (highlights != null)
                foreach (var highlight in highlights) ResetCounterHighlight(highlight);
        }

        /** Counts one label from one value to the other, swelling it in amber, then rests it in cream at its authored scale. */
        private IEnumerator CountLabel(Text label, Vector3 restScale, long before, long after, float duration, float pulse)
        {
            var theme = Theme;
            label.text = DiagnosticText.Number(before);
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / duration);
                label.text = DiagnosticText.Number(Interpolate(before, after, progress));
                Pulse(label, restScale, pulse * Mathf.Sin(progress * Mathf.PI), theme.amber);
                yield return null;
            }
            label.text = DiagnosticText.Number(after);
            label.color = theme.cream;
            label.rectTransform.localScale = restScale;
        }

        private static void Pulse(Text label, Vector3 restScale, float swell, Color color)
        {
            label.color = color;
            label.rectTransform.localScale = restScale * (1 + swell);
        }

        public IEnumerator AnimateRuleSpirit(Sprite icon, float duration)
        {
            var rect = (RectTransform)RuleSpiritSweep.transform;
            ruleSpiritIcon.sprite = icon;
            ruleSpiritIcon.gameObject.SetActive(icon != null);
            if (ruleSpiritSigil != null) ruleSpiritSigil.gameObject.SetActive(icon == null);
            RuleSpiritSweep.gameObject.SetActive(true);
            float elapsed = 0;
            while (elapsed < duration)
            {
                float progress = elapsed / duration;
                rect.anchoredPosition = new Vector2(Mathf.Lerp(-RuleSweepOverhang, DiceTrayFrame.rect.width + RuleSweepOverhang, progress),
                    Mathf.Sin(progress * Mathf.PI) * RuleSweepArc);
                RuleSpiritSweep.alpha = Mathf.Sin(progress * Mathf.PI);
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            RuleSpiritSweep.gameObject.SetActive(false);
        }

        private void ToggleMenu() => toggleMenu?.Invoke();
        private void Reroll() => reroll?.Invoke();
        private void Play() => play?.Invoke();
        private void ShowCharms() => showCharms?.Invoke();
        private void ShowHandRanks() => showHandRanks?.Invoke();
        private void ReturnToInvestigation() => returnToInvestigation?.Invoke();

        private static bool Valid(SpiritCounterHighlight highlight)
            => highlight != null && highlight.Label != null && highlight.Caption != null && highlight.Icon != null;

        private void CaptureAuthoredScales()
        {
            if (authoredScalesCaptured) return;
            modifierActivationIconScale = ModifierActivationIcon.rectTransform.localScale;
            selectionLabelScale = SelectionLabel.rectTransform.localScale;
            scoreDetailScale = ScoreDetail.rectTransform.localScale;
            baseScoreLabelScale = BaseScoreLabel.rectTransform.localScale;
            multiplierLabelScale = MultiplierLabel.rectTransform.localScale;
            handScoreLabelScale = HandScoreLabel.rectTransform.localScale;
            targetLabelScale = TargetLabel.rectTransform.localScale;
            foreach (var highlight in new[] { BaseScoreHighlight, MultiplierHighlight, RerollsHighlight, HandsHighlight })
            {
                highlight.OriginalInset = highlight.Caption.rectTransform.offsetMin;
                highlight.LabelScale = highlight.Label.rectTransform.localScale;
                highlight.IconScale = highlight.Icon.rectTransform.localScale;
            }
            authoredScalesCaptured = true;
        }

        private void ResetCounterHighlight(SpiritCounterHighlight highlight)
        {
            highlight.Label.color = Theme.cream;
            highlight.Label.rectTransform.localScale = highlight.LabelScale;
            highlight.Caption.rectTransform.offsetMin = highlight.OriginalInset;
            highlight.Icon.gameObject.SetActive(false);
            highlight.Icon.sprite = null;
            highlight.Icon.rectTransform.localScale = highlight.IconScale;
            highlight.Icon.color = Color.white;
        }

        private SpiritUiTheme Theme => SpiritUi.GetTheme(transform);

        private static long Interpolate(long before, long after, float progress)
            => (long)((decimal)before + ((decimal)after - before) * (decimal)progress);
    }
}
