using System;
using System.Collections;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Counter that a charm's score or resource effect highlights while it animates. */
    internal sealed class SpiritCounterHighlight
    {
        internal Text Label;
        internal Text Caption;
        internal Image Icon;
        internal Color Color;
        internal Vector2 OriginalInset;
    }

    /**
     * Gameplay HUD around the dice tray: header, target and resources, score preview, actions, encounter rule,
     * footer, and stage intro. Build phases run in order because the HUD interleaves with the tray, rewards,
     * and ending under the shared portrait.
     */
    public sealed class SpiritGameplayHud : MonoBehaviour
    {
        private RectTransform portrait;
        private RectTransform diceTrayFrame;
        private RectTransform rerollActionLabel;
        private RectTransform playActionLabel;
        private Vector2 rerollLabelMin;
        private Vector2 rerollLabelMax;
        private RectTransform encounterRuleSigilRoot;
        private Image ruleSpiritIcon;
        private Graphic ruleSpiritSigil;
        private RectTransform ruleSpiritSigilRoot;
        private SpiritModifierSigilGraphic modifierActivationSigil;
        private SpiritModifierCategoryBadge modifierActivationCategory;

        public Text EncounterTitle { get; private set; }
        public Text StageLabel { get; private set; }
        public Button MenuButton { get; private set; }
        public RectTransform TargetPanel { get; private set; }
        public Text TargetLabel { get; private set; }
        public Text TargetCaption { get; private set; }
        public RectTransform HandsPanel { get; private set; }
        public Text HandsLabel { get; private set; }
        public RectTransform RerollsPanel { get; private set; }
        public Text RerollsLabel { get; private set; }
        public Text ResourcesLabel { get; private set; }
        public RectTransform PlayingPanel { get; private set; }
        public Text SelectionLabel { get; private set; }
        public SpiritUiOrnament ModifierEmblem { get; private set; }
        public Image ModifierActivationIcon { get; private set; }
        public Text CategoryLabel { get; private set; }
        public Text ScoreDetail { get; private set; }
        public Text BaseScoreLabel { get; private set; }
        public Text MultiplierLabel { get; private set; }
        public Text HandScoreLabel { get; private set; }
        public Button RerollButton { get; private set; }
        public Button PlayButton { get; private set; }
        public CanvasGroup EncounterRuleGroup { get; private set; }
        public Text EncounterRuleLabel { get; private set; }
        public Image EncounterRuleIcon { get; private set; }
        public Graphic EncounterRuleSigil { get; private set; }
        public CanvasGroup RuleSpiritSweep { get; private set; }
        public RectTransform StageIntroPanel { get; private set; }
        public Text StageIntroTitle { get; private set; }
        public Text StageIntroDetail { get; private set; }
        public Button CharmsButton { get; private set; }
        public Button HandRanksButton { get; private set; }
        public Text CharmCount { get; private set; }
        public Text StatusLabel { get; private set; }
        public Button InvestigationButton { get; private set; }
        public long DisplayedBaseScore { get; private set; }
        public long DisplayedMultiplier { get; private set; }

        internal SpiritCounterHighlight BaseScoreHighlight { get; private set; }
        internal SpiritCounterHighlight MultiplierHighlight { get; private set; }
        internal SpiritCounterHighlight RerollsHighlight { get; private set; }
        internal SpiritCounterHighlight HandsHighlight { get; private set; }

        public void BuildHeader(RectTransform parent, Action toggleMenu)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (toggleMenu == null) throw new ArgumentNullException(nameof(toggleMenu));
            if (portrait != null) throw new InvalidOperationException("The gameplay HUD has already been built.");

            portrait = parent;
            EncounterTitle = SpiritUi.Heading("EncounterTitle", portrait, "", 21,
                new Vector2(.12f, .963f), new Vector2(.88f, .999f), TextAnchor.MiddleCenter);
            var headerRule = SpiritUi.Rect("HeaderRule", portrait, new Vector2(.16f, .925f), new Vector2(.84f, .995f));
            SpiritUi.Decorate(headerRule);
            StageLabel = SpiritUi.Heading("StageLabel", portrait, "", 16, new Vector2(.15f, .939f), new Vector2(.85f, .963f),
                TextAnchor.MiddleCenter);
            MenuButton = SpiritUi.Button("MenuButton", portrait, "Menu", new Vector2(.80f, .932f), new Vector2(.99f, .978f),
                () => toggleMenu());
            var menuText = MenuButton.GetComponentInChildren<Text>();
            menuText.fontSize = menuText.resizeTextMaxSize = 15;
            menuText.resizeTextMinSize = 12;
            var target = SpiritUi.Panel("TargetPanel", portrait, new Vector2(.035f, .544f), new Vector2(.965f, .63f),
                SpiritUi.GetTheme(transform).panelColor, 0, true);
            TargetPanel = target.rectTransform;
            TargetLabel = SpiritUi.Heading("TargetLabel", target.transform, "", 56, new Vector2(.2f, .29f), new Vector2(.8f, .99f),
                TextAnchor.MiddleCenter);
            TargetCaption = SpiritUi.Heading("ToClearLabel", target.transform, "TO CLEAR", 17,
                new Vector2(.23f, .1f), new Vector2(.77f, .35f), TextAnchor.MiddleCenter);
            TargetCaption.rectTransform.offsetMin = new Vector2(12, 0);
            TargetCaption.rectTransform.offsetMax = new Vector2(-12, 0);
            HandsPanel = SpiritUi.Panel("HandsPanel", portrait, new Vector2(.12f, .506f), new Vector2(.495f, .542f),
                SpiritUi.GetTheme(transform).panelColor, 0, true).rectTransform;
            HandsLabel = SpiritUi.Heading("HandsRemainingLabel", HandsPanel, "", 20, Vector2.zero, Vector2.one,
                TextAnchor.MiddleCenter);
            RerollsPanel = SpiritUi.Panel("RerollsPanel", portrait, new Vector2(.505f, .506f), new Vector2(.88f, .542f),
                SpiritUi.GetTheme(transform).panelColor, 0, true).rectTransform;
            RerollsLabel = SpiritUi.Heading("RerollsRemainingLabel", RerollsPanel, "", 20, Vector2.zero, Vector2.one,
                TextAnchor.MiddleCenter);
            ResourcesLabel = SpiritUi.Label("ResourcesLabel", portrait, "", 18, new Vector2(.04f, .503f), new Vector2(.96f, .54f),
                TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).muted);
        }

        /** The playing panel also hosts the tray, which the view builds before the rest of the playing HUD. */
        public RectTransform BuildPlayingPanel()
        {
            PlayingPanel = SpiritUi.Rect("PlayingPanel", portrait, new Vector2(0, .065f), new Vector2(1, .55f));
            return PlayingPanel;
        }

        public void BuildPlayingHud(Action reroll, Action play)
        {
            if (reroll == null) throw new ArgumentNullException(nameof(reroll));
            if (play == null) throw new ArgumentNullException(nameof(play));

            SelectionLabel = SpiritUi.Label("SelectionLabel", PlayingPanel, "Tap dice to select for reroll", 17,
                new Vector2(.115f, .438f), new Vector2(.97f, .525f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).muted);
            SelectionLabel.resizeTextForBestFit = true;
            SelectionLabel.resizeTextMinSize = 14;
            SelectionLabel.resizeTextMaxSize = 17;
            SelectionLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            SelectionLabel.rectTransform.offsetMin = new Vector2(0, 0);
            SelectionLabel.rectTransform.offsetMax = new Vector2(0, 0);
            ModifierEmblem = SpiritUi.Rect("ModifierEmblem", PlayingPanel, new Vector2(.045f, .456f), new Vector2(.105f, .511f))
                .gameObject.AddComponent<SpiritUiOrnament>();
            ModifierEmblem.kind = "charm";
            ModifierEmblem.color = SpiritUi.GetTheme(transform).amber;
            ModifierEmblem.raycastTarget = false;
            ModifierActivationIcon = SpiritCharmVisuals.MakeIcon("ModifierActivationIcon", ModifierEmblem.transform,
                new Vector2(.5f, .5f), 32);
            modifierActivationSigil = SpiritCharmVisuals.MakeSigil("ModifierActivationSigil", ModifierActivationIcon);
            modifierActivationCategory = SpiritModifierCategoryBadge.Create("ModifierActivationCategory", ModifierActivationIcon);
            ModifierEmblem.gameObject.SetActive(false);
            var score = SpiritUi.Panel("ScorePreview", PlayingPanel, new Vector2(.045f, .19f), new Vector2(.955f, .435f),
                SpiritUi.GetTheme(transform).panelColor, 0, true);
            CategoryLabel = SpiritUi.Heading("HandCategory", score.transform, "", 23,
                new Vector2(.05f, .70f), new Vector2(.95f, .91f), TextAnchor.MiddleCenter);
            SpiritUi.Decorate(score.transform);
            var divider = score.transform.Find("GoldDivider").GetComponent<RectTransform>();
            divider.anchorMin = new Vector2(.07f, .65f);
            divider.anchorMax = new Vector2(.93f, .67f);
            var formula = SpiritUi.Rect("ScoreFormula", score.transform, new Vector2(.04f, .20f), new Vector2(.96f, .65f));
            BaseScoreLabel = BuildScoreCounter("BaseScoreLabel", "BASE", formula, 0, .32f);
            MultiplierLabel = BuildScoreCounter("MultiplierLabel", "MULT", formula, .34f, .66f);
            HandScoreLabel = BuildScoreCounter("HandScore", "TOTAL", formula, .68f, 1);
            BaseScoreHighlight = MakeCounterHighlight("BaseScoreCharmIcon", BaseScoreLabel, true);
            MultiplierHighlight = MakeCounterHighlight("MultiplierCharmIcon", MultiplierLabel, true);
            RerollsHighlight = MakeCounterHighlight("RerollsCharmIcon", RerollsLabel, false);
            HandsHighlight = MakeCounterHighlight("HandsCharmIcon", HandsLabel, false);
            ScoreDetail = SpiritUi.Label("ScoreBreakdown", score.transform, "ROLL BONUSES BANK HERE", 14,
                new Vector2(.04f, .06f), new Vector2(.96f, .20f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).muted);
            ScoreDetail.rectTransform.offsetMin = ScoreDetail.rectTransform.offsetMax = Vector2.zero;
            ScoreDetail.resizeTextForBestFit = true;
            ScoreDetail.resizeTextMinSize = 11;
            ScoreDetail.resizeTextMaxSize = 14;
            SetScoreValues(0, 0);
            HandScoreLabel.text = "0";
            RerollButton = SpiritUi.Button("RerollButton", PlayingPanel, "Reroll", new Vector2(.035f, 0), new Vector2(.49f, .188f),
                () => reroll());
            PlayButton = SpiritUi.Button("PlayButton", PlayingPanel, "Play Hand", new Vector2(.51f, 0), new Vector2(.965f, .188f),
                () => play(), true);
            foreach (var button in new[] { RerollButton, PlayButton })
            {
                var text = button.GetComponentInChildren<Text>();
                text.fontSize = text.resizeTextMaxSize = 25;
                text.resizeTextMinSize = 18;
            }
        }

        /** The rule-spirit sweep crosses the dice tray; without a tray, as in composition authoring, it is omitted. */
        public void BuildEncounterRule(RectTransform trayFrame)
        {
            diceTrayFrame = trayFrame;
            rerollActionLabel = RerollButton.GetComponentInChildren<Text>().rectTransform;
            playActionLabel = PlayButton.GetComponentInChildren<Text>().rectTransform;
            rerollLabelMin = rerollActionLabel.anchorMin;
            rerollLabelMax = rerollActionLabel.anchorMax;
            var favor = SpiritUi.Rect("EncounterRuleFavor", RerollButton.transform,
                new Vector2(.08f, .06f), new Vector2(.94f, .35f));
            EncounterRuleGroup = favor.gameObject.AddComponent<CanvasGroup>();
            EncounterRuleGroup.interactable = false;
            EncounterRuleGroup.blocksRaycasts = false;
            var icon = SpiritUi.Rect("EncounterRuleIcon", favor, new Vector2(0, .5f), new Vector2(0, .5f));
            icon.sizeDelta = new Vector2(18, 18);
            icon.anchoredPosition = new Vector2(9, 0);
            EncounterRuleIcon = icon.gameObject.AddComponent<Image>();
            EncounterRuleIcon.preserveAspect = true;
            EncounterRuleIcon.color = SpiritUi.GetTheme(transform).amber;
            EncounterRuleIcon.raycastTarget = false;
            encounterRuleSigilRoot = icon;
            EncounterRuleLabel = SpiritUi.Label("EncounterRuleLabel", favor, "", 14,
                Vector2.zero, Vector2.one, TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).amber);
            EncounterRuleLabel.rectTransform.offsetMin = new Vector2(21, 0);
            EncounterRuleLabel.rectTransform.offsetMax = Vector2.zero;
            EncounterRuleLabel.resizeTextForBestFit = true;
            EncounterRuleLabel.resizeTextMinSize = 12;
            EncounterRuleLabel.resizeTextMaxSize = 14;
            favor.gameObject.SetActive(false);

            if (diceTrayFrame != null) BuildRuleSpiritSweep();
            RerollButton.GetComponent<SpiritUiButtonVisual>().CaptureContent();
        }

        public void BuildStageIntro()
        {
            StageIntroPanel = SpiritUi.Panel("StageIntroPanel", portrait, new Vector2(.06f, .37f), new Vector2(.94f, .57f),
                SpiritUi.GetTheme(transform).panelColor, 0, true).rectTransform;
            StageIntroTitle = SpiritUi.Heading("StageIntroTitle", StageIntroPanel, "", 28,
                new Vector2(.04f, .55f), new Vector2(.96f, .94f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).amber);
            StageIntroDetail = SpiritUi.Label("StageIntroDetail", StageIntroPanel, "", 19,
                new Vector2(.04f, .08f), new Vector2(.96f, .55f), TextAnchor.MiddleCenter);
            StageIntroPanel.gameObject.SetActive(false);
        }

        public void BuildFooter(Action showCharms, Action showHandRanks, Action returnToInvestigation)
        {
            if (showCharms == null) throw new ArgumentNullException(nameof(showCharms));
            if (showHandRanks == null) throw new ArgumentNullException(nameof(showHandRanks));
            if (returnToInvestigation == null) throw new ArgumentNullException(nameof(returnToInvestigation));

            CharmsButton = SpiritUi.Button("CharmsButton", portrait, "CHARMS (0)", new Vector2(.035f, .007f), new Vector2(.49f, .065f),
                () => showCharms());
            HandRanksButton = SpiritUi.Button("HandRanksButton", portrait, "HAND RANKS", new Vector2(.51f, .007f), new Vector2(.965f, .065f),
                () => showHandRanks());
            foreach (var button in new[] { CharmsButton, HandRanksButton })
            {
                var text = button.GetComponentInChildren<Text>();
                text.fontSize = text.resizeTextMaxSize = 19;
                text.resizeTextMinSize = 14;
            }
            CharmCount = SpiritUi.Label("CharmCount", portrait, "", 15, new Vector2(.35f, .005f), new Vector2(.52f, .065f),
                TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).muted);
            CharmCount.gameObject.SetActive(false);
            StatusLabel = SpiritUi.Label("StatusMessage", portrait, "", 14, new Vector2(.12f, .892f), new Vector2(.88f, .927f),
                TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).muted);
            InvestigationButton = SpiritUi.Button("ReturnToInvestigationButton", portrait, "Clues",
                new Vector2(.01f, .932f), new Vector2(.20f, .978f), () => returnToInvestigation());
            var investigationText = InvestigationButton.GetComponentInChildren<Text>();
            investigationText.fontSize = investigationText.resizeTextMaxSize = 15;
            investigationText.resizeTextMinSize = 12;
            InvestigationButton.gameObject.SetActive(false);
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
            if (EncounterRuleSigil != null)
            {
                EncounterRuleSigil.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(EncounterRuleSigil.gameObject);
                else DestroyImmediate(EncounterRuleSigil.gameObject);
            }
            EncounterRuleSigil = presentation?.CreateSigil("EncounterRuleSigil", encounterRuleSigilRoot);
        }

        public void ConfigureHostSigils(SpiritHostPresentationAsset presentation)
        {
            ConfigureEncounterRuleSigil(presentation);
            if (ruleSpiritSigil != null) { ruleSpiritSigil.gameObject.SetActive(false); Destroy(ruleSpiritSigil.gameObject); }
            ruleSpiritSigil = presentation?.CreateSigil("RuleSpiritSigil", ruleSpiritSigilRoot);
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
            var ruleButton = onPlay ? PlayButton : RerollButton;
            EncounterRuleGroup.transform.SetParent(ruleButton.transform, false);
            EncounterRuleGroup.gameObject.SetActive(visible);
            rerollActionLabel.anchorMin = visible && !onPlay ? new Vector2(rerollLabelMin.x, .36f) : rerollLabelMin;
            rerollActionLabel.anchorMax = rerollLabelMax;
            playActionLabel.anchorMin = visible && onPlay ? new Vector2(rerollLabelMin.x, .36f) : rerollLabelMin;
            playActionLabel.anchorMax = rerollLabelMax;
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

        public void ShowActivationIcon(ModifierDefinition definition)
        {
            ModifierActivationIcon.sprite = SpiritModifierIcons.GetSprite(definition);
            Color color = SpiritModifierIcons.GetEffectColor(definition);
            ModifierActivationIcon.color = modifierActivationSigil.color = color;
            ModifierActivationIcon.gameObject.SetActive(ModifierActivationIcon.sprite != null);
            modifierActivationSigil.Seed = SpiritModifierIcons.GetSigilSeed(definition);
            modifierActivationSigil.gameObject.SetActive(ModifierActivationIcon.sprite == null);
            modifierActivationCategory.Render(definition);
            ModifierEmblem.enabled = false;
            ModifierEmblem.gameObject.SetActive(true);
        }

        public void ResetTransientVisuals()
        {
            if (RuleSpiritSweep != null) RuleSpiritSweep.gameObject.SetActive(false);
            if (StageIntroPanel != null) StageIntroPanel.gameObject.SetActive(false);
            if (ModifierEmblem == null) return;
            ModifierEmblem.gameObject.SetActive(false);
            ModifierEmblem.enabled = true;
            ModifierActivationIcon.sprite = null;
            ModifierActivationIcon.gameObject.SetActive(false);
            ModifierActivationIcon.rectTransform.localScale = Vector3.one;
            ModifierActivationIcon.color = Color.white;
            modifierActivationSigil.gameObject.SetActive(false);
            modifierActivationCategory.Render(null);
            foreach (var highlight in new[] { BaseScoreHighlight, MultiplierHighlight, RerollsHighlight, HandsHighlight })
                ResetCounterHighlight(highlight);
            SelectionLabel.text = "";
            SelectionLabel.color = SpiritUi.GetTheme(transform).muted;
            SelectionLabel.rectTransform.localScale = Vector3.one;
            ScoreDetail.color = SpiritUi.GetTheme(transform).muted;
            ScoreDetail.rectTransform.localScale = Vector3.one;
            foreach (var label in new[] { BaseScoreLabel, MultiplierLabel })
            {
                label.color = SpiritUi.GetTheme(transform).cream;
                label.rectTransform.localScale = Vector3.one;
            }
            HandScoreLabel.color = SpiritUi.GetTheme(transform).cream;
            HandScoreLabel.rectTransform.localScale = Vector3.one;
            TargetLabel.color = SpiritUi.GetTheme(transform).cream;
            TargetLabel.rectTransform.localScale = Vector3.one;
        }

        internal static void AddCounterHighlight(List<SpiritCounterHighlight> highlights, SpiritCounterHighlight highlight,
            ModifierDefinition definition)
        {
            if (highlights.Contains(highlight)) return;
            var sprite = SpiritModifierIcons.GetSprite(definition);
            highlight.Color = SpiritModifierIcons.GetEffectColor(definition);
            highlight.Icon.sprite = sprite;
            highlight.Icon.color = highlight.Color;
            highlight.Icon.gameObject.SetActive(sprite != null);
            if (sprite != null) highlight.Caption.rectTransform.offsetMin = highlight.OriginalInset + new Vector2(22, 0);
            highlights.Add(highlight);
        }

        internal IEnumerator AnimateScore(long beforeBase, long beforeMultiplier, long afterBase, long afterMultiplier, float duration,
            List<SpiritCounterHighlight> highlights = null)
        {
            float elapsed = 0;
            while (elapsed < duration)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / duration);
                SetScoreValues(Interpolate(beforeBase, afterBase, progress), Interpolate(beforeMultiplier, afterMultiplier, progress));
                if (beforeBase != afterBase)
                {
                    BaseScoreLabel.color = SpiritUi.GetTheme(transform).amber;
                    BaseScoreLabel.rectTransform.localScale = Vector3.one * (1 + .07f * Mathf.Sin(progress * Mathf.PI));
                }
                if (beforeMultiplier != afterMultiplier)
                {
                    MultiplierLabel.color = SpiritUi.GetTheme(transform).amber;
                    MultiplierLabel.rectTransform.localScale = Vector3.one * (1 + .07f * Mathf.Sin(progress * Mathf.PI));
                }
                if (highlights != null)
                    foreach (var highlight in highlights)
                    {
                        float pulse = Mathf.Sin(progress * Mathf.PI);
                        highlight.Label.color = Color.Lerp(SpiritUi.GetTheme(transform).cream, highlight.Color, pulse);
                        highlight.Label.rectTransform.localScale = Vector3.one * (1 + .07f * pulse);
                        highlight.Icon.rectTransform.localScale = Vector3.one * (1 + .1f * pulse);
                    }
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            SetScoreValues(afterBase, afterMultiplier);
            foreach (var label in new[] { BaseScoreLabel, MultiplierLabel })
            {
                label.color = SpiritUi.GetTheme(transform).cream;
                label.rectTransform.localScale = Vector3.one;
            }
            if (highlights != null)
                foreach (var highlight in highlights) ResetCounterHighlight(highlight);
        }

        public IEnumerator AnimateTotal(long score, float duration)
        {
            float elapsed = 0;
            while (elapsed < duration)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / duration);
                HandScoreLabel.text = DiagnosticText.Number(Interpolate(0, score, progress));
                HandScoreLabel.color = SpiritUi.GetTheme(transform).amber;
                HandScoreLabel.rectTransform.localScale = Vector3.one * (1 + .14f * Mathf.Sin(progress * Mathf.PI));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            HandScoreLabel.text = DiagnosticText.Number(score);
            HandScoreLabel.color = SpiritUi.GetTheme(transform).cream;
            HandScoreLabel.rectTransform.localScale = Vector3.one;
        }

        public IEnumerator AnimateThreshold(long before, long after, float duration)
        {
            float elapsed = 0;
            TargetLabel.text = DiagnosticText.Number(before);
            while (elapsed < duration)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / duration);
                TargetLabel.text = DiagnosticText.Number(Interpolate(before, after, progress));
                TargetLabel.color = SpiritUi.GetTheme(transform).amber;
                TargetLabel.rectTransform.localScale = Vector3.one * (1 + .06f * Mathf.Sin(progress * Mathf.PI));
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            TargetLabel.text = DiagnosticText.Number(after);
            TargetLabel.color = SpiritUi.GetTheme(transform).cream;
            TargetLabel.rectTransform.localScale = Vector3.one;
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

        private void BuildRuleSpiritSweep()
        {
            var sweep = SpiritUi.Rect("RuleSpiritSweep", diceTrayFrame, new Vector2(0, .5f), new Vector2(0, .5f));
            sweep.sizeDelta = new Vector2(72, 72);
            RuleSpiritSweep = sweep.gameObject.AddComponent<CanvasGroup>();
            RuleSpiritSweep.interactable = false;
            RuleSpiritSweep.blocksRaycasts = false;
            ruleSpiritIcon = SpiritUi.Full("RuleSpiritArtwork", sweep).gameObject.AddComponent<Image>();
            ruleSpiritIcon.preserveAspect = true;
            ruleSpiritIcon.color = new Color(.35f, .9f, .9f, .75f);
            ruleSpiritIcon.raycastTarget = false;
            ruleSpiritSigilRoot = sweep;
            sweep.gameObject.SetActive(false);
        }

        private static Text BuildScoreCounter(string name, string caption, Transform parent, float left, float right)
        {
            var cell = SpiritUi.Rect(name + "Cell", parent, new Vector2(left, 0), new Vector2(right, 1));
            var heading = SpiritUi.Heading(name + "Caption", cell, caption, 16,
                new Vector2(0, .65f), Vector2.one, TextAnchor.MiddleCenter, SpiritUi.GetTheme(parent).muted);
            heading.rectTransform.offsetMin = heading.rectTransform.offsetMax = Vector2.zero;
            var value = SpiritUi.Heading(name, cell, "0", 36,
                Vector2.zero, new Vector2(1, .68f), TextAnchor.MiddleCenter);
            value.rectTransform.offsetMin = value.rectTransform.offsetMax = Vector2.zero;
            value.resizeTextMinSize = 22;
            return value;
        }

        private static SpiritCounterHighlight MakeCounterHighlight(string name, Text label, bool scoreCounter)
        {
            var caption = scoreCounter ? label.transform.parent.Find(label.name + "Caption").GetComponent<Text>() : label;
            var icon = SpiritCharmVisuals.MakeIcon(name, label.transform.parent,
                scoreCounter ? new Vector2(.17f, .7f) : new Vector2(0, .5f), 22);
            if (!scoreCounter) icon.rectTransform.anchoredPosition = new Vector2(19, 0);
            return new SpiritCounterHighlight
            {
                Label = label,
                Caption = caption,
                Icon = icon,
                OriginalInset = caption.rectTransform.offsetMin
            };
        }

        private static void ResetCounterHighlight(SpiritCounterHighlight highlight)
        {
            highlight.Label.color = SpiritUi.GetTheme(highlight.Label.transform).cream;
            highlight.Label.rectTransform.localScale = Vector3.one;
            highlight.Caption.rectTransform.offsetMin = highlight.OriginalInset;
            highlight.Icon.gameObject.SetActive(false);
            highlight.Icon.sprite = null;
            highlight.Icon.rectTransform.localScale = Vector3.one;
            highlight.Icon.color = Color.white;
        }

        private static long Interpolate(long before, long after, float progress)
            => (long)((decimal)before + ((decimal)after - before) * (decimal)progress);
    }
}
