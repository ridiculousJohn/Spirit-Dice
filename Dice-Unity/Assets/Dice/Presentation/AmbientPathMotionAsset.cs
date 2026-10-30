using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * A creature leaves its hiding place, moves through authored waypoints at a steady speed and hides again, once per idle run.
     * It moves only on the idle schedule: jolts pass it by, and when the activity settles the run finishes along its path.
     */
    [CreateAssetMenu(menuName = "Dice/Presentation/Ambient Motion/Path")]
    public sealed class AmbientPathMotionAsset : AmbientMotionAsset
    {
        /** One point of the path and how long the prop waits there. */
        [Serializable]
        public struct Waypoint
        {
            [Tooltip("Offset in parent units from the prop's rest position (its anchored position for a RectTransform).")]
            public Vector2 Position;
            [Tooltip("Seconds the prop waits at this point before moving on.")]
            [Min(0)] public float Pause;

            public Waypoint(Vector2 position, float pause = 0f)
            {
                Position = position;
                Pause = pause;
            }
        }

        [Tooltip("Points the prop moves through in order; it starts at the first and ends at the last, both hidden by the art drawn over them.")]
        [SerializeField] private Waypoint[] waypoints = Array.Empty<Waypoint>();
        [Tooltip("Parent units per second along the path.")]
        [SerializeField, Min(0)] private float speed = 30f;
        [Tooltip("Turns the prop to face its direction of travel; otherwise it keeps its rest rotation.")]
        [SerializeField] private bool faceTravel = true;
        [Tooltip("Degrees the prop turns from its rest rotation while travelling right (+x); travelling at any other angle adds that angle.")]
        [SerializeField] private float facingOffset;

        public IReadOnlyList<Waypoint> Waypoints => waypoints ?? Array.Empty<Waypoint>();
        public float Speed => speed;
        public bool FaceTravel => faceTravel;
        public float FacingOffset => facingOffset;

        public override AmbientMotion Create(Transform target) => new AmbientPathMotion(target, this);

        public override void Validate(string owner)
        {
            string path = owner + ": path motion " + name;
            var points = Waypoints;
            if (points.Count < 2) throw new InvalidOperationException(path + " requires at least two waypoints.");
            for (int i = 0; i < points.Count; ++i)
            {
                if (!FiniteValue.Is(points[i].Position)) throw new InvalidOperationException(path + " requires finite waypoint " + i + ".");
                if (!FiniteValue.Is(points[i].Pause) || points[i].Pause < 0f)
                    throw new InvalidOperationException(path + " requires a finite nonnegative pause at waypoint " + i + ".");
            }
            if (!FiniteValue.Is(speed) || speed <= 0f) throw new InvalidOperationException(path + " requires a finite positive speed.");
            if (!FiniteValue.Is(facingOffset)) throw new InvalidOperationException(path + " requires a finite facing offset.");
        }
    }

    /**
     * One run through the asset's waypoints, offset from the rest pose, waiting where authored and optionally turned to face
     * its travel; the strike's strength does not change the run. While waiting the prop keeps the facing it arrived with,
     * and before it first moves it faces its first leg. When the run ends the rest pose is written back.
     */
    public sealed class AmbientPathMotion : AmbientMotion
    {
        private readonly AmbientPathMotionAsset settings;

        public AmbientPathMotion(Transform target, AmbientPathMotionAsset settings) : base(target)
        {
            this.settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
        }

        public override bool AnswersJolts => false;
        protected override bool RunsToEnd => true;

        protected override float Duration(float strength)
        {
            var points = settings.Waypoints;
            float duration = 0f;
            for (int i = 0; i < points.Count; ++i)
            {
                duration += points[i].Pause;
                if (i + 1 < points.Count) duration += Vector2.Distance(points[i].Position, points[i + 1].Position) / settings.Speed;
            }
            return duration;
        }

        protected override void Evaluate(float elapsed, float strength, ref float angle, ref Vector2 offset)
        {
            var points = settings.Waypoints;
            if (points.Count == 0) return;
            float heading = FirstHeading(points);
            Vector2 position = points[points.Count - 1].Position;
            for (int i = 0; i < points.Count; ++i)
            {
                if (elapsed < points[i].Pause)
                {
                    position = points[i].Position;
                    break;
                }
                elapsed -= points[i].Pause;
                if (i + 1 == points.Count) break;
                Vector2 from = points[i].Position, to = points[i + 1].Position;
                float length = Vector2.Distance(from, to);
                // A leg of zero length has no direction, so the prop keeps the facing it had.
                if (length > 0f) heading = Heading(to - from);
                float travel = length / settings.Speed;
                if (elapsed < travel)
                {
                    position = Vector2.Lerp(from, to, elapsed / travel);
                    break;
                }
                elapsed -= travel;
            }
            offset += position;
            if (settings.FaceTravel) angle += heading + settings.FacingOffset;
        }

        private static float FirstHeading(IReadOnlyList<AmbientPathMotionAsset.Waypoint> points)
        {
            for (int i = 0; i + 1 < points.Count; ++i)
                if (Vector2.Distance(points[i].Position, points[i + 1].Position) > 0f) return Heading(points[i + 1].Position - points[i].Position);
            return 0f;
        }

        private static float Heading(Vector2 direction) => Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }
}
