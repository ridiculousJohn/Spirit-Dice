using System.IO;
using System.Linq;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class CanopyMotionTests
    {
        private const string PrefabPath = "Assets/Dice/Content/Presentation/Ambient/Canopy.prefab";
        private const string MeshPath = "Assets/Dice/Content/Presentation/Ambient/CanopyMesh.asset";
        private const string MaterialPath = "Assets/Dice/Content/Presentation/Ambient/CanopySource.mat";
        internal const string ArtPath = "Assets/Dice/Art/HauntedMerchant/";
        private static readonly Vector2[] AttachmentSourcePoints =
        {
            new Vector2(230, 205), new Vector2(350, 230), new Vector2(430, 225),
            new Vector2(520, 260), new Vector2(580, 350),
            new Vector2(450, 330), new Vector2(475, 325),
            new Vector2(429, 175), new Vector2(478, 194),
            new Vector2(525, 207), new Vector2(574, 219)
        };
        private static readonly Vector2 DisplaySize = new Vector2(540, 960);
        private Scene preview;
        private RectTransform canvas;

        [SetUp]
        public void SetUp()
        {
            preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Canopy Test Canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, preview);
            canvas = (RectTransform)root.transform;
            canvas.sizeDelta = DisplaySize;
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        }

        [TearDown]
        public void TearDown()
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }

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
                Assert.That(sourceSize, Is.EqualTo(new Vector2Int(941, 1672)));
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
                TestContext.WriteLine($"Original scene provenance: {clothPixels} moving pixels, {mismatches} source errors, {alphaErrors} alpha errors, {outsidePlateErrors} outside plate errors, {hiddenPlateChanges} hidden fill pixels.");
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(fringe);
                Object.DestroyImmediate(plate);
                Object.DestroyImmediate(mask);
            }
        }

        [Test]
        public void PrefabUsesNativeAutoplayAndOneRegisteredFringeImageWithTwoScalarBindings()
        {
            GameObject canopy = InstantiateCanopy();
            var animation = canopy.GetComponent<Animation>();
            Assert.That(animation, Is.Not.Null);
            Assert.That(animation.playAutomatically, Is.True);
            Assert.That(animation.cullingType, Is.EqualTo(AnimationCullingType.AlwaysAnimate));
            Assert.That(animation.wrapMode, Is.EqualTo(WrapMode.Loop));
            Assert.That(animation.clip, Is.Not.Null);
            Assert.That(animation.clip.legacy, Is.True);
            Assert.That(animation.clip.length, Is.EqualTo(31).Within(.001f));
            Assert.That(AnimationUtility.GetAnimationClipSettings(animation.clip).loopTime, Is.True);
            Assert.That(((RectTransform)canopy.transform).sizeDelta, Is.EqualTo(DisplaySize));
            var images = canopy.GetComponentsInChildren<SpiritPortraitImage>();
            Assert.That(images.Length, Is.EqualTo(1));
            Assert.That(canopy.GetComponentsInChildren<MonoBehaviour>(true)
                .All(component => component is SpiritPortraitImage || component is SpiritPortraitDeformationChannel
                    || component is SceneLitImage), Is.True);
            var mesh = AssetDatabase.LoadAssetAtPath<SpiritPortraitMeshAsset>(MeshPath);
            Assert.That(mesh, Is.Not.Null);
            Assert.DoesNotThrow(mesh.Validate);
            Assert.That(mesh.Columns, Is.EqualTo(96));
            Assert.That(mesh.Rows, Is.EqualTo(192));
            Assert.That(mesh.Shapes.Length, Is.EqualTo(2));
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var source = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath + "Relight/CanopyUnlit.png");
            var layerMask = AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath + "CanopyOriginalOverhangMask.png");
            Assert.That(material, Is.Not.Null);
            Assert.That(source, Is.Not.Null);
            Assert.That(layerMask, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(material.shader), Is.EqualTo("Assets/Dice/Presentation/Shared/SceneLitImage.shader"));
            Assert.That(material.GetTexture("_SourceTex"), Is.SameAs(source));
            Assert.That(material.GetTexture("_LayerMask"), Is.SameAs(layerMask));
            Assert.That(material.GetFloat("_InvertMask"), Is.EqualTo(0));
            Assert.That(source.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(layerMask.filterMode, Is.EqualTo(FilterMode.Bilinear));
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(animation.clip);
            Assert.That(bindings.Length, Is.EqualTo(images.Sum(image => image.Weights.Count)));
            Assert.That(bindings.All(binding => binding.propertyName == "weight" && binding.type == typeof(SpiritPortraitDeformationChannel)), Is.True);
            var channels = canopy.GetComponentsInChildren<SpiritPortraitDeformationChannel>();
            Assert.That(channels.Length, Is.EqualTo(bindings.Length));
            for (int i = 0; i < images.Length; ++i)
            {
                SpiritPortraitImage image = images[i];
                Assert.That(image.name, Is.EqualTo("CanopyArtwork"));
                Assert.That(AssetDatabase.GetAssetPath(image.texture), Is.EqualTo(ArtPath + "MerchantOriginal.png"));
                Assert.That(new Vector2Int(image.texture.width, image.texture.height), Is.EqualTo(new Vector2Int(941, 1672)));
                Assert.That(image.material, Is.SameAs(material));
                Assert.That(image.raycastTarget, Is.False);
                Assert.That(image.Weights.Count, Is.EqualTo(mesh.Shapes.Length));
                Assert.That(new SerializedObject(image).FindProperty("mesh").objectReferenceValue, Is.SameAs(mesh));
                Assert.That(Quaternion.Angle(image.rectTransform.localRotation, Quaternion.identity), Is.LessThan(.001f));
                Assert.That(image.rectTransform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(image.rectTransform.rect.size, Is.EqualTo(DisplaySize));
                var corners = new Vector3[4];
                var rootCorners = new Vector3[4];
                image.rectTransform.GetWorldCorners(corners);
                ((RectTransform)canopy.transform).GetWorldCorners(rootCorners);
                for (int corner = 0; corner < corners.Length; ++corner)
                    Assert.That(Vector3.Distance(corners[corner], rootCorners[corner]), Is.LessThan(.0001f));
                Assert.That(image.uvRect, Is.EqualTo(new Rect(0, 0, 1, 1)));
                Assert.That(bindings.Count(binding => binding.path.StartsWith(image.name + "/")), Is.EqualTo(image.Weights.Count));
            }
            foreach (EditorCurveBinding binding in bindings)
            {
                Transform target = canopy.transform.Find(binding.path);
                Assert.That(target, Is.Not.Null);
                var channel = new SerializedObject(target.GetComponent<SpiritPortraitDeformationChannel>());
                Assert.That(channel.FindProperty("image").objectReferenceValue, Is.SameAs(target.parent.GetComponent<SpiritPortraitImage>()));
                Assert.That(channel.FindProperty("shapeIndex").intValue, Is.EqualTo(target.name == "LeftFlutter" ? 0 : 1));
                Assert.That(AnimationUtility.GetAnimatableBindings(target.gameObject, canopy)
                    .Any(candidate => candidate.path == binding.path && candidate.type == binding.type && candidate.propertyName == binding.propertyName),
                    Is.True, "Each scalar channel must resolve to an animatable property on its authored target.");
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
        public void FractionalMaskCoverageMatchesDefaultUiAlphaBlending()
        {
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(MaterialPath));
            var source = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var mask = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            var reference = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var pixels = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            var target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32);
            var cameraObject = new GameObject("Coverage Test Camera", typeof(Camera));
            var imageObject = new GameObject("Coverage Test Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            RenderTexture previous = RenderTexture.active;
            try
            {
                source.SetPixels32(new[] { new Color32(230, 140, 70, 255) });
                mask.SetPixels32(new[] { new Color32(255, 255, 255, 128) });
                reference.SetPixels32(new[] { new Color32(230, 140, 70, 128) });
                source.Apply();
                mask.Apply();
                reference.Apply();
                material.SetTexture("_SourceTex", source);
                material.SetTexture("_LayerMask", mask);
                material.SetFloat("_InvertMask", 0);
                material.SetFloat("_SceneLitIntensity0", 0);
                SceneManager.MoveGameObjectToScene(cameraObject, preview);
                SceneManager.MoveGameObjectToScene(imageObject, preview);
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = DisplaySize.y / 2;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.04f, .08f, .12f, 1);
                camera.targetTexture = target;
                camera.scene = preview;
                var image = imageObject.GetComponent<RawImage>();
                image.transform.SetParent(canvas, false);
                image.rectTransform.anchorMin = Vector2.zero;
                image.rectTransform.anchorMax = Vector2.one;
                image.rectTransform.sizeDelta = Vector2.zero;
                image.texture = source;
                image.material = material;
                image.enabled = false;
                Color32 background = Capture();
                image.enabled = true;
                Color32 actual = Capture();
                image.material = null;
                image.texture = reference;
                Color32 expected = Capture();
                Assert.That(expected.r, Is.GreaterThan(background.r + 20), "The reference image must render visibly over the opaque background.");
                Assert.That(actual.r, Is.EqualTo(expected.r).Within(2));
                Assert.That(actual.g, Is.EqualTo(expected.g).Within(2));
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(2));

                Color32 Capture()
                {
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(32, 32, 1, 1), 0, 0);
                    pixels.Apply();
                    return pixels.GetPixels32()[0];
                }
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(imageObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(reference);
                Object.DestroyImmediate(mask);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void NativeAnimationSampleAppliesEveryScalarCallbackAndChangesEachRenderedMesh()
        {
            GameObject canopy = InstantiateCanopy();
            var images = canopy.GetComponentsInChildren<SpiritPortraitImage>();
            Sample(canopy, 0);
            Vector3[][] first = images.Select(Vertices).ToArray();
            var mesh = AssetDatabase.LoadAssetAtPath<SpiritPortraitMeshAsset>(MeshPath);
            float[] maximum = new float[images.Length];
            foreach (float time in new[] { 1.56f, 4.36f, 4.58f, 5.18f, 11.08f, 15.42f, 15.68f, 15.95f, 16.37f, 23.92f, 24.72f, 28.4f })
            {
                Sample(canopy, time);
                for (int i = 0; i < images.Length; ++i)
                {
                    Assert.That(first[i].Length, Is.EqualTo((mesh.Columns + 1) * (mesh.Rows + 1)));
                    maximum[i] = Mathf.Max(maximum[i], MaxDistance(first[i], Vertices(images[i])));
                }
            }
            for (int i = 0; i < images.Length; ++i)
            {
                Assert.That(maximum[i], Is.GreaterThan(.5f), images[i].name);
                Assert.That(maximum[i], Is.LessThan(8), "Loose fringe motion must remain small at the reference display size.");
            }
        }

        [Test]
        public void SupportedRoofAndUndersideStayFixedAcrossTheLoop()
        {
            GameObject canopy = InstantiateCanopy();
            var images = canopy.GetComponentsInChildren<SpiritPortraitImage>();
            var root = (RectTransform)canopy.transform;
            root.anchoredPosition = new Vector2(37, -53);
            Vector3 fixedRoot = root.position;
            for (float time = 0; time <= 31; time += .25f)
            {
                Sample(canopy, time);
                Assert.That(root.position, Is.EqualTo(fixedRoot));
                for (int i = 0; i < images.Length; ++i)
                {
                    SpiritPortraitImage image = images[i];
                    foreach (Vector2 point in AttachmentSourcePoints)
                        Assert.That(image.EvaluateDisplacement(new Vector2(point.x / 941, 1 - point.y / 1672),
                            image.rectTransform.rect.size).magnitude, Is.LessThan(.00001f),
                            $"Supported canopy moved at {time} seconds.");
                }
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
                int quietSamples = 0, busySamples = 0;
                for (float time = .05f; time < clip.length; time += .05f)
                {
                    float speed = Mathf.Abs(curve.Evaluate(time) - curve.Evaluate(time - .05f)) / .05f;
                    peakSpeeds[i] = Mathf.Max(peakSpeeds[i], speed);
                    if (speed < .03f) ++quietSamples;
                    if (speed > .6f) ++busySamples;
                }
                Assert.That(quietSamples, Is.GreaterThan(100), bindings[i].path + " needs sustained quiet stretches.");
                Assert.That(busySamples, Is.GreaterThan(15), bindings[i].path + " needs clearly perceptible bursts.");
                Assert.That(peakSpeeds[i], Is.GreaterThan(1));
                for (int other = 0; other < i; ++other)
                {
                    float difference = 0;
                    for (float time = 0; time < clip.length; time += .1f)
                        difference += Mathf.Abs(curve.Evaluate(time) - curves[other].Evaluate(time));
                    Assert.That(difference, Is.GreaterThan(10), "The two deformation channels must have distinct schedules.");
                }
            }
        }

        [Test]
        public void LoopJoinsWithMatchingWeightsMeshAndVelocity()
        {
            GameObject canopy = InstantiateCanopy();
            var images = canopy.GetComponentsInChildren<SpiritPortraitImage>();
            AnimationClip clip = canopy.GetComponent<Animation>().clip;
            clip.SampleAnimation(canopy, 0);
            float[] first = images.SelectMany(image => image.Weights).ToArray();
            Vector3[][] firstMesh = images.Select(Vertices).ToArray();
            clip.SampleAnimation(canopy, clip.length);
            Assert.That(images.SelectMany(image => image.Weights).ToArray(), Is.EqualTo(first).Within(.00001f));
            for (int i = 0; i < images.Length; ++i)
                Assert.That(MaxDistance(firstMesh[i], Vertices(images[i])), Is.LessThan(.0001f));
            const float step = .001f;
            clip.SampleAnimation(canopy, step);
            float[] initialVelocity = images.SelectMany(image => image.Weights).Select((value, i) => (value - first[i]) / step).ToArray();
            clip.SampleAnimation(canopy, clip.length - step);
            float[] finalVelocity = images.SelectMany(image => image.Weights).Select((value, i) => (first[i] - value) / step).ToArray();
            Assert.That(finalVelocity, Is.EqualTo(initialVelocity).Within(.002f));
        }

        [Test]
        public void InstancesShareAuthoredAssetsAndKeepIndependentAnimationAndMeshState()
        {
            GameObject first = InstantiateCanopy();
            GameObject second = InstantiateCanopy();
            Assert.That(first.GetComponent<Animation>().clip, Is.SameAs(second.GetComponent<Animation>().clip));
            var firstImages = first.GetComponentsInChildren<SpiritPortraitImage>();
            var secondImages = second.GetComponentsInChildren<SpiritPortraitImage>();
            Sample(first, 15.68f);
            Vector3[][] firstPose = firstImages.Select(Vertices).ToArray();
            float[][] firstWeights = firstImages.Select(image => image.Weights.ToArray()).ToArray();
            Sample(second, 0);
            for (int i = 0; i < firstImages.Length; ++i)
            {
                Assert.That(new SerializedObject(firstImages[i]).FindProperty("mesh").objectReferenceValue,
                    Is.SameAs(new SerializedObject(secondImages[i]).FindProperty("mesh").objectReferenceValue));
                Assert.That(firstImages[i].Weights.ToArray(), Is.EqualTo(firstWeights[i]));
                Assert.That(secondImages[i].Weights.All(value => value == 0), Is.True);
                Assert.That(MaxDistance(firstPose[i], Vertices(firstImages[i])), Is.LessThan(.0001f));
            }
            Assert.That(firstImages.Select((image, i) => MaxDistance(firstPose[i], Vertices(secondImages[i]))).Max(), Is.GreaterThan(1));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(first), Is.SameAs(PrefabUtility.GetCorrespondingObjectFromSource(second)));
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(7)]
        public void SingleWeightUpdatesPreserveArbitraryOtherChannelsAndRejectInvalidInputsWithoutMutation(int count)
        {
            var image = InstantiateCanopy().GetComponentInChildren<SpiritPortraitImage>();
            var mesh = ScriptableObject.CreateInstance<SpiritPortraitMeshAsset>();
            try
            {
                mesh.Shapes = Enumerable.Range(0, count).Select(index => new SpiritPortraitShape
                    { Id = "Shape" + index, Offsets = new Vector2[4] }).ToArray();
                image.Configure(mesh);
                float[] expected = Enumerable.Range(0, count).Select(index => .1f * (index + 1)).ToArray();
                image.SetWeights(expected);
                for (int index = 0; index < count; ++index)
                {
                    expected[index] = -.25f * (index + 1);
                    image.SetWeight(index, expected[index]);
                    Assert.That(image.Weights.ToArray(), Is.EqualTo(expected));
                }
                Assert.Throws<System.ArgumentOutOfRangeException>(() => image.SetWeight(-1, 0));
                Assert.Throws<System.ArgumentOutOfRangeException>(() => image.SetWeight(count, 0));
                foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                    Assert.Throws<System.ArgumentException>(() => image.SetWeight(count - 1, invalid));
                Assert.That(image.Weights.ToArray(), Is.EqualTo(expected));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        private GameObject InstantiateCanopy()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(asset, Is.Not.Null);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, preview);
            instance.transform.SetParent(canvas, false);
            return instance;
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

        private static void Sample(GameObject canopy, float time)
        {
            var animation = canopy.GetComponent<Animation>();
            Assert.That(animation.Play(), Is.True);
            AnimationState state = animation[animation.clip.name];
            Assert.That(state, Is.Not.Null);
            state.time = time;
            state.weight = 1;
            animation.Sample();
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(animation.clip))
            {
                var channel = canopy.transform.Find(binding.path).GetComponent<SpiritPortraitDeformationChannel>();
                var serialized = new SerializedObject(channel);
                float actual = serialized.FindProperty(binding.propertyName).floatValue;
                float expected = AnimationUtility.GetEditorCurve(animation.clip, binding).Evaluate(time);
                Assert.That(actual, Is.EqualTo(expected).Within(.001f),
                    $"Native animation must update scalar property {binding.path}/{binding.propertyName} before applying the image weight.");
                var image = (SpiritPortraitImage)serialized.FindProperty("image").objectReferenceValue;
                Assert.That(image.Weights[serialized.FindProperty("shapeIndex").intValue], Is.EqualTo(expected).Within(.001f),
                    "The native animation callback must forward its scalar value to the image.");
            }
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
    }
}
