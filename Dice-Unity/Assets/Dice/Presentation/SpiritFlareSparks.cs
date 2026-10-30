using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /**
     * A table piece's flare burst in UI space: a warm glow over the flame and a few sparks thrown up and outward that
     * shrink and fade. Each enable plays one burst from its start; every part is hidden once it finishes and when the
     * object is disabled. Offsets, sizes and the reach are in this transform's local units, so give it a uniform scale.
     */
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class SpiritFlareSparks : MonoBehaviour
    {
        /** Share of a flare spent rising to full before it settles back. */
        internal const float FlareRise = .15f;
        /** Share of a spark's life spent fading in. */
        private const float SparkRise = .1f;
        /** The glow's size at rest, as a share of its full size; it swells to full with the flare. */
        private const float GlowRestScale = .8f;
        /** Share of its launch size a spark loses by the end of its life. */
        private const float SparkShrink = .6f;
        [Tooltip("A white disc whose alpha falls smoothly to zero at its rim; every glow and spark draws it.")]
        [SerializeField] private Texture2D softDisc;
        [SerializeField, Range(0, 16)] private int sparkCount = 7;
        [Tooltip("Spark diameter at launch; sparks shrink as they fade.")]
        [SerializeField, Min(0)] private float sparkSize = 3.5f;
        [Tooltip("Farthest spark offset from this transform's pivot: sideways either way, and upward.")]
        [SerializeField] private Vector2 sparkReach = new Vector2(22, 14);
        [SerializeField, Min(.05f)] private float sparkLifetime = .4f;
        [Tooltip("Sparks launch at staggered times up to this delay.")]
        [SerializeField, Min(0)] private float sparkStagger = .08f;
        [SerializeField] private Color sparkColor = new Color(1f, .88f, .55f, 1f);
        [Tooltip("Size of the glow centered on this transform's pivot.")]
        [SerializeField] private Vector2 glowSize = new Vector2(56, 28);
        [SerializeField] private Color glowColor = new Color(1f, .8f, .45f, .55f);
        [SerializeField, Min(.05f)] private float glowDuration = .6f;
        [Tooltip("Seeds the spark spread; consecutive bursts vary from it.")]
        [SerializeField] private int seed = 4519;

        private readonly List<RawImage> sparks = new List<RawImage>();
        private readonly List<Vector2> targets = new List<Vector2>();
        private readonly List<float> delays = new List<float>();
        private RawImage glow;
        private float start = float.NegativeInfinity;
        private int burst;

        /** Seconds from enable until every part has finished. */
        private float Duration => Mathf.Max(glowDuration, sparkStagger + sparkLifetime);
        private bool IsPlaying => !float.IsNegativeInfinity(start);

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            Build();
            var random = new System.Random(unchecked(seed + 7919 * burst++));
            for (int i = 0; i < sparkCount; ++i)
            {
                // Sparks fan across the flame; the outer ones travel wider and lower than the middle ones.
                float side = Mathf.Clamp(-1f + 2f * (i + .5f) / sparkCount + Range(random, -.15f, .15f), -1f, 1f);
                float distance = Range(random, .7f, 1f);
                targets[i] = new Vector2(side * sparkReach.x * distance,
                    sparkReach.y * distance * (1f - .35f * Mathf.Abs(side)));
                delays[i] = Range(random, 0f, sparkStagger);
            }
            start = Time.unscaledTime;
            Animate(0f);
        }

        private void OnDisable()
        {
            start = float.NegativeInfinity;
            // Parts are built on the first play-mode enable; a burst never played has none.
            if (glow != null) glow.gameObject.SetActive(false);
            foreach (var spark in sparks) spark.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!IsPlaying) return;
            float elapsed = Time.unscaledTime - start;
            if (elapsed >= Duration) { OnDisable(); return; }
            Animate(elapsed);
        }

        private void Animate(float elapsed)
        {
            float glowProgress = elapsed / glowDuration;
            bool glowing = glowProgress < 1f;
            glow.gameObject.SetActive(glowing);
            if (glowing)
            {
                // The glow swells with the flare's quick rise, then settles out.
                float level = RiseAndSettle(glowProgress, FlareRise);
                Color color = glowColor;
                color.a *= level;
                glow.color = color;
                glow.rectTransform.sizeDelta = glowSize * (GlowRestScale + (1f - GlowRestScale) * level);
            }
            for (int i = 0; i < sparkCount && i < sparks.Count; ++i)
            {
                float progress = (elapsed - delays[i]) / sparkLifetime;
                bool live = progress >= 0f && progress < 1f;
                sparks[i].gameObject.SetActive(live);
                if (!live) continue;
                // Thrown quickly, slowing as they cool; they never pass their target offset.
                float travel = 1f - (1f - progress) * (1f - progress);
                sparks[i].rectTransform.anchoredPosition = targets[i] * travel;
                sparks[i].rectTransform.sizeDelta = Vector2.one * (sparkSize * (1f - SparkShrink * progress));
                Color color = sparkColor;
                color.a *= RiseAndSettle(progress, SparkRise);
                sparks[i].color = color;
            }
        }

        private void Build()
        {
            if (softDisc == null)
                throw new System.InvalidOperationException("The flare sparks on " + name + " require a soft disc texture.");
            if (glow == null) glow = CreatePart("Flare Glow");
            while (sparks.Count < sparkCount)
            {
                sparks.Add(CreatePart("Spark " + (sparks.Count + 1)));
                targets.Add(Vector2.zero);
                delays.Add(0f);
            }
        }

        private RawImage CreatePart(string partName)
        {
            var part = new GameObject(partName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            part.hideFlags = HideFlags.DontSave;
            part.layer = gameObject.layer;
            var rect = (RectTransform)part.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            var image = part.GetComponent<RawImage>();
            image.texture = softDisc;
            image.raycastTarget = false;
            part.SetActive(false);
            return image;
        }

        /** A pulse over progress from 0 to 1 that rises linearly to full over the rise share, then eases back to nothing. */
        internal static float RiseAndSettle(float progress, float rise)
            => progress < rise ? progress / rise : 1f - Mathf.SmoothStep(0f, 1f, (progress - rise) / (1f - rise));

        private static float Range(System.Random random, float min, float max) => min + (max - min) * (float)random.NextDouble();
    }
}
