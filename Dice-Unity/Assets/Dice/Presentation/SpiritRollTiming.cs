using System;
using UnityEngine;

namespace Dice.Presentation
{
    /** Recorded contact times are seconds from the beginning of the unmodified audio clip. */
    [Serializable]
    public sealed class SpiritRollContactTrack
    {
        [Min(0)] public float recordingStartTime;
        public float[] contactTimes;
        [Min(0)] public float settleTime = .12f;
    }

    /** One unscaled reference drives the scheduled recording and the dice presentation. */
    public sealed class SpiritRollTiming
    {
        private readonly float[] contacts;
        public double StartTime { get; }
        public double AudioStartDspTime { get; }
        public float RecordingStartTime { get; }
        public float AudioDelay { get; }
        public float Duration { get; }
        public int ContactCount => contacts.Length;
        public float Elapsed => Mathf.Max(0f, (float)(Time.unscaledTimeAsDouble - StartTime));

        public SpiritRollTiming(float recordingDuration, SpiritRollContactTrack track, float launchTime,
            double startTime, double startDspTime)
        {
            StartTime = startTime;
            AudioDelay = Mathf.Max(0f, launchTime);
            AudioStartDspTime = startDspTime + AudioDelay;
            RecordingStartTime = Mathf.Clamp(track != null ? track.recordingStartTime : 0f,
                0f, Mathf.Max(0f, recordingDuration - .001f));
            var validContacts = new System.Collections.Generic.List<float>();
            if (track != null && track.contactTimes != null)
                foreach (float contact in track.contactTimes)
                    if (!float.IsNaN(contact) && !float.IsInfinity(contact)
                        && contact >= RecordingStartTime && contact <= recordingDuration)
                        validContacts.Add(AudioDelay + contact - RecordingStartTime);
            validContacts.Sort();
            if (validContacts.Count == 0) validContacts.Add(AudioDelay);
            contacts = validContacts.ToArray();
            float settleTime = track != null ? Mathf.Max(0f, track.settleTime) : .12f;
            Duration = Mathf.Max(AudioDelay + recordingDuration - RecordingStartTime,
                contacts[contacts.Length - 1] + settleTime);
        }

        public float GetContactTime(int index) => contacts[index];

        public float GetFirstContactTime(int ordinal)
        {
            // A shared impact starts the group landing; individual bays land a few frames apart.
            return Mathf.Min(Duration, Mathf.Min(.3f, contacts[0] + ordinal * .012f));
        }

        public void SamplePose(int ordinal, int diceCount, float elapsed, Quaternion startRotation,
            Quaternion endRotation, out Quaternion rotation, out float lift, out float drift)
        {
            if (elapsed >= Duration || Duration <= 0f)
            {
                rotation = endRotation;
                lift = 0f;
                drift = 0f;
                return;
            }

            elapsed = Mathf.Max(0f, elapsed);
            float firstContact = GetFirstContactTime(ordinal);
            Vector3 axis = new Vector3(1f, 0f, ordinal % 2 == 0 ? .6f : -.6f).normalized;
            Quaternion contactRotation = Quaternion.AngleAxis(32f, axis) * endRotation;
            if (elapsed < firstContact && firstContact > 0f)
            {
                float progress = elapsed / firstContact;
                float arc = Mathf.Sin(progress * Mathf.PI);
                rotation = Quaternion.AngleAxis(arc * 135f, axis)
                    * Quaternion.Slerp(startRotation, contactRotation, Mathf.SmoothStep(0f, 1f, progress));
                lift = arc * .34f;
                drift = Mathf.Sin(progress * Mathf.PI * 2f) * .045f;
                return;
            }

            float settleProgress = Mathf.Clamp01((elapsed - firstContact) / Mathf.Max(.001f, Duration - firstContact));
            float decay = 1f - Mathf.SmoothStep(0f, 1f, settleProgress);
            float tilt = 32f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(settleProgress * 1.8f)));
            float previousContact = firstContact;
            float nextContact = Duration;
            int contactOrdinal = -1;
            for (int index = ordinal; index < contacts.Length; index += Mathf.Max(1, diceCount))
            {
                float contact = contacts[index];
                if (contact < firstContact) continue;
                if (contact <= elapsed)
                {
                    previousContact = contact;
                    contactOrdinal = index;
                }
                else
                {
                    nextContact = contact;
                    break;
                }
            }

            float reboundTime = Mathf.Min(.12f, nextContact - previousContact);
            float reboundProgress = Mathf.Clamp01((elapsed - previousContact) / Mathf.Max(.001f, reboundTime));
            float rebound = 4f * reboundProgress * (1f - reboundProgress);
            float contactDecay = contactOrdinal < 0 ? 1f : 1f / (1f + contactOrdinal * .13f);
            lift = rebound * .075f * contactDecay * decay;
            float rock = Mathf.Sin(reboundProgress * Mathf.PI * 2f) * 9f * contactDecay * decay;
            rotation = Quaternion.AngleAxis(tilt + rock, axis) * endRotation;
            drift = Mathf.Sin(reboundProgress * Mathf.PI * 2f) * .022f * contactDecay * decay;
        }

        internal static SpiritRollTiming ForDuration(float duration)
        {
            duration = Mathf.Max(0f, duration);
            float firstContact = Mathf.Min(.16f, duration * .3f);
            var track = new SpiritRollContactTrack
            {
                contactTimes = new[] { firstContact, duration * .55f, duration * .78f },
                settleTime = 0f
            };
            return new SpiritRollTiming(duration, track, 0f, Time.unscaledTimeAsDouble, AudioSettings.dspTime);
        }
    }
}
