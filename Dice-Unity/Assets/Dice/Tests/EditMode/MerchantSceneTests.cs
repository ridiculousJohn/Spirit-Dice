using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dice.Presentation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    /** The haunted merchant's scene: its canopy, art preview, movable props, hanging cup and their sounds. */
    [Explicit("Art-generation check; run it by name while the merchant art is being changed.")]
    [Category("ArtAuthoring")]
    public sealed class MerchantSceneTests
    {
        internal const string ArtPath = "Assets/Dice/Art/HauntedMerchant/";
        private const string PropsFolder = ArtPath + "Props/";
        private const string TablePath = "Assets/Dice/Content/Presentation/HauntedMerchant/MerchantUnquietCartTable.asset";
        private const string CanopyPrefabPath = "Assets/Dice/Content/Presentation/Ambient/Canopy.prefab";
        private const string CupPrefabPath = "Assets/Dice/Content/Presentation/Ambient/HangingCup.prefab";
        private const string PreviewScenePath = "Assets/Scenes/AmbientMotionPreview.unity";
        // Height of the 540 x 960 authoring frame the plates fill, in the props' local units.
        private const float FrameHeight = 960f;
        private static readonly Vector2[] AttachmentSourcePoints =
        {
            new Vector2(230, 205), new Vector2(350, 230), new Vector2(430, 225),
            new Vector2(520, 260), new Vector2(580, 350),
            new Vector2(450, 330), new Vector2(475, 325),
            new Vector2(429, 175), new Vector2(478, 194),
            new Vector2(525, 207), new Vector2(574, 219)
        };
        private static readonly (string Id, string Name)[] PropActors =
        {
            ("hanging-pot", "HangingPot"), ("hanging-pan", "HangingPan"), ("shelf-bottle", "ShelfBottle"), ("trunk-lid", "TrunkLid"),
        };
        private readonly List<Object> created = new List<Object>();
        private Scene preview;
        private RectTransform canvas;

        [SetUp]
        public void SetUp()
        {
            preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Merchant Test Canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, preview);
            canvas = (RectTransform)root.transform;
            canvas.sizeDelta = new Vector2(540, 960);
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        }

        [TearDown]
        public void TearDown()
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            foreach (var value in created)
                if (value != null) Object.DestroyImmediate(value);
            created.Clear();
        }

        // Canopy

        [Test]
        public void OverhangPreservesOriginalPixelsAndRoofPlateChangesOnlyTheSelectedCloth()
        {
            Texture2D source = DecodePng(ArtPath + "MerchantOriginal.png");
            Texture2D fringe = DecodePng(ArtPath + "CanopyOriginalOverhang.png");
            Texture2D plate = DecodePng(ArtPath + "MerchantOriginalRoofPlate.png");
            Texture2D mask = DecodePng(ArtPath + "CanopyOriginalOverhangMask.png");
            try
            {
                var sourceSize = new Vector2Int(source.width, source.height);
                Assert.That(new Vector2Int(fringe.width, fringe.height), Is.EqualTo(sourceSize));
                Assert.That(new Vector2Int(plate.width, plate.height), Is.EqualTo(sourceSize));
                Assert.That(new Vector2Int(mask.width, mask.height), Is.EqualTo(sourceSize));
                Color32[] original = source.GetPixels32();
                Color32[] moving = fringe.GetPixels32();
                Color32[] fixedPixels = plate.GetPixels32();
                Color32[] selection = mask.GetPixels32();
                int clothPixels = 0, mismatches = 0, firstMismatch = -1, alphaErrors = 0, invalidMaskPixels = 0;
                int outsidePlateErrors = 0, hiddenPlateChanges = 0;
                for (int index = 0; index < original.Length; ++index)
                {
                    Color32 pixel = moving[index], fixedPixel = fixedPixels[index], originalPixel = original[index];
                    if (pixel.a != 0) ++clothPixels;
                    if (pixel.r != originalPixel.r || pixel.g != originalPixel.g || pixel.b != originalPixel.b
                        || (pixel.a != 0 && pixel.a != originalPixel.a))
                    {
                        if (firstMismatch < 0) firstMismatch = index;
                        ++mismatches;
                    }
                    byte selected = selection[index].a;
                    if (selected != 0 && selected != 255) ++invalidMaskPixels;
                    byte expectedMoving = selected == 255 ? originalPixel.a : (byte)0;
                    if (pixel.a != expectedMoving) ++alphaErrors;
                    if (!fixedPixel.Equals(originalPixel))
                    {
                        if (selected == 0) ++outsidePlateErrors;
                        else ++hiddenPlateChanges;
                    }
                }
                Assert.That(clothPixels, Is.GreaterThan(0));
                Assert.That(clothPixels, Is.LessThan(original.Count(pixel => pixel.a != 0)),
                    "The scene outside the hanging cloth must remain fixed.");
                Assert.That(mismatches, Is.Zero,
                    $"The overhang must retain original source RGB and selected source alpha at the same coordinates; first mismatch index {firstMismatch}.");
                Assert.That(invalidMaskPixels, Is.Zero, "Every ownership mask alpha must be either zero or 255.");
                Assert.That(alphaErrors, Is.Zero, "The ownership mask must select original source alpha for the overhang.");
                Assert.That(outsidePlateErrors, Is.Zero,
                    "The roof plate must preserve every original pixel outside the selected cloth.");
                Assert.That(hiddenPlateChanges, Is.GreaterThan(0), "The roof plate must provide hidden fill beneath the cloth.");
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(fringe);
                Object.DestroyImmediate(plate);
                Object.DestroyImmediate(mask);
            }
        }

        [TestCase(450, 330)]
        [TestCase(475, 325)]
        [TestCase(500, 315)]
        [TestCase(535, 302)]
        [TestCase(429, 175)]
        [TestCase(478, 194)]
        [TestCase(525, 207)]
        [TestCase(574, 219)]
        public void FrontAndRearAttachmentPixelsRemainInTheOriginalClothMask(int x, int y)
        {
            Texture2D mask = DecodePng(ArtPath + "CanopyOriginalOverhangMask.png");
            try
            {
                Assert.That(mask.GetPixel(x, mask.height - 1 - y).a, Is.EqualTo(1),
                    $"The cloth mask must include the original attachment pixel at {x}, {y}.");
            }
            finally
            {
                Object.DestroyImmediate(mask);
            }
        }

        [Test]
        public void SupportedRoofAndUndersideStayFixedAcrossTheLoop()
        {
            GameObject canopy = InstantiateCanopy();
            var images = canopy.GetComponentsInChildren<SpiritPortraitImage>();
            Assert.That(images, Is.Not.Empty);
            AnimationClip clip = canopy.GetComponent<Animation>().clip;
            var root = (RectTransform)canopy.transform;
            root.anchoredPosition = new Vector2(37, -53);
            Vector3 fixedRoot = root.position;
            for (float time = 0; time <= clip.length; time += .25f)
            {
                SampleCanopy(canopy, time);
                Assert.That(root.position, Is.EqualTo(fixedRoot));
                foreach (SpiritPortraitImage image in images)
                    foreach (Vector2 point in AttachmentSourcePoints)
                        Assert.That(image.EvaluateDisplacement(new Vector2(point.x / image.texture.width, 1 - point.y / image.texture.height),
                            image.rectTransform.rect.size).magnitude, Is.LessThan(.00001f),
                            $"Supported canopy moved at {time} seconds.");
            }
        }

        [Test]
        public void CanopyFringeMovesAndReturnsToItsRestShapeAcrossTheLoop()
        {
            GameObject canopy = InstantiateCanopy();
            var images = canopy.GetComponentsInChildren<SpiritPortraitImage>();
            AnimationClip clip = canopy.GetComponent<Animation>().clip;
            SampleCanopy(canopy, 0);
            Vector3[][] first = images.Select(Vertices).ToArray();
            float[] maximum = new float[images.Length];
            for (float time = .05f; time < clip.length; time += .05f)
            {
                SampleCanopy(canopy, time);
                for (int i = 0; i < images.Length; ++i)
                    maximum[i] = Mathf.Max(maximum[i], MaxDistance(first[i], Vertices(images[i])));
            }
            SampleCanopy(canopy, clip.length);
            for (int i = 0; i < images.Length; ++i)
            {
                Assert.That(maximum[i], Is.GreaterThan(0f), images[i].name + " must move.");
                Assert.That(MaxDistance(first[i], Vertices(images[i])), Is.LessThan(.001f),
                    images[i].name + " must return to its rest shape at the loop's end.");
            }
        }

        [Test]
        public void AuthoredMotionHasQuietIntervalsShortBurstsAndDistinctChannelTiming()
        {
            AnimationClip clip = InstantiateCanopy().GetComponent<Animation>().clip;
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            AnimationCurve[] curves = bindings.Select(binding => AnimationUtility.GetEditorCurve(clip, binding)).ToArray();
            float[] peakSpeeds = new float[curves.Length];
            for (int i = 0; i < curves.Length; ++i)
            {
                AnimationCurve curve = curves[i];
                var speeds = new List<float>();
                for (float time = .05f; time < clip.length; time += .05f)
                    speeds.Add(Mathf.Abs(curve.Evaluate(time) - curve.Evaluate(time - .05f)) / .05f);
                peakSpeeds[i] = speeds.Max();
                Assert.That(peakSpeeds[i], Is.GreaterThan(0f), bindings[i].path + " must move.");
                // Quiet and busy are measured against the channel's own fastest motion.
                int quietSamples = speeds.Count(speed => speed < peakSpeeds[i] * .05f);
                int busySamples = speeds.Count(speed => speed > peakSpeeds[i] * .5f);
                Assert.That(busySamples, Is.GreaterThan(0), bindings[i].path + " needs bursts.");
                Assert.That(quietSamples, Is.GreaterThan(busySamples),
                    bindings[i].path + " must spend longer quiet than in its short bursts.");
                for (int other = 0; other < i; ++other)
                {
                    float difference = 0;
                    for (float time = 0; time < clip.length; time += .1f)
                        difference += Mathf.Abs(curve.Evaluate(time) - curves[other].Evaluate(time));
                    Assert.That(difference, Is.GreaterThan(0f), "The deformation channels must have distinct schedules.");
                }
            }
        }

        // Art preview

        [TestCase(540, 960, 1f)]
        [TestCase(720, 960, .8f)]
        [TestCase(540, 1200, 1.1f)]
        [TestCase(960, 540, .5f)]
        [TestCase(960, 960, 1f)]
        [TestCase(941, 1672, 1672f / 960)]
        public void MerchantPreviewRendersEveryLayerAndLightIndependently(int width, int height, float scale)
        {
            EditorSceneManager.ClosePreviewScene(preview);
            preview = EditorSceneManager.OpenPreviewScene(PreviewScenePath);
            GameObject[] roots = preview.GetRootGameObjects();
            foreach (GameObject root in roots)
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero);
            var canvas = roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).Single();
            var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single();
            var animation = roots.SelectMany(root => root.GetComponentsInChildren<Animation>(true)).Single(component =>
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(component.gameObject) == CanopyPrefabPath);
            RawImage[] images = canvas.GetComponentsInChildren<RawImage>(true);
            Assert.That(images.All(image => image.texture != null && !image.raycastTarget), Is.True,
                "Every layer draws its texture and none intercepts input.");
            var plateImage = animation.transform.parent.GetComponent<RawImage>();
            Texture plateTexture = plateImage.texture;
            var merchant = (RectTransform)animation.transform.parent.Find("Merchant");
            var candle = (RectTransform)animation.transform.parent.Find("Protective Candle");
            Assert.That(merchant, Is.Not.Null);
            Assert.That(candle, Is.Not.Null);
            string output = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (!string.IsNullOrWhiteSpace(output))
            {
                output = Path.Combine(output, "merchant-scene", $"{width}x{height}");
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
            // The candle moves by whole plate pixels, expressed in the plate's frame units.
            float frameUnitsPerPixel = plateImage.rectTransform.rect.width / plateTexture.width;
            try
            {
                for (int position = 0; position < 5; ++position)
                {
                    candle.anchoredPosition = candlePosition + new Vector2(position * 160f * frameUnitsPerPixel, 0);
                    RenderPose(camera, canvas, animation.gameObject, 0, output, $"candle-position-{position}.png",
                        width, height, scale);
                    Assert.That(Vector2.Distance(lighting.State.GetUvOffset(1), firstCandleUv + new Vector2(position * 160f / plateTexture.width, 0)),
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
            Assert.That(visible, Is.GreaterThan(0), "The portrait plate must render.");
            Assert.That(different, Is.GreaterThan(0), "The original overhang must produce visible motion.");
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

        // Hanging cup and movable props

        [Test]
        public void HangingCupRootSwingComposesWithItsPrefabsChildSway()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CupPrefabPath);
            var clip = prefab.GetComponent<Animation>().clip;
            Assert.That(clip, Is.Not.Null);
            var cup = Track(Object.Instantiate(prefab));
            var reference = Track(Object.Instantiate(prefab));
            var root = (RectTransform)cup.transform;
            root.anchoredPosition3D = new Vector3(30f, -50f, 0f);
            Vector3 restPosition = root.anchoredPosition3D;
            Quaternion restRotation = root.localRotation;
            Transform swing = root.Find("Swing"), referenceSwing = reference.transform.Find("Swing");
            Assert.That(swing, Is.Not.Null, "The prefab's idle sway animates its Swing child.");
            var motion = Swing(10f).Create(root);
            motion.Strike(.5, 1.5f);
            float peakRoot = 0f, peakChild = 0f;
            for (double time = 0; time < 15; time += EncounterAmbientActivityTests.Step)
            {
                // The prefab's clip sways the child every frame while the ambient motion swings the root.
                float clipTime = (float)(time % clip.length);
                clip.SampleAnimation(cup, clipTime);
                motion.Apply(time);
                clip.SampleAnimation(reference, clipTime);
                Assert.That(Quaternion.Angle(swing.localRotation, referenceSwing.localRotation), Is.LessThan(.0001f),
                    "The child keeps the clip's sway at " + time);
                Assert.That(Vector3.Distance(swing.localPosition, referenceSwing.localPosition), Is.LessThan(.0001f));
                peakRoot = Mathf.Max(peakRoot, Quaternion.Angle(root.localRotation, restRotation));
                peakChild = Mathf.Max(peakChild, Quaternion.Angle(swing.localRotation, Quaternion.identity));
            }
            Assert.That(peakRoot, Is.GreaterThan(0f), "The strike swings the cup's root.");
            Assert.That(peakChild, Is.GreaterThan(0f), "The clip sways the child.");
            Assert.That(motion.IsMoving || motion.IsOffset, Is.False);
            Assert.That(Vector3.Distance(root.anchoredPosition3D, restPosition), Is.LessThan(.0001f), "The root returns to rest.");
            Assert.That(Quaternion.Angle(root.localRotation, restRotation), Is.LessThan(.0001f));
            clip.SampleAnimation(cup, clip.length / 4f);
            clip.SampleAnimation(reference, clip.length / 4f);
            Assert.That(Quaternion.Angle(swing.localRotation, referenceSwing.localRotation), Is.LessThan(.0001f),
                "The child holds whatever pose the clip sets.");
        }

        [Test]
        public void MerchantSceneShowsTheHangingCupAtItsPreviewPlacementBoundToTheActivity()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            Vector2 previewPosition, previewParentSize, previewAnchor;
            var previewScene = EditorSceneManager.OpenPreviewScene(PreviewScenePath);
            try
            {
                var previewCup = (RectTransform)FindCup(previewScene.GetRootGameObjects()).transform;
                previewPosition = previewCup.anchoredPosition;
                previewAnchor = previewCup.anchorMin;
                previewParentSize = ((RectTransform)previewCup.parent).sizeDelta;
            }
            finally { EditorSceneManager.ClosePreviewScene(previewScene); }

            var scene = EditorSceneManager.OpenPreviewScene(AssetDatabase.GUIDToAssetPath(table.Scene.AssetGUID));
            try
            {
                var roots = scene.GetRootGameObjects();
                var view = roots.SelectMany(root => root.GetComponentsInChildren<SpiritEncounterView>(true)).Single();
                var cup = (RectTransform)AmbientBindings(view).Single(binding => binding.ActorId == "hanging-cup").Target;
                Assert.That(cup.gameObject, Is.SameAs(FindCup(roots)));
                Assert.That(PrefabUtility.GetOutermostPrefabInstanceRoot(cup.gameObject), Is.SameAs(cup.gameObject),
                    "The swing turns the cup's root about its pin, over the prefab's own sway.");
                Assert.That(cup.gameObject.activeSelf, Is.True);
                Assert.That(cup.IsChildOf(view.Portrait), Is.True);
                Assert.That(cup.anchorMin, Is.EqualTo(previewAnchor));
                Assert.That(cup.anchorMax, Is.EqualTo(previewAnchor));
                Assert.That(cup.anchoredPosition, Is.EqualTo(previewPosition));
                var parent = (RectTransform)cup.parent;
                Assert.That(parent.sizeDelta, Is.EqualTo(previewParentSize), "The cup's frame matches the preview plate.");
                // The canopy draws first, the cup over it, and the merchant over both; his layers are clear at the cup.
                Assert.That(parent.Find("Canopy").GetSiblingIndex(), Is.LessThan(cup.GetSiblingIndex()));
                Assert.That(parent.Find("Merchant").GetSiblingIndex(), Is.GreaterThan(cup.GetSiblingIndex()));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void MerchantPropsMoveWithTheMotionKindOfTheirCuts()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            var activity = table.AmbientActivity;
            Assert.DoesNotThrow(activity.Validate);
            var cuts = ReadPropCuts();
            foreach (var (id, name) in PropActors)
            {
                var actor = activity.Props.Single(candidate => candidate.Id == id);
                var cut = cuts.Single(candidate => candidate.Name == name);
                Assert.That(actor.Strength, Is.GreaterThan(0f), id);
                if (name == "ShelfBottle")
                {
                    var wobble = actor.Motion as AmbientWobbleMotionAsset;
                    Assert.That(wobble, Is.Not.Null, "The bottle rocks on its base.");
                    Vector2[] contacts = cut.Contacts.Select(contact => cut.Normalized(contact)).ToArray();
                    Assert.That(Vector2.Distance(wobble.LeftContact, contacts[0]), Is.LessThan(.0001f), "The bottle rocks on its cut's left contact.");
                    Assert.That(Vector2.Distance(wobble.RightContact, contacts[1]), Is.LessThan(.0001f), "The bottle rocks on its cut's right contact.");
                }
                else if (name == "TrunkLid") Assert.That(actor.Motion, Is.InstanceOf<AmbientThumpMotionAsset>(), "The lid thumps.");
                else Assert.That(actor.Motion, Is.InstanceOf<AmbientSwingMotionAsset>(), name + " swings from its ring.");

                // The lid's idle hop reads clearly against its cut's suggested lift; a disturbance hits harder than any idle motion.
                bool lift = name == "TrunkLid";
                float idleLow = PeakResponse(actor.Motion, actor.Strength * actor.IdleStrength.Min, lift);
                float idle = PeakResponse(actor.Motion, actor.Strength * actor.IdleStrength.Max, lift);
                float disturbance = PeakResponse(actor.Motion, actor.Strength * activity.DisturbanceStrength, lift);
                if (lift) Assert.That(idleLow / (cut.SuggestedLift * FrameHeight / PlateSize.y), Is.GreaterThanOrEqualTo(2f),
                    name + " idle hop " + idleLow);
                Assert.That(idleLow, Is.GreaterThan(0f), name + " moves on an idle jolt.");
                Assert.That(disturbance, Is.GreaterThan(idle), name + " disturbance peak " + disturbance);
            }
        }

        [Test]
        public void MerchantScenePlacesTheMovablePropsOverTheirRemovedRegionsBoundToTheActivity()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            var cuts = ReadPropCuts();
            Vector2 plateSize = PlateSize;
            var scene = EditorSceneManager.OpenPreviewScene(AssetDatabase.GUIDToAssetPath(table.Scene.AssetGUID));
            try
            {
                var roots = scene.GetRootGameObjects();
                var view = roots.SelectMany(root => root.GetComponentsInChildren<SpiritEncounterView>(true)).Single();
                var bindings = AmbientBindings(view);
                Transform frame = FindCup(roots).transform.parent;
                foreach (var (id, name) in PropActors)
                    Assert.That(bindings.Single(binding => binding.ActorId == id).Target, Is.SameAs(frame.Find(name)), id);
                foreach (float width in new[] { 540f, 443f })
                {
                    view.Portrait.anchorMin = view.Portrait.anchorMax = new Vector2(.5f, .5f);
                    view.Portrait.sizeDelta = new Vector2(width, 960);
                    view.Portrait.anchoredPosition = Vector2.zero;
                    // A preview scene neither sizes nor scales its overlay canvas, so the test gives it unit scale and recalculates rects.
                    view.Canvas.transform.localScale = Vector3.one;
                    foreach (var rect in view.GetComponentsInChildren<RectTransform>(true)) rect.ForceUpdateRectTransforms();
                    RectTransform front = view.TableImage.rectTransform, back = view.RoomImage.rectTransform;
                    // Props register to the encounter's background images, which show the room texture across both halves.
                    AssertPropsPlaced(frame, cuts, table, world =>
                    {
                        Vector2 uv = SpiritTablePieceLayout.TextureCoordinate(front.InverseTransformPoint(world), front.rect,
                            view.TableImage.uvRect, back.InverseTransformPoint(world), back.rect, view.RoomImage.uvRect);
                        return new Vector2(uv.x * plateSize.x, (1f - uv.y) * plateSize.y);
                    });
                }
                Assert.That(frame.Find("Canopy").GetSiblingIndex(), Is.LessThan(FindCup(roots).transform.GetSiblingIndex()));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void PreviewScenePlacesTheMovablePropsOverTheirRemovedRegions()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            var cuts = ReadPropCuts();
            Vector2 plateSize = PlateSize;
            var scene = EditorSceneManager.OpenPreviewScene(PreviewScenePath);
            try
            {
                var roots = scene.GetRootGameObjects();
                Transform frame = FindCup(roots).transform.parent;
                var plate = frame.GetComponent<RawImage>();
                Assert.That(plate, Is.Not.Null, "The preview's props register to its background plate.");
                Assert.That(plate.uvRect, Is.EqualTo(new Rect(0, 0, 1, 1)));
                foreach (var rect in frame.GetComponentsInChildren<RectTransform>(true)) rect.ForceUpdateRectTransforms();
                AssertPropsPlaced(frame, cuts, table, world =>
                {
                    Rect bounds = plate.rectTransform.rect;
                    Vector3 local = plate.rectTransform.InverseTransformPoint(world);
                    return new Vector2((local.x - bounds.xMin) / bounds.width * plateSize.x,
                        (bounds.yMax - local.y) / bounds.height * plateSize.y);
                });
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void MerchantScenesLightTheHangingCupWithTheLanternAtItsRestRect()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            Vector2 plateSize = PlateSize;
            foreach (string path in new[] { AssetDatabase.GUIDToAssetPath(table.Scene.AssetGUID), PreviewScenePath })
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    var cup = (RectTransform)FindCup(scene.GetRootGameObjects()).transform;
                    var frame = (RectTransform)cup.parent;
                    var image = cup.GetComponentsInChildren<RawImage>(true).Single();
                    Assert.That(image.raycastTarget, Is.False, path);
                    Assert.That(image.uvRect, Is.EqualTo(new Rect(0, 0, 1, 1)), path);
                    Assert.That(image.material.GetTexture("_LayerMask"), Is.Null, "The cup has no ownership mask.");
                    Assert.That(image.material.GetFloat("_InvertMask"), Is.EqualTo(1), "Without a mask the inverted selection covers the cup.");
                    Assert.That(image.material.GetTexture("_SourceTex"), Is.SameAs(image.texture), path);

                    var lit = image.GetComponent<SceneLitImage>();
                    Assert.That(lit, Is.Not.Null, path);
                    Assert.That(lit.enabled, Is.True, path);
                    Assert.That(lit.Lighting, Is.SameAs(table.SceneLighting), path);
                    Assert.DoesNotThrow(lit.Layer.Validate);
                    var lantern = lit.Layer.Lights.Single();
                    Assert.That(lantern.LightIndex, Is.Zero, "Only the lantern lights the cup; no computed candle light.");
                    Assert.That(lantern.Mode, Is.EqualTo(SceneLitLightMode.Reference), path);

                    // The response map is a smooth distance field, so the swinging cup samples it at its rest rect.
                    Assert.That(cup.Find("Swing").localRotation, Is.EqualTo(Quaternion.identity), "The scene holds the cup at rest.");
                    foreach (var rect in frame.GetComponentsInChildren<RectTransform>(true)) rect.ForceUpdateRectTransforms();
                    Rect bounds = frame.rect;
                    // Frame-local, so the result does not depend on the scale a preview scene gives its canvas.
                    Vector2 Plate(Vector2 point)
                    {
                        Vector3 local = point;
                        for (Transform node = image.rectTransform; node != frame; node = node.parent)
                            local = Matrix4x4.TRS(node.localPosition, node.localRotation, node.localScale).MultiplyPoint3x4(local);
                        return new Vector2((local.x - bounds.xMin) / bounds.width * plateSize.x, (bounds.yMax - local.y) / bounds.height * plateSize.y);
                    }
                    Rect imageRect = image.rectTransform.rect;
                    Vector2 topLeft = Plate(new Vector2(imageRect.xMin, imageRect.yMax)), bottomRight = Plate(new Vector2(imageRect.xMax, imageRect.yMin));
                    var mapRect = new Rect(topLeft.x / plateSize.x, 1f - bottomRight.y / plateSize.y,
                        (bottomRight.x - topLeft.x) / plateSize.x, (bottomRight.y - topLeft.y) / plateSize.y);
                    Assert.That(Vector4.Distance(new Vector4(lit.Layer.MapRect.x, lit.Layer.MapRect.y, lit.Layer.MapRect.width,
                        lit.Layer.MapRect.height), new Vector4(mapRect.x, mapRect.y, mapRect.width, mapRect.height)), Is.LessThan(.00001f),
                        path + " samples the plate's response map under the cup's rest rect " + mapRect.ToString("F6"));
                    // The material carries the same rectangle for an image no component lights.
                    Vector4 materialRect = image.material.GetVector("_SceneLitMapRect");
                    Assert.That(Vector4.Distance(materialRect, new Vector4(mapRect.x, mapRect.y, mapRect.width, mapRect.height)),
                        Is.LessThan(.00001f), path);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
        }

        [Test]
        public void MovablePropsCompositeOverThePatchedPlatesIntoTheInstalledPlates()
        {
            foreach (var plates in new[] { (Cut: "Unlit", Patched: "MerchantAmbientUnlitPropsRemoved.png", Installed: "MerchantAmbientUnlit.png"),
                (Cut: "LanternLit", Patched: "MerchantLanternOnlyPropsRemoved.png", Installed: "MerchantLanternOnly.png") })
            {
                var patched = Decode(PropsFolder + plates.Patched);
                var installed = Decode(ArtPath + plates.Installed);
                try
                {
                    Assert.That(new Vector2(installed.width, installed.height), Is.EqualTo(PlateSize), "The props are cut from the plates' frame.");
                    Assert.That(new Vector2Int(patched.width, patched.height), Is.EqualTo(new Vector2Int(installed.width, installed.height)));
                    Color32[] before = installed.GetPixels32(), after = patched.GetPixels32();
                    var cuts = ReadPropCuts().OrderBy(cut => cut.DrawOrder).ToList();
                    // Plate coverage of every piece drawn after each piece; the cut completes an earlier piece where a later one hides it.
                    var coveredLater = new bool[cuts.Count][];
                    var covered = new bool[before.Length];
                    for (int index = cuts.Count - 1; index >= 0; --index)
                    {
                        coveredLater[index] = (bool[])covered.Clone();
                        var cover = Decode(PropsFolder + cuts[index].Name + plates.Cut + ".png");
                        try
                        {
                            Color32[] coverPixels = cover.GetPixels32();
                            for (int row = 0; row < cover.height; ++row)
                                for (int column = 0; column < cover.width; ++column)
                                    if (coverPixels[row * cover.width + column].a > 0)
                                        covered[(installed.height - 1 - (cuts[index].Crop.y + cover.height - 1 - row)) * installed.width
                                            + cuts[index].Crop.x + column] = true;
                        }
                        finally { Object.DestroyImmediate(cover); }
                    }
                    for (int index = 0; index < cuts.Count; ++index)
                    {
                        var cut = cuts[index];
                        var piece = Decode(PropsFolder + cut.Name + plates.Cut + ".png");
                        try
                        {
                            Assert.That(new Vector2Int(piece.width, piece.height), Is.EqualTo(cut.Crop.size), cut.Name);
                            Color32[] pixels = piece.GetPixels32();
                            int opaque = 0, removed = 0;
                            for (int row = 0; row < piece.height; ++row)
                                for (int column = 0; column < piece.width; ++column)
                                {
                                    Color32 pixel = pixels[row * piece.width + column];
                                    if (pixel.a < 255) continue;
                                    // Texture rows start at the bottom; crop rows start at the top.
                                    int plateIndex = (installed.height - 1 - (cut.Crop.y + piece.height - 1 - row)) * installed.width
                                        + cut.Crop.x + column;
                                    // Completed pixels behind a later piece show only through its soft edge and need not match the plate.
                                    if (coveredLater[index][plateIndex]) continue;
                                    Color32 expected = before[plateIndex];
                                    Assert.That(Math.Max(Math.Abs(pixel.r - expected.r), Math.Max(Math.Abs(pixel.g - expected.g),
                                        Math.Abs(pixel.b - expected.b))), Is.LessThanOrEqualTo(2),
                                        $"{cut.Name} {plates.Cut} at crop ({column}, {piece.height - 1 - row})");
                                    ++opaque;
                                    Color32 behind = after[plateIndex];
                                    if (Math.Max(Math.Abs(behind.r - expected.r), Math.Max(Math.Abs(behind.g - expected.g),
                                        Math.Abs(behind.b - expected.b))) > 2) ++removed;
                                }
                            Assert.That(opaque, Is.GreaterThan(500), cut.Name);
                            Assert.That(removed, Is.GreaterThan(opaque / 2), cut.Name + " is removed from the patched plate.");
                        }
                        finally { Object.DestroyImmediate(piece); }
                    }
                }
                finally
                {
                    Object.DestroyImmediate(patched);
                    Object.DestroyImmediate(installed);
                }
            }
        }

        [Test]
        public void MerchantPropBurstsSitUnderThePoltergeistGiggles()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(TablePath);
            var activity = table.AmbientActivity;
            var giggles = table.AudioTheme;
            Assert.That(activity.BurstClips, Is.Not.Empty, "Each disturbance rumbles the room.");
            Assert.That(giggles.disturbanceClips, Is.Not.Empty, "The poltergeist giggles on each disturbance.");
            Assert.That(activity.BurstVolume, Is.GreaterThan(0f).And.LessThan(giggles.effectsVolume), "Bursts play under the giggles.");
            Assert.That(activity.BurstClips.Max(EncounterAmbientActivityTests.Peak),
                Is.LessThan(giggles.disturbanceClips.Min(EncounterAmbientActivityTests.Peak)), "Burst clips are normalized under the giggles.");
        }

        // Helpers

        private GameObject InstantiateCanopy()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CanopyPrefabPath);
            Assert.That(asset, Is.Not.Null);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, preview);
            instance.transform.SetParent(canvas, false);
            return instance;
        }

        private static void SampleCanopy(GameObject canopy, float time)
        {
            var animation = canopy.GetComponent<Animation>();
            Assert.That(animation.Play(), Is.True);
            AnimationState state = animation[animation.clip.name];
            Assert.That(state, Is.Not.Null);
            state.time = time;
            state.weight = 1;
            animation.Sample();
        }

        private static Vector3[] Vertices(SpiritPortraitImage image)
        {
            // Rebuild only consumes the dirty state set by the native animation callback.
            image.Rebuild(CanvasUpdate.PreRender);
            Mesh mesh = image.canvasRenderer.GetMesh();
            Assert.That(mesh, Is.Not.Null);
            return mesh.vertices;
        }

        private static float MaxDistance(Vector3[] first, Vector3[] second)
        {
            Assert.That(second.Length, Is.EqualTo(first.Length));
            float maximum = 0;
            for (int i = 0; i < first.Length; ++i) maximum = Mathf.Max(maximum, Vector3.Distance(first[i], second[i]));
            return maximum;
        }

        private static Texture2D DecodePng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
                return texture;
            }
            catch
            {
                Object.DestroyImmediate(texture);
                throw;
            }
        }

        private static Texture2D Decode(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
            return texture;
        }

        private static void AssertPropsPlaced(Transform frame, IReadOnlyList<PropCut> cuts, EncounterTablePresentationAsset table,
            Func<Vector3, Vector2> plate)
        {
            Transform canopy = frame.Find("Canopy"), merchant = frame.Find("Merchant");
            Transform cup = frame.Cast<Transform>().Single(child =>
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject) == CupPrefabPath);
            Assert.That(canopy, Is.Not.Null);
            Assert.That(merchant, Is.Not.Null);
            Vector2 plateSize = PlateSize;
            int previous = -1;
            foreach (var cut in cuts.OrderBy(candidate => candidate.DrawOrder))
            {
                var prop = frame.Find(cut.Name) as RectTransform;
                Assert.That(prop, Is.Not.Null, cut.Name);
                Assert.That(prop.gameObject.activeSelf, Is.True, cut.Name);
                // Props draw over the background in the cut's order (the pot behind the pan), under the canopy, cup and merchant.
                Assert.That(prop.GetSiblingIndex(), Is.GreaterThan(previous), cut.Name);
                previous = prop.GetSiblingIndex();
                Assert.That(prop.GetSiblingIndex(), Is.LessThan(canopy.GetSiblingIndex()), cut.Name);
                Assert.That(prop.GetSiblingIndex(), Is.LessThan(cup.GetSiblingIndex()), cut.Name);
                Assert.That(prop.GetSiblingIndex(), Is.LessThan(merchant.GetSiblingIndex()), cut.Name);

                var image = prop.GetComponent<RawImage>();
                Assert.That(image, Is.Not.Null, cut.Name);
                Assert.That(image.raycastTarget, Is.False, cut.Name + " must not intercept input.");
                Assert.That(image.uvRect, Is.EqualTo(new Rect(0, 0, 1, 1)));
                Assert.That(image.material.GetTexture("_LayerMask"), Is.Null, "Props have no ownership mask.");
                Assert.That(image.material.GetFloat("_InvertMask"), Is.EqualTo(1), "Without a mask the inverted selection covers the prop.");
                Assert.That(image.material.GetTexture("_SourceTex"), Is.SameAs(image.texture));

                var lit = prop.GetComponent<SceneLitImage>();
                Assert.That(lit, Is.Not.Null, cut.Name);
                Assert.That(lit.enabled, Is.True);
                Assert.That(lit.Lighting, Is.SameAs(table.SceneLighting));
                Assert.DoesNotThrow(lit.Layer.Validate);
                var lantern = lit.Layer.Lights.Single();
                Assert.That(lantern.LightIndex, Is.Zero, "Only the lantern lights the props; no computed candle light.");
                Assert.That(lantern.Mode, Is.EqualTo(SceneLitLightMode.Reference));
                var mapRect = new Rect(cut.Crop.x / plateSize.x, 1f - (cut.Crop.y + cut.Crop.height) / plateSize.y,
                    cut.Crop.width / plateSize.x, cut.Crop.height / plateSize.y);
                Assert.That(Vector4.Distance(new Vector4(lit.Layer.MapRect.x, lit.Layer.MapRect.y, lit.Layer.MapRect.width,
                    lit.Layer.MapRect.height), new Vector4(mapRect.x, mapRect.y, mapRect.width, mapRect.height)), Is.LessThan(.00001f),
                    cut.Name + " samples the plate's response map under its crop.");

                // The pivot is the cut's hang point, base centre or hinge, so motion turns and lifts about it.
                Assert.That(Vector2.Distance(prop.pivot, cut.Normalized(cut.Pivot)), Is.LessThan(.00001f), cut.Name);
                Assert.That(Vector2.Distance(plate(prop.position), cut.Pivot), Is.LessThan(.1f), cut.Name + " pivot");
                var corners = new Vector3[4];
                prop.GetWorldCorners(corners);
                Vector2 topLeft = plate(corners[1]), bottomRight = plate(corners[3]);
                Assert.That(Vector2.Distance(topLeft, cut.Crop.position), Is.LessThan(.1f), cut.Name + " top-left " + topLeft);
                Assert.That(Vector2.Distance(bottomRight, cut.Crop.max), Is.LessThan(.1f), cut.Name + " bottom-right " + bottomRight);

                // Every solid pixel the prop draws lands inside the placed rectangle, so the prop covers its removed region at rest.
                var piece = Decode(AssetDatabase.GetAssetPath(image.texture));
                try
                {
                    Assert.That(new Vector2Int(piece.width, piece.height), Is.EqualTo(cut.Crop.size), cut.Name + " draws its cut.");
                    Color32[] pixels = piece.GetPixels32();
                    int solid = 0;
                    for (int row = 0; row < piece.height; ++row)
                        for (int column = 0; column < piece.width; ++column)
                        {
                            if (pixels[row * piece.width + column].a <= 127) continue;
                            ++solid;
                            var point = new Vector2(cut.Crop.x + column + .5f, cut.Crop.y + piece.height - 1 - row + .5f);
                            Assert.That(point.x > topLeft.x && point.x < bottomRight.x && point.y > topLeft.y && point.y < bottomRight.y,
                                Is.True, cut.Name + " pixel " + point);
                        }
                    Assert.That(solid, Is.GreaterThan(500), cut.Name);
                }
                finally { Object.DestroyImmediate(piece); }
            }
        }

        private static float PeakResponse(AmbientMotionAsset asset, float strength, bool lift)
        {
            var target = new GameObject("Peak", typeof(RectTransform));
            try
            {
                ((RectTransform)target.transform).sizeDelta = new Vector2(40f, 60f);
                var motion = asset.Create(target.transform);
                motion.Strike(0, strength);
                float peak = 0f;
                for (double time = 0; time < 10; time += EncounterAmbientActivityTests.Step / 4f)
                {
                    motion.Apply(time);
                    peak = Mathf.Max(peak, lift ? target.transform.localPosition.y
                        : Mathf.Abs(Mathf.DeltaAngle(0f, target.transform.localEulerAngles.z)));
                }
                return peak;
            }
            finally { Object.DestroyImmediate(target); }
        }

        /** The plates' size in pixels, as the prop cuts record it. */
        private static Vector2 PlateSize
        {
            get
            {
                float[] size = JObject.Parse(File.ReadAllText(PropsFolder + "props.json"))["source_plates"]["size"].Values<float>().ToArray();
                return new Vector2(size[0], size[1]);
            }
        }

        private static List<PropCut> ReadPropCuts()
        {
            var json = JObject.Parse(File.ReadAllText(PropsFolder + "props.json"));
            var cuts = new List<PropCut>();
            foreach (var piece in json["pieces"])
            {
                var crop = piece["crop_rect"];
                var pivot = piece["pivot"]["plate"].Values<float>().ToArray();
                var contacts = piece["pivot"]["base_contacts_plate"];
                var motion = piece["motion"];
                cuts.Add(new PropCut
                {
                    Name = (string)piece["name"],
                    DrawOrder = (int)piece["draw_order"],
                    Crop = new RectInt((int)crop["x"], (int)crop["y"], (int)crop["w"], (int)crop["h"]),
                    Pivot = new Vector2(pivot[0], pivot[1]),
                    Contacts = contacts == null ? Array.Empty<Vector2>() : contacts
                        .Select(point => new Vector2((float)point[0], (float)point[1])).ToArray(),
                    SuggestedLift = (float?)motion["lift_px"] ?? 0f,
                });
            }
            Assert.That(cuts.Select(cut => cut.Name), Is.EquivalentTo(PropActors.Select(prop => prop.Name)),
                "Each cut prop is one of the merchant's movable props.");
            return cuts;
        }

        private sealed class PropCut
        {
            public string Name;
            public int DrawOrder;
            /** Plate pixels, top-left origin. */
            public RectInt Crop;
            public Vector2 Pivot;
            public Vector2[] Contacts;
            public float SuggestedLift;

            /** Plate point as normalized bottom-left coordinates of the crop, like a RectTransform pivot. */
            public Vector2 Normalized(Vector2 point)
                => new Vector2((point.x - Crop.x) / Crop.width, 1f - (point.y - Crop.y) / Crop.height);
        }

        private static GameObject FindCup(GameObject[] roots)
            => roots.SelectMany(root => root.GetComponentsInChildren<Animation>(true))
                .Single(animation => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(animation.gameObject) == CupPrefabPath)
                .gameObject;

        private static List<EncounterAmbientPropBinding> AmbientBindings(SpiritEncounterView view)
            => (List<EncounterAmbientPropBinding>)typeof(SpiritEncounterView)
                .GetField("ambientProps", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);

        private AmbientSwingMotionAsset Swing(float amplitude)
        {
            var asset = Track(ScriptableObject.CreateInstance<AmbientSwingMotionAsset>());
            typeof(AmbientSwingMotionAsset).GetField("amplitude", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(asset, amplitude);
            return asset;
        }

        private T Track<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
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
            var references = new List<RawImage>();
            var referenceMaterials = new List<Material>();
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
                    SampleCanopy(animation.gameObject, time);
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
                        reference.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath + layer.Filename);
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
                    plateImage.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath +
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
                var roof = new List<Color32>();
                foreach (Vector2 point in new[] { new Vector2(250, 150), new Vector2(450, 215),
                    new Vector2(235, 295), new Vector2(300, 310) })
                {
                    for (int y = -4; y <= 4; ++y)
                        for (int x = -4; x <= 4; ++x)
                        {
                            Vector3 local = new Vector3(plate.rect.xMin + (point.x + x) * plate.rect.width / plateTexture.width,
                                plate.rect.yMax - (point.y + y) * plate.rect.height / plateTexture.height, 0);
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
                foreach (RawImage reference in references) Object.DestroyImmediate(reference.gameObject);
                foreach (Material material in referenceMaterials) Object.DestroyImmediate(material);
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
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }

        private static bool[] ProjectHeadRepair(Camera camera, Canvas canvas, int width, int height)
        {
            var repair = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                Assert.That(repair.LoadImage(File.ReadAllBytes(ArtPath + "MerchantHeadRepairMask.png")), Is.True);
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
                Object.DestroyImmediate(repair);
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
                    Assert.That(texture.LoadImage(File.ReadAllBytes(ArtPath + filename)), Is.True);
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
                finally { Object.DestroyImmediate(texture); }
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
