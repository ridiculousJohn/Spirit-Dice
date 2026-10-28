using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class SpiritRollTimingTests
    {
        // A fabricated recording: the dice are launched before the clip starts and its contacts follow a lead-in.
        private const float RecordingDuration = .8f;
        private const float LaunchTime = .04f;
        private const double StartTime = 10;
        private const double StartDspTime = 20;
        private static SpiritRollContactTrack Track() => new SpiritRollContactTrack
        {
            recordingStartTime = .05f, contactTimes = new[] { .2f, .26f, .4f, .55f }
        };

        /**
         * Checks a roll follows its recording: contacts sit on the recording's timeline, each die tosses, touches down
         * on its first contact, rebounds only slightly and settles on its result without a snap. Fails if the dice and
         * the recording drift apart or a die jumps at landing or at the end.
         */
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void RecordedRollLandsOnItsContactsAndSettlesContinuously(int count)
        {
            var track = Track();
            var timing = new SpiritRollTiming(RecordingDuration, track, LaunchTime, StartTime, StartDspTime);
            Assert.That(timing.Duration, Is.EqualTo(LaunchTime + RecordingDuration - track.recordingStartTime).Within(.0001));
            Assert.That(timing.AudioStartDspTime, Is.EqualTo(StartDspTime + LaunchTime).Within(.0001));
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
                timing.SamplePose(die, count, landing * .5f, start, end, out rotation, out lift, out drift);
                Assert.That(lift, Is.EqualTo(SpiritRollTiming.MaximumLift).Within(.0001), "The toss peaks halfway to contact.");
                timing.SamplePose(die, count, landing, start, end, out rotation, out lift, out drift);
                Assert.That(lift, Is.EqualTo(0).Within(.0001), "The die must touch down on its first contact.");
                for (float elapsed = landing; elapsed < timing.Duration; elapsed += .003f)
                {
                    timing.SamplePose(die, count, elapsed, start, end, out rotation, out lift, out drift);
                    Assert.That(lift, Is.InRange(0f, SpiritRollTiming.ReboundLift), "After landing, only small rebounds are allowed.");
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
