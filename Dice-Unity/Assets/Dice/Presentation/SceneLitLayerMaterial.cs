using System;
using UnityEngine;

namespace Dice.Presentation
{
    /** Own one lit-layer material per image; each sample writes the shared state's light levels and positions. */
    public sealed class SceneLitLayerMaterial : IDisposable
    {
        public const string PreviewShaderName = "Hidden/Dice/Scene Lit Image Preview";
        private const int Off = 0, Reference = 1, Computed = 2;
        private static readonly int SourceTextureId = Shader.PropertyToID("_SourceTex");
        private static readonly int LayerMaskId = Shader.PropertyToID("_LayerMask");
        private static readonly int InvertMaskId = Shader.PropertyToID("_InvertMask");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int DepthTextureId = Shader.PropertyToID("_SceneLitDepthTex");
        private static readonly int DepthRangeId = Shader.PropertyToID("_SceneLitDepthRange");
        private static readonly int FrameOriginId = Shader.PropertyToID("_SceneLitFrameOrigin");
        private static readonly int FrameAxisXId = Shader.PropertyToID("_SceneLitFrameAxisX");
        private static readonly int FrameAxisYId = Shader.PropertyToID("_SceneLitFrameAxisY");
        private static readonly int FrameTransformId = Shader.PropertyToID("_SceneLitFrameTransform");
        private static readonly int MapRectId = Shader.PropertyToID("_SceneLitMapRect");
        private static readonly int WrapId = Shader.PropertyToID("_SceneLitWrap");
        private const string ModeProperty = "_SceneLitMode";
        private static readonly int[] ModeIds = SceneLightingAsset.LightPropertyIds(ModeProperty);
        private static readonly int[] ReferenceIds = SceneLightingAsset.LightPropertyIds("_SceneLitReferenceTex");
        private static readonly int[] ResponseMapIds = SceneLightingAsset.LightPropertyIds("_SceneLitResponseTex");
        private static readonly int[] ResponseEnabledIds = SceneLightingAsset.LightPropertyIds("_SceneLitResponseEnabled");
        private static readonly int[] StrengthIds = SceneLightingAsset.LightPropertyIds("_SceneLitStrength");
        private static readonly int[] IntensityIds = SceneLightingAsset.LightPropertyIds("_SceneLitIntensity");
        private static readonly int[] TintIds = SceneLightingAsset.LightPropertyIds("_SceneLitTint");
        private static readonly int[] DistanceFalloffIds = SceneLightingAsset.LightPropertyIds("_SceneLitDistanceFalloff");
        private static readonly int[] SourceResponseIds = SceneLightingAsset.LightPropertyIds("_SceneLitSourceResponse");
        private static readonly int[] IncludesBaseIds = SceneLightingAsset.LightPropertyIds("_SceneLitIncludesBase");
        private static readonly int[] ConstrainAmbientIds = SceneLightingAsset.LightPropertyIds("_SceneLitConstrainAmbient");
        private static readonly int[] ModulateIds = SceneLightingAsset.LightPropertyIds("_SceneLitModulateByBase");
        private static readonly int[] PositionIds = SceneLightingAsset.LightPropertyIds("_SceneLitPosition");
        private static readonly int[] LightColorIds = SceneLightingAsset.LightPropertyIds("_SceneLitColor");
        public Material Material { get; private set; }
        public SceneLitLayer Layer { get; }
        public Material Source { get; }

        /** A source already using a lit-layer shader is copied; otherwise its masked sampling is carried into the given shader. */
        public SceneLitLayerMaterial(SceneLitLayer layer, Shader shader, Material source = null)
        {
            Layer = layer ?? throw new ArgumentNullException(nameof(layer));
            layer.Validate();
            Source = source;
            if (source != null && UsesLitShader(source)) Material = new Material(source);
            else
            {
                if (shader == null) throw new InvalidOperationException("Lit layer shader is unavailable.");
                Material = new Material(shader);
                if (source != null)
                {
                    CopyTexture(source, SourceTextureId);
                    CopyTexture(source, LayerMaskId);
                    if (source.HasProperty(InvertMaskId)) Material.SetFloat(InvertMaskId, source.GetFloat(InvertMaskId));
                    if (source.HasProperty(ColorId)) Material.SetColor(ColorId, source.GetColor(ColorId));
                }
            }
            if (!UsesLitShader(Material))
            {
                Dispose();
                throw new InvalidOperationException("Lit layers require a shader with scene lit layer properties.");
            }
            Material.name = "Scene Lit Layer";
            Material.hideFlags = HideFlags.HideAndDontSave;
            if (layer.BaseTexture != null) Material.SetTexture(SourceTextureId, layer.BaseTexture);
            for (int i = 0; i < SceneLightingAsset.MaximumLights; ++i)
            {
                SceneLitLayerLight light = layer.Find(i);
                Material.SetFloat(ModeIds[i], light == null ? Off : light.Mode == SceneLitLightMode.Computed ? Computed : Reference);
                Material.SetFloat(StrengthIds[i], light != null ? light.Strength : 0f);
                if (light != null && light.Reference != null) Material.SetTexture(ReferenceIds[i], light.Reference);
                if (light != null && light.ResponseMap != null) Material.SetTexture(ResponseMapIds[i], light.ResponseMap);
                Material.SetFloat(ResponseEnabledIds[i], Material.GetTexture(ResponseMapIds[i]) != null ? 1f : 0f);
            }
            if (layer.Depth != null) Material.SetTexture(DepthTextureId, layer.Depth);
            Material.SetVector(DepthRangeId, new Vector4(layer.DepthRange.x, layer.DepthRange.y, 0f, 0f));
            Material.SetVector(FrameAxisXId, layer.FrameAxisX);
            Material.SetVector(FrameAxisYId, layer.FrameAxisY);
            Material.SetVector(MapRectId, new Vector4(layer.MapRect.x, layer.MapRect.y, layer.MapRect.width, layer.MapRect.height));
            Material.SetFloat(WrapId, layer.Wrap);
        }

