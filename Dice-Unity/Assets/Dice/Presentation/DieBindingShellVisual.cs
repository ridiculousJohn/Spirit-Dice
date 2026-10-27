using UnityEngine;

namespace Dice.Presentation
{
    /**
     * Draws a die's binding as two die-shaped shells around it with the rule die roots shader (Shared/RuleDieRoots.shader):
     * the mark shell fades in with its level and pulses slowly, and the root shell's coverage is the growth level, so its
     * roots climb from the die's base and over its top. A shell is disabled while its level is zero. Levels and the pulse
     * reach the shells through property blocks, leaving the authored materials shared.
     */
    public sealed class DieBindingShellVisual : DieBindingVisual
    {
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int CoverageId = Shader.PropertyToID("_Coverage");
        private static readonly int PulseId = Shader.PropertyToID("_Pulse");

        [Tooltip("Shell that shows the mark; its opacity is the mark level.")]
        [SerializeField] private Renderer markShell;
        [Tooltip("Shell that shows the binding roots; its coverage is the growth level.")]
        [SerializeField] private Renderer rootShell;
        [Tooltip("Fraction by which the mark's glow swells and fades around its level.")]
        [SerializeField, Range(0, 1)] private float markPulseAmount = .35f;
        [Tooltip("Seconds per slow pulse of the mark's glow.")]
        [SerializeField, Min(.1f)] private float markPulsePeriod = 2.8f;
        [Tooltip("Fraction by which the roots' glow swells and fades.")]
        [SerializeField, Range(0, 1)] private float rootPulseAmount = .3f;
        [Tooltip("Seconds per slow pulse of the roots' glow.")]
        [SerializeField, Min(.1f)] private float rootPulsePeriod = 3.4f;
        private MaterialPropertyBlock properties;
        private float markLevel;
        private float growthLevel;

        public Renderer MarkShell => markShell;
        public Renderer RootShell => rootShell;

        protected override void Apply(float mark, float growth)
        {
            markLevel = mark;
            growthLevel = growth;
            if (markShell != null) markShell.enabled = mark > 0f;
            if (rootShell != null) rootShell.enabled = growth > 0f;
            WriteProperties();
        }

        protected override void Update()
        {
            base.Update();
            if (markLevel > 0f || growthLevel > 0f) WriteProperties();
        }

        private void WriteProperties()
        {
            properties ??= new MaterialPropertyBlock();
            float time = Time.unscaledTime;
            if (markShell != null && markLevel > 0f)
            {
                markShell.GetPropertyBlock(properties);
                properties.SetFloat(OpacityId, markLevel);
                properties.SetFloat(CoverageId, 1f);
                properties.SetFloat(PulseId, Pulse(time, markPulseAmount, markPulsePeriod));
                markShell.SetPropertyBlock(properties);
            }
            if (rootShell != null && growthLevel > 0f)
            {
                rootShell.GetPropertyBlock(properties);
                properties.SetFloat(OpacityId, 1f);
                properties.SetFloat(CoverageId, growthLevel);
                properties.SetFloat(PulseId, Pulse(time, rootPulseAmount, rootPulsePeriod));
                rootShell.SetPropertyBlock(properties);
            }
        }

        private static float Pulse(float time, float amount, float period)
            => 1f + amount * Mathf.Sin(2f * Mathf.PI * time / Mathf.Max(.1f, period));
    }
}
