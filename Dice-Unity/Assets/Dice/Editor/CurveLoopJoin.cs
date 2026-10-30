using System;
using UnityEngine;

namespace Dice.Editor
{
    /**
     * How far a looping curve's end misses its start: the difference of its values and of its slopes at time 0 and at the
     * loop's duration. A loop that joins smoothly has both near zero; a value jump shows as a pop, a slope jump as a kink.
     */
    public readonly struct CurveLoopJoin
    {
        // Each slope is a one-sided second-order difference over steps of this fraction of the duration: small enough to
        // follow the curve at its ends, large enough that float evaluation noise stays far below the slope noise floor.
        private const float SlopeStepFraction = .001f;

        /** The absolute difference of the values at the loop's end and start, in the curve's own unit. */
        public float ValueJump { get; }
        /** The absolute difference of the slopes at the loop's end and start, in the curve's unit per second. */
        public float SlopeJump { get; }

        public CurveLoopJoin(float valueJump, float slopeJump)
        {
            ValueJump = valueJump;
            SlopeJump = slopeJump;
        }

        /** Measures the curve looping over 0 to the duration. */
        public static CurveLoopJoin Measure(AnimationCurve curve, float duration)
        {
            if (curve == null) throw new ArgumentNullException(nameof(curve));
            if (!(duration > 0f) || float.IsInfinity(duration))
                throw new ArgumentOutOfRangeException(nameof(duration), "A loop requires a positive finite duration.");
            float step = duration * SlopeStepFraction;
            float start = curve.Evaluate(0f), end = curve.Evaluate(duration);
            float startSlope = (-3f * start + 4f * curve.Evaluate(step) - curve.Evaluate(2f * step)) / (2f * step);
            float endSlope = (3f * end - 4f * curve.Evaluate(duration - step) + curve.Evaluate(duration - 2f * step)) / (2f * step);
            return new CurveLoopJoin(Mathf.Abs(end - start), Mathf.Abs(endSlope - startSlope));
        }
    }
}
