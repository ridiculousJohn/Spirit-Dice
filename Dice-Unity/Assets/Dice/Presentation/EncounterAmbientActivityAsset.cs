using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /** Inclusive range of nonnegative values picked uniformly by the ambient activity's presentation random. */
    [Serializable]
    public struct AmbientRange
    {
        [Min(0)] public float Min;
        [Min(0)] public float Max;

        public AmbientRange(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public float Pick(System.Random random) => Min + (float)random.NextDouble() * (Max - Min);

        internal void Validate(string name)
        {
            if (!FiniteValue.Is(Min) || !FiniteValue.Is(Max) || Min < 0f || Max < Min)
                throw new InvalidOperationException(name + " requires a finite nonnegative range.");
        }
    }

    /** Inclusive range of counts picked uniformly by the ambient activity's presentation random. */
    [Serializable]
    public struct AmbientCount
    {
        [Min(0)] public int Min;
        [Min(0)] public int Max;

        public AmbientCount(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public int Pick(System.Random random) => random.Next(Min, Max + 1);

        internal void Validate(string name)
        {
            if (Min < 0 || Max < Min) throw new InvalidOperationException(name + " requires a nonnegative range.");
        }
    }

    /** A scene light that sputters out, stays dark and sputters back on its idle schedule, and stutters on jolts. */
    [Serializable]
    public sealed class AmbientLightActor
    {
        [Tooltip("Index into the encounter's scene lighting asset.")]
        [Min(0)] public int LightIndex;
        [Tooltip("Seconds from the end of one outage, or from activation, to the start of the next.")]
        public AmbientRange IdleInterval = new AmbientRange(20f, 40f);
        [Tooltip("Seconds the light stays fully dark between the sputter out and the sputter back.")]
        public AmbientRange DarkDuration = new AmbientRange(1.5f, 4f);
        [Tooltip("Flickers in each sputter before and after an outage.")]
        public AmbientCount SputterFlickers = new AmbientCount(3, 6);
        [Tooltip("Flickers in the brief stutter on a jolt.")]
        public AmbientCount StutterFlickers = new AmbientCount(2, 4);
        [Tooltip("Seconds each flicker holds its level.")]
        public AmbientRange FlickerDuration = new AmbientRange(.04f, .14f);
        [Tooltip("Intensity multipliers for the dim half of a flicker.")]
        public AmbientRange DimLevel = new AmbientRange(0f, .3f);
        [Tooltip("Intensity multipliers for the bright half of a flicker.")]
        public AmbientRange BrightLevel = new AmbientRange(.55f, 1f);

        internal void Validate()
        {
            IdleInterval.Validate(nameof(IdleInterval));
            DarkDuration.Validate(nameof(DarkDuration));
            SputterFlickers.Validate(nameof(SputterFlickers));
            StutterFlickers.Validate(nameof(StutterFlickers));
            FlickerDuration.Validate(nameof(FlickerDuration));
            DimLevel.Validate(nameof(DimLevel));
            BrightLevel.Validate(nameof(BrightLevel));
            if (IdleInterval.Min <= 0f || FlickerDuration.Min <= 0f)
                throw new InvalidOperationException("Ambient light intervals and flickers require positive durations.");
        }
    }

    /** A scene prop, bound by ID in the encounter scene, that its motion kind jolts on idle timers and on the activity's jolts, with optional sounds per kind of jolt. */
    [Serializable]
    public sealed class AmbientPropActor
    {
        [Tooltip("ID the encounter scene's ambient prop binding uses for this prop's transform.")]
        public string Id;
        [Tooltip("Motion kind and its tuning; the prop's existing pose and animation stay underneath.")]
        public AmbientMotionAsset Motion;
        [Tooltip("Strike strength for this prop; the motion kind scales its amplitudes by it.")]
        [Min(0)] public float Strength = 1f;
        [Tooltip("Seconds between idle jolts; a prop group's members take the group's interval instead.")]
        public AmbientRange IdleInterval = new AmbientRange(15f, 35f);
        [Tooltip("Fraction of the prop's strength used by an idle jolt.")]
        public AmbientRange IdleStrength = new AmbientRange(.3f, .7f);
        [Tooltip("Optional sounds for idle jolts; chosen randomly without immediate repeats.")]
        public AudioClip[] IdleClips = Array.Empty<AudioClip>();
        [Range(0, 1)] public float IdleVolume = .3f;
        [Tooltip("Optional sounds when the activity's jolt strikes this prop; chosen randomly without immediate repeats.")]
        public AudioClip[] HitClips = Array.Empty<AudioClip>();
        [Range(0, 1)] public float HitVolume = .5f;
        [Tooltip("Optional sounds for this prop's final knock when the encounter ends with one; the hit clips play when none are assigned.")]
        public AudioClip[] DefeatClips = Array.Empty<AudioClip>();
        [Range(0, 1)] public float DefeatVolume = .6f;

        /** Fails with a message naming the owning activity asset and this prop. */
        internal void Validate(string activity)
        {
            if (string.IsNullOrWhiteSpace(Id)) throw new InvalidOperationException(activity + " requires an ID on each ambient prop actor.");
            string prop = activity + " prop " + Id;
            if (Motion == null) throw new InvalidOperationException(prop + " requires a motion.");
            if (!FiniteValue.Is(Strength) || Strength < 0f)
                throw new InvalidOperationException(prop + " requires a finite nonnegative strength.");
            IdleInterval.Validate(prop + " idle interval");
            IdleStrength.Validate(prop + " idle strength");
            if (IdleInterval.Min <= 0f) throw new InvalidOperationException(prop + " requires a positive idle interval.");
            if (!EncounterAmbientActivityAsset.ValidVolume(IdleVolume) || !EncounterAmbientActivityAsset.ValidVolume(HitVolume) ||
                !EncounterAmbientActivityAsset.ValidVolume(DefeatVolume))
                throw new InvalidOperationException(prop + " requires volumes between zero and one.");
            Motion.Validate(prop);
        }
    }

    /**
     * Props that take turns on one shared idle schedule, such as creatures that come out of hiding one at a time.
     * When the schedule falls due one member, chosen by the activity's random, makes its idle motion; the next turn falls due
     * the group's interval after that member comes to rest, so no two members ever move at once. Members move only on this
     * schedule: jolts and knocks strike only the activity's own props.
     */
    [Serializable]
    public sealed class AmbientPropGroup
    {
        [Tooltip("Seconds from activation, or from the moment the last member to move came to rest, to the next member's turn.")]
        public AmbientRange IdleInterval = new AmbientRange(30f, 60f);
        [Tooltip("Props that take turns; each is bound by ID in the encounter scene like the activity's own props.")]
        public AmbientPropActor[] Props = Array.Empty<AmbientPropActor>();

        public IReadOnlyList<AmbientPropActor> Members => Props ?? Array.Empty<AmbientPropActor>();

        /** Fails with a message naming the owning activity asset, this group and the failing member. */
        internal void Validate(string group)
        {
            IdleInterval.Validate(group + " idle interval");
            if (IdleInterval.Min <= 0f) throw new InvalidOperationException(group + " requires a positive idle interval.");
            if (Members.Count == 0) throw new InvalidOperationException(group + " requires at least one prop.");
            foreach (var member in Members)
            {
                if (member == null) throw new InvalidOperationException(group + " has a missing prop actor.");
                member.Validate(group);
            }
        }
    }

    /** How the ambient activity ends with the encounter. */
    public enum AmbientEnding
    {
        /** Lights return steadily to full and moving props damp to rest. */
        Settle,
        /** One prop knocks hard while lights return steadily to full. */
        Knock
    }

    /** Presentation-only ambient activity authored per encounter table: light outages, prop jolts, props taking turns and their sounds with no effect on play. */
    [CreateAssetMenu(menuName = "Dice/Presentation/Encounter Ambient Activity")]
    public sealed class EncounterAmbientActivityAsset : ScriptableObject
    {
        [SerializeField] private AmbientLightActor[] lights = Array.Empty<AmbientLightActor>();
        [SerializeField] private AmbientPropActor[] props = Array.Empty<AmbientPropActor>();
        [Tooltip("Groups of props that move one at a time on a shared idle schedule, such as creatures that come out of hiding in turn.")]
        [SerializeField] private AmbientPropGroup[] propGroups = Array.Empty<AmbientPropGroup>();
        [Tooltip("Strength multiplier for the prop struck by each jolt a rule presenter asks for.")]
        [SerializeField, Min(0)] private float joltStrength = 1f;
        [Tooltip("Strength multiplier for the final knock when the encounter ends with one.")]
        [SerializeField, Min(0)] private float defeatStrength = 2.5f;
        [Tooltip("How the activity ends when the encounter is won.")]
        [SerializeField] private AmbientEnding wonEnding = AmbientEnding.Settle;
        [Tooltip("How the activity ends when the encounter is lost.")]
        [SerializeField] private AmbientEnding lostEnding = AmbientEnding.Knock;
        [Tooltip("How the activity ends when the encounter is abandoned.")]
        [SerializeField] private AmbientEnding abandonedEnding = AmbientEnding.Settle;
        [Tooltip("Seconds over which every light returns steadily to full when the encounter ends.")]
        [SerializeField, Min(0)] private float lightRecovery = 1f;
        [Tooltip("Time constant in seconds at which moving props damp to rest when the activity settles.")]
        [SerializeField, Min(AmbientMotion.MinimumSettleTime)] private float settleTime = .35f;
        [Tooltip("Optional sounds played once on each jolt, beside the struck prop's hit; chosen randomly without immediate repeats.")]
        [SerializeField] private AudioClip[] burstClips = Array.Empty<AudioClip>();
        [SerializeField, Range(0, 1)] private float burstVolume = .4f;

        public IReadOnlyList<AmbientLightActor> Lights => lights ?? Array.Empty<AmbientLightActor>();
        public IReadOnlyList<AmbientPropActor> Props => props ?? Array.Empty<AmbientPropActor>();
        public IReadOnlyList<AmbientPropGroup> PropGroups => propGroups ?? Array.Empty<AmbientPropGroup>();
        public float JoltStrength => joltStrength;
        public float DefeatStrength => defeatStrength;
        public float LightRecovery => lightRecovery;
        public float SettleTime => settleTime;
        public AudioClip[] BurstClips => burstClips;
        public float BurstVolume => burstVolume;

        /** How the activity ends for the encounter's outcome; an encounter without an outcome settles. */
        public AmbientEnding EndingFor(EncounterOutcome outcome)
        {
            switch (outcome)
            {
                case EncounterOutcome.Won: return wonEnding;
                case EncounterOutcome.Lost: return lostEnding;
                case EncounterOutcome.Abandoned: return abandonedEnding;
                default: return AmbientEnding.Settle;
            }
        }

        internal static bool ValidVolume(float volume) => FiniteValue.Is(volume) && volume >= 0f && volume <= 1f;

        public void Validate()
        {
            var lightIndices = new HashSet<int>();
            foreach (var light in Lights)
            {
                if (light == null) throw new InvalidOperationException("An ambient light actor is missing.");
                light.Validate();
                if (!lightIndices.Add(light.LightIndex))
                    throw new InvalidOperationException("Ambient light actors require distinct light indices.");
            }
            string activity = "Ambient activity " + name;
            var propIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var prop in Props)
            {
                if (prop == null) throw new InvalidOperationException(activity + " has a missing prop actor.");
                prop.Validate(activity);
                if (!propIds.Add(prop.Id)) throw new InvalidOperationException(activity + " prop " + prop.Id + " requires a distinct ID.");
            }
            for (int i = 0; i < PropGroups.Count; ++i)
            {
                string group = activity + " prop group " + i;
                if (PropGroups[i] == null) throw new InvalidOperationException(group + " is missing.");
                PropGroups[i].Validate(group);
                foreach (var member in PropGroups[i].Members)
                    if (!propIds.Add(member.Id)) throw new InvalidOperationException(group + " prop " + member.Id + " requires a distinct ID.");
            }
            if (!FiniteValue.Is(joltStrength) || joltStrength < 0f ||
                !FiniteValue.Is(defeatStrength) || defeatStrength < 0f ||
                !FiniteValue.Is(lightRecovery) || lightRecovery < 0f ||
                !FiniteValue.Is(settleTime) || settleTime <= 0f)
                throw new InvalidOperationException("Ambient activity strengths and timings must be finite and nonnegative.");
            if (!ValidVolume(burstVolume)) throw new InvalidOperationException("The ambient burst volume must be between zero and one.");
        }
    }
}
