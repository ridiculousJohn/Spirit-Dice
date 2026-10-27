using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class SceneLitLayerTests
    {
        private const string LitShaderPath = "Assets/Dice/Presentation/Shared/SceneLitImage.shader";
        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
        private SceneLightingAsset asset;
        private Shader shader;

        [SetUp]
        public void SetUp()
        {
            shader = AssetDatabase.LoadAssetAtPath<Shader>(LitShaderPath);
            Assert.That(shader, Is.Not.Null);
            asset = Own(ScriptableObject.CreateInstance<SceneLightingAsset>());
            asset.Shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Dice/Presentation/Shared/SceneLighting.shader");
            asset.BaseTexture = Texture(new Color(.1f, .1f, .1f, 1f));
            asset.Lights = new[]
            {
                new SceneLightLayer { Contribution = Texture(Color.red), ResponseMap = Texture(Color.black), ContributionIncludesBase = true,
                    ConstrainAmbientToReference = true, Intensity = .7f, FlickerAmount = .1f, Seed = 42, Tint = new Color(1f, .5f, .25f, 1f),
                    DistanceFalloff = 1.25f, SourceResponse = .4f, ReceiverPosition = new Vector3(-2, 3, -2), ReceiverIntensity = 3f },
                new SceneLightLayer { Contribution = Texture(Color.green), Intensity = 5f, FlickerAmount = .2f, Seed = 182,
                    ModulateByBaseColor = true, ReceiverPosition = new Vector3(1, 2, -1), ReceiverColor = new Color(1f, .6f, .3f, 1f),
                    ReceiverIntensity = .2f, ReceiverRange = 6f }
            };
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; --i)
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        /** Checks the lit image shader has a single pass / fails if a second pass would make UGUI draw every lit image twice. */
        [Test]
        public void LitLayerShaderHasOnlyTheUiPass()
        {
            Assert.That(shader.passCount, Is.EqualTo(1));
        }

        /** Checks a lit material carries the layer description and the source material's mask / fails if a slot mode, map, strength, depth, frame axis or mask is lost. */
        [Test]
        public void MaterialCarriesTheLayerDescriptionAndSourceMask()
        {
            Texture2D unlit = Texture(Color.gray), reference = Texture(Color.yellow), response = Texture(Color.blue), depth = Texture(Color.white);
            var layer = Layer(unlit, reference, response, depth);
            var source = Own(new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Dice/Presentation/Shared/SourceMaskedImage.shader")));
            Texture2D mask = Texture(Color.white);
            source.SetTexture("_SourceTex", Texture(Color.magenta));
            source.SetTexture("_LayerMask", mask);
            source.SetFloat("_InvertMask", 1f);
            var lit = new SceneLitLayerMaterial(layer, shader, source);
            try
            {
                Material material = lit.Material;
                Assert.That(material.shader, Is.SameAs(shader));
                Assert.That(material.hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
                Assert.That(material.GetTexture("_SourceTex"), Is.SameAs(unlit));
                Assert.That(material.GetTexture("_LayerMask"), Is.SameAs(mask));
                Assert.That(material.GetFloat("_InvertMask"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_SceneLitMode0"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_SceneLitMode1"), Is.EqualTo(2f));
                Assert.That(material.GetFloat("_SceneLitMode2"), Is.Zero);
                Assert.That(material.GetFloat("_SceneLitMode3"), Is.Zero);
                Assert.That(material.GetTexture("_SceneLitReferenceTex0"), Is.SameAs(reference));
                Assert.That(material.GetTexture("_SceneLitResponseTex0"), Is.SameAs(response));
                Assert.That(material.GetFloat("_SceneLitResponseEnabled0"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_SceneLitResponseEnabled1"), Is.Zero);
                Assert.That(material.GetFloat("_SceneLitStrength1"), Is.EqualTo(layer.Lights[1].Strength));
                Assert.That(material.GetTexture("_SceneLitDepthTex"), Is.SameAs(depth));
                Assert.That((Vector2)material.GetVector("_SceneLitDepthRange"), Is.EqualTo(layer.DepthRange));
                Assert.That((Vector3)material.GetVector("_SceneLitFrameAxisX"), Is.EqualTo(layer.FrameAxisX));
                Assert.That((Vector3)material.GetVector("_SceneLitFrameAxisY"), Is.EqualTo(layer.FrameAxisY));
                Assert.That(material.GetFloat("_SceneLitWrap"), Is.EqualTo(layer.Wrap));
            }
            finally
            {
                lit.Dispose();
            }
            Assert.That(lit.Material, Is.Null);
        }

        /** Checks applying a sample writes its light levels, light settings, receiver positions and the image's frame / fails if a sampled value, an empty slot or the frame mapping is wrong or an unsampled state is accepted. */
        [Test]
        public void ApplyWritesTheSampledLightsAndImageFrame()
        {
            var lit = new SceneLitLayerMaterial(Layer(Texture(Color.gray), Texture(Color.yellow), Texture(Color.blue), Texture(Color.white)),
                shader);
            try
            {
                Material material = lit.Material;
                var state = new SceneLightingState();
                state.Sample(asset, 3.5, new[] { .5f, 2f }, null, new[] { new Vector3(4, 5, -6), new Vector3(7, 8, -9) });
                lit.Apply(state, new Vector3(0f, 1f, 2f), new Rect(.25f, .5f, .5f, .25f));
                Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.EqualTo(state.GetIntensity(0)));
                Assert.That(material.GetFloat("_SceneLitIntensity1"), Is.EqualTo(state.GetIntensity(1)));
                Assert.That(material.GetFloat("_SceneLitIntensity2"), Is.Zero);
                Color tint = asset.Lights[0].Tint.linear;
                Assert.That(material.GetVector("_SceneLitTint0"), Is.EqualTo(new Vector4(tint.r, tint.g, tint.b, tint.a)));
                Assert.That(material.GetFloat("_SceneLitDistanceFalloff0"), Is.EqualTo(asset.Lights[0].DistanceFalloff));
                Assert.That(material.GetFloat("_SceneLitSourceResponse0"), Is.EqualTo(asset.Lights[0].SourceResponse));
                Assert.That(material.GetFloat("_SceneLitIncludesBase0"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_SceneLitConstrainAmbient0"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_SceneLitIncludesBase1"), Is.Zero);
                Assert.That(material.GetFloat("_SceneLitModulateByBase1"), Is.EqualTo(1f));
                Assert.That(material.GetVector("_SceneLitPosition1"), Is.EqualTo(new Vector4(7, 8, -9, asset.Lights[1].ReceiverRange)));
                Color color = asset.Lights[1].ReceiverColor.linear;
                Assert.That(material.GetVector("_SceneLitColor1"),
                    Is.EqualTo(new Vector4(color.r, color.g, color.b, asset.Lights[1].ReceiverIntensity)));
                // The layer's frame origin (-1, -2, 0) plus the image position (0, 1, 2).
                Assert.That(material.GetVector("_SceneLitFrameOrigin"), Is.EqualTo(new Vector4(-1f, -1f, 2f, 0f)));
                // Scale and offset from texture coordinates to the image rectangle (.25, .5, .5, .25).
                Assert.That(material.GetVector("_SceneLitFrameTransform"), Is.EqualTo(new Vector4(2f, 4f, -.5f, -2f)));
                Assert.Throws<ArgumentException>(() => lit.Apply(new SceneLightingState(), Vector3.zero, new Rect(0f, 0f, 1f, 1f)));
            }
            finally
            {
                lit.Dispose();
            }
        }

        /** Checks applying authored lighting writes the asset's own levels and positions and no asset darkens every slot / fails if authored levels are missing or an absent asset leaves lights on. */
        [Test]
        public void ApplyAuthoredUsesTheAssetLevelsAndNoAssetDarkens()
        {
            var lit = new SceneLitLayerMaterial(Layer(Texture(Color.gray), Texture(Color.yellow), Texture(Color.blue), Texture(Color.white)),
                shader);
            try
            {
                Material material = lit.Material;
                lit.ApplyAuthored(asset, Vector3.zero, new Rect(0f, 0f, 1f, 1f));
                Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.EqualTo(asset.Lights[0].Intensity));
                Vector3 position = asset.Lights[0].ReceiverPosition;
                Assert.That(material.GetVector("_SceneLitPosition0"),
                    Is.EqualTo(new Vector4(position.x, position.y, position.z, asset.Lights[0].ReceiverRange)));
                Assert.That(material.GetVector("_SceneLitFrameTransform"), Is.EqualTo(new Vector4(1f, 1f, 0f, 0f)));
                lit.ApplyAuthored(null, Vector3.zero, new Rect(0f, 0f, 1f, 1f));
                Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.Zero);
                Assert.That(material.GetFloat("_SceneLitIntensity1"), Is.Zero);
            }
            finally
            {
                lit.Dispose();
            }
        }

        /** Checks unassigned layer textures keep the lit source material's textures while the description decides the lit slots / fails if a source texture is dropped or a source slot stays lit. */
        [Test]
        public void UnassignedLayerTexturesKeepTheLitSourceMaterialTextures()
        {
            Texture2D baseTexture = Texture(Color.gray), reference = Texture(Color.yellow), response = Texture(Color.blue);
            var source = Own(new Material(shader));
            source.SetTexture("_SourceTex", baseTexture);
            source.SetTexture("_SceneLitReferenceTex2", reference);
            source.SetTexture("_SceneLitResponseTex2", response);
            source.SetFloat("_SceneLitMode0", 1f);
            var lit = new SceneLitLayerMaterial(new SceneLitLayer { Lights = new[] { new SceneLitLayerLight { LightIndex = 2 } } }, null, source);
            try
            {
                Assert.That(lit.Material, Is.Not.SameAs(source));
                Assert.That(lit.Material.GetTexture("_SourceTex"), Is.SameAs(baseTexture));
                Assert.That(lit.Material.GetTexture("_SceneLitReferenceTex2"), Is.SameAs(reference));
                Assert.That(lit.Material.GetFloat("_SceneLitResponseEnabled2"), Is.EqualTo(1f));
                Assert.That(lit.Material.GetFloat("_SceneLitMode2"), Is.EqualTo(1f));
                Assert.That(lit.Material.GetFloat("_SceneLitMode0"), Is.Zero, "The description decides which slots are lit.");
            }
            finally
            {
                lit.Dispose();
            }
        }

        /** Checks a lit material without a shader or lit source material is refused / fails if one is created without lit-layer properties. */
        [Test]
        public void LitMaterialRequiresAShaderOrLitSource()
        {
            Assert.Throws<InvalidOperationException>(() => new SceneLitLayerMaterial(new SceneLitLayer
                { Lights = new[] { new SceneLitLayerLight() } }, null, null));
        }

        /** Checks malformed layer descriptions are rejected while the default one validates unconfigured / fails if any malformed description passes. */
        [Test]
        public void InvalidLayerDescriptionsAreRejected()
        {
            Assert.DoesNotThrow(new SceneLitLayer().Validate);
            Assert.That(new SceneLitLayer().Configured, Is.False);
            foreach (SceneLitLayer invalid in new[]
            {
                new SceneLitLayer { Lights = null },
                new SceneLitLayer { Lights = new SceneLitLayerLight[] { null } },
                new SceneLitLayer { Lights = new[] { new SceneLitLayerLight { LightIndex = SceneLightingAsset.MaximumLights } } },
                new SceneLitLayer { Lights = new[] { new SceneLitLayerLight { LightIndex = 1 }, new SceneLitLayerLight { LightIndex = 1 } } },
                new SceneLitLayer { Lights = new[] { new SceneLitLayerLight { Strength = -1f } } },
                new SceneLitLayer { Lights = new[] { new SceneLitLayerLight { Mode = (SceneLitLightMode)7 } } },
                new SceneLitLayer { DepthRange = new Vector2(float.NaN, 0f) },
                new SceneLitLayer { FrameAxisX = new Vector3(float.PositiveInfinity, 0f, 0f) },
                new SceneLitLayer { Wrap = 2f }
            })
                Assert.Throws<InvalidOperationException>(invalid.Validate);
        }

        /** Checks a host layer lit per pixel validates only while it receives scene lighting / fails if a per-pixel layer that opts out of scene lighting passes. */
        [Test]
        public void HostPerPixelLayerMustReceiveSceneLighting()
        {
            var host = Own(ScriptableObject.CreateInstance<SpiritHostPresentationAsset>());
            host.LitLayerShader = shader;
            var layer = new SpiritPortraitLayer { Id = "body", Texture = Texture2D.whiteTexture, Size = new Vector2(32, 32),
                Lighting = new SceneLitLayer { Lights = new[] { new SceneLitLayerLight() } } };
            host.Layers = new[] { layer };
            Assert.DoesNotThrow(host.Validate);
            layer.ReceiveSceneLighting = false;
            Assert.Throws<InvalidOperationException>(host.Validate);
        }

        /** Checks host layers lit per pixel take the shared sample through a lit material while other layers keep the uniform tint, and clearing restores both / fails if a lit layer is also tinted, ignores the sample or keeps its runtime material. */
        [Test]
        public void HostLitLayersUseTheSharedSampleWhileUnconfiguredLayersKeepUniformTint()
        {
            var hostAsset = Own(ScriptableObject.CreateInstance<SpiritHostPresentationAsset>());
            hostAsset.LitLayerShader = shader;
            Color physical = new Color(.8f, .6f, .4f, .9f);
            Color litTint = new Color(.9f, .95f, 1f, 1f);
            hostAsset.Layers = new[]
            {
                new SpiritPortraitLayer { Id = "body", Texture = Texture2D.whiteTexture, Tint = physical, Size = new Vector2(32, 32) },
                new SpiritPortraitLayer { Id = "lit", Texture = Texture2D.whiteTexture, Tint = litTint, Size = new Vector2(32, 32),
                    Lighting = new SceneLitLayer { BaseTexture = Texture(Color.gray), FrameOrigin = new Vector3(0f, 0f, 1f),
                        Lights = new[] { new SceneLitLayerLight { LightIndex = 0 }, new SceneLitLayerLight { LightIndex = 1, Mode = SceneLitLightMode.Computed } } } }
            };
            RectTransform parent = Own(new GameObject("Host Parent", typeof(RectTransform))).GetComponent<RectTransform>();
            SpiritHostPortrait portrait = SpiritPortraitTestAuthoring.Create(hostAsset, parent);
            Own(portrait.gameObject);
            Material authored = portrait.GetComponentsInChildren<SpiritPortraitImage>(true).Single(image => image.name == "lit").material;
            portrait.Build((RectTransform)portrait.transform, hostAsset);
            SpiritPortraitImage body = portrait.GetLayer("body"), lit = portrait.GetLayer("lit");
            Assert.That(lit.material, Is.SameAs(authored));

            var state = new SceneLightingState();
            state.Sample(asset, 1.25, new[] { .4f, 1.5f });
            portrait.SetSceneLighting(state, new Vector3(0, 1, 0));
            Color expected = (physical.linear * state.EvaluateReceiver(new Vector3(0, 1, 0), Vector3.back)).gamma;
            Assert.That(body.color.r, Is.EqualTo(expected.r).Within(.00001f));
            Assert.That(body.color.g, Is.EqualTo(expected.g).Within(.00001f));
            Assert.That(body.color.b, Is.EqualTo(expected.b).Within(.00001f));
            Assert.That(body.material.shader, Is.Not.SameAs(shader));
            Assert.That(lit.color, Is.EqualTo(litTint), "Per-pixel layers must not also receive the uniform tint.");
            Assert.That(lit.material, Is.Not.SameAs(authored));
            Assert.That(lit.material.shader, Is.SameAs(shader));
            Assert.That(lit.material.GetFloat("_SceneLitIntensity0"), Is.EqualTo(state.GetIntensity(0)));
            Assert.That(lit.material.GetFloat("_SceneLitIntensity1"), Is.EqualTo(state.GetIntensity(1)));
            Assert.That(lit.material.GetVector("_SceneLitFrameOrigin"), Is.EqualTo(new Vector4(0f, 1f, 1f, 0f)));

            state.Sample(asset, 1.25, new[] { 0f, 1.5f });
            portrait.SetSceneLighting(state, new Vector3(0, 1, 0));
            Assert.That(lit.material.GetFloat("_SceneLitIntensity0"), Is.Zero);

            portrait.SetSceneLighting(null, Vector3.zero);
            Assert.That(lit.material, Is.SameAs(authored));
            Assert.That(body.color, Is.EqualTo(physical));
            Assert.That(lit.color, Is.EqualTo(litTint));
        }

        /** Checks a presentation's bound lit images follow its sample and per-light multipliers, ignore other views while bound and return to authored levels when released / fails if multipliers miss lit images, another view captures them or release leaves the sample or replaces the serialized material. */
        [Test]
        public void PresentationIntensityMultipliersDriveBoundLitImages()
        {
            RawImage background = Image("Background");
            RawImage canopy = Image("Canopy");
            canopy.uvRect = new Rect(0f, 0f, .5f, 1f);
            canopy.material = Own(new Material(shader));
            var litImage = canopy.gameObject.AddComponent<SceneLitImage>();
            litImage.Configure(asset, new SceneLitLayer { Lights = new[] { new SceneLitLayerLight { LightIndex = 0 } } });
            Material material = canopy.materialForRendering;
            Assert.That(material, Is.SameAs(litImage.LitMaterial));
            Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.EqualTo(asset.Lights[0].Intensity), "Without a sample the image shows authored levels.");
            Assert.That(material.GetVector("_SceneLitFrameTransform"), Is.EqualTo(new Vector4(2f, 1f, 0f, 0f)));

            var presentation = background.gameObject.AddComponent<SceneLightingPresentation>();
            presentation.Configure(asset, background, Array.Empty<SceneLightingSourceBinding>(), litLayers: new[] { litImage });
            presentation.ApplyAtTime(2.75);
            Assert.That(litImage.BoundState, Is.SameAs(presentation.State));
            Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.EqualTo(presentation.State.GetIntensity(0)));
            presentation.SetLightIntensity(0, 0f);
            Assert.That(presentation.GetLightIntensity(0), Is.Zero);
            Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.Zero);
            presentation.SetLightIntensity(0, .5f);
            Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.EqualTo(presentation.State.GetIntensity(0)).And.GreaterThan(0f));

            // An explicit binding ignores other views until the presentation releases it.
            var other = new SceneLightingState();
            other.Sample(asset, 9, new[] { 2f, 2f });
            Assert.That(litImage.BoundState, Is.SameAs(presentation.State));
            presentation.enabled = false;
            Assert.That(litImage.BoundState, Is.Null);
            Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.EqualTo(asset.Lights[0].Intensity));
            Assert.That(canopy.material, Is.Not.SameAs(material), "The serialized material is never replaced.");
        }

        /** Checks an image receiver lit per pixel keeps its authored colour while a flat receiver is tinted / fails if the lit receiver is tinted too or the flat one is not. */
        [Test]
        public void ImageReceiversLitPerPixelKeepTheirAuthoredColor()
        {
            RawImage background = Image("Background");
            RawImage flat = Image("Flat Receiver");
            RawImage canopy = Image("Lit Receiver");
            Color original = new Color(.8f, .6f, .4f, .9f);
            flat.color = canopy.color = original;
            canopy.material = Own(new Material(shader));
            canopy.gameObject.AddComponent<SceneLitImage>().Configure(asset,
                new SceneLitLayer { Lights = new[] { new SceneLitLayerLight { LightIndex = 0 } } });
            var presentation = background.gameObject.AddComponent<SceneLightingPresentation>();
            presentation.Configure(asset, background, Array.Empty<SceneLightingSourceBinding>(), new[]
            {
                new SceneLightingImageReceiver { Image = flat, Position = new Vector3(0, 1, 0), Normal = Vector3.back },
                new SceneLightingImageReceiver { Image = canopy, Position = new Vector3(0, 1, 0), Normal = Vector3.back }
            });
            presentation.ApplyAtTime(1.5);
            Assert.That(flat.color, Is.Not.EqualTo(original), "Receivers without lit layers keep the uniform tint.");
            Assert.That(canopy.color, Is.EqualTo(original));
        }

        /** Checks an unbound lit image follows any view sampling its own lighting asset, ignores copies of it and keeps an explicit binding / fails if it ignores a sampling view, follows an unrelated asset or keeps its runtime material when disabled. */
        [Test]
        public void UnboundLitImagesFollowAnyViewSamplingTheirAuthoredLighting()
        {
            RawImage canopy = Image("Canopy");
            canopy.material = Own(new Material(shader));
            var litImage = canopy.gameObject.AddComponent<SceneLitImage>();
            litImage.Configure(asset, new SceneLitLayer { Lights = new[] { new SceneLitLayerLight { LightIndex = 0 } } });
            Material material = canopy.materialForRendering;
            var view = new SceneLightingState();
            view.Sample(asset, 4.5, new[] { .25f, 1f });
            Assert.That(litImage.BoundState, Is.SameAs(view));
            Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.EqualTo(view.GetIntensity(0)));
            var unrelated = Own(UnityEngine.Object.Instantiate(asset));
            var preview = new SceneLightingState();
            preview.Sample(unrelated, 4.5, new[] { 2f, 2f });
            Assert.That(litImage.BoundState, Is.SameAs(view), "A copied asset does not capture the image.");
            view.Sample(null, 5);
            Assert.That(litImage.BoundState, Is.Null);
            Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.EqualTo(asset.Lights[0].Intensity));
            litImage.Bind(preview);
            Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.EqualTo(preview.GetIntensity(0)));
            litImage.enabled = false;
            Assert.That(canopy.materialForRendering, Is.SameAs(canopy.material));
        }

        /** Checks a layer's map rectangle reaches its material, defaulting to the whole map, and empty or non-finite rectangles are rejected / fails if the rectangle is lost or a degenerate one passes. */
        [Test]
        public void MapRectangleReachesTheMaterialAndIsValidated()
        {
            var layer = new SceneLitLayer
            {
                Lights = new[] { new SceneLitLayerLight { LightIndex = 0 } },
                MapRect = new Rect(.25f, .5f, .125f, .25f)
            };
            var lit = new SceneLitLayerMaterial(layer, shader);
            try
            {
                Assert.That(lit.Material.GetVector("_SceneLitMapRect"),
                    Is.EqualTo(new Vector4(layer.MapRect.x, layer.MapRect.y, layer.MapRect.width, layer.MapRect.height)));
                var defaults = new SceneLitLayerMaterial(new SceneLitLayer { Lights = new[] { new SceneLitLayerLight() } }, shader);
                try { Assert.That(defaults.Material.GetVector("_SceneLitMapRect"), Is.EqualTo(new Vector4(0f, 0f, 1f, 1f))); }
                finally { defaults.Dispose(); }
            }
            finally
            {
                lit.Dispose();
            }
            foreach (Rect invalid in new[] { new Rect(0f, 0f, 0f, 1f), new Rect(0f, 0f, 1f, 0f), new Rect(float.NaN, 0f, 1f, 1f) })
                Assert.Throws<InvalidOperationException>(new SceneLitLayer { MapRect = invalid }.Validate);
        }

        /** A layer lit by light 0 from a reference with a response map and by light 1 as a computed light, with depth and a frame. */
        private static SceneLitLayer Layer(Texture2D unlit, Texture2D reference, Texture2D response, Texture2D depth) => new SceneLitLayer
        {
            BaseTexture = unlit,
            Lights = new[]
            {
                new SceneLitLayerLight { LightIndex = 0, Mode = SceneLitLightMode.Reference, Reference = reference, ResponseMap = response },
                new SceneLitLayerLight { LightIndex = 1, Mode = SceneLitLightMode.Computed, Strength = .6f }
            },
            Depth = depth,
            DepthRange = new Vector2(.5f, -1.5f),
            FrameOrigin = new Vector3(-1f, -2f, 0f),
            FrameAxisX = new Vector3(2f, 0f, 0f),
            FrameAxisY = new Vector3(0f, 4f, .5f),
            Wrap = .3f
        };

        private RawImage Image(string name)
        {
            var image = Own(new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage))).GetComponent<RawImage>();
            image.texture = Texture(Color.white);
            return image;
        }

        private Texture2D Texture(Color color)
        {
            var texture = Own(new Texture2D(1, 1, TextureFormat.RGBA32, false));
            texture.SetPixels(new[] { color });
            texture.Apply();
            return texture;
        }

        private T Own<T>(T value) where T : UnityEngine.Object
        {
            objects.Add(value);
            return value;
        }
    }
}
