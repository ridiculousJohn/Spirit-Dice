using System;
using UnityEngine;

namespace Dice.Presentation
{
    /** One sampled frame drives painted illumination and scene receivers without advancing separate clocks. */
    public sealed class SceneLightingState
    {
        private readonly float[] intensities = new float[SceneLightingAsset.MaximumLights];
        private readonly Vector2[] uvOffsets = new Vector2[SceneLightingAsset.MaximumLights];
        private readonly Vector3[] receiverPositions = new Vector3[SceneLightingAsset.MaximumLights];
        public SceneLightingAsset Asset { get; private set; }
        public int Count { get; private set; }
        public double SampleTime { get; private set; }

        /** Raised after every sample so lit images can follow whichever view samples their authored lighting. */
        public static event Action<SceneLightingState> Sampled;

        /** Intensities multiply authored levels; optional UV offsets and receiver positions replace them for this sample. */
        public void Sample(SceneLightingAsset asset, double time, float[] intensityOverrides = null,
            Vector2[] uvOffsets = null, Vector3[] receiverPositions = null)
        {
            if (!FiniteValue.Is(time)) throw new ArgumentOutOfRangeException(nameof(time));
            asset?.Validate();
            int count = asset != null ? asset.Lights.Length : 0;
            if (intensityOverrides != null && intensityOverrides.Length != count
                || uvOffsets != null && uvOffsets.Length != count
                || receiverPositions != null && receiverPositions.Length != count)
                throw new ArgumentException("Scene lighting overrides must match the light count.");
            for (int i = 0; i < count; ++i)
            {
                if (intensityOverrides != null && (!FiniteValue.Is(intensityOverrides[i]) || intensityOverrides[i] < 0f)
                    || uvOffsets != null && !FiniteValue.Is(uvOffsets[i])
                    || receiverPositions != null && !FiniteValue.Is(receiverPositions[i]))
                    throw new ArgumentException("Scene lighting overrides must be finite and intensities nonnegative.");
            }
            Asset = asset;
            SampleTime = time;
            Count = asset != null ? asset.Lights.Length : 0;
            for (int i = 0; i < intensities.Length; ++i)
            {
                SceneLightLayer light = i < Count ? asset.Lights[i] : null;
                float multiplier = intensityOverrides != null && i < Count ? intensityOverrides[i] : 1f;
                intensities[i] = light == null ? 0f : (float)Math.Min(float.MaxValue, (double)SceneLightFlicker.EvaluateIntensity(time,
                    light.Intensity, light.FlickerAmount, light.FlickerSpeed, light.Seed) * multiplier);
                this.uvOffsets[i] = light == null ? Vector2.zero : uvOffsets != null ? uvOffsets[i] : light.UvOffset;
                this.receiverPositions[i] = light == null ? Vector3.zero : receiverPositions != null ? receiverPositions[i] : light.ReceiverPosition;
            }
            Sampled?.Invoke(this);
        }

        public float GetIntensity(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return intensities[index];
        }

        public Vector2 GetUvOffset(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return uvOffsets[index];
        }

        public Vector3 GetReceiverPosition(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return receiverPositions[index];
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
                Vector3 offset = receiverPositions[i] - position;
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
