using System;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class SpiritHostPortraitTests
    {
        private GameObject host;
        private SpiritHostPortrait portrait;
        private SpiritHostPresentationAsset presentation;
        private SpiritPortraitMeshAsset mesh;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Third Host Test", typeof(RectTransform));
            host.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 600f);
            mesh = ScriptableObject.CreateInstance<SpiritPortraitMeshAsset>();
            mesh.Shapes = new[]
            {
                new SpiritPortraitShape
                {
                    Id = "Top sway",
                    Offsets = new[] { Vector2.zero, Vector2.zero, new Vector2(.1f, .2f), new Vector2(.1f, .2f) }
                }
            };
            presentation = ScriptableObject.CreateInstance<SpiritHostPresentationAsset>();
            presentation.ReferenceSize = new Vector2(400f, 600f);
            presentation.Layers = new[]
            {
                new SpiritPortraitLayer
                {
                    Id = "Distant Lantern", Texture = Texture2D.whiteTexture, Mesh = mesh,
                    Position = new Vector2(10f, 20f), Size = new Vector2(120f, 80f),
                    Pivot = new Vector2(.5f, 0f), Weights = new[] { 0f }
                },
                new SpiritPortraitLayer
                {
                    Id = "Loose Ribbon", Texture = Texture2D.whiteTexture,
                    Position = new Vector2(70f, 90f), Size = new Vector2(30f, 50f)
                }
            };
            presentation.Sequences = new[]
            {
                Sequence("Sway", SpiritPresentationState.Idle, 2f, true,
                    Track(0, SpiritPortraitProperty.PositionX, SpiritTrackBlend.Add, AnimationCurve.Linear(0f, 0f, 2f, 4f))),
                Sequence("Waiting", SpiritPresentationState.Waiting, 1f, false,
                    Track(0, SpiritPortraitProperty.PositionX, SpiritTrackBlend.Replace, AnimationCurve.Constant(0f, 1f, 100f))),
                Sequence("Approach", SpiritPresentationState.Arrival, 5f, false,
                    Track(1, SpiritPortraitProperty.PositionY, SpiritTrackBlend.Replace, AnimationCurve.Linear(0f, 0f, 5f, 200f))),
                Sequence("Greeting", SpiritPresentationState.Reaction, 3.25f, false,
                    Track(1, SpiritPortraitProperty.PositionX, SpiritTrackBlend.Add, AnimationCurve.Linear(0f, 0f, 3.25f, 13f)))
            };
            presentation.Sequences[3].GestureOnStart = true;
            portrait = host.AddComponent<SpiritHostPortrait>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(presentation);
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        [Test]
        public void ThirdHostBuildsArbitraryAuthoredLayersWithOneRenderer()
        {
            portrait.Build(host.GetComponent<RectTransform>(), presentation);
            Assert.That(portrait.GetLayer("Distant Lantern").texture, Is.SameAs(Texture2D.whiteTexture));
            Assert.That(portrait.GetLayer("Loose Ribbon").rectTransform.anchoredPosition, Is.EqualTo(new Vector2(70f, 90f)));
            Assert.That(host.GetComponentsInChildren<SpiritPortraitImage>().Length, Is.EqualTo(2));
            Assert.That(portrait.GetLayer("Distant Lantern").transform.GetSiblingIndex(), Is.LessThan(
                portrait.GetLayer("Loose Ribbon").transform.GetSiblingIndex()));
            Assert.That(portrait.GestureDuration, Is.EqualTo(3.25f));
            foreach (var layer in host.GetComponentsInChildren<SpiritPortraitImage>()) Assert.That(layer.raycastTarget, Is.False);
        }

        [Test]
        public void ReactionUsesAuthoredDurationAndRepeatedScoringRestartsIt()
        {
            Build();
            int gestures = 0;
            portrait.SpiritGesture += () => ++gestures;
            portrait.React();
            portrait.Advance(2f);
            Assert.That(Value(1, SpiritPortraitProperty.PositionX), Is.EqualTo(78f).Within(.0001f));
            portrait.React();
            Assert.That(Value(1, SpiritPortraitProperty.PositionX), Is.EqualTo(70f));
            portrait.Advance(3f);
            Assert.That(portrait.IsReacting, Is.True);
            portrait.Advance(.26f);
            Assert.That(portrait.IsReacting, Is.False);
            Assert.That(portrait.State, Is.EqualTo(SpiritPresentationState.Idle));
            Assert.That(gestures, Is.EqualTo(2));
        }

        [Test]
        public void RefreshingIdlePreservesReactionAndWaitingCancelsIt()
        {
            Build();
            portrait.React();
            portrait.Advance(1f);
            float pose = Value(1, SpiritPortraitProperty.PositionX);
            portrait.SetState(SpiritPresentationState.Idle, 1f);
            portrait.SetVisible(true);
            Assert.That(portrait.IsReacting, Is.True);
            Assert.That(Value(1, SpiritPortraitProperty.PositionX), Is.EqualTo(pose));
            portrait.SetState(SpiritPresentationState.Waiting, 0f);
            Assert.That(portrait.IsReacting, Is.False);
            Assert.That(Value(0, SpiritPortraitProperty.PositionX), Is.EqualTo(100f));
            Assert.That(Value(1, SpiritPortraitProperty.PositionX), Is.EqualTo(70f));
        }

        [Test]
        public void ArrivalSeeksInNormalizedProgressAndStaysFixedWhenAdvanced()
        {
            Build();
            portrait.SetState(SpiritPresentationState.Arrival, .4f);
            Assert.That(Value(1, SpiritPortraitProperty.PositionY), Is.EqualTo(80f).Within(.0001f));
            portrait.Advance(100f);
            Assert.That(Value(1, SpiritPortraitProperty.PositionY), Is.EqualTo(80f).Within(.0001f));
            portrait.SetState(SpiritPresentationState.Arrival, .9f);
            portrait.ResetPose();
            portrait.SetState(SpiritPresentationState.Arrival, .4f);
            Assert.That(Value(1, SpiritPortraitProperty.PositionY), Is.EqualTo(80f).Within(.0001f));
            Assert.That(portrait.Progress, Is.EqualTo(.4f));
        }

        [Test]
        public void ArrivalGesturesFollowForwardSeeksWithoutReplayingSkippedMarkers()
        {
            presentation.Sequences[2].GestureOnStart = true;
            presentation.Sequences[2].GestureTimes = new[] { 1f, 3f, 4f };
            Build();
            int gestures = 0;
            portrait.SpiritGesture += () => ++gestures;
            portrait.SetState(SpiritPresentationState.Arrival, .5f);
            Assert.That(gestures, Is.EqualTo(1));
            portrait.Advance(100f);
            Assert.That(gestures, Is.EqualTo(1));
            portrait.SetState(SpiritPresentationState.Arrival, .8f);
            Assert.That(gestures, Is.EqualTo(3));
            portrait.SetState(SpiritPresentationState.Arrival, .8f);
            portrait.SetState(SpiritPresentationState.Arrival, .2f);
            Assert.That(gestures, Is.EqualTo(3));
            portrait.SetState(SpiritPresentationState.Arrival, .6f);
            Assert.That(gestures, Is.EqualTo(4));
            portrait.SetVisible(false);
            portrait.SetState(SpiritPresentationState.Arrival, 1f);
            Assert.That(gestures, Is.EqualTo(4));
        }
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidTimeStepsPreserveReactionPoseAndClock(float deltaTime)
        {
            Build();
            portrait.React();
            portrait.Advance(1f);
            float pose = Value(1, SpiritPortraitProperty.PositionX);
            portrait.Advance(deltaTime);
            Assert.That(Value(1, SpiritPortraitProperty.PositionX), Is.EqualTo(pose));
            portrait.Advance(2f);
            Assert.That(portrait.IsReacting, Is.True);
            portrait.Advance(.26f);
            Assert.That(portrait.IsReacting, Is.False);
        }

        [Test]
        public void LoopingIdleUsesSecondsAndRefreshPreservesItsPhase()
        {
            Build();
            portrait.Advance(2.5f);
            Assert.That(Value(0, SpiritPortraitProperty.PositionX), Is.EqualTo(11f).Within(.0001f));
            portrait.SetState(SpiritPresentationState.Idle, 1f);
            portrait.Advance(.5f);
            Assert.That(Value(0, SpiritPortraitProperty.PositionX), Is.EqualTo(12f).Within(.0001f));
        }

        [Test]
        public void IndependentIdleSequencesKeepTheirOwnLoopDurationsDuringReaction()
        {
            var sequences = new SpiritPresentationSequence[presentation.Sequences.Length + 1];
            Array.Copy(presentation.Sequences, sequences, presentation.Sequences.Length);
            sequences[sequences.Length - 1] = Sequence("Ribbon sway", SpiritPresentationState.Idle, 3f, true,
                Track(1, SpiritPortraitProperty.PositionY, SpiritTrackBlend.Add, AnimationCurve.Linear(0f, 0f, 3f, 9f)));
            presentation.Sequences = sequences;
            presentation.Sequences[3].IncludeIdle = true;
            Build();
            portrait.Advance(2.5f);
            Assert.That(Value(0, SpiritPortraitProperty.PositionX), Is.EqualTo(11f).Within(.0001f));
            Assert.That(Value(1, SpiritPortraitProperty.PositionY), Is.EqualTo(97.5f).Within(.0001f));
            portrait.React();
            portrait.Advance(1f);
            Assert.That(Value(0, SpiritPortraitProperty.PositionX), Is.EqualTo(13f).Within(.0001f));
            Assert.That(Value(1, SpiritPortraitProperty.PositionY), Is.EqualTo(91.5f).Within(.0001f));
        }
        [Test]
        public void WeightedReplacementAndAdditionCombineOnAuthoredBasePose()
        {
            var replace = Track(0, SpiritPortraitProperty.PositionX, SpiritTrackBlend.Replace,
                AnimationCurve.Constant(0f, 3.25f, 50f));
            replace.EntryWeight = AnimationCurve.Constant(0f, 3.25f, .25f);
            presentation.Sequences[3].Tracks = new[]
            {
                replace,
                Track(0, SpiritPortraitProperty.PositionX, SpiritTrackBlend.Add, AnimationCurve.Constant(0f, 3.25f, 3f))
            };
            Build();
            portrait.React();
            portrait.Advance(1f);
            Assert.That(Value(0, SpiritPortraitProperty.PositionX), Is.EqualTo(43f).Within(.0001f));
            Assert.That(portrait.GetLayer("Distant Lantern").rectTransform.anchoredPosition.x,
                Is.EqualTo(43f).Within(.0001f));
        }

        [Test]
        public void LoopGestureMarkersFireWhenCrossedAndDoNotRepeatAtTheSameTime()
        {
            presentation.Sequences[0].GestureTimes = new[] { .5f, 1.5f };
            Build();
            int gestures = 0;
            portrait.SpiritGesture += () => ++gestures;
            portrait.Advance(.5f);
            Assert.That(gestures, Is.EqualTo(1));
            portrait.Advance(0f);
            Assert.That(gestures, Is.EqualTo(1));
            portrait.Advance(2f);
            Assert.That(gestures, Is.EqualTo(3));
        }

        [Test]
        public void MeshWeightsMoveTheHeadAndLeaveTheFootContactPlanted()
        {
            Build();
            var image = portrait.GetLayer("Distant Lantern");
            image.SetWeights(new[] { .5f });
            var size = new Vector2(120f, 80f);
            Assert.That(image.EvaluateDisplacement(new Vector2(.5f, 0f), size), Is.EqualTo(Vector2.zero));
            Assert.That(image.EvaluateDisplacement(new Vector2(.5f, 1f), size), Is.EqualTo(new Vector2(6f, 8f)));
            image.SetWeights(new[] { 1f });
            Assert.That(image.EvaluateDisplacement(new Vector2(.5f, .5f), size), Is.EqualTo(new Vector2(6f, 8f)));
        }

        [TestCase("duplicate layer")]
        [TestCase("missing target")]
        [TestCase("missing shape")]
        [TestCase("zero duration")]
        [TestCase("reaction loop")]
        [TestCase("invalid curve")]
        [TestCase("invalid mesh")]
        [TestCase("invalid weights")]
        [TestCase("invalid alpha")]
        public void InvalidAuthoredDataIsRejectedBeforeBuilding(string error)
        {
            switch (error)
            {
                case "duplicate layer": presentation.Layers[1].Id = presentation.Layers[0].Id; break;
                case "missing target": presentation.Sequences[0].Tracks[0].Layer = 2; break;
                case "missing shape": presentation.Sequences[0].Tracks[0].Property = SpiritPortraitProperty.Deformation;
                    presentation.Sequences[0].Tracks[0].Channel = 1; break;
                case "zero duration": presentation.Sequences[0].Duration = 0f; break;
                case "reaction loop": presentation.Sequences[3].Loop = true; break;
                case "invalid curve": presentation.Sequences[0].Tracks[0].Curve = new AnimationCurve(); break;
                case "invalid mesh": mesh.Shapes[0].Offsets = new Vector2[3]; break;
                case "invalid weights": presentation.Layers[0].Weights = new[] { float.NaN }; break;
                case "invalid alpha": mesh.Alpha = new[] { 0f, 0f, 0f, 2f }; break;
            }
            Assert.Throws<InvalidOperationException>(() => presentation.Validate());
            Assert.Throws<InvalidOperationException>(() => Build());
            Assert.That(host.GetComponentsInChildren<RawImage>().Length, Is.Zero);
        }

        private void Build() => portrait.Build(host.GetComponent<RectTransform>(), presentation);
        private float Value(int layer, SpiritPortraitProperty property) => portrait.ReadLayerValue(layer, property);

        private static SpiritPortraitTrack Track(int layer, SpiritPortraitProperty property, SpiritTrackBlend blend,
            AnimationCurve curve) => new SpiritPortraitTrack { Layer = layer, Property = property, Blend = blend, Curve = curve };

        private static SpiritPresentationSequence Sequence(string id, SpiritPresentationState state, float duration,
            bool loop, params SpiritPortraitTrack[] tracks)
            => new SpiritPresentationSequence { Id = id, State = state, Duration = duration, Loop = loop, Tracks = tracks };
    }
}
