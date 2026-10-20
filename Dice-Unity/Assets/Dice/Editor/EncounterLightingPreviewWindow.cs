using System;
using Dice.Engine;
using Dice.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dice.Editor
{
    /** An isolated presentation stage previews authored scene lighting with the encounter's renderers. */
    public sealed class EncounterLightingPreviewWindow : EditorWindow
    {
        private const string DefaultTablePath = "Assets/Dice/Content/Presentation/MothCrossroads/MothCrossroadsTable.asset";
        private const string DefaultEntryPath = "Assets/Dice/Content/Presentation/MothCrossroads/MothCrossroadsEntryLighting.asset";
        private const string PreviewPipelinePath = "Assets/Settings/SpiritPrototypePipeline.asset";
        private const int UiLayer = 30;
        [SerializeField] private EncounterTablePresentationAsset table;
        [SerializeField] private SceneLightingAsset entry;
        [SerializeField] private bool showTable = true;
        [SerializeField] private bool animate = true;
        [SerializeField] private float intensity = 1f;
        [SerializeField] private float poseTime;
        private SpiritEncounterPreviewScene encounter;
        private string stagedSceneSettings;
        private Scene previewScene;
        private GameObject stage;
        private Camera camera;
        private RenderTexture output;
        private RawImage roomImage;
        private RawImage tableImage;
        private SpiritTrayScene trayScene;
        private SpiritDiceTray tray;
        private SpiritModifierHistoryRenderer history;
        private SpiritHostPortrait host;
        private SceneLightingAsset lighting;
        private SceneLightingAsset sourceLighting;
        private SceneLightingMaterial compositor;
        private RenderPipelineAsset previousPipeline;
        private UniversalRenderPipelineAsset previewPipeline;
        private readonly SceneLightingState state = new SceneLightingState();
        private EncounterTablePresentationAsset stagedTable;
        private SceneLightingAsset stagedEntry;
        private bool stagedShowTable;
        private double startedAt;
        private double lastRepaint;
        private string message;

        [MenuItem("Dice/Encounter Lighting Preview")]
        public static void Open()
        {
            var window = GetWindow<EncounterLightingPreviewWindow>("Encounter Lighting Preview");
            window.minSize = new Vector2(420, 650);
            window.Show();
        }

        private void OnEnable()
        {
            if (table == null) table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(DefaultTablePath);
            if (entry == null) entry = AssetDatabase.LoadAssetAtPath<SceneLightingAsset>(DefaultEntryPath);
            startedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update += UpdatePreview;
            EditorApplication.playModeStateChanged += PlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdatePreview;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            try { ReleaseStage(); }
            finally { ReleasePipeline(); }
        }

        private void PlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                try { ReleaseStage(); }
                finally { ReleasePipeline(); }
            }
            Repaint();
        }

        private void UpdatePreview()
        {
            double now = EditorApplication.timeSinceStartup;
            if (animate && !EditorApplication.isPlayingOrWillChangePlaymode && now - lastRepaint >= 1d / 30d)
            {
                lastRepaint = now;
                Repaint();
            }
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                showTable = GUILayout.Toolbar(showTable ? 1 : 0, new[] { "Entry", "Table" },
                    EditorStyles.toolbarButton) == 1;
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60))) ReleaseStage();
            }
            table = (EncounterTablePresentationAsset)EditorGUILayout.ObjectField("Table", table,
                typeof(EncounterTablePresentationAsset), false);
            entry = (SceneLightingAsset)EditorGUILayout.ObjectField("Entry lighting", entry,
                typeof(SceneLightingAsset), false);
            intensity = EditorGUILayout.Slider("Intensity multiplier", intensity, 0f, 2f);
            bool nextAnimate = EditorGUILayout.Toggle("Animate", animate);
            if (animate && !nextAnimate) poseTime = (float)(EditorApplication.timeSinceStartup - startedAt);
            if (!animate && nextAnimate) startedAt = EditorApplication.timeSinceStartup - poseTime;
            animate = nextAnimate;
            using (new EditorGUI.DisabledScope(animate))
                poseTime = EditorGUILayout.FloatField("Pose time", Mathf.Max(0f, poseTime));
            EditorGUILayout.HelpBox("Presentation stage for lighting and host poses. This is not a playable encounter. " +
                "Intensity changes are temporary; authored assets and the open scene are untouched. " +
                "The gameplay 3D pipeline stays active until this preview closes or Play Mode starts.", MessageType.Info);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Return to Edit Mode to use this preview.", MessageType.Info);
                return;
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Warning);
            Rect area = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(area, new Color(.035f, .035f, .035f));
            try
            {
                EnsureStage();
                RenderPreview();
                message = null;
                GUI.DrawTexture(area, output, ScaleMode.ScaleToFit, false);
            }
            catch (Exception error)
            {
                message = error.Message;
                GUI.Label(area, message, EditorStyles.wordWrappedLabel);
            }
        }

        private void EnsureStage()
        {
            var selectedLighting = showTable ? table?.SceneLighting : entry;
            string settings = showTable && table != null ? SpiritEncounterPreviewScene.SourceSettings(table) : null;
            if (stage != null && stagedTable == table && stagedEntry == entry && stagedShowTable == showTable &&
                sourceLighting == selectedLighting && stagedSceneSettings == settings) return;
            ReleaseStage();
            if (showTable && table == null) throw new InvalidOperationException("Select an encounter table.");
            if (!showTable && entry == null) throw new InvalidOperationException("Select entry lighting.");
            selectedLighting?.Validate();
            if (showTable) AcquirePipeline();
            stagedSceneSettings = settings;
            stagedTable = table;
            stagedEntry = entry;
            stagedShowTable = showTable;
            sourceLighting = selectedLighting;
            if (sourceLighting != null)
            {
                lighting = Instantiate(sourceLighting);
                lighting.name = "Encounter Preview Lighting";
                lighting.hideFlags = HideFlags.HideAndDontSave;
                lighting.Lights = new SceneLightLayer[sourceLighting.Lights.Length];
                for (int i = 0; i < lighting.Lights.Length; ++i) lighting.Lights[i] = Copy(sourceLighting.Lights[i]);
                compositor = new SceneLightingMaterial(lighting.Shader);
            }
            if (showTable)
            {
                encounter = SpiritEncounterPreviewScene.Open(table, false);
                encounter.ShowHud(false);
                previewScene = encounter.Scene;
                stage = encounter.Root;
                camera = encounter.Camera;
                output = encounter.Output;
                roomImage = encounter.View.RoomImage;
                tableImage = encounter.View.TableImage;
                trayScene = encounter.View.TrayScene;
                tray = trayScene.DiceTray;
                history = trayScene.HistoryRenderer;
                host = encounter.View.HostPortrait;
                tray.SetValues(new DiceValues(1, 2, 3, 4, 5));
            }
            else
            {
                previewScene = EditorSceneManager.NewPreviewScene();
                stage = Object("Encounter Lighting Preview");
                camera = Object("Encounter Preview Camera").AddComponent<Camera>();
                camera.scene = previewScene;
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = 480;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 30;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.04f, .055f, .05f, 1);
                camera.cullingMask = 1 << UiLayer;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                output = new RenderTexture(540, 960, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "Encounter Lighting Preview", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                output.Create();
                camera.targetTexture = output;
                var canvasRoot = Object("Encounter Preview Canvas", typeof(RectTransform)).GetComponent<RectTransform>();
                canvasRoot.SetParent(stage.transform, false);
                var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(540, 960);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 1;
                roomImage = Image("Entry", canvasRoot, Vector2.zero, Vector2.one);
            }
        }

        private void RenderPreview()
        {
            double time = animate ? EditorApplication.timeSinceStartup - startedAt : Math.Max(0, poseTime);
            if (lighting != null)
                for (int i = 0; i < lighting.Lights.Length; ++i)
                {
                    lighting.Lights[i].Intensity = (float)Math.Min(float.MaxValue,
                        (double)sourceLighting.Lights[i].Intensity * intensity);
                    lighting.Lights[i].FlickerAmount = animate ? sourceLighting.Lights[i].FlickerAmount : 0f;
                }
            state.Sample(lighting, time);
            if (lighting != null) compositor.Apply(state);
            Texture2D texture = lighting != null ? lighting.BaseTexture : table?.RoomTexture;
            if (showTable) roomImage.material = tableImage.material = compositor?.Material;
            else BindImage(roomImage, texture);
            var sampled = lighting != null ? state : null;
            tray?.SetSceneLighting(sampled);
            history?.SetSceneLighting(sampled);
            Canvas.ForceUpdateCanvases();
            if (host != null)
            {
                host.Sample((float)time);
                host.SetSceneLighting(sampled, table.HostLightingPosition);
            }
            RenderTexture previous = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                Canvas.ForceUpdateCanvases();
                if (tray != null) Render(tray.RenderCamera, tray.Texture);
                Render(camera, output);
            }
            finally
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousSrgb;
            }
        }

        private void AcquirePipeline()
        {
            if (previewPipeline != null) return;
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PreviewPipelinePath);
            if (pipeline == null) throw new InvalidOperationException("The encounter 3D preview pipeline is unavailable.");
            previousPipeline = QualitySettings.renderPipeline;
            previewPipeline = pipeline;
            // Keep URP resources alive across repaints and stage changes.
            QualitySettings.renderPipeline = previewPipeline;
        }

        private void ReleasePipeline()
        {
            if (previewPipeline == null) return;
            if (QualitySettings.renderPipeline == previewPipeline)
                QualitySettings.renderPipeline = previousPipeline;
            previewPipeline = null;
            previousPipeline = null;
        }

        private void BindImage(RawImage image, Texture2D texture)
        {
            if (image == null) return;
            image.texture = texture;
            image.color = texture != null ? Color.white : Color.clear;
            image.material = compositor?.Material;
        }

        private static void Render(Camera renderCamera, RenderTexture target)
        {
            if (GraphicsSettings.currentRenderPipeline == null) renderCamera.Render();
            else
            {
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(renderCamera, request))
                    throw new InvalidOperationException("The active render pipeline cannot preview an isolated camera.");
                RenderPipeline.SubmitRenderRequest(renderCamera, request);
            }
        }

        private static SceneLightLayer Copy(SceneLightLayer source) => new SceneLightLayer
        {
            Contribution = source.Contribution, ResponseMap = source.ResponseMap, Tint = source.Tint,
            Intensity = source.Intensity, DistanceFalloff = source.DistanceFalloff, SourceResponse = source.SourceResponse,
            FlickerAmount = source.FlickerAmount, FlickerSpeed = source.FlickerSpeed, Seed = source.Seed,
            ContributionIncludesBase = source.ContributionIncludesBase,
            ConstrainAmbientToReference = source.ConstrainAmbientToReference,
            ReceiverPosition = source.ReceiverPosition, ReceiverColor = source.ReceiverColor,
            ReceiverIntensity = source.ReceiverIntensity, ReceiverRange = source.ReceiverRange
        };

        private GameObject Object(string name, params Type[] components)
        {
            var result = new GameObject(name, components) { hideFlags = HideFlags.HideAndDontSave, layer = UiLayer };
            SceneManager.MoveGameObjectToScene(result, previewScene);
            return result;
        }

        private RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = Object(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private RawImage Image(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var image = Rect(name, parent, min, max).gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        private void ReleaseStage()
        {
            compositor?.Dispose();
            compositor = null;
            if (encounter != null)
            {
                encounter.Dispose();
                encounter = null;
            }
            else
            {
                if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
                if (output != null) { output.Release(); DestroyImmediate(output); }
            }
            previewScene = default;
            if (lighting != null) DestroyImmediate(lighting);
            output = null;
            lighting = sourceLighting = null;
            stage = null;
            camera = null;
            roomImage = tableImage = null;
            trayScene = null;
            tray = null;
            history = null;
            host = null;
            stagedSceneSettings = null;
            state.Sample(null, 0);
        }
    }
}
