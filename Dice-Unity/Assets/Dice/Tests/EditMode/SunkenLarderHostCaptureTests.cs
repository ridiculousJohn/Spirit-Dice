using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
    /**
     * Review capture of the Sunken Larder's host in its art preview scene (Assets/Scenes/SunkenLarderPreview.unity),
     * rendered by Unity in batch mode: the lit plate with the old woman's host at the hearth fire's resting level (light
     * 0 sampled where its flicker is exactly 1), driven through the host state API. For idle and each state it renders
     * frames at evenly spaced times across the state, each at the concept's own scale (941 x 1672) with the face at 2x
     * (FaceRegion) and the table edge and hands at 2x (TableRegion), plus idle frames at each authored blink. It writes
     * PNGs and frames.txt (each frame's state, progress and layer values) under DICE_SPIRIT_CAPTURE_ROOT/sunken-larder-host
     * and is ignored without it; it is a capture, not a regression test. The fixture is explicit: it runs only when a
     * run selects it by name, never in a run of the whole suite.
     */
    [Explicit("Review capture tool; select it by name with DICE_SPIRIT_CAPTURE_ROOT set.")]
    [Category("Capture")]
    public sealed class SunkenLarderHostCaptureTests
    {
        private const string ScenePath = "Assets/Scenes/SunkenLarderPreview.unity";
        private const string HostPath = "Assets/Dice/Content/Presentation/SunkenLarder/SunkenLarderHost.asset";
        private const int FrameWidth = 941, FrameHeight = 1672;
        // Concept pixels, top-left origin.
        private static readonly RectInt FaceRegion = new RectInt(370, 360, 260, 280);
        private static readonly RectInt TableRegion = new RectInt(100, 720, 820, 220);
        // Frames per state, evenly spaced from its start to its end.
        private const int Frames = 9;
        // The idle loop's authored blinks (author-sunken-larder-host.py IDLE_BLINKS), sampled at their closed hold.
        private static readonly float[] IdleBlinks = { 1.9f, 6.35f, 9.2f, 14.8f, 17.55f };
        private const float BlinkHold = .06f;

        private Camera camera;
        private Canvas canvas;
        private SceneLightingPresentation lighting;
        private SpiritHostPortrait portrait;
        private double lightTime;
        private string root;
        private readonly StringBuilder log = new StringBuilder();

        [Test]
        public void SunkenLarderHostReviewCapture()
        {
            string capture = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(capture)) Assert.Ignore("Review capture only; set DICE_SPIRIT_CAPTURE_ROOT to write it.");
            root = Path.Combine(capture, "sunken-larder-host");
            Assert.That(Directory.Exists(root), Is.False, "Capture output must be fresh.");
            Directory.CreateDirectory(root);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var objects = scene.GetRootGameObjects();
                camera = objects.Select(o => o.GetComponentInChildren<Camera>(true)).Single(c => c != null);
                canvas = objects.Select(o => o.GetComponentInChildren<Canvas>(true)).Single(c => c != null);
                lighting = objects.Select(o => o.GetComponentInChildren<SceneLightingPresentation>(true)).Single(c => c != null);
                portrait = lighting.GetComponentInChildren<SpiritHostPortrait>(true);
                Assert.That(portrait, Is.Not.Null, "The plate holds the host.");
                Assert.That(portrait.GetLayer("Figure"), Is.Not.Null, "The host is built when the plate samples its lighting.");
                canvas.GetComponent<CanvasScaler>().enabled = false;
                lightTime = RestingLightTime(lighting.State.Asset.Lights[0]);
                log.AppendLine("Light 0 sample time " + lightTime.ToString("R", CultureInfo.InvariantCulture) + ", flicker factor "
                    + Flicker(lighting.State.Asset.Lights[0], lightTime).ToString("R", CultureInfo.InvariantCulture));
                foreach (SpiritPresentationState state in Enum.GetValues(typeof(SpiritPresentationState)))
                    log.AppendLine("Duration " + state + ": " + portrait.GetDuration(state).ToString("F3", CultureInfo.InvariantCulture));

                float idle = IdleLoop();
                for (int i = 0; i < Frames; ++i)
                {
                    float t = idle * i / Frames;
                    Shot("idle", i, "t" + t.ToString("00.00", CultureInfo.InvariantCulture), () => portrait.Sample(t));
                }
                for (int i = 0; i < IdleBlinks.Length; ++i)
                {
                    float t = IdleBlinks[i] + BlinkHold;
                    Shot("idle-blinks", i, "t" + t.ToString("00.00", CultureInfo.InvariantCulture), () => portrait.Sample(t));
                }
                RealTime("waiting", 2f * portrait.GetDuration(SpiritPresentationState.Waiting),
                    () => portrait.SetState(SpiritPresentationState.Waiting, 0f));
                RealTime("reaction", portrait.GetDuration(SpiritPresentationState.Reaction),
                    () => portrait.React());
                Directed("arrival", SpiritPresentationState.Arrival, true);
                Directed("victory", SpiritPresentationState.Victory, false);
                Directed("defeat", SpiritPresentationState.Defeat, false);
                Directed("farewell", SpiritPresentationState.Farewell, false);
                Shot("rest", 0, "idle-t0", () => { });
                portrait.SetVisible(true);
                LogTransitionJumps(idle);
                portrait.ResetPose();
            }
            finally
            {
                File.WriteAllText(Path.Combine(root, "frames.txt"), log.ToString());
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /** The idle loop's length: the longest idle sequence of the host asset. */
        private static float IdleLoop()
        {
            var host = AssetDatabase.LoadAssetAtPath<SpiritHostPresentationAsset>(HostPath);
            Assert.That(host, Is.Not.Null, HostPath);
            return host.Sequences.Where(s => s.State == SpiritPresentationState.Idle).Max(s => s.Duration);
        }

        /**
         * The jumps content validation measures for a host a table uses (Arrival from Waiting, Reaction start and end, each
         * outcome's entry), and Waiting's entry from Idle, each over HostTransitionJumps.IdlePhaseCount idle phases; values
         * above JumpFloor are listed, largest first, in frames.txt.
         */
        private void LogTransitionJumps(float idle)
        {
            var host = AssetDatabase.LoadAssetAtPath<SpiritHostPresentationAsset>(HostPath);
            var jumps = Dice.Editor.HostTransitionJumps.Measure(portrait, host).ToList();
            int layers = host.Layers.Length;
            var largest = new float[layers][];
            for (int index = 0; index < Dice.Editor.HostTransitionJumps.IdlePhaseCount; ++index)
            {
                float phase = idle * index / Dice.Editor.HostTransitionJumps.IdlePhaseCount;
                portrait.Sample(phase);
                float[][] before = Values(host);
                portrait.SetState(SpiritPresentationState.Waiting, 0f);
                float[][] after = Values(host);
                for (int layer = 0; layer < layers; ++layer)
                {
                    largest[layer] ??= new float[before[layer].Length];
                    for (int value = 0; value < before[layer].Length; ++value)
                        largest[layer][value] = Math.Max(largest[layer][value], Math.Abs(after[layer][value] - before[layer][value]));
                }
            }
            for (int layer = 0; layer < layers; ++layer)
                for (int value = 0; value < largest[layer].Length; ++value)
                {
                    var property = value < (int)SpiritPortraitProperty.Deformation ? (SpiritPortraitProperty)value : SpiritPortraitProperty.Deformation;
                    int channel = Math.Max(0, value - (int)SpiritPortraitProperty.Deformation);
                    jumps.Add(new Dice.Editor.HostTransitionJump("Waiting entry", layer, property, channel, largest[layer][value], 0f));
                }
            log.AppendLine("Transition jumps above " + JumpFloor.ToString(CultureInfo.InvariantCulture) + ":");
            foreach (var jump in jumps.Where(j => j.Jump > JumpFloor).OrderByDescending(j => j.Jump))
                log.AppendLine("  " + jump.Transition + ", layer " + host.Layers[jump.Layer].Id + ", "
                    + Dice.Editor.HostTransitionJumps.Describe(host, jump.Layer, jump.Property, jump.Channel) + ": "
                    + jump.Jump.ToString("F5", CultureInfo.InvariantCulture));
        }

        private const float JumpFloor = .0001f;

        private float[][] Values(SpiritHostPresentationAsset host)
        {
            var values = new float[host.Layers.Length][];
            for (int layer = 0; layer < values.Length; ++layer)
            {
                int channels = host.Layers[layer].Mesh.Shapes.Length;
                values[layer] = new float[(int)SpiritPortraitProperty.Deformation + channels];
                for (int value = 0; value < values[layer].Length; ++value)
                    values[layer][value] = value < (int)SpiritPortraitProperty.Deformation
                        ? portrait.ReadLayerValue(layer, (SpiritPortraitProperty)value)
                        : portrait.ReadLayerValue(layer, SpiritPortraitProperty.Deformation, value - (int)SpiritPortraitProperty.Deformation);
            }
            return values;
        }

        /** A real-time state entered from rest, then advanced to evenly spaced ages. */
        private void RealTime(string series, float span, Action enter)
        {
            for (int i = 0; i < Frames; ++i)
            {
                float t = span * i / (Frames - 1);
                Shot(series, i, "t" + t.ToString("0.00", CultureInfo.InvariantCulture), () =>
                {
                    enter();
                    portrait.Advance(t);
                });
            }
        }

        /** A directed state entered from rest (Arrival from Waiting, as journeys enter it) with its progress raised in steps. */
        private void Directed(string series, SpiritPresentationState state, bool fromWaiting)
        {
            for (int i = 0; i < Frames; ++i)
            {
                float progress = (float)i / (Frames - 1);
                Shot(series, i, "p" + progress.ToString("0.000", CultureInfo.InvariantCulture), () =>
                {
                    if (fromWaiting)
                    {
                        portrait.SetState(SpiritPresentationState.Waiting, 0f);
                        portrait.Advance(2f * portrait.GetDuration(SpiritPresentationState.Waiting));
                    }
                    for (int step = 0; step <= i; ++step) portrait.SetState(state, (float)step / (Frames - 1));
                });
            }
        }

        private void Shot(string series, int index, string label, Action pose)
        {
            portrait.SetVisible(true);
            portrait.ResetPose();
            pose();
            lighting.ApplyAtTime(lightTime);
            string name = index.ToString("00") + "-" + label;
            string folder = Path.Combine(root, series);
            Directory.CreateDirectory(folder);
            Render(1f, null, Path.Combine(folder, name + ".png"));
            Render(2f, FaceRegion, Path.Combine(folder, name + "-face-2x.png"));
            Render(2f, TableRegion, Path.Combine(folder, name + "-table-2x.png"));
            var line = new StringBuilder(series + " " + name + ": state " + portrait.State + ", progress "
                + portrait.Progress.ToString("F4", CultureInfo.InvariantCulture));
            string[] layers = { "Figure", "Downcast", "Blink" };
            for (int layer = 0; layer < layers.Length; ++layer)
            {
                line.Append(", " + layers[layer] + " alpha " + portrait.ReadLayerValue(layer, SpiritPortraitProperty.Alpha).ToString("F4", CultureInfo.InvariantCulture));
                if (layer == 0)
                {
                    line.Append(" weights");
                    for (int channel = 0; channel < portrait.GetLayer("Figure").Weights.Count; ++channel)
                        line.Append(" " + portrait.ReadLayerValue(layer, SpiritPortraitProperty.Deformation, channel).ToString("F4", CultureInfo.InvariantCulture));
                }
            }
            log.AppendLine(line.ToString());
        }

        /** Renders the 540 x 960 plate at the concept's scale times `scale`, optionally only a concept-pixel region. */
        private void Render(float scale, RectInt? region, string path)
        {
            float pixels = scale * FrameWidth / 540f;
            int width = Mathf.RoundToInt(540 * pixels), height = Mathf.RoundToInt(960 * pixels);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D readback = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                canvas.scaleFactor = pixels;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                var rect = new RectInt(0, 0, width, height);
                if (region.HasValue)
                {
                    RectInt r = region.Value;
                    float sx = (float)width / FrameWidth, sy = (float)height / FrameHeight;
                    int x0 = Mathf.FloorToInt(r.xMin * sx), x1 = Mathf.CeilToInt(r.xMax * sx);
                    int y0 = Mathf.FloorToInt(r.yMin * sy), y1 = Mathf.CeilToInt(r.yMax * sy);
                    rect = new RectInt(x0, height - y1, x1 - x0, y1 - y0);
                }
                readback = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(rect.x, rect.y, rect.width, rect.height), 0, 0);
                readback.Apply();
                Assert.That(File.Exists(path), Is.False, "Capture output must be fresh.");
                File.WriteAllBytes(path, readback.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                if (readback != null) Object.DestroyImmediate(readback);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        private static float Flicker(SceneLightLayer light, double time)
            => SceneLightFlicker.EvaluateIntensity(time, 1f, light.FlickerAmount, light.FlickerSpeed, light.Seed);

        /** The first time the light's flicker passes exactly through 1, so it shows its resting level. */
        private static double RestingLightTime(SceneLightLayer light)
        {
            double a = 0, step = .01;
            float previous = Flicker(light, a) - 1f;
            for (double b = step; b < 60; b += step)
            {
                float value = Flicker(light, b) - 1f;
                if (previous == 0f) return a;
                if (Math.Sign(previous) != Math.Sign(value))
                {
                    double lo = a, hi = b;
                    for (int i = 0; i < 60; ++i)
                    {
                        double mid = (lo + hi) / 2;
                        if (Math.Sign(Flicker(light, mid) - 1f) == Math.Sign(previous)) lo = mid; else hi = mid;
                    }
                    return Math.Abs(Flicker(light, lo) - 1f) <= Math.Abs(Flicker(light, hi) - 1f) ? lo : hi;
                }
                a = b;
                previous = value;
            }
            throw new InvalidOperationException("The light's flicker never passes through 1.");
        }
    }
}
