using System;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Dice.Editor
{
    /** Samples the authored encounter scene without a gameplay session. */
    public sealed class TrayCompositionPreviewWindow : EditorWindow
    {
        private const string PipelinePath = "Assets/Settings/SpiritPrototypePipeline.asset";
        private const string TableKey = "Dice.TrayCompositionPreview.Table";
        // Any fixed seed: the preview samples one repeatable opening.
        private const ulong PreviewSeed = 60;
        private EncounterTablePresentationAsset table;
        [SerializeField] private bool showGameplayUi = true;
        [SerializeField] private bool narrow;
        [SerializeField] private int historyCount = 6;
        [SerializeField] private float historyScroll;
        [SerializeField] private float sampleTime;
        [SerializeField] private bool showPreviewOptions;
        private SpiritEncounterPreviewScene encounter;
        private SpiritEncounterView view;
        private GameObject stage;
        private Camera camera;
        private RenderTexture output;
        private SpiritTrayScene trayScene;
        private SpiritDiceTray dice;
        private SpiritModifierHistoryRenderer history;
        private SpiritHostPortrait host;
        private SpiritGameplayHud gameplayUi;
        private RectTransform gameplayUiRoot;
        private SceneLightingMaterial compositor;
        private RawImage roomImage, tableImage;
        private EncounterTablePresentationAsset stagedTable;
        private string stagedSourceSettings;
        private bool rebuild;
        private RenderPipelineAsset previousPipeline;
        private UniversalRenderPipelineAsset previewPipeline;
        private readonly SceneLightingState lighting = new SceneLightingState();
        private readonly int[] faces = { 1, 2, 3, 4, 5 };
        private DiceApplicationConfig configuration;
        private MechanicalContent content;
        private ModifierPresentationCatalog charms;
        private ModifierDefinition historyDefinition;
        private ModifierContribution historyContribution;
        private string message;
        private EncounterSnapshot ruleSnapshot;
        private string ruleMessage;

        [MenuItem("Dice/Tray Composition Preview")]
        public static void Open()
        {
            var window = GetWindow<TrayCompositionPreviewWindow>("Tray Composition Preview");
            window.minSize = new Vector2(420, 650);
            if (Selection.activeObject is EncounterTablePresentationAsset selected)
            {
                window.table = selected;
                PreviewAssetSelection.Save(TableKey, selected);
            }
            window.Show();
        }

        private void OnEnable()
        {
            table = PreviewAssetSelection.Load<EncounterTablePresentationAsset>(TableKey);
            EditorApplication.playModeStateChanged += PlayModeChanged;
            EditorApplication.projectChanged += SourceChanged;
            Undo.undoRedoPerformed += SourceChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorApplication.projectChanged -= SourceChanged;
            Undo.undoRedoPerformed -= SourceChanged;
            try { ReleaseStage(); }
            finally { ReleasePipeline(); }
        }

        private void SourceChanged() { rebuild = true; Repaint(); }

        private void PlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                try { ReleaseStage(); }
                finally { ReleasePipeline(); }
            }
            Repaint();
        }

        private void OnGUI()
        {
            table = PreviewAssetSelection.Field("Encounter table", table, TableKey);
            narrow = GUILayout.Toolbar(narrow ? 1 : 0, new[] { "540 x 960", "443 x 960" }) == 1;
            showGameplayUi = EditorGUILayout.Toggle("Show gameplay UI", showGameplayUi);
            showPreviewOptions = EditorGUILayout.Foldout(showPreviewOptions, "Preview options", true);
            if (showPreviewOptions)
            {
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
            DrawAuthoring();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Return to Edit Mode to preview tray composition.", MessageType.Info);
                return;
            }
            if (GUILayout.Button("Refresh preview")) SourceChanged();
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
            catch (Exception error) { message = error.Message; GUI.Label(area, message, EditorStyles.wordWrappedLabel); }
        }

        private void DrawAuthoring()
        {
            if (table == null) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open encounter scene"))
                    AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<SceneAsset>(SpiritEncounterPreviewScene.PathFor(table)));
                if (GUILayout.Button("Open encounter rig") && table.TrayPresentation?.RigPrefab != null)
                    AssetDatabase.OpenAsset(table.TrayPresentation.RigPrefab.gameObject);
            }
            EditorGUILayout.HelpBox("Edit tray placement on the encounter scene's DiceTrayFrame. Edit cameras, lights, " +
                "geometry, and materials on the scene or its rig prefab. Open scene edits and Undo are reflected here; " +
                "save prefab edits before refreshing.", MessageType.Info);
        }

        private void EnsureStage()
        {
            if (table == null) throw new InvalidOperationException("Select a table with an authored encounter scene.");
            string settings = SpiritEncounterPreviewScene.SourceSettings(table);
            if (encounter == null || stagedTable != table || rebuild || settings != stagedSourceSettings)
            {
                ReleaseStage();
                AcquirePipeline();
                encounter = SpiritEncounterPreviewScene.Open(table, narrow);
                view = encounter.View;
                stage = encounter.Root;
                camera = encounter.Camera;
                output = encounter.Output;
                trayScene = view.TrayScene;
                dice = trayScene.DiceTray;
                history = trayScene.HistoryRenderer;
                host = view.HostPortrait;
                gameplayUi = view.GameplayHud;
                gameplayUiRoot = (RectTransform)gameplayUi.transform;
                roomImage = view.RoomImage;
                tableImage = view.TableImage;
                stagedTable = table;
                stagedSourceSettings = settings;
                rebuild = false;
                gameplayUi.ConfigureEncounterRuleSigil(table.HostPresentation);
                PrepareRuleSnapshot();
                RefreshHistory();
            }
            encounter.Resize(narrow);
            trayScene.ApplyPresentationSettings();
            dice.SetValues(new DiceValues(faces[0], faces[1], faces[2], faces[3], faces[4]));
            if (history.Count != historyCount) RefreshHistory();
            history.SetScroll(historyScroll);
            RefreshGameplayValues();
            encounter.ShowHud(showGameplayUi);
            Shader shader = table.SceneLighting != null ? table.SceneLighting.Shader : null;
            if (compositor != null && compositor.Material.shader != shader) { compositor.Dispose(); compositor = null; }
            if (compositor == null && shader != null) compositor = new SceneLightingMaterial(shader);
        }

        private void RefreshGameplayValues()
        {
            var hud = gameplayUi;
            hud.EncounterTitle.text = table.DisplayTitle;
            hud.StageLabel.text = "STAGE 1 / 3";
            hud.TargetLabel.text = "120";
            hud.TargetCaption.text = "TO CLEAR";
            hud.SetResources(3, 3);
            hud.ResourcesLabel.gameObject.SetActive(false);
            // The hand rank shows only while a played hand is scored.
            hud.CategoryLabel.text = "";
            hud.SetScoreValues(0, 0);
            hud.HandScoreLabel.text = "0";
            hud.SelectionLabel.text = "";
            hud.ScoreDetail.text = "";
            hud.StatusLabel.text = "COMPOSITION PREVIEW";
            var rule = table.HudRuleCard;
            if (ruleMessage != null) hud.RenderEncounterRuleBadge(rule, true, true, ruleMessage);
            else hud.RenderEncounterRule(ruleSnapshot, rule);
        }

        /**
         * The opening snapshot of the encounter of the first journey, in catalog order, that plays on the table; the HUD
         * badge fills its card text from that encounter's rule as the game does. Without such a journey there is no rule
         * to fill it from, so the badge shows a selection message in place of its text.
         */
        private void PrepareRuleSnapshot()
        {
            ruleSnapshot = null;
            ruleMessage = null;
            if (table.HudRuleCard == null) return;
            EnsureContent();
            var journey = configuration.JourneyCatalog?.Entries
                .FirstOrDefault(entry => entry != null && !entry.IsStoryOnly && entry.TablePresentation == table);
            if (journey == null)
            {
                ruleMessage = "Select a table a journey plays on to preview its rule badge text.";
                return;
            }
            ruleSnapshot = EncounterSession.CreateEncounter(content.Catalog, journey.EncounterId, PreviewSeed).Snapshot;
        }

        /** Loads the application's configuration and builds its content once per staged preview. */
        private void EnsureContent()
        {
            if (content != null) return;
            DiceApplicationConfig[] configurations = AssetDatabase.FindAssets("t:DiceApplicationConfig")
                .Select(guid => AssetDatabase.LoadAssetAtPath<DiceApplicationConfig>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
            if (configurations.Length != 1)
                throw new InvalidOperationException("The preview requires exactly one application configuration.");
            configurations[0].Validate();
            content = configurations[0].CatalogAsset.Build();
            configuration = configurations[0];
        }

        private void RefreshHistory()
        {
            history.Clear();
            if (historyCount == 0) return;
            EnsureHistoryCharm();
            for (int i = 0; i < historyCount; ++i) history.Add(historyContribution, charms, historyDefinition, false);
        }

        /**
         * Loads the application's content and skin and shows the first of its charms that activates on the opening roll
         * of an encounter holding only that charm.
         */
        private void EnsureHistoryCharm()
        {
            if (charms != null) return;
            EnsureContent();
            var presentation = new ModifierPresentationCatalog(configuration.ModifierSkin, content);
            var hands = Enum.GetValues(typeof(HandCategory)).Cast<HandCategory>()
                .Select(category => new HandDefinition("preview.hand." + category, category, 1, 1, 0, 0, 0)).ToArray();
            foreach (ModifierDefinition definition in content.Catalog.Modifiers)
            {
                if (definition.Phase != ModifierPhase.InitialRoll) continue;
                var definitionEncounter = new EncounterDefinition("preview.table", new[] { new StageDefinition(1000000, 3, 3) },
                    startingModifiers: new[] { definition.Id });
                var catalog = new MechanicalCatalog("preview.table", hands, new[] { definitionEncounter }, new[] { definition });
                var contributions = EncounterSession.CreateEncounter(catalog, definitionEncounter.Id, PreviewSeed).Snapshot.ModifierContributions;
                if (contributions.Count == 0) continue;
                charms = presentation;
                historyDefinition = definition;
                historyContribution = contributions[0];
                return;
            }
            throw new InvalidOperationException("The application's content has no charm that activates on an opening roll.");
        }

        private void RenderPreview()
        {
            lighting.Sample(table.SceneLighting, sampleTime);
            compositor?.Apply(lighting);
            roomImage.material = tableImage.material = compositor?.Material;
            SceneLightingState state = table.SceneLighting != null ? lighting : null;
            trayScene.SetSceneLighting(state);
            history.SetSceneLighting(state);
            host.Sample(sampleTime);
            host.SetSceneLighting(state, table.HostLightingPosition);
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

        private void ReleaseStage()
        {
            compositor?.Dispose();
            compositor = null;
            charms = null;
            historyDefinition = null;
            content = null;
            configuration = null;
            ruleSnapshot = null;
            ruleMessage = null;
            encounter?.Dispose();
            encounter = null;
            view = null;
            stage = null;
            output = null;
            camera = null;
            trayScene = null;
            dice = null;
            history = null;
            host = null;
            gameplayUi = null;
            gameplayUiRoot = null;
            roomImage = tableImage = null;
            stagedTable = null;
            stagedSourceSettings = null;
            lighting.Sample(null, 0);
        }
    }
}
