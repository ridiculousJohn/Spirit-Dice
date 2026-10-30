using System;
using System.Collections.Generic;
using Dice.Presentation;

namespace Dice.Editor
{
    /** The largest jump of one layer value across one host transition, over the idle phases it was sampled at. */
    public readonly struct HostTransitionJump
    {
        public string Transition { get; }
        public int Layer { get; }
        public SpiritPortraitProperty Property { get; }
        /** The deformation channel; 0 for the other properties. */
        public int Channel { get; }
        /** The absolute change of the value as the transition happens, in the property's own unit. */
        public float Jump { get; }
        /** The idle time, in seconds, at which the transition started when the jump was largest. */
        public float Phase { get; }

        public HostTransitionJump(string transition, int layer, SpiritPortraitProperty property, int channel, float jump, float phase)
        {
            Transition = transition;
            Layer = layer;
            Property = property;
            Channel = channel;
            Jump = jump;
            Phase = phase;
        }
    }

    /**
     * Measures how far each layer value of a built host portrait jumps when the host changes state: Arrival starting, a
     * Reaction starting and ending, and each outcome state's entry, for the states the host authors a sequence for. Arrival
     * starts from Waiting, the pose the journey holds before it; every other transition starts from Idle. Each is sampled
     * at phases spread across the host's longest idle loop, because a value without an entry weight jumps by however far the
     * idle motion has carried it. A layer's position, size, rotation and deformation jumps count only where the layer shows
     * both before and after; where it appears or disappears, its alpha jump is the change. Everything goes through the
     * portrait's public API, so the result is what the game shows.
     */
    public static class HostTransitionJumps
    {
        /**
         * Idle phases each transition is sampled at, evenly spread over the longest idle loop: dense enough that a brief idle
         * accent, such as a flicker lasting a second or two in a loop of half a minute, is caught near its peak.
         */
        public const int IdlePhaseCount = 128;
        // Below half an 8-bit step a layer draws nothing.
        private const float HiddenAlpha = .5f / 255f;
        // Seconds the reaction end is straddled by: the jump measured includes this much idle motion, far below any visible
        // change, and the step ends the reaction however its duration rounds.
        private const float ReactionEndStep = .001f;
        private static readonly SpiritPresentationState[] OutcomeStates =
            { SpiritPresentationState.Victory, SpiritPresentationState.Defeat, SpiritPresentationState.Farewell };

        private sealed class Transition
        {
            public string Name;
            // Brings the portrait to the pose before the transition, at the idle phase in seconds; Idle at that phase by default.
            public Action<SpiritHostPortrait, float> Prepare = (portrait, phase) => portrait.Sample(phase);
            public Action<SpiritHostPortrait> Enter;
        }

        /** The largest jump of every layer value across every transition; the portrait must be built with the host and visible. */
        public static IReadOnlyList<HostTransitionJump> Measure(SpiritHostPortrait portrait, SpiritHostPresentationAsset host,
            int phaseCount = IdlePhaseCount)
        {
            if (portrait == null) throw new ArgumentNullException(nameof(portrait));
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (phaseCount < 1) throw new ArgumentOutOfRangeException(nameof(phaseCount));
            float loop = 0f;
            foreach (var sequence in host.Sequences)
                if (sequence.State == SpiritPresentationState.Idle && sequence.Loop) loop = Math.Max(loop, sequence.Duration);
            int phases = loop > 0f ? phaseCount : 1;

            var results = new List<HostTransitionJump>();
            foreach (var transition in Transitions(host))
            {
                float[][] largest = null;
                float[][] largestPhase = null;
                for (int index = 0; index < phases; ++index)
                {
                    float phase = loop * index / phases;
                    transition.Prepare(portrait, phase);
                    float[][] before = Read(portrait, host);
                    transition.Enter(portrait);
                    float[][] after = Read(portrait, host);
                    largest ??= Zeroed(before);
                    largestPhase ??= Zeroed(before);
                    for (int layer = 0; layer < before.Length; ++layer)
                    {
                        bool shows = Shows(before[layer]) && Shows(after[layer]);
                        for (int value = 0; value < before[layer].Length; ++value)
                        {
                            if (!shows && value != (int)SpiritPortraitProperty.Alpha) continue;
                            float jump = Math.Abs(after[layer][value] - before[layer][value]);
                            if (jump <= largest[layer][value]) continue;
                            largest[layer][value] = jump;
                            largestPhase[layer][value] = phase;
                        }
                    }
                }
                for (int layer = 0; layer < largest.Length; ++layer)
                    for (int value = 0; value < largest[layer].Length; ++value)
                    {
                        Address(value, out var property, out int channel);
                        results.Add(new HostTransitionJump(transition.Name, layer, property, channel, largest[layer][value],
                            largestPhase[layer][value]));
                    }
            }
            portrait.ResetPose();
            return results;
        }

