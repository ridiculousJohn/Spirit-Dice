using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Presents authored investigation and arrival beats without creating encounter mechanics. */
    public sealed class EncounterJourneyView : MonoBehaviour
    {
        private IEncounterJourneyControls controls;
        private ISpiritEncounterControls encounterControls;
        private RectTransform board;
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
        private Text journeyStatus;
        private Text boardStatus;
        private Text investigationTitle;
        private Text investigationDescription;
        private Button advanceButton;
        private Button acceptButton;
        private Button skipButton;
        private Button returnButton;
        private Button resumeButton;
        private Button loadButton;
        private RectTransform clueContent;
        private readonly List<GameObject> clueCards = new List<GameObject>();
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

        public bool IsAnimating { get; private set; }
        public int VisibleBeatIndex { get; private set; } = -1;

        public void Build(SpiritEncounterAudio encounterAudio,
            Action<SpiritPresentationState, float> presentHostState = null)
        {
            hostState = presentHostState;
            audio = encounterAudio;
            var root = (RectTransform)transform;
            board = SpiritUi.Panel("InvestigationBoard", root, Vector2.zero, Vector2.one,
                new Color32(17, 28, 23, 255)).rectTransform;
            investigationTitle = SpiritUi.Heading("InvestigationTitle", board, "INVESTIGATION", 31,
                new Vector2(.04f, .917f), new Vector2(.96f, .982f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).amber);
            investigationDescription = SpiritUi.Label("InvestigationSubtitle", board, "", 18,
                new Vector2(.06f, .85f), new Vector2(.94f, .92f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).muted);
            SpiritUi.Scroll("InvestigationClues", board, new Vector2(.015f, .24f), new Vector2(.985f, .845f), out clueContent);
            var layout = clueContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            clueContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            boardStatus = SpiritUi.Label("InvestigationStatus", board, "", 17,
                new Vector2(.04f, .172f), new Vector2(.96f, .24f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).muted);
            resumeButton = SpiritUi.Button("ResumeEncounterButton", board, "Continue at the table",
                new Vector2(.035f, .09f), new Vector2(.965f, .165f), () => controls.ResumeEncounter(), true);
            loadButton = SpiritUi.Button("LoadJourneyButton", board, "Load saved encounter",
                new Vector2(.035f, .012f), new Vector2(.965f, .087f), () => encounterControls?.Load());

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
            var narrative = narrativeImage.rectTransform;
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
            advanceButton = SpiritUi.Button("JourneyAdvanceButton", sequence, "Continue",
                new Vector2(.035f, .065f), new Vector2(.965f, .145f), () => controls.AdvanceJourney(), true);
            acceptButton = SpiritUi.Button("JourneyAcceptButton", sequence, "Accept",
                new Vector2(.035f, .065f), new Vector2(.965f, .145f), () => controls.AcceptEncounter(), true);
            returnButton = SpiritUi.Button("JourneyReturnButton", sequence, "Back to clues",
                new Vector2(.035f, .003f), new Vector2(.505f, .064f), () => controls.ReturnToInvestigation());
            skipButton = SpiritUi.Button("JourneySkipButton", sequence, "Skip to invitation",
                new Vector2(.505f, .003f), new Vector2(.965f, .064f), () => controls.SkipIntro());
            journeyStatus = SpiritUi.Label("JourneyStatus", sequence, "", 16,
                new Vector2(.05f, .895f), new Vector2(.95f, .945f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).muted);
            root.gameObject.SetActive(false);
        }

        public void Bind(IEncounterJourneyControls journey, ISpiritEncounterControls encounter)
        {
            CancelAnimation();
            ClearSceneLighting();
            controls = journey;
            encounterControls = encounter;
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
            board.gameObject.SetActive(phase == EncounterJourneyPhase.Investigation);
            sequence.gameObject.SetActive(phase == EncounterJourneyPhase.Sequence);
            if (phase == EncounterJourneyPhase.Investigation)
            {
                CancelAnimation();
                ClearSceneLighting();
                investigationTitle.text = controls.JourneyCatalog != null ? controls.JourneyCatalog.Title : "INVESTIGATION";
                investigationDescription.text = controls.JourneyCatalog != null ? controls.JourneyCatalog.Description : "";
                if (changed) BuildClues();
                boardStatus.text = controls.JourneyStatus;
                resumeButton.gameObject.SetActive(controls.CanResume);
                loadButton.interactable = encounterControls != null && !encounterControls.IsBusy;
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

        private void BuildClues()
        {
            foreach (var card in clueCards) { card.SetActive(false); Destroy(card); }
            clueCards.Clear();
            if (controls.JourneyCatalog == null) return;
            int index = 0;
            foreach (var journey in controls.JourneyCatalog.Entries)
            {
                if (journey == null) continue;
                var selected = journey;
                var cardImage = SpiritUi.Panel("ClueButton" + index++, clueContent, Vector2.zero, Vector2.one,
                    SpiritUi.GetTheme(transform).panelColor, 0, true);
                var button = cardImage.gameObject.AddComponent<SpiritUiButtonVisual>();
                button.targetGraphic = cardImage;
                var theme = SpiritUi.GetTheme(transform);
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = colors.selectedColor = theme != null ? theme.highlightedTint : new Color(1.10f, 1.08f, 1.01f, 1);
                colors.pressedColor = theme != null ? theme.pressedTint : new Color(.76f, .71f, .58f, 1);
                colors.disabledColor = theme != null ? theme.disabledTint : new Color(.46f, .47f, .43f, .88f);
                colors.fadeDuration = .12f;
                button.colors = colors;
                button.onClick.AddListener(() => controls.SelectEncounter(selected.EncounterId));
                clueCards.Add(button.gameObject);
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 430;
                var artworkFrame = SpiritUi.Rect("ClueArtworkFrame", button.transform, new Vector2(.055f, .46f), new Vector2(.945f, .94f));
                artworkFrame.gameObject.AddComponent<RectMask2D>();
                var image = SpiritUi.Full("ClueArtwork", artworkFrame).gameObject.AddComponent<RawImage>();
                image.texture = journey.CardArtwork;
                image.color = journey.CardArtwork != null ? Color.white : Color.clear;
                image.raycastTarget = false;
                var aspect = image.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                aspect.aspectRatio = journey.CardArtwork != null ? (float)journey.CardArtwork.width / journey.CardArtwork.height : 1;
                SpiritUi.Heading("ClueTitle", button.transform, journey.LocationTitle, 26,
                    new Vector2(.055f, .33f), new Vector2(.945f, .45f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).amber);
                var lead = SpiritUi.Label("ClueLead", button.transform, journey.LeadText, 19,
                    new Vector2(.055f, .09f), new Vector2(.945f, .32f), TextAnchor.MiddleCenter);
                lead.resizeTextForBestFit = true;
                lead.resizeTextMinSize = 17;
                lead.resizeTextMaxSize = 19;
                bool visited = false;
                foreach (string visitedId in controls.VisitedEncounterIds)
                    if (visitedId == journey.EncounterId) { visited = true; break; }
                bool completed = false;
                if (encounterControls is ISpiritProgressionControls progression)
                    foreach (string completedId in progression.CompletedEncounterIds)
                        if (completedId == journey.EncounterId) { completed = true; break; }
                SpiritUi.Label("ClueState", button.transform, visited ? "VISITED - RETURN TO THE TABLE" : "FOLLOW THIS LEAD", 14,
                    new Vector2(.055f, .027f), new Vector2(.945f, .09f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).muted);
                button.CaptureContent();
                if (completed && !string.IsNullOrWhiteSpace(journey.CompletionClueText))
                {
                    var discovered = SpiritUi.Panel("CompletionClue", clueContent, Vector2.zero, Vector2.one,
                        SpiritUi.GetTheme(transform).panelColor, 0, true).rectTransform;
                    clueCards.Add(discovered.gameObject);
                    discovered.gameObject.AddComponent<LayoutElement>().preferredHeight = 180;
                    SpiritUi.Heading("CompletionClueTitle", discovered, journey.CompletionClueTitle, 24,
                        new Vector2(.035f, .67f), new Vector2(.965f, .95f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).amber);
                    var discoveredText = SpiritUi.Label("CompletionClueText", discovered, journey.CompletionClueText, 19,
                        new Vector2(.045f, .06f), new Vector2(.955f, .66f), TextAnchor.MiddleCenter);
                    discoveredText.resizeTextForBestFit = true;
                    discoveredText.resizeTextMinSize = 17;
                    discoveredText.resizeTextMaxSize = 19;
                }
            }
        }

        private void ShowBeat(EncounterJourneyAsset journey, int index)
        {
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
            bool invitation = beat.Kind == EncounterJourneyBeatKind.Invitation;
            rules.text = "";
            if (invitation && journey.TablePresentation != null)
                foreach (var rule in journey.TablePresentation.RulePresentations)
                    if (rule != null) rules.text += (rules.text.Length == 0 ? "" : "\n") + rule.DisplayName + "\n" +
                        (string.IsNullOrWhiteSpace(rule.Description) ? rule.AvailableText : rule.Description);
            body.rectTransform.anchorMin = new Vector2(.04f, invitation && rules.text.Length != 0 ? .36f : .065f);
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
            StartCoroutine(AnimateBeat(beat, parting));
        }

        private IEnumerator AnimateBeat(EncounterJourneyBeat beat, bool parting)
        {
            IsAnimating = true;
            SetButtons(false);
            float duration = Mathf.Max(0, beat.Duration);
            float age = 0;
            while (age < duration)
            {
                ApplyMotion(beat, duration > 0 ? age / duration : 1, parting);
                age += Time.unscaledDeltaTime;
                yield return null;
            }
            ApplyMotion(beat, 1, parting);
            leftPanel.gameObject.SetActive(false);
            rightPanel.gameObject.SetActive(false);
            ClearPartingLighting();
            IsAnimating = false;
            narrativeGroup.alpha = 1;
            SetButtons(true);
        }

        private void ApplyMotion(EncounterJourneyBeat beat, float value, bool parting)
        {
            float eased = Mathf.SmoothStep(0, 1, value);
            hostState?.Invoke(beat.HostState, value);
            narrativeGroup.alpha = beat.HideNarrativeDuringMotion ? Mathf.Clamp01((value - .9f) * 10f) : 1;
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

        private void SetButtons(bool enabled)
        {
            advanceButton.interactable = acceptButton.interactable = enabled;
            skipButton.interactable = controls != null && controls.CanSkipIntro;
            returnButton.interactable = true;
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
            SetButtons(true);
        }

        private void OnDisable()
        {
            CancelAnimation();
            ClearSceneLighting();
            hasRendered = false;
        }

        private void LateUpdate()
        {
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
