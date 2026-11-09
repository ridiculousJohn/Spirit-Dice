using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Dice.Tests
{
    /**
     * Renders the application's portrait canvas through a capture camera for [Explicit] [Category("Capture")] fixtures:
     * a single still, or a recording of one frame per 1/30 s tick of real time with the game's mixed audio. The output is the 540 x 960 authoring size
     * times a whole scale, so scale 2 writes 1080 x 1920; a still may frame a narrower portrait width in canvas units, so a
     * 443 wide still at scale 1 writes 443 x 960. The canvas renders at that pixel size with its scaler disabled
     * and a scale factor equal to the scale, so its layout in canvas units is the same at every scale. The tray layer is
     * left out of the capture camera, as the tray is shown through its own render texture on the canvas.
     */
    internal static class PortraitCapture
    {
        internal const int Width = 540, Height = 960, FramesPerSecond = 30;
        // A still pixel is lit when a channel is above this value; an unrendered canvas leaves the camera's dark background.
        private const int LitChannel = 45;
        // A written still must render: more than its pixel count over this divisor are lit.
        internal const int StillLitDivisor = 20;
        // Real seconds a still waits for its ready condition with the canvas bound.
        private const float ReadyTimeout = 15;

        /**
         * Lays the portrait out at the given width in canvas units (the 540 authoring width by default) and the 960
         * authoring height, centred on the canvas the capture camera frames.
         */
        internal static void FitPortrait(int width = Width)
        {
            var portrait = ContentReady.Portrait();
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(width, Height);
            portrait.anchoredPosition = Vector2.zero;
        }

        /**
         * Gives the shown encounter screen and its audio the focus the editor does not report. A test run's editor window is
         * not the focused window, so Application.isFocused is false; the encounter screen suspends its ambient activity and
         * the encounter audio plays nothing without focus, as they do when a player switches away. Each takes its focus from
         * Application.isFocused when it is enabled, so this is called once the screen is shown and before recording. It does
         * nothing when the application has focus, and returns whether it told the screen.
         */
        internal static bool PresentAsFocused(SpiritEncounterView view)
        {
            if (Application.isFocused) return false;
            Assert.That(view, Is.Not.Null, "Focus is given to a shown encounter screen.");
            var audio = view.GetComponentsInChildren<SpiritEncounterAudio>(true);
            Assert.That(audio, Has.Length.EqualTo(1), "The encounter screen has one encounter audio.");
            // SendMessage reaches every component of an object, so the audio is told separately only when it sits apart.
            view.SendMessage("OnApplicationFocus", true);
            if (audio[0].gameObject != view.gameObject) audio[0].SendMessage("OnApplicationFocus", true);
            return true;
        }

        /** Renders the portrait canvas once through a capture camera and writes it as a PNG of the authoring size times the scale. */
        internal static IEnumerator Still(string path, int scale) => Still(path, Width, scale);

        /**
         * Renders the portrait canvas once through a capture camera at the given width in canvas units and writes it as a PNG
         * of that width by the authoring height, times the scale. The still must render, and its file must not exist yet.
         */
        internal static IEnumerator Still(string path, int width, int scale) =>
            Render(width, scale, Path.GetFileName(path), null, StillLitDivisor, (readback, camera) => Write(path, readback));

        /**
         * Renders the portrait canvas once through a capture camera into a target of the given width in canvas units by the
         * authoring height, times the scale, and reads it back. With a ready condition, the canvas is bound first and the
         * condition polled once a frame for up to ReadyTimeout seconds, and the frame is rendered on the frame it holds. The
         * readback must have more than its pixel count over litDivisor lit pixels. Read then receives the readback, which is
         * destroyed after it returns, and the capture camera, whose screen points are readback pixels. The label names the
         * capture in failures. The canvas, the active render texture and sRGB writing are restored afterwards.
         */
        internal static IEnumerator Render(int width, int scale, string label, Func<bool> ready, int litDivisor,
            Action<Texture2D, Camera> read)
        {
            var binding = new CanvasBinding(width, scale, "Portrait Still Capture Camera");
            Texture2D readback = null;
            RenderTexture previousTarget = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                binding.Camera.enabled = false;
                Canvas.ForceUpdateCanvases();
                yield return null;
                float deadline = Time.realtimeSinceStartup + ReadyTimeout;
                while (ready != null && !ready() && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(ready == null || Time.realtimeSinceStartup < deadline, Is.True, label + " condition");
                Canvas.ForceUpdateCanvases();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = binding.Target };
                Assert.That(RenderPipeline.SupportsRenderRequest(binding.Camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(binding.Camera, request);
                RenderTexture.active = binding.Target;
                readback = new Texture2D(binding.Target.width, binding.Target.height, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, binding.Target.width, binding.Target.height), 0, 0);
                readback.Apply();
                int lit = readback.GetPixels32().Count(pixel => pixel.r > LitChannel || pixel.g > LitChannel || pixel.b > LitChannel);
                Assert.That(lit, Is.GreaterThan(readback.width * readback.height / litDivisor), label + ": the portrait must render.");
                read(readback, binding.Camera);
            }
            finally
            {
                RenderTexture.active = previousTarget; GL.sRGBWrite = previousSrgb;
                binding.Release();
                if (readback != null) UnityEngine.Object.Destroy(readback);
            }
        }

        /** Writes the pixels as a PNG at the path, which must not exist yet: capture output is fresh. */
        internal static void Write(string path, Texture2D pixels)
        {
            Assert.That(File.Exists(path), Is.False, "Capture output must be fresh.");
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }

        /**
         * Renders the portrait canvas through a capture camera into a target of the authoring size times the scale every
         * frame and reads back the first rendered frame of each 1/30 s tick of real time into frames/ under the root.
         * Readbacks are asynchronous and PNG encoding runs off the main thread, so recording does not slow the playback it
         * records. A tick with no rendered frame is counted as skipped; frames.csv records each written tick with the
         * caller's metrics, whose comma-separated column names the caller gives as its header. The audio listener's mixed
         * output is recorded over the same span into audio.wav, which starts at tick 0; audio-sync.txt describes its format,
         * its alignment and the application's focus while recording.
         */
        internal sealed class Recorder
        {
            private readonly string root;
            private readonly string frames;
            private readonly int scale;
            private readonly Func<string> metrics;
            private readonly StringBuilder csv;
            private readonly List<Task> writes = new List<Task>();
            private CanvasBinding binding;
            private PortraitAudioCapture audio;
            private string audioReport = "not recorded", focusAtBegin;
            private double start;
            private int lastTick = -1, written, skipped, failed;
            private string mark = string.Empty;
            private bool recording;

            internal Recorder(string root, int scale, string header, Func<string> metrics)
            {
                this.root = root;
                frames = Path.Combine(root, "frames");
                this.scale = scale;
                this.metrics = metrics;
                csv = new StringBuilder("tick,time," + header + ",mark\n");
            }

            internal float Elapsed => (float)(Time.realtimeSinceStartupAsDouble - start);

            internal string Report => written + " frames of " + Width * scale + " x " + Height * scale + " at " + FramesPerSecond +
                " fps over " + (lastTick + 1) + " ticks, " + skipped + " ticks without a rendered frame, " + failed + " failed readbacks; " +
                audioReport;

            internal void Mark(string value) => mark = mark.Length == 0 ? value : mark + " " + value;

            internal IEnumerator Begin()
            {
                Directory.CreateDirectory(frames);
                binding = new CanvasBinding(Width, scale, "Portrait Recording Capture Camera");
                var listener = UnityEngine.Object.FindAnyObjectByType<AudioListener>();
                Assert.That(listener, Is.Not.Null, "Recording captures the audio listener's mix.");
                audio = listener.gameObject.AddComponent<PortraitAudioCapture>();
                Canvas.ForceUpdateCanvases();
                yield return null;
                yield return null;
                focusAtBegin = Focus();
                start = Time.realtimeSinceStartupAsDouble;
                audio.Begin(Stopwatch.GetTimestamp());
                recording = true;
                RenderPipelineManager.endCameraRendering += Rendered;
            }

            private void Rendered(ScriptableRenderContext context, Camera rendered)
            {
                if (!recording || rendered != binding.Camera) return;
                int tick = (int)Math.Floor((Time.realtimeSinceStartupAsDouble - start) * FramesPerSecond);
                if (tick <= lastTick) return;
                skipped += tick - lastTick - 1;
                lastTick = tick;
                csv.Append(tick).Append(',').Append((tick / (double)FramesPerSecond).ToString("F3", CultureInfo.InvariantCulture))
                    .Append(',').Append(metrics()).Append(',').Append(mark).Append('\n');
                mark = string.Empty;
                string path = Path.Combine(frames, "frame_" + tick.ToString("D5", CultureInfo.InvariantCulture) + ".png");
                uint width = (uint)binding.Target.width, height = (uint)binding.Target.height;
                AsyncGPUReadback.Request(binding.Target, 0, TextureFormat.RGBA32, request =>
                {
                    if (request.hasError) { ++failed; return; }
                    byte[] pixels = request.GetData<byte>().ToArray();
                    ++written;
                    writes.Add(Task.Run(() => File.WriteAllBytes(path,
                        ImageConversion.EncodeArrayToPNG(pixels, GraphicsFormat.R8G8B8A8_SRGB, width, height))));
                });
            }

            internal void End()
            {
                if (recording)
                {
                    recording = false;
                    audioReport = "audio: " + audio.End(Path.Combine(root, "audio.wav"), Path.Combine(root, "audio-sync.txt"),
                        "At the start of recording " + focusAtBegin + "; at its end " + Focus() + ".");
                    RenderPipelineManager.endCameraRendering -= Rendered;
                    AsyncGPUReadback.WaitAllRequests();
                    Task.WaitAll(writes.ToArray());
                    File.WriteAllText(Path.Combine(root, "frames.csv"), csv.ToString());
                }
                binding?.Release();
                binding = null;
                if (audio != null) UnityEngine.Object.Destroy(audio);
                audio = null;
            }

            private static string Focus() => "Application.isFocused " + Application.isFocused + ", Application.runInBackground " +
                Application.runInBackground;
        }

        /**
         * Points the portrait canvas at a capture camera rendering into a target of the given width in canvas units by the
         * authoring height, times the scale, and gives the canvas its render mode, camera, plane distance, scaler and scale
         * factor back on release.
         */
        private sealed class CanvasBinding
        {
            private readonly Canvas canvas;
            private readonly CanvasScaler scaler;
            private readonly bool scalerEnabled;
            private readonly RenderMode previousMode;
            private readonly Camera previousCamera;
            private readonly float previousPlane, previousScale;
            private readonly GameObject cameraRoot;

            internal Camera Camera { get; }
            internal RenderTexture Target { get; }

            internal CanvasBinding(int width, int scale, string cameraName)
            {
                Assert.That(scale, Is.GreaterThanOrEqualTo(1), "Captures render at a whole multiple of the authoring size.");                canvas = ContentReady.PresentationRoot().GetComponentInChildren<Canvas>(true);
                scaler = canvas.GetComponent<CanvasScaler>();
                scalerEnabled = scaler != null && scaler.enabled;
                previousMode = canvas.renderMode;
                previousCamera = canvas.worldCamera;
                previousPlane = canvas.planeDistance;
                previousScale = canvas.scaleFactor;
                cameraRoot = new GameObject(cameraName);
                Camera = cameraRoot.AddComponent<Camera>();
                Target = new RenderTexture(width * scale, Height * scale, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Target.Create();
                Camera.transform.position = new Vector3(0, 0, -100);
                Camera.orthographic = true;
                Camera.orthographicSize = Height * scale / 2f;
                Camera.nearClipPlane = .1f; Camera.farClipPlane = 200;
                Camera.cullingMask = ~(1 << SpiritDiceTray.RenderLayer);
                Camera.backgroundColor = new Color(.04f, .055f, .05f, 1);
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.allowHDR = Camera.allowMSAA = false;
                Camera.targetTexture = Target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera;
                canvas.planeDistance = 1;
                if (scaler != null) scaler.enabled = false;
                canvas.scaleFactor = scale;
            }

            internal void Release()
            {
                canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; canvas.planeDistance = previousPlane;
                if (scaler != null) scaler.enabled = scalerEnabled;
                if (scaler == null || !scalerEnabled) canvas.scaleFactor = previousScale;
                Camera.targetTexture = null;
                Target.Release();
                UnityEngine.Object.Destroy(Target);
                UnityEngine.Object.Destroy(cameraRoot);
            }
        }
    }
}
