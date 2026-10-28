using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /**
     * Authored prop of a table piece interaction, with what every interaction kind shares: the piece and interaction IDs, the
     * presenter that binds the prop to the interaction's state and input, and the prop's light and repel flare. Behaviour of
     * one interaction kind lives in that kind's presenter and the components it owns on the prop.
     */
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class SpiritTablePieceView : MonoBehaviour
    {
        [Tooltip("Table piece ID from the encounter's mechanical content.")]
        [SerializeField] private string pieceId;
        [Tooltip("ID of the piece's interaction.")]
        [SerializeField] private string interactionId;
        [Tooltip("Presenter kind of the interaction, which binds this prop to the interaction's state and input; it must present the interaction's kind.")]
        [SerializeField] private PieceInteractionPresenterAsset presenter;
        [Tooltip("Flame or glow graphics whose alpha follows the bound scene light's sampled intensity.")]
        [SerializeField] private Graphic[] emissiveImages = Array.Empty<Graphic>();
        [Tooltip("Optional part scaled while the piece repels interference, such as the flame.")]
        [SerializeField] private RectTransform flareTarget;
        [Tooltip("Optional authored effect, such as sparks, shown only while the piece flares; it restarts each time it is enabled.")]
        [SerializeField] private GameObject flareEffect;
        [SerializeField, Min(.05f)] private float flareDuration = .7f;
        [Tooltip("Peak width scale of the flare target.")]
        [SerializeField, Min(1)] private float flareScale = 1.35f;
        [Tooltip("Peak height scale of the flare target; below the width scale, the flare widens rather than reaching upward.")]
        [SerializeField, Min(1)] private float flareHeightScale = 1.35f;
        [Tooltip("Peak multiplier applied to the bound scene light while flaring.")]
        [SerializeField, Min(1)] private float flareLightIntensity = 2.2f;

        private float flareStart = float.NegativeInfinity;
        private Vector3 flareBaseScale;
        private Color[] emissiveColors;

        public string PieceId => pieceId;
        public string InteractionId => interactionId;
        public PieceInteractionPresenterAsset Presenter => presenter;
        /** Multiplies the bound scene light; above one only while flaring. */
        public float LightIntensity => 1f + (flareLightIntensity - 1f) * FlarePulse();
        public RectTransform FlareTarget => flareTarget;
        public float FlareDuration => flareDuration;
        public bool IsFlaring => !float.IsNegativeInfinity(flareStart);

        /** The prop among these that presents the piece's interaction, or null when none does. */
        public static SpiritTablePieceView Find(IReadOnlyList<SpiritTablePieceView> pieces, string pieceId, string interactionId)
        {
            foreach (var piece in pieces)
                if (piece != null && string.Equals(piece.PieceId, pieceId, StringComparison.Ordinal) &&
                    string.Equals(piece.InteractionId, interactionId, StringComparison.Ordinal)) return piece;
            return null;
        }

        /** Starts the repel reaction and returns its duration. */
        public float Flare()
        {
            if (flareTarget != null && float.IsNegativeInfinity(flareStart)) flareBaseScale = flareTarget.localScale;
            flareStart = Time.unscaledTime;
            if (flareEffect != null)
            {
                // A repeated flare restarts the effect from its first frame.
                flareEffect.SetActive(false);
                flareEffect.SetActive(true);
            }
            return flareDuration;
        }

        /** Scales emissive alpha from the authored colors; one restores them. */
        public void SetEmission(float emission)
        {
            if (emissiveImages == null || emissiveImages.Length == 0) return;
            if (emissiveColors == null)
            {
                emissiveColors = new Color[emissiveImages.Length];
                for (int i = 0; i < emissiveImages.Length; ++i)
                    if (emissiveImages[i] != null) emissiveColors[i] = emissiveImages[i].color;
            }
            for (int i = 0; i < emissiveImages.Length; ++i)
            {
                if (emissiveImages[i] == null) continue;
                Color color = emissiveColors[i];
                color.a *= Mathf.Clamp01(emission);
                emissiveImages[i].color = color;
            }
        }

        public void RestoreEmission()
        {
            if (emissiveColors == null) return;
            SetEmission(1f);
            emissiveColors = null;
        }

        private float FlarePulse()
        {
            if (float.IsNegativeInfinity(flareStart)) return 0f;
            float progress = (Time.unscaledTime - flareStart) / flareDuration;
            if (progress < 0f || progress >= 1f) return 0f;
            // A quick flare-up followed by a slower settle back to the authored flame.
            return SpiritFlareSparks.RiseAndSettle(progress, SpiritFlareSparks.FlareRise);
        }

        private void Update()
        {
            if (float.IsNegativeInfinity(flareStart)) return;
            if (Time.unscaledTime - flareStart >= flareDuration) { EndFlare(); return; }
            if (flareTarget == null) return;
            float pulse = FlarePulse();
            flareTarget.localScale = new Vector3(flareBaseScale.x * (1f + (flareScale - 1f) * pulse),
                flareBaseScale.y * (1f + (flareHeightScale - 1f) * pulse), flareBaseScale.z);
        }

        private void EndFlare()
        {
            if (float.IsNegativeInfinity(flareStart)) return;
            if (flareTarget != null) flareTarget.localScale = flareBaseScale;
            if (flareEffect != null) flareEffect.SetActive(false);
            flareStart = float.NegativeInfinity;
        }

        private void OnDisable()
        {
            EndFlare();
            RestoreEmission();
        }
    }
}
