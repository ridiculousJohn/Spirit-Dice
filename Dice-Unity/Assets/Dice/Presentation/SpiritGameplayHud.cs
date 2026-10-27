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
     * footer, and stage intro. Authored controls bind to the encounter and score playback.
     */
    public sealed class SpiritGameplayHud : MonoBehaviour
    {
        // Units a highlighted counter's caption moves aside for the charm icon shown beside it.
        private const float CounterIconInset = 22f;
        [SerializeField] private RectTransform portrait;
        public RectTransform Root => portrait;
        [field: SerializeField] public RectTransform DiceTrayFrame { get; private set; }
        [field: SerializeField] public RawImage DiceTrayImage { get; private set; }
        [SerializeField] private RectTransform diceTrayFrame;
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

        [field: SerializeField] public Text EncounterTitle { get; private set; }
        [field: SerializeField] public Text StageLabel { get; private set; }
        [field: SerializeField] public Button MenuButton { get; private set; }
        [field: SerializeField] public RectTransform TargetPanel { get; private set; }
        [field: SerializeField] public Text TargetLabel { get; private set; }
        [field: SerializeField] public Text TargetCaption { get; private set; }
        [field: SerializeField] public RectTransform HandsPanel { get; private set; }
        [field: SerializeField] public Text HandsLabel { get; private set; }
        [field: SerializeField] public RectTransform RerollsPanel { get; private set; }
        [field: SerializeField] public Text RerollsLabel { get; private set; }
        [field: SerializeField] public Text ResourcesLabel { get; private set; }
        [field: SerializeField] public RectTransform PlayingPanel { get; private set; }
        [field: SerializeField] public Text SelectionLabel { get; private set; }
        [field: SerializeField] public SpiritUiOrnament ModifierEmblem { get; private set; }
        [field: SerializeField] public Image ModifierActivationIcon { get; private set; }
        [field: SerializeField] public Text CategoryLabel { get; private set; }
        [field: SerializeField] public Text ScoreDetail { get; private set; }
        [field: SerializeField] public Text BaseScoreLabel { get; private set; }
        [field: SerializeField] public Text MultiplierLabel { get; private set; }
        [field: SerializeField] public Text HandScoreLabel { get; private set; }
        [field: SerializeField] public Button RerollButton { get; private set; }
        [field: SerializeField] public Button PlayButton { get; private set; }
        [field: SerializeField] public CanvasGroup EncounterRuleGroup { get; private set; }
        [field: SerializeField] public Text EncounterRuleLabel { get; private set; }
        [field: SerializeField] public Image EncounterRuleIcon { get; private set; }
        public Graphic EncounterRuleSigil { get; private set; }
        [field: SerializeField] public CanvasGroup RuleSpiritSweep { get; private set; }
        [field: SerializeField] public RectTransform StageIntroPanel { get; private set; }
        [field: SerializeField] public Text StageIntroTitle { get; private set; }
        [field: SerializeField] public Text StageIntroDetail { get; private set; }
        [field: SerializeField, Min(1)] public int StageIntroDetailFontSize { get; private set; } = 19;
        [field: SerializeField] public Button CharmsButton { get; private set; }
        [field: SerializeField] public Button HandRanksButton { get; private set; }
        [field: SerializeField] public Text CharmCount { get; private set; }
        [field: SerializeField] public Text StatusLabel { get; private set; }
        [field: SerializeField] public Button InvestigationButton { get; private set; }
        public long DisplayedBaseScore { get; private set; }
        public long DisplayedMultiplier { get; private set; }

        [field: SerializeField] internal SpiritCounterHighlight BaseScoreHighlight { get; private set; } = new SpiritCounterHighlight();
        [field: SerializeField] internal SpiritCounterHighlight MultiplierHighlight { get; private set; } = new SpiritCounterHighlight();
        [field: SerializeField] internal SpiritCounterHighlight RerollsHighlight { get; private set; } = new SpiritCounterHighlight();
        [field: SerializeField] internal SpiritCounterHighlight HandsHighlight { get; private set; } = new SpiritCounterHighlight();

        public void BuildHeader(RectTransform parent, Action toggleMenu)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (toggleMenu == null) throw new ArgumentNullException(nameof(toggleMenu));
            if (portrait == null || EncounterTitle == null || StageLabel == null || MenuButton == null ||
                TargetPanel == null || TargetLabel == null || TargetCaption == null || HandsPanel == null ||
                HandsLabel == null || RerollsPanel == null || RerollsLabel == null || ResourcesLabel == null)
                throw new InvalidOperationException("The gameplay HUD requires authored header references.");
            this.toggleMenu = toggleMenu;
            MenuButton.onClick.RemoveListener(ToggleMenu);
            MenuButton.onClick.AddListener(ToggleMenu);
        }

        /** Returns the authored playing area and tray display. */
        public RectTransform BuildPlayingPanel()
        {
            if (PlayingPanel == null || DiceTrayFrame == null || DiceTrayImage == null)
                throw new InvalidOperationException("The gameplay HUD requires an authored playing panel and tray display.");
            return PlayingPanel;
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

        /** Binds the authored rule display to the tray used by the spirit sweep animation. */
        public void BuildEncounterRule(RectTransform trayFrame)
        {
            if (rerollEncounterRuleGroup == null || rerollEncounterRuleLabel == null || rerollEncounterRuleIcon == null ||
                playEncounterRuleGroup == null || playEncounterRuleLabel == null || playEncounterRuleIcon == null ||
                encounterRuleSigilRoot == null || playEncounterRuleSigilRoot == null || RuleSpiritSweep == null ||
                ruleSpiritIcon == null || ruleSpiritSigilRoot == null)
                throw new InvalidOperationException("The gameplay HUD requires authored encounter rule references.");
            diceTrayFrame = trayFrame != null ? trayFrame : DiceTrayFrame;
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
            ReplaceSigil(ref rerollEncounterRuleSigil, presentation, encounterRuleSigilRoot, "EncounterRuleSigil");
            ReplaceSigil(ref playEncounterRuleSigil, presentation, playEncounterRuleSigilRoot, "PlayEncounterRuleSigil");
            EncounterRuleSigil = EncounterRuleGroup == playEncounterRuleGroup ? playEncounterRuleSigil : rerollEncounterRuleSigil;
        }

        public void ConfigureHostSigils(SpiritHostPresentationAsset presentation)
        {
            ConfigureEncounterRuleSigil(presentation);
            ReplaceSigil(ref ruleSpiritSigil, presentation, ruleSpiritSigilRoot, "RuleSpiritSigil");
            if (ruleSpiritSigil != null) ruleSpiritSigil.color = ruleSpiritIcon.color;
        }

        private void ReplaceSigil(ref Graphic current, SpiritHostPresentationAsset presentation, RectTransform parent, string name)
        {
            if (current != null)
            {
                current.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(current.gameObject);
                else DestroyImmediate(current.gameObject);
            }
            current = presentation != null ? presentation.CreateSigil(name, parent) : null;
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
                EncounterRuleGroup.alpha = available ? 1 : .4f;
                EncounterRuleIcon.sprite = presentation.Icon;
                EncounterRuleIcon.enabled = presentation.Icon != null;
                if (EncounterRuleSigil != null) EncounterRuleSigil.gameObject.SetActive(presentation.Icon == null);
            }
            RerollButton.GetComponent<SpiritUiButtonVisual>().CaptureContent();
            PlayButton.GetComponent<SpiritUiButtonVisual>().CaptureContent();
        }

        public void SetScore(ScoreBreakdown score, bool played)
        {
            CategoryLabel.text = score == null ? "" : DiagnosticText.Humanize(score.Category.ToString()).ToUpperInvariant();
            if (played) SelectionLabel.text = "HAND SCORED";
        }

        public void SetScoreValues(long baseScore, long multiplier)
        {
            DisplayedBaseScore = baseScore;
            DisplayedMultiplier = multiplier;
            BaseScoreLabel.text = DiagnosticText.Number(baseScore);
            MultiplierLabel.text = DiagnosticText.Number(multiplier);
        }

        public void SetResources(int hands, int rerolls)
        {
            HandsLabel.text = "HANDS  " + hands;
            RerollsLabel.text = "REROLLS  " + rerolls;
            ResourcesLabel.text = hands + " hands left  /  " + rerolls + " rerolls left";
        }

        public void ShowResource(ResourceChangedEvent resource)
        {
            if (resource.Resource == ResourceKind.Rerolls) RerollsLabel.text = "REROLLS  " + resource.After;
            else if (resource.Resource == ResourceKind.Hands) HandsLabel.text = "HANDS  " + resource.After;
        }

        public void ShowActivationIcon(ModifierPresentationCatalog charms, ModifierDefinition definition)
        {
            ModifierActivationIcon.sprite = charms.GetIcon(definition);
            ModifierActivationIcon.color = charms.GetEffectColor(definition);
            ModifierActivationIcon.gameObject.SetActive(true);
            modifierActivationCategory.Render(charms, definition);
            ModifierEmblem.enabled = false;
            ModifierEmblem.gameObject.SetActive(true);
        }

        public void ResetTransientVisuals()
        {
            if (RuleSpiritSweep != null) RuleSpiritSweep.gameObject.SetActive(false);
            if (StageIntroPanel != null) StageIntroPanel.gameObject.SetActive(false);
            if (ModifierEmblem == null) return;
            CaptureAuthoredScales();
            ModifierEmblem.gameObject.SetActive(false);
            ModifierEmblem.enabled = true;
            ModifierActivationIcon.sprite = null;
            ModifierActivationIcon.gameObject.SetActive(false);
            ModifierActivationIcon.rectTransform.localScale = modifierActivationIconScale;
            ModifierActivationIcon.color = Color.white;
            modifierActivationCategory.Render(null, null);
            foreach (var highlight in new[] { BaseScoreHighlight, MultiplierHighlight, RerollsHighlight, HandsHighlight })
                ResetCounterHighlight(highlight);
            SelectionLabel.text = "";
            SelectionLabel.color = SpiritUi.GetTheme(transform).muted;
            SelectionLabel.rectTransform.localScale = selectionLabelScale;
            ScoreDetail.color = SpiritUi.GetTheme(transform).muted;
            ScoreDetail.rectTransform.localScale = scoreDetailScale;
            foreach (var label in new[] { BaseScoreLabel, MultiplierLabel })
            {
                label.color = SpiritUi.GetTheme(transform).cream;
                label.rectTransform.localScale = label == BaseScoreLabel ? baseScoreLabelScale : multiplierLabelScale;
            }
            HandScoreLabel.color = SpiritUi.GetTheme(transform).cream;
            HandScoreLabel.rectTransform.localScale = handScoreLabelScale;
            TargetLabel.color = SpiritUi.GetTheme(transform).cream;
            TargetLabel.rectTransform.localScale = targetLabelScale;
        }

        internal static void AddCounterHighlight(List<SpiritCounterHighlight> highlights, SpiritCounterHighlight highlight,
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

        internal IEnumerator AnimateScore(long beforeBase, long beforeMultiplier, long afterBase, long afterMultiplier, float duration,
            List<SpiritCounterHighlight> highlights = null)
        {
            CaptureAuthoredScales();
            float elapsed = 0;
            while (elapsed < duration)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / duration);
                SetScoreValues(Interpolate(beforeBase, afterBase, progress), Interpolate(beforeMultiplier, afterMultiplier, progress));
                if (beforeBase != afterBase)
                {
                    BaseScoreLabel.color = SpiritUi.GetTheme(transform).amber;
                    BaseScoreLabel.rectTransform.localScale = baseScoreLabelScale * (1 + .07f * Mathf.Sin(progress * Mathf.PI));
                }
                if (beforeMultiplier != afterMultiplier)
                {
                    MultiplierLabel.color = SpiritUi.GetTheme(transform).amber;
                    MultiplierLabel.rectTransform.localScale = multiplierLabelScale * (1 + .07f * Mathf.Sin(progress * Mathf.PI));
                }
                if (highlights != null)
                    foreach (var highlight in highlights)
                    {
                        float pulse = Mathf.Sin(progress * Mathf.PI);
                        highlight.Label.color = Color.Lerp(SpiritUi.GetTheme(transform).cream, highlight.Color, pulse);
                        highlight.Label.rectTransform.localScale = highlight.LabelScale * (1 + .07f * pulse);
                        highlight.Icon.rectTransform.localScale = highlight.IconScale * (1 + .1f * pulse);
                    }
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            SetScoreValues(afterBase, afterMultiplier);
            foreach (var label in new[] { BaseScoreLabel, MultiplierLabel })
            {
                label.color = SpiritUi.GetTheme(transform).cream;
                label.rectTransform.localScale = label == BaseScoreLabel ? baseScoreLabelScale : multiplierLabelScale;
            }
            if (highlights != null)
                foreach (var highlight in highlights) ResetCounterHighlight(highlight);
        }

        public IEnumerator AnimateTotal(long score, float duration)
        {
            CaptureAuthoredScales();
            float elapsed = 0;
            while (elapsed < duration)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / duration);
                HandScoreLabel.text = DiagnosticText.Number(Interpolate(0, score, progress));
                HandScoreLabel.color = SpiritUi.GetTheme(transform).amber;
                HandScoreLabel.rectTransform.localScale = handScoreLabelScale * (1 + .14f * Mathf.Sin(progress * Mathf.PI));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            HandScoreLabel.text = DiagnosticText.Number(score);
            HandScoreLabel.color = SpiritUi.GetTheme(transform).cream;
            HandScoreLabel.rectTransform.localScale = handScoreLabelScale;
        }

        public IEnumerator AnimateThreshold(long before, long after, float duration)
        {
            CaptureAuthoredScales();
            float elapsed = 0;
            TargetLabel.text = DiagnosticText.Number(before);
            while (elapsed < duration)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / duration);
                TargetLabel.text = DiagnosticText.Number(Interpolate(before, after, progress));
                TargetLabel.color = SpiritUi.GetTheme(transform).amber;
                TargetLabel.rectTransform.localScale = targetLabelScale * (1 + .06f * Mathf.Sin(progress * Mathf.PI));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            TargetLabel.text = DiagnosticText.Number(after);
            TargetLabel.color = SpiritUi.GetTheme(transform).cream;
            TargetLabel.rectTransform.localScale = targetLabelScale;
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
                rect.anchoredPosition = new Vector2(Mathf.Lerp(-36, diceTrayFrame.rect.width + 36, progress),
                    Mathf.Sin(progress * Mathf.PI) * 16);
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

        private static void ResetCounterHighlight(SpiritCounterHighlight highlight)
        {
            highlight.Label.color = SpiritUi.GetTheme(highlight.Label.transform).cream;
            highlight.Label.rectTransform.localScale = highlight.LabelScale;
            highlight.Caption.rectTransform.offsetMin = highlight.OriginalInset;
            highlight.Icon.gameObject.SetActive(false);
            highlight.Icon.sprite = null;
            highlight.Icon.rectTransform.localScale = highlight.IconScale;
            highlight.Icon.color = Color.white;
        }

        private static long Interpolate(long before, long after, float progress)
            => (long)((decimal)before + ((decimal)after - before) * (decimal)progress);
    }
}
