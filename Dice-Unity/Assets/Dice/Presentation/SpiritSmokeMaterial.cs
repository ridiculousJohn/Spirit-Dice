using System;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Presentation
{
    /**
     * Each smoke layer owns an animated instance of its authored smoke material. The authored material holds the
     * smoke's flow noise, motion mask and values; one without both textures fails construction.
     */
    public sealed class SpiritSmokeMaterial : IDisposable
    {
        private static readonly int EffectTimeId = Shader.PropertyToID("_EffectTime");
        private static readonly int NoiseId = Shader.PropertyToID("_NoiseTex");
        private static readonly int MotionMaskId = Shader.PropertyToID("_MotionMask");
        public Material Material { get; private set; }

        public SpiritSmokeMaterial(Shader shader, Material authored)
        {
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Spirit smoke shader is unavailable.");
            if (authored == null) throw new ArgumentNullException(nameof(authored));
            if (authored.shader != shader)
                throw new InvalidOperationException("The authored smoke material must use the host's smoke shader.");
            if (authored.GetTexture(NoiseId) == null || authored.GetTexture(MotionMaskId) == null)
                throw new InvalidOperationException("The smoke material " + authored.name + " requires flow noise and motion mask textures.");
            Material = new Material(authored) { name = authored.name + " Instance", hideFlags = HideFlags.HideAndDontSave };
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
            if (Material != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(Material);
                else UnityEngine.Object.DestroyImmediate(Material);
            }
            Material = null;
        }
    }
}
