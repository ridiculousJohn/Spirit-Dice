using System;
using System.Collections;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Presents authored investigation and arrival beats without creating encounter mechanics. */
    public sealed class EncounterJourneyView : MonoBehaviour
    {
        private IEncounterJourneyControls controls;
        private ISpiritEncounterControls encounterControls;
        private RectTransform sequence;
        private RectTransform artwork;
        private RawImage background;
        private RawImage foreground;
        private RawImage leftPanel;
        private RawImage rightPanel;
        private CanvasGroup illustrationGroup;
        private Text title;
        private Text body;
        private Text rules;
        private RectTransform narrative;
        private RectTransform ruleDivider;
        private Text ruleHeading;
        private Text ruleScope;
        private readonly List<InvitationRuleView> invitationRules = new List<InvitationRuleView>();
        private int invitationRuleCount;
        private float invitationWidth;
        private RectTransform charmDetail;
        private RectTransform charmDivider;
        private RectTransform charmIconRoot;
        private Image charmIcon;
        private SpiritModifierSigilGraphic charmSigil;
        private Text charmName;
        private Text charmEffect;
        private Text charmCondition;
        private float charmWidth;
        private Text journeyStatus;
        private Button advanceButton;
        private Button acceptButton;
        private Button skipButton;
        private Button returnButton;
        private Action showCollection;
        private Action<SpiritPresentationState, float> hostState;
        private CanvasGroup narrativeGroup;
        private SpiritEncounterAudio audio;
        private EncounterJourneyAsset renderedJourney;
        private EncounterJourneyCatalogAsset renderedCatalog;
        private EncounterJourneyPhase renderedPhase;
        private bool hasRendered;
        private EncounterJourneyBeat activeBeat;
        private SceneLightingAsset previousSceneLighting;
        private SceneLightingState sceneLightingState = new SceneLightingState();
        private SceneLightingState previousSceneLightingState = new SceneLightingState();
        private SceneLightingMaterial sceneLightingMaterial;
        private SceneLightingMaterial previousSceneLightingMaterial;

        private sealed class InvitationRuleView
        {
            public Text Name;
            public Text Effect;
            public RectTransform IconRoot;
            public Graphic Icon;
        }

        public bool IsAnimating { get; private set; }
        public int VisibleBeatIndex { get; private set; } = -1;
        public SpiritInvestigationBoard Board { get; private set; }
        public SpiritEncounterCharmsScreen EncounterCharms { get; private set; }

        public void Build(SpiritEncounterAudio encounterAudio,
            Action<SpiritPresentationState, float> presentHostState = null)
        {
            hostState = presentHostState;
            audio = encounterAudio;
            var root = (RectTransform)transform;
            Board = gameObject.AddComponent<SpiritInvestigationBoard>();
            Board.Build(root, () => controls.ResumeEncounter(), () => encounterControls?.Load(), () => showCollection?.Invoke());

            sequence = SpiritUi.Full("JourneySequence", root);
            var blocker = sequence.gameObject.AddComponent<Image>();
            blocker.color = Color.clear;
            artwork = SpiritUi.Full("JourneyArtwork", sequence);
            illustrationGroup = artwork.gameObject.AddComponent<CanvasGroup>();
            illustrationGroup.blocksRaycasts = false;
            background = SpiritUi.Full("JourneyBackground", artwork).gameObject.AddComponent<RawImage>();
            background.raycastTarget = false;
            var clip = SpiritUi.Full("JourneyForegroundClip", artwork);
            clip.gameObject.AddComponent<RectMask2D>();
            foreground = SpiritUi.Full("JourneyForeground", clip).gameObject.AddComponent<RawImage>();
            foreground.raycastTarget = false;
            var foregroundAspect = foreground.gameObject.AddComponent<AspectRatioFitter>();
            foregroundAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            leftPanel = SpiritUi.Rect("JourneyLeftPanel", artwork, Vector2.zero, new Vector2(.5f, 1))
                .gameObject.AddComponent<RawImage>();
            rightPanel = SpiritUi.Rect("JourneyRightPanel", artwork, new Vector2(.5f, 0), Vector2.one)
                .gameObject.AddComponent<RawImage>();
            leftPanel.raycastTarget = rightPanel.raycastTarget = false;
            var narrativeImage = SpiritUi.Panel("JourneyNarrative", sequence, new Vector2(.035f, .145f), new Vector2(.965f, .57f),
                SpiritUi.GetTheme(transform).panelColor, 0, true);
            narrative = narrativeImage.rectTransform;
            narrativeGroup = narrative.gameObject.AddComponent<CanvasGroup>();
            title = SpiritUi.Heading("JourneyTitle", narrative, "", 29,
                new Vector2(.035f, .79f), new Vector2(.965f, .98f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).amber);
            body = SpiritUi.Label("JourneyBody", narrative, "", 23,
                new Vector2(.04f, .36f), new Vector2(.96f, .79f), TextAnchor.MiddleCenter);
            body.resizeTextForBestFit = true;
            body.resizeTextMinSize = 19;
            body.resizeTextMaxSize = 23;
            rules = SpiritUi.Label("JourneyRuleText", narrative, "", 19,
                new Vector2(.04f, .045f), new Vector2(.96f, .345f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).amber);
            rules.resizeTextForBestFit = true;
            rules.resizeTextMinSize = 17;
            rules.resizeTextMaxSize = 19;
            var theme = SpiritUi.GetTheme(transform);
            var dividerColor = theme.muted;
            dividerColor.a = .25f;
            ruleDivider = SpiritUi.Panel("JourneyRuleDivider", narrative, Vector2.zero, Vector2.one,
                dividerColor).rectTransform;
            ruleDivider.GetComponent<Image>().raycastTarget = false;
            ruleHeading = SpiritUi.Label("JourneyRuleHeading", narrative, "ENCOUNTER RULE", 14,
                Vector2.zero, Vector2.one, TextAnchor.UpperLeft, theme.muted);
            ruleScope = SpiritUi.Label("JourneyRuleScope", narrative, "Active throughout this encounter.", 16,
                Vector2.zero, Vector2.one, TextAnchor.UpperLeft, theme.muted);
            invitationRules.Add(CreateInvitationRule(rules, ""));
            SetInvitationRulesVisible(false);
            BuildCharmDetail();
            advanceButton = SpiritUi.Button("JourneyAdvanceButton", sequence, "Continue",
                new Vector2(.035f, .065f), new Vector2(.965f, .145f), () => controls.AdvanceJourney(), true);
            acceptButton = SpiritUi.Button("JourneyAcceptButton", sequence, "Accept",
                new Vector2(.035f, .065f), new Vector2(.965f, .145f), ShowEncounterCharms, true);
            returnButton = SpiritUi.Button("JourneyReturnButton", sequence, "Back to clues",
                new Vector2(.035f, .003f), new Vector2(.505f, .064f), () => controls.ReturnToInvestigation());
            skipButton = SpiritUi.Button("JourneySkipButton", sequence, "Skip to invitation",
                new Vector2(.505f, .003f), new Vector2(.965f, .064f), () => controls.SkipIntro());
            journeyStatus = SpiritUi.Label("JourneyStatus", sequence, "", 16,
                new Vector2(.05f, .895f), new Vector2(.95f, .945f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).muted);
            EncounterCharms = gameObject.AddComponent<SpiritEncounterCharmsScreen>();
            EncounterCharms.Build(sequence, () => controls.AcceptEncounter());
            root.gameObject.SetActive(false);
        }

        public void SetCollectionAction(Action openCollection)
        {
            showCollection = openCollection;
        }

        private void BuildCharmDetail()
        {
            var theme = SpiritUi.GetTheme(transform);
            charmDetail = SpiritUi.Rect("JourneyCharmDetail", narrative, Vector2.zero, Vector2.one);
            var dividerColor = theme.muted;
            dividerColor.a = .25f;
            charmDivider = SpiritUi.Panel("JourneyCharmDivider", charmDetail, Vector2.zero, Vector2.one,
                dividerColor).rectTransform;
            charmDivider.GetComponent<Image>().raycastTarget = false;
            charmIconRoot = SpiritUi.Rect("JourneyCharmIconRoot", charmDetail, Vector2.zero, Vector2.one);
            charmIcon = SpiritUi.Full("JourneyCharmIcon", charmIconRoot).gameObject.AddComponent<Image>();
            charmIcon.preserveAspect = true;
            charmIcon.raycastTarget = false;
            charmSigil = SpiritUi.Full("JourneyCharmSigil", charmIconRoot).gameObject.AddComponent<SpiritModifierSigilGraphic>();
            charmSigil.raycastTarget = false;
            charmName = SpiritUi.Heading("JourneyCharmName", charmDetail, "", 24,
                Vector2.zero, Vector2.one, TextAnchor.UpperLeft, theme.amber);
            charmName.resizeTextForBestFit = false;
            charmEffect = SpiritUi.Label("JourneyCharmEffect", charmDetail, "", 21,
                Vector2.zero, Vector2.one, TextAnchor.UpperLeft);
            charmCondition = SpiritUi.Label("JourneyCharmCondition", charmDetail, "", 19,
                Vector2.zero, Vector2.one, TextAnchor.UpperLeft);
            charmEffect.supportRichText = charmCondition.supportRichText = true;
            charmDetail.gameObject.SetActive(false);
        }

        private void ShowEncounterCharms()
        {
            var journey = controls?.SelectedJourney;
            if (journey == null || journey.IsStoryOnly || encounterControls?.Catalog == null) return;
            var encounter = encounterControls.Catalog.GetEncounter(journey.EncounterId);
            if (encounter.EncounterModifierIds.Count == 0)
            {
                controls.AcceptEncounter();
                return;
            }
            // Back to invitation returns to this beat, so it must rest at its authored final pose.
            FinishAnimation();
            EncounterCharms.Show(encounterControls.Catalog, encounter.EncounterModifierIds);
        }

        public void Bind(IEncounterJourneyControls journey, ISpiritEncounterControls encounter)
        {
            CancelAnimation();
            ClearSceneLighting();
            controls = journey;
            encounterControls = encounter;
            EncounterCharms.Hide();
            hasRendered = false;
        }

        public void Refresh()
        {
            if (controls == null) return;
            var phase = controls.JourneyPhase;
            var journey = controls.SelectedJourney;
            int index = controls.BeatIndex;
            bool changed = !hasRendered || renderedPhase != phase || renderedJourney != journey ||
                renderedCatalog != controls.JourneyCatalog || VisibleBeatIndex != index;
            gameObject.SetActive(phase != EncounterJourneyPhase.Playing);
            if (phase == EncounterJourneyPhase.Playing)
            {
                CancelAnimation();
                ClearSceneLighting();
                hasRendered = false;
                return;
            }
            Board.Root.gameObject.SetActive(phase == EncounterJourneyPhase.Investigation);
            sequence.gameObject.SetActive(phase == EncounterJourneyPhase.Sequence);
            if (phase == EncounterJourneyPhase.Investigation)
            {
                CancelAnimation();
                ClearSceneLighting();
                Board.Render(controls, encounterControls, changed, showCollection != null);
            }
            else if (journey != null && index >= 0 && index < journey.Beats.Count)
            {
                journeyStatus.text = controls.JourneyStatus;
                if (changed) ShowBeat(journey, index);
            }
            renderedJourney = journey;
            renderedCatalog = controls.JourneyCatalog;
            renderedPhase = phase;
            VisibleBeatIndex = index;
            hasRendered = true;
        }

        private void ShowBeat(EncounterJourneyAsset journey, int index)
        {
            EncounterCharms.Hide();
            bool continuingJourney = hasRendered && renderedJourney == journey && renderedPhase == EncounterJourneyPhase.Sequence;
            Texture2D previous = continuingJourney ? background.texture as Texture2D : null;
            Rect previousUv = background.uvRect;
            var previousLighting = continuingJourney ? activeBeat?.SceneLighting : null;
            CancelAnimation();
            var beat = journey.Beats[index];
            bool parting = beat.Transition == EncounterJourneyTransition.PartingPanels && previous != null;
            background.material = null;
            if (parting)
            {
                previousSceneLighting = previousLighting;
                previousSceneLightingMaterial = sceneLightingMaterial;
                var state = previousSceneLightingState;
                previousSceneLightingState = sceneLightingState;
                sceneLightingState = state;
                sceneLightingMaterial = null;
            }
            else
            {
                sceneLightingMaterial?.Dispose();
                sceneLightingMaterial = null;
            }
            sceneLightingState.Sample(null, Time.unscaledTimeAsDouble);
            activeBeat = beat;
            title.text = beat.Title;
            body.text = beat.Text;
            bool invitation = !journey.IsStoryOnly && beat.Kind == EncounterJourneyBeatKind.Invitation;
            ConfigureCharm(beat);
            ConfigureInvitation(journey.TablePresentation, invitation);
            if (charmDetail.gameObject.activeSelf && invitationRuleCount == 0) LayoutCharmNarrative();
            advanceButton.gameObject.SetActive(!invitation);
            acceptButton.gameObject.SetActive(invitation);
            SpiritUi.SetButtonLabel(invitation ? acceptButton : advanceButton,
                string.IsNullOrWhiteSpace(beat.ContinueLabel) ? invitation ? "Accept" : "Continue" : beat.ContinueLabel);
            skipButton.gameObject.SetActive(controls.CanSkipIntro && !invitation);
            var backgroundTexture = beat.SceneLighting != null ? beat.SceneLighting.BaseTexture : beat.Background;
            background.texture = backgroundTexture;
            background.color = backgroundTexture != null ? Color.white : Color.clear;
            foreground.texture = beat.Foreground;
            foreground.GetComponent<AspectRatioFitter>().aspectRatio = beat.Foreground != null ?
                (float)beat.Foreground.width / beat.Foreground.height : 1;
            foreground.color = beat.Foreground != null ? beat.ForegroundTint : Color.clear;
            var clip = (RectTransform)foreground.transform.parent;
            clip.anchorMin = beat.ForegroundRect.min;
            clip.anchorMax = beat.ForegroundRect.max;
            clip.offsetMin = clip.offsetMax = Vector2.zero;
            leftPanel.gameObject.SetActive(parting);
            rightPanel.gameObject.SetActive(parting);
            leftPanel.texture = rightPanel.texture = previous;
            leftPanel.material = rightPanel.material = previousSceneLightingMaterial?.Material;
            leftPanel.uvRect = new Rect(previousUv.x, previousUv.y, previousUv.width * .5f, previousUv.height);
            rightPanel.uvRect = new Rect(previousUv.x + previousUv.width * .5f, previousUv.y,
                previousUv.width * .5f, previousUv.height);
            if (beat.AudioCue != null) audio?.PlayJourneyCue(beat.AudioCue);
            StartCoroutine(AnimateBeat(journey, index, parting));
        }

        private InvitationRuleView CreateInvitationRule(Text effect, string suffix)
        {
            var theme = SpiritUi.GetTheme(transform);
            var row = new InvitationRuleView
            {
                Name = SpiritUi.Heading("JourneyRuleName" + suffix, narrative, "", 24,
                    Vector2.zero, Vector2.one, TextAnchor.UpperLeft, theme.amber),
                Effect = effect != null ? effect : SpiritUi.Label("JourneyRuleText" + suffix, narrative, "", 21,
                    Vector2.zero, Vector2.one, TextAnchor.UpperLeft),
                IconRoot = SpiritUi.Rect("JourneyRuleIconRoot" + suffix, narrative, Vector2.zero, Vector2.one)
            };
            row.Name.resizeTextForBestFit = false;
            row.Effect.resizeTextForBestFit = false;
            row.Effect.supportRichText = true;
            row.Effect.fontSize = 21;
            row.Effect.alignment = TextAnchor.UpperLeft;
            row.Effect.color = theme.cream;
            return row;
        }

        private void ConfigureCharm(EncounterJourneyBeat beat)
        {
            bool visible = beat.Charm != null && encounterControls?.Catalog != null;
            charmDetail.gameObject.SetActive(visible);
            if (!visible) return;
            var definition = encounterControls.Catalog.GetModifier(beat.Charm.Id);
            var theme = SpiritUi.GetTheme(transform);
            string name = SpiritModifierPresentation.GetName(definition);
            body.supportRichText = true;
            body.text = body.text.Replace("{charm}", "<b><color=#" + ColorUtility.ToHtmlStringRGB(theme.amber) +
                ">" + SpiritModifierFamilyRow.GetFamilyName(definition) + "</color></b>");
            charmName.text = name;
            charmEffect.text = "<b>" + SpiritModifierPresentation.GetEffectText(definition) + "</b>";
            charmEffect.color = SpiritModifierIcons.GetEffectColor(definition);
            charmCondition.text = SpiritRewardCard.GetModifierDescription(definition, theme.amber,
                theme.GetModifierTriggerColor(definition.Phase));
            charmIcon.sprite = SpiritModifierIcons.GetSprite(definition);
            charmIcon.color = charmSigil.color = SpiritModifierIcons.GetEffectColor(definition);
            charmIcon.gameObject.SetActive(charmIcon.sprite != null);
            charmSigil.Seed = SpiritModifierIcons.GetSigilSeed(definition);
            charmSigil.gameObject.SetActive(charmIcon.sprite == null);
        }

        private void ConfigureInvitation(EncounterTablePresentationAsset table, bool invitation)
        {
            invitationRuleCount = 0;
            rules.text = "";
            if (invitation && table != null)
            {
                foreach (var rule in table.RulePresentations)
                {
                    if (rule == null) continue;
                    int index = invitationRuleCount++;
                    string suffix = index == 0 ? "" : (index + 1).ToString();
                    if (index == invitationRules.Count)
                        invitationRules.Add(CreateInvitationRule(null, suffix));
                    var row = invitationRules[index];
                    row.Name.text = rule.DisplayName;
                    row.Effect.text = rule.Description;
                    if (row.Icon != null)
                    {
                        row.Icon.gameObject.SetActive(false);
                        if (Application.isPlaying) Destroy(row.Icon.gameObject);
                        else DestroyImmediate(row.Icon.gameObject);
                    }
                    row.Icon = null;
                    if (rule.Icon != null)
                    {
                        var image = SpiritUi.Full("JourneyRuleIcon" + suffix, row.IconRoot).gameObject.AddComponent<Image>();
                        image.sprite = rule.Icon;
                        image.preserveAspect = true;
                        image.raycastTarget = false;
                        row.Icon = image;
                    }
                    else if (table.HostPresentation != null)
                        row.Icon = table.HostPresentation.CreateSigil("JourneyRuleIcon" + suffix, row.IconRoot);
                }
            }

            bool showRules = invitationRuleCount > 0;
            ruleHeading.text = invitationRuleCount > 1 ? "ENCOUNTER RULES" : "ENCOUNTER RULE";
            SetInvitationRulesVisible(showRules);
            title.gameObject.SetActive(!showRules);
            title.resizeTextForBestFit = !charmDetail.gameObject.activeSelf;
            var theme = SpiritUi.GetTheme(transform);
            body.fontSize = 23;
            body.fontStyle = FontStyle.Normal;
            body.color = theme.cream;
            body.alignment = TextAnchor.MiddleCenter;
            body.resizeTextForBestFit = !showRules;
            if (showRules)
            {
                narrative.anchorMin = new Vector2(.035f, .145f);
                narrative.anchorMax = new Vector2(.965f, .145f);
                narrative.pivot = new Vector2(.5f, 0);
                narrative.anchoredPosition = Vector2.zero;
                LayoutInvitation();
            }
            else
            {
                narrative.pivot = new Vector2(.5f, .5f);
                narrative.anchorMin = new Vector2(.035f, .145f);
                narrative.anchorMax = new Vector2(.965f, .57f);
                narrative.offsetMin = narrative.offsetMax = Vector2.zero;
                title.rectTransform.anchorMin = new Vector2(.035f, .79f);
                title.rectTransform.anchorMax = new Vector2(.965f, .98f);
                title.rectTransform.pivot = new Vector2(.5f, .5f);
                title.rectTransform.offsetMin = new Vector2(12, 0);
                title.rectTransform.offsetMax = new Vector2(-12, 0);
                body.rectTransform.anchorMin = new Vector2(.04f, .065f);
                body.rectTransform.anchorMax = new Vector2(.96f, .79f);
                body.rectTransform.pivot = new Vector2(.5f, .5f);
                body.rectTransform.offsetMin = new Vector2(12, 4);
                body.rectTransform.offsetMax = new Vector2(-12, -4);
            }
        }

        private void SetInvitationRulesVisible(bool visible)
        {
            ruleDivider.gameObject.SetActive(visible);
            ruleHeading.gameObject.SetActive(visible);
            ruleScope.gameObject.SetActive(visible);
            for (int i = 0; i < invitationRules.Count; ++i)
            {
                bool active = visible && i < invitationRuleCount;
                var row = invitationRules[i];
                row.Name.gameObject.SetActive(active);
                row.Effect.gameObject.SetActive(active);
                row.IconRoot.gameObject.SetActive(active && row.Icon != null);
            }
        }

        private void LayoutInvitation()
        {
            invitationWidth = narrative.rect.width;
            const float padding = 24;
            float width = Mathf.Max(1, invitationWidth - padding * 2);
            float top = padding;
            float height = PreferredHeight(body, width);
            PlaceInvitationElement(body.rectTransform, padding, top, width, height);
            top += height + 18;
            PlaceInvitationElement(ruleDivider, padding, top, width, 1);
            top += 15;
            height = PreferredHeight(ruleHeading, width);
            PlaceInvitationElement(ruleHeading.rectTransform, padding, top, width, height);
            top += height + 10;
            for (int i = 0; i < invitationRuleCount; ++i)
            {
                var row = invitationRules[i];
                float iconSpace = row.Icon != null ? 54 : 0;
                height = Mathf.Max(row.Icon != null ? 44 : 0, PreferredHeight(row.Name, width - iconSpace));
                PlaceInvitationElement(row.IconRoot, padding, top + (height - 44) * .5f, 44, 44);
                PlaceInvitationElement(row.Name.rectTransform, padding + iconSpace, top, width - iconSpace, height);
                top += height + 10;
                height = PreferredHeight(row.Effect, width);
                PlaceInvitationElement(row.Effect.rectTransform, padding, top, width, height);
                top += height + (i + 1 < invitationRuleCount ? 22 : 14);
            }
            height = PreferredHeight(ruleScope, width);
            PlaceInvitationElement(ruleScope.rectTransform, padding, top, width, height);
            top += height;
            if (charmDetail.gameObject.activeSelf)
            {
                top += 18;
                top += LayoutCharmDetail(padding, top, width);
            }
            narrative.sizeDelta = new Vector2(0, top + padding);
        }

        private void LayoutCharmNarrative()
        {
            charmWidth = narrative.rect.width;
            narrative.anchorMin = new Vector2(.035f, .145f);
            narrative.anchorMax = new Vector2(.965f, .145f);
            narrative.pivot = new Vector2(.5f, 0);
            narrative.anchoredPosition = Vector2.zero;
            const float padding = 24;
            float width = Mathf.Max(1, charmWidth - padding * 2);
            float top = padding;
            float height = PreferredHeight(title, width);
            PlaceInvitationElement(title.rectTransform, padding, top, width, height);
            top += height + 12;
            body.resizeTextForBestFit = false;
            height = PreferredHeight(body, width);
            PlaceInvitationElement(body.rectTransform, padding, top, width, height);
            top += height + 18;
            top += LayoutCharmDetail(padding, top, width);
            narrative.sizeDelta = new Vector2(0, top + padding);
        }

        private float LayoutCharmDetail(float left, float top, float width)
        {
            PlaceInvitationElement(charmDivider, 0, 0, width, 1);
            const float contentTop = 16;
            const float iconSize = 50;
            const float iconSpace = 62;
            float nameHeight = PreferredHeight(charmName, width - iconSpace);
            float effectHeight = PreferredHeight(charmEffect, width - iconSpace);
            float headerHeight = Mathf.Max(iconSize, nameHeight + 4 + effectHeight);
            PlaceInvitationElement(charmIconRoot, 0, contentTop + (headerHeight - iconSize) * .5f, iconSize, iconSize);
            PlaceInvitationElement(charmName.rectTransform, iconSpace, contentTop, width - iconSpace, nameHeight);
            PlaceInvitationElement(charmEffect.rectTransform, iconSpace, contentTop + nameHeight + 4,
                width - iconSpace, effectHeight);
            float conditionTop = contentTop + headerHeight + 10;
            float conditionHeight = PreferredHeight(charmCondition, width);
            PlaceInvitationElement(charmCondition.rectTransform, 0, conditionTop, width, conditionHeight);
            float height = conditionTop + conditionHeight;
            PlaceInvitationElement(charmDetail, left, top, width, height);
            return height;
        }

        private static float PreferredHeight(Text text, float width)
        {
            var settings = text.GetGenerationSettings(new Vector2(Mathf.Max(1, width), 0));
            return Mathf.Ceil(text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit) + 2;
        }

        private static void PlaceInvitationElement(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private IEnumerator AnimateBeat(EncounterJourneyAsset journey, int index, bool parting)
        {
            var beat = journey.Beats[index];
            IsAnimating = true;
            SetButtons();
            float duration = Mathf.Max(0, beat.Duration);
            float age = 0;
            // Let Refresh finish recording this beat before an immediate continuation can refresh it.
            if (duration == 0 && beat.AutoAdvance)
            {
                ApplyMotion(beat, 0, parting);
                yield return null;
            }
            while (age < duration)
            {
                ApplyMotion(beat, duration > 0 ? age / duration : 1, parting);
                age += Time.unscaledDeltaTime;
                yield return null;
            }
            if (controls == null || controls.JourneyPhase != EncounterJourneyPhase.Sequence ||
                controls.SelectedJourney != journey || controls.BeatIndex != index || activeBeat != beat) yield break;
            ApplyMotion(beat, 1, parting);
            leftPanel.gameObject.SetActive(false);
            rightPanel.gameObject.SetActive(false);
            ClearPartingLighting();
            IsAnimating = false;
            if (beat.AutoAdvance && index + 1 < journey.Beats.Count)
            {
                controls.AdvanceJourney();
                yield break;
            }
            narrativeGroup.alpha = 1;
            SetButtons();
        }

        private void ApplyMotion(EncounterJourneyBeat beat, float value, bool parting)
        {
            float eased = Mathf.SmoothStep(0, 1, value);
            hostState?.Invoke(beat.HostState, value);
            narrativeGroup.alpha = beat.HideNarrativeDuringMotion ?
                beat.AutoAdvance ? 0 : Mathf.Clamp01((value - .9f) * 10f) : 1;
            background.rectTransform.anchoredPosition = Vector2.Scale(
                Vector2.Lerp(beat.BackgroundMotionFrom, beat.BackgroundMotionTo, eased), artwork.rect.size);
            background.rectTransform.localScale = Vector3.one * Mathf.Lerp(beat.BackgroundScaleFrom, beat.BackgroundScaleTo, eased);
            var rect = foreground.rectTransform;
            rect.anchoredPosition = Vector2.Scale(Vector2.Lerp(beat.MotionFrom, beat.MotionTo, eased), artwork.rect.size);
            rect.localScale = Vector3.one * Mathf.Lerp(beat.ScaleFrom, beat.ScaleTo, eased);
            illustrationGroup.alpha = 1;
            if (beat.Foreground != null)
            {
                Color tint = beat.ForegroundTint;
                tint.a *= parting || value >= 1 ? 1 : Mathf.Clamp01(value * beat.Duration / .25f);
                foreground.color = tint;
            }
            float distance = artwork.rect.width * .54f * eased;
            leftPanel.rectTransform.anchoredPosition = new Vector2(-distance, 0);
            rightPanel.rectTransform.anchoredPosition = new Vector2(distance, 0);
        }

        /** Continue and Accept stay available throughout; only skipping depends on journey state. */
        private void SetButtons()
        {
            skipButton.interactable = controls != null && controls.CanSkipIntro;
            returnButton.interactable = true;
        }

        /** Ends the current beat at its authored final pose, as its uninterrupted motion would, without auto-advancing. */
        private void FinishAnimation()
        {
            if (!IsAnimating || activeBeat == null) return;
            StopAllCoroutines();
            ApplyMotion(activeBeat, 1, false);
            leftPanel.gameObject.SetActive(false);
            rightPanel.gameObject.SetActive(false);
            ClearPartingLighting();
            IsAnimating = false;
            narrativeGroup.alpha = 1;
            SetButtons();
        }

        private void CancelAnimation()
        {
            StopAllCoroutines();
            IsAnimating = false;
            ClearPartingLighting();
            if (illustrationGroup == null) return;
            illustrationGroup.alpha = 1;
            leftPanel.rectTransform.anchoredPosition = rightPanel.rectTransform.anchoredPosition = Vector2.zero;
            leftPanel.gameObject.SetActive(false);
            rightPanel.gameObject.SetActive(false);
            foreground.rectTransform.anchoredPosition = Vector2.zero;
            foreground.rectTransform.localScale = Vector3.one;
            background.rectTransform.anchoredPosition = Vector2.zero;
            background.rectTransform.localScale = Vector3.one;
            narrativeGroup.alpha = 1;
            SetButtons();
        }

        private void OnDisable()
        {
            CancelAnimation();
            ClearSceneLighting();
            hasRendered = false;
        }

        private void LateUpdate()
        {
            if (invitationRuleCount > 0 && activeBeat?.Kind == EncounterJourneyBeatKind.Invitation &&
                !Mathf.Approximately(invitationWidth, narrative.rect.width)) LayoutInvitation();
            else if (charmDetail != null && charmDetail.gameObject.activeSelf && invitationRuleCount == 0 &&
                !Mathf.Approximately(charmWidth, narrative.rect.width)) LayoutCharmNarrative();
            double time = Time.unscaledTimeAsDouble;
            var asset = activeBeat?.SceneLighting;
            sceneLightingState.Sample(asset, time);
            if (asset != null)
            {
                if (sceneLightingMaterial == null || sceneLightingMaterial.Material.shader != asset.Shader)
                {
                    sceneLightingMaterial?.Dispose();
                    sceneLightingMaterial = new SceneLightingMaterial(asset.Shader);
                }
                sceneLightingMaterial.Apply(sceneLightingState);
            }
            else
            {
                sceneLightingMaterial?.Dispose();
                sceneLightingMaterial = null;
            }
            if (background != null)
            {
                var texture = asset != null ? asset.BaseTexture : activeBeat?.Background;
                background.texture = texture;
                background.color = texture != null ? Color.white : Color.clear;
                background.material = sceneLightingMaterial?.Material;
            }
            if (previousSceneLighting == null) return;
            previousSceneLightingState.Sample(previousSceneLighting, time);
            if (previousSceneLightingMaterial == null) previousSceneLightingMaterial = new SceneLightingMaterial(previousSceneLighting.Shader);
            previousSceneLightingMaterial.Apply(previousSceneLightingState);
            leftPanel.texture = rightPanel.texture = previousSceneLighting.BaseTexture;
            leftPanel.material = rightPanel.material = previousSceneLightingMaterial.Material;
        }

        private void ClearPartingLighting()
        {
            if (leftPanel != null) { leftPanel.material = null; leftPanel.texture = null; }
            if (rightPanel != null) { rightPanel.material = null; rightPanel.texture = null; }
            previousSceneLightingMaterial?.Dispose();
            previousSceneLightingMaterial = null;
            previousSceneLighting = null;
            previousSceneLightingState.Sample(null, Time.unscaledTimeAsDouble);
        }

        private void ClearSceneLighting()
        {
            if (background != null) background.material = null;
            sceneLightingMaterial?.Dispose();
            sceneLightingMaterial = null;
            sceneLightingState.Sample(null, Time.unscaledTimeAsDouble);
            activeBeat = null;
        }

        private void OnDestroy()
        {
            ClearPartingLighting();
            ClearSceneLighting();
        }
    }
}
