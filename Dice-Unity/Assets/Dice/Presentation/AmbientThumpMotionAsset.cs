using UnityEngine;

namespace Dice.Presentation
{
    /** A prop or lid hops and drops with a few quick bounces, like a knock from beneath the board. */
    [CreateAssetMenu(menuName = "Dice/Presentation/Ambient Motion/Thump")]
    public sealed class AmbientThumpMotionAsset : AmbientMotionAsset
    {
        /** Shortest first hop, in seconds. */
        public const float MinimumHopDuration = .02f;
        /** Largest share of its height a bounce keeps, so the bounces always die away. */
        public const float MaximumRestitution = .9f;
        [Tooltip("First hop height in parent units for a strike of strength one.")]
        [SerializeField, Min(0)] private float height = 6f;
        [Tooltip("Seconds the first hop is off the table.")]
        [SerializeField, Min(MinimumHopDuration)] private float hopDuration = .16f;
        [Tooltip("Height kept by each following bounce.")]
        [SerializeField, Range(0, MaximumRestitution)] private float restitution = .35f;
        [Tooltip("Tilt in degrees at the top of the first hop; bounces alternate sides.")]
        [SerializeField] private float tilt = 2f;

        public float Height => height;
        public float HopDuration => hopDuration;
        public float Restitution => restitution;
        public float Tilt => tilt;

        public override AmbientMotion Create(Transform target) => new AmbientThumpMotion(target, this);
    }

    /** Ballistic hops of decreasing height; each lasts in proportion to the square root of its height. */
    public sealed class AmbientThumpMotion : AmbientMotion
    {
        private const float RestHeight = .05f;
        private const int MaximumBounces = 8;
        private readonly AmbientThumpMotionAsset settings;

        public AmbientThumpMotion(Transform target, AmbientThumpMotionAsset settings) : base(target)
        {
            this.settings = settings != null ? settings : throw new System.ArgumentNullException(nameof(settings));
        }

        protected override float Duration(float strength)
        {
            float duration = 0f;
            float scale = 1f;
            for (int bounce = 0; bounce < MaximumBounces && settings.Height * strength * scale >= RestHeight; ++bounce)
            {
                duration += HopDuration(scale);
                scale *= Mathf.Clamp(settings.Restitution, 0f, AmbientThumpMotionAsset.MaximumRestitution);
            }
            return duration;
        }

        protected override void Evaluate(float elapsed, float strength, ref float angle, ref Vector2 offset)
        {
            float scale = 1f;
            for (int bounce = 0; bounce < MaximumBounces && settings.Height * strength * scale >= RestHeight; ++bounce)
            {
                float hop = HopDuration(scale);
                if (elapsed < hop)
                {
                    float phase = elapsed / hop;
                    offset.y += settings.Height * strength * scale * 4f * phase * (1f - phase);
                    angle += (bounce % 2 == 0 ? 1f : -1f) * settings.Tilt * strength * scale * Mathf.Sin(Mathf.PI * phase);
                    return;
                }
                elapsed -= hop;
                scale *= Mathf.Clamp(settings.Restitution, 0f, AmbientThumpMotionAsset.MaximumRestitution);
            }
        }

        private float HopDuration(float scale) => Mathf.Max(AmbientThumpMotionAsset.MinimumHopDuration, settings.HopDuration) * Mathf.Sqrt(scale);
    }
}
