using System;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * One die's binding visual, driven through two normalized levels: Mark, how strongly the die shows the spirit's mark,
     * and Growth, how far the binding roots have grown around it (0 none, 1 closed around the die). A kind of visual, such
     * as die-shaped root shells or an animated root mesh, only renders the two levels, so the presenter driving them does
     * not depend on how the roots are drawn. Animated changes advance on unscaled time.
     */
    [DisallowMultipleComponent]
    public abstract class DieBindingVisual : MonoBehaviour
    {
        private Level mark;
        private Level growth;
        private bool applied;

        public float Mark => mark.Value;
        public float Growth => growth.Value;
        public float MarkTarget => mark.Target;
        public float GrowthTarget => growth.Target;
        public bool IsAnimating => mark.Moving || growth.Moving;

        /** Shows both levels at once, ending any animation. */
        public void Show(float markLevel, float growthLevel)
        {
            mark = Level.At(Validate(markLevel, nameof(markLevel)));
            growth = Level.At(Validate(growthLevel, nameof(growthLevel)));
            Render();
        }

        /** Moves the mark from its present level to the target over the duration, eased at both ends. */
        public void AnimateMark(float target, float duration)
        {
            mark = mark.Toward(Validate(target, nameof(target)), ValidateDuration(duration));
            Render();
        }

        /** Grows or withdraws the roots from their present level to the target over the duration, eased at both ends. */
        public void AnimateGrowth(float target, float duration)
        {
            growth = growth.Toward(Validate(target, nameof(target)), ValidateDuration(duration));
            Render();
        }

        /** Advances running animations; the visual calls it every frame with unscaled time. */
        public void Advance(float deltaTime)
        {
            if (!FiniteValue.Is(deltaTime) || deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (!IsAnimating && applied) return;
            mark = mark.Advance(deltaTime);
            growth = growth.Advance(deltaTime);
            Render();
        }

        /** Renders the levels; both are in 0..1. */
        protected abstract void Apply(float markLevel, float growthLevel);

        /** A new visual shows neither mark nor roots. */
        protected virtual void Awake()
        {
            if (!applied) Render();
        }

        protected virtual void Update() => Advance(Time.unscaledDeltaTime);

        private void Render()
        {
            applied = true;
            Apply(mark.Value, growth.Value);
        }

        private static float Validate(float level, string name)
        {
            if (!FiniteValue.Is(level) || level < 0f || level > 1f) throw new ArgumentOutOfRangeException(name);
            return level;
        }

        private static float ValidateDuration(float duration)
        {
            if (!FiniteValue.Is(duration) || duration < 0f) throw new ArgumentOutOfRangeException(nameof(duration));
            return duration;
        }

        private readonly struct Level
        {
            public readonly float Value;
            public readonly float Target;
            private readonly float from;
            private readonly float elapsed;
            private readonly float duration;

            private Level(float value, float from, float target, float elapsed, float duration)
            {
                Value = value;
                this.from = from;
                Target = target;
                this.elapsed = elapsed;
                this.duration = duration;
            }

            public bool Moving => Value != Target;

            public static Level At(float value) => new Level(value, value, value, 0f, 0f);

            public Level Toward(float target, float seconds)
                => seconds <= 0f || target == Value ? At(target) : new Level(Value, Value, target, 0f, seconds);

            public Level Advance(float deltaTime)
            {
                if (!Moving) return this;
                float time = elapsed + deltaTime;
                if (time >= duration) return At(Target);
                return new Level(Mathf.Lerp(from, Target, Mathf.SmoothStep(0f, 1f, time / duration)), from, Target, time, duration);
            }
        }
    }
}
