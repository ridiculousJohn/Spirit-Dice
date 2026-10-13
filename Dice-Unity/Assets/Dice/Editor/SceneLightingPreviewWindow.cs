using System;
using System.Collections.Generic;
using System.IO;
using Dice.Presentation;
using UnityEditor;
using UnityEngine;

namespace Dice.Editor
{
    public sealed class SceneLightingPreviewWindow : EditorWindow
    {
        private sealed class PreviewLight
        {
            public string Path;
            public string ResponsePath;
            public SceneLightLayer Layer;
        }

        [Serializable]
        private sealed class CaptureFrame
        {
            public string File;
            public double Time;
            public float Intensity;
        }

        [Serializable]
        private sealed class CaptureRecord
        {
            public string Ambient;
            public string Illuminated;
            public string ResponseMap;
            public float DistanceFalloff;
            public float SourceResponse;
            public bool ConstrainAmbientToReference;
            public string ColorSpace;
            public List<CaptureFrame> Frames = new List<CaptureFrame>();
        }

        private const string DefaultAmbientPath = "Docs/work/moth-lighting-prototype/ambient-v2.png";
        private const string DefaultIlluminatedPath = "Docs/work/moth-lighting-prototype/illuminated.png";
        private const string DefaultResponsePath = "Docs/work/moth-lighting-prototype/lantern-response-v2.png";

        private readonly List<PreviewLight> lights = new List<PreviewLight>();
        private SceneLightingAsset lighting;
        private SceneLightingMaterial compositor;
        private RenderTexture output;
        private string ambientPath = DefaultAmbientPath;
        private string message;
        private Vector2 scroll;
        private bool animate = true;
        private int comparison;
        private double startedAt;
        private double lastRepaint;

        [MenuItem("Dice/Lighting Preview")]
        public static void Open()
        {
            SceneLightingPreviewWindow window = GetWindow<SceneLightingPreviewWindow>("Lighting Preview");
            window.minSize = new Vector2(650f, 600f);
            window.Show();
        }

        public static void CaptureDefaultPreview()
        {
            string capturePath = Environment.GetEnvironmentVariable("DICE_LIGHTING_CAPTURE");
            if (string.IsNullOrWhiteSpace(capturePath))
                throw new InvalidOperationException("Set DICE_LIGHTING_CAPTURE to a new directory beneath Docs/work.");
            string absolute = System.IO.Path.GetFullPath(Resolve(capturePath));
            string workingRoot = System.IO.Path.GetFullPath(Resolve("Docs/work")) + System.IO.Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(workingRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Lighting captures must stay beneath Docs/work.");
            if (Directory.Exists(absolute))
                throw new InvalidOperationException("Choose a new capture directory to preserve existing output.");
            Directory.CreateDirectory(absolute);

            SceneLightingAsset scene = CreateInstance<SceneLightingAsset>();
            scene.hideFlags = HideFlags.HideAndDontSave;
            SceneLightingMaterial material = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                scene.BaseTexture = LoadTexture(DefaultAmbientPath);
                SceneLightLayer light = CreateDefaultLight();
                scene.Lights = new[] { light };
                light.Contribution = LoadTexture(DefaultIlluminatedPath);
                light.ResponseMap = LoadTexture(DefaultResponsePath, linear: true);
                material = new SceneLightingMaterial();
                if (!material.Material.shader.isSupported)
                    throw new InvalidOperationException("Scene lighting shader is unsupported by the graphics device.");
                target = new RenderTexture(scene.BaseTexture.width, scene.BaseTexture.height, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, false);
                CaptureRecord record = new CaptureRecord
                {
                    Ambient = DefaultAmbientPath,
                    Illuminated = DefaultIlluminatedPath,
                    ResponseMap = DefaultResponsePath,
                    DistanceFalloff = light.DistanceFalloff,
                    SourceResponse = light.SourceResponse,
                    ConstrainAmbientToReference = light.ConstrainAmbientToReference,
                    ColorSpace = QualitySettings.activeColorSpace.ToString()
                };
                File.WriteAllBytes(System.IO.Path.Combine(absolute, "ambient.png"), scene.BaseTexture.EncodeToPNG());
                File.WriteAllBytes(System.IO.Path.Combine(absolute, "original.png"), light.Contribution.EncodeToPNG());
                foreach (float intensity in new[] { 0f, .05f, .1f, .2f, .4f, .65f, 1f })
                {
                    light.Intensity = intensity;
                    string file = "intensity-" + intensity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".png";
                    CaptureFrameToFile(material, scene, target, pixels, absolute, file, 0d, false);
                    record.Frames.Add(new CaptureFrame { File = file, Time = 0d, Intensity = intensity });
                }
                target.Release();
                DestroyImmediate(target);
                DestroyImmediate(pixels);
                int width = Mathf.Min(scene.BaseTexture.width, 540);
                int height = Mathf.RoundToInt((float)width / scene.BaseTexture.width * scene.BaseTexture.height);
                target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                pixels = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                light.Intensity = .65f;
                for (int index = 0; index < 120; ++index)
                {
                    double time = index * .05d;
                    string file = "frame-" + index.ToString("D3") + ".png";
                    CaptureFrameToFile(material, scene, target, pixels, absolute, file, time, true);
                    record.Frames.Add(new CaptureFrame
                    {
                        File = file,
                        Time = time,
                        Intensity = SceneLightFlicker.EvaluateIntensity(time, light.Intensity, light.FlickerAmount,
                            light.FlickerSpeed, light.Seed)
                    });
                }
                File.WriteAllText(System.IO.Path.Combine(absolute, "capture.json"), JsonUtility.ToJson(record, true));
                Debug.Log("Scene lighting captures: " + absolute);
            }
            finally
            {
                material?.Dispose();
                if (target != null) target.Release();
                DestroyImmediate(target);
                DestroyImmediate(pixels);
                DestroyImmediate(scene.BaseTexture);
                if (scene.Lights != null)
                    foreach (SceneLightLayer light in scene.Lights)
                    {
                        DestroyImmediate(light.Contribution);
                        DestroyImmediate(light.ResponseMap);
                    }
                DestroyImmediate(scene);
            }
        }

