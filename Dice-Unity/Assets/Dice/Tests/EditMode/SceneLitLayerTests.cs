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
        private const string AmbientPath = "Assets/Dice/Content/Presentation/Ambient/";
        private const string MerchantTablePath = "Assets/Dice/Content/Presentation/HauntedMerchant/MerchantUnquietCartTable.asset";
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

        [Test]
        public void LitLayerShaderHasOnlyTheUiPass()
        {
            // UGUI draws every pass of a material's shader, so a second pass would also draw over each lit image.
            Assert.That(shader.passCount, Is.EqualTo(1));
            var lit = new SceneLitLayerMaterial(new SceneLitLayer { BaseTexture = Texture(Color.gray) }, shader);
            try
            {
                Assert.That(lit.Material.passCount, Is.EqualTo(1));
                Assert.That(lit.Material.GetPassName(0), Is.EqualTo("Scene Lit Image UI"));
            }
            finally
            {
                lit.Dispose();
            }
        }

        [Test]
        public void MaterialCarriesLayerConfigurationAndSampledLightParameters()
        {
            Texture2D unlit = Texture(Color.gray), reference = Texture(Color.yellow), response = Texture(Color.blue), depth = Texture(Color.white);
            var layer = new SceneLitLayer
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
                Assert.That(material.GetFloat("_SceneLitStrength1"), Is.EqualTo(.6f));
                Assert.That(material.GetTexture("_SceneLitDepthTex"), Is.SameAs(depth));
                Assert.That((Vector2)material.GetVector("_SceneLitDepthRange"), Is.EqualTo(new Vector2(.5f, -1.5f)));
                Assert.That((Vector3)material.GetVector("_SceneLitFrameAxisX"), Is.EqualTo(new Vector3(2f, 0f, 0f)));
                Assert.That((Vector3)material.GetVector("_SceneLitFrameAxisY"), Is.EqualTo(new Vector3(0f, 4f, .5f)));
                Assert.That(material.GetFloat("_SceneLitWrap"), Is.EqualTo(.3f));

                var state = new SceneLightingState();
                state.Sample(asset, 3.5, new[] { .5f, 2f }, null, new[] { new Vector3(4, 5, -6), new Vector3(7, 8, -9) });
                lit.Apply(state, new Vector3(0f, 1f, 2f), new Rect(.25f, .5f, .5f, .25f));
                Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.EqualTo(state.GetIntensity(0)));
                Assert.That(material.GetFloat("_SceneLitIntensity1"), Is.EqualTo(state.GetIntensity(1)));
                Assert.That(material.GetFloat("_SceneLitIntensity2"), Is.Zero);
                Color tint = asset.Lights[0].Tint.linear;
                Assert.That(material.GetVector("_SceneLitTint0"), Is.EqualTo(new Vector4(tint.r, tint.g, tint.b, tint.a)));
                Assert.That(material.GetFloat("_SceneLitDistanceFalloff0"), Is.EqualTo(1.25f));
                Assert.That(material.GetFloat("_SceneLitSourceResponse0"), Is.EqualTo(.4f));
                Assert.That(material.GetFloat("_SceneLitIncludesBase0"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_SceneLitConstrainAmbient0"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_SceneLitIncludesBase1"), Is.Zero);
                Assert.That(material.GetFloat("_SceneLitModulateByBase1"), Is.EqualTo(1f));
                Assert.That(material.GetVector("_SceneLitPosition1"), Is.EqualTo(new Vector4(7, 8, -9, 6)));
                Color color = asset.Lights[1].ReceiverColor.linear;
                Assert.That(material.GetVector("_SceneLitColor1"), Is.EqualTo(new Vector4(color.r, color.g, color.b, .2f)));
                Assert.That(material.GetVector("_SceneLitFrameOrigin"), Is.EqualTo(new Vector4(-1f, -1f, 2f, 0f)));
                Assert.That(material.GetVector("_SceneLitFrameTransform"), Is.EqualTo(new Vector4(2f, 4f, -.5f, -2f)));

                lit.ApplyAuthored(asset, Vector3.zero, new Rect(0f, 0f, 1f, 1f));
                Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.EqualTo(.7f));
                Assert.That(material.GetVector("_SceneLitPosition0"), Is.EqualTo(new Vector4(-2, 3, -2, 10)));
                Assert.That(material.GetVector("_SceneLitFrameTransform"), Is.EqualTo(new Vector4(1f, 1f, 0f, 0f)));
                lit.ApplyAuthored(null, Vector3.zero, new Rect(0f, 0f, 1f, 1f));
                Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.Zero);
                Assert.That(material.GetFloat("_SceneLitIntensity1"), Is.Zero);
                Assert.Throws<ArgumentException>(() => lit.Apply(new SceneLightingState(), Vector3.zero, new Rect(0f, 0f, 1f, 1f)));
            }
            finally
            {
                lit.Dispose();
            }
            Assert.That(lit.Material, Is.Null);
        }

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
            Assert.Throws<InvalidOperationException>(() => new SceneLitLayerMaterial(new SceneLitLayer
                { Lights = new[] { new SceneLitLayerLight() } }, null, null));
        }

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
            var host = Own(ScriptableObject.CreateInstance<SpiritHostPresentationAsset>());
            host.Layers = new[] { new SpiritPortraitLayer { Id = "body", Size = Vector2.one, Silhouette = true,
                Lighting = new SceneLitLayer { Lights = new[] { new SceneLitLayerLight() } } } };
            Assert.Throws<InvalidOperationException>(host.Validate);
        }

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
            Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.EqualTo(.7f), "Without a sample the image shows authored levels.");
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
            Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.EqualTo(.7f));
            Assert.That(canopy.material, Is.Not.SameAs(material), "The serialized material is never replaced.");
        }

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
            Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.EqualTo(.7f));
            litImage.Bind(preview);
            Assert.That(material.GetFloat("_SceneLitIntensity0"), Is.EqualTo(preview.GetIntensity(0)));
            litImage.enabled = false;
            Assert.That(canopy.materialForRendering, Is.SameAs(canopy.material));
        }

        [Test]
        public void CanopyIsLitFromItsRelitClothThroughItsUnchangedMask()
        {
            string art = CanopyMotionTests.ArtPath;
            var unlit = AssetDatabase.LoadAssetAtPath<Texture2D>(art + "Relight/CanopyUnlit.png");
            var lantern = AssetDatabase.LoadAssetAtPath<Texture2D>(art + "Relight/CanopyLanternLit.png");
            var response = AssetDatabase.LoadAssetAtPath<Texture2D>(art + "MerchantLanternResponse.png");
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(art + "CanopyOriginalOverhangMask.png");
            var lighting = AssetDatabase.LoadAssetAtPath<SceneLightingAsset>(AmbientPath + "MerchantSceneLighting.asset");
            Assert.That(lighting.Lights[0].ResponseMap, Is.SameAs(response));
            Assert.That(lighting.Lights[0].ContributionIncludesBase, Is.True, "The cloth reference includes its unlit base.");
            foreach (Texture2D texture in new[] { unlit, lantern, response })
                Assert.That(new Vector2Int(texture.width, texture.height), Is.EqualTo(new Vector2Int(mask.width, mask.height)),
                    "The cloth sources and response map must share the canopy mask's frame.");
            foreach (string name in new[] { "CanopySource.mat", "CanopyRoofSource.mat" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AmbientPath + name);
                Assert.That(material.shader, Is.SameAs(shader), name);
                Assert.That(material.GetTexture("_SourceTex"), Is.SameAs(unlit), name);
                Assert.That(material.GetTexture("_LayerMask"), Is.SameAs(mask), name);
                Assert.That(material.GetTexture("_SceneLitReferenceTex0"), Is.SameAs(lantern), name);
                Assert.That(material.GetTexture("_SceneLitResponseTex0"), Is.SameAs(response), name);
                Assert.That(material.GetFloat("_SceneLitMode0"), Is.EqualTo(1f), name);
                Assert.That(material.GetFloat("_SceneLitIncludesBase0"), Is.EqualTo(1f), name);
                Assert.That(material.GetTexture("_SceneLitDepthTex"), Is.Null, name);
                Assert.That(material.GetVector("_SceneLitMapRect"), Is.EqualTo(new Vector4(0f, 0f, 1f, 1f)), name);
                for (int i = 1; i < SceneLightingAsset.MaximumLights; ++i) Assert.That(material.GetFloat("_SceneLitMode" + i), Is.Zero, name);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AmbientPath + "Canopy.prefab");
            SceneLitImage litImage = prefab.GetComponentsInChildren<SceneLitImage>(true).Single();
            Assert.That(litImage.name, Is.EqualTo("CanopyArtwork"));
            var image = litImage.GetComponent<SpiritPortraitImage>();
            Assert.That(image.material, Is.SameAs(AssetDatabase.LoadAssetAtPath<Material>(AmbientPath + "CanopySource.mat")));
            Assert.That(image.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Assert.That(litImage.Lighting, Is.SameAs(lighting));
            Assert.That(litImage.Layer.BaseTexture, Is.SameAs(unlit));
            Assert.That(litImage.Layer.MapRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            SceneLitLayerLight light = litImage.Layer.Lights.Single();
            Assert.That(light.LightIndex, Is.Zero);
            Assert.That(light.Mode, Is.EqualTo(SceneLitLightMode.Reference));
            Assert.That(light.Reference, Is.SameAs(lantern));
            Assert.That(light.ResponseMap, Is.SameAs(response));
            Assert.DoesNotThrow(litImage.Layer.Validate);
        }

        [Test]
        public void MerchantBackgroundUsesThePropsRemovedPlatesEverywhere()
        {
            string art = CanopyMotionTests.ArtPath + "Props/";
            var unlit = AssetDatabase.LoadAssetAtPath<Texture2D>(art + "MerchantAmbientUnlitPropsRemoved.png");
            var lantern = AssetDatabase.LoadAssetAtPath<Texture2D>(art + "MerchantLanternOnlyPropsRemoved.png");
            Assert.That(unlit, Is.Not.Null);
            Assert.That(lantern, Is.Not.Null);
            var lighting = AssetDatabase.LoadAssetAtPath<SceneLightingAsset>(AmbientPath + "MerchantSceneLighting.asset");
            Assert.That(lighting.BaseTexture, Is.SameAs(unlit));
            Assert.That(lighting.Lights[0].Contribution, Is.SameAs(lantern));
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(MerchantTablePath);
            Assert.That(table.SceneLighting, Is.SameAs(lighting));
            Assert.That(table.RoomTexture, Is.SameAs(unlit));
            string original = AssetDatabase.AssetPathToGUID(CanopyMotionTests.ArtPath + "MerchantAmbientUnlit.png");
            string originalLantern = AssetDatabase.AssetPathToGUID(CanopyMotionTests.ArtPath + "MerchantLanternOnly.png");
            foreach (string path in new[] { "Assets/Scenes/MerchantEncounter.unity", "Assets/Scenes/AmbientMotionPreview.unity",
                MerchantTablePath, AmbientPath + "MerchantSceneLighting.asset" })
            {
                string text = System.IO.File.ReadAllText(path);
                Assert.That(text.Contains(original) || text.Contains(originalLantern), Is.False, path + " must not reference the plates with props.");
            }
        }

        [Test]
        public void MerchantHostLayersAreLitPerPixelInTheCandleRig()
        {
            const string relight = "Assets/Dice/Art/HauntedMerchant/Relight/";
            var host = AssetDatabase.LoadAssetAtPath<SpiritHostPresentationAsset>(AmbientPath + "MerchantHost.asset");
            Assert.That(host.LitLayerShader, Is.SameAs(shader));
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(MerchantTablePath);
            EncounterTablePieceLight candle = table.PieceLights.Single();
            SceneLightLayer candleLight = table.SceneLighting.Lights[candle.LightIndex];
            var response = AssetDatabase.LoadAssetAtPath<Texture2D>(relight + "MerchantLanternResponseHost.png");
            var depth = AssetDatabase.LoadAssetAtPath<Texture2D>(relight + "MerchantDepth.png");
            Assert.That(((TextureImporter)AssetImporter.GetAtPath(relight + "MerchantDepth.png")).sRGBTexture, Is.False);
            Assert.That(((TextureImporter)AssetImporter.GetAtPath(relight + "MerchantLanternResponseHost.png")).sRGBTexture, Is.False);
            var expected = new Dictionary<string, string>
            {
                ["Torso"] = "MerchantTorsoAnimation", ["Head"] = "MerchantHeadRepaired", ["Resting Arm"] = "MerchantHost",
                ["Supporting Arm"] = "MerchantHost", ["Blink"] = "MerchantBlinkEyes"
            };
            foreach (SpiritPortraitLayer layer in host.Layers)
            {
                SceneLitLayer lit = layer.Lighting;
                Assert.That(lit.Configured, Is.True, layer.Id);
                Assert.DoesNotThrow(lit.Validate, layer.Id);
                Assert.That(lit.BaseTexture, Is.SameAs(AssetDatabase.LoadAssetAtPath<Texture2D>(relight + expected[layer.Id] + "Unlit.png")), layer.Id);
                Assert.That(lit.Lights.Length, Is.EqualTo(2), layer.Id);
                Assert.That(lit.Lights[0].LightIndex, Is.Zero);
                Assert.That(lit.Lights[0].Mode, Is.EqualTo(SceneLitLightMode.Reference));
                Assert.That(lit.Lights[0].Reference, Is.SameAs(AssetDatabase.LoadAssetAtPath<Texture2D>(relight + expected[layer.Id] + "LanternLit.png")), layer.Id);
                Assert.That(lit.Lights[0].ResponseMap, Is.SameAs(response), layer.Id);
                Assert.That(lit.Lights[1].LightIndex, Is.EqualTo(candle.LightIndex));
                Assert.That(lit.Lights[1].Mode, Is.EqualTo(SceneLitLightMode.Computed));
                Assert.That(lit.Lights[1].Strength, Is.GreaterThan(0f));
                Assert.That(lit.Depth, Is.SameAs(depth), layer.Id);
                Assert.That(lit.DepthRange.x, Is.GreaterThan(lit.DepthRange.y), "Nearer pixels must move toward the viewer (-Z).");
                Assert.That(lit.DepthRange.y, Is.GreaterThan(candleLight.ReceiverPosition.z), "The merchant sits behind the table candle.");
                Assert.That(lit.MapRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
                Assert.That(lit.Wrap, Is.EqualTo(1f));
                // The source frame registers to the plate at plate px = (0.9136 x + 21, 0.94 y + 28.8); the candle's piece light
                // maps plate UV to the rig, so both frame corners must land where the candle's own mapping puts them.
                foreach (Vector2 corner in new[] { Vector2.zero, Vector2.one, new Vector2(.36f, .62f) })
                {
                    var plate = new Vector2((.9136f * corner.x * 941f + 21f) / 941f, 1f - (.94f * (1f - corner.y) * 1672f + 28.8f) / 1672f);
                    Vector2 delta = plate - candle.ReferencePosition;
                    Vector3 candleRig = candleLight.ReceiverPosition + candle.ReceiverOffsetX * delta.x + candle.ReceiverOffsetY * delta.y;
                    Vector3 layerRig = table.HostLightingPosition + lit.FrameOrigin + lit.FrameAxisX * corner.x + lit.FrameAxisY * corner.y;
                    Assert.That(layerRig.x, Is.EqualTo(candleRig.x).Within(.0005f), layer.Id + " " + corner);
                    Assert.That(layerRig.y, Is.EqualTo(candleRig.y).Within(.0005f), layer.Id + " " + corner);
                }
            }
            Assert.That(host.Layers.Select(layer => layer.Lighting.Lights[1].Strength).Distinct().Count(), Is.EqualTo(1));
        }

        [Test]
        public void MapRectangleReachesTheMaterialAndIsValidated()
        {
            var layer = new SceneLitLayer
            {
                Lights = new[] { new SceneLitLayerLight { LightIndex = 0 } },
                MapRect = new Rect(600f / 941f, 1f - 493f / 1672f, 49f / 941f, 86f / 1672f)
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
