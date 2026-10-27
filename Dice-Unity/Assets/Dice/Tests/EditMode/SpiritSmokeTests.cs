using System;
using System.Linq;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    public sealed class SpiritSmokeTests
    {
        private static Shader SmokeShader => AssetDatabase.LoadAssetAtPath<Shader>("Assets/Dice/Presentation/Shared/SpiritSmoke.shader");

        /** Checks the smoke owns a copy of its authored material and releases only that copy / fails if the authored material or its textures are destroyed or the copy outlives disposal. */
        [Test]
        public void DisposalReleasesTheOwnedCopyAndRetainsTheAuthoredTextures()
        {
            var mask = Texture(Color.black);
            var noise = Texture(Color.gray);
            var authored = Authored(noise, mask);
            try
            {
                Material owned;
                using (var wrapper = new SpiritSmokeMaterial(SmokeShader, authored))
                {
                    owned = wrapper.Material;
                    Assert.That(owned, Is.Not.SameAs(authored));
                    Assert.That(owned.GetTexture("_NoiseTex"), Is.SameAs(noise));
                    Assert.That(owned.GetTexture("_MotionMask"), Is.SameAs(mask));
                    wrapper.ApplyTime(1.5, null);
                    Assert.That(owned.GetFloat("_EffectTime"), Is.EqualTo(1.5f));
                }
                Assert.That(owned == null, Is.True);
                Assert.That(authored != null && noise != null && mask != null, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(authored);
                Object.DestroyImmediate(mask);
                Object.DestroyImmediate(noise);
            }
        }

        /** Checks an authored smoke material without flow noise or motion mask fails construction / fails if smoke runs without either texture. */
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void AnAuthoredMaterialWithoutEitherTextureFails(bool withNoise, bool withMask)
        {
            var mask = Texture(Color.black);
            var noise = Texture(Color.gray);
            var authored = Authored(withNoise ? noise : null, withMask ? mask : null);
            try
            {
                Assert.Throws<InvalidOperationException>(() => new SpiritSmokeMaterial(SmokeShader, authored).Dispose());
            }
            finally
            {
                Object.DestroyImmediate(authored);
                Object.DestroyImmediate(mask);
                Object.DestroyImmediate(noise);
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
            var authored = Authored(noise, mask);
            authored.SetFloat("_Opacity", opacity);
            var target = new RenderTexture(4, 4, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(4, 4, TextureFormat.RGBAFloat, false, true);
            var previous = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                using (var wrapper = new SpiritSmokeMaterial(SmokeShader, authored))
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
                Object.DestroyImmediate(authored);
                Object.DestroyImmediate(mask);
                Object.DestroyImmediate(noise);
                Object.DestroyImmediate(smoke);
            }
        }

        private static Material Authored(Texture2D noise, Texture2D mask)
        {
            var material = new Material(SmokeShader) { name = "Test Smoke" };
            material.SetTexture("_NoiseTex", noise);
            material.SetTexture("_MotionMask", mask);
            return material;
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
