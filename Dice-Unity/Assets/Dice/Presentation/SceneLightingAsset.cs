using System;
using UnityEngine;

namespace Dice.Presentation
{
    [Serializable]
    public sealed class SceneLightLayer
    {
        public const float MaximumDistanceFalloff = 4f;
        public const float MinimumSourceResponse = .1f;
        public const float MaximumSourceResponse = 2f;
        public const float MinimumReceiverRange = .01f;
        [Tooltip("A color texture sharing the base image's framing and UV coordinates.")]
        public Texture2D Contribution;
        [Tooltip("Aligned linear data texture: R is normalized authored surface distance; G is the source/glow mask. Disable sRGB when importing.")]
        public Texture2D ResponseMap;
        [Tooltip("Translate this light's contribution and response together in normalized image coordinates.")]
        public Vector2 UvOffset;
        [Tooltip("Multiply the light contribution by ambient RGB so translated illumination preserves painted surface detail.")]
        public bool ModulateByBaseColor;
        public Color Tint = Color.white;
        [Min(0f)] public float Intensity = 1f;
        [Tooltip("Extra artistic surface falloff below full intensity, increasing with the response map's distance. Requires a response map.")]
        [Range(0f, MaximumDistanceFalloff)] public float DistanceFalloff;
        [Tooltip("Intensity exponent for source/glow pixels. Values below one retain visible glow at low power. Requires a response map.")]
        [Range(MinimumSourceResponse, MaximumSourceResponse)] public float SourceResponse = 1f;
        [Range(0f, 1f)] public float FlickerAmount = .12f;
        [Min(0f)] public float FlickerSpeed = 1f;
        public int Seed;
        [Tooltip("Derive light from a full-lit reference by subtracting shared ambient in linear space. Misalignment and painted changes remain an approximation.")]
        public bool ContributionIncludesBase;
        [Tooltip("Constrain shared ambient to this full-lit reference before separating any light layers. Requires Contribution Includes Base.")]
        public bool ConstrainAmbientToReference;
        [Tooltip("Point light position in the receiver rig's local coordinates.")]
        public Vector3 ReceiverPosition = new Vector3(0f, 3f, 2f);
        public Color ReceiverColor = new Color(1f, .64f, .25f, 1f);
        [Min(0f)] public float ReceiverIntensity;
        [Min(MinimumReceiverRange)] public float ReceiverRange = 10f;
    }

    /** Fixed-view lighting combines aligned sRGB color textures with optional linear response maps. */
    [CreateAssetMenu(menuName = "Dice/Presentation/Scene Lighting")]
    public sealed class SceneLightingAsset : ScriptableObject
    {
        public const int MaximumLights = 4;
        public Shader Shader;
        public Texture2D BaseTexture;
        public SceneLightLayer[] Lights = Array.Empty<SceneLightLayer>();
        public Color ReceiverAmbient = new Color(.22f, .25f, .32f, 1f);
        public Color ReceiverKeyColor = new Color(.45f, .5f, .62f, 1f);
        [Tooltip("Direction toward the ambient key light in receiver rig coordinates.")]
        public Vector3 ReceiverKeyDirection = new Vector3(-.35f, .8f, -.45f);

        public void Validate()
        {
            if (Shader == null) throw new InvalidOperationException("The scene lighting " + name + " requires a shader.");
            if (BaseTexture == null) throw new InvalidOperationException("Scene lighting requires a base texture.");
            if (Lights == null || Lights.Length > MaximumLights)
                throw new InvalidOperationException("Scene lighting supports up to " + MaximumLights + " light layers.");
            if (!ValidColor(ReceiverAmbient) || !ValidColor(ReceiverKeyColor)
                || !FiniteValue.Is(ReceiverKeyDirection) || ReceiverKeyDirection.sqrMagnitude < .0001f)
                throw new InvalidOperationException("Scene receiver ambient and key settings are invalid.");
            for (int i = 0; i < Lights.Length; ++i)
            {
                SceneLightLayer light = Lights[i];
                if (light == null || light.Contribution == null)
                    throw new InvalidOperationException("Scene lighting layers require contribution textures.");
                if (!FiniteValue.Is(light.UvOffset))
                    throw new InvalidOperationException("Scene light UV offsets must be finite.");
                if (!FiniteValue.Is(light.Intensity) || light.Intensity < 0f || !FiniteValue.Is(light.FlickerAmount)
                    || light.FlickerAmount < 0f || light.FlickerAmount > 1f
                    || !FiniteValue.Is(light.FlickerSpeed) || light.FlickerSpeed < 0f)
                    throw new InvalidOperationException("Scene light intensity and flicker settings are invalid.");
                if (!FiniteValue.Is(light.DistanceFalloff) || light.DistanceFalloff < 0f || light.DistanceFalloff > SceneLightLayer.MaximumDistanceFalloff
                    || !FiniteValue.Is(light.SourceResponse) || light.SourceResponse < SceneLightLayer.MinimumSourceResponse
                    || light.SourceResponse > SceneLightLayer.MaximumSourceResponse)
                    throw new InvalidOperationException("Scene light distance falloff and source response settings are invalid.");
                if (!FiniteValue.Is(light.Tint) || light.Tint.r < 0f || light.Tint.g < 0f || light.Tint.b < 0f)
                    throw new InvalidOperationException("Scene light tint must be finite and nonnegative.");
                if (!FiniteValue.Is(light.ReceiverPosition) || !ValidColor(light.ReceiverColor)
                    || !FiniteValue.Is(light.ReceiverIntensity) || light.ReceiverIntensity < 0f
                    || !FiniteValue.Is(light.ReceiverRange) || light.ReceiverRange < SceneLightLayer.MinimumReceiverRange)
                    throw new InvalidOperationException("Scene light receiver settings are invalid.");
            }
        }

        /** Shader property ids for one property of each light slot, named by the prefix and the slot index. */
        internal static int[] LightPropertyIds(string prefix)
        {
            var values = new int[MaximumLights];
            for (int i = 0; i < values.Length; ++i) values[i] = Shader.PropertyToID(prefix + i);
            return values;
        }

        private static bool ValidColor(Color value) => FiniteValue.Is(value) && value.r >= 0f && value.g >= 0f && value.b >= 0f;
    }
}
