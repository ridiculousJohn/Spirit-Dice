using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class SpiritTraySceneTests
    {
        private const string Lantern = "Assets/Dice/Content/Presentation/SpiritHouse/LastLanternTable.asset";
        private const string Watchtower = "Assets/Dice/Content/Presentation/Watchtower/WatchtowerTable.asset";
        private const string Moth = "Assets/Dice/Content/Presentation/MothCrossroads/MothCrossroadsTable.asset";
        private Scene preview;
        private readonly List<UnityEngine.Object> resources = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp() => preview = EditorSceneManager.NewPreviewScene();

        [TearDown]
        public void TearDown()
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            foreach (UnityEngine.Object resource in resources)
                if (resource != null) UnityEngine.Object.DestroyImmediate(resource);
            resources.Clear();
        }

        [TestCase(Lantern)]
        [TestCase(Watchtower)]
        [TestCase(Moth)]
        public void AuthoredTablesUseOneCameraAndTextureForBothBays(string path)
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(path);
            Assert.That(table, Is.Not.Null, path);
            Assert.That(table.TrayPresentation, Is.Not.Null, path);
            table.TrayPresentation.ValidateScene();
            var scene = CreateScene(table.TrayPresentation);
            var dice = AddDice(scene);
            var history = AddHistory(scene);
            Assert.That(dice.RenderCamera, Is.SameAs(scene.RenderCamera));
            Assert.That(history.RenderCamera, Is.SameAs(scene.RenderCamera));
            Assert.That(dice.Texture, Is.SameAs(scene.Texture));
            Assert.That(history.Texture, Is.SameAs(scene.Texture));
            Assert.That(scene.RenderRoot.GetComponentsInChildren<Camera>(true).Length, Is.EqualTo(1));
            Assert.That(scene.Texture.width, Is.EqualTo(table.TrayPresentation.TextureSize.x));
            Assert.That(scene.Texture.height, Is.EqualTo(table.TrayPresentation.TextureSize.y));
            Assert.That(scene.Geometry, Is.Not.SameAs(table.TrayPresentation.GeometryPrefab));
            foreach (var anchor in scene.Geometry.DiceSlots)
                Assert.That(anchor.IsChildOf(scene.RenderRoot), Is.True);
            Material[] authored = table.TrayPresentation.GeometryPrefab.GetComponentsInChildren<Renderer>()
                .SelectMany(renderer => renderer.sharedMaterials).ToArray();
            foreach (var renderer in scene.Geometry.GetComponentsInChildren<Renderer>())
                foreach (Material material in renderer.sharedMaterials)
                    Assert.That(authored.Contains(material), Is.False, "Scene lighting must not mutate authored materials.");
        }

        [TestCase(Lantern)]
        [TestCase(Watchtower)]
        [TestCase(Moth)]
        public void ModifierCategoriesUseAuthoredInkAndSeparateGlyphsWithoutTintingBoneOrNumberedDice(string path)
        {
            var catalog = TestContent.BuildStandardCatalog();
            var assets = AssetDatabase.FindAssets("t:ModifierPresentationAsset", new[] { "Assets/Dice/Content" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<ModifierPresentationAsset>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
            using var theme = SpiritModifierPresentation.InstallTheme(assets, catalog);
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(path);
            var scene = CreateScene(table.TrayPresentation);
            var dice = AddDice(scene);
            var history = AddHistory(scene);
            var lighting = new SceneLightingState();
            lighting.Sample(table.SceneLighting, 3.47);
            history.SetSceneLighting(lighting);
            var colors = new List<Color>();
            var glyphs = new List<Texture>();
            foreach (var symbol in new[] { ModifierCategorySymbol.Base, ModifierCategorySymbol.Multiplier,
                ModifierCategorySymbol.Combined, ModifierCategorySymbol.Rerolls })
            {
                var definition = catalog.Modifiers.First(value => SpiritModifierIcons.GetCategorySymbol(value) == symbol);
                var activated = new ModifierDefinition(definition.Id, definition.FamilyId, ModifierPhase.InitialRoll,
                    new AlwaysCondition(), new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 1) });
                var encounter = new EncounterDefinition("test.category.ink", new[] { new StageDefinition(1000000, 3, 3) },
                    startingModifiers: new[] { activated.Id });
                var mechanical = new MechanicalCatalog("test.category.ink", TestContent.Hands(), new[] { encounter }, new[] { activated });
                var contribution = EncounterSession.CreateEncounter(mechanical, encounter.Id, 60).Snapshot.ModifierContributions[0];
                history.Add(contribution, definition, false);
                var material = history.GetDieRenderer(history.Count - 1).sharedMaterial;
                var sprite = SpiritModifierIcons.GetCategorySprite(definition);
                Assert.That(sprite, Is.Not.Null, definition.Id);
                Assert.That(Vector4.Distance(material.GetColor("_BaseColor"), new Color(1, .97f, .9f)), Is.LessThan(.00001f));
                Assert.That(Vector4.Distance(material.GetColor("_FaceIconColor"), SpiritModifierIcons.GetInkColor(definition)), Is.LessThan(.00001f));
                Assert.That(Vector4.Distance(material.GetColor("_CategoryColor"), SpiritModifierIcons.GetInkColor(definition)), Is.LessThan(.00001f));
                Assert.That(material.GetFloat("_ShowCategory"), Is.EqualTo(1));
                Assert.That(material.GetTexture("_CategoryIcon"), Is.SameAs(sprite.texture));
                Assert.That(material.GetTexture("_FaceIcon"), Is.Not.SameAs(sprite.texture), "Family artwork keeps its own identity.");
                Vector2 min = sprite.uv.Aggregate(Vector2.one, Vector2.Min);
                Vector2 max = sprite.uv.Aggregate(Vector2.zero, Vector2.Max);
                Assert.That(material.GetVector("_CategoryIconRect"), Is.EqualTo(new Vector4(min.x, min.y, max.x - min.x, max.y - min.y)));
                Assert.That(material.GetFloat("_SceneLightingEnabled"), Is.EqualTo(table.SceneLighting != null ? 1 : 0));
                colors.Add(material.GetColor("_CategoryColor"));
                glyphs.Add(material.GetTexture("_CategoryIcon"));
            }
            Assert.That(colors.Distinct().Count(), Is.EqualTo(4));
            Assert.That(glyphs.Distinct().Count(), Is.EqualTo(4));
            history.SetResolving(1);
            Assert.That(Vector4.Distance(history.GetDieRenderer(1).sharedMaterial.GetColor("_CategoryColor"), colors[1]), Is.LessThan(.00001f));
            history.SetResolving(-1);
            Assert.That(scene.Geometry.DiceSlots.Length, Is.EqualTo(DiceValues.Count));
            var numberedDice = scene.Geometry.DiceSlots.Select((slot, index) =>
                slot.Find("Spirit Dice Slot Content " + index + "/Spirit Die " + index)
                    .GetComponent<MeshRenderer>()).ToArray();
            Assert.That(numberedDice.Length, Is.EqualTo(5));
            foreach (var renderer in numberedDice)
                Assert.That(renderer.sharedMaterial.GetFloat("_ShowCategory"), Is.Zero, renderer.name);
        }

        [Test]
        public void UnauthoredModifierKeepsItsFamilySigilAndHidesCategoryMarks()
        {
            var scene = CreateScene(AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(Lantern).TrayPresentation);
            var history = AddHistory(scene);
            var contribution = Contribution(out ModifierDefinition definition);
            history.Add(contribution, definition, false);
            var material = history.GetDieRenderer(0).sharedMaterial;
            Assert.That(material.GetFloat("_ShowCategory"), Is.Zero);
            Assert.That(material.GetFloat("_ShowFaceIcon"), Is.EqualTo(1));
            Assert.That(material.GetTexture("_FaceIcon"), Is.Not.Null);
            Assert.That(Vector4.Distance(material.GetColor("_FaceIconColor"), SpiritModifierIcons.GetInkColor(definition)), Is.LessThan(.00001f));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void TopFaceArtworkKeepsItsUpperLeftAwayFromThePlayer(bool category, bool glyphOnly)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Top-face orientation pixels require a real graphics device.");
            var scene = CreateScene(AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(Lantern).TrayPresentation);
            var history = AddHistory(scene);
            var contribution = Contribution(out ModifierDefinition definition);
            var icon = new Texture2D(8, 8, TextureFormat.RGBA32, false, true)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            resources.Add(icon);
            var pixels = new Color[64];
            for (int y = 4; y < 8; ++y)
                for (int x = 0; x < 4; ++x) pixels[y * 8 + x] = Color.white;
            icon.SetPixels(pixels);
            icon.Apply();
            var sprite = Sprite.Create(icon, new Rect(0, 0, 8, 8), Vector2.one * .5f, 8);
            resources.Add(sprite);
            Sprite previous = SpiritModifierIcons.GetSprite(definition);
            try
            {
                SpiritModifierIcons.RegisterIcon(definition.Id, sprite);
                history.Add(contribution, definition, false);
            }
            finally { SpiritModifierIcons.RegisterIcon(definition.Id, previous); }
            var renderer = history.GetDieRenderer(0);
            foreach (var other in scene.RenderRoot.GetComponentsInChildren<MeshRenderer>()) other.enabled = other == renderer;
            Material material = renderer.sharedMaterial;
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_SurfaceTexture", Texture2D.whiteTexture);
            material.SetColor("_FaceIconColor", Color.black);
            material.SetFloat("_ShowFaceIcon", glyphOnly ? 0 : 1);
            material.SetFloat("_ShowCategory", category ? 1 : 0);
            material.SetTexture("_CategoryIcon", icon);
            material.SetVector("_CategoryIconRect", new Vector4(0, 0, 1, 1));
            material.SetVector("_CategoryIconUVTransform", new Vector4(1, 1, 0, 0));
            material.SetColor("_CategoryColor", Color.black);
            material.SetFloat("_SceneLightingEnabled", 1);
            material.SetVector("_SceneLightingAmbient", Vector4.one);
            material.SetVector("_SceneLightingKeyColor", Vector4.zero);
            Transform die = history.GetDieTransform(0);
            var camera = Root("Top-face orientation camera").AddComponent<Camera>();
            camera.scene = preview;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.enabled = false;
            camera.transform.position = die.TransformPoint(new Vector3(0, 3, -4));
            camera.transform.LookAt(die.TransformPoint(new Vector3(0, .5f, 0)), die.up);
            camera.orthographic = true;
            camera.orthographicSize = die.TransformVector(Vector3.right).magnitude * .75f;
            camera.aspect = 1;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 20;
            camera.cullingMask = 1 << SpiritDiceTray.RenderLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.magenta;
            camera.allowHDR = camera.allowMSAA = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            resources.Add(target);
            target.Create();
            camera.targetTexture = target;
            RenderPipelineAsset previousPipeline = QualitySettings.renderPipeline;
            try
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/SpiritPrototypePipeline.asset");
                Assert.That(pipeline, Is.Not.Null);
                QualitySettings.renderPipeline = pipeline;
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            finally { QualitySettings.renderPipeline = previousPipeline; }
            var readback = new Texture2D(256, 256, TextureFormat.RGBAFloat, false, true);
            resources.Add(readback);
            RenderTexture previousTarget = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                readback.Apply();
            }
            finally { RenderTexture.active = previousTarget; }

            float centerX = glyphOnly ? .20f : 0;
            float centerZ = glyphOnly ? -.28f : category ? .09f : 0;
            float offset = glyphOnly ? .0315f : category ? .125f : .17f;
            Color inkSample = Pixel(centerX - offset, centerZ + offset);
            if (inkSample.maxColorComponent >= .15f)
            {
                var rendered = readback.GetPixels();
                TestContext.WriteLine("Top-face sample " + inkSample + "; opposite " + Pixel(centerX + offset, centerZ - offset) +
                    "; background pixels " + rendered.Count(value => value.r > .9f && value.g < .1f && value.b > .9f) +
                    "; dark pixels " + rendered.Count(value => value.r < .15f && value.g < .15f && value.b < .15f));
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                    "top-face-orientation", Guid.NewGuid().ToString("N")));
                Directory.CreateDirectory(directory);
                var diagnostic = new Texture2D(256, 256, TextureFormat.RGBA32, false, true);
                resources.Add(diagnostic);
                diagnostic.SetPixels(rendered);
                diagnostic.Apply();
                string path = Path.Combine(directory, "category-" + category + "-glyph-" + glyphOnly + ".png");
                File.WriteAllBytes(path, diagnostic.EncodeToPNG());
                TestContext.WriteLine("Top-face diagnostic: " + path);
            }
            Assert.That(Pixel(centerX - offset, centerZ + offset).maxColorComponent, Is.LessThan(.15f),
                "The texture's upper-left quadrant belongs on the far-left part of the physical top face.");
            AssertBone(Pixel(centerX - offset, centerZ - offset));
            AssertBone(Pixel(centerX + offset, centerZ + offset));
            AssertBone(Pixel(centerX + offset, centerZ - offset));
            if (glyphOnly)
            {
                Assert.That(Pixel(-.13f, -.2925f).maxColorComponent, Is.LessThan(.15f), "The category bar belongs on the player edge.");
                AssertBone(Pixel(-.13f, .2925f));
            }

            Color Pixel(float x, float z)
            {
                Vector3 viewport = camera.WorldToViewportPoint(die.TransformPoint(new Vector3(x, .5f, z)));
                Assert.That(viewport.x, Is.InRange(0f, 1f));
                Assert.That(viewport.y, Is.InRange(0f, 1f));
                return readback.GetPixel(Mathf.FloorToInt(viewport.x * 256), Mathf.FloorToInt(viewport.y * 256));
            }

            void AssertBone(Color value)
            {
                Assert.That(value.r, Is.GreaterThan(.75f));
                Assert.That(value.g, Is.GreaterThan(.75f));
                Assert.That(value.b, Is.GreaterThan(.75f));
            }
        }

        [TestCase(Lantern, false)]
        [TestCase(Lantern, true)]
        [TestCase(Watchtower, false)]
        [TestCase(Watchtower, true)]
        [TestCase(Moth, false)]
        [TestCase(Moth, true)]
        public void AutomaticFramingContainsEveryAuthoredMeshCornerAndBay(string path, bool orthographic)
        {
            var source = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(path).TrayPresentation;
            var asset = UnityEngine.Object.Instantiate(source);
            resources.Add(asset);
            asset.Orthographic = orthographic;
            var scene = CreateScene(asset);
            AssertAuthoredGeometryFits(scene);
            var dice = AddDice(scene);
            dice.SetSelection(DiceValues.AllMask);
            AssertRollEnvelopeFits(scene, dice);
            var history = AddHistory(scene);
            ModifierContribution contribution = Contribution(out ModifierDefinition definition);
            for (int index = 0; index < history.Capacity; ++index) history.Add(contribution, definition, true);
            for (int sample = 0; sample <= 6; ++sample)
            {
                history.Advance(SpiritModifierHistoryRenderer.ArrivalDuration / 6);
                AssertMeshCornersFit(scene.RenderCamera, scene.Geometry.ModifierRoot);
            }
        }

        [TestCase(false, 512, 1024, 27, 43, 18)]
        [TestCase(false, 1536, 256, 82, -35, -24)]
        [TestCase(false, 768, 768, 12, 125, 32)]
        [TestCase(true, 512, 1024, 27, 43, 18)]
        [TestCase(true, 1536, 256, 82, -35, -24)]
        [TestCase(true, 768, 768, 12, 125, 32)]
        public void ViewAndAspectChangesRefitGeometryWithoutReplacingCameraOrTexture(bool orthographic,
            int width, int height, float pitch, float yaw, float roll)
        {
            var asset = Variant(orthographic);
            var scene = CreateScene(asset);
            Camera camera = scene.RenderCamera;
            RenderTexture texture = scene.Texture;
            asset.TextureSize = new Vector2Int(width, height);
            asset.CameraEulerAngles = new Vector3(pitch, yaw, roll);
            asset.CameraPosition = new Vector3(1000, -2000, 500);
            asset.OrthographicSize = .01f;
            asset.FieldOfView = 31;
            scene.ApplyPresentationSettings();
            Assert.That(scene.RenderCamera, Is.SameAs(camera));
            Assert.That(scene.Texture, Is.SameAs(texture));
            Assert.That(texture.IsCreated(), Is.True);
            Assert.That(camera.aspect, Is.EqualTo((float)width / height).Within(.0001f));
            Assert.That(Quaternion.Angle(camera.transform.localRotation, Quaternion.Euler(asset.CameraEulerAngles)),
                Is.LessThan(.001f));
            AssertAuthoredGeometryFits(scene);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RebuiltOversizedTransformedGeometryExpandsFramingAndDepthPlanes(bool orthographic)
        {
            var asset = Variant(orthographic);
            var scene = CreateScene(asset);
            Camera camera = scene.RenderCamera;
            RenderTexture texture = scene.Texture;
            var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = "Oversized custom front rail";
            rail.transform.SetParent(asset.GeometryPrefab.transform, false);
            rail.transform.localPosition = new Vector3(30, -8, -15);
            rail.transform.localRotation = Quaternion.Euler(17, 39, -12);
            rail.transform.localScale = new Vector3(70, 18, 45);
            asset.GeometryPrefab.transform.localPosition = new Vector3(-13, 4, 21);
            asset.GeometryPrefab.transform.localRotation = Quaternion.Euler(-12, 18, 9);
            asset.GeometryPrefab.transform.localScale = new Vector3(1.3f, .8f, 1.7f);
            asset.CameraEulerAngles = new Vector3(49, -23, 11);
            asset.NearClipPlane = .1f;
            asset.FarClipPlane = .2f;
            scene.RebuildGeometry();
            Assert.That(scene.RenderCamera, Is.SameAs(camera));
            Assert.That(scene.Texture, Is.SameAs(texture));
            Assert.That(camera.farClipPlane, Is.GreaterThan(asset.FarClipPlane));
            AssertAuthoredGeometryFits(scene);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AnimatedDiceAndGrowingHistoryKeepTheAuthoredFramingStable(bool orthographic)
        {
            var asset = Variant(orthographic);
            var scene = CreateScene(asset);
            var dice = AddDice(scene);
            var history = AddHistory(scene);
            Vector3 position = scene.RenderCamera.transform.localPosition;
            float size = scene.RenderCamera.orthographicSize;
            float near = scene.RenderCamera.nearClipPlane;
            float far = scene.RenderCamera.farClipPlane;
            ModifierContribution contribution = Contribution(out ModifierDefinition definition);
            for (int index = 0; index < history.Capacity + 30; ++index)
            {
                history.Add(contribution, definition, true);
                history.SetScroll(index % 2);
                history.Advance(.015f);
                scene.ApplyPresentationSettings();
                Assert.That(scene.RenderCamera.transform.localPosition, Is.EqualTo(position));
                Assert.That(scene.RenderCamera.orthographicSize, Is.EqualTo(size));
                Assert.That(scene.RenderCamera.nearClipPlane, Is.EqualTo(near));
                Assert.That(scene.RenderCamera.farClipPlane, Is.EqualTo(far));
            }
            AssertRollEnvelopeFits(scene, dice);
            Assert.That(scene.RenderCamera.transform.localPosition, Is.EqualTo(position));
            Assert.That(scene.RenderCamera.orthographicSize, Is.EqualTo(size));
            Assert.That(scene.RenderCamera.nearClipPlane, Is.EqualTo(near));
            Assert.That(scene.RenderCamera.farClipPlane, Is.EqualTo(far));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ChangedCameraAndSurfaceAnchorsPreserveEveryFaceAndSelectiveInterruptedRolls(bool orthographic)
        {
            var asset = Variant(orthographic);
            var scene = CreateScene(asset);
            var dice = AddDice(scene);
            for (int face = 1; face <= 6; ++face)
            {
                dice.SetValues(new DiceValues(face, face, face, face, face));
                for (int index = 0; index < DiceValues.Count; ++index)
                {
                    Assert.That(dice.GetVisibleFace(index), Is.EqualTo(face), "Slot " + index);
                    Rect projected = dice.GetSlotViewportRect(index);
                    Assert.That(projected.width, Is.GreaterThan(0));
                    Assert.That(projected.height, Is.GreaterThan(0));
                    Assert.That(projected.Contains(dice.GetSlotViewportPosition(index)), Is.True);
                    Vector3 label = scene.RenderCamera.WorldToViewportPoint(scene.Geometry.DiceLabelAnchors[index].position);
                    Assert.That(Vector2.Distance(dice.GetSlotLabelViewportPosition(index), label), Is.LessThan(.0001f));
                }
            }
            dice.SetValues(new DiceValues(1, 2, 3, 4, 5));
            IEnumerator roll = dice.Roll(new DiceValues(6, 5, 4, 3, 2), (1 << 1) | (1 << 3), 0);
            Assert.That(roll.MoveNext(), Is.False);
            AssertFaces(dice, new[] { 1, 5, 3, 3, 5 });
            roll = dice.Roll(new DiceValues(6, 6, 6, 6, 6), 1 << 2);
            Assert.That(roll.MoveNext(), Is.True);
            ((IDisposable)roll).Dispose();
            Assert.That(dice.IsRolling, Is.False);
            AssertFaces(dice, new[] { 1, 5, 6, 3, 5 });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RotatedBayScrollClipsInAuthoredCoordinatesAndPointerTargetsFollowProjection(bool orthographic)
        {
            var asset = Variant(orthographic);
            var scene = CreateScene(asset);
            var canvasRoot = Root("History pointer projection", typeof(RectTransform)).GetComponent<RectTransform>();
            canvasRoot.sizeDelta = new Vector2(700, 350);
            canvasRoot.gameObject.AddComponent<Canvas>();
            var history = Root("History UI").AddComponent<SpiritModifierHistoryTray>();
            history.Configure(asset, scene);
            history.Build(canvasRoot);
            ModifierDefinition definition;
            ModifierContribution contribution = Contribution(out definition);
            int capacity = scene.Geometry.ModifierVisibleCapacity;
            for (int i = 0; i < capacity + 5; ++i) history.Add(contribution, definition, false);
            Canvas.ForceUpdateCanvases();
            history.Scroll.horizontalNormalizedPosition = 0;
            Invoke(history, "LateUpdate");
            Assert.That(history.Renderer.GetDieViewportRect(capacity + 4), Is.EqualTo(Rect.zero));
            Assert.That(history.Renderer.GetDieViewportRect(0).width, Is.GreaterThan(0));
            AssertProjectedToken(history, canvasRoot, 0);
            Vector3 first = history.Renderer.GetDieTransform(0).position;
            history.Scroll.horizontalNormalizedPosition = 1;
            Invoke(history, "LateUpdate");
            Assert.That(history.Renderer.GetDieViewportRect(0), Is.EqualTo(Rect.zero));
            Assert.That(history.Renderer.GetDieViewportRect(capacity + 4).width, Is.GreaterThan(0));
            AssertProjectedToken(history, canvasRoot, capacity + 4);
            Assert.That(Vector3.Distance(first, history.Renderer.GetDieTransform(0).position), Is.GreaterThan(1));
            Material material = history.Renderer.GetDieRenderer(capacity + 4).sharedMaterial;
            var clip = material.GetVector("_ClipLocalX");
            Assert.That(clip.x, Is.EqualTo(-scene.Geometry.ModifierWidth * .5f));
            Assert.That(clip.y, Is.EqualTo(scene.Geometry.ModifierWidth * .5f));
            Vector3 local = new Vector3(.23f, .4f, -.1f);
            Vector3 world = scene.Geometry.ModifierRoot.TransformPoint(local);
            Vector3 shaderLocal = material.GetMatrix("_ClipWorldToLocal").MultiplyPoint3x4(world);
            Assert.That(Vector3.Distance(shaderLocal, local), Is.LessThan(.003f));
            Assert.That(history.GetComponentsInChildren<RawImage>(true), Is.Empty,
                "History UI should project hits over the shared texture.");
        }

        [Test]
        public void ReplacingSceneAndDestroyingConsumersLeavesBorrowedResourcesAndSiblingCameraAlive()
        {
            var first = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(Lantern).TrayPresentation;
            var second = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(Watchtower).TrayPresentation;
            var scene = CreateScene(first);
            var dice = AddDice(scene);
            var history = AddHistory(scene);
            Transform oldRoot = scene.RenderRoot;
            Camera oldCamera = scene.RenderCamera;
            RenderTexture oldTexture = scene.Texture;
            dice.ReleasePresentation();
            history.ReleasePresentation();
            scene.Configure(second);
            scene.Initialize();
            dice.Configure(second, scene); dice.Initialize();
            history.Configure(second, scene); history.Initialize();
            Assert.That(oldRoot == null && oldCamera == null && oldTexture == null, Is.True);
            Camera sharedCamera = scene.RenderCamera;
            RenderTexture sharedTexture = scene.Texture;
            UnityEngine.Object.DestroyImmediate(dice.gameObject);
            Assert.That(sharedCamera != null && sharedTexture != null && sharedTexture.IsCreated(), Is.True);
            Assert.That(history.RenderCamera, Is.SameAs(sharedCamera));
            history.enabled = false;
            Invoke(history, "OnDisable");
            Assert.That(scene.RenderRoot.gameObject.activeSelf, Is.True,
                "A consumer must not disable the shared scene.");
            scene.enabled = false;
            Invoke(scene, "OnDisable");
            Assert.That(scene.RenderRoot.gameObject.activeSelf, Is.False);
            scene.enabled = true;
            Invoke(scene, "OnEnable");
            Assert.That(scene.RenderRoot.gameObject.activeSelf, Is.True);
            Assert.That(first.GeometryPrefab != null && second.GeometryPrefab != null && first.BoneTexture != null, Is.True);
        }

        private SpiritTrayPresentationAsset Variant(bool orthographic)
        {
            var source = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(Lantern).TrayPresentation;
            var asset = UnityEngine.Object.Instantiate(source);
            resources.Add(asset);
            var geometry = UnityEngine.Object.Instantiate(source.GeometryPrefab);
            resources.Add(geometry.gameObject);
            geometry.gameObject.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(geometry.gameObject, preview);
            geometry.gameObject.SetActive(false);
            asset.GeometryPrefab = geometry;
            asset.Orthographic = orthographic;
            asset.CameraPosition = new Vector3(0, 12, -7);
            asset.CameraEulerAngles = Quaternion.LookRotation(new Vector3(0, 0, .3f) - asset.CameraPosition).eulerAngles;
            asset.FieldOfView = 48;
            asset.OrthographicSize = 5.4f;
            for (int i = 0; i < DiceValues.Count; ++i)
            {
                geometry.DiceSlots[i].localPosition += new Vector3((i % 2) * .1f, .025f * i, .12f * (i - 2));
                geometry.DiceSlots[i].localRotation = Quaternion.Euler(0, i * 11 - 20, 0);
                geometry.DiceLabelAnchors[i].localPosition += new Vector3(.08f, 0, -.1f);
            }
            geometry.ModifierRoot.localRotation = Quaternion.Euler(0, 27, 0);
            return asset;
        }

        private SpiritTrayScene CreateScene(SpiritTrayPresentationAsset asset)
        {
            var scene = Root("Shared tray test").AddComponent<SpiritTrayScene>();
            scene.Configure(asset); scene.Initialize();
            scene.Geometry.gameObject.SetActive(true);
            SceneManager.MoveGameObjectToScene(scene.RenderRoot.gameObject, preview);
            scene.RenderCamera.scene = preview;
            scene.RenderCamera.enabled = false;
            return scene;
        }

        private SpiritDiceTray AddDice(SpiritTrayScene scene)
        {
            var dice = Root("Shared dice consumer").AddComponent<SpiritDiceTray>();
            dice.Configure(scene.Presentation, scene); dice.Initialize();
            return dice;
        }

        [Test]
        public void ReadyTrayFiltersRollsPreservesResourceOnlyActivationsAndRefreshesDescriptions()
        {
            var history = ReadyTray(out MechanicalCatalog catalog, out EncounterSession session);
            var preview = session.PreviewHandResolution();
            var before = session.Snapshot;
            history.SynchronizeReady(preview.Score.ModifierContributions, catalog);
            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(history.Contributions.Select(value => value.Phase), Is.All.EqualTo(ModifierPhase.PlayHand));
            Assert.That(history.Contributions[2].BaseScore, Is.Zero);
            Assert.That(history.Contributions[2].Multiplier, Is.Zero);
            Assert.That(history.Contributions[2].Rerolls, Is.EqualTo(2));
            Assert.That(session.Snapshot.Revision, Is.EqualTo(before.Revision));
            Assert.That(session.Snapshot.RerollsRemaining, Is.EqualTo(before.RerollsRemaining));

            Transform die = history.Renderer.GetDieTransform(0);
            history.SynchronizeReady(session.PreviewHandResolution().Score.ModifierContributions, catalog);
            Assert.That(history.Renderer.GetDieTransform(0), Is.SameAs(die));
            ModifierContribution selected = default;
            history.TokenSelected += (contribution, definition) => selected = contribution;
            history.SynchronizeReady(new[] { history.Contributions[2] }, catalog);
            Assert.That(history.Count, Is.EqualTo(1));
            history.Content.GetComponentInChildren<Button>(true).onClick.Invoke();
            Assert.That(selected.InstanceId, Is.EqualTo(preview.Score.ModifierContributions.Last().InstanceId));
            history.SynchronizeReady(Array.Empty<ModifierContribution>(), catalog);
            Assert.That(history.Count, Is.Zero);
            Assert.That(history.Renderer.Count, Is.Zero);
        }

        [Test]
        public void ReadyTrayHighlightsExactInstancesAndOccurrencesAndRevealsOverflow()
        {
            var history = ReadyTray(out MechanicalCatalog catalog, out EncounterSession session);
            var ready = session.PreviewHandResolution().Score.ModifierContributions
                .Where(value => value.Phase == ModifierPhase.PlayHand).ToArray();
            var repeated = Enumerable.Repeat(ready[0], history.Renderer.Capacity + 2).ToList();
            repeated.Insert(1, ready[1]);
            history.SynchronizeReady(repeated, catalog);
            Canvas.ForceUpdateCanvases();
            Color bone = history.Renderer.GetDieRenderer(0).sharedMaterial.GetColor("_BaseColor");
            Vector3 resting = history.Renderer.GetDieTransform(1).localPosition;
            Assert.That(history.SetResolving(ready[1]), Is.True);
            Assert.That(history.ResolvingIndex, Is.EqualTo(1));
            Assert.That(history.Renderer.GetDieTransform(1).localPosition.y, Is.GreaterThan(resting.y));
            Assert.That(history.Renderer.GetDieRenderer(1).sharedMaterial.GetColor("_BaseColor"), Is.Not.EqualTo(bone));
            Assert.That(history.Renderer.GetDieRenderer(0).sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(bone));
            Assert.That(history.SetResolving(ready[0], history.Renderer.Capacity + 1), Is.True);
            Assert.That(history.ResolvingIndex, Is.EqualTo(repeated.Count - 1));
            Assert.That(history.Scroll.horizontalNormalizedPosition, Is.EqualTo(1).Within(.0001f));
            Assert.That(history.Renderer.GetDieViewportRect(repeated.Count - 1).width, Is.GreaterThan(0));
            history.ClearResolving();
            Assert.That(history.ResolvingIndex, Is.EqualTo(-1));
            Assert.That(history.Renderer.GetDieTransform(1).localPosition.y, Is.EqualTo(resting.y));
            Assert.That(history.Renderer.GetDieRenderer(1).sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(bone));
            Assert.That(history.SetResolving(ready[2]), Is.False);
            Assert.That(history.ResolvingIndex, Is.EqualTo(-1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClearingReadyHighlightAfterSceneHierarchyDestructionDoesNotRebuildPresentation(bool destroyWholeScene)
        {
            var history = ReadyTray(out MechanicalCatalog catalog, out EncounterSession session);
            history.SynchronizeReady(session.PreviewHandResolution().Score.ModifierContributions, catalog);
            Assert.That(history.SetResolving(history.Contributions[0]), Is.True);
            var renderer = history.Renderer;
            Transform renderRoot = renderer.RenderRoot;
            if (destroyWholeScene) UnityEngine.Object.DestroyImmediate(renderRoot.gameObject);
            else UnityEngine.Object.DestroyImmediate(renderer.GetDieTransform(0).gameObject);

            Assert.DoesNotThrow(() => history.ClearResolving());
            Assert.That(history.ResolvingIndex, Is.EqualTo(-1));
            Assert.That(renderer.RenderRoot, destroyWholeScene ? Is.Null : Is.SameAs(renderRoot));
            Assert.That(renderer.GetDieTransform(0) == null, Is.True);
        }

        private SpiritModifierHistoryTray ReadyTray(out MechanicalCatalog catalog, out EncounterSession session)
        {
            var scene = CreateScene(AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(Lantern).TrayPresentation);
            var canvasRoot = Root("Ready tray canvas", typeof(RectTransform)).GetComponent<RectTransform>();
            canvasRoot.sizeDelta = new Vector2(700, 350);
            canvasRoot.gameObject.AddComponent<Canvas>();
            var history = Root("Ready tray").AddComponent<SpiritModifierHistoryTray>();
            history.Configure(scene.Presentation, scene);
            history.Build(canvasRoot);
            var roll = new ModifierDefinition("test.ready.roll", "test.ready.roll", ModifierPhase.InitialRoll,
                new AlwaysCondition(), new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 1) });
            var score = new ModifierDefinition("test.ready.score", "test.ready.score", ModifierPhase.PlayHand,
                new AlwaysCondition(), new[] { new ModifierEffect(ModifierEffectKind.AddMultiplier, 2) });
            var grant = new ModifierDefinition("test.ready.grant", "test.ready.grant", ModifierPhase.PlayHand,
                new AlwaysCondition(), new[] { new ModifierEffect(ModifierEffectKind.AddRerolls, 2) });
            var encounter = new EncounterDefinition("test.ready", new[] { new StageDefinition(1000000, 3, 3) },
                startingModifiers: new[] { roll.Id, score.Id, score.Id, grant.Id });
            catalog = new MechanicalCatalog("test.ready", TestContent.Hands(), new[] { encounter }, new[] { roll, score, grant });
            session = EncounterSession.CreateEncounter(catalog, encounter.Id, 60);
            return history;
        }

        private SpiritModifierHistoryRenderer AddHistory(SpiritTrayScene scene)
        {
            var history = Root("Shared history consumer").AddComponent<SpiritModifierHistoryRenderer>();
            history.Configure(scene.Presentation, scene); history.Initialize();
            return history;
        }

        private GameObject Root(string name, params Type[] components)
        {
            var root = new GameObject(name, components) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(root, preview);
            return root;
        }

        private static ModifierContribution Contribution(out ModifierDefinition definition)
        {
            definition = new ModifierDefinition("test.shared.history", "test.shared.history", ModifierPhase.InitialRoll,
                new AlwaysCondition(), new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 1) });
            var encounter = new EncounterDefinition("test.shared.tray", new[] { new StageDefinition(1000000, 3, 3) },
                startingModifiers: new[] { definition.Id });
            var catalog = new MechanicalCatalog("test.shared.tray", TestContent.Hands(), new[] { encounter }, new[] { definition });
            return EncounterSession.CreateEncounter(catalog, encounter.Id, 60).Snapshot.ModifierContributions[0];
        }

        private static void AssertFaces(SpiritDiceTray tray, int[] values)
        { for (int i = 0; i < values.Length; ++i) Assert.That(tray.GetVisibleFace(i), Is.EqualTo(values[i]), "Slot " + i); }

        private static void AssertAuthoredGeometryFits(SpiritTrayScene scene)
        {
            AssertMeshCornersFit(scene.RenderCamera, scene.Geometry.transform);
            foreach (Transform label in scene.Geometry.DiceLabelAnchors)
                AssertPointFits(scene.RenderCamera, label.position, label.name);
            SpiritTrayGeometry geometry = scene.Geometry;
            for (int x = -1; x <= 1; x += 2)
                for (int y = 0; y <= 1; ++y)
                    for (int z = -1; z <= 1; z += 2)
                        AssertPointFits(scene.RenderCamera, geometry.ModifierRoot.TransformPoint(new Vector3(
                            x * geometry.ModifierWidth * .5f, y * geometry.ModifierDieSize * 1.5f,
                            z * geometry.ModifierDepth * .5f)), "Modifier bay");
        }

        private static void AssertMeshCornersFit(Camera camera, Transform root)
        {
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds bounds = filter.sharedMesh.bounds;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                            AssertPointFits(camera, filter.transform.TransformPoint(bounds.center +
                                Vector3.Scale(bounds.extents, new Vector3(x, y, z))), filter.name);
            }
        }

        private static void AssertPointFits(Camera camera, Vector3 world, string name)
        {
            Vector3 viewport = camera.WorldToViewportPoint(world);
            Assert.That(viewport.x, Is.InRange(.025f, .975f), name + " horizontal");
            Assert.That(viewport.y, Is.InRange(.025f, .975f), name + " vertical");
            Assert.That(viewport.z, Is.GreaterThan(camera.nearClipPlane), name + " near plane");
            Assert.That(viewport.z, Is.LessThan(camera.farClipPlane), name + " far plane");
        }

        private static void AssertRollEnvelopeFits(SpiritTrayScene scene, SpiritDiceTray dice)
        {
            var timing = new SpiritRollTiming(.55f, new SpiritRollContactTrack
            {
                contactTimes = new[] { .16f, .3f, .43f }, settleTime = 0
            }, 0, 0, 0);
            var transforms = (Transform[])typeof(SpiritDiceTray).GetField("dice",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(dice);
            MethodInfo support = typeof(SpiritDiceTray).GetMethod("SupportHeight",
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (int sample = 0; sample <= 24; ++sample)
                for (int index = 0; index < transforms.Length; ++index)
                {
                    timing.SamplePose(index, transforms.Length, sample * timing.Duration / 24,
                        Quaternion.identity, Quaternion.Euler(0, index * 13, 90),
                        out Quaternion rotation, out float lift, out float drift);
                    Transform die = transforms[index];
                    die.localRotation = rotation;
                    float height = (float)support.Invoke(dice, new object[] { rotation });
                    die.localPosition = new Vector3(drift, height + lift, 0) * scene.Geometry.DiceScale;
                    scene.ApplyPresentationSettings();
                    AssertMeshCornersFit(scene.RenderCamera, die);
                }
        }

        private static void AssertProjectedToken(SpiritModifierHistoryTray history, RectTransform image, int index)
        {
            var token = history.Content.Find("ModifierToken" + index) as RectTransform;
            Assert.That(token, Is.Not.Null);
            Assert.That(token.gameObject.activeSelf, Is.True);
            Rect projected = history.Renderer.GetDieViewportRect(index);
            Vector3 expected = image.TransformPoint(image.rect.min + Vector2.Scale(projected.center, image.rect.size));
            Assert.That(Vector3.Distance(token.position, expected), Is.LessThan(.01f));
            Assert.That(Vector2.Distance(token.sizeDelta, Vector2.Scale(projected.size, image.rect.size)), Is.LessThan(.01f));
        }

        private static void Invoke(object value, string name)
            => value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, null);
    }
}
