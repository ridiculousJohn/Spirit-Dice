using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dice.Tests
{
    /** Deformed portrait images: masked coverage, shape weights, native animation channels and mesh alpha, on synthetic rigs. */
    public sealed class DeformedPortraitImageTests
    {
        private static readonly Vector2 DisplaySize = SpiritPortraitSafeArea.DefaultReferenceSize;
        private static readonly Vector2 ImageSize = new Vector2(200, 100);
        private readonly List<Object> created = new List<Object>();
        private Scene preview;
        private RectTransform canvas;

        [SetUp]
        public void SetUp()
        {
            preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Image Test Canvas", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, preview);
            canvas = (RectTransform)root.transform;
            canvas.sizeDelta = DisplaySize;
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        }

        [TearDown]
        public void TearDown()
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            foreach (Object value in created)
                if (value != null) Object.DestroyImmediate(value);
            created.Clear();
        }

        [TestCase("Assets/Dice/Presentation/Shared/SourceMaskedImage.shader")]
        [TestCase("Assets/Dice/Presentation/Shared/SceneLitImage.shader")]
        public void FractionalMaskCoverageMatchesDefaultUiAlphaBlending(string shaderPath)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            Assert.That(shader, Is.Not.Null, shaderPath);
            var material = new Material(shader);
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
                mask.SetPixels32(new[] { new Color32(255, 255, 255, 0) });
                mask.Apply();
                Color32 unselected = Capture();
                image.material = null;
                image.texture = reference;
                Color32 expected = Capture();
                Assert.That(expected.r, Is.GreaterThan(background.r + 20), "The reference image must render visibly over the opaque background.");
                Assert.That(actual.r, Is.EqualTo(expected.r).Within(2));
                Assert.That(actual.g, Is.EqualTo(expected.g).Within(2));
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(2));
                Assert.That(unselected.r, Is.EqualTo(background.r).Within(1), "A pixel outside the mask draws nothing.");
                Assert.That(unselected.g, Is.EqualTo(background.g).Within(1), "A pixel outside the mask draws nothing.");
                Assert.That(unselected.b, Is.EqualTo(background.b).Within(1), "A pixel outside the mask draws nothing.");

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
        public void NativeAnimationSampleForwardsEachScalarChannelToItsImageWeightAndMesh()
        {
            SpiritPortraitMeshAsset mesh = SyntheticMesh();
            SpiritPortraitImage image = Rig(mesh);
            var curves = new[] { AnimationCurve.Linear(0f, 0f, 2f, 1f), AnimationCurve.Linear(0f, .5f, 2f, -.5f) };
            var clip = Own(new AnimationClip { legacy = true, name = "Synthetic Flutter", wrapMode = WrapMode.Loop });
            for (int channel = 0; channel < curves.Length; ++channel)
                clip.SetCurve("Image/" + mesh.Shapes[channel].Id, typeof(SpiritPortraitDeformationChannel), "weight", curves[channel]);
            var animation = image.transform.parent.gameObject.AddComponent<Animation>();
            animation.AddClip(clip, clip.name);
            animation.clip = clip;
            Vector3[] rest = Vertices(image);

            const float time = 1.5f;
            Assert.That(animation.Play(), Is.True);
            AnimationState state = animation[clip.name];
            Assert.That(state, Is.Not.Null);
            state.time = time;
            state.weight = 1;
            animation.Sample();

            float[] expected = curves.Select(curve => curve.Evaluate(time)).ToArray();
            for (int channel = 0; channel < curves.Length; ++channel)
                Assert.That(image.Weights[channel], Is.EqualTo(expected[channel]).Within(.001f),
                    "The channel forwards its animated scalar to its own image weight.");
            // The upper-left grid vertex moves by the weighted sum of the shape offsets, scaled to the image.
            Vector2 offset = (mesh.Shapes[0].Offsets[2] * expected[0] + mesh.Shapes[1].Offsets[2] * expected[1]) * ImageSize;
            Vector3 moved = Vertices(image)[2] - rest[2];
            Assert.That(Vector2.Distance(moved, offset), Is.LessThan(.01f), "The rendered mesh follows the weights.");
        }

        [Test]
        public void ImagesSharingAMeshAssetKeepIndependentWeightsAndVertices()
        {
            SpiritPortraitMeshAsset mesh = SyntheticMesh();
            SpiritPortraitImage first = Rig(mesh), second = Rig(mesh);
            Vector3[] rest = Vertices(second);

            first.SetWeights(new[] { 1f, .5f });

            Assert.That(second.Weights.All(weight => weight == 0f), Is.True, "Weights belong to each image.");
            Assert.That(MaxDistance(rest, Vertices(second)), Is.LessThan(.0001f), "The other image keeps its rest mesh.");
            Assert.That(MaxDistance(rest, Vertices(first)), Is.GreaterThan(1f), "The weighted image deforms.");
        }

        [Test]
        public void SingleWeightUpdatesChangeOnlyTheirChannelAndRejectInvalidInputsWithoutMutation()
        {
            var image = Rig(SyntheticMesh(), false);
            var mesh = Own(ScriptableObject.CreateInstance<SpiritPortraitMeshAsset>());
            mesh.Shapes = Enumerable.Range(0, 3).Select(index => new SpiritPortraitShape
                { Id = "Shape" + index, Offsets = new Vector2[4] }).ToArray();
            image.Configure(mesh);
            float[] expected = { .1f, .2f, .3f };
            image.SetWeights(expected);
            expected[1] = -.5f;
            image.SetWeight(1, expected[1]);
            Assert.That(image.Weights.ToArray(), Is.EqualTo(expected));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => image.SetWeight(-1, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => image.SetWeight(3, 0));
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.Throws<System.ArgumentException>(() => image.SetWeight(2, invalid));
            Assert.That(image.Weights.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void DisplacementBlendsTheWeightedShapesAcrossTheGrid()
        {
            var mesh = Own(ScriptableObject.CreateInstance<SpiritPortraitMeshAsset>());
            mesh.Shapes = new[]
            {
                new SpiritPortraitShape { Id = "Lift", Offsets = new[] { Vector2.zero, Vector2.zero, new Vector2(.1f, .2f), new Vector2(.1f, .2f) } }
            };
            SpiritPortraitImage image = Rig(mesh, false);
            var size = new Vector2(120f, 80f);
            image.SetWeights(new[] { .5f });
            Assert.That(image.EvaluateDisplacement(new Vector2(.5f, 0f), size), Is.EqualTo(Vector2.zero), "The unshaped lower edge stays planted.");
            Assert.That(image.EvaluateDisplacement(new Vector2(.5f, 1f), size), Is.EqualTo(new Vector2(6f, 8f)), "The upper edge moves by the weighted offset.");
            image.SetWeights(new[] { 1f });
            Assert.That(image.EvaluateDisplacement(new Vector2(.5f, .5f), size), Is.EqualTo(new Vector2(6f, 8f)), "Halfway up the grid moves half the offset.");
        }

        [Test]
        public void MeshAlphaFadesVertexAlphaOfTheImageTint()
        {
            var faded = Own(ScriptableObject.CreateInstance<SpiritPortraitMeshAsset>());
            faded.Alpha = new[] { 0f, 0f, 1f, .5f };
            SpiritPortraitImage image = Rig(faded, false);
            const float tint = .8f;
            image.color = new Color(1f, 1f, 1f, tint);
            byte[] alphas = VertexAlphas(image);
            Assert.That(alphas[0], Is.Zero, "A vertex with zero mesh alpha is transparent.");
            Assert.That(alphas[2], Is.EqualTo(tint * 255f).Within(1f), "Full mesh alpha keeps the tint's alpha.");
            Assert.That(alphas[3], Is.EqualTo(tint * .5f * 255f).Within(1f), "Partial mesh alpha scales the tint's alpha.");

            image.Configure(Own(ScriptableObject.CreateInstance<SpiritPortraitMeshAsset>()));
            Assert.That(VertexAlphas(image), Is.All.EqualTo(tint * 255f).Within(1f), "Without mesh alpha every vertex keeps the tint's alpha.");
        }

        /** A one-cell grid whose two shapes move the upper edge in different directions. */
        private SpiritPortraitMeshAsset SyntheticMesh()
        {
            var mesh = Own(ScriptableObject.CreateInstance<SpiritPortraitMeshAsset>());
            mesh.Shapes = new[]
            {
                new SpiritPortraitShape { Id = "Left", Offsets = new[] { Vector2.zero, Vector2.zero, new Vector2(-.1f, 0f), new Vector2(-.1f, 0f) } },
                new SpiritPortraitShape { Id = "Right", Offsets = new[] { Vector2.zero, Vector2.zero, new Vector2(.05f, .1f), new Vector2(.05f, .1f) } }
            };
            return mesh;
        }

        /** An image under a rig root, optionally with one deformation channel child per mesh shape, named after the shape. */
        private SpiritPortraitImage Rig(SpiritPortraitMeshAsset mesh, bool channels = true)
        {
            var root = new GameObject("Synthetic Rig", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, preview);
            root.transform.SetParent(canvas, false);
            var imageObject = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(SpiritPortraitImage));
            imageObject.transform.SetParent(root.transform, false);
            var image = imageObject.GetComponent<SpiritPortraitImage>();
            image.texture = Texture2D.whiteTexture;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = ImageSize;
            image.Configure(mesh);
            for (int index = 0; channels && index < mesh.Shapes.Length; ++index)
            {
                var channelObject = new GameObject(mesh.Shapes[index].Id);
                channelObject.transform.SetParent(imageObject.transform, false);
                var serialized = new SerializedObject(channelObject.AddComponent<SpiritPortraitDeformationChannel>());
                serialized.FindProperty("image").objectReferenceValue = image;
                serialized.FindProperty("shapeIndex").intValue = index;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            return image;
        }

        private T Own<T>(T value) where T : Object
        {
            created.Add(value);
            return value;
        }

        private static Vector3[] Vertices(SpiritPortraitImage image)
        {
            // Rebuild only consumes the dirty state set by the weights.
            image.Rebuild(CanvasUpdate.PreRender);
            UnityEngine.Mesh mesh = image.canvasRenderer.GetMesh();
            Assert.That(mesh, Is.Not.Null);
            return mesh.vertices;
        }

        /** The alpha of every grid vertex the image generates, in grid order. */
        private static byte[] VertexAlphas(SpiritPortraitImage image)
        {
            using var vertices = new VertexHelper();
            typeof(SpiritPortraitImage).GetMethod("OnPopulateMesh", BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(VertexHelper) }, null).Invoke(image, new object[] { vertices });
            var alphas = new byte[vertices.currentVertCount];
            for (int index = 0; index < alphas.Length; ++index)
            {
                UIVertex vertex = default;
                vertices.PopulateUIVertex(ref vertex, index);
                alphas[index] = vertex.color.a;
            }
            return alphas;
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
