using System;
using System.Linq;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class SpiritMothPortraitTests
    {
        private GameObject host;
        private SpiritHostPortrait portrait;
        private SpiritHostPresentationAsset presentation;
        private static readonly Vector2 LanternPosition = new Vector2(213.3f, 753.6f);

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Spirit Moth Test", typeof(RectTransform));
            var parent = host.GetComponent<RectTransform>();
            parent.sizeDelta = new Vector2(540f, 960f);
            presentation = AssetDatabase.LoadAssetAtPath<SpiritHostPresentationAsset>(
                "Assets/Dice/Content/Presentation/MothCrossroads/MothCrossroadsHost.asset");
            Assert.That(presentation, Is.Not.Null);
            portrait = host.AddComponent<SpiritHostPortrait>();
            portrait.Build(parent, presentation);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [TestCase(.137f)]
        [TestCase(3.47f)]
        [TestCase(9.81f)]
        public void ArrivalStartsFromEveryVisibleWaitingMothPose(float waitingAge)
        {
            portrait.SetState(SpiritPresentationState.Waiting, 0f);
            portrait.Advance(waitingAge);
            var poses = Enumerable.Range(1, 3).Select(index => Capture("Lantern moth " + index)).ToArray();
            foreach (var pose in poses) Assert.That(pose.Alpha, Is.GreaterThan(.99f));

            portrait.SetState(SpiritPresentationState.Arrival, 0f);

            for (int index = 1; index <= 3; ++index)
                AssertPose("Lantern moth " + index, poses[index - 1]);
        }

        [Test]
        public void ArrivalFinishesAtTheSettledBodyAndPreservesWingSmoke()
        {
            portrait.SetState(SpiritPresentationState.Waiting, 0f);
            portrait.Advance(3.47f);
            var smoke = portrait.GetLayer("Wing smoke");
            Material material = smoke.material;
            float effectTime = material.GetFloat("_EffectTime");
            portrait.SetState(SpiritPresentationState.Arrival, 0f);
            portrait.SetState(SpiritPresentationState.Arrival, 1f);
            var bodyPose = Capture("Body");
            var smokePose = Capture("Wing smoke");
            var bodyLayer = presentation.Layers.Single(layer => layer.Id == "Body");
            Assert.That(Vector2.Distance(bodyPose.Position, bodyLayer.Position), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(bodyPose.Size, bodyLayer.Size), Is.LessThan(.001f));
            Assert.That(bodyPose.Alpha, Is.GreaterThan(.99f));
            Assert.That(smokePose.Alpha, Is.GreaterThan(.99f));
            Assert.That(Vector2.Distance(smokePose.Position, bodyPose.Position), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(smokePose.Size, bodyPose.Size), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(smokePose.Rotation, bodyPose.Rotation), Is.LessThan(.001f));
            AssertHiddenArrivalLayers();

            portrait.SetState(SpiritPresentationState.Idle, 1f);

            AssertPose("Body", bodyPose);
            AssertPose("Wing smoke", smokePose);
            AssertHiddenArrivalLayers();
            Assert.That(smoke.texture, Is.SameAs(presentation.Layers.Single(layer => layer.Id == "Wing smoke").Texture));
            Assert.That(smoke.material, Is.SameAs(material));
            Assert.That(material.shader, Is.SameAs(presentation.SmokeShader));
            Assert.That(material.GetFloat("_EffectTime"), Is.EqualTo(effectTime));
            Assert.That(material.GetFloat("_Opacity"), Is.GreaterThan(0f));
            portrait.Advance(.25f);
            Assert.That(material.GetFloat("_EffectTime"), Is.EqualTo(effectTime + .25f).Within(.0001f));
        }

        [TestCase("Lantern moth 1")]
        [TestCase("Lantern moth 2")]
        [TestCase("Lantern moth 3")]
        public void WaitingFlightLoopsContinuouslyAroundTheLanternWithDartsAndReversals(string name)
        {
            float duration = presentation.Sequences.Single(sequence => sequence.State == SpiritPresentationState.Waiting).Duration;
            portrait.SetState(SpiritPresentationState.Waiting, 0f);
            var start = Capture(name);
            portrait.Advance(duration);
            AssertPose(name, start);

            portrait.SetState(SpiritPresentationState.Idle, 1f);
            portrait.SetState(SpiritPresentationState.Waiting, 0f);
            portrait.Advance(duration - .001f);
            var beforeSeam = Capture(name);
            portrait.Advance(.002f);
            var afterSeam = Capture(name);
            Assert.That(Vector2.Distance(beforeSeam.Position, afterSeam.Position), Is.LessThan(1f));
            Assert.That(Quaternion.Angle(beforeSeam.Rotation, afterSeam.Rotation), Is.LessThan(1f));

            portrait.SetState(SpiritPresentationState.Idle, 1f);
            portrait.SetState(SpiritPresentationState.Waiting, 0f);
            int samples = Mathf.CeilToInt(duration / .02f);
            float step = duration / samples;
            var speeds = new float[samples];
            Vector2 previous = portrait.GetLayer(name).rectTransform.anchoredPosition;
            Vector2 previousDirection = Vector2.zero;
            int clockwiseTurns = 0, counterclockwiseTurns = 0;
            float distance = 0f;
            for (int sample = 0; sample < samples; ++sample)
            {
                portrait.Advance(step);
                var image = portrait.GetLayer(name);
                Vector2 position = image.rectTransform.anchoredPosition;
                Vector2 movement = position - previous;
                float speed = movement.magnitude / step;
                speeds[sample] = speed;
                distance += movement.magnitude;
                Assert.That(Vector2.Distance(position, LanternPosition), Is.LessThan(95f), name);
                Assert.That(movement.magnitude, Is.LessThan(10f), "Waiting flight must not teleport between samples.");
                Assert.That(image.color.a, Is.GreaterThan(.99f), name);
                // Retain the direction through a hover so the next dart still counts as a turn.
                if (speed > 8f)
                {
                    if (previousDirection != Vector2.zero)
                    {
                        float turn = Vector2.SignedAngle(previousDirection, movement);
                        if (turn < -30f) ++clockwiseTurns;
                        if (turn > 30f) ++counterclockwiseTurns;
                    }
                    previousDirection = movement;
                }
                previous = position;
            }
            Array.Sort(speeds);
            Assert.That(distance, Is.GreaterThan(200f), "The moth must travel between distinct locations.");
            Assert.That(speeds[samples - 1], Is.GreaterThan(80f), "The loop needs visible darts.");
            Assert.That(speeds[samples / 4], Is.LessThan(speeds[samples - 1] * .3f),
                "Darts must be interspersed with slower movement and hovers.");
            Assert.That(clockwiseTurns, Is.GreaterThanOrEqualTo(2), "The moth must change its winding direction.");
            Assert.That(counterclockwiseTurns, Is.GreaterThanOrEqualTo(2), "The moth must change its winding direction.");
        }

        [TestCase(SpiritPresentationState.Victory, 1.6f)]
        [TestCase(SpiritPresentationState.Defeat, 1.2f)]
        [TestCase(SpiritPresentationState.Farewell, .5f)]
        public void OutcomesHoldDirectedPosesWhileTheSameWingSmokeContinues(
            SpiritPresentationState state, float duration)
        {
            Assert.That(portrait.GetDuration(state), Is.EqualTo(duration).Within(.001f));
            Assert.That(presentation.Sequences.Where(sequence => sequence.State == state).All(sequence => !sequence.Loop), Is.True);
            var smoke = portrait.GetLayer("Wing smoke");
            Material material = smoke.material;
            Texture texture = smoke.texture;
            portrait.SetState(state, .4f);
            var poses = presentation.Layers.Select(layer => Capture(layer.Id)).ToArray();
            float effectTime = material.GetFloat("_EffectTime");

            portrait.Advance(3f);

            Assert.That(portrait.Progress, Is.EqualTo(.4f));
            for (int index = 0; index < poses.Length; ++index) AssertPose(presentation.Layers[index].Id, poses[index]);
            Assert.That(smoke.material, Is.SameAs(material));
            Assert.That(smoke.texture, Is.SameAs(texture));
            Assert.That(material.GetFloat("_EffectTime"), Is.EqualTo(effectTime + 3f).Within(.001f));
            portrait.SetState(state, .9f);
            portrait.SetState(state, .4f);
            for (int index = 0; index < poses.Length; ++index) AssertPose(presentation.Layers[index].Id, poses[index]);

            portrait.SetState(state, 1f);
            poses = presentation.Layers.Select(layer => Capture(layer.Id)).ToArray();
            portrait.Advance(20f);
            Assert.That(portrait.State, Is.EqualTo(state));
            Assert.That(portrait.Progress, Is.EqualTo(1f));
            for (int index = 0; index < poses.Length; ++index) AssertPose(presentation.Layers[index].Id, poses[index]);
            AssertHiddenArrivalLayers();
        }

        [Test]
        public void OutcomeEntryPreservesIdleAndInterruptedReaction(
            [Values(SpiritPresentationState.Victory, SpiritPresentationState.Defeat, SpiritPresentationState.Farewell)]
            SpiritPresentationState state, [Values(false, true)] bool reacting)
        {
            portrait.Advance(3.85f);
            if (reacting)
            {
                portrait.React();
                portrait.Advance(.4f);
                Assert.That(portrait.IsReacting, Is.True);
            }
            var poses = presentation.Layers.Select(layer => Capture(layer.Id)).ToArray();

            portrait.SetState(state, 0f);

            for (int index = 0; index < poses.Length; ++index) AssertPose(presentation.Layers[index].Id, poses[index]);
            Assert.That(portrait.IsReacting, Is.False);
            portrait.SetState(state, 1f);
            var terminal = Capture("Body");
            portrait.ResetPose();
            portrait.SetState(state, 1f);
            AssertPose("Body", terminal);
        }

        [Test]
        public void VictoryBrightensTheSpectralEdgeAndTurnsTowardTheRevealedRoad()
        {
            var settled = Capture("Body");
            float glowPeak = 0f;
            for (int sample = 0; sample <= 20; ++sample)
            {
                portrait.SetState(SpiritPresentationState.Victory, sample / 20f);
                glowPeak = Mathf.Max(glowPeak, portrait.GetLayer("Spectral arrival").color.a);
                AssertRegisteredBodyAndSmoke();
                Assert.That(portrait.GetLayer("Wing smoke").color.a, Is.GreaterThan(.99f));
                foreach (int index in Enumerable.Range(1, 3))
                    Assert.That(portrait.GetLayer("Lantern moth " + index).color.a, Is.Zero);
            }
            Assert.That(glowPeak, Is.GreaterThan(.15f), "The response needs a visible spectral brightening.");
            var terminal = Capture("Body");
            Assert.That(terminal.Position, Is.EqualTo(settled.Position));
            Assert.That(terminal.Size, Is.EqualTo(settled.Size));
            Assert.That(Mathf.DeltaAngle(0f, terminal.Rotation.eulerAngles.z), Is.LessThan(-1f));
            Assert.That(terminal.Weights[0], Is.LessThan(0f), "The wings should settle outward.");
            AssertHiddenArrivalLayers();
        }

        [Test]
        public void DefeatWithdrawsTowardTheLanternAndDimsMoreThanTheBriefFarewell()
        {
            var settled = Capture("Body");
            portrait.SetState(SpiritPresentationState.Defeat, 1f);
            var defeat = Capture("Body");
            float defeatSmoke = Capture("Wing smoke").Alpha;
            AssertRegisteredBodyAndSmoke();
            Assert.That(Vector2.Distance(defeat.Position, LanternPosition),
                Is.LessThan(Vector2.Distance(settled.Position, LanternPosition)));
            Assert.That(defeat.Size.x, Is.LessThan(settled.Size.x));
            Assert.That(defeat.Weights[1], Is.GreaterThan(.3f));
            Assert.That(defeat.Alpha, Is.GreaterThan(.99f));
            Assert.That(defeatSmoke, Is.InRange(.3f, .55f));
            AssertHiddenArrivalLayers();

            portrait.ResetPose();
            portrait.SetState(SpiritPresentationState.Farewell, 1f);
            var farewell = Capture("Body");
            AssertRegisteredBodyAndSmoke();
            Assert.That(farewell.Position, Is.EqualTo(settled.Position));
            Assert.That(farewell.Size, Is.EqualTo(settled.Size));
            Assert.That(farewell.Weights[1], Is.InRange(.05f, defeat.Weights[1] - .05f));
            Assert.That(Capture("Wing smoke").Alpha, Is.InRange(defeatSmoke + .1f, .9f));
            AssertHiddenArrivalLayers();
        }

        private void AssertRegisteredBodyAndSmoke()
        {
            var body = Capture("Body");
            var smoke = Capture("Wing smoke");
            Assert.That(smoke.Position, Is.EqualTo(body.Position));
            Assert.That(smoke.Size, Is.EqualTo(body.Size));
            Assert.That(Quaternion.Angle(smoke.Rotation, body.Rotation), Is.LessThan(.001f));
            Assert.That(body.Alpha, Is.GreaterThan(.99f));
        }

        private Pose Capture(string name)
        {
            var image = portrait.GetLayer(name);
            int layer = Array.FindIndex(presentation.Layers, value => value.Id == name);
            return new Pose
            {
                Position = image.rectTransform.anchoredPosition,
                Size = image.rectTransform.sizeDelta,
                Rotation = image.rectTransform.localRotation,
                Alpha = image.color.a,
                Weights = Enumerable.Range(0, presentation.Layers[layer].Mesh.Shapes.Length)
                    .Select(channel => portrait.ReadLayerValue(layer, SpiritPortraitProperty.Deformation, channel)).ToArray()
            };
        }

        private void AssertPose(string name, Pose expected)
        {
            var actual = Capture(name);
            Assert.That(Vector2.Distance(actual.Position, expected.Position), Is.LessThan(.001f), name + " position");
            Assert.That(Vector2.Distance(actual.Size, expected.Size), Is.LessThan(.001f), name + " size");
            Assert.That(Quaternion.Angle(actual.Rotation, expected.Rotation), Is.LessThan(.01f), name + " rotation");
            Assert.That(actual.Alpha, Is.EqualTo(expected.Alpha).Within(.001f), name + " alpha");
            Assert.That(actual.Weights, Is.EqualTo(expected.Weights).Within(.001f), name + " deformation");
        }

        private void AssertHiddenArrivalLayers()
        {
            foreach (string name in new[] { "Lantern moth 1", "Lantern moth 2", "Lantern moth 3", "Spectral arrival" })
                Assert.That(portrait.GetLayer(name).color.a, Is.Zero, name);
        }

        private sealed class Pose
        {
            public Vector2 Position, Size;
            public Quaternion Rotation;
            public float Alpha;
            public float[] Weights;
        }
    }
}