        private static IEnumerable<Transition> Transitions(SpiritHostPresentationAsset host)
        {
            if (Authors(host, SpiritPresentationState.Arrival))
                yield return new Transition
                {
                    Name = "Arrival start",
                    // Waiting keeps the idle running, so the idle reaches the phase as it would from Idle.
                    Prepare = (portrait, phase) =>
                    {
                        portrait.Sample(0f);
                        portrait.SetState(SpiritPresentationState.Waiting, 0f);
                        portrait.Advance(phase);
                    },
                    Enter = portrait => portrait.SetState(SpiritPresentationState.Arrival, 0f)
                };
            if (Authors(host, SpiritPresentationState.Reaction))
            {
                yield return new Transition { Name = "Reaction start", Enter = portrait => portrait.React() };
                yield return new Transition
                {
                    Name = "Reaction end",
                    Prepare = (portrait, phase) =>
                    {
                        portrait.Sample(phase);
                        portrait.React();
                        portrait.Advance(Math.Max(0f, portrait.GestureDuration - ReactionEndStep));
                    },
                    Enter = portrait => portrait.Advance(2f * ReactionEndStep)
                };
            }
            foreach (var state in OutcomeStates)
                if (Authors(host, state))
                    yield return new Transition { Name = state + " entry", Enter = portrait => portrait.SetState(state, 0f) };
        }

        private static bool Authors(SpiritHostPresentationAsset host, SpiritPresentationState state)
        {
            foreach (var sequence in host.Sequences)
                if (sequence.State == state) return true;
            return false;
        }

        private static bool Shows(float[] layer) => Math.Min(1f, layer[(int)SpiritPortraitProperty.Alpha]) > HiddenAlpha;

        // Each layer's values in the portrait's order: the properties before Deformation, then one per deformation channel.
        private static float[][] Read(SpiritHostPortrait portrait, SpiritHostPresentationAsset host)
        {
            var values = new float[host.Layers.Length][];
            for (int layer = 0; layer < values.Length; ++layer)
            {
                var mesh = host.Layers[layer].Mesh;
                int channels = mesh != null && mesh.Shapes != null ? mesh.Shapes.Length : 0;
                values[layer] = new float[(int)SpiritPortraitProperty.Deformation + channels];
                for (int value = 0; value < values[layer].Length; ++value)
                {
                    Address(value, out var property, out int channel);
                    values[layer][value] = portrait.ReadLayerValue(layer, property, channel);
                }
            }
            return values;
        }

        private static void Address(int value, out SpiritPortraitProperty property, out int channel)
        {
            bool deformation = value >= (int)SpiritPortraitProperty.Deformation;
            property = deformation ? SpiritPortraitProperty.Deformation : (SpiritPortraitProperty)value;
            channel = deformation ? value - (int)SpiritPortraitProperty.Deformation : 0;
        }

        private static float[][] Zeroed(float[][] shape)
        {
            var zeroed = new float[shape.Length][];
            for (int i = 0; i < shape.Length; ++i) zeroed[i] = new float[shape[i].Length];
            return zeroed;
        }

        /** The unit a portrait property's values are in. */
        public static string Unit(SpiritPortraitProperty property)
        {
            switch (property)
            {
                case SpiritPortraitProperty.Rotation: return "degrees";
                case SpiritPortraitProperty.Alpha: return "alpha";
                case SpiritPortraitProperty.Deformation: return "weight";
                default: return "units";
            }
        }

        /** The property's name, with the deformation channel's shape id when there is one. */
        public static string Describe(SpiritHostPresentationAsset host, int layer, SpiritPortraitProperty property, int channel)
        {
            if (property != SpiritPortraitProperty.Deformation) return property.ToString();
            var shapes = host.Layers[layer].Mesh != null ? host.Layers[layer].Mesh.Shapes : null;
            string shape = shapes != null && channel < shapes.Length ? shapes[channel].Id : null;
            return "Deformation " + (string.IsNullOrEmpty(shape) ? channel.ToString() : shape);
        }
    }
}
