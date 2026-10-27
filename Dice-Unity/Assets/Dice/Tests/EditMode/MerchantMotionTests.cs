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
    [Explicit("Art-generation check; run it by name while the merchant art is being changed.")]
    [Category("ArtAuthoring")]
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
        public void BreathingFieldMovesEveryLayerTogetherWhileTableContactsStayFixed()
        {
            // Living still: no layer moves on its own. Every track deforms through the one shared field,
            // apart from the blink overlay's opacity.
            foreach (SpiritPresentationSequence sequence in asset.Sequences)
                foreach (SpiritPortraitTrack track in sequence.Tracks)
                    Assert.That(track.Property == SpiritPortraitProperty.Deformation
                        || track.Property == SpiritPortraitProperty.Alpha && asset.Layers[track.Layer].Id == "Blink", Is.True,
                        sequence.Id + " must not move or fade " + asset.Layers[track.Layer].Id + " as a rigid part.");
            float idleDuration = asset.Sequences.Single(sequence => sequence.State == SpiritPresentationState.Idle).Duration;
            float chestRise = 0, shoulderRise = 0, headRise = 0;
            const float step = 1 / 60f;
            for (int frame = 0; frame * step < idleDuration; ++frame)
            {
                host.Sample(frame * step);
                if (frame % 6 == 0) AssertRegisteredAttachmentsAndTopology();
                chestRise = Mathf.Max(chestRise, Displacement(host.GetLayer("Torso"), Chest).y);
                shoulderRise = Mathf.Max(shoulderRise, Displacement(host.GetLayer("Torso"), Shoulder).y);
                headRise = Mathf.Max(headRise, Displacement(host.GetLayer("Head"), Eyes).y);
            }
            Assert.That(chestRise, Is.GreaterThan(0f), "The chest rises with each breath.");
            Assert.That(shoulderRise, Is.GreaterThan(0f), "The shoulders rise with the chest.");
            Assert.That(headRise, Is.GreaterThan(0f).And.LessThan(shoulderRise), "The head rides the breath but rises less than the shoulders.");

            float strongest = 0;
            for (int frame = 0; frame <= 60; ++frame)
            {
                host.Sample(frame / 60f * host.GestureDuration);
                float idleOnly = Displacement(host.GetLayer("Torso"), Chest).y;
                SampleState(SpiritPresentationState.Reaction, frame / 60f);
                strongest = Mathf.Max(strongest, Mathf.Abs(Displacement(host.GetLayer("Torso"), Chest).y - idleOnly));
                AssertRegisteredAttachmentsAndTopology();
            }
            Assert.That(strongest, Is.GreaterThan(0f), "The reaction moves the whole body beyond its idle breath.");

            foreach (SpiritPresentationState state in new[] { SpiritPresentationState.Arrival, SpiritPresentationState.Reaction,
                SpiritPresentationState.Victory, SpiritPresentationState.Defeat, SpiritPresentationState.Farewell })
            {
                Assert.That(asset.Sequences.Any(sequence => sequence.State == state && sequence.Tracks.Length > 0), Is.True,
                    state + " requires an authored performance.");
                for (int frame = 0; frame <= 60; ++frame)
                {
                    SampleState(state, frame / 60f);
                    AssertRegisteredAttachmentsAndTopology();
                }
            }
        }

        [Test]
        public void BlinksAreBriefIrregularAndHiddenAtRest()
        {
            SpiritPortraitImage blink = host.GetLayer("Blink");
            Assert.That(blink, Is.Not.Null);
            host.Sample(0);
            Assert.That(blink.color.a, Is.Zero, "The eyes are open at rest.");
            float idleDuration = asset.Sequences.Single(sequence => sequence.State == SpiritPresentationState.Idle).Duration;
            var starts = new List<float>();
            var lengths = new List<float>();
            float closedSince = -1, peak = 0;
            const float step = .002f;
            for (int frame = 0; frame * step < idleDuration; ++frame)
            {
                host.Sample(frame * step);
                float alpha = blink.color.a;
                foreach (string layer in new[] { "Torso", "Head", "Resting Arm", "Supporting Arm" })
                    Assert.That(host.GetLayer(layer).color.a, Is.EqualTo(1), layer + " stays opaque.");
                if (alpha >= .5f && closedSince < 0)
                {
                    closedSince = frame * step;
                    peak = 0;
                }
                peak = Mathf.Max(peak, alpha);
                if (alpha < .5f && closedSince >= 0)
                {
                    starts.Add(closedSince);
                    lengths.Add(frame * step - closedSince);
                    Assert.That(peak, Is.GreaterThan(.99f), "Each blink fully closes the eyes.");
                    closedSince = -1;
                }
            }
            Assert.That(closedSince, Is.LessThan(0), "No blink spans the loop seam.");
            Assert.That(starts.Count, Is.GreaterThan(1), "Uneven blinking needs more than one blink in the idle loop.");
            float[] gaps = starts.Select((start, index) => (index + 1 < starts.Count ? starts[index + 1] : starts[0] + idleDuration) - start).ToArray();
            foreach (float length in lengths)
                Assert.That(length, Is.LessThan(gaps.Min()), "Each blink is brief beside the open intervals between blinks.");
            Assert.That(gaps.Max(), Is.GreaterThan(gaps.Min()), "Blinks fall at uneven intervals.");
            foreach (float start in starts)
            {
                host.Sample(start + .03f);
                Assert.That(Vector2.Distance(Displacement(blink, Eyes), Displacement(host.GetLayer("Head"), Eyes)), Is.LessThan(.0001f),
                    "The closed eyes follow the breathing head.");
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
            if (width == 941 && output != null && Environment.GetEnvironmentVariable("DICE_MERCHANT_MOTION_CAPTURE") == "1")
            {
                // A 15 fps recording of one full idle loop, then a reaction that continues from the loop seam.
                const float rate = 15;
                float idleDuration = asset.Sequences.Single(sequence => sequence.State == SpiritPresentationState.Idle).Duration;
                foreach (SpiritPresentationState state in new[] { SpiritPresentationState.Idle, SpiritPresentationState.Reaction })
                {
                    string frames = Path.Combine(output, "motion", state.ToString().ToLowerInvariant());
                    Directory.CreateDirectory(frames);
                    int count = Mathf.RoundToInt((state == SpiritPresentationState.Idle ? idleDuration : host.GestureDuration) * rate);
                    for (int frame = 0; frame < count; ++frame)
                    {
                        if (state == SpiritPresentationState.Idle) host.Sample(frame / rate);
                        else SampleState(state, frame / (float)count);
                        RenderPose(camera, canvas, lighting, host, width, height, frames, "frame-" + frame.ToString("D4"), true);
                    }
                }
            }
            if (output != null) TestContext.WriteLine("Merchant motion captures: " + output);

            void Capture(string name, SpiritPresentationState state)
            {
                AssertRegisteredAttachmentsAndTopology();
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

        private static readonly Vector2 Chest = new Vector2(380, 820);
        private static readonly Vector2 Shoulder = new Vector2(520, 690);
        private static readonly Vector2 Eyes = new Vector2(330, 605);
        // Points where the forearms, elbows and hands rest on the table.
        private static readonly Vector2[] TableContacts = { new Vector2(120, 960), new Vector2(315, 977),
            new Vector2(570, 958), new Vector2(450, 985) };
        private static readonly Vector2[] FieldProbes = { new Vector2(320, 470), new Vector2(298, 614), new Vector2(262, 680),
            new Vector2(360, 735), new Vector2(150, 700), new Vector2(520, 670), new Vector2(380, 820),
            new Vector2(400, 900), new Vector2(165, 924), new Vector2(420, 950) };

        private void AssertRegisteredAttachmentsAndTopology()
        {
            SpiritPortraitImage torso = host.GetLayer("Torso");
            foreach (SpiritPortraitLayer layer in asset.Layers)
            {
                SpiritPortraitImage image = host.GetLayer(layer.Id);
                Assert.That(image.Weights, Is.EqualTo(torso.Weights), layer.Id + " must share the torso's breathing weight.");
                foreach (Vector2 point in FieldProbes)
                    Assert.That(Vector2.Distance(Displacement(image, point), Displacement(torso, point)), Is.LessThan(.0001f),
                        layer.Id + " must stay registered with the torso at " + point);
            }
            AssertAttachmentsAndTopology();
        }

        private void AssertAttachmentsAndTopology()
        {
            foreach (SpiritPortraitLayer layer in asset.Layers)
                AssertPinned(layer.Id, TableContacts);
            foreach (SpiritPortraitLayer layer in asset.Layers)
            {
                SpiritPortraitMeshAsset mesh = layer.Mesh;
                Assert.That(mesh, Is.Not.Null, layer.Id + " must deform with the shared breathing field.");
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

        private static string CaptureDirectory(int width, int height)
        {
            string output = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrWhiteSpace(output)) return null;
            output = Path.Combine(output, "merchant-motion", width + "x" + height);
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
                Assert.That(interiorHoles, Is.Zero, name + " must retain opaque neck and shirt coverage beneath the overlapping parts.");
                foreach (Vector2 overlap in new[] { new Vector2(294, 713), new Vector2(300, 720), new Vector2(306, 728) })
                {
                    Vector3 point = camera.WorldToScreenPoint(torso.TransformPoint(new Vector3(
                        torso.rect.xMin + overlap.x / 941 * torso.rect.width,
                        torso.rect.yMax - overlap.y / 1672 * torso.rect.height, 0)));
                    int column = Mathf.Clamp(Mathf.RoundToInt(point.x), 0, width - 1);
                    int row = Mathf.Clamp(Mathf.RoundToInt(point.y), 0, height - 1);
                    Assert.That(isolated[row * width + column].a, Is.GreaterThanOrEqualTo(240),
                        name + " must retain the concealed collar beneath the cheek and hand at " + overlap);
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
