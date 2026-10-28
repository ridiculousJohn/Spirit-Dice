using System;
using System.Collections.Generic;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Dice.Tests.TestPixels;

namespace Dice.Tests
{
    public sealed class SceneLitLayerShaderTests
    {
        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
        private readonly List<IDisposable> materials = new List<IDisposable>();
        private Shader litShader;
        private Shader previewShader;

        [SetUp]
        public void SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Lit layer pixels require a real graphics device.");
            Assert.That(QualitySettings.activeColorSpace, Is.EqualTo(ColorSpace.Linear));
            Assert.That(SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat), Is.True);
            litShader = TestShaders.Load(TestShaders.SceneLitImage);
            Assert.That(litShader.isSupported, Is.True);
            Assert.That(ShaderUtil.ShaderHasError(litShader), Is.False);
            previewShader = TestShaders.Load(TestShaders.SceneLitImagePreview);
            Assert.That(previewShader.name, Is.EqualTo(SceneLitLayerMaterial.PreviewShaderName));
            Assert.That(previewShader.isSupported, Is.True);
            Assert.That(ShaderUtil.ShaderHasError(previewShader), Is.False);
            Assert.That(Shader.Find(SceneLitLayerMaterial.PreviewShaderName), Is.SameAs(previewShader));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (IDisposable material in materials) material.Dispose();
            materials.Clear();
            foreach (UnityEngine.Object item in objects) UnityEngine.Object.DestroyImmediate(item);
            objects.Clear();
        }

        [Test]
        public void ZeroIntensityRendersExactlyTheUnlitBase()
        {
            Color[] albedo = { new Color(.1f, .2f, .3f, 1f), new Color(.6f, .5f, .4f, .5f), new Color(0f, .9f, .05f, 1f), new Color(.33f, .33f, .7f, .25f) };
            SceneLightLayer areaLight = Light(Solid(new Color(.9f, .8f, .7f, 1f)), Solid(new Color(.3f, .2f, 0f, 1f)), true);
            SceneLightLayer pointLight = Light(Solid(Color.white), null, false);
            pointLight.ReceiverPosition = new Vector3(.5f, .5f, -.5f);
            pointLight.ReceiverIntensity = 4f;
            SceneLightingAsset asset = Asset(Solid(Color.black), areaLight, pointLight);
            var layer = new SceneLitLayer
            {
                BaseTexture = Texture(albedo),
                Lights = new[]
                {
                    new SceneLitLayerLight { LightIndex = 0, Mode = SceneLitLightMode.Reference,
                        Reference = Texture(Solid(new Color(1f, 1f, 1f, 1f))), ResponseMap = Texture(Solid(new Color(.5f, .5f, 0f, 1f))) },
                    new SceneLitLayerLight { LightIndex = 1, Mode = SceneLitLightMode.Computed }
                }
            };
            var state = new SceneLightingState();
            state.Sample(asset, 1.5, new[] { 0f, 0f });
            SceneLitLayerMaterial lit = Lit(layer);
            lit.Apply(state, Vector3.zero, new Rect(0f, 0f, 1f, 1f));
            Color[] actual = RenderLit(lit, layer.BaseTexture);
            for (int i = 0; i < actual.Length; ++i)
                Assert.That(actual[i], Is.EqualTo(albedo[SampleOf(i)]), "Pixel " + i);

            // An sRGB base round-trips exactly as the same texture copied without lighting.
            var encoded = new Texture2D(SampleCount, 1, TextureFormat.RGBA32, false, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            objects.Add(encoded);
            encoded.SetPixels32(new[] { new Color32(12, 64, 200, 255), new Color32(255, 128, 3, 255),
                new Color32(77, 77, 77, 128), new Color32(0, 0, 0, 255) });
            encoded.Apply();
            layer.BaseTexture = encoded;
            SceneLitLayerMaterial litEncoded = Lit(layer);
            litEncoded.Apply(state, Vector3.zero, new Rect(0f, 0f, 1f, 1f));
            Color[] lit8 = RenderLit(litEncoded, encoded);
            Color[] copied = Render(null, 0, encoded);
            Assert.That(lit8, Is.EqualTo(copied));
        }

        [TestCase(0f, false)]
        [TestCase(.2f, false)]
        [TestCase(.65f, false)]
        [TestCase(1f, true)]
        [TestCase(1.6f, true)]
        public void ReferenceAndResponseMatchTheBackgroundCompose(float multiplier, bool constrain)
        {
            Color[] ambient = { new Color(.03f, .04f, .05f, 1f), new Color(.2f, .1f, .05f, 1f), new Color(.4f, .3f, .2f, 1f), new Color(.01f, .3f, .02f, 1f) };
            Color[] reference = { new Color(.5f, .3f, .1f, 1f), new Color(.15f, .5f, .1f, 1f), new Color(.6f, .2f, .3f, 1f), new Color(.02f, .1f, .9f, 1f) };
            Color[] response = { new Color(0f, 0f, 0f, 1f), new Color(1f, 0f, 0f, 1f), new Color(.5f, 1f, 0f, 1f), new Color(.25f, .4f, 0f, 1f) };
            SceneLightLayer areaLight = Light(reference, response, true);
            areaLight.Tint = new Color(1f, .8f, .6f, 1f);
            areaLight.FlickerAmount = .15f;
            areaLight.FlickerSpeed = 1.3f;
            areaLight.Seed = 71;
            areaLight.ConstrainAmbientToReference = constrain;
            SceneLightingAsset asset = Asset(ambient, areaLight);
            var state = new SceneLightingState();
            state.Sample(asset, 2.25, new[] { multiplier });
            var background = new SceneLightingMaterial(TestShaders.Load(TestShaders.SceneLighting));
            materials.Add(background);
            background.Apply(state);
            var layer = new SceneLitLayer
            {
                BaseTexture = asset.BaseTexture,
                Lights = new[] { new SceneLitLayerLight { LightIndex = 0, Reference = areaLight.Contribution, ResponseMap = areaLight.ResponseMap } }
            };
            SceneLitLayerMaterial lit = Lit(layer);
            lit.Apply(state, Vector3.zero, new Rect(0f, 0f, 1f, 1f));
            Color[] expected = Render(background.Material, SceneLightingMaterial.PreviewPass, asset.BaseTexture);
            Color[] actual = RenderLit(lit, asset.BaseTexture);
            for (int i = 0; i < actual.Length; ++i)
            {
                AssertChannel(actual[i].r, expected[i].r, i, "r");
                AssertChannel(actual[i].g, expected[i].g, i, "g");
                AssertChannel(actual[i].b, expected[i].b, i, "b");
                AssertChannel(actual[i].a, expected[i].a, i, "a");
            }
            if (multiplier > 0f) Assert.That(actual[5].g, Is.GreaterThan(ambient[1].g), "The reference must light the layer.");
        }

        [Test]
        public void ComputedPointLightFallsOffWithFrameDistance()
        {
            Color albedo = new Color(.5f, .4f, .3f, 1f);
            SceneLightLayer pointLight = Light(Solid(Color.white), null, false);
            pointLight.ReceiverPosition = new Vector3(0f, .5f, -1f);
            pointLight.ReceiverColor = new Color(1f, .7f, .4f, 1f);
            pointLight.ReceiverIntensity = 3f;
            pointLight.ReceiverRange = 8f;
            pointLight.DistanceFalloff = .5f;
            SceneLightingAsset asset = Asset(Solid(Color.black), pointLight);
            var layer = new SceneLitLayer
            {
                BaseTexture = Texture(Solid(albedo)),
                Lights = new[] { new SceneLitLayerLight { LightIndex = 0, Mode = SceneLitLightMode.Computed, Strength = .8f } },
                FrameAxisX = new Vector3(4f, 0f, 0f),
                FrameAxisY = new Vector3(0f, 1f, 0f)
            };
            var state = new SceneLightingState();
            state.Sample(asset, 0, new[] { .75f });
            SceneLitLayerMaterial lit = Lit(layer);
            lit.Apply(state, Vector3.zero, new Rect(0f, 0f, 1f, 1f));
            Color[] actual = RenderLit(lit, layer.BaseTexture);
            for (int i = 0; i < actual.Length; ++i)
            {
                Vector2 uv = PixelUv(i);
                Vector3 position = new Vector3(uv.x * 4f, uv.y, 0f);
                Color expected = albedo + albedo * Point(position, pointLight, state.GetIntensity(0), layer.Wrap) * .8f;
                AssertChannel(actual[i].r, expected.r, i, "r");
                AssertChannel(actual[i].g, expected.g, i, "g");
                AssertChannel(actual[i].b, expected.b, i, "b");
            }
            int row = RenderWidth;
            for (int x = 1; x < RenderWidth; ++x)
                Assert.That(actual[row + x].r, Is.LessThan(actual[row + x - 1].r), "Light must fall off away from the source at column " + x);
            Assert.That(actual[row].r, Is.GreaterThan(albedo.r * 1.05f));
        }

        [Test]
        public void ComputedPointLightBrightensNearerDepth()
        {
            Color albedo = new Color(.4f, .4f, .4f, 1f);
            SceneLightLayer pointLight = Light(Solid(Color.white), null, false);
            pointLight.ReceiverPosition = new Vector3(0f, 0f, -3f);
            pointLight.ReceiverColor = Color.white;
            pointLight.ReceiverIntensity = 6f;
            pointLight.ReceiverRange = 10f;
            SceneLightingAsset asset = Asset(Solid(Color.black), pointLight);
            float[] depth = { 0f, 1f / 3f, 2f / 3f, 1f };
            var layer = new SceneLitLayer
            {
                BaseTexture = Texture(Solid(albedo)),
                Lights = new[] { new SceneLitLayerLight { LightIndex = 0, Mode = SceneLitLightMode.Computed } },
                Depth = Texture(new[] { Gray(depth[0]), Gray(depth[1]), Gray(depth[2]), Gray(depth[3]) }),
                DepthRange = new Vector2(0f, -2f),
                FrameAxisX = Vector3.zero,
                FrameAxisY = Vector3.zero,
                Wrap = 0f
            };
            var state = new SceneLightingState();
            state.Sample(asset, 0, new[] { 1f });
            SceneLitLayerMaterial lit = Lit(layer);
            lit.Apply(state, Vector3.zero, new Rect(0f, 0f, 1f, 1f));
            Color[] actual = RenderLit(lit, layer.BaseTexture);
            for (int i = 0; i < actual.Length; ++i)
            {
                Vector3 position = new Vector3(0f, 0f, Mathf.Lerp(0f, -2f, depth[SampleOf(i)]));
                Color expected = albedo + albedo * Point(position, pointLight, state.GetIntensity(0), layer.Wrap);
                AssertChannel(actual[i].r, expected.r, i, "r");
            }
            for (int sample = 1; sample < SampleCount; ++sample)
                Assert.That(actual[sample * 4 + 1].r, Is.GreaterThan(actual[(sample - 1) * 4 + 1].r),
                    "Nearer depth must receive more light from a light in front of the layer.");

            // Zero intensity leaves only the albedo at every depth.
            state.Sample(asset, 0, new[] { 0f });
            lit.Apply(state, Vector3.zero, new Rect(0f, 0f, 1f, 1f));
            foreach (Color pixel in RenderLit(lit, layer.BaseTexture))
                Assert.That(pixel, Is.EqualTo(albedo));
        }

        [Test]
        public void ResponseAndDepthFollowTheImageRectangleWhileColorFollowsItsUvRectangle()
        {
            Color albedo = new Color(.4f, .4f, .4f, 1f);
            SceneLightLayer pointLight = Light(Solid(Color.white), null, false);
            pointLight.ReceiverPosition = new Vector3(0f, 0f, -3f);
            pointLight.ReceiverColor = Color.white;
            pointLight.ReceiverIntensity = 6f;
            SceneLightingAsset asset = Asset(Solid(Color.black), pointLight);
            float[] depth = { 0f, 1f / 3f, 2f / 3f, 1f };
            var layer = new SceneLitLayer
            {
                BaseTexture = Texture(Solid(albedo)),
                Lights = new[] { new SceneLitLayerLight { LightIndex = 0, Mode = SceneLitLightMode.Computed } },
                Depth = Texture(new[] { Gray(depth[0]), Gray(depth[1]), Gray(depth[2]), Gray(depth[3]) }),
                DepthRange = new Vector2(0f, -2f),
                FrameAxisX = Vector3.zero,
                FrameAxisY = Vector3.zero
            };
            var state = new SceneLightingState();
            state.Sample(asset, 0, new[] { 1f });
            SceneLitLayerMaterial lit = Lit(layer);
            // A UV rectangle stretching the right half of the texture across the image maps texture u back to the full frame.
            lit.Apply(state, Vector3.zero, new Rect(.5f, 0f, .5f, 1f));
            Color[] actual = RenderLit(lit, layer.BaseTexture);
            // Preview texture u in [0, 0.5) maps to frame u below zero, clamped to the first depth sample.
            Vector3 position = new Vector3(0f, 0f, 0f);
            Color expected = albedo + albedo * Point(position, pointLight, state.GetIntensity(0), layer.Wrap);
            AssertChannel(actual[RenderWidth + 1].r, expected.r, RenderWidth + 1, "r");
            position.z = -2f / 3f * 2f;
            expected = albedo + albedo * Point(position, pointLight, state.GetIntensity(0), layer.Wrap);
            // Texture u 0.84375 maps to frame u 0.6875, the third depth sample.
            AssertChannel(actual[RenderWidth + 13].r, expected.r, RenderWidth + 13, "r");
        }

        [Test]
        public void CroppedImagesSampleTheirMapRectangleOfFullFrameResponseAndDepth()
        {
            Color ambient = new Color(.1f, .1f, .1f, 1f);
            Color reference = new Color(.6f, .4f, .2f, 1f);
            float[] distance = { 0f, .25f, .5f, 1f };
            SceneLightLayer areaLight = Light(Solid(reference), new[] { new Color(distance[0], 0f, 0f, 1f), new Color(distance[1], 0f, 0f, 1f),
                new Color(distance[2], 0f, 0f, 1f), new Color(distance[3], 0f, 0f, 1f) }, true);
            areaLight.DistanceFalloff = 3f;
            SceneLightingAsset asset = Asset(Solid(ambient), areaLight);
            var layer = new SceneLitLayer
            {
                BaseTexture = asset.BaseTexture,
                Lights = new[] { new SceneLitLayerLight { LightIndex = 0, Reference = areaLight.Contribution, ResponseMap = areaLight.ResponseMap } },
                // The image covers the right half of the full-frame map.
                MapRect = new Rect(.5f, 0f, .5f, 1f)
            };
            var state = new SceneLightingState();
            state.Sample(asset, 0, new[] { .5f });
            SceneLitLayerMaterial lit = Lit(layer);
            lit.Apply(state, Vector3.zero, new Rect(0f, 0f, 1f, 1f));
            Color[] actual = RenderLit(lit, layer.BaseTexture);
            float intensity = state.GetIntensity(0);
            for (int i = 0; i < actual.Length; ++i)
            {
                // Image u in [0, 0.5) reads map sample 2 and [0.5, 1) reads sample 3.
                float r = distance[2 + (i % RenderWidth) / (RenderWidth / 2)];
                float scale = intensity * Mathf.Pow(Mathf.Min(intensity, 1f), r * areaLight.DistanceFalloff);
                Color expected = ambient + (reference - ambient) * scale;
                AssertChannel(actual[i].r, expected.r, i, "r");
                AssertChannel(actual[i].g, expected.g, i, "g");
            }
            Assert.That(actual[RenderWidth + 1].r, Is.GreaterThan(actual[RenderWidth + 14].r), "Farther map samples must receive less light.");
        }

        private SceneLitLayerMaterial Lit(SceneLitLayer layer)
        {
            var material = new SceneLitLayerMaterial(layer, litShader);
            materials.Add(material);
            return material;
        }

        private SceneLightingAsset Asset(Color[] ambient, params SceneLightLayer[] lights)
        {
            var asset = ScriptableObject.CreateInstance<SceneLightingAsset>();
            objects.Add(asset);
            asset.Shader = TestShaders.Load(TestShaders.SceneLighting);
            asset.BaseTexture = Texture(ambient);
            asset.Lights = lights;
            return asset;
        }

        private SceneLightLayer Light(Color[] contribution, Color[] response, bool includesBase) => new SceneLightLayer
        {
            Contribution = Texture(contribution),
            ResponseMap = response != null ? Texture(response) : null,
            ContributionIncludesBase = includesBase,
            DistanceFalloff = 1.5f,
            SourceResponse = .5f,
            FlickerAmount = 0f
        };

        private Texture2D Texture(Color[] samples) => TestPixels.Texture(samples, objects);

        private static Color Gray(float value) => new Color(value, value, value, 1f);

        private static Vector2 PixelUv(int index)
            => new Vector2((index % RenderWidth + .5f) / RenderWidth, (index / RenderWidth + .5f) / RenderHeight);

        /** The receiver falloff evaluated per pixel by the lit-layer shader. */
        private static Color Point(Vector3 position, SceneLightLayer light, float intensity, float wrap)
        {
            if (intensity <= 0f || light.ReceiverIntensity <= 0f) return Color.clear;
            Vector3 offset = light.ReceiverPosition - position;
            float distanceSquared = offset.sqrMagnitude;
            float distance = Mathf.Sqrt(distanceSquared);
            float normalizedDistance = Mathf.Clamp01(distance / Mathf.Max(light.ReceiverRange, .01f));
            float edge = Mathf.Clamp01(1f - normalizedDistance * normalizedDistance);
            float attenuation = edge * edge / Mathf.Max(1f, distanceSquared);
            float response = intensity * Mathf.Pow(Mathf.Min(intensity, 1f), normalizedDistance * light.DistanceFalloff);
            float facing = distance > .0001f ? Mathf.Clamp01(-offset.z / distance) : 1f;
            float diffuse = Mathf.Lerp(facing, 1f, wrap);
            Color color = light.ReceiverColor.linear * (light.ReceiverIntensity * response * attenuation * diffuse);
            color.a = 0f;
            return color;
        }

        /** The lit composition through the preview shader's single pass on a copy of the lit material. */
        private Color[] RenderLit(SceneLitLayerMaterial lit, Texture source)
        {
            Material preview = SceneLitLayerMaterial.CreatePreview(lit.Material, previewShader);
            objects.Add(preview);
            return Render(preview, 0, source);
        }

        private static void AssertChannel(float actual, float expected, int pixel, string channel)
            => TestPixels.AssertChannel(actual, expected, .0005f, pixel, channel);
    }
}
