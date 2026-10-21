using System;
using UnityEngine;

namespace Dice.Presentation
{
    public enum SceneLitLightMode { Reference = 0, Computed = 1 }

    [Serializable]
    public sealed class SceneLitLayerLight
    {
        [Tooltip("Index into the scene lighting asset's lights; intensity, tint and position follow its sampled state.")]
        [Min(0)] public int LightIndex;
        [Tooltip("Reference adds an aligned lit texture through the light's response; Computed lights the unlit albedo from the light's receiver position.")]
        public SceneLitLightMode Mode;
        [Tooltip("This layer lit by only this light, sampled like the base texture, with the same semantics as the light's Contribution texture. Unassigned keeps the material's texture.")]
        public Texture2D Reference;
        [Tooltip("Aligned linear data texture over the layer frame: R is normalized authored distance; G is the source/glow mask. Disable sRGB when importing. Unassigned keeps the material's texture.")]
        public Texture2D ResponseMap;
        [Tooltip("Scale of a computed light on this layer.")]
        [Min(0f)] public float Strength = 1f;
    }

    /** One foreground layer lit per pixel by the same sampled scene lights as the background. */
    [Serializable]
    public sealed class SceneLitLayer
    {
        [Tooltip("The layer's albedo under ambient light, sampled in place of its source texture. Unassigned keeps the material's source texture.")]
        public Texture2D BaseTexture;
        public SceneLitLayerLight[] Lights = Array.Empty<SceneLitLayerLight>();
        [Tooltip("Linear depth over the layer frame: R is 0 farthest from the viewer and 1 nearest. Disable sRGB when importing. Unassigned uses depth 0.")]
        public Texture2D Depth;
        [Tooltip("Receiver rig Z at depth 0 (x) and depth 1 (y). Receivers face the viewer along -Z, so nearer pixels have lower Z.")]
        public Vector2 DepthRange = new Vector2(0f, -1f);
        [Tooltip("Receiver rig position of the frame's bottom-left corner, relative to the receiver position.")]
        public Vector3 FrameOrigin;
        [Tooltip("Receiver rig displacement across the full frame width.")]
        public Vector3 FrameAxisX = new Vector3(1f, 0f, 0f);
        [Tooltip("Receiver rig displacement across the full frame height.")]
        public Vector3 FrameAxisY = new Vector3(0f, 1f, 0f);
        [Tooltip("The part of the response and depth maps that the image rectangle covers, in normalized bottom-left map coordinates. A cropped image samples full-frame maps through it.")]
        public Rect MapRect = new Rect(0f, 0f, 1f, 1f);
        [Tooltip("Computed lights: 0 lights a flat surface facing the viewer, 1 ignores light direction.")]
        [Range(0f, 1f)] public float Wrap = 1f;

        /** Layers without lights keep the uniform receiver tint. */
        public bool Configured => Lights != null && Lights.Length > 0;

        public void Validate()
        {
            if (Lights == null) throw new InvalidOperationException("Lit layer lights cannot be null.");
            var used = new bool[SceneLightingAsset.MaximumLights];
            foreach (SceneLitLayerLight light in Lights)
            {
                if (light == null || light.LightIndex < 0 || light.LightIndex >= SceneLightingAsset.MaximumLights || used[light.LightIndex])
                    throw new InvalidOperationException("Lit layer lights require unique indices below " + SceneLightingAsset.MaximumLights + ".");
                used[light.LightIndex] = true;
                if (light.Mode != SceneLitLightMode.Reference && light.Mode != SceneLitLightMode.Computed)
                    throw new InvalidOperationException("Lit layer light mode is invalid.");
                if (!SceneLightingAsset.Finite(light.Strength) || light.Strength < 0f)
                    throw new InvalidOperationException("Lit layer light strength must be finite and nonnegative.");
            }
            if (!SceneLightingAsset.Finite(DepthRange.x) || !SceneLightingAsset.Finite(DepthRange.y) || !Finite(FrameOrigin)
                || !Finite(FrameAxisX) || !Finite(FrameAxisY))
                throw new InvalidOperationException("Lit layer frame mapping and depth range must be finite.");
            if (!SceneLightingAsset.Finite(MapRect.x) || !SceneLightingAsset.Finite(MapRect.y) || !SceneLightingAsset.Finite(MapRect.width)
                || !SceneLightingAsset.Finite(MapRect.height) || Mathf.Abs(MapRect.width) < .000001f || Mathf.Abs(MapRect.height) < .000001f)
                throw new InvalidOperationException("Lit layer map rectangle must be finite with a nonzero size.");
            if (!SceneLightingAsset.Finite(Wrap) || Wrap < 0f || Wrap > 1f)
                throw new InvalidOperationException("Lit layer wrap must be between zero and one.");
        }

        internal SceneLitLayerLight Find(int index)
        {
            if (Lights == null) return null;
            foreach (SceneLitLayerLight light in Lights)
                if (light != null && light.LightIndex == index) return light;
            return null;
        }

        private static bool Finite(Vector3 value) => SceneLightingAsset.Finite(value.x) && SceneLightingAsset.Finite(value.y)
            && SceneLightingAsset.Finite(value.z);
    }
}
