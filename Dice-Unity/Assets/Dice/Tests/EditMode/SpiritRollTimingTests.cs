using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class SpiritRollTimingTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void RecordedRollLandsPromptlyAndSettlesContinuously(int count)
        {
            var theme = Resources.Load<SpiritAudioTheme>("SpiritAudio/SpiritAudioTheme");
            var track = theme.rollContactTracks[count - 1];
            var clip = theme.rollClips[count - 1];
            var timing = new SpiritRollTiming(clip.length, track, theme.rollLaunchTime, 10, 20);
            Assert.That(timing.Duration, Is.LessThan(1.2f));
            Assert.That(timing.AudioStartDspTime, Is.EqualTo(20 + (double)theme.rollLaunchTime).Within(.0001));
            for (int contact = 0; contact < track.contactTimes.Length; contact++)
                Assert.That(timing.GetContactTime(contact) - timing.AudioDelay + timing.RecordingStartTime,
                    Is.EqualTo(track.contactTimes[contact]).Within(.0001), "Contacts must align to the recording timeline.");

            var start = Quaternion.Euler(10, 35, 5);
            var end = Quaternion.Euler(75, -15, 90);
            for (int die = 0; die < count; die++)
            {
                timing.SamplePose(die, count, 0, start, end, out var rotation, out var lift, out var drift);
                Assert.That(Quaternion.Angle(start, rotation), Is.LessThan(.05f));
                Assert.That(lift, Is.Zero);
                float landing = timing.GetFirstContactTime(die);
                Assert.That(landing, Is.InRange(.1f, .3f));
                timing.SamplePose(die, count, landing * .5f, start, end, out rotation, out lift, out drift);
                Assert.That(lift, Is.GreaterThan(.2f), "A short toss should precede contact.");
                timing.SamplePose(die, count, landing, start, end, out rotation, out lift, out drift);
                Assert.That(lift, Is.EqualTo(0).Within(.0001), "The die must touch down on its first contact.");
                for (float elapsed = landing; elapsed < timing.Duration; elapsed += .003f)
                {
                    timing.SamplePose(die, count, elapsed, start, end, out rotation, out lift, out drift);
                    Assert.That(lift, Is.InRange(0f, .0751f), "After landing, only small rebounds are allowed.");
                }
                timing.SamplePose(die, count, timing.Duration - .001f, start, end,
                    out var beforeEnd, out lift, out drift);
                Assert.That(Quaternion.Angle(beforeEnd, end), Is.LessThan(.1f), "Settling must not snap to the result.");
                Assert.That(lift, Is.LessThan(.001f));
                timing.SamplePose(die, count, timing.Duration, start, end, out rotation, out lift, out drift);
                Assert.That(Quaternion.Angle(rotation, end), Is.LessThan(.05f));
                Assert.That(lift, Is.Zero);
                Assert.That(drift, Is.Zero);
            }
        }
    }
}
