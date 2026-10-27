using System;
using System.Collections.Generic;
using System.IO;
using Dice.Presentation;
using UnityEditor;
using UnityEngine;

namespace Dice.Editor
{
    /**
     * Previews scene lighting composed from texture assets: an ambient scene and up to four lights, each with a
     * contribution and an optional response map. The selections are remembered between sessions.
     */
    public sealed class SceneLightingPreviewWindow : EditorWindow
    {
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

        private const int MaximumLights = 4;
        private const string ShaderPath = "Assets/Dice/Presentation/Shared/SceneLighting.shader";
        private const string AmbientKey = "Dice.SceneLightingPreview.Ambient";

        private readonly List<SceneLightLayer> lights = new List<SceneLightLayer>();
        private SceneLightingAsset lighting;
        private SceneLightingMaterial compositor;
        private RenderTexture output;
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

        /**
         * Captures the lighting preview's remembered ambient scene and first light to a new directory beneath Docs/work
         * named by DICE_LIGHTING_CAPTURE: the ambient and original images, steady intensity samples, animated frames and
         * a JSON record of the inputs and sampled intensities.
         */
        public static void CaptureSelectedPreview()
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
            Texture2D ambient = PreviewAssetSelection.Load<Texture2D>(AmbientKey);
            SceneLightLayer light = CreateDefaultLight();
            light.Contribution = PreviewAssetSelection.Load<Texture2D>(ContributionKey(0));
            light.ResponseMap = PreviewAssetSelection.Load<Texture2D>(ResponseKey(0));
            if (ambient == null || light.Contribution == null)
                throw new InvalidOperationException("Choose an ambient scene and a first light contribution in the lighting preview.");
            Directory.CreateDirectory(absolute);

