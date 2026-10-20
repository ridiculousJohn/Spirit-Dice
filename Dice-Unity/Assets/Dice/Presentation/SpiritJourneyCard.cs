using System;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Binds journey or chapter data to an authored card hierarchy. */
    public sealed class SpiritJourneyCard : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private RawImage artwork;
        [SerializeField] private AspectRatioFitter artworkAspect;
        [SerializeField] private Text title;
        [SerializeField] private Text description;
        [SerializeField] private Text number;
        [SerializeField] private Text state;
        [SerializeField] private Outline highlight;
        [SerializeField] private GameObject newLead;
        [SerializeField] private Color stateColor = Color.white;
        [SerializeField] private Color highlightedStateColor = Color.white;
        private Action select;

        public Button Button => button;
        public Text Title => title;
        public Text Description => description;
        public Text State => state;

        public void Bind(Texture texture, string titleText, string descriptionText, string numberText,
            string stateText, bool highlighted, bool showNewLead, bool available, Action onSelect)
        {
            if (button == null || artwork == null || artworkAspect == null || title == null ||
                description == null || number == null || state == null || highlight == null || newLead == null)
                throw new InvalidOperationException("The journey card requires authored UI references.");
            select = onSelect;
            button.onClick.RemoveListener(Select);
            button.onClick.AddListener(Select);
            button.interactable = available;
            artwork.texture = texture;
            artwork.color = texture != null ? Color.white : Color.clear;
            artworkAspect.aspectRatio = texture != null ? (float)texture.width / texture.height : 1;
            title.text = titleText;
            description.text = descriptionText;
            number.text = numberText;
            state.text = stateText;
            state.color = highlighted ? highlightedStateColor : stateColor;
            highlight.enabled = highlighted;
            newLead.SetActive(showNewLead);
        }

        private void Select() => select?.Invoke();
    }
}
