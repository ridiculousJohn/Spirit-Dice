using System.Linq;
using Dice.Editor;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    /** The content validation tool's measurements on fabricated curves and a fabricated host. */
    public sealed class ContentValidationMeasurementTests
    {
        private const float Tolerance = .001f;

        [Test]
        public void LoopJoinMeasuresHowFarALoopsEndMissesItsStart()
        {
            var open = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(2f, .5f, 1f, 1f));
            var join = CurveLoopJoin.Measure(open, 2f);
            Assert.That(join.ValueJump, Is.EqualTo(.5f).Within(Tolerance));
            Assert.That(join.SlopeJump, Is.EqualTo(1f).Within(Tolerance));

            var closed = new AnimationCurve(new Keyframe(0f, 1f, .5f, .5f), new Keyframe(1f, 3f, 0f, 0f), new Keyframe(2f, 1f, .5f, .5f));
            join = CurveLoopJoin.Measure(closed, 2f);
            Assert.That(join.ValueJump, Is.LessThan(ContentValidation.ValueNoiseFloor));
            Assert.That(join.SlopeJump, Is.LessThan(ContentValidation.SlopeNoiseFloor));
        }

        [Test]
        public void HostTransitionJumpsAreTheLargestChangeOverTheSampledIdlePhases()
        {
            var room = new GameObject("Synthetic Host Room", typeof(RectTransform));
            var host = ScriptableObject.CreateInstance<SpiritHostPresentationAsset>();
            try
            {
                ((RectTransform)room.transform).sizeDelta = new Vector2(400f, 600f);
                host.ReferenceSize = new Vector2(400f, 600f);
                host.Layers = new[]
                {
                    new SpiritPortraitLayer { Id = "Figure", Texture = Texture2D.whiteTexture, Position = new Vector2(10f, 20f), Size = new Vector2(120f, 80f) }
                };
                // The idle carries the figure 4 units a second over a 2 s loop; the reaction, without idle or an entry weight,
                // drops it back to rest and replaces its alpha of 1 with 0.7.
                host.Sequences = new[]
                {
                    new SpiritPresentationSequence
                    {
                        Id = "Idle", State = SpiritPresentationState.Idle, Duration = 2f, Loop = true,
                        Tracks = new[] { Track(SpiritPortraitProperty.PositionX, SpiritTrackBlend.Add, AnimationCurve.Linear(0f, 0f, 2f, 8f)) }
                    },
                    new SpiritPresentationSequence
                    {
                        Id = "React", State = SpiritPresentationState.Reaction, Duration = .5f,
                        Tracks = new[] { Track(SpiritPortraitProperty.Alpha, SpiritTrackBlend.Replace, AnimationCurve.Constant(0f, .5f, .7f)) }
                    }
                };
                var portrait = SpiritPortraitTestAuthoring.Create(host, (RectTransform)room.transform);
                portrait.Build(host);

                var jumps = HostTransitionJumps.Measure(portrait, host, 8);
                HostTransitionJump Find(string transition, SpiritPortraitProperty property)
                    => jumps.Single(jump => jump.Transition == transition && jump.Layer == 0 && jump.Property == property);
                Assert.That(Find("Reaction start", SpiritPortraitProperty.Alpha).Jump, Is.EqualTo(.3f).Within(Tolerance));
                Assert.That(Find("Reaction end", SpiritPortraitProperty.Alpha).Jump, Is.EqualTo(.3f).Within(Tolerance));
                var position = Find("Reaction start", SpiritPortraitProperty.PositionX);
                Assert.That(position.Jump, Is.EqualTo(7f).Within(Tolerance), "The last of eight phases is 1.75 s into the loop.");
                Assert.That(position.Phase, Is.EqualTo(1.75f).Within(Tolerance));
                Assert.That(Find("Reaction start", SpiritPortraitProperty.Width).Jump, Is.EqualTo(0f));
                Assert.That(jumps.Any(jump => jump.Transition == "Arrival start"), Is.False, "The host authors no Arrival.");
            }
            finally
            {
                Object.DestroyImmediate(room);
                Object.DestroyImmediate(host);
            }
        }

        private static SpiritPortraitTrack Track(SpiritPortraitProperty property, SpiritTrackBlend blend, AnimationCurve curve)
            => new SpiritPortraitTrack { Layer = 0, Property = property, Blend = blend, Curve = curve };
    }
}
