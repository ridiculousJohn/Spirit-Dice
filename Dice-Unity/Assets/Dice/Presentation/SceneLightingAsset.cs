using System;
using UnityEngine;

namespace Dice.Presentation
{
    [Serializable]
    public sealed class SceneLightLayer
    {
        [Tooltip("A color texture sharing the base image's framing and UV coordinates.")]
        public Texture2D Contribution;
        [Tooltip("Aligned linear data texture: R is normalized authored surface distance; G is the source/glow mask. Disable sRGB when importing.")]
        public Texture2D ResponseMap;
        public Color Tint = Color.white;
        [Min(0f)] public float Intensity = 1f;
        [Tooltip("Extra artistic surface falloff below full intensity, increasing with the response map's distance. Requires a response map.")]
        [Range(0f, 4f)] public float DistanceFalloff;
        [Tooltip("Intensity exponent for source/glow pixels. Values below one retain visible glow at low power. Requires a response map.")]
        [Range(.1f, 2f)] public float SourceResponse = 1f;
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
        [Min(.01f)] public float ReceiverRange = 10f;
    }

    /** Fixed-view lighting combines aligned sRGB color textures with optional linear response maps. */
    [CreateAssetMenu(menuName = "Dice/Presentation/Scene Lighting")]
    public sealed class SceneLightingAsset : ScriptableObject
    {
        public const int MaximumLights = 4;
        public Texture2D BaseTexture;
        public SceneLightLayer[] Lights = Array.Empty<SceneLightLayer>();
        public Color ReceiverAmbient = new Color(.22f, .25f, .32f, 1f);
        public Color ReceiverKeyColor = new Color(.45f, .5f, .62f, 1f);
        [Tooltip("Direction toward the ambient key light in receiver rig coordinates.")]
        public Vector3 ReceiverKeyDirection = new Vector3(-.35f, .8f, -.45f);

        public void Validate()
        {
            if (BaseTexture == null) throw new InvalidOperationException("Scene lighting requires a base texture.");
            if (Lights == null || Lights.Length > MaximumLights)
                throw new InvalidOperationException("Scene lighting supports up to four light layers.");
            if (!ValidColor(ReceiverAmbient) || !ValidColor(ReceiverKeyColor)
                || !Finite(ReceiverKeyDirection) || ReceiverKeyDirection.sqrMagnitude < .0001f)
                throw new InvalidOperationException("Scene receiver ambient and key settings are invalid.");
            for (int i = 0; i < Lights.Length; ++i)
            {
                SceneLightLayer light = Lights[i];
                if (light == null || light.Contribution == null)
                    throw new InvalidOperationException("Scene lighting layers require contribution textures.");
                if (!Finite(light.Intensity) || light.Intensity < 0f || !Finite(light.FlickerAmount)
                    || light.FlickerAmount < 0f || light.FlickerAmount > 1f
                    || !Finite(light.FlickerSpeed) || light.FlickerSpeed < 0f)
                    throw new InvalidOperationException("Scene light intensity and flicker settings are invalid.");
                if (!Finite(light.DistanceFalloff) || light.DistanceFalloff < 0f || light.DistanceFalloff > 4f
                    || !Finite(light.SourceResponse) || light.SourceResponse < .1f || light.SourceResponse > 2f)
                    throw new InvalidOperationException("Scene light distance falloff and source response settings are invalid.");
                if (!Finite(light.Tint.r) || !Finite(light.Tint.g) || !Finite(light.Tint.b)
                    || !Finite(light.Tint.a) || light.Tint.r < 0f || light.Tint.g < 0f || light.Tint.b < 0f)
                    throw new InvalidOperationException("Scene light tint must be finite and nonnegative.");
                if (!Finite(light.ReceiverPosition) || !ValidColor(light.ReceiverColor)
                    || !Finite(light.ReceiverIntensity) || light.ReceiverIntensity < 0f
                    || !Finite(light.ReceiverRange) || light.ReceiverRange < .01f)
                    throw new InvalidOperationException("Scene light receiver settings are invalid.");
            }
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool ValidColor(Color value) => Finite(value.r) && Finite(value.g) && Finite(value.b)
            && Finite(value.a) && value.r >= 0f && value.g >= 0f && value.b >= 0f;
    }
}
