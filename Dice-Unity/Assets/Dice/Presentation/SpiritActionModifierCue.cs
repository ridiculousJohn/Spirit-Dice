using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Marks the triggering action and any action resources granted by a modifier. */
    public sealed class SpiritActionModifierCue : MonoBehaviour
    {
        [System.Serializable]
        private sealed class Cue
        {
            public CanvasGroup Group;
            public Image Artwork;
            public SpiritModifierSigilGraphic Sigil;
            public SpiritModifierCategoryBadge Category;
            public Outline Border;
            public Graphic HostSigil;
            public CanvasGroup Label;
            public float LabelAlpha;
            [System.NonSerialized] public Vector3 Scale;
        }

        [SerializeField] private Cue reroll;
        [SerializeField] private Cue play;
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
            if (reroll == null || play == null || reroll.Group == null || play.Group == null)
                throw new System.InvalidOperationException("Action modifier cues require authored image groups.");
            reroll.Scale = reroll.Group.transform.localScale;
            play.Scale = play.Group.transform.localScale;
            Clear();
        }

        public void Show(ModifierDefinition definition, float displayDuration)
        {
            if (definition == null) { Clear(); return; }
            Show(SpiritModifierIcons.GetSprite(definition), definition.Phase, displayDuration,
                SpiritModifierIcons.GetSigilSeed(definition), definition.Phase != ModifierPhase.PlayHand,
                definition.Phase == ModifierPhase.PlayHand, definition);
        }

        public void Show(Sprite sprite, ModifierPhase phase, float displayDuration, uint sigilSeed = 0)
            => Show(sprite, phase, displayDuration, sigilSeed, phase != ModifierPhase.PlayHand, phase == ModifierPhase.PlayHand);

        public void Show(ModifierDefinition definition, float displayDuration, bool showReroll, bool showPlay)
        {
            if (definition == null) { Clear(); return; }
            Show(SpiritModifierIcons.GetSprite(definition), definition.Phase, displayDuration,
                SpiritModifierIcons.GetSigilSeed(definition), showReroll, showPlay, definition);
        }

        private void Show(Sprite sprite, ModifierPhase phase, float displayDuration, uint sigilSeed,
            bool showReroll, bool showPlay, ModifierDefinition definition = null)
        {
            Clear();
            if (reroll == null || play == null || (!showReroll && !showPlay)) return;
            active = showReroll ? reroll : play;
            secondary = showReroll && showPlay ? play : null;
            ActivePhase = phase;
            ActiveSprite = sprite;
            Prepare(active, sprite, sigilSeed, definition);
            if (secondary != null) Prepare(secondary, sprite, sigilSeed, definition);
            duration = Mathf.Max(.01f, displayDuration);
            elapsed = 0;
        }

        private static void Prepare(Cue cue, Sprite sprite, uint sigilSeed, ModifierDefinition definition)
        {
            Color color = definition == null ? SpiritUi.GetTheme(cue.Group.transform).cream :
                SpiritModifierIcons.GetEffectColor(definition);
            cue.Artwork.color = cue.Sigil.color = cue.Border.effectColor = color;
            cue.Category.Render(definition);
            cue.Artwork.sprite = sprite;
            cue.Artwork.gameObject.SetActive(sprite != null);
            cue.Sigil.Seed = sigilSeed;
            cue.Sigil.gameObject.SetActive(sprite == null);
            if (cue.HostSigil != null) cue.HostSigil.gameObject.SetActive(false);
            cue.Group.gameObject.SetActive(true);
            cue.Group.alpha = 0;
            cue.LabelAlpha = cue.Label.alpha;
            cue.Group.transform.localScale = cue.Scale;
        }

        public void ConfigureHost(SpiritHostPresentationAsset presentation)
        {
            Clear();
            foreach (var cue in new[] { reroll, play })
            {
                if (cue == null) continue;
                if (cue.HostSigil != null)
                {
                    cue.HostSigil.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(cue.HostSigil.gameObject);
                    else DestroyImmediate(cue.HostSigil.gameObject);
                }
                cue.HostSigil = presentation?.CreateSigil("HostSigil", (RectTransform)cue.Group.transform);
                if (cue.HostSigil != null)
                {

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
            foreach (var cue in new[] { reroll, play })
            {
                if (cue == null) continue;
                cue.Category.Render(null);
                cue.Artwork.color = cue.Sigil.color = cue.Border.effectColor = SpiritUi.GetTheme(cue.Group.transform).cream;
            }
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
            cue.Group.transform.localScale = cue.Scale * (1 + .1f * Mathf.Sin(progress * Mathf.PI));
        }

        private void OnDisable() => Clear();

    }
}
