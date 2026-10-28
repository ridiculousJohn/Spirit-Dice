using Dice.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Marks the triggering action and any action resources granted by a modifier. */
    public sealed class SpiritActionModifierCue : MonoBehaviour
    {
        /** Shortest cue shown, in seconds. */
        private const float MinimumDuration = .01f;
        /** The cue fades in over at most this many seconds... */
        private const float MaximumFadeInSeconds = .1f;
        /** ...or this fraction of its duration, whichever is shorter. */
        private const float MaximumFadeInFraction = .2f;
        /** Fraction of the duration after which the cue fades out. */
        private const float FadeOutStart = .7f;
        /** Extra scale at the peak of the cue's swell. */
        private const float PulseScale = .1f;

        [System.Serializable]
        private sealed class Cue
        {
            public CanvasGroup Group;
            public Image Artwork;
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

        public void Initialize()
        {
            if (reroll == null || play == null || reroll.Group == null || play.Group == null)
                throw new System.InvalidOperationException("Action modifier cues require authored image groups.");
            reroll.Scale = reroll.Group.transform.localScale;
            play.Scale = play.Group.transform.localScale;
            Clear();
        }

        public void Show(Sprite sprite, ModifierPhase phase, float displayDuration)
            => Show(sprite, phase, displayDuration, phase != ModifierPhase.PlayHand, phase == ModifierPhase.PlayHand);

        public void Show(ModifierPresentationCatalog charms, ModifierDefinition definition, float displayDuration,
            bool showReroll, bool showPlay)
        {
            if (definition == null) { Clear(); return; }
            Show(charms.GetIcon(definition), definition.Phase, displayDuration, showReroll, showPlay, charms, definition);
        }

        private void Show(Sprite sprite, ModifierPhase phase, float displayDuration, bool showReroll, bool showPlay,
            ModifierPresentationCatalog charms = null, ModifierDefinition definition = null)
        {
            Clear();
            if (reroll == null || play == null || (!showReroll && !showPlay)) return;
            active = showReroll ? reroll : play;
            secondary = showReroll && showPlay ? play : null;
            ActivePhase = phase;
            ActiveSprite = sprite;
            Prepare(active, sprite, charms, definition);
            if (secondary != null) Prepare(secondary, sprite, charms, definition);
            duration = Mathf.Max(MinimumDuration, displayDuration);
            elapsed = 0;
        }

        /** A charm cue takes the charm's category colour and mark; a host cue, without a charm, takes the theme's cream. */
        private static void Prepare(Cue cue, Sprite sprite, ModifierPresentationCatalog charms, ModifierDefinition definition)
        {
            Color color = definition == null ? SpiritUi.GetTheme(cue.Group.transform).cream :
                charms.GetEffectColor(definition);
            cue.Artwork.color = cue.Border.effectColor = color;
            cue.Category.Render(charms, definition);
            cue.Artwork.sprite = sprite;
            cue.Artwork.gameObject.SetActive(sprite != null);
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
                cue.HostSigil = SpiritHostPresentationAsset.ReplaceSigil(cue.HostSigil, presentation, "HostSigil",
                    (RectTransform)cue.Group.transform);
                if (cue.HostSigil != null) cue.HostSigil.gameObject.SetActive(false);
            }
        }

        public void ShowHost(Sprite sprite, ModifierPhase phase, float displayDuration)
        {
            Show(sprite, phase, displayDuration);
            if (active == null || sprite != null || active.HostSigil == null) return;
            active.HostSigil.gameObject.SetActive(true);
        }

        public void Clear()
        {
            if (active != null && active.Label != null) active.Label.alpha = active.LabelAlpha;
            if (secondary != null && secondary.Label != null) secondary.Label.alpha = secondary.LabelAlpha;
            foreach (var cue in new[] { reroll, play })
            {
                if (cue == null) continue;
                cue.Category.Render(null, null);
                cue.Artwork.color = cue.Border.effectColor = SpiritUi.GetTheme(cue.Group.transform).cream;
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
            float fadeIn = Mathf.SmoothStep(0, 1, elapsed / Mathf.Min(MaximumFadeInSeconds, duration * MaximumFadeInFraction));
            float alpha = fadeIn * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(FadeOutStart, 1, progress)));
            UpdateCue(active, alpha, progress);
            if (secondary != null) UpdateCue(secondary, alpha, progress);
            if (elapsed >= duration) Clear();
        }

        private static void UpdateCue(Cue cue, float alpha, float progress)
        {
            cue.Group.alpha = alpha;
            cue.Label.alpha = cue.LabelAlpha * (1 - alpha);
            cue.Group.transform.localScale = cue.Scale * (1 + PulseScale * Mathf.Sin(progress * Mathf.PI));
        }

        private void OnDisable() => Clear();
    }
}
