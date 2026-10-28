using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    /**
     * The pixel fixture of the shader tests: a strip of linear samples, rendered into a small float target and read
     * back, each sample covering a quarter of the target's width.
     */
    internal static class TestPixels
    {
        public const int SampleCount = 4;
        public const int RenderWidth = 16;
        public const int RenderHeight = 4;

        /** A point-sampled linear float strip of the given samples, recorded in owned for the fixture to destroy. */
        public static Texture2D Texture(Color[] samples, List<Object> owned)
        {
            Assert.That(samples.Length, Is.EqualTo(SampleCount));
            var texture = new Texture2D(SampleCount, 1, TextureFormat.RGBAFloat, false, true)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            owned.Add(texture);
            texture.SetPixels(samples);
            texture.Apply();
            return texture;
        }

        public static Color[] Solid(Color color) => new[] { color, color, color, color };

        /** The sample a target pixel shows. */
        public static int SampleOf(int pixel) => (pixel % RenderWidth) / (RenderWidth / SampleCount);

        /** Renders the source through the material's pass, or copies it without a material, and reads the target back. */
        public static Color[] Render(Material material, int pass, Texture source)
        {
            var target = RenderTexture.GetTemporary(RenderWidth, RenderHeight, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            bool previousWrite = GL.sRGBWrite;
            var readback = new Texture2D(RenderWidth, RenderHeight, TextureFormat.RGBAFloat, false, true);
            try
            {
                GL.sRGBWrite = false;
                if (material != null) Graphics.Blit(source, target, material, pass);
                else Graphics.Blit(source, target);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, RenderWidth, RenderHeight), 0, 0);
                readback.Apply();
                return readback.GetPixels();
            }
            finally
            {
                GL.sRGBWrite = previousWrite;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(readback);
            }
        }

        /** Checks a channel is finite and within the tolerance, relative to the expected value above one. */
        public static void AssertChannel(float actual, float expected, float tolerance, int pixel, string channel)
        {
            Assert.That(float.IsNaN(actual) || float.IsInfinity(actual), Is.False, "Pixel " + pixel + " channel " + channel);
            Assert.That(actual, Is.EqualTo(expected).Within(tolerance * Mathf.Max(1f, Mathf.Abs(expected))), "Pixel " + pixel + " channel " + channel);
        }
    }
}