            SceneLightingAsset scene = CreateInstance<SceneLightingAsset>();
            scene.Shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            scene.hideFlags = HideFlags.HideAndDontSave;
            SceneLightingMaterial material = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                scene.BaseTexture = ambient;
                scene.Lights = new[] { light };
                material = new SceneLightingMaterial(scene.Shader);
                if (!material.Material.shader.isSupported)
                    throw new InvalidOperationException("Scene lighting shader is unsupported by the graphics device.");
                target = new RenderTexture(scene.BaseTexture.width, scene.BaseTexture.height, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, false);
                CaptureRecord record = new CaptureRecord
                {
                    Ambient = AssetDatabase.GetAssetPath(ambient),
                    Illuminated = AssetDatabase.GetAssetPath(light.Contribution),
                    ResponseMap = light.ResponseMap != null ? AssetDatabase.GetAssetPath(light.ResponseMap) : "",
                    DistanceFalloff = light.DistanceFalloff,
                    SourceResponse = light.SourceResponse,
                    ConstrainAmbientToReference = light.ConstrainAmbientToReference,
                    ColorSpace = QualitySettings.activeColorSpace.ToString()
                };
                CaptureTextureToFile(scene.BaseTexture, target, pixels, absolute, "ambient.png");
                CaptureTextureToFile(light.Contribution, target, pixels, absolute, "original.png");
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
                DestroyImmediate(scene);
            }
        }

        /** Imported textures need not be readable, so each is copied through the render target before encoding. */
        private static void CaptureTextureToFile(Texture2D texture, RenderTexture target, Texture2D pixels, string directory,
            string file)
        {
            RenderTexture previousTarget = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = QualitySettings.activeColorSpace == UnityEngine.ColorSpace.Linear;
                Graphics.Blit(texture, target);
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

        private static string ContributionKey(int light) => "Dice.SceneLightingPreview.Light" + light + ".Contribution";
        private static string ResponseKey(int light) => "Dice.SceneLightingPreview.Light" + light + ".Response";

        private static SceneLightLayer CreateLight(int index) =>
            index == 0 ? CreateDefaultLight() : new SceneLightLayer { Seed = 1977 + index * 101 };

        private void OnEnable()
        {
            startedAt = EditorApplication.timeSinceStartup;
            lighting = CreateInstance<SceneLightingAsset>();
            lighting.Shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            lighting.hideFlags = HideFlags.HideAndDontSave;
            lighting.BaseTexture = PreviewAssetSelection.Load<Texture2D>(AmbientKey);
            var remembered = new List<SceneLightLayer>();
            for (int index = 0; index < MaximumLights; ++index)
            {
                SceneLightLayer light = CreateLight(index);
                light.Contribution = PreviewAssetSelection.Load<Texture2D>(ContributionKey(index));
                light.ResponseMap = PreviewAssetSelection.Load<Texture2D>(ResponseKey(index));
                remembered.Add(light);
            }
            int count = Math.Max(1, remembered.FindLastIndex(light => light.Contribution != null || light.ResponseMap != null) + 1);
            lights.AddRange(remembered.GetRange(0, count));
            SelectionChanged();
            EditorApplication.update += UpdatePreview;
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdatePreview;
            ReleaseOutput();
            compositor?.Dispose();
            compositor = null;
            DestroyImmediate(lighting);
            lighting = null;
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
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(285f)))
                {
                    scroll = EditorGUILayout.BeginScrollView(scroll);
                    EditorGUILayout.LabelField("Ambient scene", EditorStyles.boldLabel);
                    Texture2D ambient = PreviewAssetSelection.Field("Texture", lighting.BaseTexture, AmbientKey);
                    if (ambient != lighting.BaseTexture)
                    {
                        lighting.BaseTexture = ambient;
                        SelectionChanged();
                    }
                    EditorGUILayout.Space();
                    for (int index = 0; index < lights.Count; ++index)
                    {
                        SceneLightLayer light = lights[index];
                        EditorGUILayout.LabelField("Light " + (index + 1), EditorStyles.boldLabel);
                        Texture2D contribution = PreviewAssetSelection.Field("Contribution", light.Contribution, ContributionKey(index));
                        Texture2D response = PreviewAssetSelection.Field("Response map (optional)", light.ResponseMap, ResponseKey(index));
                        if (contribution != light.Contribution || response != light.ResponseMap)
                        {
                            light.Contribution = contribution;
                            light.ResponseMap = response;
                            SelectionChanged();
                        }
                        light.ContributionIncludesBase = EditorGUILayout.Toggle("Includes ambient scene", light.ContributionIncludesBase);
                        light.DistanceFalloff = EditorGUILayout.Slider(new GUIContent("Distance falloff",
                            "Controls how quickly distant surfaces lose illumination as intensity falls."), light.DistanceFalloff, 0f, 4f);
                        light.SourceResponse = EditorGUILayout.Slider(new GUIContent("Source response",
                            "Response exponent for the source and glow mask. Values below 1 keep the source visible longer as intensity falls."), light.SourceResponse, .1f, 2f);
                        using (new EditorGUI.DisabledScope(!light.ContributionIncludesBase))
                        {
                            light.ConstrainAmbientToReference = EditorGUILayout.Toggle(new GUIContent("Constrain ambient",
                                "Constrain the ambient scene to the full illuminated reference. Applies only when the contribution includes ambient."),
                                light.ConstrainAmbientToReference);
                        }
                        light.Tint = EditorGUILayout.ColorField("Tint", light.Tint);
                        light.Intensity = EditorGUILayout.Slider("Intensity", light.Intensity, 0f, 2f);
                        light.FlickerAmount = EditorGUILayout.Slider("Flicker amount", light.FlickerAmount, 0f, .5f);
                        light.FlickerSpeed = EditorGUILayout.Slider("Flicker speed", light.FlickerSpeed, .05f, 5f);
                        light.Seed = EditorGUILayout.IntField("Seed", light.Seed);
                        float intensity = animate
                            ? SceneLightFlicker.EvaluateIntensity(Elapsed, light.Intensity, light.FlickerAmount,
                                light.FlickerSpeed, light.Seed)
                            : light.Intensity;
                        EditorGUILayout.LabelField("Current intensity", intensity.ToString("F3"));
                        if (lights.Count > 1 && GUILayout.Button("Remove light"))
                        {
                            lights.RemoveAt(index);
                            SaveLights();
                            SelectionChanged();
                            GUIUtility.ExitGUI();
                        }
                        EditorGUILayout.Space();
                    }
                    using (new EditorGUI.DisabledScope(lights.Count >= MaximumLights))
                    {
                        if (GUILayout.Button("Add light"))
                        {
                            lights.Add(CreateLight(lights.Count));
                            SelectionChanged();
                        }
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox("Nothing is saved to a scene or asset. Each light can use a contribution image or an illuminated image that includes the ambient scene. Optional response maps hold linear data, so import them with sRGB off: red is normalized surface distance, green is the source and glow mask.", MessageType.Info);
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

        /** Remembers every light's selection under its current index and forgets indices beyond the last light. */
        private void SaveLights()
        {
            for (int index = 0; index < MaximumLights; ++index)
            {
                PreviewAssetSelection.Save(ContributionKey(index), index < lights.Count ? lights[index].Contribution : null);
                PreviewAssetSelection.Save(ResponseKey(index), index < lights.Count ? lights[index].ResponseMap : null);
            }
        }

        private void SelectionChanged()
        {
            message = null;
            UpdateLayers();
            ReleaseOutput();
            try
            {
                if (compositor == null) compositor = new SceneLightingMaterial(lighting.Shader);
            }
            catch (Exception exception) { message = exception.Message; }
            Repaint();
        }

        private void UpdateLayers()
        {
            lighting.Lights = lights.FindAll(light => light.Contribution != null).ToArray();
        }

        private Texture PreviewTexture()
        {
            if (lighting == null || lighting.BaseTexture == null) return null;
            if (comparison == 2) return lighting.BaseTexture;
            if (comparison == 1)
                return lights.Count > 0 && lights[0].ContributionIncludesBase && lights[0].Contribution != null
                    ? lights[0].Contribution : lighting.BaseTexture;
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