        public static bool UsesLitShader(Material material) => material != null && material.HasProperty(ModeIds[0]);

        /** Whether the shader has scene lit layer properties, so a lit layer material can be made from it. */
        public static bool IsLitShader(Shader shader) => shader != null && shader.FindPropertyIndex(ModeProperty + 0) >= 0;
        /**
         * A copy of a lit material's properties on the preview shader, whose single pass (pass 0) blits the lit composition
         * opaque, without vertex color, alpha or layer mask. It is a separate shader because UGUI draws every pass of a
         * material's shader. The caller destroys the returned material.
         */
        public static Material CreatePreview(Material lit, Shader previewShader)
        {
            if (!UsesLitShader(lit)) throw new ArgumentException("A lit layer material is required.", nameof(lit));
            if (previewShader == null) throw new ArgumentNullException(nameof(previewShader));
            var preview = new Material(previewShader) { name = "Scene Lit Layer Preview", hideFlags = HideFlags.HideAndDontSave };
            preview.CopyPropertiesFromMaterial(lit);
            return preview;
        }

        /** Levels, flicker, bindings and positions come from the sample, exactly as for the background. */
        public void Apply(SceneLightingState state, Vector3 position, Rect uvRect)
        {
            if (state == null || state.Asset == null) throw new ArgumentException("A sampled lighting asset is required.", nameof(state));
            Apply(state.Asset, state, position, uvRect);
        }

        /** Authored levels without flicker, for images no view is currently sampling. */
        public void ApplyAuthored(SceneLightingAsset asset, Vector3 position, Rect uvRect) => Apply(asset, null, position, uvRect);

        private void Apply(SceneLightingAsset asset, SceneLightingState state, Vector3 position, Rect uvRect)
        {
            if (Material == null) throw new ObjectDisposedException(nameof(SceneLitLayerMaterial));
            asset?.Validate();
            int count = state != null ? state.Count : asset != null ? asset.Lights.Length : 0;
            for (int i = 0; i < SceneLightingAsset.MaximumLights; ++i)
            {
                SceneLightLayer light = i < count ? asset.Lights[i] : null;
                if (light == null)
                {
                    Material.SetFloat(IntensityIds[i], 0f);
                    Material.SetVector(TintIds[i], Vector4.zero);
                    Material.SetFloat(DistanceFalloffIds[i], 0f);
                    Material.SetFloat(SourceResponseIds[i], 1f);
                    Material.SetFloat(IncludesBaseIds[i], 0f);
                    Material.SetFloat(ConstrainAmbientIds[i], 0f);
                    Material.SetFloat(ModulateIds[i], 0f);
                    Material.SetVector(PositionIds[i], new Vector4(0f, 0f, 0f, 1f));
                    Material.SetVector(LightColorIds[i], Vector4.zero);
                    continue;
                }
                Color tint = light.Tint.linear;
                Color color = light.ReceiverColor.linear;
                Vector3 lightPosition = state != null ? state.GetReceiverPosition(i) : light.ReceiverPosition;
                Material.SetFloat(IntensityIds[i], state != null ? state.GetIntensity(i) : light.Intensity);
                Material.SetVector(TintIds[i], new Vector4(tint.r, tint.g, tint.b, tint.a));
                Material.SetFloat(DistanceFalloffIds[i], light.DistanceFalloff);
                Material.SetFloat(SourceResponseIds[i], light.SourceResponse);
                Material.SetFloat(IncludesBaseIds[i], light.ContributionIncludesBase ? 1f : 0f);
                Material.SetFloat(ConstrainAmbientIds[i], light.ContributionIncludesBase && light.ConstrainAmbientToReference ? 1f : 0f);
                Material.SetFloat(ModulateIds[i], light.ModulateByBaseColor ? 1f : 0f);
                Material.SetVector(PositionIds[i], new Vector4(lightPosition.x, lightPosition.y, lightPosition.z, light.ReceiverRange));
                Material.SetVector(LightColorIds[i], new Vector4(color.r, color.g, color.b, light.ReceiverIntensity));
            }
            Vector3 origin = position + Layer.FrameOrigin;
            Material.SetVector(FrameOriginId, new Vector4(origin.x, origin.y, origin.z, 0f));
            // Rig position covers the image rectangle, depth and response its map rectangle; color textures follow its UV rectangle.
            bool valid = Mathf.Abs(uvRect.width) > .000001f && Mathf.Abs(uvRect.height) > .000001f
                && FiniteValue.Is(uvRect.x) && FiniteValue.Is(uvRect.y);
            Material.SetVector(FrameTransformId, valid
                ? new Vector4(1f / uvRect.width, 1f / uvRect.height, -uvRect.x / uvRect.width, -uvRect.y / uvRect.height)
                : new Vector4(1f, 1f, 0f, 0f));
        }

        public void Dispose()
        {
            OwnedObject.Release(Material);
            Material = null;
        }

        private void CopyTexture(Material source, int id)
        {
            if (!source.HasProperty(id)) return;
            Material.SetTexture(id, source.GetTexture(id));
            Material.SetTextureScale(id, source.GetTextureScale(id));
            Material.SetTextureOffset(id, source.GetTextureOffset(id));
        }
    }
}
