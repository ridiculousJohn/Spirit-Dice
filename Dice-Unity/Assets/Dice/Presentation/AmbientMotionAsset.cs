using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dice.Presentation
{
    /** One motion kind's authored tuning; each kind creates its own runtime motion, so new kinds need no other changes. */
    public abstract class AmbientMotionAsset : ScriptableObject
    {
        /** Creates an independent runtime motion that offsets the target's pose. */
        public abstract AmbientMotion Create(Transform target);
    }

    /**
     * A prop's decaying response to strikes, applied on top of whatever pose the prop holds this frame.
     * A pose written by anything else (an animation, layout) becomes the new base; otherwise the base is
     * the pose before the motion started, so the prop always returns exactly to it.
     */
    public abstract class AmbientMotion
    {
        private const float SettledFactor = .002f;
        private readonly List<Response> strikes = new List<Response>();
        private double settleStart = double.NaN;
        private float settleTime;
        private bool applied;
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private Vector3 writtenPosition;
        private Quaternion writtenRotation;

        protected AmbientMotion(Transform target)
        {
            Target = target != null ? target : throw new ArgumentNullException(nameof(target));
        }

        public Transform Target { get; }
        /** True while any strike still moves the prop. */
        public bool IsMoving => strikes.Count > 0;
        /** True while the prop's pose carries an offset from this motion. */
        public bool IsOffset => applied;

        /** Starts a decaying response of the given strength at the given activity time; responses add together. */
        public void Strike(double time, float strength)
        {
            if (!SceneLightingAsset.Finite(strength) || strength < 0f) throw new ArgumentOutOfRangeException(nameof(strength));
            if (strength == 0f) return;
            strikes.Add(new Response(time, strength, Duration(strength)));
        }

        /** Damps every current response to rest with the given time constant; idempotent while settling. */
        public void Settle(double time, float timeConstant)
        {
            if (strikes.Count == 0 || !double.IsNaN(settleStart)) return;
            settleStart = time;
            settleTime = Mathf.Max(.01f, timeConstant);
        }

        /** Drops every response and restores the base pose at once. */
        public void Rest()
        {
            strikes.Clear();
            settleStart = double.NaN;
            if (!applied) return;
            ReadBase();
            Target.localPosition = basePosition;
            Target.localRotation = baseRotation;
            applied = false;
        }

        /** Writes the base pose plus the summed responses at the given activity time. */
        public void Apply(double time)
        {
            float settle = double.IsNaN(settleStart) ? 1f : Mathf.Exp(-(float)(time - settleStart) / settleTime);
            float angle = 0f;
            Vector2 offset = Vector2.zero;
            for (int i = strikes.Count - 1; i >= 0; --i)
            {
                float elapsed = (float)(time - strikes[i].Start);
                if (elapsed >= strikes[i].Duration || settle < SettledFactor) { strikes.RemoveAt(i); continue; }
                if (elapsed >= 0f) Evaluate(elapsed, strikes[i].Strength, ref angle, ref offset);
            }
            if (strikes.Count == 0) settleStart = double.NaN;
            if (strikes.Count == 0 && !applied) return;
            ReadBase();
            if (strikes.Count == 0)
            {
                Target.localPosition = basePosition;
                Target.localRotation = baseRotation;
                applied = false;
                return;
            }
            Quaternion turn = Quaternion.Euler(0f, 0f, angle * settle);
            // Rotating about a local pivot keeps that point fixed in the parent.
            Vector3 pivot = Vector3.Scale(Target.localScale, Pivot(angle * settle));
            Target.localRotation = baseRotation * turn;
            Target.localPosition = basePosition + baseRotation * (pivot - turn * pivot) + (Vector3)(offset * settle);
            // Read back so an unchanged pose next frame compares exactly equal to what was written.
            writtenRotation = Target.localRotation;
            writtenPosition = Target.localPosition;
            applied = true;
        }

        /** Seconds after which a strike of the given strength has decayed to rest. */
        protected abstract float Duration(float strength);

        /** Adds one strike's rotation in degrees about the pivot and its translation in parent units. */
        protected abstract void Evaluate(float elapsed, float strength, ref float angle, ref Vector2 offset);

        /** Local point a rotation by the given degrees turns about; the transform's own pivot unless a kind names contact points. */
        protected virtual Vector3 Pivot(float angle) => Vector3.zero;

        private void ReadBase()
        {
            Vector3 position = Target.localPosition;
            Quaternion rotation = Target.localRotation;
            if (!applied || !position.Equals(writtenPosition)) basePosition = position;
            if (!applied || !rotation.Equals(writtenRotation)) baseRotation = rotation;
        }

        /** Seconds until a decay exp(-t / timeConstant) of the amplitude falls below the threshold. */
        protected static float DecayDuration(float amplitude, float timeConstant, float threshold)
            => amplitude > threshold ? timeConstant * Mathf.Log(amplitude / threshold) : 0f;

        private readonly struct Response
        {
            public readonly double Start;
            public readonly float Strength;
            public readonly float Duration;

            public Response(double start, float strength, float duration)
            {
                Start = start;
                Strength = strength;
                Duration = duration;
            }
        }
    }
}
