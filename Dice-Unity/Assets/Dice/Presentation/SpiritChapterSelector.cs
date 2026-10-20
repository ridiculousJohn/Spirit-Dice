using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Presents authored chapters without exposing the leads belonging to locked chapters. */
    public sealed class SpiritChapterSelector : MonoBehaviour
    {
        private readonly List<GameObject> chapterCards = new List<GameObject>();

        public RectTransform Root { get; private set; }
        public ScrollRect Scroll { get; private set; }
        public RectTransform Content { get; private set; }
        public Button CloseButton { get; private set; }
        public bool IsOpen => Root != null && Root.gameObject.activeSelf;

        public void Build(RectTransform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (Root != null) throw new InvalidOperationException("The chapter selector has already been built.");

            var theme = SpiritUi.GetTheme(transform);
            Root = SpiritUi.Panel("ChapterSelectorOverlay", parent, Vector2.zero, Vector2.one,
                new Color(0, 0, 0, .78f)).rectTransform;
            var panel = SpiritUi.Panel("ChapterSelectorPanel", Root,
                new Vector2(.025f, .065f), new Vector2(.975f, .955f), theme.panelColor, 0, true).rectTransform;
            SpiritUi.Heading("ChapterSelectorTitle", panel, "CHAPTERS", 28,
                new Vector2(.035f, .875f), new Vector2(.7f, .975f), TextAnchor.MiddleLeft, theme.amber);
            CloseButton = SpiritUi.Button("CloseChapterSelectorButton", panel, "Close",
                new Vector2(.72f, .89f), new Vector2(.975f, .975f), Close, compact: true);
            SpiritUi.Label("ChapterSelectorSubtitle", panel, "Choose a chapter to investigate.", 18,
                new Vector2(.035f, .81f), new Vector2(.965f, .885f), TextAnchor.MiddleLeft, theme.muted);
            Scroll = SpiritUi.Scroll("ChapterSelectorScroll", panel,
                new Vector2(.015f, .105f), new Vector2(.985f, .81f), out var content);
            Content = content;
            var layout = Content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            SpiritUi.Label("ChapterSelectorFooter", panel, "Completed chapters remain available to revisit.", 16,
                new Vector2(.04f, .015f), new Vector2(.96f, .105f), TextAnchor.MiddleCenter, theme.muted);
            Close();
        }

        public void Show(IEncounterJourneyControls controls)
        {
            if (controls == null) throw new ArgumentNullException(nameof(controls));
            if (Root == null) throw new InvalidOperationException("Build the chapter selector before opening it.");
            foreach (var card in chapterCards) { card.SetActive(false); Destroy(card); }
            chapterCards.Clear();
            var catalog = controls.JourneyCatalog;
            if (catalog == null || catalog.Chapters == null || catalog.Chapters.Count == 0) return;

            var theme = SpiritUi.GetTheme(transform);
            for (int i = 0; i < catalog.Chapters.Count; ++i)
            {
                var chapter = catalog.Chapters[i];
                if (chapter == null) continue;
                var selected = chapter;
                var cardImage = SpiritUi.Panel("ChapterButton" + i, Content, Vector2.zero, Vector2.one,
                    theme.panelColor, 0, true);
                var button = cardImage.gameObject.AddComponent<SpiritUiButtonVisual>();
                button.targetGraphic = cardImage;
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = colors.selectedColor = theme.highlightedTint;
                colors.pressedColor = theme.pressedTint;
                colors.disabledColor = theme.disabledTint;
                colors.fadeDuration = .12f;
                button.colors = colors;
                button.onClick.AddListener(() =>
                {
                    Close();
                    controls.SelectChapter(selected.Id);
                });
                chapterCards.Add(button.gameObject);
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 280;
                var artworkFrame = SpiritUi.Rect("ChapterArtworkFrame", button.transform,
                    new Vector2(.055f, .43f), new Vector2(.945f, .94f));
                artworkFrame.gameObject.AddComponent<RectMask2D>();
                var image = SpiritUi.Full("ChapterArtwork", artworkFrame).gameObject.AddComponent<RawImage>();
                image.texture = chapter.Artwork;
                image.color = chapter.Artwork != null ? Color.white : Color.clear;
                image.raycastTarget = false;
                var aspect = image.gameObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                aspect.aspectRatio = chapter.Artwork != null ? (float)chapter.Artwork.width / chapter.Artwork.height : 1;
                SpiritUi.Label("ChapterNumber", button.transform, "CHAPTER " + (i + 1), 14,
                    new Vector2(.055f, .33f), new Vector2(.945f, .43f), TextAnchor.MiddleCenter, theme.muted);
                var title = SpiritUi.Heading("ChapterTitle", button.transform, chapter.Title, 25,
                    new Vector2(.055f, .13f), new Vector2(.945f, .33f), TextAnchor.MiddleCenter, theme.amber);
                title.resizeTextMinSize = 20;
                bool available = controls.IsChapterAvailable(chapter.Id);
                bool current = controls.SelectedChapter == chapter;
                bool completed = controls.IsChapterCompleted(chapter.Id);
                string state = !available ? "LOCKED" : current ? completed ? "CURRENT - COMPLETED" : "CURRENT" :
                    completed ? "COMPLETED" : "AVAILABLE";
                SpiritUi.Label("ChapterState", button.transform, state, 16,
                    new Vector2(.055f, .025f), new Vector2(.945f, .13f), TextAnchor.MiddleCenter,
                    current ? theme.amber : theme.muted);
                if (current)
                {
                    var outline = cardImage.gameObject.AddComponent<Outline>();
                    outline.effectColor = theme.amber;
                    outline.effectDistance = new Vector2(2, -2);
                }
                button.CaptureContent();
                button.interactable = available;
            }

            Root.SetAsLastSibling();
            Root.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
            Scroll.StopMovement();
            Scroll.verticalNormalizedPosition = 1;
        }

        public void Close()
        {
            if (Root != null) Root.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            Close();
        }
    }
}
