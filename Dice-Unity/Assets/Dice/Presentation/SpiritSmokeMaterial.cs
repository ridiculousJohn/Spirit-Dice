using System;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /** Each artwork layer owns its animated material and any generated fallback textures. */
    public sealed class SpiritSmokeMaterial : IDisposable
    {
        private static readonly int EffectTimeId = Shader.PropertyToID("_EffectTime");
        private Texture2D generatedNoise, generatedMask;
        public Material Material { get; private set; }

        public SpiritSmokeMaterial(SpiritPortraitSmoke settings, Shader shader)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Spirit smoke shader is unavailable.");
            try
            {
                Material = new Material(shader) { name = "Spirit Portrait Smoke", hideFlags = HideFlags.HideAndDontSave };
                Texture2D noise = settings.FlowNoise != null ? settings.FlowNoise : generatedNoise = CreateDataTexture(128, true);
                Texture2D mask = settings.MotionMask != null ? settings.MotionMask : generatedMask = CreateDataTexture(256, false);
                Material.SetTexture("_NoiseTex", noise);
                Material.SetTexture("_MotionMask", mask);
                Material.SetFloat("_Amount", settings.Distortion);
                Material.SetFloat("_Speed", settings.Speed);
                Material.SetFloat("_Opacity", settings.Opacity);
                Material.SetVector("_Flow", new Vector4(settings.Flow.x, settings.Flow.y, 0f, 0f));
                Material.SetColor("_Color", Color.white);
            }
            catch { Dispose(); throw; }
        }

        public void ApplyTime(double time, Graphic image)
        {
            if (Material == null) return;
            float value = (float)time;
            Material.SetFloat(EffectTimeId, value);
            // UI masks clone the material, so its animated property must follow the source material.
            Material rendered = image != null ? image.materialForRendering : null;
            if (rendered != null && rendered != Material && rendered.shader == Material.shader)
                rendered.SetFloat(EffectTimeId, value);
        }

        public void Dispose()
        {
            DestroyOwned(Material);
            DestroyOwned(generatedNoise);
            DestroyOwned(generatedMask);
            Material = null;
            generatedNoise = generatedMask = null;
        }

        private static Texture2D CreateDataTexture(int size, bool noise)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = noise ? "Spirit smoke noise" : "Spirit smoke motion mask",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = noise ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; ++y)
                for (int x = 0; x < size; ++x)
                {
                    float u = (float)x / (size - 1), v = (float)y / (size - 1);
                    float motion = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.2f, .36f, Mathf.Abs(u - .5f)));
                    pixels[y * size + x] = noise
                        ? new Color(RepeatNoise(u, v, 17f), RepeatNoise(u, v, 83f), 0f, 1f)
                        : new Color(motion, motion, motion, 1f);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static float RepeatNoise(float u, float v, float offset)
        {
            float x = u * 4f + offset, y = v * 4f + offset;
            return Mathf.Lerp(Mathf.Lerp(Mathf.PerlinNoise(x, y), Mathf.PerlinNoise(x - 4f, y), u),
                Mathf.Lerp(Mathf.PerlinNoise(x, y - 4f), Mathf.PerlinNoise(x - 4f, y - 4f), u), v);
        }

        private static void DestroyOwned(UnityEngine.Object owned)
        {
            if (owned == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(owned);
            else UnityEngine.Object.DestroyImmediate(owned);
        }
    }
}
