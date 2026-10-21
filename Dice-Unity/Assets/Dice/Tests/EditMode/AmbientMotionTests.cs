using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class AmbientMotionTests
    {
        private const string PrefabPath = "Assets/Dice/Content/Presentation/Ambient/HangingCup.prefab";
        private const string CanopyPrefabPath = "Assets/Dice/Content/Presentation/Ambient/Canopy.prefab";
        private const string ScenePath = "Assets/Scenes/AmbientMotionPreview.unity";
        private Scene preview;

        [SetUp]
        public void SetUp() => preview = EditorSceneManager.NewPreviewScene();

        [TearDown]
        public void TearDown()
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }

        [Test]
        public void PrefabUsesAuthoredNativeAutoplayWithoutRuntimeScripts()
        {
            GameObject cup = InstantiateCup();
            var animation = cup.GetComponent<Animation>();
            Assert.That(animation, Is.Not.Null);
            Assert.That(animation.playAutomatically, Is.True);
            Assert.That(animation.cullingType, Is.EqualTo(AnimationCullingType.AlwaysAnimate));
            Assert.That(animation.wrapMode, Is.EqualTo(WrapMode.Loop));
            Assert.That(animation.clip, Is.Not.Null);
            Assert.That(animation.clip.legacy, Is.True);
            Assert.That(animation.clip.wrapMode, Is.EqualTo(WrapMode.Loop));
            Assert.That(animation.clip.length, Is.EqualTo(4).Within(.001f));
            Assert.That(AnimationUtility.GetAnimationClipSettings(animation.clip).loopTime, Is.True);
            Assert.That(cup.transform, Is.TypeOf<RectTransform>());
            Assert.That(cup.GetComponentsInChildren<MonoBehaviour>(true).All(component => component is RawImage),
                Is.True, "The reusable element should need only native animation and its image.");
            var image = cup.GetComponentInChildren<RawImage>();
            Assert.That(image, Is.Not.Null);
            Assert.That(image.texture, Is.Not.Null);
            Assert.That(image.raycastTarget, Is.False);
            Assert.That(image.rectTransform.parent.name, Is.EqualTo("Swing"));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(cup), Is.Not.Null);
        }

        [Test]
        public void AnimationBindingSwingsTheImageAroundAFixedPin()
        {
            GameObject cup = InstantiateCup();
            var root = (RectTransform)cup.transform;
            root.anchoredPosition = new Vector2(73, 181);
            var swing = (RectTransform)root.Find("Swing");
            var image = cup.GetComponentInChildren<RawImage>().rectTransform;
            Vector3 pin = swing.position;
            Vector3 rootPosition = root.position;
            Vector3 lowerPoint = image.TransformPoint(new Vector3(0, image.rect.yMin, 0));
            Sample(cup, 1);
            Assert.That(Mathf.DeltaAngle(0, swing.localEulerAngles.z), Is.EqualTo(3).Within(.02f));
            Assert.That(Vector3.Distance(pin, swing.position), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(rootPosition, root.position), Is.LessThan(.0001f));
            Vector3 right = image.TransformPoint(new Vector3(0, image.rect.yMin, 0));
            Assert.That(Vector3.Distance(right, lowerPoint), Is.GreaterThan(1));
            Sample(cup, 3);
            Assert.That(Mathf.DeltaAngle(0, swing.localEulerAngles.z), Is.EqualTo(-3).Within(.02f));
            Assert.That(Vector3.Distance(pin, swing.position), Is.LessThan(.0001f));
            Vector3 left = image.TransformPoint(new Vector3(0, image.rect.yMin, 0));
            Assert.That(Vector3.Distance(left, right), Is.GreaterThan(5));
        }

        [Test]
        public void LoopJoinsAtTheSamePoseAndAngularVelocity()
        {
            GameObject cup = InstantiateCup();
            AnimationClip clip = cup.GetComponent<Animation>().clip;
            Transform swing = cup.transform.Find("Swing");
            clip.SampleAnimation(cup, 0);
            Quaternion first = swing.localRotation;
            clip.SampleAnimation(cup, clip.length);
            Assert.That(Quaternion.Angle(first, swing.localRotation), Is.LessThan(.001f));
            const float step = .001f;
            clip.SampleAnimation(cup, step);
            float initialVelocity = Mathf.DeltaAngle(0, swing.localEulerAngles.z) / step;
            clip.SampleAnimation(cup, clip.length - step);
            float finalVelocity = -Mathf.DeltaAngle(0, swing.localEulerAngles.z) / step;
            Assert.That(initialVelocity, Is.GreaterThan(4));
            Assert.That(finalVelocity, Is.EqualTo(initialVelocity).Within(.02f));
        }

        [Test]
        public void ReusedInstancesShareTheClipAndKeepIndependentTransformState()
        {
            GameObject first = InstantiateCup();
            GameObject second = InstantiateCup();
            Assert.That(first.GetComponent<Animation>().clip, Is.SameAs(second.GetComponent<Animation>().clip));
            Sample(first, 1);
            Quaternion firstPose = first.transform.Find("Swing").localRotation;
            Sample(second, 3);
            Assert.That(Quaternion.Angle(firstPose, first.transform.Find("Swing").localRotation), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(firstPose, second.transform.Find("Swing").localRotation), Is.GreaterThan(5.9f));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(first),
                Is.SameAs(PrefabUtility.GetCorrespondingObjectFromSource(second)));
        }

        [Test]
        public void CleanRoofPlateMatchesThePreservedReferenceFile()
        {
            string reference = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../Docs/Reference/Art/HauntedMerchantRegisteredRoofPlate.png"));
            using (SHA256 hash = SHA256.Create())
                Assert.That(hash.ComputeHash(File.ReadAllBytes(CanopyMotionTests.ArtPath + "MerchantRegisteredRoofPlate.png")),
                    Is.EqualTo(hash.ComputeHash(File.ReadAllBytes(reference))),
                    "The imported clean roof plate must be an exact copy of the preserved reference PNG.");
        }

        [TestCase(540, 960, 1f)]
        [TestCase(720, 960, .8f)]
        [TestCase(540, 1200, 1.1f)]
        [TestCase(960, 540, .5f)]
        [TestCase(960, 960, 1f)]
        [TestCase(941, 1672, 1672f / 960)]
        public void PreviewSceneRendersBothExtremaWithLinkedPrefabAndCompleteReferences(int width, int height, float scale)
        {
            EditorSceneManager.ClosePreviewScene(preview);
            preview = EditorSceneManager.OpenPreviewScene(ScenePath);
            GameObject[] roots = preview.GetRootGameObjects();
            foreach (GameObject root in roots)
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero);
            var canvas = roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).Single();
            var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single();
            Animation[] animations = roots.SelectMany(root => root.GetComponentsInChildren<Animation>(true)).ToArray();
            var animation = animations.Single(component =>
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(component.gameObject) == CanopyPrefabPath);
            Animation cup = animations.Single(component =>
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(component.gameObject) == PrefabPath);
            Assert.That(cup.gameObject.activeSelf, Is.False,
                "The original scene already contains its cookware and must not render the extra cup instance.");
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(canvas.worldCamera, Is.SameAs(camera));
            Assert.That(canvas.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(540, 960)));
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(animation.gameObject), Is.EqualTo(CanopyPrefabPath));
            RawImage[] images = canvas.GetComponentsInChildren<RawImage>(true);
            Assert.That(images.All(image => image.texture != null && !image.raycastTarget), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(animation.transform.parent.GetComponent<RawImage>().texture),
                Is.EqualTo(CanopyMotionTests.ArtPath + "Props/MerchantAmbientUnlitPropsRemoved.png"));
            Texture plateTexture = animation.transform.parent.GetComponent<RawImage>().texture;
            Assert.That(new Vector2Int(plateTexture.width, plateTexture.height), Is.EqualTo(new Vector2Int(941, 1672)));
            var merchant = (RectTransform)animation.transform.parent.Find("Merchant");
            var candle = (RectTransform)animation.transform.parent.Find("Protective Candle");
            foreach (var layer in new[] { (Group: merchant, Filename: "MerchantHost.png", Count: 5),
                (Group: candle, Filename: "ProtectiveCandle.png", Count: 2) })
            {
                string path = CanopyMotionTests.ArtPath + layer.Filename;
                Assert.That(layer.Group, Is.Not.Null);
                Assert.That(layer.Group.GetComponent<RawImage>(), Is.Null);
                Assert.That(layer.Group.parent, Is.SameAs(animation.transform.parent));
                Assert.That(layer.Group.gameObject.activeSelf, Is.True);
                RawImage[] parts = layer.Group.GetComponentsInChildren<RawImage>();
                Assert.That(parts.Length, Is.EqualTo(layer.Count));
                Assert.That(parts.Select(part => part.name), Is.EquivalentTo(layer.Group == merchant
                    ? new[] { "Torso", "Head", "Blink", "Supporting Arm", "Resting Arm" }
                    : new[] { "Body", "Flame" }));
                foreach (RawImage part in parts)
                {
                    string expectedPart = layer.Group != merchant ? path
                        : part.name == "Head" ? CanopyMotionTests.ArtPath + "MerchantHeadRepaired.png"
                        : part.name == "Torso" ? CanopyMotionTests.ArtPath + "MerchantTorsoAnimation.png"
                        : part.name == "Blink" ? CanopyMotionTests.ArtPath + "MerchantBlinkEyes.png" : path;
                    Assert.That(AssetDatabase.GetAssetPath(part.texture), Is.EqualTo(expectedPart));
                    Assert.That(part.transform.IsChildOf(layer.Group), Is.True);
                    Assert.That(part.gameObject.activeSelf && part.enabled, Is.True);
                    if (layer.Group == merchant) Assert.That(part, Is.TypeOf<SpiritPortraitImage>());
                }
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.alphaIsTransparency, Is.True);
                Assert.That(importer.DoesSourceTextureHaveAlpha(), Is.True);
            }
            Assert.That(Vector2.Distance(merchant.anchoredPosition, new Vector2(12.051010f, -16.535885f)), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(merchant.sizeDelta, new Vector2(493.344f, 902.4f)), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(candle.anchoredPosition, new Vector2(49.581296f, -507.559809f)), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(candle.sizeDelta, new Vector2(75.364718f, 118.593301f)), Is.LessThan(.001f));
            RawImage fixedCanopy = images.Single(image => image.name == "Fixed Canopy");
            Assert.That(fixedCanopy.gameObject.activeSelf, Is.False,
                "The clean plate must remain visible when the animated canopy is hidden.");
            Assert.That(AssetDatabase.GetAssetPath(fixedCanopy.texture), Is.EqualTo(CanopyMotionTests.ArtPath + "MerchantOriginal.png"));
            Material movingMaterial = animation.GetComponentInChildren<SpiritPortraitImage>().material;
            Assert.That(fixedCanopy.material, Is.SameAs(AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Dice/Content/Presentation/Ambient/CanopyRoofSource.mat")));
            Assert.That(fixedCanopy.material.shader, Is.SameAs(movingMaterial.shader));
            Assert.That(fixedCanopy.material.GetTexture("_SourceTex"), Is.SameAs(movingMaterial.GetTexture("_SourceTex")));
            Assert.That(fixedCanopy.material.GetTexture("_LayerMask"), Is.SameAs(movingMaterial.GetTexture("_LayerMask")));
            Assert.That(fixedCanopy.material.GetFloat("_InvertMask"), Is.EqualTo(1));
            Assert.That(movingMaterial.GetFloat("_InvertMask"), Is.EqualTo(0));
            Assert.That(fixedCanopy.transform.parent, Is.SameAs(animation.transform.parent));
            Assert.That(fixedCanopy.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(fixedCanopy.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(Quaternion.Angle(fixedCanopy.transform.localRotation, Quaternion.identity), Is.LessThan(.001f));
            Assert.That(fixedCanopy.rectTransform.rect.size, Is.EqualTo(new Vector2(540, 960)));
            Assert.That(fixedCanopy.uvRect, Is.EqualTo(new Rect(0, 0, 1, 1)));
            Assert.That(fixedCanopy.GetComponentsInChildren<Animation>(), Is.Empty);
            Assert.That(EditorBuildSettings.scenes.Any(scene => scene.path == ScenePath), Is.False);
            string output = Environment.GetEnvironmentVariable("DICE_AMBIENT_PREVIEW_OUTPUT");
            if (!string.IsNullOrWhiteSpace(output))
            {
                output = Path.Combine(output, $"{width}x{height}");
                Assert.That(Directory.Exists(output), Is.False, "Capture output must be a new directory.");
                Directory.CreateDirectory(output);
            }
            var restCapture = RenderPose(camera, canvas, animation.gameObject, 0, output, "background-only.png", width, height, scale,
                hiddenCanopy: true, backgroundOnly: true, lanternIntensity: 0, candleIntensity: 0);
            var sourceCapture = RenderPose(camera, canvas, animation.gameObject, 0, output, "clean-plate-reference.png", width, height, scale,
                originalSource: true);
            ReportRestComparison(restCapture.Pixels, sourceCapture.Pixels, width, height);
            var firstCapture = RenderPose(camera, canvas, animation.gameObject, 0, output, "cloth-rest.png", width, height, scale);
            var splitCapture = RenderPose(camera, canvas, animation.gameObject, 0, output, "geometry-split.png",
                width, height, scale, useLighting: false, repairExclusion: true);
            var unsplitCapture = RenderPose(camera, canvas, animation.gameObject, 0, output, "unsplit-reference.png",
                width, height, scale, unsplitParts: true);
            var maskedReference = RenderPose(camera, canvas, animation.gameObject, 0, output, "unsplit-masked-shader-reference.png",
                width, height, scale, unsplitParts: true, maskedReference: true);
            foreach (RawImage layer in merchant.GetComponentsInChildren<RawImage>().Concat(candle.GetComponentsInChildren<RawImage>()))
            {
                string filename = layer.name.ToLowerInvariant().Replace(' ', '-') + "-hidden.png";
                var hiddenCapture = RenderPose(camera, canvas, animation.gameObject, 0, output, filename, width, height, scale,
                    hiddenLayer: layer);
                bool changed = hiddenCapture.Pixels.Where((pixel, index) => !pixel.Equals(firstCapture.Pixels[index])).Any();
                // The closed-eye overlay is transparent at rest and appears only during a blink.
                if (layer.color.a == 0) Assert.That(changed, Is.False, $"{layer.name} must be invisible at rest.");
                else Assert.That(changed, Is.True, $"Hiding {layer.name} must independently change the assembled scene.");
            }
            var lightsOff = RenderPose(camera, canvas, animation.gameObject, 0, output, "lights-off.png",
                width, height, scale, lanternIntensity: 0, candleIntensity: 0);
            var lanternOnly = RenderPose(camera, canvas, animation.gameObject, 0, output, "lantern-only.png",
                width, height, scale, lanternIntensity: 1, candleIntensity: 0);
            var candleOnly = RenderPose(camera, canvas, animation.gameObject, 0, output, "candle-only.png",
                width, height, scale, lanternIntensity: 0, candleIntensity: 1);
            Assert.That(lanternOnly.Pixels, Is.Not.EqualTo(lightsOff.Pixels), "The lantern must illuminate the rendered scene.");
            Assert.That(candleOnly.Pixels, Is.Not.EqualTo(lightsOff.Pixels), "The candle must illuminate the rendered scene.");
            Assert.That(firstCapture.Pixels, Is.Not.EqualTo(lanternOnly.Pixels), "The candle must contribute independently alongside the lantern.");
            Assert.That(firstCapture.Pixels, Is.Not.EqualTo(candleOnly.Pixels), "The lantern must contribute independently alongside the candle.");
            var lanternBackground = RenderPose(camera, canvas, animation.gameObject, 0, output, "lantern-background.png",
                width, height, scale, hiddenCanopy: true, backgroundOnly: true, lanternIntensity: 1, candleIntensity: 0);
            Vector2 candlePosition = candle.anchoredPosition;
            Color32[] firstCandleIllumination = null;
            SceneLightingPresentation lighting = canvas.GetComponentInChildren<SceneLightingPresentation>(true);
            lighting.ApplyAtTime(0);
            Vector2 firstCandleUv = lighting.State.GetUvOffset(1);
            Vector2 firstLanternUv = lighting.State.GetUvOffset(0);
            Assert.That(Vector2.Distance(firstCandleUv, lighting.State.Asset.Lights[1].UvOffset), Is.LessThan(.00001f),
                "The candle's original position must retain its authored light offset.");
            try
            {
                for (int position = 0; position < 5; ++position)
                {
                    candle.anchoredPosition = candlePosition + new Vector2(position * 160f * 540 / 941, 0);
                    RenderPose(camera, canvas, animation.gameObject, 0, output, $"candle-position-{position}.png",
                        width, height, scale);
                    Assert.That(Vector2.Distance(lighting.State.GetUvOffset(1), firstCandleUv + new Vector2(position * 160f / 941, 0)),
                        Is.LessThan(.00001f), "The shared candle light must move by the source's normalized image displacement.");
                    Assert.That(lighting.State.GetUvOffset(0), Is.EqualTo(firstLanternUv),
                        "The lantern's sampled position must remain independent of candle movement.");
                    var illumination = RenderPose(camera, canvas, animation.gameObject, 0, output,
                        $"candle-illumination-position-{position}.png", width, height, scale,
                        hiddenCanopy: true, backgroundOnly: true, lanternIntensity: 0, candleIntensity: 1);
                    Assert.That(illumination.Pixels, Is.Not.EqualTo(restCapture.Pixels),
                        $"The candle must illuminate the background at table preview position {position}.");
                    if (position == 0) firstCandleIllumination = illumination.Pixels;
                    else Assert.That(illumination.Pixels, Is.Not.EqualTo(firstCandleIllumination),
                        $"Candle illumination must follow its source at table preview position {position}.");
                    var fixedLantern = RenderPose(camera, canvas, animation.gameObject, 0, output,
                        $"lantern-candle-position-{position}.png", width, height, scale,
                        hiddenCanopy: true, backgroundOnly: true, lanternIntensity: 1, candleIntensity: 0);
                    Assert.That(fixedLantern.Pixels, Is.EqualTo(lanternBackground.Pixels),
                        "Moving the candle must preserve independently sampled lantern illumination.");
                }
            }
            finally
            {
                candle.anchoredPosition = candlePosition;
                Canvas.ForceUpdateCanvases();
            }
            if (width == 941 && height == 1672)
            {
                RawImage head = merchant.GetComponentsInChildren<RawImage>().Single(image => image.name == "Head");
                RawImage supportingArm = merchant.GetComponentsInChildren<RawImage>().Single(image => image.name == "Supporting Arm");
                RenderPose(camera, canvas, animation.gameObject, 0, output, "head-only.png", width, height, scale,
                    useLighting: false, onlyLayer: head);
                RenderPose(camera, canvas, animation.gameObject, 0, output, "supporting-arm-only.png", width, height, scale,
                    useLighting: false, onlyLayer: supportingArm);
                Vector3 armPosition = supportingArm.rectTransform.anchoredPosition3D;
                try
                {
                    supportingArm.rectTransform.position += merchant.TransformVector(new Vector3(-40, -35, 0));
                    RenderPose(camera, canvas, animation.gameObject, 0, output, "supporting-arm-displaced.png", width, height, scale,
                        useLighting: false);
                }
                finally
                {
                    supportingArm.rectTransform.anchoredPosition3D = armPosition;
                    Canvas.ForceUpdateCanvases();
                }
                var originalCapture = RenderPose(camera, canvas, animation.gameObject, 0, output, "original-unmodified.png",
                    width, height, scale, referenceTexture: "MerchantOriginal.png");
                ReportRestComparison(firstCapture.Pixels, originalCapture.Pixels, width, height, requireExact: false);
            }
            var secondCapture = RenderPose(camera, canvas, animation.gameObject, 15.68f, output, "cloth-gust.png", width, height, scale);
            Color32[] first = firstCapture.Pixels;
            Color32[] second = secondCapture.Pixels;
            Assert.That(secondCapture.Roof, Is.EqualTo(firstCapture.Roof),
                "The supported roof and underside must remain static while the loose pieces move.");
            var gustCapture = RenderPose(camera, canvas, animation.gameObject, 15.42f, output, "gust.png", width, height, scale);
            Assert.That(gustCapture.Roof, Is.EqualTo(firstCapture.Roof),
                "The strongest gust must not deform the roof or underside.");
            int different = 0;
            int visible = 0;
            for (int i = 0; i < first.Length; ++i)
            {
                if (!first[i].Equals(second[i])) ++different;
                if (first[i].r > 15 || first[i].g > 15 || first[i].b > 15) ++visible;
            }
            Assert.That(visible, Is.GreaterThan(width * height / 10), "The portrait plate must render.");
            Assert.That(different, Is.GreaterThan(50), "The original overhang must produce visible motion.");
            if ((width == 540 && height == 960 || width == 941 && height == 1672) && !string.IsNullOrWhiteSpace(output)
                && Environment.GetEnvironmentVariable("DICE_AMBIENT_MOTION_CAPTURE") == "1")
            {
                string motionOutput = Path.Combine(output, "motion");
                Directory.CreateDirectory(motionOutput);
                for (int frame = 0; frame < 240; ++frame)
                    RenderPose(camera, canvas, animation.gameObject, 10 + frame / 20f, motionOutput,
                        $"frame-{frame:D4}.png", width, height, scale);
            }
            ReportSplitComparison(unsplitCapture.Pixels, maskedReference.Pixels, width, height,
                comparison: "Default UI/white-mask shader on identical source frame");
            // Independent pivots and grid tessellation shift GPU sampling at alpha quantization boundaries.
            // Every merchant layer, including the torso, is tessellated by the shared breathing grid. The minified
            // 960x540 capture measures four levels at two pixels on the suspender's dark edge inside the torso;
            // all other sizes remain within three.
            ReportSplitComparison(splitCapture.Pixels, maskedReference.Pixels, width, height, 4,
                "Split parts/white-mask source outside declared anatomy repairs and filtered cut edges", splitCapture.RepairExclusion);
            ReportSplitComparison(splitCapture.Pixels, unsplitCapture.Pixels, width, height, 4,
                "Split parts/default UI source outside declared anatomy repairs and filtered cut edges", splitCapture.RepairExclusion);
        }

        private GameObject InstantiateCup()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(asset, Is.Not.Null);
            return (GameObject)PrefabUtility.InstantiatePrefab(asset, preview);
        }

        private static void Sample(GameObject cup, float time)
        {
            var animation = cup.GetComponent<Animation>();
            Assert.That(animation.Play(), Is.True);
            AnimationState state = animation[animation.clip.name];
            Assert.That(state, Is.Not.Null);
            state.time = time;
            state.weight = 1;
            animation.Sample();
        }

        private static (Color32[] Pixels, Color32[] Roof, bool[] RepairExclusion) RenderPose(Camera camera, Canvas canvas, GameObject cup, float time,
            string output, string filename, int width, int height, float scale, bool originalSource = false, bool hiddenCanopy = false,
            bool backgroundOnly = false, RawImage hiddenLayer = null, string referenceTexture = null, bool unsplitParts = false,
            bool useLighting = true, float lanternIntensity = 1, float candleIntensity = 1, bool maskedReference = false,
            RawImage onlyLayer = null, bool repairExclusion = false)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var plateImage = cup.transform.parent.GetComponent<RawImage>();
            Texture plateTexture = plateImage.texture;
            var cloth = cup.GetComponentsInChildren<SpiritPortraitImage>(true);
            RawImage[] overlays = canvas.GetComponentsInChildren<RawImage>(true).Where(image => image != plateImage).ToArray();
            bool[] overlayEnabled = overlays.Select(image => image.enabled).ToArray();
            bool canopyActive = cup.activeSelf;
            var references = new System.Collections.Generic.List<RawImage>();
            var referenceMaterials = new System.Collections.Generic.List<Material>();
            SceneLightingPresentation lighting = canvas.GetComponentInChildren<SceneLightingPresentation>(true);
            Assert.That(lighting, Is.Not.Null, "The scene must use shared scene lighting.");
            bool lightingEnabled = lighting.enabled;
            float originalLanternIntensity = lighting.GetLightIntensity(0);
            float originalCandleIntensity = lighting.GetLightIntensity(1);
            Material referenceMaterial = null;
            bool changedReferenceMaterial = false;
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                camera.scene = cup.scene;
                // Exercise different canvas sizes and scales without depending on the editor Game view.
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.scaleFactor = scale;
                Canvas.ForceUpdateCanvases();
                lighting.enabled = useLighting && !unsplitParts && !originalSource && referenceTexture == null;
                if (lighting.enabled)
                {
                    lighting.SetLightIntensity(0, lanternIntensity);
                    lighting.SetLightIntensity(1, candleIntensity);
                    lighting.ApplyAtTime(0);
                }
                if (hiddenCanopy) cup.SetActive(false);
                foreach (Animation animation in canvas.GetComponentsInChildren<Animation>())
                    Sample(animation.gameObject, time);
                if (backgroundOnly)
                    foreach (RawImage image in overlays) image.enabled = false;
                if (hiddenLayer != null) hiddenLayer.enabled = false;
                if (onlyLayer != null)
                    foreach (RawImage image in overlays) image.enabled = image == onlyLayer;
                if (unsplitParts)
                {
                    foreach (var layer in new[] { (Name: "Merchant", Filename: "MerchantHost.png"),
                        (Name: "Protective Candle", Filename: "ProtectiveCandle.png") })
                    {
                        var group = (RectTransform)cup.transform.parent.Find(layer.Name);
                        RawImage[] parts = group.GetComponentsInChildren<RawImage>();
                        foreach (RawImage part in parts) part.enabled = false;
                        var referenceObject = new GameObject("Unsplit Source Reference", typeof(RectTransform), typeof(RawImage));
                        var reference = referenceObject.GetComponent<RawImage>();
                        RectTransform sourceFrame = parts[0].rectTransform;
                        reference.rectTransform.SetParent(sourceFrame.parent, false);
                        reference.rectTransform.anchorMin = sourceFrame.anchorMin;
                        reference.rectTransform.anchorMax = sourceFrame.anchorMax;
                        reference.rectTransform.pivot = sourceFrame.pivot;
                        reference.rectTransform.sizeDelta = sourceFrame.sizeDelta;
                        reference.rectTransform.anchoredPosition3D = sourceFrame.anchoredPosition3D;
                        reference.rectTransform.localRotation = sourceFrame.localRotation;
                        reference.rectTransform.localScale = sourceFrame.localScale;
                        reference.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CanopyMotionTests.ArtPath + layer.Filename);
                        reference.material = null;
                        reference.raycastTarget = false;
                        if (maskedReference)
                        {
                            var material = new Material(parts[0].material) { hideFlags = HideFlags.HideAndDontSave };
                            material.SetTexture("_LayerMask", Texture2D.whiteTexture);
                            material.SetTexture("_SourceTex", reference.texture);
                            material.SetFloat("_InvertMask", 0);
                            reference.material = material;
                            referenceMaterials.Add(material);
                        }
                        references.Add(reference);
                    }
                }
                if (originalSource || referenceTexture != null)
                {
                    referenceMaterial = plateImage.material;
                    changedReferenceMaterial = true;
                    plateImage.material = null;
                    plateImage.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CanopyMotionTests.ArtPath +
                        (referenceTexture ?? "Props/MerchantAmbientUnlitPropsRemoved.png"));
                    Assert.That(plateImage.texture, Is.Not.Null);
                    foreach (RawImage image in overlays) image.enabled = false;
                }
                else if (!hiddenCanopy && time == 0)
                    Assert.That(cloth.SelectMany(image => image.Weights).All(weight => weight == 0), Is.True,
                        "The source registration capture requires every cloth channel at rest.");
                Canvas.ForceUpdateCanvases();
                camera.Render();
                var plate = cup.transform.parent.GetComponent<RawImage>().rectTransform;
                var canopy = (RectTransform)cup.transform;
                Vector3 canopyOrigin = camera.WorldToScreenPoint(canopy.TransformPoint(Vector3.zero));
                Vector3 plateOrigin = camera.WorldToScreenPoint(plate.TransformPoint(Vector3.zero));
                Assert.That(Vector2.Distance(canopyOrigin, plateOrigin), Is.LessThan(.1f));
                Assert.That(canopy.rect.size, Is.EqualTo(plate.rect.size),
                    "The loose cloth's authoring frame must retain the background artwork's full-canvas registration.");
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                if (!string.IsNullOrWhiteSpace(output))
                    File.WriteAllBytes(Path.Combine(output, filename), pixels.EncodeToPNG());
                Color32[] rendered = pixels.GetPixels32();
                var roof = new System.Collections.Generic.List<Color32>();
                foreach (Vector2 point in new[] { new Vector2(250, 150), new Vector2(450, 215),
                    new Vector2(235, 295), new Vector2(300, 310) })
                {
                    for (int y = -4; y <= 4; ++y)
                        for (int x = -4; x <= 4; ++x)
                        {
                            Vector3 local = new Vector3(plate.rect.xMin + (point.x + x) * plate.rect.width / 941,
                                plate.rect.yMax - (point.y + y) * plate.rect.height / 1672, 0);
                            Vector3 screen = camera.WorldToScreenPoint(plate.TransformPoint(local));
                            int column = Mathf.RoundToInt(screen.x);
                            int row = Mathf.RoundToInt(screen.y);
                            Assert.That(column, Is.InRange(0, width - 1));
                            Assert.That(row, Is.InRange(0, height - 1));
                            roof.Add(rendered[row * width + column]);
                        }
                }
                bool[] excluded = repairExclusion ? ProjectHeadRepair(camera, canvas, width, height) : null;
                if (excluded != null && !string.IsNullOrWhiteSpace(output))
                {
                    pixels.SetPixels32(excluded.Select(selected => selected
                        ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 255)).ToArray());
                    pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output, "anatomy-repair-comparison-exclusion.png"), pixels.EncodeToPNG());
                }
                return (rendered, roof.ToArray(), excluded);
            }
            finally
            {
                foreach (RawImage reference in references) UnityEngine.Object.DestroyImmediate(reference.gameObject);
                foreach (Material material in referenceMaterials) UnityEngine.Object.DestroyImmediate(material);
                plateImage.texture = plateTexture;
                if (changedReferenceMaterial) plateImage.material = referenceMaterial;
                for (int i = 0; i < overlays.Length; ++i) overlays[i].enabled = overlayEnabled[i];
                cup.SetActive(canopyActive);
                lighting.SetLightIntensity(0, originalLanternIntensity);
                lighting.SetLightIntensity(1, originalCandleIntensity);
                lighting.enabled = lightingEnabled;
                if (lightingEnabled) lighting.ApplyAtTime(0);
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static bool[] ProjectHeadRepair(Camera camera, Canvas canvas, int width, int height)
        {
            var repair = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Assert.That(repair.LoadImage(File.ReadAllBytes(CanopyMotionTests.ArtPath + "MerchantHeadRepairMask.png")), Is.True);
                Color32[] source = repair.GetPixels32();
                AddMerchantOverlapRegions(source, repair.width, repair.height);
                var expanded = new bool[source.Length];
                // Bilinear source sampling can read a repaired neighbor one source texel away.
                for (int y = 0; y < repair.height; ++y)
                    for (int x = 0; x < repair.width; ++x)
                    {
                        if (source[y * repair.width + x].a == 0) continue;
                        for (int dy = -1; dy <= 1; ++dy)
                            for (int dx = -1; dx <= 1; ++dx)
                                if (x + dx >= 0 && x + dx < repair.width && y + dy >= 0 && y + dy < repair.height)
                                    expanded[(y + dy) * repair.width + x + dx] = true;
                    }
                RectTransform head = canvas.GetComponentsInChildren<RawImage>(true)
                    .Single(image => image.name == "Head").rectTransform;
                var corners = new Vector3[4];
                head.GetWorldCorners(corners);
                Vector2 origin = camera.WorldToScreenPoint(corners[0]);
                Vector2 horizontal = (Vector2)camera.WorldToScreenPoint(corners[3]) - origin;
                Vector2 vertical = (Vector2)camera.WorldToScreenPoint(corners[1]) - origin;
                float determinant = horizontal.x * vertical.y - horizontal.y * vertical.x;
                Assert.That(Mathf.Abs(determinant), Is.GreaterThan(.001f));
                var excluded = new bool[width * height];
                for (int y = 0; y < height; ++y)
                    for (int x = 0; x < width; ++x)
                    {
                        Vector2 delta = new Vector2(x + .5f, y + .5f) - origin;
                        float u = (delta.x * vertical.y - delta.y * vertical.x) / determinant;
                        float v = (horizontal.x * delta.y - horizontal.y * delta.x) / determinant;
                        int column = Mathf.FloorToInt(u * repair.width), row = Mathf.FloorToInt(v * repair.height);
                        if (column >= 0 && column < repair.width && row >= 0 && row < repair.height)
                            excluded[y * width + x] = expanded[row * repair.width + column];
                    }
                Assert.That(excluded.Any(selected => selected), Is.True, "The declared repair must project into the captured merchant.");
                return excluded;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(repair);
            }
        }

        private static void AddMerchantOverlapRegions(Color32[] selected, int width, int height)
        {
            foreach (string filename in new[] { "MerchantTorsoAnimationRepairMask.png", "MerchantHeadCompleteMask.png",
                "MerchantSupportingForearmAnimationMask.png" })
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                try
                {
                    Assert.That(texture.LoadImage(File.ReadAllBytes(CanopyMotionTests.ArtPath + filename)), Is.True);
                    Assert.That(new Vector2Int(texture.width, texture.height), Is.EqualTo(new Vector2Int(width, height)));
                    Color32[] mask = texture.GetPixels32();
                    bool backing = filename == "MerchantTorsoAnimationRepairMask.png";
                    for (int y = 1; y < height - 1; ++y)
                        for (int x = 1; x < width - 1; ++x)
                        {
                            int index = y * width + x;
                            // Reconstructed backing affects bilinear neighbors; filtered cuts change only their boundary band.
                            bool changed = backing ? mask[index].a != 0
                                : mask[index].a != mask[index - 1].a || mask[index].a != mask[index + 1].a
                                || mask[index].a != mask[index - width].a || mask[index].a != mask[index + width].a;
                            if (changed) selected[index] = new Color32(255, 255, 255, 255);
                        }
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
        }

        private static void ReportSplitComparison(Color32[] split, Color32[] unsplit, int width, int height,
            int maximumAllowedDifference = 0, string comparison = "Split/unsplit source", bool[] excluded = null)
        {
            int differingPixels = 0, maximumDifference = 0, excludedPixels = 0, excludedDifferences = 0;
            int maximumRepairDifference = 0;
            long totalDifference = 0;
            for (int index = 0; index < split.Length; ++index)
            {
                Color32 actual = split[index], expected = unsplit[index];
                int difference = Mathf.Max(Mathf.Abs(actual.r - expected.r),
                    Mathf.Max(Mathf.Abs(actual.g - expected.g), Mathf.Abs(actual.b - expected.b)));
                totalDifference += Mathf.Abs(actual.r - expected.r) + Mathf.Abs(actual.g - expected.g) + Mathf.Abs(actual.b - expected.b);
                if (excluded != null && excluded[index])
                {
                    ++excludedPixels;
                    if (difference != 0) ++excludedDifferences;
                    maximumRepairDifference = Mathf.Max(maximumRepairDifference, difference);
                    continue;
                }
                if (difference != 0) ++differingPixels;
                maximumDifference = Mathf.Max(maximumDifference, difference);
            }
            TestContext.WriteLine($"{comparison} comparison {width}x{height}: {differingPixels} differing compared RGB pixels, maximum compared channel difference {maximumDifference}, allowed maximum {maximumAllowedDifference}; {excludedPixels} declared repair pixels excluded, {excludedDifferences} differing excluded pixels, maximum repair difference {maximumRepairDifference}; full-frame mean absolute RGB difference {totalDifference / (3.0 * split.Length):F6}.");
            Assert.That(maximumDifference, Is.LessThanOrEqualTo(maximumAllowedDifference),
                $"{comparison} must preserve rendered source RGB within {maximumAllowedDifference} channel levels.");
        }

        private static void ReportRestComparison(Color32[] rest, Color32[] original, int width, int height, bool requireExact = true)
        {
            int differences = 0, overTwo = 0, significantDifferences = 0, maximumDifference = 0;
            long totalDifference = 0;
            for (int index = 0; index < rest.Length; ++index)
            {
                Color32 actual = rest[index], expected = original[index];
                int difference = Mathf.Max(Mathf.Abs(actual.r - expected.r),
                    Mathf.Max(Mathf.Abs(actual.g - expected.g), Mathf.Abs(actual.b - expected.b)));
                if (!actual.Equals(expected)) ++differences;
                if (difference > 2) ++overTwo;
                if (difference > 8) ++significantDifferences;
                maximumDifference = Mathf.Max(maximumDifference, difference);
                totalDifference += Mathf.Abs(actual.r - expected.r) + Mathf.Abs(actual.g - expected.g) + Mathf.Abs(actual.b - expected.b);
            }
            string comparison = requireExact ? "Background-only/clean plate" : "Complete composite/unmodified original";
            TestContext.WriteLine($"{comparison} comparison {width}x{height}: {differences} differing pixels, {overTwo} over two RGB units, {significantDifferences} over eight RGB units, maximum channel difference {maximumDifference}, mean absolute RGB error {totalDifference / (rest.Length * 3.0):F4}.");
            if (requireExact)
                Assert.That(differences, Is.Zero,
                    "Hiding the canopy, merchant and candle must reveal only the clean background plate.");
        }
    }
}
