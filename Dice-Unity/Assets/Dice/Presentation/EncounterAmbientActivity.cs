using System;
using System.Collections.Generic;
using Dice.Engine;
using UnityEngine;

namespace Dice.Presentation
{
    /** Binds an ambient prop actor's ID to the encounter scene transform its motion moves. */
    [Serializable]
    public sealed class EncounterAmbientPropBinding
    {
        [Tooltip("Prop actor ID in the table's ambient activity.")]
        public string ActorId;
        [Tooltip("Transform the actor's motion offsets; it turns about its pivot unless the motion kind names a contact point.")]
        public Transform Target;
    }

    /**
     * Runs an encounter's ambient activity on its own clock and presentation random, never the engine's streams.
     * Lights and props idle on their schedules from the first hand; a jolt adds a stutter, a burst sound and a prop strike;
     * the encounter's end settles everything or knocks one prop, as the asset authors for its outcome, after which the
     * activity stops at rest.
     * Struck props play their authored sound for the kind of jolt through the optional sound target.
     */
    public sealed class EncounterAmbientActivity
    {
        private enum State { Stopped, Running, Ending }
        private enum JoltKind { Idle, Hit, Defeat }

        private readonly EncounterAmbientActivityAsset asset;
        private readonly IAmbientLightingTarget lighting;
        private readonly IAmbientSoundTarget sound;
        private readonly LightActorState[] lights;
        private readonly PropActorState[] props;
        private System.Random random = new System.Random(0);
        // Sound choices draw from their own random so authored clips never change the lights' and props' sequence.
        private System.Random soundRandom = new System.Random(1);
        private AudioClip lastBurstClip;
        private State state;
        private double time;

        public EncounterAmbientActivity(EncounterAmbientActivityAsset asset, IAmbientLightingTarget lighting,
            IReadOnlyList<EncounterAmbientPropBinding> bindings, IAmbientSoundTarget sound = null)
        {
            this.asset = asset != null ? asset : throw new ArgumentNullException(nameof(asset));
            this.lighting = lighting ?? throw new ArgumentNullException(nameof(lighting));
            this.sound = sound;
            asset.Validate();
            lights = new LightActorState[asset.Lights.Count];
            for (int i = 0; i < lights.Length; ++i)
            {
                var actor = asset.Lights[i];
                if (actor.LightIndex >= lighting.LightCount)
                    throw new InvalidOperationException("Each ambient light actor requires a scene light.");
                lights[i] = new LightActorState(actor);
            }
            props = new PropActorState[asset.Props.Count];
            for (int i = 0; i < props.Length; ++i)
            {
                var actor = asset.Props[i];
                Transform target = null;
                if (bindings != null)
                    foreach (var binding in bindings)
                        if (binding != null && string.Equals(binding.ActorId, actor.Id, StringComparison.Ordinal))
                        {
                            target = binding.Target;
                            break;
                        }
                if (target == null) throw new InvalidOperationException("Ambient prop " + actor.Id + " requires a bound scene transform.");
                props[i] = new PropActorState(actor, actor.Motion.Create(target));
            }
        }

        /** True from the first hand until the encounter ends; idle events and jolts act only then. */
        public bool IsRunning => state == State.Running;

        /** True when every light is at full intensity and every prop rests at its base pose. */
        public bool IsAtRest
        {
            get
            {
                foreach (var light in lights)
                    if (light.Steps.Count > 0 || !double.IsNaN(light.RecoveryStart) || light.Level != 1f) return false;
                foreach (var prop in props)
                    if (prop.Motion.IsMoving || prop.Motion.IsOffset) return false;
                return true;
            }
        }

        /** Starts idle events with a fresh presentation random; does nothing while already running. */
        public void Begin() => Begin(Environment.TickCount);

        public void Begin(int seed)
        {
            if (state == State.Running) return;
            Stop();
            random = new System.Random(seed);
            soundRandom = new System.Random(unchecked(seed * 31 + 17));
            lastBurstClip = null;
            time = 0;
            state = State.Running;
            foreach (var light in lights) light.NextIdle = time + light.Actor.IdleInterval.Pick(random);
            foreach (var prop in props)
            {
                prop.NextIdle = time + prop.Actor.IdleInterval.Pick(random);
                prop.LastIdleClip = prop.LastHitClip = null;
            }
        }

