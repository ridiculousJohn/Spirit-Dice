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
            Build();
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

        [TestCase(SpiritPresentationState.Victory)]
        [TestCase(SpiritPresentationState.Defeat)]
        [TestCase(SpiritPresentationState.Farewell)]
        public void OutcomeSeeksEachSequenceByNormalizedProgressAndHoldsItsTerminalPose(SpiritPresentationState state)
        {
            Array.Resize(ref presentation.Sequences, presentation.Sequences.Length + 2);
            presentation.Sequences[presentation.Sequences.Length - 2] = Sequence("Outcome head", state, 4f, false,
                Track(1, SpiritPortraitProperty.PositionY, SpiritTrackBlend.Replace, AnimationCurve.Linear(0f, 90f, 4f, 110f)));
            presentation.Sequences[presentation.Sequences.Length - 1] = Sequence("Outcome accent", state, 2f, false,
                Track(0, SpiritPortraitProperty.Rotation, SpiritTrackBlend.Replace, AnimationCurve.Linear(0f, 0f, 2f, 8f)));
            Build();

            Assert.That(portrait.GetDuration(state), Is.EqualTo(4f));
            portrait.SetState(state, .25f);
            Assert.That(Value(1, SpiritPortraitProperty.PositionY), Is.EqualTo(95f).Within(.0001f));
            Assert.That(Value(0, SpiritPortraitProperty.Rotation), Is.EqualTo(2f).Within(.0001f));
            portrait.Advance(20f);
            Assert.That(portrait.Progress, Is.EqualTo(.25f));
            Assert.That(Value(1, SpiritPortraitProperty.PositionY), Is.EqualTo(95f).Within(.0001f));
            portrait.SetState(state, 2f);
            portrait.Advance(20f);
            Assert.That(portrait.State, Is.EqualTo(state));
            Assert.That(portrait.Progress, Is.EqualTo(1f));
            Assert.That(Value(1, SpiritPortraitProperty.PositionY), Is.EqualTo(110f).Within(.0001f));
            Assert.That(Value(0, SpiritPortraitProperty.Rotation), Is.EqualTo(8f).Within(.0001f));
            portrait.SetState(state, -.1f);
            Assert.That(portrait.Progress, Is.Zero);
            Assert.That(Value(1, SpiritPortraitProperty.PositionY), Is.EqualTo(90f));
        }

        [TestCase(SpiritPresentationState.Victory)]
        [TestCase(SpiritPresentationState.Defeat)]
        [TestCase(SpiritPresentationState.Farewell)]
        public void OutcomeEntryWeightsPreserveReactionPoseAndDecayDuringDirectedSampling(SpiritPresentationState state)
        {
            var track = Track(1, SpiritPortraitProperty.PositionX, SpiritTrackBlend.Add, AnimationCurve.Linear(0f, 0f, 2f, 8f));
            track.EntryWeight = AnimationCurve.Linear(0f, 1f, 2f, 0f);
            Array.Resize(ref presentation.Sequences, presentation.Sequences.Length + 1);
            presentation.Sequences[presentation.Sequences.Length - 1] = Sequence("Outcome", state, 2f, false, track);
            Build();
            portrait.React();
            portrait.Advance(.75f);
            Assert.That(Value(1, SpiritPortraitProperty.PositionX), Is.EqualTo(73f).Within(.0001f));

            portrait.SetState(state, 0f);
            Assert.That(Value(1, SpiritPortraitProperty.PositionX), Is.EqualTo(73f).Within(.0001f));
            portrait.React();
            Assert.That(portrait.State, Is.EqualTo(state));
            portrait.SetState(state, .5f);
            portrait.Advance(2f);
            Assert.That(Value(1, SpiritPortraitProperty.PositionX), Is.EqualTo(75.5f).Within(.0001f));
            portrait.SetState(state, 1f);
            Assert.That(Value(1, SpiritPortraitProperty.PositionX), Is.EqualTo(78f).Within(.0001f));
        }

        [Test]
        public void DirectedOutcomeKeepsContinuousSmokeTimeAdvancing()
        {
            presentation.SmokeShader = Shader.Find("Dice/Spirit Smoke");
            presentation.Layers[0].Smoke.Enabled = true;
            Build();
            portrait.Advance(.5f);
            Material material = portrait.GetLayer("Distant Lantern").material;
            portrait.SetState(SpiritPresentationState.Victory, .4f);
            portrait.Advance(.25f);
            Assert.That(portrait.Progress, Is.EqualTo(.4f));
            Assert.That(material.GetFloat("_EffectTime"), Is.EqualTo(.75f).Within(.0001f));
        }

        [Test]
        public void MissingOutcomeDurationsReturnZeroAndExistingDurationsRemainAvailable()
        {
            Build();
            Assert.That(portrait.GetDuration(SpiritPresentationState.Victory), Is.Zero);
            Assert.That(portrait.GetDuration(SpiritPresentationState.Defeat), Is.Zero);
            Assert.That(portrait.GetDuration(SpiritPresentationState.Farewell), Is.Zero);
            Assert.That(portrait.GetDuration(SpiritPresentationState.Arrival), Is.EqualTo(5f));
            Assert.That(portrait.GetDuration(SpiritPresentationState.Reaction), Is.EqualTo(portrait.GestureDuration));
            Assert.Throws<ArgumentOutOfRangeException>(() => portrait.GetDuration((SpiritPresentationState)7));
        }

        [TestCase(SpiritPresentationState.Victory)]
        [TestCase(SpiritPresentationState.Defeat)]
        [TestCase(SpiritPresentationState.Farewell)]
        public void LoopingOutcomeSequencesAreRejected(SpiritPresentationState state)
        {
            presentation.Sequences[3].State = state;
            presentation.Sequences[3].Loop = true;
            Assert.Throws<InvalidOperationException>(() => presentation.Validate());
        }

        [Test]
        public void ArrivalEntryWeightsPreserveWaitingPoseAndDecayToAuthoredPose(
            [Values(.35f, 1.7f, 7.35f)] float waitingAge, [Values(false, true)] bool includeIdle)
        {
            presentation.Sequences[0].Tracks = new[]
            {
                Track(0, SpiritPortraitProperty.PositionX, SpiritTrackBlend.Replace, AnimationCurve.Linear(0f, 20f, 2f, 40f)),
                Track(0, SpiritPortraitProperty.PositionY, SpiritTrackBlend.Add, AnimationCurve.Linear(0f, 4f, 2f, 12f)),
                Track(0, SpiritPortraitProperty.Rotation, SpiritTrackBlend.Replace, AnimationCurve.Linear(0f, -10f, 2f, 10f)),
                Track(0, SpiritPortraitProperty.Deformation, SpiritTrackBlend.Add, AnimationCurve.Linear(0f, .1f, 2f, .9f))
            };
            presentation.Sequences[1].Tracks = Array.Empty<SpiritPortraitTrack>();
            SpiritPresentationSequence arrival = presentation.Sequences[2];
            arrival.IncludeIdle = includeIdle;
            arrival.Tracks = new[]
            {
                Track(0, SpiritPortraitProperty.PositionX, SpiritTrackBlend.Replace, AnimationCurve.Linear(0f, 50f, 5f, 90f)),
                Track(0, SpiritPortraitProperty.PositionY, SpiritTrackBlend.Add, AnimationCurve.Linear(0f, 10f, 5f, 30f)),
                Track(0, SpiritPortraitProperty.Rotation, SpiritTrackBlend.Replace, AnimationCurve.Linear(0f, 25f, 5f, 5f)),
                Track(0, SpiritPortraitProperty.Deformation, SpiritTrackBlend.Add, AnimationCurve.Linear(0f, .3f, 5f, .8f)),
                Track(1, SpiritPortraitProperty.PositionY, SpiritTrackBlend.Replace, AnimationCurve.Linear(0f, 0f, 5f, 200f))
            };
            for (int i = 0; i < 4; ++i) arrival.Tracks[i].EntryWeight = AnimationCurve.Linear(0f, 1f, 2f, 0f);
            Build();
            portrait.SetState(SpiritPresentationState.Waiting, 0f);
            portrait.Advance(waitingAge);
            float x = Value(0, SpiritPortraitProperty.PositionX);
            float y = Value(0, SpiritPortraitProperty.PositionY);
            float rotation = Value(0, SpiritPortraitProperty.Rotation);
            float deformation = Value(0, SpiritPortraitProperty.Deformation);

            portrait.SetState(SpiritPresentationState.Arrival, 0f);
            Assert.That(Value(0, SpiritPortraitProperty.PositionX), Is.EqualTo(x).Within(.0001f));
            Assert.That(Value(0, SpiritPortraitProperty.PositionY), Is.EqualTo(y).Within(.0001f));
            Assert.That(Value(0, SpiritPortraitProperty.Rotation), Is.EqualTo(rotation).Within(.0001f));
            Assert.That(Value(0, SpiritPortraitProperty.Deformation), Is.EqualTo(deformation).Within(.0001f));
            Assert.That(Value(1, SpiritPortraitProperty.PositionY), Is.Zero);

            portrait.SetState(SpiritPresentationState.Arrival, .2f);
            float expectedY = (includeIdle ? 28f : 20f) + 14f + (y - (includeIdle ? 24f : 20f) - 10f) * .5f;
            float expectedDeformation = (includeIdle ? .5f : 0f) + .4f + (deformation - (includeIdle ? .1f : 0f) - .3f) * .5f;
            for (int i = 0; i < 3; ++i)
            {
                portrait.SetState(SpiritPresentationState.Arrival, .2f);
                portrait.Advance(.5f);
                Assert.That(Value(0, SpiritPortraitProperty.PositionX), Is.EqualTo(58f + (x - 50f) * .5f).Within(.0001f));
                Assert.That(Value(0, SpiritPortraitProperty.PositionY), Is.EqualTo(expectedY).Within(.0001f));
                Assert.That(Value(0, SpiritPortraitProperty.Rotation), Is.EqualTo(21f + (rotation - 25f) * .5f).Within(.0001f));
                Assert.That(Value(0, SpiritPortraitProperty.Deformation), Is.EqualTo(expectedDeformation).Within(.0001f));
            }

            portrait.SetState(SpiritPresentationState.Arrival, .4f);
            Assert.That(Value(0, SpiritPortraitProperty.PositionX), Is.EqualTo(66f).Within(.0001f));
            Assert.That(Value(0, SpiritPortraitProperty.PositionY), Is.EqualTo(includeIdle ? 42f : 38f).Within(.0001f));
            Assert.That(Value(0, SpiritPortraitProperty.Rotation), Is.EqualTo(17f).Within(.0001f));
            Assert.That(Value(0, SpiritPortraitProperty.Deformation), Is.EqualTo(includeIdle ? .6f : .5f).Within(.0001f));

            portrait.SetState(SpiritPresentationState.Arrival, 1f);
            Assert.That(Value(0, SpiritPortraitProperty.PositionX), Is.EqualTo(90f).Within(.0001f));
            Assert.That(Value(0, SpiritPortraitProperty.PositionY), Is.EqualTo(includeIdle ? 58f : 50f).Within(.0001f));
            Assert.That(Value(0, SpiritPortraitProperty.Rotation), Is.EqualTo(5f).Within(.0001f));
            Assert.That(Value(0, SpiritPortraitProperty.Deformation), Is.EqualTo(includeIdle ? 1.3f : .8f).Within(.0001f));
            Assert.That(Value(1, SpiritPortraitProperty.PositionY), Is.EqualTo(200f).Within(.0001f));
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
        public void ReactionEntryWeightsPreserveInterruptedPoseWhenIdleRestarts()
        {
            presentation.Sequences[0].Tracks[0].Curve = AnimationCurve.Linear(0f, 3f, 2f, 7f);
            presentation.Sequences[0].RestartOnReaction = true;
            SpiritPresentationSequence reaction = presentation.Sequences[3];
            reaction.IncludeIdle = true;
            var track = Track(0, SpiritPortraitProperty.PositionX, SpiritTrackBlend.Add,
                AnimationCurve.Linear(0f, 2f, reaction.Duration, 10f));
            track.EntryWeight = AnimationCurve.Linear(0f, 1f, reaction.Duration, 0f);
            reaction.Tracks = new[] { track };
            Build();
            portrait.Advance(.75f);
            Assert.That(Value(0, SpiritPortraitProperty.PositionX), Is.EqualTo(14.5f).Within(.0001f));

            portrait.React();
            Assert.That(Value(0, SpiritPortraitProperty.PositionX), Is.EqualTo(14.5f).Within(.0001f));
            portrait.Advance(.5f);
            float interrupted = Value(0, SpiritPortraitProperty.PositionX);
            Assert.That(interrupted, Is.EqualTo(14f + 2f + 8f * .5f / reaction.Duration
                - .5f * (1f - .5f / reaction.Duration)).Within(.0001f));
            portrait.React();
            Assert.That(Value(0, SpiritPortraitProperty.PositionX), Is.EqualTo(interrupted).Within(.0001f));
            portrait.Advance(reaction.Duration);
            Assert.That(portrait.IsReacting, Is.False);
            Assert.That(Value(0, SpiritPortraitProperty.PositionX), Is.EqualTo(15.5f).Within(.0001f));
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

        private void Build()
        {
            if (portrait == null) portrait = SpiritPortraitTestAuthoring.Create(presentation, host.GetComponent<RectTransform>());
            portrait.Build((RectTransform)portrait.transform, presentation);
        }
        private float Value(int layer, SpiritPortraitProperty property) => portrait.ReadLayerValue(layer, property);

        private static SpiritPortraitTrack Track(int layer, SpiritPortraitProperty property, SpiritTrackBlend blend,
            AnimationCurve curve) => new SpiritPortraitTrack { Layer = layer, Property = property, Blend = blend, Curve = curve };

        private static SpiritPresentationSequence Sequence(string id, SpiritPresentationState state, float duration,
            bool loop, params SpiritPortraitTrack[] tracks)
            => new SpiritPresentationSequence { Id = id, State = state, Duration = duration, Loop = loop, Tracks = tracks };
    }
}
