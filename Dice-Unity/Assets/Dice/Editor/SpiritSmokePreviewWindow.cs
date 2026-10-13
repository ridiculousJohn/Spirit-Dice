using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dice.Editor
{
    public sealed class SpiritSmokePreviewWindow : EditorWindow, IDisposable
    {
        private const float PreviewAspect = 1536f / 1024f;
        [SerializeField] private string basePath = "Docs/work/moth-smoke-prototype-v1/base.png";
        [SerializeField] private string smokePath = "Docs/work/moth-smoke-prototype-v1/smoke.png";
        [SerializeField] private bool animate = true;
        [SerializeField] private bool smokeOnly;
        [SerializeField] private float effectTime;
        [SerializeField] private float amount = .035f;
        [SerializeField] private float speed = .7f;
        [SerializeField] private float opacity = .6f;
        private Texture2D baseTexture, smokeTexture, noiseTexture, motionMask;
        private Material material;
        private RenderTexture output;
        private string message;
        private double lastUpdate, lastRepaint;

        [MenuItem("Dice/Spirit Smoke Preview")]
        public static void Open()
        {
            SpiritSmokePreviewWindow window = GetWindow<SpiritSmokePreviewWindow>("Spirit Smoke Preview");
            window.minSize = new Vector2(600f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            lastUpdate = EditorApplication.timeSinceStartup;
            EditorApplication.update += UpdatePreview;
            if (File.Exists(Resolve(basePath)) && File.Exists(Resolve(smokePath))) Reload();
            else message = "Choose aligned 1536 x 1024 base and smoke PNG files, then click Load files.";
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdatePreview;
            Dispose();
        }

        public void Dispose()
        {
            ReleaseOutput();
            DestroyImmediate(material);
            DestroyImmediate(baseTexture);
            DestroyImmediate(smokeTexture);
            DestroyImmediate(noiseTexture);
            DestroyImmediate(motionMask);
            material = null;
            baseTexture = smokeTexture = noiseTexture = motionMask = null;
        }

        private void UpdatePreview()
        {
            double now = EditorApplication.timeSinceStartup;
            if (animate) effectTime += (float)(now - lastUpdate);
            lastUpdate = now;
            if (animate && now - lastRepaint >= 1d / 30d)
            {
                lastRepaint = now;
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawPath("Base PNG", ref basePath);
            DrawPath("Smoke PNG", ref smokePath);
            if (GUILayout.Button("Load files")) Reload();
            animate = EditorGUILayout.Toggle("Animate", animate);
            using (new EditorGUI.DisabledScope(animate))
                effectTime = EditorGUILayout.FloatField("Time (seconds)", effectTime);
            amount = EditorGUILayout.Slider("Distortion amount", amount, 0f, .05f);
            speed = EditorGUILayout.Slider("Noise speed", speed, 0f, 1f);
            opacity = EditorGUILayout.Slider("Opacity", opacity, 0f, 1f);
            smokeOnly = EditorGUILayout.Toggle("Show smoke only", smokeOnly);
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Warning);
            Rect area = GUILayoutUtility.GetRect(100f, 100f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(area, new Color(.035f, .035f, .035f));
            if (material == null || baseTexture == null || smokeTexture == null) return;
            try
            {
                RenderPreview(area);
                GUI.DrawTexture(area, output, ScaleMode.ScaleToFit, false);
            }
            catch (Exception exception) { message = exception.Message; }
        }

        private static string Resolve(string path)
        {
            string repository = Directory.GetParent(Application.dataPath).Parent.FullName;
            return Path.IsPathRooted(path) ? path : Path.Combine(repository, path);
        }

        private static void DrawPath(string label, ref string path)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                path = EditorGUILayout.TextField(label, path);
                if (GUILayout.Button("Browse...", GUILayout.Width(80f)))
                {
                    string selected = EditorUtility.OpenFilePanel(label, Path.GetDirectoryName(Resolve(path)), "png");
                    if (!string.IsNullOrEmpty(selected)) path = selected;
                }
            }
        }

        private static Texture2D LoadTexture(string path)
        {
            string absolute = Resolve(path);
            byte[] bytes = File.ReadAllBytes(absolute);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
            {
                name = Path.GetFileName(absolute), hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            try
            {
                if (!ImageConversion.LoadImage(texture, bytes)) throw new InvalidDataException("Could not decode PNG: " + absolute);
                return texture;
            }
            catch { DestroyImmediate(texture); throw; }
        }

        private void Reload()
        {
            Texture2D loadedBase = null, loadedSmoke = null;
            try
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Dice/Presentation/Shared/SpiritSmoke.shader");
                if (shader == null || !shader.isSupported) throw new InvalidOperationException("Dice/Spirit Smoke shader is missing or unsupported.");
                loadedBase = LoadTexture(basePath);
                loadedSmoke = LoadTexture(smokePath);
                if (loadedBase.width != 1536 || loadedBase.height != 1024 || loadedSmoke.width != 1536 || loadedSmoke.height != 1024)
                    throw new InvalidDataException("Base and smoke PNG files must both be aligned 1536 x 1024 images.");
                if (material == null) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                if (material.passCount < 2) throw new InvalidOperationException("Spirit Smoke shader preview pass is missing.");
                if (noiseTexture == null) noiseTexture = CreateDataTexture(128, true);
                if (motionMask == null) motionMask = CreateDataTexture(256, false);
                DestroyImmediate(baseTexture);
                DestroyImmediate(smokeTexture);
                baseTexture = loadedBase;
                smokeTexture = loadedSmoke;
                loadedBase = loadedSmoke = null;
                message = null;
            }
            catch (Exception exception) { message = exception.Message; }
            finally { DestroyImmediate(loadedBase); DestroyImmediate(loadedSmoke); }
            Repaint();
        }

        private static Texture2D CreateDataTexture(int size, bool noise)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = noise ? "Spirit smoke noise" : "Spirit smoke motion mask",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = noise ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; ++y)
                for (int x = 0; x < size; ++x)
                {
                    float u = (float)x / (size - 1), v = (float)y / (size - 1);
                    float motion = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.2f, .36f, Mathf.Abs(u - .5f)));
                    pixels[y * size + x] = noise
                        ? new Color(RepeatNoise(u, v, 17f), RepeatNoise(u, v, 83f), 0f, 1f)
                        : new Color(motion, motion, motion, 1f);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static float RepeatNoise(float u, float v, float offset)
        {
            float x = u * 4f + offset, y = v * 4f + offset;
            return Mathf.Lerp(Mathf.Lerp(Mathf.PerlinNoise(x, y), Mathf.PerlinNoise(x - 4f, y), u),
                Mathf.Lerp(Mathf.PerlinNoise(x, y - 4f), Mathf.PerlinNoise(x - 4f, y - 4f), u), v);
        }

        private void RenderPreview(Rect area)
        {
            int width = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(1024f, area.width, area.height * PreviewAspect)));
            int height = Mathf.Max(1, Mathf.RoundToInt(width / PreviewAspect));
            if (output == null || output.width != width || output.height != height)
            {
                ReleaseOutput();
                output = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                { name = "Spirit smoke preview", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
                if (!output.Create()) throw new InvalidOperationException("Could not create the smoke preview render texture.");
            }
            material.SetTexture("_MotionMask", motionMask);
            material.SetTexture("_NoiseTex", noiseTexture);
            material.SetFloat("_EffectTime", effectTime);
            material.SetFloat("_Amount", amount);
            material.SetFloat("_Speed", speed);
            material.SetVector("_Flow", new Vector4(0f, .018f, 0f, 0f));
            material.SetFloat("_Opacity", opacity);
            material.SetColor("_Color", Color.white);
            RenderTexture previousTarget = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(smokeOnly ? Texture2D.blackTexture : baseTexture, output);
                Graphics.Blit(smokeTexture, output, material, 1);
            }
            finally { RenderTexture.active = previousTarget; GL.sRGBWrite = previousSrgb; }
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
