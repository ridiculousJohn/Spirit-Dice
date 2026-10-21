using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dice.Tests
{
    public sealed class MerchantMotionTests
    {
        private const string AssetPath = "Assets/Dice/Content/Presentation/Ambient/MerchantHost.asset";
        private const string ScenePath = "Assets/Scenes/AmbientMotionPreview.unity";
        private static readonly Vector2 SourceSize = new Vector2(941, 1672);
        private Scene preview;
        private SpiritHostPresentationAsset asset;
        private SpiritHostPortrait host;

        [SetUp]
        public void SetUp()
        {
            preview = EditorSceneManager.NewPreviewScene();
            asset = AssetDatabase.LoadAssetAtPath<SpiritHostPresentationAsset>(AssetPath);
            Assert.That(asset, Is.Not.Null);
            Assert.DoesNotThrow(asset.Validate);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(asset.PortraitPrefab.gameObject, preview);
            host = root.GetComponent<SpiritHostPortrait>();
            host.Build((RectTransform)root.transform, asset);
        }

        [TearDown]
        public void TearDown()
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }

        [Test]
        public void AuthoredMotionMovesHandsAndHeadWhileKeepingAnatomicalAttachmentsFixed()
        {
            var moving = new[] { "Head", "Resting Arm", "Supporting Arm" };
            var probes = new[] { new Vector2(350, 600), new Vector2(315, 977), new Vector2(268, 680) };
            var maxima = new float[moving.Length];
            float independence = 0;
            for (int frame = 0; frame <= 24; ++frame)
            {
                host.Sample(frame * .5f);
                var displacements = new Vector2[moving.Length];
                for (int part = 0; part < moving.Length; ++part)
                {
                    SpiritPortraitImage image = host.GetLayer(moving[part]);
                    Assert.That(image.Weights, Is.Not.Empty, moving[part]);
                    displacements[part] = Displacement(image, probes[part]);
                    maxima[part] = Mathf.Max(maxima[part], displacements[part].magnitude);
                }
                independence = Mathf.Max(independence, Vector2.Distance(displacements[0], displacements[2]));
                AssertAttachmentsAndTopology();
            }
            for (int part = 0; part < moving.Length; ++part)
                Assert.That(maxima[part], Is.GreaterThan(.25f), moving[part] + " must visibly move within the idle loop.");
            Assert.That(independence, Is.GreaterThan(.25f), "The hand and head must have independent motion.");
            SampleState(SpiritPresentationState.Reaction, .55f / host.GestureDuration);
            SpiritPortraitImage hand = host.GetLayer("Supporting Arm");
            var first = new Vector2(245, 660);
            var second = new Vector2(285, 680);
            float originalDistance = Vector2.Distance(first, second);
            float deformedDistance = Vector2.Distance(new Vector2(first.x, -first.y) + Displacement(hand, first),
                new Vector2(second.x, -second.y) + Displacement(hand, second));
            // Blending identity with an 8-degree rotation contracts every chord by this uniform scale.
            float weight = hand.Weights[0];
            float scale = Mathf.Sqrt(1 - 2 * weight * (1 - weight) * (1 - Mathf.Cos(8 * Mathf.Deg2Rad)));
            Assert.That(deformedDistance, Is.EqualTo(originalDistance * scale).Within(.003f),
                "Releasing the supporting hand must retain its uniformly blended proportions.");

            foreach (SpiritPresentationState state in new[] { SpiritPresentationState.Reaction,
                SpiritPresentationState.Victory, SpiritPresentationState.Defeat, SpiritPresentationState.Farewell })
            {
                Assert.That(asset.Sequences.Any(sequence => sequence.State == state && sequence.Tracks.Length > 0), Is.True,
                    state + " requires an authored performance.");
                Assert.That(host.GetDuration(state), Is.GreaterThan(0), state.ToString());
                for (int frame = 0; frame <= 60; ++frame)
                {
                    SampleState(state, frame / 60f);
                    AssertAttachmentsAndTopology();
                }
            }
        }

        [Test]
        public void IdleLoopAndReactionTransitionsJoinContinuously()
        {
            foreach (SpiritPresentationSequence sequence in asset.Sequences.Where(sequence => sequence.State == SpiritPresentationState.Idle))
            {
                Assert.That(sequence.Loop, Is.True, sequence.Id);
                foreach (SpiritPortraitTrack track in sequence.Tracks)
                {
                    Assert.That(track.Curve.Evaluate(sequence.Duration), Is.EqualTo(track.Curve.Evaluate(0)).Within(.00001f), sequence.Id);
                    const float step = .001f;
                    float initialVelocity = (track.Curve.Evaluate(step) - track.Curve.Evaluate(0)) / step;
                    float finalVelocity = (track.Curve.Evaluate(sequence.Duration) - track.Curve.Evaluate(sequence.Duration - step)) / step;
                    Assert.That(finalVelocity, Is.EqualTo(initialVelocity).Within(.01f), sequence.Id + " loop velocity");
                }
            }
            host.Sample(1.17f);
            float[] entry = Pose();
            host.React();
            AssertPoseNear(entry, .001f, "Reaction entry must retain the sampled idle pose.");
            host.Advance(host.GestureDuration - .0001f);
            float[] exit = Pose();
            host.Advance(.0002f);
            Assert.That(host.State, Is.EqualTo(SpiritPresentationState.Idle));
            AssertPoseNear(exit, .05f, "Reaction completion must rejoin idle without a pose jump.");
            foreach (SpiritPresentationState state in new[] { SpiritPresentationState.Victory,
                SpiritPresentationState.Defeat, SpiritPresentationState.Farewell })
            {
                host.Sample(2.37f);
                entry = Pose();
                host.SetState(state, 0);
                AssertPoseNear(entry, .001f, state + " entry must preserve the current pose.");
            }
        }

        [TestCase(941, 1672)]
        [TestCase(540, 960)]
        public void ActualPreviewSceneRendersIdleReactionAndDirectedPerformances(int width, int height)
        {
            EditorSceneManager.ClosePreviewScene(preview);
            preview = EditorSceneManager.OpenPreviewScene(ScenePath);
            GameObject[] roots = preview.GetRootGameObjects();
            Canvas canvas = roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).Single();
            Camera camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single();
            SceneLightingPresentation lighting = canvas.GetComponentInChildren<SceneLightingPresentation>(true);
            Assert.That(lighting, Is.Not.Null);
            host = canvas.GetComponentsInChildren<SpiritHostPortrait>(true).Single(candidate =>
                candidate.GetComponentsInChildren<SpiritPortraitImage>(true).Any(image => image.name == "Supporting Arm"));
            Assert.That(host.gameObject.activeInHierarchy, Is.True);
            lighting.ApplyAtTime(0);
            Assert.That(lighting.State.Asset, Is.Not.Null, "Capture the scene through its authored lighting presentation.");
            string output = CaptureDirectory(width, height);
            var baseline = new Dictionary<SpiritPresentationState, Color32[]>();
            var changed = new Dictionary<SpiritPresentationState, bool>();
            for (int frame = 0; frame <= 24; ++frame)
            {
                host.Sample(frame * .5f);
                Capture("idle-" + frame.ToString("D2"), SpiritPresentationState.Idle);
            }
            foreach (SpiritPresentationState state in new[] { SpiritPresentationState.Reaction,
                SpiritPresentationState.Victory, SpiritPresentationState.Defeat, SpiritPresentationState.Farewell })
                for (int frame = 0; frame <= 12; ++frame)
                {
                    SampleState(state, frame / 12f);
                    Capture(state.ToString().ToLowerInvariant() + "-" + frame.ToString("D2"), state);
                }
            foreach (var state in changed)
                Assert.That(state.Value, Is.True, state.Key + " must change the rendered merchant, independent of background lighting.");
            if (width == 540 && output != null && Environment.GetEnvironmentVariable("DICE_MERCHANT_MOTION_CAPTURE") == "1")
            {
                foreach (SpiritPresentationState state in new[] { SpiritPresentationState.Idle, SpiritPresentationState.Reaction })
                {
                    string frames = Path.Combine(output, "motion", state.ToString().ToLowerInvariant());
                    Directory.CreateDirectory(frames);
                    int count = Mathf.RoundToInt((state == SpiritPresentationState.Idle ? 12 : host.GestureDuration) * 12);
                    for (int frame = 0; frame < count; ++frame)
                    {
                        if (state == SpiritPresentationState.Idle) host.Sample(frame / 12f);
                        else SampleState(state, frame / (float)count);
                        RenderPose(camera, canvas, lighting, host, width, height, frames, "frame-" + frame.ToString("D4"), true);
                    }
                }
            }
            if (output != null) TestContext.WriteLine("Merchant motion captures: " + output);

            void Capture(string name, SpiritPresentationState state)
            {
                AssertAttachmentsAndTopology();
                Color32[] pixels = RenderPose(camera, canvas, lighting, host, width, height, output, name);
                if (!baseline.ContainsKey(state))
                {
                    baseline.Add(state, pixels);
                    changed.Add(state, false);
                }
                else if (!changed[state])
                    changed[state] = pixels.Where((pixel, index) => !pixel.Equals(baseline[state][index])).Any();
            }
        }

        private void SampleState(SpiritPresentationState state, float progress)
        {
            host.Sample(0);
            if (state == SpiritPresentationState.Reaction)
            {
                host.React();
                host.Advance(host.GestureDuration * progress);
            }
            else host.SetState(state, progress);
        }

        private void AssertAttachmentsAndTopology()
        {
            AssertPinned("Supporting Arm", new Vector2(165, 924));
            AssertPinned("Torso", new Vector2(220, 660), new Vector2(160, 750));
            AssertPinned("Resting Arm", new Vector2(500, 680), new Vector2(570, 955));
            AssertPinned("Head", new Vector2(370, 770));
            foreach (SpiritPortraitLayer layer in asset.Layers)
            {
                SpiritPortraitMeshAsset mesh = layer.Mesh;
                if (mesh == null) continue;
                SpiritPortraitImage image = host.GetLayer(layer.Id);
                Assert.That(image.Weights.Count, Is.EqualTo(mesh.Shapes.Length), layer.Id);
                var vertices = new Vector2[(mesh.Columns + 1) * (mesh.Rows + 1)];
                int invalidVertices = 0;
                for (int row = 0; row <= mesh.Rows; ++row)
                    for (int column = 0; column <= mesh.Columns; ++column)
                    {
                        var uv = new Vector2((float)column / mesh.Columns, (float)row / mesh.Rows);
                        Vector2 point = uv + image.EvaluateDisplacement(uv, Vector2.one);
                        if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.x) || float.IsInfinity(point.y))
                            ++invalidVertices;
                        vertices[row * (mesh.Columns + 1) + column] = point;
                    }
                Assert.That(invalidVertices, Is.Zero, layer.Id + " must produce finite mesh vertices.");
                float minimumArea = .01f / (mesh.Columns * mesh.Rows);
                float largestArea = float.NegativeInfinity;
                for (int row = 0; row < mesh.Rows; ++row)
                    for (int column = 0; column < mesh.Columns; ++column)
                    {
                        int first = row * (mesh.Columns + 1) + column;
                        int upper = first + mesh.Columns + 1;
                        largestArea = Mathf.Max(largestArea, Mathf.Max(
                            Cross(vertices[upper] - vertices[first], vertices[upper + 1] - vertices[first]),
                            Cross(vertices[first + 1] - vertices[upper + 1], vertices[first] - vertices[upper + 1])));
                    }
                Assert.That(largestArea, Is.LessThan(-minimumArea), layer.Id + " triangles must retain their winding and area.");
            }
        }

        private void AssertPinned(string layer, params Vector2[] points)
        {
            foreach (Vector2 point in points)
                Assert.That(Displacement(host.GetLayer(layer), point).magnitude, Is.LessThan(.02f), layer + " attachment " + point);
        }

        private static Vector2 Displacement(SpiritPortraitImage image, Vector2 point)
            => image.EvaluateDisplacement(new Vector2(point.x / SourceSize.x, 1 - point.y / SourceSize.y), SourceSize);

        private static float Cross(Vector2 first, Vector2 second) => first.x * second.y - first.y * second.x;

        private float[] Pose()
        {
            var values = new List<float>();
            foreach (SpiritPortraitLayer layer in asset.Layers)
            {
                SpiritPortraitImage image = host.GetLayer(layer.Id);
                RectTransform rect = image.rectTransform;
                values.AddRange(new[] { rect.anchoredPosition.x, rect.anchoredPosition.y, rect.sizeDelta.x, rect.sizeDelta.y,
                    Mathf.DeltaAngle(0, rect.localEulerAngles.z), image.color.a });
                foreach (Vector2 point in new[] { new Vector2(350, 600), new Vector2(315, 977), new Vector2(268, 680) })
                {
                    Vector2 displacement = Displacement(image, point);
                    values.Add(displacement.x);
                    values.Add(displacement.y);
                }
            }
            return values.ToArray();
        }

        private void AssertPoseNear(float[] previous, float tolerance, string message)
        {
            float[] current = Pose();
            Assert.That(current.Length, Is.EqualTo(previous.Length));
            for (int index = 0; index < current.Length; ++index)
                Assert.That(current[index], Is.EqualTo(previous[index]).Within(tolerance), message + " value " + index);
        }

        private static string CaptureDirectory(int width, int height)
        {
            string output = Environment.GetEnvironmentVariable("DICE_MERCHANT_PREVIEW_OUTPUT");
            if (string.IsNullOrWhiteSpace(output)) return null;
            output = Path.Combine(output, width + "x" + height);
            Assert.That(Directory.Exists(output), Is.False, "Motion captures require a new output directory.");
            Directory.CreateDirectory(output);
            return output;
        }

        private static Color32[] RenderPose(Camera camera, Canvas canvas, SceneLightingPresentation lighting,
            SpiritHostPortrait portrait, int width, int height, string output, string name, bool motionFrame = false)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            CameraClearFlags clearFlags = camera.clearFlags;
            Color clearColor = camera.backgroundColor;
            RawImage[] images = canvas.GetComponentsInChildren<RawImage>(true);
            bool[] enabled = images.Select(image => image.enabled).ToArray();
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                camera.scene = portrait.gameObject.scene;
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.scaleFactor = height / 960f;
                Canvas.ForceUpdateCanvases();
                foreach (Animation animation in canvas.GetComponentsInChildren<Animation>())
                {
                    Assert.That(animation.Play(), Is.True);
                    AnimationState state = animation[animation.clip.name];
                    state.time = 0;
                    state.weight = 1;
                    animation.Sample();
                }
                lighting.ApplyAtTime(0);
                Canvas.ForceUpdateCanvases();
                RectInt crop = MerchantCrop(camera, portrait.GetLayer("Head").rectTransform, width, height);
                camera.Render();
                Read();
                if (output != null)
                {
                    File.WriteAllBytes(Path.Combine(output, name + (motionFrame ? ".png" : "-scene.png")), pixels.EncodeToPNG());
                    if (!motionFrame) SaveCrop("-merchant.png");
                }
                for (int index = 0; index < images.Length; ++index)
                    images[index].enabled = enabled[index] && images[index].transform.IsChildOf(portrait.transform);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.Render();
                Read();
                Color32[] isolated = pixels.GetPixels32();
                Assert.That(isolated.Any(pixel => pixel.a > 0), Is.True, "The isolated merchant must remain visible.");
                Assert.That(isolated.Any(pixel => pixel.a == 0), Is.True, "Isolated captures must retain transparency.");
                int interiorHoles = 0;
                RectTransform torso = portrait.GetLayer("Torso").rectTransform;
                for (int y = 760; y <= 910; y += 10)
                    for (int x = 320; x <= 430; x += 10)
                    {
                        Vector3 point = camera.WorldToScreenPoint(torso.TransformPoint(new Vector3(
                            torso.rect.xMin + x / 941f * torso.rect.width,
                            torso.rect.yMax - y / 1672f * torso.rect.height, 0)));
                        int column = Mathf.Clamp(Mathf.RoundToInt(point.x), 0, width - 1);
                        int row = Mathf.Clamp(Mathf.RoundToInt(point.y), 0, height - 1);
                        if (isolated[row * width + column].a < 240) ++interiorHoles;
                    }
                Assert.That(interiorHoles, Is.Zero, name + " must retain opaque neck and shirt coverage beneath moving parts.");
                foreach (Vector2 overlap in new[] { new Vector2(294, 713), new Vector2(300, 720), new Vector2(306, 728) })
                {
                    Vector3 point = camera.WorldToScreenPoint(torso.TransformPoint(new Vector3(
                        torso.rect.xMin + overlap.x / 941 * torso.rect.width,
                        torso.rect.yMax - overlap.y / 1672 * torso.rect.height, 0)));
                    int column = Mathf.Clamp(Mathf.RoundToInt(point.x), 0, width - 1);
                    int row = Mathf.Clamp(Mathf.RoundToInt(point.y), 0, height - 1);
                    Assert.That(isolated[row * width + column].a, Is.GreaterThanOrEqualTo(240),
                        name + " must retain the concealed collar beneath the released cheek at " + overlap);
                }
                if (output != null && !motionFrame) SaveCrop("-alpha.png");
                return isolated;

                void Read()
                {
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    pixels.Apply();
                }

                void SaveCrop(string suffix)
                {
                    var closeup = new Texture2D(crop.width, crop.height, TextureFormat.RGBA32, false);
                    try
                    {
                        closeup.SetPixels(pixels.GetPixels(crop.x, crop.y, crop.width, crop.height));
                        closeup.Apply();
                        File.WriteAllBytes(Path.Combine(output, name + suffix), closeup.EncodeToPNG());
                    }
                    finally { Object.DestroyImmediate(closeup); }
                }
            }
            finally
            {
                for (int index = 0; index < images.Length; ++index) images[index].enabled = enabled[index];
                camera.clearFlags = clearFlags;
                camera.backgroundColor = clearColor;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }

        private static RectInt MerchantCrop(Camera camera, RectTransform frame, int width, int height)
        {
            Vector3 lower = camera.WorldToScreenPoint(frame.TransformPoint(new Vector3(
                frame.rect.xMin + 120f / 941 * frame.rect.width, frame.rect.yMax - 1120f / 1672 * frame.rect.height, 0)));
            Vector3 upper = camera.WorldToScreenPoint(frame.TransformPoint(new Vector3(
                frame.rect.xMin + 680f / 941 * frame.rect.width, frame.rect.yMax - 430f / 1672 * frame.rect.height, 0)));
            int x = Mathf.Clamp(Mathf.FloorToInt(lower.x), 0, width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(lower.y), 0, height - 1);
            return new RectInt(x, y, Mathf.Clamp(Mathf.CeilToInt(upper.x), x + 1, width) - x,
                Mathf.Clamp(Mathf.CeilToInt(upper.y), y + 1, height) - y);
        }
    }
}
