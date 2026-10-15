using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Marks the triggering action and any action resources granted by a modifier. */
    public sealed class SpiritActionModifierCue : MonoBehaviour
    {
        private sealed class Cue
        {
            internal CanvasGroup Group;
            internal Image Artwork;
            internal SpiritModifierSigilGraphic Sigil;
            internal Graphic HostSigil;
            internal CanvasGroup Label;
            internal float LabelAlpha;
        }

        private Cue reroll;
        private Cue play;
        private Cue active;
        private Cue secondary;
        private float elapsed;
        private float duration;

        public ModifierPhase? ActivePhase { get; private set; }
        public Sprite ActiveSprite { get; private set; }
        public bool IsVisible => active != null && active.Group.gameObject.activeInHierarchy;
        public bool IsRerollVisible => reroll != null && reroll.Group.gameObject.activeInHierarchy;
        public bool IsPlayVisible => play != null && play.Group.gameObject.activeInHierarchy;

        public void Initialize(Button rerollButton, Button playButton)
        {
            reroll = Create(rerollButton, "RerollModifierCue");
            play = Create(playButton, "PlayHandModifierCue");
            Clear();
        }

        public void Show(ModifierDefinition definition, float displayDuration)
        {
            if (definition == null) { Clear(); return; }
            Show(SpiritModifierIcons.GetSprite(definition), definition.Phase, displayDuration,
                SpiritModifierIcons.GetSigilSeed(definition));
        }

        public void Show(Sprite sprite, ModifierPhase phase, float displayDuration, uint sigilSeed = 0)
            => Show(sprite, phase, displayDuration, sigilSeed, phase != ModifierPhase.PlayHand, phase == ModifierPhase.PlayHand);

        public void Show(ModifierDefinition definition, float displayDuration, bool showReroll, bool showPlay)
        {
            if (definition == null) { Clear(); return; }
            Show(SpiritModifierIcons.GetSprite(definition), definition.Phase, displayDuration,
                SpiritModifierIcons.GetSigilSeed(definition), showReroll, showPlay);
        }

        private void Show(Sprite sprite, ModifierPhase phase, float displayDuration, uint sigilSeed,
            bool showReroll, bool showPlay)
        {
            Clear();
            if (reroll == null || play == null || (!showReroll && !showPlay)) return;
            active = showReroll ? reroll : play;
            secondary = showReroll && showPlay ? play : null;
            ActivePhase = phase;
            ActiveSprite = sprite;
            Prepare(active, sprite, sigilSeed);
            if (secondary != null) Prepare(secondary, sprite, sigilSeed);
            duration = Mathf.Max(.01f, displayDuration);
            elapsed = 0;
        }

        private static void Prepare(Cue cue, Sprite sprite, uint sigilSeed)
        {
            cue.Artwork.sprite = sprite;
            cue.Artwork.gameObject.SetActive(sprite != null);
            cue.Sigil.Seed = sigilSeed;
            cue.Sigil.gameObject.SetActive(sprite == null);
            if (cue.HostSigil != null) cue.HostSigil.gameObject.SetActive(false);
            cue.Group.gameObject.SetActive(true);
            cue.Group.alpha = 0;
            cue.LabelAlpha = cue.Label.alpha;
            cue.Group.transform.localScale = Vector3.one;
        }

        public void ConfigureHost(SpiritHostPresentationAsset presentation)
        {
            Clear();
            foreach (var cue in new[] { reroll, play })
            {
                if (cue == null) continue;
                if (cue.HostSigil != null) { cue.HostSigil.gameObject.SetActive(false); Destroy(cue.HostSigil.gameObject); }
                cue.HostSigil = presentation?.CreateSigil("HostSigil", (RectTransform)cue.Group.transform);
                if (cue.HostSigil != null)
                {
                    Stretch(cue.HostSigil.rectTransform, 4);
                    cue.HostSigil.gameObject.SetActive(false);
                }
            }
        }

        public void ShowHost(Sprite sprite, ModifierPhase phase, float displayDuration)
        {
            Show(sprite, phase, displayDuration);
            if (active == null || sprite != null || active.HostSigil == null) return;
            active.Sigil.gameObject.SetActive(false);
            active.HostSigil.gameObject.SetActive(true);
        }

        public void Clear()
        {
            if (active != null && active.Label != null) active.Label.alpha = active.LabelAlpha;
            if (secondary != null && secondary.Label != null) secondary.Label.alpha = secondary.LabelAlpha;
            if (reroll != null) reroll.Group.gameObject.SetActive(false);
            if (play != null) play.Group.gameObject.SetActive(false);
            active = null;
            secondary = null;
            ActivePhase = null;
            ActiveSprite = null;
        }

        private void Update()
        {
            if (active == null) return;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float fadeIn = Mathf.SmoothStep(0, 1, elapsed / Mathf.Min(.1f, duration * .2f));
            float alpha = fadeIn * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.7f, 1, progress)));
            UpdateCue(active, alpha, progress);
            if (secondary != null) UpdateCue(secondary, alpha, progress);
            if (elapsed >= duration) Clear();
        }

        private static void UpdateCue(Cue cue, float alpha, float progress)
        {
            cue.Group.alpha = alpha;
            cue.Label.alpha = cue.LabelAlpha * (1 - alpha);
            cue.Group.transform.localScale = Vector3.one * (1 + .1f * Mathf.Sin(progress * Mathf.PI));
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
            sigil.color = SpiritUi.GetTheme(button.transform).amber;
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
