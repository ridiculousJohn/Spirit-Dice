using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Dice.Tests
{
    /**
     * Review capture of the merchant encounter: lit host layers with the lantern on and off, the candle at its
     * neutral spot and on three bays, a poltergeist disturbance mid-motion, and close crops of the host and props.
     * It writes PNGs under DICE_SPIRIT_CAPTURE_ROOT and is ignored without it; it is a capture, not a regression test.
     */
    public sealed class MerchantReviewCapturePlayTests
    {
        private const string EncounterId = "encounter.merchant.unquiet_cart";
        private const int Width = 540, Height = 960, Zoom = 4;
        private const int Lantern = 0;
        private static readonly string[] MovingProps = { "HangingPot", "HangingPan", "ShelfBottle", "TrunkLid" };
        private static readonly string[] LeftProps = { "ShelfBottle", "TrunkLid" };
        private static readonly string[] RightProps = { "HangingPot", "HangingPan", "HangingCup" };
        // Head and shirt within the host's full-frame torso layer, bottom-left normalized.
        private static readonly Rect HostCrop = new Rect(.17f, .33f, .46f, .39f);

        private delegate RectInt Region(Camera camera, int width, int height);

        [UnityTest]
        public IEnumerator MerchantLightingReviewCapture()
        {
            string root = Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Review capture only; set DICE_SPIRIT_CAPTURE_ROOT to write it.");
            root = Path.Combine(root, "merchant-review");
            Assert.That(Directory.Exists(root), Is.False, "Capture output must be fresh.");
            Directory.CreateDirectory(root);

            yield return SceneManager.LoadSceneAsync("Application");
            yield return ContentReady.Spirit(false);
            var controller = UnityEngine.Object.FindAnyObjectByType<SpiritEncounterController>();
            var journey = controller.JourneyCatalog.Entries.Single(value => !value.IsStoryOnly && value.EncounterId == EncounterId);
            controller.ReturnToInvestigation();
            yield return ContentReady.Routed(controller);
            ContentReady.SeedJourneyPrerequisites(controller, journey.Id);
            controller.SelectEncounter(journey.EncounterId);
            yield return ContentReady.Routed(controller);
            while (controller.SelectedJourney.Beats[controller.BeatIndex].Kind != EncounterJourneyBeatKind.Invitation)
                controller.AdvanceJourney();
            controller.AcceptEncounter();
            yield return ContentReady.Routed(controller);
            var view = ContentReady.EncounterView(controller);
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(controller.IsBusy, Is.False);
            yield return null;
            var portrait = ContentReady.Portrait();
            portrait.anchorMin = portrait.anchorMax = new Vector2(.5f, .5f);
            portrait.sizeDelta = new Vector2(Width, Height);
            portrait.anchoredPosition = Vector2.zero;

            var activity = (EncounterAmbientActivity)typeof(SpiritEncounterView)
                .GetField("ambientActivity", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            Assert.That(activity, Is.Not.Null, "The merchant table authors ambient activity.");
            var lighting = (IAmbientLightingTarget)view;
            // A hidden editor reports no focus, which suspends the activity as it does the ambient audio.
            if (!Application.isFocused) view.SendMessage("OnApplicationFocus", true);
            var piece = view.TablePieces.Single();
            var torso = view.HostPortrait.GetComponentsInChildren<SpiritPortraitImage>(true).Single(image => image.name == "Torso");
            var transforms = view.GetComponentsInChildren<RectTransform>(true);
            RectTransform Prop(string name) => transforms.Single(rect => rect.name == name);
            Region host = (camera, width, height) => PixelRect(camera, width, height, torso.rectTransform, HostCrop);
            Region left = (camera, width, height) => PropRect(camera, width, height, LeftProps.Select(Prop));
            Region right = (camera, width, height) => PropRect(camera, width, height, RightProps.Select(Prop));
            foreach (string name in RightProps.Concat(LeftProps))
                Assert.That(Prop(name).gameObject.activeInHierarchy, Is.True, name + " is shown.");

            // (a) Lantern on, candle at its neutral spot; (e) host close crop; (f) props at rest.
            yield return Move(controller, piece, PiecePosition.Neutral);
            Rest(activity, lighting, 1f);
            yield return Capture(root, "a-lantern-on-candle-neutral.png", 1, null);
            yield return Capture(root, null, Zoom, null, ("e-host-lantern-on.png", host),
                ("f-props-left-rest-lantern-on.png", left), ("f-props-right-rest-lantern-on.png", right));

            // (b) Lantern multiplier zero, candle at neutral; (e) host close crop; props unlit.
            Rest(activity, lighting, 0f);
            yield return Capture(root, "b-lantern-off-candle-neutral.png", 1, null);
            yield return Capture(root, null, Zoom, null, ("e-host-lantern-off.png", host),
                ("f-props-left-rest-lantern-off.png", left), ("f-props-right-rest-lantern-off.png", right));

            // (c) Lantern on, candle on bays 1, 3 and 5.
            foreach (int bay in new[] { 0, 2, 4 })
            {
                yield return Move(controller, piece, PiecePosition.Bay(bay));
                Rest(activity, lighting, 1f);
                yield return Capture(root, "c-lantern-on-candle-bay" + (bay + 1) + ".png", 1, null);
                yield return Capture(root, null, Zoom, null, ("c-host-candle-bay" + (bay + 1) + ".png", host));
            }
            yield return Move(controller, piece, PiecePosition.Neutral);
            Rest(activity, lighting, 1f);

            // (d) A disturbance burst: repeated poltergeist disturbances until the lantern stutters dim while props move.
            var rest = MovingProps.ToDictionary(name => name, name => (Prop(name).localPosition, Prop(name).localRotation));
            int Displaced() => MovingProps.Count(name => (Prop(name).localPosition - rest[name].localPosition).magnitude > .2f
                || Quaternion.Angle(Prop(name).localRotation, rest[name].localRotation) > .2f);
            foreach (var shot in new[] { (File: "d-disturbance-burst.png", Zoom: 1), (File: (string)null, Zoom: Zoom) })
            {
                activity.Stop();
                activity.Begin(17 + shot.Zoom);
                int disturbances = 0;
                float next = 0f;
                Func<bool> burst = () =>
                {
                    bool ready = disturbances >= 3 && lighting.GetLightIntensity(Lantern) < .5f && Displaced() >= 2;
                    if (!ready && Time.realtimeSinceStartup >= next)
                    {
                        activity.Disturb();
                        ++disturbances;
                        next = Time.realtimeSinceStartup + .2f;
                    }
                    return ready;
                };
                if (shot.File != null) yield return Capture(root, shot.File, 1, burst);
                else yield return Capture(root, null, Zoom, burst, ("d-props-left-burst.png", left), ("d-props-right-burst.png", right));
                TestContext.WriteLine("Burst " + (shot.File ?? "props") + ": lantern multiplier " + lighting.GetLightIntensity(Lantern)
                    + ", displaced " + string.Join(", ", MovingProps.Select(name => name + " "
                        + (Prop(name).localPosition - rest[name].localPosition).ToString("F2") + " "
                        + Quaternion.Angle(Prop(name).localRotation, rest[name].localRotation).ToString("F2") + " deg")));
            }
            Rest(activity, lighting, 1f);
            yield return null;
            foreach (string name in MovingProps)
            {
                Assert.That(Vector3.Distance(Prop(name).localPosition, rest[name].localPosition), Is.LessThan(.001f), name + " rests.");
                Assert.That(Quaternion.Angle(Prop(name).localRotation, rest[name].localRotation), Is.LessThan(.01f), name + " rests.");
            }
            LogAssert.NoUnexpectedReceived();
        }

        /** Ends the activity at rest, then holds the lantern at the given multiplier of its authored level. */
        private static void Rest(EncounterAmbientActivity activity, IAmbientLightingTarget lighting, float lantern)
        {
            activity.Stop();
            lighting.SetLightIntensity(Lantern, lantern);
        }

        private static IEnumerator Move(SpiritEncounterController controller, SpiritTablePieceView piece, PiecePosition position)
        {
            if (piece.Position != position)
            {
                controller.InteractWithPiece(piece.PieceId, piece.InteractionId, new MovePieceInput(position));
                float deadline = Time.realtimeSinceStartup + 10;
                while ((controller.IsBusy || piece.Position != position) && Time.realtimeSinceStartup < deadline) yield return null;
            }
            Assert.That(piece.Position, Is.EqualTo(position), piece.PieceId);
            Vector3 previous = piece.transform.position;
            int stable = 0;
            float settle = Time.realtimeSinceStartup + 5;
            while (stable < 3 && Time.realtimeSinceStartup < settle)
            {
                yield return null;
                stable = (piece.transform.position - previous).sqrMagnitude < 1e-6f ? stable + 1 : 0;
                previous = piece.transform.position;
            }
            Assert.That(stable, Is.EqualTo(3), piece.PieceId + " must settle.");
        }

        /**
         * Renders the portrait canvas through a capture camera at the given pixel density of its 540 x 960 layout.
         * The frame is read once the condition holds; the full frame and each cropped region are written as PNGs.
         */
        private static IEnumerator Capture(string root, string filename, int zoom, Func<bool> ready,
            params (string File, Region Region)[] crops)
        {
            int width = Width * zoom, height = Height * zoom;
            var canvas = ContentReady.PresentationRoot().GetComponentInChildren<Canvas>(true);
            var scaler = canvas.GetComponent<CanvasScaler>();
            bool scalerEnabled = scaler != null && scaler.enabled;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            float previousPlane = canvas.planeDistance;
            float previousScale = canvas.scaleFactor;
            var cameraRoot = new GameObject("Merchant Review Capture Camera");
            var camera = cameraRoot.AddComponent<Camera>();
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Texture2D readback = null;
            RenderTexture previousTarget = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                target.Create();
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -100);
                camera.orthographic = true;
                camera.orthographicSize = Height / 2f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 200;
                camera.cullingMask = ~(1 << SpiritDiceTray.RenderLayer);
                camera.backgroundColor = new Color(.04f, .055f, .05f, 1);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.allowHDR = camera.allowMSAA = false;
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                // The layout stays 540 x 960 canvas units; a zoomed capture renders it at a higher pixel density.
                if (scaler != null) scaler.enabled = false;
                canvas.scaleFactor = zoom;
                Canvas.ForceUpdateCanvases();
                yield return null;
                float deadline = Time.realtimeSinceStartup + 15;
                while (ready != null && !ready() && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(ready == null || Time.realtimeSinceStartup < deadline, Is.True, (filename ?? crops[0].File) + " condition");
                Canvas.ForceUpdateCanvases();
                TestContext.WriteLine("Capture canvas " + canvas.pixelRect.size + " at scale " + canvas.scaleFactor);
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readback.Apply();
                int lit = readback.GetPixels32().Count(pixel => pixel.r > 45 || pixel.g > 45 || pixel.b > 45);
                Assert.That(lit, Is.GreaterThan(width * height / 10), "The portrait must render.");
                if (filename != null) Write(root, filename, readback);
                foreach (var crop in crops)
                {
                    RectInt region = crop.Region(camera, width, height);
                    var part = new Texture2D(region.width, region.height, TextureFormat.RGBA32, false);
                    try
                    {
                        part.SetPixels(readback.GetPixels(region.x, region.y, region.width, region.height));
                        part.Apply();
                        Write(root, crop.File, part);
                    }
                    finally { UnityEngine.Object.Destroy(part); }
                }
            }
            finally
            {
                canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; canvas.planeDistance = previousPlane;
                if (scaler != null) scaler.enabled = scalerEnabled;
                if (scaler == null || !scalerEnabled) canvas.scaleFactor = previousScale;
                RenderTexture.active = previousTarget; GL.sRGBWrite = previousSrgb;
                camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(cameraRoot);
                if (readback != null) UnityEngine.Object.Destroy(readback);
            }
            yield return null;
        }

        private static void Write(string root, string filename, Texture2D pixels)
        {
            string path = Path.Combine(root, filename);
            Assert.That(File.Exists(path), Is.False, "Capture output must be fresh.");
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            TestContext.WriteLine("Merchant review capture: " + path);
        }

        /** The capture pixels covering a normalized part of a rect transform's rectangle. */
        private static RectInt PixelRect(Camera camera, int width, int height, RectTransform rect, Rect normalized)
        {
            Rect local = rect.rect;
            var points = new List<Vector3>();
            foreach (var corner in new[] { normalized.min, normalized.max, new Vector2(normalized.xMin, normalized.yMax),
                new Vector2(normalized.xMax, normalized.yMin) })
                points.Add(camera.WorldToScreenPoint(rect.TransformPoint(new Vector3(
                    local.xMin + corner.x * local.width, local.yMin + corner.y * local.height, 0f))));
            return Bounds(points, width, height, 0);
        }

        /** The capture pixels covering every image of the given props, with a margin around them. */
        private static RectInt PropRect(Camera camera, int width, int height, IEnumerable<RectTransform> props)
        {
            var points = new List<Vector3>();
            var corners = new Vector3[4];
            foreach (var prop in props)
                foreach (var image in prop.GetComponentsInChildren<RawImage>())
                {
                    image.rectTransform.GetWorldCorners(corners);
                    points.AddRange(corners.Select(corner => camera.WorldToScreenPoint(corner)));
                }
            Assert.That(points, Is.Not.Empty);
            return Bounds(points, width, height, width / 30);
        }

        private static RectInt Bounds(IReadOnlyCollection<Vector3> points, int width, int height, int margin)
        {
            int xMin = Mathf.Clamp(Mathf.FloorToInt(points.Min(point => point.x)) - margin, 0, width - 1);
            int yMin = Mathf.Clamp(Mathf.FloorToInt(points.Min(point => point.y)) - margin, 0, height - 1);
            int xMax = Mathf.Clamp(Mathf.CeilToInt(points.Max(point => point.x)) + margin, xMin + 1, width);
            int yMax = Mathf.Clamp(Mathf.CeilToInt(points.Max(point => point.y)) + margin, yMin + 1, height);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }
    }
}
