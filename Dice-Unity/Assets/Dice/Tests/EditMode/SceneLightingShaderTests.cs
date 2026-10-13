using System;
using System.Collections.Generic;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dice.Tests
{
    public sealed class SceneLightingShaderTests
    {
        private const int SampleCount = 4;
        private const int RenderWidth = 16;
        private const int RenderHeight = 4;
        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
        private SceneLightingMaterial material;

        [SetUp]
        public void SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Scene lighting pixels require a real graphics device.");
            Assert.That(QualitySettings.activeColorSpace, Is.EqualTo(ColorSpace.Linear));
            Assert.That(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat), Is.True);
            Assert.That(SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat), Is.True);
            Shader shader = Shader.Find("Dice/Scene Lighting");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            material = new SceneLightingMaterial(shader);
        }

        [TearDown]
        public void TearDown()
        {
            material?.Dispose();
            material = null;
            foreach (UnityEngine.Object item in objects) UnityEngine.Object.DestroyImmediate(item);
            objects.Clear();
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void EachSlotUsesItsOwnContributionAndResponseMap(int active)
        {
            Color ambient = new Color(.03f, .04f, .05f, 1f);
            Color[] colors = { new Color(.3f, .1f, .05f), new Color(.06f, .4f, .12f),
                new Color(.08f, .13f, .5f), new Color(.4f, .2f, .1f) };
            float[] intensities = { .2f, .4f, .65f, 1.6f };
            float[] distances = { .2f, .4f, .8f, .1f };
            float[] masks = { 0f, 1f, .4f, 0f };
            float[] exponents = { .5f, .6f, 1.5f, 2f };
            var lights = new SceneLightLayer[SampleCount];
            for (int i = 0; i < lights.Length; ++i)
            {
                Color[] contribution = Solid(Color.black);
                contribution[i] = colors[i];
                lights[i] = Layer(contribution, Solid(new Color(distances[i], masks[i], 0f, 1f)), i + 1f, exponents[i]);
            }
            SceneLightingAsset asset = Asset(Solid(ambient), lights);
            var overrides = new float[SampleCount];
            overrides[active] = intensities[active];
            Color[] expected = Solid(ambient);
            expected[active] += colors[active] * Scale(intensities[active], distances[active], masks[active], active + 1f, exponents[active]);
            Pixels(asset, overrides, expected);
        }

        [Test]
        public void AllSlotsAddTheirIndependentlyScaledContributions()
        {
            Color ambient = new Color(.03f, .04f, .05f, 1f);
            Color[] colors = { new Color(.3f, .1f, .05f), new Color(.06f, .4f, .12f),
                new Color(.08f, .13f, .5f), new Color(.4f, .2f, .1f) };
            float[] intensities = { .2f, .4f, .65f, 1.6f };
            var lights = new SceneLightLayer[SampleCount];
            Color expected = ambient;
            for (int i = 0; i < lights.Length; ++i)
            {
                float distance = .2f * i;
                float mask = .25f * i;
                lights[i] = Layer(Solid(colors[i]), Solid(new Color(distance, mask, 0f, 1f)), i + 1f, .5f);
                expected += colors[i] * Scale(intensities[i], distance, mask, i + 1f, .5f);
            }
            Pixels(Asset(Solid(ambient), lights), intensities, Solid(expected));
        }

        [Test]
        public void AuthoredTintAndColorTexturesAreConvertedToLinear()
        {
            var ambient = new Color32(32, 48, 64, 255);
            var contribution = new Color32(96, 128, 160, 255);
            var light = new SceneLightLayer
            {
                Contribution = ColorTexture(contribution),
                Tint = new Color(.5f, .75f, 1f),
                FlickerAmount = 0f
            };
            var asset = Asset(Solid(Color.black), light);
            asset.BaseTexture = ColorTexture(ambient);
            Pixels(asset, new[] { .75f }, Solid(((Color)ambient).linear + ((Color)contribution).linear * light.Tint.linear * .75f));
        }

        [Test]
        public void LowIntensityUsesLinearDistanceAndSourceMaskWithoutChangingHue()
        {
            Color contribution = new Color(.48f, .24f, .12f, 1f);
            Color[] response = { new Color(0f, 0f, 0f, 1f), new Color(1f, 0f, 0f, 1f),
                new Color(1f, 1f, 0f, 1f), new Color(.5f, .25f, 0f, 1f) };
            var light = Layer(Solid(contribution), response, 2f, .5f);
            // Fractional map values distinguish linear distance and mask data from sRGB samples.
            float[] scales = { .25f, .015625f, .5f, .171875f };
            var expected = new Color[SampleCount];
            for (int i = 0; i < expected.Length; ++i) expected[i] = contribution * scales[i];
            Color[] actual = Pixels(Asset(Solid(Color.black), light), new[] { .25f }, expected);
            Assert.That(actual[1].r, Is.LessThan(actual[0].r * .1f));
            Assert.That(actual[2].r, Is.GreaterThan(actual[0].r));
            foreach (Color pixel in actual)
            {
                Assert.That(pixel.r, Is.GreaterThanOrEqualTo(0f));
                Assert.That(pixel.g / pixel.r, Is.EqualTo(.5f).Within(.001f));
                Assert.That(pixel.b / pixel.r, Is.EqualTo(.25f).Within(.001f));
            }
        }

        [Test]
        public void ResponseEndpointsRemainFinite(
            [Values(0f, 1f, 2.5f)] float intensity,
            [Values(.1f, .5f, 1f, 2f)] float exponent)
        {
            Color ambient = new Color(.02f, .03f, .04f, 1f);
            Color contribution = new Color(.2f, .3f, .4f, 1f);
            Color[] response = { new Color(0f, 0f, 0f, 1f), new Color(1f, 0f, 0f, 1f),
                new Color(.5f, 1f, 0f, 1f), new Color(.5f, .5f, 0f, 1f) };
            var light = Layer(Solid(contribution), response, 4f, exponent);
            var expected = new Color[SampleCount];
            for (int i = 0; i < expected.Length; ++i)
                expected[i] = ambient + contribution * Scale(intensity, response[i].r, response[i].g, 4f, exponent);
            Pixels(Asset(Solid(ambient), light), new[] { intensity }, expected);
        }

        [Test]
        public void ZeroIntensityWithZeroFalloffProducesOnlyAmbient()
        {
            Color ambient = new Color(.02f, .03f, .04f, 1f);
            var light = Layer(Solid(Color.white), Solid(new Color(0f, .5f, 0f, 1f)), 0f, .1f);
            Pixels(Asset(Solid(ambient), light), new[] { 0f }, Solid(ambient));
        }

        [Test]
        public void MissingResponseMapRetainsLinearIntensityScaling(
            [Values(0f, .25f, 1f, 2.5f)] float intensity,
            [Values(0f, 4f)] float falloff,
            [Values(.1f, 2f)] float exponent)
        {
            var encoded = new Color32(96, 128, 160, 255);
            Color ambient = new Color(.02f, .03f, .04f, 1f);
            var light = Layer(Solid(Color.black), null, falloff, exponent);
            light.Contribution = ColorTexture(encoded);
            Pixels(Asset(Solid(ambient), light), new[] { intensity }, Solid(ambient + ((Color)encoded).linear * intensity));
        }

        [TestCase(0f)]
        [TestCase(.5f)]
        [TestCase(1f)]
        public void FullReferenceSubtractsAmbientAndClampsNegativeChannels(float intensity)
        {
            var ambient = new Color32(32, 48, 64, 255);
            var reference = new Color32(96, 24, 96, 255);
            Color linearAmbient = ((Color)ambient).linear;
            Color linearReference = ((Color)reference).linear;
            Color extracted = new Color(Mathf.Max(linearReference.r - linearAmbient.r, 0f),
                Mathf.Max(linearReference.g - linearAmbient.g, 0f), Mathf.Max(linearReference.b - linearAmbient.b, 0f));
            var light = new SceneLightLayer { Contribution = ColorTexture(reference), ContributionIncludesBase = true, FlickerAmount = 0f };
            var asset = Asset(Solid(Color.black), light);
            asset.BaseTexture = ColorTexture(ambient);
            Pixels(asset, new[] { intensity }, Solid(linearAmbient + extracted * intensity));
        }

        [Test]
        public void ReferenceResponsePreservesTheExtractedHue()
        {
            Color ambient = new Color(.3f, .2f, .1f, 1f);
            var light = Layer(Solid(new Color(.1f, .4f, .5f, 1f)), Solid(new Color(.5f, 0f, 0f, 1f)), 2f, .5f);
            light.ContributionIncludesBase = true;
            Color[] actual = Pixels(Asset(Solid(ambient), light), new[] { .25f }, Solid(ambient + new Color(0f, .2f, .4f) * .0625f));
            foreach (Color pixel in actual)
            {
                Color extracted = pixel - ambient;
                Assert.That(extracted.r, Is.EqualTo(0f).Within(.00001f));
                Assert.That(extracted.g / extracted.b, Is.EqualTo(.5f).Within(.001f));
            }
        }

        [TestCase(0f, 0f)]
        [TestCase(1f, 0f)]
        [TestCase(0f, 1f)]
        [TestCase(.25f, .4f)]
        public void ConstrainedAmbientReconstructsReferencesBeforeExtraction(float first, float second)
        {
            Color referenceA = new Color(.6f, .18f, .1f, 1f);
            Color referenceB = new Color(.15f, .5f, .08f, 1f);
            Color ambient = new Color(.15f, .18f, .08f, 1f);
            var a = Layer(Solid(referenceA), Solid(new Color(.8f, .3f, 0f, 1f)), 3f, .5f);
            var b = Layer(Solid(referenceB), Solid(new Color(.2f, .7f, 0f, 1f)), 2f, .6f);
            a.ContributionIncludesBase = b.ContributionIncludesBase = true;
            a.ConstrainAmbientToReference = b.ConstrainAmbientToReference = true;
            var asset = Asset(Solid(new Color(.4f, .3f, .2f, 1f)), a, b);
            Color expected = ambient + (referenceA - ambient) * Scale(first, .8f, .3f, 3f, .5f)
                + (referenceB - ambient) * Scale(second, .2f, .7f, 2f, .6f);
            Pixels(asset, new[] { first, second }, Solid(expected));
            asset.Lights = new[] { b, a };
            Pixels(asset, new[] { second, first }, Solid(expected));
        }

        [Test]
        public void AmbientConstraintRequiresAFullReferenceAndClearsWhenDisabled()
        {
            Color ambient = new Color(.4f, .3f, .2f, 1f);
            var light = Layer(Solid(new Color(.15f, .18f, .08f, 1f)), null, 0f, 1f);
            light.ConstrainAmbientToReference = true;
            var asset = Asset(Solid(ambient), light);
            Pixels(asset, new[] { 0f }, Solid(ambient));
            light.ContributionIncludesBase = true;
            Pixels(asset, new[] { 0f }, Solid(new Color(.15f, .18f, .08f, 1f)));
            light.ConstrainAmbientToReference = false;
            Pixels(asset, new[] { 0f }, Solid(ambient));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ChangingResponseMapReplacesThePreviousResponse(bool replace)
        {
            Color ambient = new Color(.02f, .03f, .04f, 1f);
            Color contribution = new Color(.4f, .2f, .1f, 1f);
            var light = Layer(Solid(contribution), Solid(new Color(1f, 0f, 0f, 1f)), 4f, .1f);
            var asset = Asset(Solid(ambient), light);
            Pixels(asset, new[] { .25f }, Solid(ambient + contribution * .0009765625f));
            light.ResponseMap = replace ? Texture(Solid(new Color(1f, 1f, 0f, 1f))) : null;
            float scale = replace ? (float)Math.Pow(.25, .1) : .25f;
            Pixels(asset, new[] { .25f }, Solid(ambient + contribution * scale));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SwitchingAssetsOrRemovingLayersClearsUnusedSlots(bool removeAll)
        {
            Color ambient = new Color(.02f, .03f, .04f, 1f);
            Color contribution = new Color(.4f, .2f, .1f, 1f);
            var light = Layer(Solid(contribution), Solid(new Color(1f, 1f, 0f, 1f)), 4f, .1f);
            var mapped = Asset(Solid(ambient), light, light, light, light);
            Pixels(mapped, new[] { .25f, .25f, .25f, .25f }, Solid(ambient + contribution * (4f * (float)Math.Pow(.25, .1))));
            if (removeAll)
            {
                mapped.Lights = Array.Empty<SceneLightLayer>();
                Pixels(mapped, Array.Empty<float>(), Solid(ambient));
                mapped.Lights = new[] { Layer(Solid(contribution), null, 4f, 2f) };
                Pixels(mapped, new[] { .25f }, Solid(ambient + contribution * .25f));
            }
            else
            {
                var unmapped = Asset(Solid(ambient), Layer(Solid(contribution), null, 4f, 2f));
                Pixels(unmapped, new[] { .25f }, Solid(ambient + contribution * .25f));
            }
        }

        private SceneLightingAsset Asset(Color[] ambient, params SceneLightLayer[] lights)
        {
            var asset = ScriptableObject.CreateInstance<SceneLightingAsset>();
            objects.Add(asset);
            asset.BaseTexture = Texture(ambient);
            asset.Lights = lights;
            return asset;
        }

        private SceneLightLayer Layer(Color[] contribution, Color[] response, float falloff, float exponent)
            => new SceneLightLayer
            {
                Contribution = Texture(contribution),
                ResponseMap = response != null ? Texture(response) : null,
                DistanceFalloff = falloff,
                SourceResponse = exponent,
                FlickerAmount = 0f
            };

        private Texture2D Texture(Color[] samples)
        {
            Assert.That(samples.Length, Is.EqualTo(SampleCount));
            var texture = new Texture2D(SampleCount, 1, TextureFormat.RGBAFloat, false, true)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            objects.Add(texture);
            texture.SetPixels(samples);
            texture.Apply();
            return texture;
        }

        private Texture2D ColorTexture(Color32 sample)
        {
            var texture = new Texture2D(SampleCount, 1, TextureFormat.RGBA32, false, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            objects.Add(texture);
            texture.SetPixels32(new[] { sample, sample, sample, sample });
            texture.Apply();
            return texture;
        }

        private static Color[] Solid(Color color) => new[] { color, color, color, color };

        private static float Scale(float intensity, float distance, float mask, float falloff, float exponent)
        {
            if (intensity == 0f) return 0f;
            double surface = intensity * Math.Pow(Math.Min(intensity, 1f), distance * falloff);
            double source = Math.Pow(intensity, exponent);
            return (float)(surface * (1f - mask) + source * mask);
        }

        private Color[] Pixels(SceneLightingAsset asset, float[] overrides, Color[] expected)
        {
            material.Apply(asset, 3.25, overrides, false);
            var target = RenderTexture.GetTemporary(RenderWidth, RenderHeight, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            bool previousWrite = GL.sRGBWrite;
            var readback = new Texture2D(RenderWidth, RenderHeight, TextureFormat.RGBAFloat, false, true);
            try
            {
                GL.sRGBWrite = false;
                Graphics.Blit(asset.BaseTexture, target, material.Material, SceneLightingMaterial.PreviewPass);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, RenderWidth, RenderHeight), 0, 0);
                readback.Apply();
                Color[] actual = readback.GetPixels();
                for (int i = 0; i < actual.Length; ++i)
                {
                    Color wanted = expected[(i % RenderWidth) / (RenderWidth / SampleCount)];
                    AssertChannel(actual[i].r, wanted.r, i, "r");
                    AssertChannel(actual[i].g, wanted.g, i, "g");
                    AssertChannel(actual[i].b, wanted.b, i, "b");
                    AssertChannel(actual[i].a, 1f, i, "a");
                }
                return new[] { actual[1], actual[5], actual[9], actual[13] };
            }
            finally
            {
                GL.sRGBWrite = previousWrite;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(readback);
            }
        }

        private static void AssertChannel(float actual, float expected, int pixel, string channel)
        {
            Assert.That(float.IsNaN(actual) || float.IsInfinity(actual), Is.False, "Pixel " + pixel + " channel " + channel);
            Assert.That(actual, Is.EqualTo(expected).Within(.001f * Mathf.Max(1f, Mathf.Abs(expected))), "Pixel " + pixel + " channel " + channel);
        }
    }
}
