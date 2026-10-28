using System;
using UnityEngine;

namespace Dice.Presentation
{
    /** Recorded contact times are seconds from the beginning of the unmodified audio clip. */
    [Serializable]
    public sealed class SpiritRollContactTrack
    {
        /** Seconds the dice keep settling after the last recorded contact, unless a track authors its own. */
        public const float DefaultSettleTime = .12f;
        [Min(0)] public float recordingStartTime;
        public float[] contactTimes;
        [Min(0)] public float settleTime = DefaultSettleTime;
    }

    /** One unscaled reference drives the scheduled recording and the dice presentation. */
    public sealed class SpiritRollTiming
    {
        /** Peak height of a die's launch arc, in die sizes. */
        public const float MaximumLift = .34f;
        /** Peak sideways drift of a die during its launch arc, in die sizes. */
        public const float MaximumDrift = .045f;
        /** Lean of each die's tumble axis away from the row, alternating side by die. */
        private const float TumbleAxisLean = .6f;
        /** Degrees a die is tipped toward its landing edge at first contact. */
        private const float ContactTilt = 32f;
        /** Extra degrees a die tumbles at the top of its launch arc. */
        private const float LaunchTumble = 135f;
        /** Seconds by which every die has made first contact... */
        private const float LatestFirstContact = .3f;
        /** ...with each later die landing this many seconds after the one before. */
        private const float FirstContactStagger = .012f;
        /** The landing tilt has eased out once this share of the settle is reached, as a multiple of the settle. */
        private const float TiltSettleRate = 1.8f;
        /** Longest rebound between contacts, in seconds. */
        private const float MaximumReboundTime = .12f;
        /** How quickly each later contact's rebound weakens. */
        private const float ContactDecayRate = .13f;
        /** Peak rebound lift, drift and rock after a contact, in die sizes and degrees. */
        // The lift is internal so EditMode tests bound a settling die's rebounds by it.
        internal const float ReboundLift = .075f;
        private const float ReboundDrift = .022f;
        private const float ReboundRock = 9f;
        /** Shortest span the timing divides by, in seconds. */
        private const float MinimumSpan = .001f;
        /** Unrecorded rolls make first contact this share of the way through, at most at the latest first contact... */
        private const float UnrecordedFirstContactShare = .3f;
        private const float UnrecordedLatestFirstContact = .16f;
        /** ...then contact again at these shares of the roll. */
        private const float UnrecordedSecondContactShare = .55f;
        private const float UnrecordedThirdContactShare = .78f;
        private readonly float[] contacts;
        public double StartTime { get; }
        public double AudioStartDspTime { get; }
        public float RecordingStartTime { get; }
        public float AudioDelay { get; }
        public float Duration { get; }
        public float Elapsed => Mathf.Max(0f, (float)(Time.unscaledTimeAsDouble - StartTime));

        public SpiritRollTiming(float recordingDuration, SpiritRollContactTrack track, float launchTime,
            double startTime, double startDspTime)
        {
            StartTime = startTime;
            AudioDelay = Mathf.Max(0f, launchTime);
            AudioStartDspTime = startDspTime + AudioDelay;
            RecordingStartTime = Mathf.Clamp(track != null ? track.recordingStartTime : 0f,
                0f, Mathf.Max(0f, recordingDuration - MinimumSpan));
            var validContacts = new System.Collections.Generic.List<float>();
            if (track != null && track.contactTimes != null)
                foreach (float contact in track.contactTimes)
                    if (FiniteValue.Is(contact)
                        && contact >= RecordingStartTime && contact <= recordingDuration)
                        validContacts.Add(AudioDelay + contact - RecordingStartTime);
            validContacts.Sort();
            if (validContacts.Count == 0) validContacts.Add(AudioDelay);
            contacts = validContacts.ToArray();
            float settleTime = track != null ? Mathf.Max(0f, track.settleTime) : SpiritRollContactTrack.DefaultSettleTime;
            Duration = Mathf.Max(AudioDelay + recordingDuration - RecordingStartTime,
                contacts[contacts.Length - 1] + settleTime);
        }

        public float GetContactTime(int index) => contacts[index];

        public float GetFirstContactTime(int ordinal)
        {
            // A shared impact starts the group landing; individual bays land a few frames apart.
            return Mathf.Min(Duration, Mathf.Min(LatestFirstContact, contacts[0] + ordinal * FirstContactStagger));
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
            Vector3 axis = new Vector3(1f, 0f, ordinal % 2 == 0 ? TumbleAxisLean : -TumbleAxisLean).normalized;
            Quaternion contactRotation = Quaternion.AngleAxis(ContactTilt, axis) * endRotation;
            if (elapsed < firstContact && firstContact > 0f)
            {
                float progress = elapsed / firstContact;
                float arc = Mathf.Sin(progress * Mathf.PI);
                rotation = Quaternion.AngleAxis(arc * LaunchTumble, axis)
                    * Quaternion.Slerp(startRotation, contactRotation, Mathf.SmoothStep(0f, 1f, progress));
                lift = arc * MaximumLift;
                drift = Mathf.Sin(progress * Mathf.PI * 2f) * MaximumDrift;
                return;
            }

            float settleProgress = Mathf.Clamp01((elapsed - firstContact) / Mathf.Max(MinimumSpan, Duration - firstContact));
            float decay = 1f - Mathf.SmoothStep(0f, 1f, settleProgress);
            float tilt = ContactTilt * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(settleProgress * TiltSettleRate)));
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

            float reboundTime = Mathf.Min(MaximumReboundTime, nextContact - previousContact);
            float reboundProgress = Mathf.Clamp01((elapsed - previousContact) / Mathf.Max(MinimumSpan, reboundTime));
            float rebound = 4f * reboundProgress * (1f - reboundProgress);
            float contactDecay = contactOrdinal < 0 ? 1f : 1f / (1f + contactOrdinal * ContactDecayRate);
            lift = rebound * ReboundLift * contactDecay * decay;
            float rock = Mathf.Sin(reboundProgress * Mathf.PI * 2f) * ReboundRock * contactDecay * decay;
            rotation = Quaternion.AngleAxis(tilt + rock, axis) * endRotation;
            drift = Mathf.Sin(reboundProgress * Mathf.PI * 2f) * ReboundDrift * contactDecay * decay;
        }

        internal static SpiritRollTiming ForDuration(float duration)
        {
            duration = Mathf.Max(0f, duration);
            float firstContact = Mathf.Min(UnrecordedLatestFirstContact, duration * UnrecordedFirstContactShare);
            var track = new SpiritRollContactTrack
            {
                contactTimes = new[] { firstContact, duration * UnrecordedSecondContactShare, duration * UnrecordedThirdContactShare },
                settleTime = 0f
            };
            return new SpiritRollTiming(duration, track, 0f, Time.unscaledTimeAsDouble, AudioSettings.dspTime);
        }
    }
}
