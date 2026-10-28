using UnityEngine;

namespace Dice.Presentation
{
    /** A standing prop rocks on the edges of its base, decaying back to rest. */
    [CreateAssetMenu(menuName = "Dice/Presentation/Ambient Motion/Wobble")]
    public sealed class AmbientWobbleMotionAsset : AmbientMotionAsset
    {
        /** Shortest period and decay time constant, in seconds. */
        public const float MinimumTime = .02f;
        [Tooltip("Peak rock in degrees for a strike of strength one.")]
        [SerializeField, Min(0)] private float amplitude = 4f;
        [Tooltip("Seconds per full rock to one side and back.")]
        [SerializeField, Min(MinimumTime)] private float period = .3f;
        [Tooltip("Decay time constant in seconds.")]
        [SerializeField, Min(MinimumTime)] private float decay = .35f;
        [Tooltip("Left base contact in normalized rect coordinates of a RectTransform target; counterclockwise rocks turn about it.")]
        [SerializeField] private Vector2 leftContact = new Vector2(.25f, 0f);
        [Tooltip("Right base contact in normalized rect coordinates of a RectTransform target; clockwise rocks turn about it.")]
        [SerializeField] private Vector2 rightContact = new Vector2(.75f, 0f);

        public float Amplitude => amplitude;
        public float Period => period;
        public float Decay => decay;
        public Vector2 LeftContact => leftContact;
        public Vector2 RightContact => rightContact;

        public override AmbientMotion Create(Transform target) => new AmbientWobbleMotion(target, this);
    }

    /**
     * Damped rocking that lifts one side of the base: a counterclockwise (positive) angle stands on the left contact
     * and a clockwise one on the right, switching at rest where the turn is zero. Non-rect targets rock about their own pivot.
     */
    public sealed class AmbientWobbleMotion : AmbientMotion
    {
        private const float RestDegrees = .01f;
        private readonly AmbientWobbleMotionAsset settings;

        public AmbientWobbleMotion(Transform target, AmbientWobbleMotionAsset settings) : base(target)
        {
            this.settings = settings != null ? settings : throw new System.ArgumentNullException(nameof(settings));
        }

        protected override float Duration(float strength)
            => DecayDuration(settings.Amplitude * strength, Mathf.Max(AmbientWobbleMotionAsset.MinimumTime, settings.Decay), RestDegrees);

        protected override void Evaluate(float elapsed, float strength, ref float angle, ref Vector2 offset)
            => angle += settings.Amplitude * strength * Mathf.Exp(-elapsed / Mathf.Max(AmbientWobbleMotionAsset.MinimumTime, settings.Decay)) *
                Mathf.Sin(2f * Mathf.PI * elapsed / Mathf.Max(AmbientWobbleMotionAsset.MinimumTime, settings.Period));

        protected override Vector3 Pivot(float angle)
        {
            if (!(Target is RectTransform rect)) return Vector3.zero;
            Rect bounds = rect.rect;
            Vector2 contact = angle >= 0f ? settings.LeftContact : settings.RightContact;
            return new Vector3(bounds.xMin + contact.x * bounds.width, bounds.yMin + contact.y * bounds.height, 0f);
        }
    }
}
