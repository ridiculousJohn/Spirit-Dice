using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    /** The host portrait's state machine on a synthetic two-layer host whose sequences each test supplies. */
    public sealed class SpiritHostPortraitTests
    {
        private const int Figure = 0, Ribbon = 1;
        private const float Tolerance = .0001f;
        private const float RibbonRestX = 70f;
        private static readonly Vector2 FigureRest = new Vector2(10f, 20f);
        private const SpiritPresentationState Idle = SpiritPresentationState.Idle;
        private const SpiritPresentationState Waiting = SpiritPresentationState.Waiting;
        private const SpiritPresentationState Arrival = SpiritPresentationState.Arrival;
        private const SpiritPresentationState Reaction = SpiritPresentationState.Reaction;
        private const SpiritPresentationState Victory = SpiritPresentationState.Victory;
        private const SpiritPortraitProperty X = SpiritPortraitProperty.PositionX;
        private const SpiritPortraitProperty Y = SpiritPortraitProperty.PositionY;
        private const SpiritPortraitProperty Rotation = SpiritPortraitProperty.Rotation;
        private const SpiritTrackBlend Add = SpiritTrackBlend.Add;
        private const SpiritTrackBlend Replace = SpiritTrackBlend.Replace;
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
        private GameObject room;
        private SpiritPortraitMeshAsset mesh;
        private SpiritHostPresentationAsset presentation;
        private SpiritHostPortrait portrait;

        [SetUp]
        public void SetUp()
        {
            room = new GameObject("Synthetic Host Room", typeof(RectTransform));
            Room.sizeDelta = new Vector2(400f, 600f);
            mesh = ScriptableObject.CreateInstance<SpiritPortraitMeshAsset>();
            mesh.Shapes = new[]
            {
                new SpiritPortraitShape { Id = "Lift", Offsets = new[] { Vector2.zero, Vector2.zero, new Vector2(.1f, .2f), new Vector2(.1f, .2f) } }
            };
            presentation = ScriptableObject.CreateInstance<SpiritHostPresentationAsset>();
            presentation.ReferenceSize = new Vector2(400f, 600f);
            presentation.Layers = new[]
            {
                new SpiritPortraitLayer
                {
                    Id = "Figure", Texture = Texture2D.whiteTexture, Mesh = mesh, Position = FigureRest,
                    Size = new Vector2(120f, 80f), Pivot = new Vector2(.5f, 0f)
                },
                new SpiritPortraitLayer
                {
                    Id = "Ribbon", Texture = Texture2D.whiteTexture, Position = new Vector2(RibbonRestX, 90f), Size = new Vector2(30f, 50f)
                }
            };
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(room);
            UnityEngine.Object.DestroyImmediate(presentation);
            UnityEngine.Object.DestroyImmediate(mesh);
            foreach (UnityEngine.Object value in created)
                if (value != null) UnityEngine.Object.DestroyImmediate(value);
            created.Clear();
        }

        [Test]
        public void BuildRequiresAnAuthoredImageForEveryLayer()
        {
            portrait = SpiritPortraitTestAuthoring.Create(presentation, Room);
            var wider = Own(ScriptableObject.CreateInstance<SpiritHostPresentationAsset>());
            wider.ReferenceSize = presentation.ReferenceSize;
            wider.Layers = presentation.Layers.Append(new SpiritPortraitLayer
                { Id = "Extra", Texture = Texture2D.whiteTexture, Size = Vector2.one }).ToArray();
            Assert.Throws<InvalidOperationException>(() => portrait.Build((RectTransform)portrait.transform, wider));

            portrait.Build((RectTransform)portrait.transform, presentation);
            Assert.That(portrait.GetLayer("Ribbon"), Is.Not.Null);
            Assert.That(portrait.GetLayer("Extra"), Is.Null);
            Assert.That(Value(Ribbon, X), Is.EqualTo(RibbonRestX), "The rest pose is read from the authored image.");
        }

        [TestCase("duplicate layer id")]
        [TestCase("track layer out of range")]
        [TestCase("deformation channel without a shape")]
        [TestCase("nonpositive duration")]
        [TestCase("looping reaction")]
        [TestCase("looping outcome")]
        [TestCase("empty curve")]
        [TestCase("shape offsets off the grid")]
        [TestCase("nonfinite layer weight")]
        [TestCase("mesh alpha above one")]
        public void ValidateRejectsInvalidAuthoredData(string error)
        {
            SpiritPortraitTrack track = Track(Figure, X, Add, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            SpiritPresentationSequence reaction = Sequence("React", Reaction, 1f, false);
            SpiritPresentationSequence outcome = Sequence("Win", Victory, 1f, false);
            presentation.Sequences = new[] { Sequence("Idle", Idle, 1f, true, track), reaction, outcome };
            Assert.DoesNotThrow(presentation.Validate, "The unmodified host is valid.");
            switch (error)
            {
                case "duplicate layer id": presentation.Layers[Ribbon].Id = presentation.Layers[Figure].Id; break;
                case "track layer out of range": track.Layer = presentation.Layers.Length; break;
                case "deformation channel without a shape":
                    track.Property = SpiritPortraitProperty.Deformation;
                    track.Channel = mesh.Shapes.Length;
                    break;
                case "nonpositive duration": presentation.Sequences[0].Duration = 0f; break;
                case "looping reaction": reaction.Loop = true; break;
                case "looping outcome": outcome.Loop = true; break;
                case "empty curve": track.Curve = new AnimationCurve(); break;
                case "shape offsets off the grid": mesh.Shapes[0].Offsets = new Vector2[3]; break;
                case "nonfinite layer weight": presentation.Layers[Figure].Weights = new[] { float.NaN }; break;
                case "mesh alpha above one": mesh.Alpha = new[] { 0f, 0f, 0f, 2f }; break;
            }
            Assert.Throws<InvalidOperationException>(presentation.Validate);
        }

        [Test]
        public void ReactionPlaysForItsDurationThenDropsItsTracks()
        {
            SpiritPresentationSequence reaction = RibbonReaction();
            reaction.GestureOnStart = true;
            Build(reaction);
            int gestures = 0;
            portrait.SpiritGesture += () => ++gestures;

            portrait.React();
            portrait.Advance(1f);
            Assert.That(portrait.IsReacting, Is.True);
            Assert.That(Value(Ribbon, X), Is.EqualTo(RibbonRestX + 4f).Within(Tolerance));
            portrait.React();
            Assert.That(Value(Ribbon, X), Is.EqualTo(RibbonRestX).Within(Tolerance), "A repeated reaction restarts.");
            Assert.That(gestures, Is.EqualTo(2));
            portrait.Advance(2.9f);
            Assert.That(portrait.IsReacting, Is.True);
            portrait.Advance(.2f);
            Assert.That(portrait.IsReacting, Is.False);
            Assert.That(portrait.State, Is.EqualTo(Idle));
            Assert.That(Value(Ribbon, X), Is.EqualTo(RibbonRestX), "The ended reaction no longer moves its layer.");

            portrait.SetState(Waiting, 0f);
            portrait.React();
            Assert.That(portrait.IsReacting, Is.False, "Only an idle host reacts.");
            Assert.That(gestures, Is.EqualTo(2));
        }

        [Test]
        public void SettingIdleAgainKeepsTheRunningIdleAndReactionWhileAnotherStateEndsThem()
        {
            Build(FigureIdle(), RibbonReaction());
            portrait.Advance(.5f);
            portrait.SetState(Idle, 1f);
            portrait.Advance(.5f);
            Assert.That(Value(Figure, X), Is.EqualTo(FigureRest.x + 2f).Within(Tolerance), "The idle keeps its phase.");

            portrait.React();
            portrait.Advance(1f);
            portrait.SetState(Idle, 1f);
            Assert.That(portrait.IsReacting, Is.True);
            Assert.That(Value(Ribbon, X), Is.EqualTo(RibbonRestX + 4f).Within(Tolerance));

            portrait.SetState(Waiting, 0f);
            Assert.That(portrait.IsReacting, Is.False);
            Assert.That(Value(Ribbon, X), Is.EqualTo(RibbonRestX).Within(Tolerance));
        }

        [Test]
        public void IdleSequencesLoopOnTheirOwnDurations()
        {
            Build(Sequence("Short", Idle, 2f, true, Track(Figure, X, Add, AnimationCurve.Linear(0f, 0f, 2f, 4f))),
                Sequence("Long", Idle, 3f, true, Track(Ribbon, Y, Add, AnimationCurve.Linear(0f, 0f, 3f, 9f))));
            portrait.Advance(2.5f);
            Assert.That(Value(Figure, X), Is.EqualTo(FigureRest.x + 1f).Within(Tolerance), "The two-second loop has wrapped.");
            Assert.That(Value(Ribbon, Y), Is.EqualTo(90f + 7.5f).Within(Tolerance), "The three-second loop has not.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ReactionKeepsTheIdleRunningUnderneathOnlyWhenItIncludesIdle(bool includeIdle)
        {
            SpiritPresentationSequence reaction = RibbonReaction();
            reaction.IncludeIdle = includeIdle;
            Build(FigureIdle(), reaction);
            portrait.Advance(.5f);
            portrait.React();
            portrait.Advance(.5f);
            Assert.That(Value(Figure, X), Is.EqualTo(FigureRest.x + (includeIdle ? 2f : 0f)).Within(Tolerance));
        }

        [Test]
        public void ReactionRestartsOnlyTheIdleSequencesMarkedToRestart()
        {
            SpiritPresentationSequence restarting = Sequence("Restarting", Idle, 4f, true,
                Track(Figure, X, Add, AnimationCurve.Linear(0f, 0f, 4f, 8f)));
            restarting.RestartOnReaction = true;
            SpiritPresentationSequence reaction = Sequence("React", Reaction, 1f, false);
            reaction.IncludeIdle = true;
            Build(restarting, Sequence("Continuing", Idle, 4f, true, Track(Ribbon, X, Add, AnimationCurve.Linear(0f, 0f, 4f, 8f))), reaction);
            portrait.Advance(1.5f);
            portrait.React();
            portrait.Advance(.5f);
            Assert.That(Value(Figure, X), Is.EqualTo(FigureRest.x + 1f).Within(Tolerance), "The marked idle restarted at the reaction.");
            Assert.That(Value(Ribbon, X), Is.EqualTo(RibbonRestX + 4f).Within(Tolerance), "The other idle kept running.");
        }

        [Test]
        public void ReactionEntryKeepsTheCurrentPoseAndDecaysIntoTheReaction()
        {
            SpiritPortraitTrack weighted = Weighted(Track(Figure, X, Add, AnimationCurve.Constant(0f, 2f, 6f)));
            SpiritPresentationSequence reaction = Sequence("React", Reaction, 2f, false,
                weighted, Track(Figure, Y, Add, AnimationCurve.Constant(0f, 2f, 6f)));
            reaction.IncludeIdle = true;
            Build(Sequence("Idle", Idle, 2f, true, Track(Figure, X, Add, AnimationCurve.Linear(0f, 0f, 2f, 4f)),
                Track(Figure, Y, Add, AnimationCurve.Linear(0f, 0f, 2f, 4f))), reaction);
            portrait.Advance(.75f);
            float x = Value(Figure, X), y = Value(Figure, Y);

            portrait.React();
            Assert.That(Value(Figure, X), Is.EqualTo(x).Within(Tolerance), "A weighted track starts from the current pose.");
            Assert.That(Value(Figure, Y), Is.EqualTo(y + 6f).Within(Tolerance), "A track without an entry weight starts at its curve.");
            portrait.Advance(1f);
            // The idle at 1.75 s plus the reaction's 6, less half the entry offset of 6.
            Assert.That(Value(Figure, X), Is.EqualTo(FigureRest.x + 3.5f + 6f - 3f).Within(Tolerance));
        }

        [Test]
        public void EnteringAStateCarriesThePreviousPoseThroughItsEntryWeights()
        {
            Build(Sequence("Wait", Waiting, 2f, true,
                    Track(Figure, X, Replace, AnimationCurve.Linear(0f, 0f, 2f, 40f)),
                    Track(Figure, Y, Add, AnimationCurve.Linear(0f, 0f, 2f, 40f)),
                    Track(Figure, Rotation, Replace, AnimationCurve.Linear(0f, 0f, 2f, 40f))),
                Sequence("Arrive", Arrival, 4f, false,
                    Weighted(Track(Figure, X, Replace, AnimationCurve.Constant(0f, 4f, 100f))),
                    Weighted(Track(Figure, Y, Add, AnimationCurve.Constant(0f, 4f, 100f))),
                    Track(Figure, Rotation, Replace, AnimationCurve.Constant(0f, 4f, 100f))));
            portrait.SetState(Waiting, 0f);
            portrait.Advance(1.5f);
            float x = Value(Figure, X), y = Value(Figure, Y);

            portrait.SetState(Arrival, 0f);
            Assert.That(Value(Figure, X), Is.EqualTo(x).Within(Tolerance), "A weighted replacement starts from the previous pose.");
            Assert.That(Value(Figure, Y), Is.EqualTo(y).Within(Tolerance), "A weighted addition starts from the previous pose.");
            Assert.That(Value(Figure, Rotation), Is.EqualTo(100f).Within(Tolerance), "A track without an entry weight starts at its curve.");
            // Halfway through the entry weight, halfway from the previous pose to the curve.
            portrait.SetState(Arrival, .25f);
            Assert.That(Value(Figure, X), Is.EqualTo(100f + (x - 100f) * .5f).Within(Tolerance));
            Assert.That(Value(Figure, Y), Is.EqualTo(FigureRest.y + 100f + (y - FigureRest.y - 100f) * .5f).Within(Tolerance));
            portrait.SetState(Arrival, .5f);
            Assert.That(Value(Figure, X), Is.EqualTo(100f).Within(Tolerance));
            Assert.That(Value(Figure, Y), Is.EqualTo(FigureRest.y + 100f).Within(Tolerance));
        }

        [Test]
        public void DirectedStateSeeksEachSequenceByProgressAndHoldsWhenAdvanced()
        {
            Build(Sequence("Head", Victory, 4f, false, Track(Ribbon, Y, Replace, AnimationCurve.Linear(0f, 90f, 4f, 110f))),
                Sequence("Accent", Victory, 2f, false, Track(Figure, Rotation, Replace, AnimationCurve.Linear(0f, 0f, 2f, 8f))));
            portrait.SetState(Victory, .25f);
            Assert.That(Value(Ribbon, Y), Is.EqualTo(95f).Within(Tolerance));
            Assert.That(Value(Figure, Rotation), Is.EqualTo(2f).Within(Tolerance), "Each sequence is sought over its own duration.");
            portrait.Advance(20f);
            Assert.That(portrait.Progress, Is.EqualTo(.25f));
            Assert.That(Value(Ribbon, Y), Is.EqualTo(95f).Within(Tolerance), "Time does not move a directed state.");

            portrait.SetState(Victory, 1f);
            portrait.Advance(20f);
            Assert.That(portrait.State, Is.EqualTo(Victory));
            Assert.That(portrait.Progress, Is.EqualTo(1f));
            Assert.That(Value(Ribbon, Y), Is.EqualTo(110f).Within(Tolerance));
            Assert.That(Value(Figure, Rotation), Is.EqualTo(8f).Within(Tolerance));
        }

        [Test]
        public void DirectedProgressIsClampedAndNonFiniteProgressIsIgnored()
        {
            Build(Sequence("Head", Victory, 4f, false, Track(Ribbon, Y, Replace, AnimationCurve.Linear(0f, 0f, 4f, 200f))));
            portrait.SetState(Victory, 2f);
            Assert.That(portrait.Progress, Is.EqualTo(1f));
            Assert.That(Value(Ribbon, Y), Is.EqualTo(200f).Within(Tolerance));
            portrait.SetState(Victory, -1f);
            Assert.That(portrait.Progress, Is.Zero);
            Assert.That(Value(Ribbon, Y), Is.Zero.Within(Tolerance));
            portrait.SetState(Victory, .4f);
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity })
            {
                portrait.SetState(Victory, invalid);
                Assert.That(portrait.Progress, Is.EqualTo(.4f), invalid.ToString());
                Assert.That(Value(Ribbon, Y), Is.EqualTo(80f).Within(Tolerance), invalid.ToString());
            }
        }

        [Test]
        public void StateDurationIsItsLongestSequenceAndZeroWithoutOne()
        {
            Build(Sequence("Head", Victory, 4f, false), Sequence("Accent", Victory, 2f, false));
            Assert.That(portrait.GetDuration(Victory), Is.EqualTo(4f));
            Assert.That(portrait.GetDuration(SpiritPresentationState.Defeat), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => portrait.GetDuration((SpiritPresentationState)7));
        }

        [Test]
        public void LoopMarkersFireOncePerCrossingAndNotAfterALongStep()
        {
            SpiritPresentationSequence idle = FigureIdle();
            idle.GestureTimes = new[] { .5f, 1.5f };
            Build(idle);
            int gestures = 0;
            portrait.SpiritGesture += () => ++gestures;
            portrait.Advance(.5f);
            Assert.That(gestures, Is.EqualTo(1));
            portrait.Advance(0f);
            Assert.That(gestures, Is.EqualTo(1), "A marker does not repeat at the same time.");
            portrait.Advance(2f);
            Assert.That(gestures, Is.EqualTo(3), "Each marker crossed in the step fires once.");
            portrait.Advance(3f);
            Assert.That(gestures, Is.EqualTo(3), "A resumed frame replays no backlog of markers.");
        }

        [Test]
        public void DirectedMarkersFireOnlyWhenProgressMovesForward()
        {
            SpiritPresentationSequence arrival = Sequence("Arrive", Arrival, 5f, false);
            arrival.GestureOnStart = true;
            arrival.GestureTimes = new[] { 1f, 3f, 4f };
            Build(arrival);
            int gestures = 0;
            portrait.SpiritGesture += () => ++gestures;
            portrait.SetState(Arrival, .5f);
            Assert.That(gestures, Is.EqualTo(1), "Entering fires the start gesture only.");
            portrait.Advance(100f);
            Assert.That(gestures, Is.EqualTo(1));
            portrait.SetState(Arrival, .8f);
            Assert.That(gestures, Is.EqualTo(3), "A forward seek fires the markers it crosses.");
            portrait.SetState(Arrival, .2f);
            Assert.That(gestures, Is.EqualTo(3), "A backward seek fires nothing.");
            portrait.SetState(Arrival, .6f);
            Assert.That(gestures, Is.EqualTo(4));
            portrait.SetVisible(false);
            portrait.SetState(Arrival, 1f);
            Assert.That(gestures, Is.EqualTo(4), "A hidden host fires nothing.");
        }

        [Test]
        public void AdvanceIgnoresNegativeAndNonFiniteSteps()
        {
            Build(RibbonReaction());
            portrait.React();
            portrait.Advance(1f);
            foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                portrait.Advance(invalid);
            Assert.That(portrait.IsReacting, Is.True);
            Assert.That(Value(Ribbon, X), Is.EqualTo(RibbonRestX + 4f).Within(Tolerance));
            portrait.Advance(2.1f);
            Assert.That(portrait.IsReacting, Is.False, "Only the valid steps count toward the reaction's duration.");
        }

        [Test]
        public void ResetPoseEndsAReactionAndRestoresTheRestPose()
        {
            Build(FigureIdle(), RibbonReaction());
            portrait.Advance(.5f);
            portrait.React();
            portrait.Advance(1f);
            Assert.That(Value(Ribbon, X), Is.Not.EqualTo(RibbonRestX), "The reaction moves the ribbon.");

            portrait.ResetPose();

            Assert.That(portrait.IsReacting, Is.False);
            Assert.That(portrait.State, Is.EqualTo(Idle));
            Assert.That(Value(Figure, X), Is.EqualTo(FigureRest.x).Within(Tolerance));
            Assert.That(portrait.GetLayer("Ribbon").rectTransform.anchoredPosition.x, Is.EqualTo(RibbonRestX).Within(Tolerance));
        }

        [Test]
        public void HiddenHostIgnoresReactionsTimeAndGesturesUntilShown()
        {
            SpiritPresentationSequence reaction = RibbonReaction();
            reaction.GestureOnStart = true;
            Build(FigureIdle(), reaction);
            int gestures = 0;
            portrait.SpiritGesture += () => ++gestures;
            portrait.React();
            portrait.Advance(1f);

            portrait.SetVisible(false);
            Assert.That(portrait.IsReacting, Is.False, "Hiding the host ends its reaction.");
            Assert.That(Value(Ribbon, X), Is.EqualTo(RibbonRestX).Within(Tolerance));
            portrait.React();
            portrait.Advance(1.5f);
            Assert.That(portrait.IsReacting, Is.False, "A hidden host does not react.");
            Assert.That(Value(Figure, X), Is.EqualTo(FigureRest.x).Within(Tolerance), "A hidden host's idle does not advance.");
            Assert.That(gestures, Is.EqualTo(1), "A hidden host makes no gesture.");

            portrait.SetVisible(true);
            portrait.React();
            Assert.That(portrait.IsReacting, Is.True);
            Assert.That(gestures, Is.EqualTo(2));
        }

        [Test]
        public void LayersKeepTheirPlaceInTheRoomWhenItResizes()
        {
            Build();
            Vector2[] before = presentation.Layers.Select(layer => RoomPoint(portrait.GetLayer(layer.Id).rectTransform)).ToArray();
            Room.sizeDelta = new Vector2(300f, 900f);
            portrait.Advance(0f);
            for (int i = 0; i < before.Length; ++i)
                Assert.That(Vector2.Distance(RoomPoint(portrait.GetLayer(presentation.Layers[i].Id).rectTransform), before[i]),
                    Is.LessThan(Tolerance), presentation.Layers[i].Id + " keeps its normalized place in the room.");
        }

        [Test]
        public void ReplaceTracksSetAPropertyAndAddTracksOffsetItsRestValue()
        {
            Build(Sequence("Pose", Idle, 1f, true,
                Track(Figure, X, Replace, AnimationCurve.Constant(0f, 1f, 50f)),
                Track(Figure, Y, Add, AnimationCurve.Constant(0f, 1f, 3f)),
                Track(Figure, SpiritPortraitProperty.Deformation, Replace, AnimationCurve.Constant(0f, 1f, .5f))));
            portrait.Advance(.5f);
            SpiritPortraitImage figure = portrait.GetLayer("Figure");
            Assert.That(Vector2.Distance(figure.rectTransform.anchoredPosition, new Vector2(50f, FigureRest.y + 3f)), Is.LessThan(Tolerance));
            Assert.That(figure.Weights[0], Is.EqualTo(.5f).Within(Tolerance), "A deformation track drives the image's shape weight.");
        }

        [Test]
        public void DirectedStateKeepsTheSmokeEffectTimeRunning()
        {
            presentation.SmokeShader = Shader.Find("Dice/Spirit Smoke");
            presentation.Layers[Figure].Smoke.Enabled = true;
            Build(Sequence("Win", Victory, 1f, false));
            portrait.Advance(.5f);
            Material material = portrait.GetLayer("Figure").material;
            portrait.SetState(Victory, .4f);
            portrait.Advance(.25f);
            Assert.That(portrait.Progress, Is.EqualTo(.4f), "The directed pose holds.");
            Assert.That(material.GetFloat("_EffectTime"), Is.EqualTo(.75f).Within(Tolerance), "The smoke keeps its own clock.");
        }

        private RectTransform Room => (RectTransform)room.transform;

        private void Build(params SpiritPresentationSequence[] sequences)
        {
            presentation.Sequences = sequences;
            portrait = SpiritPortraitTestAuthoring.Create(presentation, Room);
            portrait.Build((RectTransform)portrait.transform, presentation);
        }

        private float Value(int layer, SpiritPortraitProperty property) => portrait.ReadLayerValue(layer, property);

        private Vector2 RoomPoint(RectTransform layer)
        {
            Vector3 local = Room.InverseTransformPoint(layer.TransformPoint(layer.rect.center));
            return new Vector2((local.x - Room.rect.xMin) / Room.rect.width, (local.y - Room.rect.yMin) / Room.rect.height);
        }

        private T Own<T>(T value) where T : UnityEngine.Object
        {
            created.Add(value);
            return value;
        }

        /** A two-second idle loop moving the figure right by two units a second. */
        private static SpiritPresentationSequence FigureIdle()
            => Sequence("Figure idle", Idle, 2f, true, Track(Figure, X, Add, AnimationCurve.Linear(0f, 0f, 2f, 4f)));

        /** A three-second reaction moving the ribbon right by four units a second. */
        private static SpiritPresentationSequence RibbonReaction()
            => Sequence("Ribbon reaction", Reaction, 3f, false, Track(Ribbon, X, Add, AnimationCurve.Linear(0f, 0f, 3f, 12f)));

        /** Gives a track an entry weight that falls from one to zero over its first two seconds. */
        private static SpiritPortraitTrack Weighted(SpiritPortraitTrack track)
        {
            track.EntryWeight = AnimationCurve.Linear(0f, 1f, 2f, 0f);
            return track;
        }

        private static SpiritPortraitTrack Track(int layer, SpiritPortraitProperty property, SpiritTrackBlend blend,
            AnimationCurve curve) => new SpiritPortraitTrack { Layer = layer, Property = property, Blend = blend, Curve = curve };

        private static SpiritPresentationSequence Sequence(string id, SpiritPresentationState state, float duration,
            bool loop, params SpiritPortraitTrack[] tracks)
            => new SpiritPresentationSequence { Id = id, State = state, Duration = duration, Loop = loop, Tracks = tracks };
    }
}
