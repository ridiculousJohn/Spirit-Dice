using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
