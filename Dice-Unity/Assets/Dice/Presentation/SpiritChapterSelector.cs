using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Presents authored chapters without exposing the leads belonging to locked chapters. */
    public sealed class SpiritChapterSelector : MonoBehaviour
    {
        [SerializeField] private SpiritJourneyCard chapterCardPrefab;

        private readonly List<GameObject> chapterCards = new List<GameObject>();

        [field: SerializeField] public RectTransform Root { get; private set; }
        [field: SerializeField] public ScrollRect Scroll { get; private set; }
        [field: SerializeField] public RectTransform Content { get; private set; }
        [field: SerializeField] public Button CloseButton { get; private set; }
        public bool IsOpen => Root != null && Root.gameObject.activeSelf;

        public void Build()
        {
            if (Root == null || Scroll == null || Content == null || CloseButton == null || chapterCardPrefab == null)
                throw new InvalidOperationException("The chapter selector requires authored UI references.");
            SpiritUi.RequireWording(transform);
            CloseButton.onClick.RemoveListener(Close);
            CloseButton.onClick.AddListener(Close);
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

            var wording = SpiritUi.GetWording(transform).chapters;
            for (int i = 0; i < catalog.Chapters.Count; ++i)
            {
                var chapter = catalog.Chapters[i];
                if (chapter == null) continue;
                var selected = chapter;
                var card = Instantiate(chapterCardPrefab, Content, false);
                card.name = "ChapterButton" + i;
                chapterCards.Add(card.gameObject);
                bool available = controls.IsChapterAvailable(chapter.Id);
                bool current = controls.SelectedChapter == chapter;
                bool completed = controls.IsChapterCompleted(chapter.Id);
                string state = !available ? wording.locked : current ? completed ? wording.currentCompleted : wording.current :
                    completed ? wording.completed : wording.available;
                card.Bind(chapter.Artwork, chapter.Title, "", wording.Number(i + 1), state,
                    current, false, available, () =>
                    {
                        Close();
                        controls.SelectChapter(selected.Id);
                    });
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
