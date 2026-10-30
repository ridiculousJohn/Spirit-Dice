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

        /** Fails, naming the owner (the activity asset and actor using this motion) and this asset, when the tuning cannot run. */
        public virtual void Validate(string owner) { }
    }

    /**
     * A prop's response to strikes, offset from the rest pose it held when the motion started.
     * The first strike while the prop is not offset records that rest pose; each frame of the motion writes
     * the rest pose plus the summed responses, and when the motion ends or rests the rest pose is written back exactly.
     * A RectTransform's position is held as its anchored position: layout re-derives its local position from that,
     * with rounding, and moves it with the parent's rect, so the prop and its offset follow the laid-out position.
     */
    public abstract class AmbientMotion
    {
        /** Shortest settle time constant, in seconds. */
        public const float MinimumSettleTime = .01f;
        private const float SettledFactor = .002f;
        private readonly List<Response> strikes = new List<Response>();
        private double settleStart = double.NaN;
        private float settleTime;
        private bool applied;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private readonly RectTransform rect;

        protected AmbientMotion(Transform target)
        {
            Target = target != null ? target : throw new ArgumentNullException(nameof(target));
            rect = target as RectTransform;
        }

        public Transform Target { get; }
        /** True while any strike still moves the prop. */
        public bool IsMoving => strikes.Count > 0;
        /** True while the prop's pose carries an offset from this motion. */
        public bool IsOffset => applied;

        /** False for a kind that moves only on the idle schedule; the activity's jolts and knocks pass such props by. */
        public virtual bool AnswersJolts => true;

        /**
         * True for a kind whose strike plays one whole run along an authored course rather than a response that decays:
         * a strike while it runs is ignored and settling lets the run finish, so the prop never leaves its course.
         */
        protected virtual bool RunsToEnd => false;

        /** Starts a response of the given strength at the given activity time; responses add together unless the kind runs to its end. */
        public void Strike(double time, float strength)
        {
            if (!FiniteValue.Is(strength) || strength < 0f) throw new ArgumentOutOfRangeException(nameof(strength));
            if (strength == 0f || (RunsToEnd && strikes.Count > 0)) return;
            if (!applied)
            {
                restPosition = Position;
                restRotation = Target.localRotation;
            }
            strikes.Add(new Response(time, strength, Duration(strength)));
        }

        /** Damps every current response to rest with the given time constant; idempotent while settling. A run that runs to its end finishes instead. */
        public void Settle(double time, float timeConstant)
        {
            if (strikes.Count == 0 || !double.IsNaN(settleStart) || RunsToEnd) return;
            settleStart = time;
            settleTime = Mathf.Max(MinimumSettleTime, timeConstant);
        }

        /** Drops every response and restores the rest pose at once. */
        public void Rest()
        {
            strikes.Clear();
            settleStart = double.NaN;
            if (applied) ReturnToRest();
        }

        /** Writes the rest pose plus the summed responses at the given activity time, or the rest pose once they end. */
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
            if (strikes.Count == 0)
            {
                settleStart = double.NaN;
                if (applied) ReturnToRest();
                return;
            }
            Quaternion turn = Quaternion.Euler(0f, 0f, angle * settle);
            // Rotating about a local pivot keeps that point fixed in the parent.
            Vector3 pivot = Vector3.Scale(Target.localScale, Pivot(angle * settle));
            Target.localRotation = restRotation * turn;
            // A parent-local offset moves the anchored position by the same amount as the local position.
            Position = restPosition + restRotation * (pivot - turn * pivot) + (Vector3)(offset * settle);
            applied = true;
        }

        /** Seconds after which a strike of the given strength has decayed to rest or finished its run. */
        protected abstract float Duration(float strength);

        /** Adds one strike's rotation in degrees about the pivot and its translation in parent units. */
        protected abstract void Evaluate(float elapsed, float strength, ref float angle, ref Vector2 offset);

        /** Local point a rotation by the given degrees turns about; the transform's own pivot unless a kind names contact points. */
        protected virtual Vector3 Pivot(float angle) => Vector3.zero;

        /** The anchored position of a RectTransform target, else the local position. */
        private Vector3 Position
        {
            get => rect != null ? rect.anchoredPosition3D : Target.localPosition;
            set
            {
                if (rect != null) rect.anchoredPosition3D = value;
                else Target.localPosition = value;
            }
        }

        private void ReturnToRest()
        {
            Position = restPosition;
            Target.localRotation = restRotation;
            applied = false;
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
