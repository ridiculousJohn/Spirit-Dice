using UnityEngine;

namespace Dice.Presentation
{
    /** A hanging prop swings like a pendulum about its pivot, decaying back to its idle sway. */
    [CreateAssetMenu(menuName = "Dice/Presentation/Ambient Motion/Swing")]
    public sealed class AmbientSwingMotionAsset : AmbientMotionAsset
    {
        [Tooltip("Peak swing in degrees for a strike of strength one.")]
        [SerializeField, Min(0)] private float amplitude = 12f;
        [Tooltip("Seconds per full swing out and back.")]
        [SerializeField, Min(.05f)] private float period = 1.4f;
        [Tooltip("Decay time constant in seconds.")]
        [SerializeField, Min(.05f)] private float decay = 1.6f;

        public float Amplitude => amplitude;
        public float Period => period;
        public float Decay => decay;

        public override AmbientMotion Create(Transform target) => new AmbientSwingMotion(target, this);
    }

    /** Pendulum swing about the target's own pivot, such as the pin a hanging prop hangs from. */
    public sealed class AmbientSwingMotion : AmbientMotion
    {
        private const float RestDegrees = .01f;
        private readonly AmbientSwingMotionAsset settings;

        public AmbientSwingMotion(Transform target, AmbientSwingMotionAsset settings) : base(target)
        {
            this.settings = settings != null ? settings : throw new System.ArgumentNullException(nameof(settings));
        }

        protected override float Duration(float strength)
            => DecayDuration(settings.Amplitude * strength, Mathf.Max(.05f, settings.Decay), RestDegrees);

        protected override void Evaluate(float elapsed, float strength, ref float angle, ref Vector2 offset)
            => angle += settings.Amplitude * strength * Mathf.Exp(-elapsed / Mathf.Max(.05f, settings.Decay)) *
                Mathf.Sin(2f * Mathf.PI * elapsed / Mathf.Max(.05f, settings.Period));
    }
}
