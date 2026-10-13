using System;

namespace Dice.Presentation
{
    /** Seeded smooth noise samples absolute time without sharing or advancing random state. */
    public static class SceneLightFlicker
    {
        public static float EvaluateIntensity(double time, float intensity, float amount, float speed, int seed)
        {
            if (double.IsNaN(time) || double.IsInfinity(time)) throw new ArgumentOutOfRangeException(nameof(time));
            if (!SceneLightingAsset.Finite(intensity) || intensity < 0f) throw new ArgumentOutOfRangeException(nameof(intensity));
            if (!SceneLightingAsset.Finite(amount) || amount < 0f || amount > 1f) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!SceneLightingAsset.Finite(speed) || speed < 0f) throw new ArgumentOutOfRangeException(nameof(speed));
            if (amount == 0f || speed == 0f || intensity == 0f) return intensity;
            double sample = time * speed;
            double fluctuation = .60 * Noise(sample * .73, seed, 0)
                + .27 * Noise(sample * 2.137, seed, 1)
                + .13 * Noise(sample * 5.731, seed, 2);
            return (float)Math.Min(float.MaxValue, intensity * (1.0 + amount * fluctuation));
        }

        private static double Noise(double time, int seed, int band)
        {
            double position = Math.Floor(time);
            long first = unchecked((long)position);
            double fraction = time - position;
            double weight = fraction * fraction * fraction * (fraction * (fraction * 6.0 - 15.0) + 10.0);
            double start = Hash(first, seed, band);
            return start + (Hash(unchecked(first + 1), seed, band) - start) * weight;
        }

        private static double Hash(long position, int seed, int band)
        {
            unchecked
            {
                ulong value = (ulong)position ^ ((ulong)(uint)seed << 32) ^ ((ulong)(uint)band * 0x9e3779b97f4a7c15UL);
                value += 0x9e3779b97f4a7c15UL;
                value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
                value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
                value ^= value >> 31;
                return (value >> 11) * (2.0 / 9007199254740991.0) - 1.0;
            }
        }
    }
}