        private static void CaptureFrameToFile(SceneLightingMaterial material, SceneLightingAsset scene,
            RenderTexture target, Texture2D pixels, string directory, string file, double time, bool animated)
        {
            material.Apply(scene, time, animate: animated);
            RenderTexture previousTarget = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = QualitySettings.activeColorSpace == UnityEngine.ColorSpace.Linear;
                Graphics.Blit(scene.BaseTexture, target, material.Material, SceneLightingMaterial.PreviewPass);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply(false, false);
                File.WriteAllBytes(System.IO.Path.Combine(directory, file), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousTarget;
                GL.sRGBWrite = previousSrgb;
            }
        }

        private static SceneLightLayer CreateDefaultLight()
        {
            return new SceneLightLayer
            {
                ContributionIncludesBase = true,
                DistanceFalloff = 1.6f,
                SourceResponse = .35f,
                ConstrainAmbientToReference = true,
                Intensity = .65f,
                FlickerAmount = .12f,
                FlickerSpeed = 1f,
                Seed = 1977
            };
        }

        private void OnEnable()
        {
            startedAt = EditorApplication.timeSinceStartup;
            lighting = CreateInstance<SceneLightingAsset>();
            lighting.hideFlags = HideFlags.HideAndDontSave;
            lights.Add(new PreviewLight
            {
                Path = DefaultIlluminatedPath,
                ResponsePath = DefaultResponsePath,
                Layer = CreateDefaultLight()
            });
            Reload();
            EditorApplication.update += UpdatePreview;
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdatePreview;
            ReleaseOutput();
            compositor?.Dispose();
            compositor = null;
            if (lighting != null)
            {
                DestroyImmediate(lighting.BaseTexture);
                DestroyImmediate(lighting);
                lighting = null;
            }
            foreach (PreviewLight light in lights)
            {
                DestroyImmediate(light.Layer.Contribution);
                DestroyImmediate(light.Layer.ResponseMap);
            }
            lights.Clear();
        }

