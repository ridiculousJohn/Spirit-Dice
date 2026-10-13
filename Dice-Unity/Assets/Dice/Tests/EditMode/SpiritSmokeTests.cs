using System.Linq;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class SpiritSmokeTests
    {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void DisposalReleasesOwnedResourcesAndRetainsBorrowedTextures(bool borrowMask, bool borrowNoise)
        {
            var mask = borrowMask ? Texture(Color.black) : null;
            var noise = borrowNoise ? Texture(Color.gray) : null;
            try
            {
                Material owned;
                UnityEngine.Texture usedMask, usedNoise;
                var settings = new SpiritPortraitSmoke { Enabled = true, MotionMask = mask, FlowNoise = noise };
                using (var wrapper = new SpiritSmokeMaterial(settings,
                    AssetDatabase.LoadAssetAtPath<Shader>("Assets/Dice/Presentation/Shared/SpiritSmoke.shader")))
                {
                    owned = wrapper.Material;
                    usedMask = owned.GetTexture("_MotionMask");
                    usedNoise = owned.GetTexture("_NoiseTex");
                    Assert.That(usedMask != null && usedNoise != null, Is.True);
                    if (borrowMask) Assert.That(usedMask, Is.SameAs(mask));
                    if (borrowNoise) Assert.That(usedNoise, Is.SameAs(noise));
                    wrapper.ApplyTime(1.5, null);
                    Assert.That(owned.GetFloat("_EffectTime"), Is.EqualTo(1.5f));
                }
                Assert.That(owned == null, Is.True);
                Assert.That(usedMask != null, Is.EqualTo(borrowMask));
                Assert.That(usedNoise != null, Is.EqualTo(borrowNoise));
            }
            finally
            {
                if (mask != null) Object.DestroyImmediate(mask);
                if (noise != null) Object.DestroyImmediate(noise);
            }
        }

        [TestCase(0f, .8f, .5f)]
        [TestCase(.25f, .8f, .5f)]
        [TestCase(1f, .4f, .75f)]
        public void SmokeOpacityMultipliesTextureAlphaAndLayerAlpha(float opacity, float textureAlpha, float layerAlpha)
        {
            var mask = Texture(Color.black);
            var noise = Texture(Color.gray);
            var smoke = Texture(new Color(1f, 1f, 1f, textureAlpha));
            var target = new RenderTexture(4, 4, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(4, 4, TextureFormat.RGBAFloat, false, true);
            var previous = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                using (var wrapper = new SpiritSmokeMaterial(new SpiritPortraitSmoke
                    { Enabled = true, MotionMask = mask, FlowNoise = noise, Opacity = opacity },
                    AssetDatabase.LoadAssetAtPath<Shader>("Assets/Dice/Presentation/Shared/SpiritSmoke.shader")))
                {
                    target.Create();
                    RenderTexture.active = target;
                    GL.Clear(false, true, Color.clear);
                    GL.sRGBWrite = false;
                    wrapper.Material.SetColor("_Color", new Color(1f, 1f, 1f, layerAlpha));
                    Graphics.Blit(smoke, target, wrapper.Material, 1);
                    readback.ReadPixels(new Rect(0, 0, 4, 4), 0, 0);
                    readback.Apply();
                    Assert.That(readback.GetPixel(2, 2).a, Is.EqualTo(opacity * textureAlpha * layerAlpha).Within(.005f));
                }
            }
            finally
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousSrgb;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(mask);
                Object.DestroyImmediate(noise);
                Object.DestroyImmediate(smoke);
            }
        }

        private static Texture2D Texture(Color color)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
            texture.SetPixels(Enumerable.Repeat(color, 16).ToArray());
            texture.Apply();
            return texture;
        }
    }
}
