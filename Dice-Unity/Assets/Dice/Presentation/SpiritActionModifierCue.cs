using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Identifies the action responsible for the modifier currently being presented. */
    public sealed class SpiritActionModifierCue : MonoBehaviour
    {
        private sealed class Cue
        {
            internal CanvasGroup Group;
            internal Image Artwork;
            internal SpiritModifierSigilGraphic Sigil;
            internal CanvasGroup Label;
            internal float LabelAlpha;
        }

        private Cue reroll;
        private Cue play;
        private Cue active;
        private float elapsed;
        private float duration;

        public ModifierPhase? ActivePhase { get; private set; }
        public Sprite ActiveSprite { get; private set; }
        public bool IsVisible => active != null && active.Group.gameObject.activeInHierarchy;

        public void Initialize(Button rerollButton, Button playButton)
        {
            reroll = Create(rerollButton, "RerollModifierCue");
            play = Create(playButton, "PlayHandModifierCue");
            Clear();
        }

        public void Show(ModifierDefinition definition, float displayDuration)
        {
            Clear();
            if (definition == null || reroll == null || play == null) return;
            active = definition.Phase == ModifierPhase.PlayHand ? play : reroll;
            ActivePhase = definition.Phase;
            ActiveSprite = SpiritModifierIcons.GetSprite(definition);
            active.Artwork.sprite = ActiveSprite;
            active.Artwork.gameObject.SetActive(ActiveSprite != null);
            active.Sigil.Seed = SpiritModifierIcons.GetSigilSeed(definition);
            active.Sigil.gameObject.SetActive(ActiveSprite == null);
            active.Group.gameObject.SetActive(true);
            active.Group.alpha = 0;
            active.LabelAlpha = active.Label.alpha;
            active.Group.transform.localScale = Vector3.one;
            duration = Mathf.Max(.01f, displayDuration);
            elapsed = 0;
        }

        public void Clear()
        {
            if (active != null && active.Label != null) active.Label.alpha = active.LabelAlpha;
            if (reroll != null) reroll.Group.gameObject.SetActive(false);
            if (play != null) play.Group.gameObject.SetActive(false);
            active = null;
            ActivePhase = null;
            ActiveSprite = null;
        }

        private void Update()
        {
            if (active == null) return;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float fadeIn = Mathf.SmoothStep(0, 1, elapsed / Mathf.Min(.1f, duration * .2f));
            active.Group.alpha = fadeIn * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.7f, 1, progress)));
            active.Label.alpha = active.LabelAlpha * (1 - active.Group.alpha);
            active.Group.transform.localScale = Vector3.one * (1 + .1f * Mathf.Sin(progress * Mathf.PI));
            if (elapsed >= duration) Clear();
        }

        private void OnDisable() => Clear();

        private static Cue Create(Button button, string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)root.transform;
            rect.SetParent(button.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(44, 44);
            var group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            var shadow = new GameObject("Backing", typeof(RectTransform), typeof(Image));
            var backing = shadow.GetComponent<Image>();
            backing.transform.SetParent(rect, false);
            Stretch(backing.rectTransform, 0);
            backing.color = new Color(.07f, .09f, .07f, .94f);
            backing.raycastTarget = false;
            var border = shadow.AddComponent<Outline>();
            border.effectColor = new Color(.65f, .45f, .2f, .9f);
            border.effectDistance = new Vector2(1, -1);
            var artwork = new GameObject("Artwork", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            artwork.transform.SetParent(rect, false);
            Stretch(artwork.rectTransform, 3);
            artwork.preserveAspect = true;
            artwork.raycastTarget = false;
            var sigil = new GameObject("Sigil", typeof(RectTransform), typeof(SpiritModifierSigilGraphic))
                .GetComponent<SpiritModifierSigilGraphic>();
            sigil.transform.SetParent(rect, false);
            Stretch(sigil.rectTransform, 4);
            sigil.color = SpiritUi.Amber;
            sigil.raycastTarget = false;
            // A separate opacity group leaves the button's disabled and hover tints under Selectable's control.
            var label = button.GetComponentInChildren<Text>().gameObject;
            var labelGroup = label.GetComponent<CanvasGroup>();
            if (labelGroup == null) labelGroup = label.AddComponent<CanvasGroup>();
            return new Cue { Group = group, Artwork = artwork, Sigil = sigil, Label = labelGroup };
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
