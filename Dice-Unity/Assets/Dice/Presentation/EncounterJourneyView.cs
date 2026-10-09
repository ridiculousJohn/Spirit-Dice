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
        private Action arrival;
        private SpiritEncounterAudio audio;
        private EncounterJourneyAsset renderedJourney;
        private EncounterJourneyCatalogAsset renderedCatalog;
        private EncounterJourneyPhase renderedPhase;
        private bool hasRendered;

        public bool IsAnimating { get; private set; }
        public int VisibleBeatIndex { get; private set; } = -1;

        public void Build(Action hostArrival, SpiritEncounterAudio encounterAudio)
        {
            arrival = hostArrival;
            audio = encounterAudio;
            var root = (RectTransform)transform;
            board = SpiritUi.Panel("InvestigationBoard", root, Vector2.zero, Vector2.one,
                new Color32(17, 28, 23, 255)).rectTransform;
            investigationTitle = SpiritUi.Heading("InvestigationTitle", board, "INVESTIGATION", 31,
                new Vector2(.04f, .917f), new Vector2(.96f, .982f), TextAnchor.MiddleCenter, SpiritUi.Amber);
            investigationDescription = SpiritUi.Label("InvestigationSubtitle", board, "", 18,
                new Vector2(.06f, .85f), new Vector2(.94f, .92f), TextAnchor.MiddleCenter, SpiritUi.Muted);
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
                new Vector2(.04f, .172f), new Vector2(.96f, .24f), TextAnchor.MiddleCenter, SpiritUi.Muted);
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
                SpiritUi.PanelColor, 0, true);
            narrativeImage.type = Image.Type.Tiled;
            var narrative = narrativeImage.rectTransform;
            title = SpiritUi.Heading("JourneyTitle", narrative, "", 29,
                new Vector2(.035f, .79f), new Vector2(.965f, .98f), TextAnchor.MiddleCenter, SpiritUi.Amber);
            body = SpiritUi.Label("JourneyBody", narrative, "", 23,
                new Vector2(.04f, .36f), new Vector2(.96f, .79f), TextAnchor.MiddleCenter);
            body.resizeTextForBestFit = true;
            body.resizeTextMinSize = 19;
            body.resizeTextMaxSize = 23;
            rules = SpiritUi.Label("JourneyRuleText", narrative, "", 19,
                new Vector2(.04f, .045f), new Vector2(.96f, .345f), TextAnchor.MiddleCenter, SpiritUi.Amber);
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
                new Vector2(.05f, .895f), new Vector2(.95f, .945f), TextAnchor.MiddleCenter, SpiritUi.Muted);
            root.gameObject.SetActive(false);
        }

        public void Bind(IEncounterJourneyControls journey, ISpiritEncounterControls encounter)
        {
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
            if (phase == EncounterJourneyPhase.Playing) { CancelAnimation(); hasRendered = false; return; }
            board.gameObject.SetActive(phase == EncounterJourneyPhase.Investigation);
            sequence.gameObject.SetActive(phase == EncounterJourneyPhase.Sequence);
            if (phase == EncounterJourneyPhase.Investigation)
            {
                CancelAnimation();
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
                    SpiritUi.PanelColor, 0, true);
                cardImage.type = Image.Type.Tiled;
                var button = cardImage.gameObject.AddComponent<SpiritUiButtonVisual>();
                button.targetGraphic = cardImage;
                var theme = Resources.Load<SpiritUiTheme>("SpiritHouseTheme");
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
                    new Vector2(.055f, .33f), new Vector2(.945f, .45f), TextAnchor.MiddleCenter, SpiritUi.Amber);
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
                    new Vector2(.055f, .027f), new Vector2(.945f, .09f), TextAnchor.MiddleCenter, SpiritUi.Muted);
                button.CaptureContent();
                if (completed && !string.IsNullOrWhiteSpace(journey.CompletionClueText))
                {
                    var discovered = SpiritUi.Panel("CompletionClue", clueContent, Vector2.zero, Vector2.one,
                        SpiritUi.PanelColor, 0, true).rectTransform;
                    clueCards.Add(discovered.gameObject);
                    discovered.gameObject.AddComponent<LayoutElement>().preferredHeight = 180;
                    SpiritUi.Heading("CompletionClueTitle", discovered, journey.CompletionClueTitle, 24,
                        new Vector2(.035f, .67f), new Vector2(.965f, .95f), TextAnchor.MiddleCenter, SpiritUi.Amber);
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
            Texture2D previous = background.texture as Texture2D;
            CancelAnimation();
            var beat = journey.Beats[index];
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
            background.texture = beat.Background;
            background.color = beat.Background != null ? Color.white : Color.clear;
            foreground.texture = beat.Foreground;
            foreground.GetComponent<AspectRatioFitter>().aspectRatio = beat.Foreground != null ?
                (float)beat.Foreground.width / beat.Foreground.height : 1;
            foreground.color = beat.Foreground != null ? beat.ForegroundTint : Color.clear;
            var clip = (RectTransform)foreground.transform.parent;
            clip.anchorMin = beat.ForegroundRect.min;
            clip.anchorMax = beat.ForegroundRect.max;
            clip.offsetMin = clip.offsetMax = Vector2.zero;
            bool parting = beat.Transition == EncounterJourneyTransition.PartingPanels && previous != null;
            leftPanel.gameObject.SetActive(parting);
            rightPanel.gameObject.SetActive(parting);
            leftPanel.texture = rightPanel.texture = previous;
            leftPanel.uvRect = new Rect(0, 0, .5f, 1);
            rightPanel.uvRect = new Rect(.5f, 0, .5f, 1);
            if (beat.Kind == EncounterJourneyBeatKind.Arrival) arrival?.Invoke();
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
            IsAnimating = false;
            SetButtons(true);
        }

        private void ApplyMotion(EncounterJourneyBeat beat, float value, bool parting)
        {
            float eased = Mathf.SmoothStep(0, 1, value);
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
            if (illustrationGroup == null) return;
            illustrationGroup.alpha = 1;
            leftPanel.rectTransform.anchoredPosition = rightPanel.rectTransform.anchoredPosition = Vector2.zero;
            leftPanel.gameObject.SetActive(false);
            rightPanel.gameObject.SetActive(false);
            foreground.rectTransform.anchoredPosition = Vector2.zero;
            foreground.rectTransform.localScale = Vector3.one;
            SetButtons(true);
        }

        private void OnDisable()
        {
            CancelAnimation();
            hasRendered = false;
        }
    }
}