        private void UpdatePreview()
        {
            double now = EditorApplication.timeSinceStartup;
            if (animate && now - lastRepaint >= 1d / 30d)
            {
                lastRepaint = now;
                Repaint();
            }
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                comparison = GUILayout.Toolbar(comparison, new[] { "Lit scene", "Original", "Ambient" }, EditorStyles.toolbarButton);
                animate = GUILayout.Toggle(animate, "Animate", EditorStyles.toolbarButton, GUILayout.Width(75f));
                if (GUILayout.Button("Reload files", EditorStyles.toolbarButton, GUILayout.Width(90f))) Reload();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(285f)))
                {
                    scroll = EditorGUILayout.BeginScrollView(scroll);
                    EditorGUILayout.LabelField("Ambient scene", EditorStyles.boldLabel);
                    DrawPath(ref ambientPath, "Choose ambient scene");
                    EditorGUILayout.Space();
                    for (int index = 0; index < lights.Count; ++index)
                    {
                        PreviewLight light = lights[index];
                        EditorGUILayout.LabelField("Light " + (index + 1), EditorStyles.boldLabel);
                        DrawPath(ref light.Path, "Choose light contribution");
                        light.Layer.ContributionIncludesBase = EditorGUILayout.Toggle("Includes ambient scene", light.Layer.ContributionIncludesBase);
                        EditorGUILayout.LabelField("Response map (optional)");
                        DrawPath(ref light.ResponsePath, "Choose light response map");
                        light.Layer.DistanceFalloff = EditorGUILayout.Slider(new GUIContent("Distance falloff",
                            "Controls how quickly distant surfaces lose illumination as intensity falls."), light.Layer.DistanceFalloff, 0f, 4f);
                        light.Layer.SourceResponse = EditorGUILayout.Slider(new GUIContent("Source response",
                            "Response exponent for the source and glow mask. Values below 1 keep the source visible longer as intensity falls."), light.Layer.SourceResponse, .1f, 2f);
                        using (new EditorGUI.DisabledScope(!light.Layer.ContributionIncludesBase))
                        {
                            light.Layer.ConstrainAmbientToReference = EditorGUILayout.Toggle(new GUIContent("Constrain ambient",
                                "Constrain the ambient scene to the full illuminated reference. Applies only when the contribution includes ambient."),
                                light.Layer.ConstrainAmbientToReference);
                        }
                        light.Layer.Tint = EditorGUILayout.ColorField("Tint", light.Layer.Tint);
                        light.Layer.Intensity = EditorGUILayout.Slider("Intensity", light.Layer.Intensity, 0f, 2f);
                        light.Layer.FlickerAmount = EditorGUILayout.Slider("Flicker amount", light.Layer.FlickerAmount, 0f, .5f);
                        light.Layer.FlickerSpeed = EditorGUILayout.Slider("Flicker speed", light.Layer.FlickerSpeed, .05f, 5f);
                        light.Layer.Seed = EditorGUILayout.IntField("Seed", light.Layer.Seed);
                        float intensity = animate
                            ? SceneLightFlicker.EvaluateIntensity(Elapsed, light.Layer.Intensity, light.Layer.FlickerAmount,
                                light.Layer.FlickerSpeed, light.Layer.Seed)
                            : light.Layer.Intensity;
                        EditorGUILayout.LabelField("Current intensity", intensity.ToString("F3"));
                        if (lights.Count > 1 && GUILayout.Button("Remove light"))
                        {
                            DestroyImmediate(light.Layer.Contribution);
                            DestroyImmediate(light.Layer.ResponseMap);
                            lights.RemoveAt(index);
                            UpdateLayers();
                            GUIUtility.ExitGUI();
                        }
                        EditorGUILayout.Space();
                    }
                    using (new EditorGUI.DisabledScope(lights.Count >= 4))
                    {
                        if (GUILayout.Button("Add light"))
                        {
                            lights.Add(new PreviewLight { Path = "", ResponsePath = "", Layer = new SceneLightLayer { Seed = 1977 + lights.Count * 101 } });
                            UpdateLayers();
                        }
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox("PNG files are loaded into memory. Nothing is imported into Assets or saved to a scene. Each light can use a contribution image or an illuminated image that includes the ambient scene. Optional response maps use linear data: red is normalized surface distance, green is the source and glow mask.", MessageType.Info);
                    if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Warning);
                    EditorGUILayout.EndScrollView();
                }

