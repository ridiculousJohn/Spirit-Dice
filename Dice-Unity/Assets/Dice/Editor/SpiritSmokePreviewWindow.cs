using System;
using Dice.Presentation;
using UnityEditor;
using UnityEngine;

namespace Dice.Editor
{
    /**
     * Previews an authored smoke material over aligned base and smoke textures. The material supplies the flow noise,
     * motion mask and starting values; the sliders change only the preview's own copy.
     */
    public sealed class SpiritSmokePreviewWindow : EditorWindow, IDisposable
    {
        private const int PreviewPass = 1;
        private const string BaseKey = "Dice.SpiritSmokePreview.Base";
        private const string SmokeKey = "Dice.SpiritSmokePreview.Smoke";
        private const string MaterialKey = "Dice.SpiritSmokePreview.Material";
        [SerializeField] private bool animate = true;
        [SerializeField] private bool smokeOnly;
        [SerializeField] private float effectTime;
        private Texture2D baseTexture, smokeTexture;
        private Material authored;
        private SpiritSmokeMaterial smoke;
        private float amount, speed, opacity;
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
            baseTexture = PreviewAssetSelection.Load<Texture2D>(BaseKey);
            smokeTexture = PreviewAssetSelection.Load<Texture2D>(SmokeKey);
            authored = PreviewAssetSelection.Load<Material>(MaterialKey);
            Reload();
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdatePreview;
            Dispose();
        }

        public void Dispose()
        {
            ReleaseOutput();
            smoke?.Dispose();
            smoke = null;
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
            Texture2D nextBase = PreviewAssetSelection.Field("Base texture", baseTexture, BaseKey);
            Texture2D nextSmoke = PreviewAssetSelection.Field("Smoke texture", smokeTexture, SmokeKey);
            Material nextMaterial = PreviewAssetSelection.Field("Smoke material", authored, MaterialKey);
            if (nextBase != baseTexture || nextSmoke != smokeTexture || nextMaterial != authored)
            {
                baseTexture = nextBase;
                smokeTexture = nextSmoke;
                authored = nextMaterial;
                Reload();
            }
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
            if (smoke == null) return;
            try
            {
                RenderPreview(area);
                GUI.DrawTexture(area, output, ScaleMode.ScaleToFit, false);
            }
            catch (Exception exception) { message = exception.Message; }
        }

        /** Rebuilds the preview's copy of the selected material and takes its values as the slider values. */
        private void Reload()
        {
            Dispose();
            message = null;
            try
            {
                if (baseTexture == null || smokeTexture == null || authored == null)
                    throw new InvalidOperationException("Choose aligned base and smoke textures and an authored smoke material.");
                if (baseTexture.width != smokeTexture.width || baseTexture.height != smokeTexture.height)
                    throw new InvalidOperationException("The base and smoke textures must have the same size.");
                if (authored.passCount <= PreviewPass) throw new InvalidOperationException("The smoke material's shader has no preview pass.");
                smoke = new SpiritSmokeMaterial(authored.shader, authored);
                amount = authored.GetFloat("_Amount");
                speed = authored.GetFloat("_Speed");
                opacity = authored.GetFloat("_Opacity");
            }
            catch (Exception exception) { message = exception.Message; }
            Repaint();
        }

        private void RenderPreview(Rect area)
        {
            float aspect = (float)baseTexture.width / baseTexture.height;
            int width = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(1024f, area.width, area.height * aspect)));
            int height = Mathf.Max(1, Mathf.RoundToInt(width / aspect));
            if (output == null || output.width != width || output.height != height)
            {
                ReleaseOutput();
                output = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                { name = "Spirit smoke preview", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
                if (!output.Create()) throw new InvalidOperationException("Could not create the smoke preview render texture.");
            }
            Material material = smoke.Material;
            smoke.ApplyTime(effectTime, null);
            material.SetFloat("_Amount", amount);
            material.SetFloat("_Speed", speed);
            material.SetFloat("_Opacity", opacity);
            RenderTexture previousTarget = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(smokeOnly ? Texture2D.blackTexture : baseTexture, output);
                Graphics.Blit(smokeTexture, output, material, PreviewPass);
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
