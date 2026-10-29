using System;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using Dice.UnityContent;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Editor
{
    /** Samples the authored encounter scene without a gameplay session. */
    public sealed class TrayCompositionPreviewWindow : EditorWindow
    {
        private const string TableKey = "Dice.TrayCompositionPreview.Table";
        // Any fixed seed: the preview samples one repeatable opening.
        private const ulong PreviewSeed = 60;
        /** The id of the one-charm encounter the history preview fabricates; no content lists it. */
        private static readonly EncounterDefinition.Id PreviewEncounterId =
            new EncounterDefinition.Id(new Guid("6d1c3a52f0b84e8e9a7c2b4d5e6f7a81"));
        // The sample opening the gameplay overlay shows: the first of three stages, its threshold and its hands and rerolls.
        private const int SampleStageCount = 3;
        private const long SampleThreshold = 120;
        private const int SampleHands = 3;
        private const int SampleRerolls = 3;
        // The preview options, the staged view and its output are internal so EditMode tests drive and inspect the stage.
        internal EncounterTablePresentationAsset table;
        [SerializeField] internal bool showGameplayUi = true;
        [SerializeField] internal bool narrow;
        [SerializeField] internal int historyCount = 6;
        [SerializeField] internal float historyScroll;
        [SerializeField] internal float sampleTime;
        [SerializeField] private bool showPreviewOptions;
        private SpiritEncounterPreviewScene encounter;
        internal SpiritEncounterView view;
        private Camera camera;
        internal RenderTexture output;
        private SpiritTrayScene trayScene;
        private SpiritDiceTray dice;
        private SpiritModifierHistoryRenderer history;
        private SpiritHostPortrait host;
        private SpiritGameplayHud gameplayUi;
        private SceneLightingMaterial compositor;
        private RawImage roomImage, tableImage;
        private EncounterTablePresentationAsset stagedTable;
        private string stagedSourceSettings;
        internal bool rebuild;
        private readonly EncounterPreviewPipeline pipeline = new EncounterPreviewPipeline();
        private readonly SceneLightingState lighting = new SceneLightingState();
        internal readonly int[] faces = { 1, 2, 3, 4, 5 };
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
            pipeline.ReleaseAfter(ReleaseStage);
        }

        private void SourceChanged() { rebuild = true; Repaint(); }

        private void PlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode) pipeline.ReleaseAfter(ReleaseStage);
            Repaint();
        }

        private void OnGUI()
        {
            table = PreviewAssetSelection.Field("Encounter table", table, TableKey);
            narrow = GUILayout.Toolbar(narrow ? 1 : 0, new[] { SizeLabel(false), SizeLabel(true) }) == 1;
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

        // Internal so EditMode tests drive the stage without the window's GUI.
        internal void EnsureStage()
        {
            if (table == null) throw new InvalidOperationException("Select a table with an authored encounter scene.");
            string settings = SpiritEncounterPreviewScene.SourceSettings(table);
            if (encounter == null || stagedTable != table || rebuild || settings != stagedSourceSettings)
            {
                ReleaseStage();
                pipeline.Acquire();
                encounter = SpiritEncounterPreviewScene.Open(table, narrow);
                view = encounter.View;
                camera = encounter.Camera;
                output = encounter.Output;
                trayScene = view.TrayScene;
                dice = trayScene.DiceTray;
                history = trayScene.HistoryRenderer;
                host = view.HostPortrait;
                gameplayUi = view.GameplayHud;
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
            hud.ShowEncounterTitle(table.DisplayTitle);
            hud.ShowStageTarget(1, SampleStageCount, SampleThreshold);
            hud.ShowHandCounters(SampleHands, SampleRerolls);
            hud.ShowIdleScore(0, 0);
            hud.ShowStatus("COMPOSITION PREVIEW");
            var rule = table.HudRuleCard;
            if (ruleMessage != null) hud.RenderEncounterRuleBadge(rule, true, true, ruleMessage);
            else hud.RenderEncounterRule(ruleSnapshot, rule);
        }

        /**
         * The opening snapshot of the encounter of the first Encounter beat, in catalog order, that plays on the table; the HUD
         * badge fills its card text from that encounter's rule as the game does. Without such a journey there is no rule
         * to fill it from, so the badge shows a selection message in place of its text.
         */
        private void PrepareRuleSnapshot()
        {
            ruleSnapshot = null;
            ruleMessage = null;
            if (table.HudRuleCard == null) return;
            EnsureContent();
            var encounter = configuration.JourneyCatalog?.Entries
                .Where(entry => entry != null)
                .SelectMany(entry => Enumerable.Range(0, entry.Beats.Count)
                    .Where(index => entry.TableSceneAt(index) == table).Select(entry.EncounterBeatAt))
                .FirstOrDefault(beat => beat != null)?.Encounter;
            if (encounter == null)
            {
                ruleMessage = "Select a table a journey plays on to preview its rule badge text.";
                return;
            }
            ruleSnapshot = EncounterSession.CreateEncounter(content.Catalog, encounter.Identity, PreviewSeed).Snapshot;
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
                .Select(category => new HandDefinition(category, 1, 1, 0, 0, 0)).ToArray();
            foreach (ModifierDefinition definition in content.Catalog.Modifiers)
            {
                if (definition.Phase != ModifierPhase.InitialRoll) continue;
                var definitionEncounter = new EncounterDefinition(PreviewEncounterId, new[] { new StageDefinition(1000000, 3, 3) },
                    startingModifiers: new[] { definition.Id });
                var catalog = new MechanicalCatalog("preview.table", hands, new[] { definitionEncounter }, new[] { definition });
                var contributions = EncounterSession.CreateEncounter(catalog, PreviewEncounterId, PreviewSeed).Snapshot.ModifierContributions;
                if (contributions.Count == 0) continue;
                charms = presentation;
                historyDefinition = definition;
                historyContribution = contributions[0];
                return;
            }
            throw new InvalidOperationException("The application's content has no charm that activates on an opening roll.");
        }

        // Internal so EditMode tests drive the stage without the window's GUI.
        internal void RenderPreview()
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
                EncounterPreviewPipeline.Render(trayScene.RenderCamera, trayScene.Texture);
                EncounterPreviewPipeline.Render(camera, output);
            }
            finally { RenderTexture.active = previous; GL.sRGBWrite = previousSrgb; }
        }

        private static string SizeLabel(bool narrowWidth)
            => SpiritEncounterPreviewScene.Width(narrowWidth) + " x " + SpiritEncounterPreviewScene.PreviewHeight;

        // Internal so EditMode tests drive the stage without the window's GUI.
        internal void ReleaseStage()
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
            output = null;
            camera = null;
            trayScene = null;
            dice = null;
            history = null;
            host = null;
            gameplayUi = null;
            roomImage = tableImage = null;
            stagedTable = null;
            stagedSourceSettings = null;
            lighting.Sample(null, 0);
        }
    }
}
