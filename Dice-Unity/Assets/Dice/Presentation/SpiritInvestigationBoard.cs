using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Investigation board listing available journeys as clue cards, with resume, load, and Collection actions. */
    public sealed class SpiritInvestigationBoard : MonoBehaviour
    {
        private readonly List<GameObject> clueCards = new List<GameObject>();
        private string renderedProgress;

        public RectTransform Root { get; private set; }
        public Text Title { get; private set; }
        public Text Description { get; private set; }
        public ScrollRect Scroll { get; private set; }
        public RectTransform Content { get; private set; }
        public Text Status { get; private set; }
        public Button ResumeButton { get; private set; }
        public Button LoadButton { get; private set; }
        public Button CollectionButton { get; private set; }

        public void Build(RectTransform parent, Action resume, Action load, Action openCollection)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (resume == null) throw new ArgumentNullException(nameof(resume));
            if (load == null) throw new ArgumentNullException(nameof(load));
            if (openCollection == null) throw new ArgumentNullException(nameof(openCollection));
            if (Root != null) throw new InvalidOperationException("The investigation board has already been built.");

            Root = SpiritUi.Panel("InvestigationBoard", parent, Vector2.zero, Vector2.one,
                new Color32(17, 28, 23, 255)).rectTransform;
            Title = SpiritUi.Heading("InvestigationTitle", Root, "INVESTIGATION", 31,
                new Vector2(.04f, .917f), new Vector2(.96f, .982f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).amber);
            Description = SpiritUi.Label("InvestigationSubtitle", Root, "", 18,
                new Vector2(.06f, .85f), new Vector2(.94f, .92f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).muted);
            Scroll = SpiritUi.Scroll("InvestigationClues", Root, new Vector2(.015f, .24f), new Vector2(.985f, .845f), out var content);
            Content = content;
            var layout = Content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Status = SpiritUi.Label("InvestigationStatus", Root, "", 17,
                new Vector2(.04f, .172f), new Vector2(.96f, .24f), TextAnchor.MiddleCenter, SpiritUi.GetTheme(transform).muted);
            ResumeButton = SpiritUi.Button("ResumeEncounterButton", Root, "Continue at the table",
                new Vector2(.035f, .09f), new Vector2(.965f, .165f), () => resume(), true);
            LoadButton = SpiritUi.Button("LoadJourneyButton", Root, "Load saved encounter",
                new Vector2(.035f, .012f), new Vector2(.58f, .087f), () => load());
            CollectionButton = SpiritUi.Button("InvestigationCollectionButton", Root, "Collection",
                new Vector2(.58f, .012f), new Vector2(.965f, .087f), () => openCollection());
        }

        /** Clue cards rebuild when forced or when availability, visits, highlights, or completions change. */
        public void Render(IEncounterJourneyControls controls, ISpiritEncounterControls encounter, bool rebuild,
            bool collectionAvailable)
        {
            if (controls == null) throw new ArgumentNullException(nameof(controls));
            Title.text = controls.JourneyCatalog != null ? controls.JourneyCatalog.Title : "INVESTIGATION";
            Description.text = controls.JourneyCatalog != null ? controls.JourneyCatalog.Description : "";
            string progress = Progress(controls, encounter);
            if (rebuild || renderedProgress != progress) BuildClues(controls, encounter);
            renderedProgress = progress;
            Status.text = controls.JourneyStatus;
            ResumeButton.gameObject.SetActive(controls.CanResume);
            LoadButton.interactable = encounter != null && !encounter.IsBusy;
            CollectionButton.interactable = collectionAvailable;
        }

        private void BuildClues(IEncounterJourneyControls controls, ISpiritEncounterControls encounter)
        {
            foreach (var card in clueCards) { card.SetActive(false); Destroy(card); }
            clueCards.Clear();
            if (controls.JourneyCatalog == null) return;
            int index = 0;
            RectTransform firstHighlight = null;
            var theme = SpiritUi.GetTheme(transform);
            foreach (var journey in controls.JourneyCatalog.Entries)
            {
                if (journey == null || !controls.IsJourneyAvailable(journey.Id)) continue;
                var selected = journey;
                var cardImage = SpiritUi.Panel("ClueButton" + index++, Content, Vector2.zero, Vector2.one,
                    theme.panelColor, 0, true);
                var button = cardImage.gameObject.AddComponent<SpiritUiButtonVisual>();
                button.targetGraphic = cardImage;
                bool highlighted = false;
                foreach (string id in controls.HighlightedEncounterIds)
                    if (id == journey.Id) { highlighted = true; break; }
                if (highlighted)
                {
                    var outline = cardImage.gameObject.AddComponent<Outline>();
                    outline.effectColor = theme.amber;
                    outline.effectDistance = new Vector2(3, -3);
                    var marker = SpiritUi.Label("ClueNewLead", button.transform, "NEW LEAD", 14,
                        new Vector2(.055f, .94f), new Vector2(.945f, 1), TextAnchor.MiddleCenter, theme.amber);
                    marker.rectTransform.offsetMin = new Vector2(12, 0);
                    marker.rectTransform.offsetMax = new Vector2(-12, 0);
                    if (firstHighlight == null) firstHighlight = cardImage.rectTransform;
                }
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = colors.selectedColor = theme.highlightedTint;
                colors.pressedColor = theme.pressedTint;
                colors.disabledColor = theme.disabledTint;
                colors.fadeDuration = .12f;
                button.colors = colors;
                button.onClick.AddListener(() => controls.SelectEncounter(selected.Id));
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
                var clueTitle = SpiritUi.Heading("ClueTitle", button.transform, journey.LocationTitle, 26,
                    new Vector2(.055f, .33f), new Vector2(.945f, .45f), TextAnchor.MiddleCenter, theme.amber);
                clueTitle.resizeTextForBestFit = true;
                clueTitle.resizeTextMinSize = 21;
                clueTitle.resizeTextMaxSize = 26;
                var lead = SpiritUi.Label("ClueLead", button.transform, journey.LeadText, 19,
                    new Vector2(.055f, .09f), new Vector2(.945f, .32f), TextAnchor.MiddleCenter);
                lead.resizeTextForBestFit = true;
                lead.resizeTextMinSize = 17;
                lead.resizeTextMaxSize = 19;
                bool visited = false;
                foreach (string visitedId in controls.VisitedEncounterIds)
                    if (visitedId == journey.EncounterId) { visited = true; break; }
                bool completed = false;
                if (journey.IsStoryOnly)
                {
                    foreach (string completedId in controls.CompletedStoryIds)
                        if (completedId == journey.Id) { completed = true; break; }
                }
                else if (encounter is ISpiritProgressionControls progression)
                    foreach (string completedId in progression.CompletedEncounterIds)
                        if (completedId == journey.EncounterId) { completed = true; break; }
                string state = journey.IsStoryOnly ? completed ? "READ AGAIN" : "FOLLOW THIS LEAD" :
                    visited ? "VISITED - RETURN TO THE TABLE" : "FOLLOW THIS LEAD";
                SpiritUi.Label("ClueState", button.transform, state, 14,
                    new Vector2(.055f, .027f), new Vector2(.945f, .09f), TextAnchor.MiddleCenter, theme.muted);
                button.CaptureContent();
                if (completed && !string.IsNullOrWhiteSpace(journey.CompletionClueText))
                {
                    var discovered = SpiritUi.Panel("CompletionClue", Content, Vector2.zero, Vector2.one,
                        theme.panelColor, 0, true).rectTransform;
                    clueCards.Add(discovered.gameObject);
                    discovered.gameObject.AddComponent<LayoutElement>().preferredHeight = 180;
                    SpiritUi.Heading("CompletionClueTitle", discovered, journey.CompletionClueTitle, 24,
                        new Vector2(.035f, .67f), new Vector2(.965f, .95f), TextAnchor.MiddleCenter, theme.amber);
                    var discoveredText = SpiritUi.Label("CompletionClueText", discovered, journey.CompletionClueText, 19,
                        new Vector2(.045f, .06f), new Vector2(.955f, .66f), TextAnchor.MiddleCenter);
                    discoveredText.resizeTextForBestFit = true;
                    discoveredText.resizeTextMinSize = 17;
                    discoveredText.resizeTextMaxSize = 19;
                }
            }
            if (firstHighlight != null)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
                float scrollableHeight = Content.rect.height - Scroll.viewport.rect.height;
                var cardTop = Content.InverseTransformPoint(firstHighlight.TransformPoint(
                    new Vector3(0, firstHighlight.rect.yMax, 0)));
                Scroll.StopMovement();
                Scroll.verticalNormalizedPosition = scrollableHeight > 0 ?
                    1 - Mathf.Clamp01(-cardTop.y / scrollableHeight) : 1;
            }
        }

        private static string Progress(IEncounterJourneyControls controls, ISpiritEncounterControls encounter)
        {
            var available = new List<string>();
            if (controls.JourneyCatalog != null)
                foreach (var journey in controls.JourneyCatalog.Entries)
                    if (journey != null && controls.IsJourneyAvailable(journey.Id)) available.Add(journey.Id);
            return string.Join("\n", available) + "\n\n" + string.Join("\n", controls.VisitedEncounterIds) +
                "\n\n" + string.Join("\n", controls.CompletedStoryIds) + "\n\n" +
                string.Join("\n", controls.HighlightedEncounterIds) + "\n\n" +
                (encounter is ISpiritProgressionControls progression ?
                    string.Join("\n", progression.CompletedEncounterIds) : "");
        }
    }
}