                Rect area = GUILayoutUtility.GetRect(100f, 100f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(area, new Color(.035f, .035f, .035f));
                    Texture texture = PreviewTexture();
                    if (texture != null) GUI.DrawTexture(area, texture, ScaleMode.ScaleToFit, false);
                }
            }
        }

        private double Elapsed => EditorApplication.timeSinceStartup - startedAt;

        private static string Resolve(string path)
        {
            string repository = Directory.GetParent(Application.dataPath).Parent.FullName;
            return System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(repository, path);
        }

        private void DrawPath(ref string path, string title)
        {
            path = EditorGUILayout.TextField(path);
            if (GUILayout.Button("Choose PNG..."))
            {
                string selected = EditorUtility.OpenFilePanel(title, Directory.Exists(Resolve(path))
                    ? Resolve(path) : System.IO.Path.GetDirectoryName(Resolve(path)), "png");
                if (!string.IsNullOrEmpty(selected))
                {
                    path = selected;
                    Reload();
                }
            }
        }

        private static Texture2D LoadTexture(string path, bool linear = false)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            string absolute = Resolve(path);
            if (!File.Exists(absolute)) throw new FileNotFoundException("PNG file is missing", absolute);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear)
            {
                name = System.IO.Path.GetFileName(absolute),
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(absolute)))
            {
                DestroyImmediate(texture);
                throw new InvalidDataException("Could not decode PNG: " + absolute);
            }
            return texture;
        }

        private void Reload()
        {
            message = null;
            try
            {
                Texture2D ambient = LoadTexture(ambientPath);
                DestroyImmediate(lighting.BaseTexture);
                lighting.BaseTexture = ambient;
                foreach (PreviewLight light in lights)
                {
                    Texture2D contribution = LoadTexture(light.Path);
                    DestroyImmediate(light.Layer.Contribution);
                    light.Layer.Contribution = contribution;
                    Texture2D response = LoadTexture(light.ResponsePath, linear: true);
                    DestroyImmediate(light.Layer.ResponseMap);
                    light.Layer.ResponseMap = response;
                }
                UpdateLayers();
                ReleaseOutput();
                if (compositor == null) compositor = new SceneLightingMaterial();
            }
            catch (Exception exception)
            {
                message = exception.Message;
                UpdateLayers();
            }
            Repaint();
        }

        private void UpdateLayers()
        {
            lighting.Lights = lights.FindAll(light => light.Layer.Contribution != null)
                .ConvertAll(light => light.Layer).ToArray();
        }

        private Texture PreviewTexture()
        {
            if (lighting == null || lighting.BaseTexture == null) return null;
            if (comparison == 2) return lighting.BaseTexture;
            if (comparison == 1)
                return lights.Count > 0 && lights[0].Layer.ContributionIncludesBase && lights[0].Layer.Contribution != null
                    ? lights[0].Layer.Contribution : lighting.BaseTexture;
            if (compositor == null) return null;
            if (output == null)
            {
                float scale = Mathf.Min(1f, 1600f / lighting.BaseTexture.height);
                output = new RenderTexture(Mathf.Max(1, Mathf.RoundToInt(lighting.BaseTexture.width * scale)),
                    Mathf.Max(1, Mathf.RoundToInt(lighting.BaseTexture.height * scale)), 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "Scene lighting preview",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear
                };
                output.Create();
            }
            compositor.Apply(lighting, Elapsed, animate: animate);
            RenderTexture previousTarget = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = QualitySettings.activeColorSpace == UnityEngine.ColorSpace.Linear;
                Graphics.Blit(lighting.BaseTexture, output, compositor.Material, SceneLightingMaterial.PreviewPass);
            }
            finally
            {
                RenderTexture.active = previousTarget;
                GL.sRGBWrite = previousSrgb;
            }
            return output;
        }

        private void ReleaseOutput()
        {
            if (output == null) return;
            output.Release();
            DestroyImmediate(output);
            output = null;
        }
    }
}
