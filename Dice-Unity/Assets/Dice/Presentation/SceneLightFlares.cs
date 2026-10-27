using System;
using UnityEngine;

namespace Dice.Presentation
{
    /**
     * Timed raises of scene lights, as multipliers of their authored intensities sampled on absolute time: each flare rises
     * from the light's present raise to its peak, holds, and falls to zero, where the light's own level takes over again.
     */
    public sealed class SceneLightFlares
    {
        private Flare[] flares = Array.Empty<Flare>();

        public int Count => flares.Length;

        /** Sizes the flares for a light count and ends every flare. */
        public void Resize(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            flares = new Flare[count];
        }

        /** Ends every flare at once. */
        public void Clear()
        {
            for (int i = 0; i < flares.Length; ++i) flares[i] = default;
        }

        public void Start(int index, float peak, float rise, float hold, float fall, double time)
        {
            if (index < 0 || index >= flares.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (!SceneLightingAsset.Finite(peak) || peak < 0f) throw new ArgumentOutOfRangeException(nameof(peak));
            if (!SceneLightingAsset.Finite(rise) || rise < 0f) throw new ArgumentOutOfRangeException(nameof(rise));
            if (!SceneLightingAsset.Finite(hold) || hold < 0f) throw new ArgumentOutOfRangeException(nameof(hold));
            if (!SceneLightingAsset.Finite(fall) || fall < 0f) throw new ArgumentOutOfRangeException(nameof(fall));
            if (double.IsNaN(time) || double.IsInfinity(time)) throw new ArgumentOutOfRangeException(nameof(time));
            flares[index] = new Flare(time, Level(index, time), peak, rise, hold, fall);
        }

        /** The light's raise at the given time; zero when no flare is running. */
        public float Level(int index, double time)
        {
            if (index < 0 || index >= flares.Length) throw new ArgumentOutOfRangeException(nameof(index));
            Flare flare = flares[index];
            if (!flare.Active) return 0f;
            float elapsed = (float)(time - flare.Start);
            if (elapsed < 0f) return flare.From;
            if (elapsed < flare.Rise) return Mathf.Lerp(flare.From, flare.Peak, Mathf.SmoothStep(0f, 1f, elapsed / flare.Rise));
            elapsed -= flare.Rise;
            if (elapsed < flare.Hold) return flare.Peak;
            elapsed -= flare.Hold;
            if (elapsed >= flare.Fall) return 0f;
            // The fall eases out, like a flare dying back into the glow.
            float remaining = 1f - elapsed / flare.Fall;
            return flare.Peak * remaining * remaining;
        }

        private readonly struct Flare
        {
            public readonly bool Active;
            public readonly double Start;
            public readonly float From, Peak, Rise, Hold, Fall;

            public Flare(double start, float from, float peak, float rise, float hold, float fall)
            {
                Active = true;
                Start = start;
                From = from;
                Peak = peak;
                Rise = rise;
                Hold = hold;
                Fall = fall;
            }
        }
    }
}
