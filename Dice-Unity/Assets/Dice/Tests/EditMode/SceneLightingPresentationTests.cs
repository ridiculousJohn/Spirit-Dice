using System;
using System.Collections.Generic;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class SceneLightingPresentationTests
    {
        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
        private SceneLightingAsset asset;
        private RawImage background;
        private RawImage flame;
        private RectTransform source;
        private SceneLightingPresentation presentation;

        [SetUp]
        public void SetUp()
        {
            asset = Own(ScriptableObject.CreateInstance<SceneLightingAsset>());
            asset.Shader = TestShaders.Load(TestShaders.SceneLighting);
            asset.BaseTexture = Texture(new[] { new Color(.1f, .1f, .1f, 1f) });
            asset.Lights = new[]
            {
                new SceneLightLayer { Contribution = Texture(new[] { Color.red }), Intensity = .7f,
                    FlickerAmount = .1f, Seed = 42, ReceiverPosition = new Vector3(-2, 3, -2), ReceiverIntensity = 3f },
                new SceneLightLayer { Contribution = Texture(new[] { Color.green }), Intensity = .5f,
                    FlickerAmount = .2f, Seed = 182, ReceiverPosition = new Vector3(1, 2, -2), ReceiverIntensity = 2f,
                    UvOffset = new Vector2(.01f, -.02f), SourceResponse = .5f }
            };
            background = Image("Background");
            background.rectTransform.pivot = Vector2.zero;
            background.rectTransform.sizeDelta = new Vector2(100, 200);
            source = (RectTransform)Own(new GameObject("Source", typeof(RectTransform))).transform;
            source.SetParent(background.transform, false);
            source.anchorMin = source.anchorMax = Vector2.zero;
            source.anchoredPosition = new Vector2(20, 60);
            flame = Image("Flame");
            flame.transform.SetParent(source, false);
            flame.color = new Color(.8f, .6f, .4f, .75f);
            presentation = background.gameObject.AddComponent<SceneLightingPresentation>();
            presentation.Configure(asset, background, new[]
            {
                new SceneLightingSourceBinding { LightIndex = 0 },
                new SceneLightingSourceBinding { LightIndex = 1, Source = source, ReferencePosition = new Vector2(.2f, .3f),
                    ReceiverOffsetX = new Vector3(10, 0, 0), ReceiverOffsetY = new Vector3(0, 5, 0),
                    EmissiveImages = new[] { flame } }
            });
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; --i)
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        [Test]
        public void MovingSourceUpdatesContributionResponseAndReceiverWithoutChangingTheAsset()
        {
            presentation.ApplyAtTime(2.75);
            Vector2 originalOffset = asset.Lights[1].UvOffset;
            Vector3 originalReceiver = asset.Lights[1].ReceiverPosition;
            float originalIntensity = asset.Lights[1].Intensity;
            source.anchoredPosition += new Vector2(25, 20);
            presentation.SetLightIntensity(1, .4f);
            presentation.ApplyAtTime(2.75);
            Assert.That(Vector2.Distance(presentation.State.GetUvOffset(1), originalOffset + new Vector2(.25f, .1f)), Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(presentation.State.GetReceiverPosition(1), originalReceiver + new Vector3(2.5f, .5f, 0)), Is.LessThan(.00001f));
            Vector4 offset = background.material.GetVector("_SceneLightUvOffset1");
            Assert.That(offset.x, Is.EqualTo(presentation.State.GetUvOffset(1).x));
            Assert.That(offset.y, Is.EqualTo(presentation.State.GetUvOffset(1).y));
            Assert.That(background.material.GetFloat("_LightIntensity1"), Is.EqualTo(presentation.State.GetIntensity(1)));
            Assert.That(asset.Lights[1].UvOffset, Is.EqualTo(originalOffset));
            Assert.That(asset.Lights[1].ReceiverPosition, Is.EqualTo(originalReceiver));
            Assert.That(asset.Lights[1].Intensity, Is.EqualTo(originalIntensity));
            var separate = new SceneLightingState();
            separate.Sample(asset, 2.75);
            Assert.That(separate.GetUvOffset(1), Is.EqualTo(originalOffset));
            Assert.That(separate.GetReceiverPosition(1), Is.EqualTo(originalReceiver));
            Assert.That(presentation.State.GetIntensity(1), Is.EqualTo(separate.GetIntensity(1) * .4f).Within(.000001f));
        }

        [Test]
        public void InactiveSourceAndExplicitZeroExtinguishOnlyTheirOwnLightAndEmission()
        {
            presentation.ApplyAtTime(4);
            float fixedIntensity = presentation.State.GetIntensity(0);
            Assert.That(flame.color.a, Is.EqualTo(.75f * Mathf.Clamp01(Mathf.Sqrt(
                presentation.State.GetIntensity(1) / asset.Lights[1].Intensity))).Within(.00001f));
            source.gameObject.SetActive(false);
            presentation.ApplyAtTime(4);
            Assert.That(presentation.State.GetIntensity(1), Is.Zero);
            Assert.That(background.material.GetFloat("_LightIntensity1"), Is.Zero);
            Assert.That(flame.color.a, Is.Zero);
            Assert.That(presentation.State.GetIntensity(0), Is.EqualTo(fixedIntensity));
            source.gameObject.SetActive(true);
            presentation.SetLightIntensity(1, 0);
            presentation.ApplyAtTime(4);
            Assert.That(presentation.State.GetIntensity(1), Is.Zero);
            Assert.That(flame.color.a, Is.Zero);
            presentation.SetLightIntensity(1, 1);
            presentation.ApplyAtTime(4);
            Assert.That(presentation.State.GetIntensity(1), Is.GreaterThan(0));
            Assert.That(flame.color.a, Is.GreaterThan(0));
        }

        [Test]
        public void DisableRestoresOriginalImageStateAndReenableRebuildsTheSameInstanceSettings()
        {
            presentation.SetLightIntensity(1, .3f);
            presentation.ApplyAtTime(0);
            presentation.enabled = false;
            Assert.That(flame.color, Is.EqualTo(new Color(.8f, .6f, .4f, .75f)));
            Assert.That(background.texture, Is.Null);
            Assert.That(background.material, Is.EqualTo(background.defaultMaterial));
            presentation.enabled = true;
            presentation.ApplyAtTime(0);
            Assert.That(presentation.GetLightIntensity(1), Is.EqualTo(.3f));
            Assert.That(background.texture, Is.SameAs(asset.BaseTexture));
            Assert.That(background.material.GetFloat("_LightIntensity1"), Is.EqualTo(SceneLightFlicker.EvaluateIntensity(0,
                asset.Lights[1].Intensity, asset.Lights[1].FlickerAmount, asset.Lights[1].FlickerSpeed, asset.Lights[1].Seed) * .3f).Within(.00001f));
        }

        [Test]
        public void EmissionTracksRelativeFlickerAndOverridesForStrongPaintedLights()
        {
            asset.Lights[1].Intensity = 8f;
            asset.Lights[1].FlickerAmount = .4f;
            float minimumAlpha = 1f, maximumAlpha = 0f;
            for (int i = 0; i < 30; ++i)
            {
                presentation.ApplyAtTime(i * .419);
                float expected = .75f * Mathf.Clamp01(Mathf.Pow(presentation.State.GetIntensity(1) / 8f, .5f));
                Assert.That(flame.color.a, Is.EqualTo(expected).Within(.00001f));
                minimumAlpha = Mathf.Min(minimumAlpha, flame.color.a);
                maximumAlpha = Mathf.Max(maximumAlpha, flame.color.a);
            }
            Assert.That(maximumAlpha - minimumAlpha, Is.GreaterThan(.01f));
            presentation.SetLightIntensity(1, .25f);
            presentation.ApplyAtTime(2.75);
            Assert.That(flame.color.a, Is.EqualTo(.75f * Mathf.Sqrt(presentation.State.GetIntensity(1) / 8f)).Within(.00001f));
            Assert.That(asset.Lights[1].Intensity, Is.EqualTo(8f));
            asset.Lights[1].Intensity = 0f;
            presentation.ApplyAtTime(2.75);
            Assert.That(flame.color.a, Is.Zero);
        }

        [Test]
        public void NativeHostReceivesTheSharedSampleAndClearsLightingWithoutResettingItsPoseState()
        {
            var hostAsset = Own(ScriptableObject.CreateInstance<SpiritHostPresentationAsset>());
            Color physical = new Color(.8f, .6f, .4f, .9f);
            Color emissive = new Color(.1f, .5f, .9f, .3f);
            hostAsset.Layers = new[]
            {
                new SpiritPortraitLayer { Id = "body", Texture = Texture2D.whiteTexture, Tint = physical, Size = new Vector2(32, 32) },
                new SpiritPortraitLayer { Id = "glow", Texture = Texture2D.whiteTexture, Tint = emissive,
                    Size = new Vector2(32, 32), ReceiveSceneLighting = false }
            };
            SpiritHostPortrait portrait = SpiritPortraitTestAuthoring.Create(hostAsset, background.rectTransform);
            Own(portrait.gameObject);
            string authored = EditorJsonUtility.ToJson(hostAsset);
            presentation.Configure(asset, background, Array.Empty<SceneLightingSourceBinding>(), hostReceivers: new[]
            {
                new SceneLightingHostReceiver { Portrait = portrait, Presentation = hostAsset, Position = new Vector3(0, 1, 0) }
            });
            portrait.SetState(SpiritPresentationState.Waiting, 0);
            presentation.ApplyAtTime(2.75);
            Color expected = (physical.linear * presentation.State.EvaluateReceiver(new Vector3(0, 1, 0), Vector3.back)).gamma;
            Color actual = portrait.GetLayer("body").color;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.00001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.00001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.00001f));
            Assert.That(portrait.GetLayer("glow").color, Is.EqualTo(emissive));
            Assert.That(EditorJsonUtility.ToJson(hostAsset), Is.EqualTo(authored));
            presentation.enabled = false;
            Assert.That(portrait.State, Is.EqualTo(SpiritPresentationState.Waiting));
            Assert.That(portrait.GetLayer("body").color, Is.EqualTo(physical));
            Assert.That(portrait.GetLayer("glow").color, Is.EqualTo(emissive));
            Assert.That(EditorJsonUtility.ToJson(hostAsset), Is.EqualTo(authored));
        }

        [Test]
        public void MovableImageReceiverEvaluatesLightingAtItsCurrentSourcePosition()
        {
            RawImage physical = Image("Physical Receiver");
            physical.transform.SetParent(source, false);
            Color original = new Color(.8f, .6f, .4f, .9f);
            physical.color = original;
            var receiver = new SceneLightingImageReceiver
            {
                Image = physical, Position = new Vector3(0, 1, 0), Source = source,
                ReferencePosition = new Vector2(.2f, .3f), ReceiverOffsetX = new Vector3(10, 0, 0),
                ReceiverOffsetY = new Vector3(0, 5, 0)
            };
            presentation.Configure(asset, background, Array.Empty<SceneLightingSourceBinding>(), new[] { receiver });
            presentation.ApplyAtTime(2.75);
            Color before = physical.color;
            source.anchoredPosition += new Vector2(30, 40);
            presentation.ApplyAtTime(2.75);
            Vector3 position = receiver.Position + new Vector3(3, 1, 0);
            Color expected = (original.linear * presentation.State.EvaluateReceiver(position, Vector3.back)).gamma;
            Assert.That(physical.color.r, Is.EqualTo(expected.r).Within(.00001f));
            Assert.That(physical.color.g, Is.EqualTo(expected.g).Within(.00001f));
            Assert.That(physical.color.b, Is.EqualTo(expected.b).Within(.00001f));
            Assert.That(Vector3.Distance(new Vector3(before.r, before.g, before.b),
                new Vector3(physical.color.r, physical.color.g, physical.color.b)), Is.GreaterThan(.001f));
            Assert.That(receiver.Position, Is.EqualTo(new Vector3(0, 1, 0)));
            presentation.enabled = false;
            Assert.That(physical.color, Is.EqualTo(original));
        }

        /** Checks scene lighting without a shader fails validation, naming the asset / fails if it passes until first drawn. */
        [Test]
        public void SceneLightingWithoutAShaderFailsValidation()
        {
            Assert.DoesNotThrow(asset.Validate);
            asset.name = "Synthetic Scene Lighting";
            asset.Shader = null;
            Assert.That(Assert.Throws<InvalidOperationException>(asset.Validate).Message, Does.Contain(asset.name));
        }

        [Test]
        public void InstanceOverrideArraysAreCopiedAndMalformedInputDoesNotReplaceTheCurrentSample()
        {
            var state = new SceneLightingState();
            float[] multipliers = { 1, .2f };
            Vector2[] offsets = { Vector2.zero, new Vector2(.1f, .3f) };
            Vector3[] positions = { Vector3.zero, new Vector3(2, 4, -3) };
            state.Sample(asset, 0, multipliers, offsets, positions);
            float expected = state.GetIntensity(1);
            multipliers[1] = 0;
            offsets[1] = Vector2.zero;
            positions[1] = Vector3.zero;
            Assert.That(state.GetIntensity(1), Is.EqualTo(expected));
            Assert.That(state.GetUvOffset(1), Is.EqualTo(new Vector2(.1f, .3f)));
            Assert.That(state.GetReceiverPosition(1), Is.EqualTo(new Vector3(2, 4, -3)));
            Assert.Throws<ArgumentException>(() => state.Sample(asset, 1, new[] { 1f }));
            Assert.That(state.SampleTime, Is.Zero);
            Assert.That(state.GetIntensity(1), Is.EqualTo(expected));
        }

        [Test]
        public void TranslatedFootprintPreservesBaseDetailAndMovesTheResponseMapWithIt()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null
                || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat))
                Assert.Ignore("Lighting composition pixels require a graphics device with floating-point render textures.");
            Assert.That(ShaderUtil.ShaderHasError(asset.Shader), Is.False);
            asset.BaseTexture = Texture(new[] { Gray(.1f), Gray(.2f), Gray(.3f), Gray(.4f) });
            asset.Lights = new[] { new SceneLightLayer
            {
                Contribution = Texture(new[] { Color.white, Color.white, Color.black, Color.black }),
                ResponseMap = Texture(new[] { new Color(0, 0, 0), new Color(1, 0, 0), Color.black, Color.black }),
                ModulateByBaseColor = true, FlickerAmount = 0, Intensity = .5f, DistanceFalloff = 1
            } };
            var state = new SceneLightingState();
            using (var compositor = new SceneLightingMaterial(asset.Shader))
            {
                state.Sample(asset, 0);
                compositor.Apply(state);
                Color[] stationary = Render(compositor);
                state.Sample(asset, 0, uvOffsets: new[] { new Vector2(.5f, 0) });
                compositor.Apply(state);
                Color[] moved = Render(compositor);
                float[] expectedStationary = { .15f, .25f, .3f, .4f };
                float[] expectedMoved = { .1f, .2f, .45f, .5f };
                for (int i = 0; i < moved.Length; ++i)
                {
                    Assert.That(stationary[i].r, Is.EqualTo(expectedStationary[i]).Within(.003f));
                    Assert.That(moved[i].r, Is.EqualTo(expectedMoved[i]).Within(.003f));
                }
                compositor.Apply(asset, 0);
                Assert.That(compositor.Material.GetVector("_SceneLightUvOffset0"), Is.EqualTo(Vector4.zero));
                asset.Lights = Array.Empty<SceneLightLayer>();
                compositor.Apply(asset, 0);
                Assert.That(compositor.Material.GetFloat("_SceneLightModulateByBase0"), Is.Zero);
            }
        }

        private Color[] Render(SceneLightingMaterial compositor)
        {
            var target = new RenderTexture(4, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(4, 1, TextureFormat.RGBAFloat, false, true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(asset.BaseTexture, target, compositor.Material, SceneLightingMaterial.PreviewPass);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 4, 1), 0, 0);
                pixels.Apply();
                return pixels.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static Color Gray(float value) => new Color(value, value, value, 1);

        private Texture2D Texture(Color[] colors)
        {
            var texture = Own(new Texture2D(colors.Length, 1, TextureFormat.RGBAFloat, false, true)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp });
            texture.SetPixels(colors);
            texture.Apply();
            return texture;
        }

        private RawImage Image(string name) => Own(new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)))
            .GetComponent<RawImage>();

        private T Own<T>(T value) where T : UnityEngine.Object
        {
            objects.Add(value);
            return value;
        }
    }
}
