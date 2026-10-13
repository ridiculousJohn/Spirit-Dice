using UnityEngine;

namespace Dice.Presentation
{
    /** Applies shared sampled light levels to materials without changing Unity's scene lights. */
    internal static class SceneLightingReceiverMaterial
    {
        private static readonly int EnabledId = Shader.PropertyToID("_SceneLightingEnabled");
        private static readonly int AmbientId = Shader.PropertyToID("_SceneLightingAmbient");
        private static readonly int KeyColorId = Shader.PropertyToID("_SceneLightingKeyColor");
        private static readonly int KeyDirectionId = Shader.PropertyToID("_SceneLightingKeyDirection");
        private static readonly int[] PositionIds = CreateIds("_SceneLightPosition");
        private static readonly int[] ColorIds = CreateIds("_SceneLightColor");
        private static readonly int[] ParametersIds = CreateIds("_SceneLightParameters");

        internal static void Apply(Material material, SceneLightingState state, Transform renderRoot)
        {
            if (material == null || !material.HasProperty(EnabledId)) return;
            SceneLightingAsset asset = state != null ? state.Asset : null;
            material.SetFloat(EnabledId, asset != null ? 1f : 0f);
            if (asset != null)
            {
                Color ambient = asset.ReceiverAmbient.linear;
                Color key = asset.ReceiverKeyColor.linear;
                Vector3 direction = renderRoot.TransformDirection(asset.ReceiverKeyDirection).normalized;
                material.SetVector(AmbientId, new Vector4(ambient.r, ambient.g, ambient.b, 0f));
                material.SetVector(KeyColorId, new Vector4(key.r, key.g, key.b, 0f));
                material.SetVector(KeyDirectionId, new Vector4(direction.x, direction.y, direction.z, 0f));
            }
            else
            {
                material.SetVector(AmbientId, Vector4.zero);
                material.SetVector(KeyColorId, Vector4.zero);
                material.SetVector(KeyDirectionId, Vector4.zero);
            }

            for (int i = 0; i < SceneLightingAsset.MaximumLights; ++i)
            {
                SceneLightLayer light = asset != null && i < state.Count ? asset.Lights[i] : null;
                if (light == null)
                {
                    material.SetVector(PositionIds[i], Vector4.zero);
                    material.SetVector(ColorIds[i], Vector4.zero);
                    material.SetVector(ParametersIds[i], Vector4.zero);
                    continue;
                }
                Vector3 position = renderRoot.TransformPoint(light.ReceiverPosition);
                Color color = light.ReceiverColor.linear;
                material.SetVector(PositionIds[i], new Vector4(position.x, position.y, position.z, light.ReceiverRange));
                material.SetVector(ColorIds[i], new Vector4(color.r, color.g, color.b, 0f));
                material.SetVector(ParametersIds[i], new Vector4(state.GetIntensity(i), light.ReceiverIntensity,
                    light.DistanceFalloff, 0f));
            }
        }

        private static int[] CreateIds(string prefix)
        {
            var values = new int[SceneLightingAsset.MaximumLights];
            for (int i = 0; i < values.Length; ++i) values[i] = Shader.PropertyToID(prefix + i);
            return values;
        }
    }
}
