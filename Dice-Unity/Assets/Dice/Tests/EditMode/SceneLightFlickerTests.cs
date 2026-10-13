using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class SceneLightFlickerTests
    {
        [Test]
        public void AbsoluteTimeSamplesAreDeterministicRegardlessOfCallOrder()
        {
            var samples = new float[200];
            for (int i = 0; i < samples.Length; ++i)
                samples[i] = SceneLightFlicker.EvaluateIntensity(i * .031, 1f, .3f, 1.7f, 91);
            for (int i = samples.Length - 1; i >= 0; --i)
            {
                SceneLightFlicker.EvaluateIntensity(i * .017, 2f, .8f, 3f, 92);
                Assert.That(SceneLightFlicker.EvaluateIntensity(i * .031, 1f, .3f, 1.7f, 91), Is.EqualTo(samples[i]));
            }
        }

        [Test]
        public void DifferentSeedsProduceIndependentFlicker()
        {
            bool different = false;
            for (int i = 0; i < 200; ++i)
            {
                float first = SceneLightFlicker.EvaluateIntensity(i * .031, 1f, .3f, 1.7f, 91);
                float other = SceneLightFlicker.EvaluateIntensity(i * .031, 1f, .3f, 1.7f, 92);
                different |= Mathf.Abs(first - other) > .001f;
            }
            Assert.That(different, Is.True);
        }

        [Test]
        public void FlickerStaysWithinAuthoredBounds()
        {
            for (int i = 0; i < 200; ++i)
                Assert.That(SceneLightFlicker.EvaluateIntensity(i * .031, 1f, .3f, 1.7f, 91), Is.InRange(.7f, 1.3f));
        }

        [Test]
        public void NearbyTimeSamplesRemainContinuous()
        {
            for (int i = 0; i < 200; ++i)
            {
                double time = i * .031;
                float first = SceneLightFlicker.EvaluateIntensity(time, 1f, .3f, 1.7f, 91);
                float nearby = SceneLightFlicker.EvaluateIntensity(time + .0001, 1f, .3f, 1.7f, 91);
                Assert.That(Mathf.Abs(first - nearby), Is.LessThan(.001f));
            }
        }

        [Test]
        public void FlickerDoesNotAdvanceUnityRandomState()
        {
            Random.State state = Random.state;
            for (int i = 0; i < 200; ++i)
                SceneLightFlicker.EvaluateIntensity(i * .031, 1f, .3f, 1.7f, 91);
            Assert.That(Random.state.Equals(state), Is.True);
        }

        [TestCase(2f, 0f, 1f)]
        [TestCase(2f, .3f, 0f)]
        [TestCase(0f, .3f, 1f)]
        public void DisabledFlickerPreservesAuthoredIntensity(float intensity, float amount, float speed)
        {
            Assert.That(SceneLightFlicker.EvaluateIntensity(12, intensity, amount, speed, 91), Is.EqualTo(intensity));
        }
    }
}
