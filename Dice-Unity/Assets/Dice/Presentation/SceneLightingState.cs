using System;
using UnityEngine;

namespace Dice.Presentation
{
    /** One sampled frame drives painted illumination and scene receivers without advancing separate clocks. */
    public sealed class SceneLightingState
    {
        private readonly float[] intensities = new float[SceneLightingAsset.MaximumLights];
        public SceneLightingAsset Asset { get; private set; }
        public int Count { get; private set; }
        public double SampleTime { get; private set; }

        public void Sample(SceneLightingAsset asset, double time)
        {
            if (double.IsNaN(time) || double.IsInfinity(time)) throw new ArgumentOutOfRangeException(nameof(time));
            asset?.Validate();
            Asset = asset;
            SampleTime = time;
            Count = asset != null ? asset.Lights.Length : 0;
            for (int i = 0; i < intensities.Length; ++i)
            {
                SceneLightLayer light = i < Count ? asset.Lights[i] : null;
                intensities[i] = light == null ? 0f : SceneLightFlicker.EvaluateIntensity(time,
                    light.Intensity, light.FlickerAmount, light.FlickerSpeed, light.Seed);
            }
        }

        public float GetIntensity(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return intensities[index];
        }

        /** Approximate a flat painted receiver using the same local-space lights as the 3D trays. */
        public Color EvaluateReceiver(Vector3 position, Vector3 normal)
        {
            if (Asset == null) return Color.white;
            normal = normal.normalized;
            Color result = Asset.ReceiverAmbient.linear + Asset.ReceiverKeyColor.linear
                * Mathf.Max(0f, Vector3.Dot(normal, Asset.ReceiverKeyDirection.normalized));
            for (int i = 0; i < Count; ++i)
            {
                SceneLightLayer light = Asset.Lights[i];
                float intensity = intensities[i];
                if (intensity <= 0f || light.ReceiverIntensity <= 0f) continue;
                Vector3 offset = light.ReceiverPosition - position;
                float distanceSquared = offset.sqrMagnitude;
                float distance = Mathf.Sqrt(distanceSquared);
                float normalizedDistance = Mathf.Clamp01(distance / light.ReceiverRange);
                float edge = Mathf.Clamp01(1f - normalizedDistance * normalizedDistance);
                float attenuation = edge * edge / Mathf.Max(1f, distanceSquared);
                float response = intensity * Mathf.Pow(Mathf.Min(intensity, 1f), normalizedDistance * light.DistanceFalloff);
                float diffuse = distance > .0001f ? Mathf.Max(0f, Vector3.Dot(normal, offset / distance)) : 0f;
                result += light.ReceiverColor.linear * (light.ReceiverIntensity * response * attenuation * diffuse);
            }
            result.a = 1f;
            return result;
        }
    }
}