        /** Advances the activity clock; idle events fall due at their scheduled times even across long steps. */
        public void Tick(float deltaTime)
        {
            if (!SceneLightingAsset.Finite(deltaTime) || deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (state == State.Stopped) return;
            double previous = time;
            time += deltaTime;
            foreach (var light in lights)
            {
                // An outage that fell due during a stutter starts once the stutter has finished.
                if (state == State.Running && light.Steps.Count == 0 && time >= light.NextIdle)
                    StartOutage(light, Math.Max(light.NextIdle, previous));
                UpdateLight(light);
            }
            foreach (var prop in props)
            {
                bool struck = false;
                while (state == State.Running && time >= prop.NextIdle)
                {
                    prop.Motion.Strike(prop.NextIdle, prop.Actor.Strength * prop.Actor.IdleStrength.Pick(random));
                    prop.NextIdle += prop.Actor.IdleInterval.Pick(random);
                    struck = true;
                }
                // Idle jolts that fall due together in one long step sound once.
                if (struck) PlayJolt(prop, JoltKind.Idle);
                prop.Motion.Apply(time);
            }
            if (state == State.Ending && IsAtRest) state = State.Stopped;
        }

        /** A jolt to the room: lights not already out stutter briefly, the burst sounds once and one random prop is struck. */
        public void Jolt()
        {
            if (state != State.Running) return;
            foreach (var light in lights)
                if (light.Steps.Count == 0) Play(light, Stutter(light.Actor), false, time);
            if (sound != null)
            {
                lastBurstClip = SpiritEncounterAudio.ChooseClip(asset.BurstClips, lastBurstClip, null, soundRandom);
                if (lastBurstClip != null) sound.PlayAmbientSound(lastBurstClip, asset.BurstVolume);
            }
            if (props.Length == 0) return;
            var prop = props[random.Next(props.Length)];
            prop.Motion.Strike(time, prop.Actor.Strength * asset.JoltStrength);
            PlayJolt(prop, JoltKind.Hit);
        }

        /** Ends the activity as the asset authors for the encounter's outcome. */
        public void End(EncounterOutcome outcome)
        {
            switch (asset.EndingFor(outcome))
            {
                case AmbientEnding.Knock: Knock(); break;
                default: Settle(); break;
            }
        }

        /** Pending outages cancel, lights return steadily to full, props damp to rest and idle events stop. */
        public void Settle()
        {
            if (state != State.Running) return;
            state = State.Ending;
            RecoverLights();
            foreach (var prop in props) prop.Motion.Settle(time, asset.SettleTime);
        }

        /** One random prop knocks hard, lights return steadily to full, and idle events stop. */
        public void Knock()
        {
            if (state != State.Running) return;
            state = State.Ending;
            RecoverLights();
            if (props.Length == 0) return;
            var prop = props[random.Next(props.Length)];
            prop.Motion.Strike(time, prop.Actor.Strength * asset.DefeatStrength);
            PlayJolt(prop, JoltKind.Defeat);
        }

        /** Ends the activity at once: every light at exactly full intensity and every prop at its base pose. */
        public void Stop()
        {
            state = State.Stopped;
            foreach (var light in lights)
            {
                light.Steps.Clear();
                light.Outage = false;
                light.RecoveryStart = double.NaN;
                light.Level = 1f;
                light.Written = 1f;
                lighting.SetLightIntensity(light.Actor.LightIndex, 1f);
            }
            foreach (var prop in props) prop.Motion.Rest();
        }

        /** Plays one of the prop's clips for the kind of jolt, other than the one it played last for that kind. */
        private void PlayJolt(PropActorState prop, JoltKind jolt)
        {
            if (sound == null) return;
            var actor = prop.Actor;
            AudioClip clip;
            float volume;
            switch (jolt)
            {
                case JoltKind.Idle:
                    clip = prop.LastIdleClip = SpiritEncounterAudio.ChooseClip(actor.IdleClips, prop.LastIdleClip, null, soundRandom);
                    volume = actor.IdleVolume;
                    break;
                case JoltKind.Hit:
                    clip = prop.LastHitClip = SpiritEncounterAudio.ChooseClip(actor.HitClips, prop.LastHitClip, null, soundRandom);
                    volume = actor.HitVolume;
                    break;
                default:
                    // The knock happens once, so only the hit fallback needs to avoid a repeat.
                    clip = SpiritEncounterAudio.ChooseClip(actor.DefeatClips, null, null, soundRandom);
                    if (clip == null) clip = SpiritEncounterAudio.ChooseClip(actor.HitClips, prop.LastHitClip, null, soundRandom);
                    volume = actor.DefeatVolume;
                    break;
            }
            if (clip != null) sound.PlayAmbientSound(clip, volume);
        }

        private void RecoverLights()
        {
            foreach (var light in lights)
            {
                light.Steps.Clear();
                light.Outage = false;
                light.RecoveryStart = time;
                light.RecoveryFrom = light.Level;
            }
        }

        private void StartOutage(LightActorState light, double start)
        {
            var actor = light.Actor;
            var steps = new List<FlickerStep>();
            AddFlickers(steps, actor, actor.SputterFlickers.Pick(random), true);
            steps.Add(new FlickerStep(0f, actor.DarkDuration.Pick(random)));
            AddFlickers(steps, actor, actor.SputterFlickers.Pick(random), false);
            Play(light, steps, true, start);
        }

        private List<FlickerStep> Stutter(AmbientLightActor actor)
        {
            var steps = new List<FlickerStep>();
            AddFlickers(steps, actor, Math.Max(1, actor.StutterFlickers.Pick(random)), true);
            return steps;
        }

        /** Flickers alternate dim and bright halves, starting dim when the light is going out. */
        private void AddFlickers(List<FlickerStep> steps, AmbientLightActor actor, int count, bool startDim)
        {
            for (int i = 0; i < count; ++i)
            {
                bool dim = (i % 2 == 0) == startDim;
                steps.Add(new FlickerStep((dim ? actor.DimLevel : actor.BrightLevel).Pick(random), actor.FlickerDuration.Pick(random)));
            }
        }

        private static void Play(LightActorState light, List<FlickerStep> steps, bool outage, double start)
        {
            if (steps.Count == 0) return;
            light.Steps.Clear();
            foreach (var step in steps) light.Steps.Enqueue(step);
            light.StepEnd = start + steps[0].Duration;
            light.Outage = outage;
            light.RecoveryStart = double.NaN;
        }

        private void UpdateLight(LightActorState light)
        {
            while (light.Steps.Count > 0 && time >= light.StepEnd)
            {
                light.Steps.Dequeue();
                if (light.Steps.Count > 0) light.StepEnd += light.Steps.Peek().Duration;
                else if (light.Outage)
                {
                    // The next outage is timed from the moment the light came back on.
                    light.Outage = false;
                    light.NextIdle = light.StepEnd + light.Actor.IdleInterval.Pick(random);
                }
            }
            if (light.Steps.Count > 0) light.Level = light.Steps.Peek().Level;
            else if (!double.IsNaN(light.RecoveryStart))
            {
                float duration = asset.LightRecovery;
                float progress = duration > 0f ? (float)((time - light.RecoveryStart) / duration) : 1f;
                if (progress >= 1f) { light.RecoveryStart = double.NaN; light.Level = 1f; }
                else light.Level = Mathf.Lerp(light.RecoveryFrom, 1f, progress);
            }
            else light.Level = 1f;
            if (light.Level == light.Written) return;
            light.Written = light.Level;
            lighting.SetLightIntensity(light.Actor.LightIndex, light.Level);
        }

        private readonly struct FlickerStep
        {
            public readonly float Level;
            public readonly float Duration;

            public FlickerStep(float level, float duration)
            {
                Level = level;
                Duration = duration;
            }
        }

        private sealed class LightActorState
        {
            public readonly AmbientLightActor Actor;
            public readonly Queue<FlickerStep> Steps = new Queue<FlickerStep>();
            public double StepEnd;
            public double NextIdle;
            public bool Outage;
            public float Level = 1f;
            public float Written = float.NaN;
            public double RecoveryStart = double.NaN;
            public float RecoveryFrom = 1f;

            public LightActorState(AmbientLightActor actor) => Actor = actor;
        }

        private sealed class PropActorState
        {
            public readonly AmbientPropActor Actor;
            public readonly AmbientMotion Motion;
            public double NextIdle;
            public AudioClip LastIdleClip;
            public AudioClip LastHitClip;

            public PropActorState(AmbientPropActor actor, AmbientMotion motion)
            {
                Actor = actor;
                Motion = motion;
            }
        }
    }
}
