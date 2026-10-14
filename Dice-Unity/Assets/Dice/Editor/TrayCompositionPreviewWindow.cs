using System;
using System.Linq;
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
    /** The runtime tray and split artwork share a portrait stage for authoring camera composition. */
    public sealed class TrayCompositionPreviewWindow : EditorWindow
    {
        private const int UiLayer = 30;
        private const string PipelinePath = "Assets/Settings/SpiritPrototypePipeline.asset";
        private const string ThemePath = "Assets/Dice/Presentation/Shared/SpiritHouseTheme.asset";
        [SerializeField] private EncounterTablePresentationAsset table;
        [SerializeField] private SpiritUiTheme uiTheme;
        [SerializeField] private bool showGameplayUi = true;
        [SerializeField] private bool narrow;
        [SerializeField] private int historyCount = 6;
        [SerializeField] private float historyScroll;
        [SerializeField] private float sampleTime;
        [SerializeField] private bool showGeometry;
        [SerializeField] private bool showCamera;
        [SerializeField] private bool showPreviewOptions;
        private Vector2 authoringScroll;
        private Scene previewScene;
        private GameObject stage;
        private Camera camera;
        private RenderTexture output;
        private SpiritTrayScene trayScene;
        private SpiritDiceTray dice;
        private SpiritModifierHistoryRenderer history;
        private SpiritHostPortrait host;
        private SpiritEncounterView gameplayUi;
        private RectTransform gameplayUiRoot;
        private SpiritUiTheme stagedUiTheme;
        private SceneLightingMaterial compositor;
        private RawImage roomImage, tableImage;
        private RectTransform canvasRoot, frame;
        private RawImage image;
        private EncounterTablePresentationAsset stagedTable;
        private SpiritTrayGeometry stagedGeometry;
        private string stagedGeometrySettings;
        private Shader stagedDiceShader;
        private Texture2D stagedBoneTexture, stagedWoodTexture;
        private SpiritHostPresentationAsset stagedHost;
        private bool rebuildGeometry, rebuildHost;
        private RenderPipelineAsset previousPipeline;
        private UniversalRenderPipelineAsset previewPipeline;
        private readonly SceneLightingState lighting = new SceneLightingState();
        private readonly int[] faces = { 1, 2, 3, 4, 5 };
        private string message;

        [MenuItem("Dice/Tray Composition Preview")]
        public static void Open()
        {
            var window = GetWindow<TrayCompositionPreviewWindow>("Tray Composition Preview");
            window.minSize = new Vector2(420, 650);
            if (Selection.activeObject is EncounterTablePresentationAsset selected) window.table = selected;
            window.Show();
        }

        private void OnEnable()
        {
            if (table == null) table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(
                "Assets/Dice/Content/Presentation/SpiritHouse/LastLanternTable.asset");
            if (uiTheme == null) uiTheme = AssetDatabase.LoadAssetAtPath<SpiritUiTheme>(ThemePath);
            EditorApplication.playModeStateChanged += PlayModeChanged;
            Undo.undoRedoPerformed += UndoRedoPerformed;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            Undo.undoRedoPerformed -= UndoRedoPerformed;
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

        private void UndoRedoPerformed()
        {
            message = null;
            Repaint();
        }

        private void OnGUI()
        {
            authoringScroll = EditorGUILayout.BeginScrollView(authoringScroll,
                GUILayout.Height(Mathf.Min(320, position.height * .4f)));
            EditorGUI.BeginChangeCheck();
            table = (EncounterTablePresentationAsset)EditorGUILayout.ObjectField("Encounter table", table,
                typeof(EncounterTablePresentationAsset), false);
            narrow = GUILayout.Toolbar(narrow ? 1 : 0, new[] { "540 x 960", "443 x 960" }) == 1;
            showGameplayUi = EditorGUILayout.Toggle("Show gameplay UI", showGameplayUi);
            showPreviewOptions = EditorGUILayout.Foldout(showPreviewOptions, "Preview options", true);
            if (showPreviewOptions)
            {
                uiTheme = (SpiritUiTheme)EditorGUILayout.ObjectField("UI theme", uiTheme, typeof(SpiritUiTheme), false);
                historyCount = EditorGUILayout.IntSlider("History dice", historyCount, 0, 18);
                historyScroll = EditorGUILayout.Slider("History scroll", historyScroll, 0, 1);
                sampleTime = EditorGUILayout.FloatField("Lighting time", Mathf.Max(0, sampleTime));
                using (new EditorGUILayout.HorizontalScope())
                    for (int i = 0; i < faces.Length; ++i)
                    {
                        GUILayout.Label("Die " + (i + 1), GUILayout.Width(35));
                        faces[i] = Mathf.Clamp(EditorGUILayout.IntField(faces[i], GUILayout.MinWidth(25)), 1, 6);
                    }
            }
            if (EditorGUI.EndChangeCheck()) Repaint();
            DrawAuthoring();
            EditorGUILayout.EndScrollView();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Return to Edit Mode to preview tray composition.", MessageType.Info);
                return;
            }
            if (GUILayout.Button("Refresh preview"))
            {
                rebuildGeometry = rebuildHost = true;
                Repaint();
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

        private void DrawAuthoring()
        {
            if (table == null) return;
            var tableObject = new SerializedObject(table);
            tableObject.Update();
            EditorGUILayout.PropertyField(tableObject.FindProperty("trayPresentation"));
            ApplyAuthoring(tableObject);
            var asset = table.TrayPresentation;
            if (asset == null) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select tray asset")) Selection.activeObject = asset;
                if (GUILayout.Button("Open geometry prefab") && asset.GeometryPrefab != null)
                    AssetDatabase.OpenAsset(asset.GeometryPrefab.gameObject);
            }
            var serialized = new SerializedObject(asset);
            serialized.Update();
            SpiritTrayPresentationAssetEditor.DrawPlacement(serialized);
            EditorGUILayout.PropertyField(serialized.FindProperty("CameraEulerAngles"), new GUIContent("Viewing angle"));
            showCamera = EditorGUILayout.Foldout(showCamera, "Rendering and materials", true);
            if (showCamera)
            {
                EditorGUILayout.PropertyField(serialized.FindProperty("GeometryPrefab"));
                SpiritTrayPresentationAssetEditor.DrawRendering(serialized);
            }
            ApplyAuthoring(serialized);
            showGeometry = EditorGUILayout.Foldout(showGeometry, "Authored bay anchors and dimensions", true);
            if (!showGeometry || asset.GeometryPrefab == null) return;
            var geometry = new SerializedObject(asset.GeometryPrefab);
            geometry.Update();
            foreach (string name in new[] { "DiceSlots", "DiceLabelAnchors", "DiceScale", "ModifierRoot",
                "ModifierWidth", "ModifierDepth", "ModifierDieSize", "ModifierSlotSpacing", "ModifierVisibleCapacity" })
                EditorGUILayout.PropertyField(geometry.FindProperty(name), true);
            ApplyAuthoring(geometry);
        }

        private bool ApplyAuthoring(SerializedObject serialized)
        {
            if (!serialized.ApplyModifiedProperties()) return false;
            if (serialized.targetObject is SpiritTrayGeometry) rebuildGeometry = true;
            Repaint();
            return true;
        }

        private void EnsureStage()
        {
            if (table == null || table.TrayPresentation == null)
                throw new InvalidOperationException("Select a table with an authored tray presentation.");
            table.TrayPresentation.ValidateScene();
            table.SceneLighting?.Validate();
            if (stage != null)
            {
                RefreshPreview();
                return;
            }
            AcquirePipeline();
            previewScene = EditorSceneManager.NewPreviewScene();
            stage = Object("Tray Composition Preview");
            camera = Object("Composition Camera").AddComponent<Camera>();
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
            camera.allowHDR = camera.allowMSAA = false;
            output = new RenderTexture(narrow ? 443 : 540, 960, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            { name = "Tray Composition Preview", hideFlags = HideFlags.HideAndDontSave };
            output.Create();
            camera.targetTexture = output;
            canvasRoot = Object("Composition Canvas", typeof(RectTransform)).GetComponent<RectTransform>();
            canvasRoot.SetParent(stage.transform, false);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540, 960);
            scaler.matchWidthOrHeight = 1;
            roomImage = Image("Room", canvasRoot, new Vector2(0, table.RoomSplit), Vector2.one);
            tableImage = Image("ForegroundTable", canvasRoot, Vector2.zero, new Vector2(1, table.RoomSplit));
            trayScene = stage.AddComponent<SpiritTrayScene>();
            dice = stage.AddComponent<SpiritDiceTray>();
            history = stage.AddComponent<SpiritModifierHistoryRenderer>();
            frame = Object("Tray Composition Frame", typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(canvasRoot, false);
            image = Image("SharedTray", frame, Vector2.zero, Vector2.one);
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            if (stage == null) return;
            if (table == null || table.TrayPresentation == null)
                throw new InvalidOperationException("Select a table with an authored tray presentation.");
            var asset = table.TrayPresentation;
            asset.ValidateScene();
            table.SceneLighting?.Validate();
            string geometrySettings = EditorJsonUtility.ToJson(asset.GeometryPrefab);
            bool replacePresentation = trayScene.Presentation != asset || trayScene.RenderRoot == null;
            bool replaceGeometry = rebuildGeometry || stagedGeometry != asset.GeometryPrefab ||
                stagedGeometrySettings != geometrySettings;
            bool replaceContent = replacePresentation || replaceGeometry || stagedDiceShader != asset.DiceShader ||
                stagedBoneTexture != asset.BoneTexture || stagedWoodTexture != asset.WoodTexture;
            if (replaceContent)
            {
                dice.ReleasePresentation();
                history.ReleasePresentation();
                trayScene.Configure(asset);
                if (replacePresentation) trayScene.Initialize();
                else if (replaceGeometry) trayScene.RebuildGeometry();
                SceneManager.MoveGameObjectToScene(trayScene.RenderRoot.gameObject, previewScene);
                trayScene.RenderCamera.scene = previewScene;
                trayScene.RenderCamera.enabled = false;
                dice.Configure(asset, trayScene);
                dice.Initialize();
                history.Configure(asset, trayScene);
                history.Initialize();
                foreach (Transform child in trayScene.RenderRoot.GetComponentsInChildren<Transform>(true))
                    child.gameObject.hideFlags = HideFlags.HideAndDontSave;
                stagedGeometry = asset.GeometryPrefab;
                stagedGeometrySettings = geometrySettings;
                stagedDiceShader = asset.DiceShader;
                stagedBoneTexture = asset.BoneTexture;
                stagedWoodTexture = asset.WoodTexture;
                rebuildGeometry = false;
            }
            trayScene.ApplyPresentationSettings();
            dice.SetValues(new DiceValues(faces[0], faces[1], faces[2], faces[3], faces[4]));
            if (history.Count != historyCount) RefreshHistory();
            history.SetScroll(historyScroll);
            int width = narrow ? 443 : 540;
            if (output.width != width)
            {
                camera.targetTexture = null;
                output.Release();
                output.width = width;
                output.Create();
                camera.targetTexture = output;
            }
            camera.aspect = (float)output.width / output.height;
            Rect placement = asset.GetCompositionRect((float)output.width / output.height);
            frame.anchorMin = placement.min;
            frame.anchorMax = placement.max;
            frame.offsetMin = frame.offsetMax = Vector2.zero;
            image.texture = trayScene.Texture;
            ((RectTransform)roomImage.transform).anchorMin = new Vector2(0, table.RoomSplit);
            ((RectTransform)tableImage.transform).anchorMax = new Vector2(1, table.RoomSplit);
            roomImage.uvRect = new Rect(0, table.RoomTableEdge, 1, 1 - table.RoomTableEdge);
            tableImage.uvRect = new Rect(0, 0, 1, table.RoomTableEdge);
            Shader shader = table.SceneLighting != null ? table.SceneLighting.Shader : null;
            if (compositor != null && compositor.Material.shader != shader)
            {
                roomImage.material = tableImage.material = null;
                compositor.Dispose();
                compositor = null;
            }
            if (compositor == null && shader != null) compositor = new SceneLightingMaterial(shader);
            if (rebuildHost || stagedHost != table.HostPresentation || stagedTable != table)
            {
                if (host != null) DestroyImmediate(host.gameObject);
                host = null;
                if (table.HostPresentation != null)
                {
                    host = table.HostPresentation.Create(canvasRoot);
                    host.transform.SetSiblingIndex(frame.GetSiblingIndex());
                    foreach (Transform child in host.GetComponentsInChildren<Transform>(true))
                    { child.gameObject.layer = UiLayer; child.gameObject.hideFlags = HideFlags.HideAndDontSave; }
                }
                stagedHost = table.HostPresentation;
                rebuildHost = false;
            }
            stagedTable = table;
            RefreshGameplayUi();
        }

        private void RefreshGameplayUi()
        {
            if (gameplayUiRoot != null && stagedUiTheme != uiTheme)
            {
                DestroyImmediate(gameplayUiRoot.gameObject);
                gameplayUiRoot = null;
                gameplayUi = null;
            }
            if (gameplayUiRoot == null && uiTheme != null)
            {
                gameplayUiRoot = Object("Gameplay UI", typeof(RectTransform)).GetComponent<RectTransform>();
                gameplayUiRoot.SetParent(canvasRoot, false);
                gameplayUiRoot.anchorMin = Vector2.zero;
                gameplayUiRoot.anchorMax = Vector2.one;
                gameplayUiRoot.offsetMin = gameplayUiRoot.offsetMax = Vector2.zero;
                gameplayUi = gameplayUiRoot.gameObject.AddComponent<SpiritEncounterView>();
                gameplayUi.BuildCompositionOverlay(gameplayUiRoot, uiTheme);
                stagedUiTheme = uiTheme;
            }
            if (gameplayUiRoot == null) return;
            gameplayUi.RefreshCompositionOverlay(table, new DiceValues(faces[0], faces[1], faces[2], faces[3], faces[4]));
            foreach (Transform child in gameplayUiRoot.GetComponentsInChildren<Transform>(true))
            { child.gameObject.layer = UiLayer; child.gameObject.hideFlags = HideFlags.HideAndDontSave; }
            gameplayUiRoot.SetAsLastSibling();
            gameplayUiRoot.gameObject.SetActive(showGameplayUi);
        }

        private void RefreshHistory()
        {
            history.Clear();
            var definition = new ModifierDefinition("preview.history", "preview.history", ModifierPhase.InitialRoll,
                new AlwaysCondition(), new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 1) });
            var encounter = new EncounterDefinition("preview.table", new[] { new StageDefinition(1000000, 3, 3) },
                startingModifiers: new[] { definition.Id });
            var hands = Enum.GetValues(typeof(HandCategory)).Cast<HandCategory>()
                .Select(category => new HandDefinition("preview.hand." + category, category, 1, 1, 0, 0, 0));
            var catalog = new MechanicalCatalog("preview.table", hands, new[] { encounter }, new[] { definition });
            var contribution = EncounterSession.CreateEncounter(catalog, encounter.Id, 60).Snapshot.ModifierContributions[0];
            for (int i = 0; i < historyCount; ++i) history.Add(contribution, definition, false);
        }

        private void RenderPreview()
        {
            lighting.Sample(table.SceneLighting, sampleTime);
            compositor?.Apply(lighting);
            foreach (RawImage image in new[] { roomImage, tableImage })
            {
                image.texture = table.SceneLighting != null ? table.SceneLighting.BaseTexture : table.RoomTexture;
                image.material = compositor?.Material;
            }
            SceneLightingState state = table.SceneLighting != null ? lighting : null;
            trayScene.SetSceneLighting(state);
            dice.SetSceneLighting(state);
            history.SetSceneLighting(state);
            if (host != null)
            { host.Sample(sampleTime); host.SetSceneLighting(state, table.HostLightingPosition); }
            var previous = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            try
            {
                Canvas.ForceUpdateCanvases();
                Render(trayScene.RenderCamera, trayScene.Texture);
                Render(camera, output);
            }
            finally { RenderTexture.active = previous; GL.sRGBWrite = previousSrgb; }
        }

        private static void Render(Camera renderCamera, RenderTexture target)
        {
            if (GraphicsSettings.currentRenderPipeline == null) renderCamera.Render();
            else
            {
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(renderCamera, request))
                    throw new InvalidOperationException("The active pipeline cannot preview this camera.");
                RenderPipeline.SubmitRenderRequest(renderCamera, request);
            }
        }

        private void AcquirePipeline()
        {
            if (previewPipeline != null) return;
            previewPipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (previewPipeline == null) throw new InvalidOperationException("The encounter preview pipeline is unavailable.");
            previousPipeline = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = previewPipeline;
        }

        private void ReleasePipeline()
        {
            if (previewPipeline == null) return;
            if (QualitySettings.renderPipeline == previewPipeline) QualitySettings.renderPipeline = previousPipeline;
            previewPipeline = null;
            previousPipeline = null;
        }

        private GameObject Object(string name, params Type[] components)
        {
            var result = new GameObject(name, components) { layer = UiLayer, hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(result, previewScene);
            return result;
        }

        private RawImage Image(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = Object(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        private void ReleaseStage()
        {
            compositor?.Dispose();
            compositor = null;
            // Preview scenes do not guarantee runtime component destruction callbacks in Edit Mode.
            dice?.ReleasePresentation();
            history?.ReleasePresentation();
            trayScene?.ReleasePresentation();
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
            previewScene = default;
            if (output != null) { output.Release(); DestroyImmediate(output); }
            output = null; stage = null; camera = null; trayScene = null; dice = null; history = null;
            roomImage = tableImage = null; host = null; stagedTable = null;
            gameplayUi = null; gameplayUiRoot = null; stagedUiTheme = null;
            canvasRoot = frame = null; image = null;
            stagedGeometry = null; stagedGeometrySettings = null; stagedHost = null;
            stagedDiceShader = null; stagedBoneTexture = stagedWoodTexture = null;
            rebuildGeometry = rebuildHost = false;
            lighting.Sample(null, 0);
        }
    }
}
