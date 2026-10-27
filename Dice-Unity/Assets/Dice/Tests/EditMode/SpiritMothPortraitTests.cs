using System;
using System.Linq;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    [Explicit("Art-generation check; run it by name while the moth art is being changed.")]
    [Category("ArtAuthoring")]
    public sealed class SpiritMothPortraitTests
    {
        private GameObject host;
        private SpiritHostPortrait portrait;
        private SpiritHostPresentationAsset presentation;
        // The lantern the moths circle: the authored rest point of the lantern moth layers.
        private Vector2 LanternPosition => presentation.Layers.First(layer => layer.Id == "Lantern moth 1").Position;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Spirit Moth Test", typeof(RectTransform));
            var parent = host.GetComponent<RectTransform>();
            parent.sizeDelta = new Vector2(540f, 960f);
            presentation = AssetDatabase.LoadAssetAtPath<SpiritHostPresentationAsset>(
                "Assets/Dice/Content/Presentation/MothCrossroads/MothCrossroadsHost.asset");
            Assert.That(presentation, Is.Not.Null);
            portrait = presentation.Create(parent);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

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
            var movements = new Vector2[samples];
            Vector2 previous = portrait.GetLayer(name).rectTransform.anchoredPosition;
            for (int sample = 0; sample < samples; ++sample)
            {
                portrait.Advance(step);
                var image = portrait.GetLayer(name);
                Vector2 position = image.rectTransform.anchoredPosition;
                movements[sample] = position - previous;
                Assert.That(image.color.a, Is.GreaterThan(.99f), name);
                previous = position;
            }
            float[] speeds = movements.Select(movement => movement.magnitude / step).OrderBy(speed => speed).ToArray();
            float fastest = speeds[samples - 1];
            Assert.That(fastest, Is.GreaterThan(0f), name + " must fly while waiting.");
            Assert.That(speeds[samples / 4], Is.LessThan(fastest * .3f),
                "Darts must be interspersed with slower movement and hovers.");
            // A turn is measured between moving samples, keeping the direction through a hover so the next dart counts.
            Vector2 previousDirection = Vector2.zero;
            int clockwiseTurns = 0, counterclockwiseTurns = 0;
            foreach (Vector2 movement in movements)
            {
                if (movement.magnitude / step <= fastest * .1f) continue;
                if (previousDirection != Vector2.zero)
                {
                    float turn = Vector2.SignedAngle(previousDirection, movement);
                    if (turn < -30f) ++clockwiseTurns;
                    if (turn > 30f) ++counterclockwiseTurns;
                }
                previousDirection = movement;
            }
            Assert.That(clockwiseTurns, Is.GreaterThan(0), "The moth must change its winding direction.");
            Assert.That(counterclockwiseTurns, Is.GreaterThan(0), "The moth must change its winding direction.");
        }

        [TestCase(SpiritPresentationState.Victory)]
        [TestCase(SpiritPresentationState.Defeat)]
        [TestCase(SpiritPresentationState.Farewell)]
        public void OutcomesHoldDirectedPosesWhileTheSameWingSmokeContinues(SpiritPresentationState state)
        {
            Assert.That(portrait.GetDuration(state), Is.GreaterThan(0f), state + " requires an authored performance.");
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
            Assert.That(glowPeak, Is.GreaterThan(0f), "The response brightens the spectral edge.");
            var terminal = Capture("Body");
            Assert.That(terminal.Position, Is.EqualTo(settled.Position));
            Assert.That(terminal.Size, Is.EqualTo(settled.Size));
            Assert.That(Mathf.DeltaAngle(0f, terminal.Rotation.eulerAngles.z), Is.LessThan(0f), "Victory turns toward the revealed road.");
            Assert.That(terminal.Weights[0], Is.LessThan(0f), "The wings should settle outward.");
            AssertHiddenArrivalLayers();
        }

        [Test]
        public void DefeatWithdrawsTowardTheLanternAndDimsMoreThanTheBriefFarewell()
        {
            var settled = Capture("Body");
            float settledSmoke = Capture("Wing smoke").Alpha;
            portrait.SetState(SpiritPresentationState.Defeat, 1f);
            var defeat = Capture("Body");
            float defeatSmoke = Capture("Wing smoke").Alpha;
            AssertRegisteredBodyAndSmoke();
            Assert.That(Vector2.Distance(defeat.Position, LanternPosition),
                Is.LessThan(Vector2.Distance(settled.Position, LanternPosition)));
            Assert.That(defeat.Size.x, Is.LessThan(settled.Size.x));
            Assert.That(defeat.Weights[1], Is.GreaterThan(settled.Weights[1]), "Defeat folds the wings in.");
            Assert.That(defeat.Alpha, Is.GreaterThan(.99f));
            Assert.That(defeatSmoke, Is.GreaterThan(0f).And.LessThan(settledSmoke), "Defeat dims the wing smoke without hiding it.");
            AssertHiddenArrivalLayers();

            portrait.ResetPose();
            portrait.SetState(SpiritPresentationState.Farewell, 1f);
            var farewell = Capture("Body");
            AssertRegisteredBodyAndSmoke();
            Assert.That(farewell.Position, Is.EqualTo(settled.Position));
            Assert.That(farewell.Size, Is.EqualTo(settled.Size));
            Assert.That(farewell.Weights[1], Is.GreaterThan(settled.Weights[1]).And.LessThan(defeat.Weights[1]),
                "The farewell folds the wings less than defeat.");
            Assert.That(Capture("Wing smoke").Alpha, Is.GreaterThan(defeatSmoke).And.LessThan(settledSmoke),
                "The farewell dims the wing smoke less than defeat.");
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
