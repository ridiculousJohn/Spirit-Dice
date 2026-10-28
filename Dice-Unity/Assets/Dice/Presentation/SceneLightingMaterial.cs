using System;
using UnityEngine;

namespace Dice.Presentation
{
    /** Own one material per view; sampling the same asset and time keeps split scene views synchronized. */
    public sealed class SceneLightingMaterial : IDisposable
    {
        public const int PreviewPass = 1;
        private static readonly int BaseTextureId = Shader.PropertyToID("_BaseTex");
        private static readonly int[] ContributionIds = SceneLightingAsset.LightPropertyIds("_LightTex");
        private static readonly int[] ColorIds = SceneLightingAsset.LightPropertyIds("_SceneLightTint");
        private static readonly int[] IntensityIds = SceneLightingAsset.LightPropertyIds("_LightIntensity");
        private static readonly int[] ReferenceIds = SceneLightingAsset.LightPropertyIds("_LightReference");
        private static readonly int[] ResponseMapIds = SceneLightingAsset.LightPropertyIds("_SceneLightResponseTex");
        private static readonly int[] ResponseEnabledIds = SceneLightingAsset.LightPropertyIds("_SceneLightResponseEnabled");
        private static readonly int[] DistanceFalloffIds = SceneLightingAsset.LightPropertyIds("_SceneLightDistanceFalloff");
        private static readonly int[] SourceResponseIds = SceneLightingAsset.LightPropertyIds("_SceneLightSourceResponse");
        private static readonly int[] ConstrainAmbientIds = SceneLightingAsset.LightPropertyIds("_SceneLightConstrainAmbient");
        private static readonly int[] UvOffsetIds = SceneLightingAsset.LightPropertyIds("_SceneLightUvOffset");
        private static readonly int[] ModulateIds = SceneLightingAsset.LightPropertyIds("_SceneLightModulateByBase");
        public Material Material { get; private set; }

        public SceneLightingMaterial(Shader shader)
        {
            if (shader == null) throw new InvalidOperationException("Scene lighting shader is unavailable.");
            Material = new Material(shader) { name = "Scene Lighting", hideFlags = HideFlags.HideAndDontSave };
        }

        /** Overrides multiply each authored intensity; disabling animation retains the authored light levels. */
        public void Apply(SceneLightingAsset asset, double time, float[] intensityOverrides = null, bool animate = true)
            => Apply(asset, time, intensityOverrides, animate, null);

        public void Apply(SceneLightingState state)
        {
            if (state == null || state.Asset == null) throw new ArgumentException("A sampled lighting asset is required.", nameof(state));
            Apply(state.Asset, state.SampleTime, null, false, state);
        }

        private void Apply(SceneLightingAsset asset, double time, float[] intensityOverrides, bool animate, SceneLightingState state)
        {
            if (Material == null) throw new ObjectDisposedException(nameof(SceneLightingMaterial));
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            asset.Validate();
            if (intensityOverrides != null && intensityOverrides.Length != asset.Lights.Length)
                throw new ArgumentException("Intensity overrides must match the authored light count.", nameof(intensityOverrides));
            if (intensityOverrides != null)
                foreach (float value in intensityOverrides)
                    if (!FiniteValue.Is(value) || value < 0f)
                        throw new ArgumentException("Intensity overrides must be finite and nonnegative.", nameof(intensityOverrides));
            Material.SetTexture(BaseTextureId, asset.BaseTexture);
            for (int i = 0; i < SceneLightingAsset.MaximumLights; ++i)
            {
                SceneLightLayer light = i < asset.Lights.Length ? asset.Lights[i] : null;
                if (light == null)
                {
                    Material.SetTexture(ContributionIds[i], Texture2D.blackTexture);
                    Material.SetFloat(IntensityIds[i], 0f);
                    Material.SetFloat(ReferenceIds[i], 0f);
                    Material.SetVector(ColorIds[i], Vector4.zero);
                    Material.SetTexture(ResponseMapIds[i], Texture2D.blackTexture);
                    Material.SetFloat(ResponseEnabledIds[i], 0f);
                    Material.SetFloat(DistanceFalloffIds[i], 0f);
                    Material.SetFloat(SourceResponseIds[i], 1f);
                    Material.SetFloat(ConstrainAmbientIds[i], 0f);
                    Material.SetVector(UvOffsetIds[i], Vector4.zero);
                    Material.SetFloat(ModulateIds[i], 0f);
                    continue;
                }
                float intensity = state != null ? state.GetIntensity(i) : SceneLightFlicker.EvaluateIntensity(time, light.Intensity,
                    animate ? light.FlickerAmount : 0f, light.FlickerSpeed, light.Seed);
                float multiplier = intensityOverrides != null ? intensityOverrides[i] : 1f;
                intensity = (float)Math.Min(float.MaxValue, (double)intensity * multiplier);
                Color tint = light.Tint.linear;
                Material.SetTexture(ContributionIds[i], light.Contribution);
                Material.SetVector(ColorIds[i], new Vector4(tint.r, tint.g, tint.b, tint.a));
                Material.SetFloat(IntensityIds[i], intensity);
                Material.SetFloat(ReferenceIds[i], light.ContributionIncludesBase ? 1f : 0f);
                Material.SetTexture(ResponseMapIds[i], light.ResponseMap != null ? light.ResponseMap : Texture2D.blackTexture);
                Material.SetFloat(ResponseEnabledIds[i], light.ResponseMap != null ? 1f : 0f);
                Material.SetFloat(DistanceFalloffIds[i], light.DistanceFalloff);
                Material.SetFloat(SourceResponseIds[i], light.SourceResponse);
                Material.SetFloat(ConstrainAmbientIds[i], light.ContributionIncludesBase && light.ConstrainAmbientToReference ? 1f : 0f);
                Vector2 offset = state != null ? state.GetUvOffset(i) : light.UvOffset;
                Material.SetVector(UvOffsetIds[i], new Vector4(offset.x, offset.y, 0f, 0f));
                Material.SetFloat(ModulateIds[i], light.ModulateByBaseColor ? 1f : 0f);
            }
        }

        public void Dispose()
        {
            OwnedObject.Release(Material);
            Material = null;
        }
    }
}
