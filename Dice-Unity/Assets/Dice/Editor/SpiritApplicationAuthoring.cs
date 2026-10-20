using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Dice.App;
using Dice.Presentation;
using Dice.UnityContent;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dice.Editor
{
    /** Creates the application entry scene and authored presentation scenes without replacing authored assets. */
    public static class SpiritApplicationAuthoring
    {
        public const string ApplicationScenePath = "Assets/Scenes/Application.unity";
        public const string FrontendScenePath = "Assets/Scenes/Frontend.unity";
        private const string DiagnosticScenePath = "Assets/Scenes/DiagnosticEncounter.unity";
        private const string UiFolder = "Assets/Dice/Content/Presentation/UI/";
        private const string ConfigurationPath = "Assets/Dice/Content/DiceApplicationConfig.asset";
        private const string InputActionsPath = "Assets/Settings/InputSystem_Actions.inputactions";
        private static readonly string[] EncounterScenePaths =
        {
            "Assets/Scenes/FoxEncounter.unity",
            "Assets/Scenes/WatchtowerEncounter.unity",
            "Assets/Scenes/MothEncounter.unity"
        };
        private static readonly string[] TablePaths =
        {
            "Assets/Dice/Content/Presentation/SpiritHouse/LastLanternTable.asset",
            "Assets/Dice/Content/Presentation/Watchtower/WatchtowerTable.asset",
            "Assets/Dice/Content/Presentation/MothCrossroads/MothCrossroadsTable.asset"
        };

        [MenuItem("Dice/Create Authored Application")]
        public static void CreateAssets()
        {
            PreserveUntitledScenes();
            SpiritEncounterObjectAuthoring.CreateAssets();
            SpiritCharmPrefabAuthoring.CreateAssets();
            SpiritFrontendPrefabAuthoring.CreateAssets();
            SpiritHudPrefabAuthoring.CreateAssets();
            SpiritJourneyPrefabAuthoring.CreateAssets();
            SpiritEncounterScreenAuthoring.CreateAssets();
            SpiritDiagnosticPrefabAuthoring.CreateAssets();
            var startup = CreateStartup();
            var frontend = CreateFrontend();
            var tables = TablePaths.Select(Load<EncounterTablePresentationAsset>).ToArray();
            var pipeline = EnsurePipeline();
            CreateScene(ApplicationScenePath, () => ApplicationScene(startup, pipeline));
            CreateScene(FrontendScenePath, () => Instantiate(frontend, null));
            for (int i = 0; i < tables.Length; ++i)
            {
                var table = tables[i];
                CreateScene(EncounterScenePaths[i], () => EncounterScene(table));
                SetSceneReference(table, "scene", EncounterScenePaths[i]);
            }
            RepairApplicationSceneReferences();
            var configuration = Load<DiceApplicationConfig>(ConfigurationPath);
            SetSceneReference(configuration, "frontendScene", FrontendScenePath);
            MigrateDiagnostic(configuration.CatalogAsset);
            ConfigureAddressables(configuration);
            ConfigureBuildScenes();
            RepairAuthoredRenderTargets();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Dice/Repair Application Scene References")]
        public static void RepairApplicationSceneReferences()
        {
            PreserveUntitledScenes();
            var legacy = Regex.Matches(File.ReadAllText(ApplicationScenePath),
                @"(?m)^  defaultTable:\s*\{fileID:\s*-?\d+,\s*guid:\s*([0-9a-fA-F]{32}),[^}]*\}\s*$");
            if (legacy.Count > 1) throw new InvalidOperationException("The application scene has multiple table fallback references.");
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ApplicationScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty)
                throw new InvalidOperationException("Save the application scene before repairing its references.");
            if (opened) scene = EditorSceneManager.OpenScene(ApplicationScenePath, OpenSceneMode.Additive);
            var router = Find<SpiritApplicationPresentation>(scene)
                ?? throw new InvalidOperationException("The application scene requires its presentation router.");
            var serialized = new SerializedObject(router);
            var guid = serialized.FindProperty("defaultEncounterScene").FindPropertyRelative("m_AssetGUID");
            bool changed = string.IsNullOrEmpty(guid.stringValue);
            if (changed)
            {
                string sceneGuid;
                if (legacy.Count == 1)
                {
                    var table = Load<EncounterTablePresentationAsset>(AssetDatabase.GUIDToAssetPath(legacy[0].Groups[1].Value));
                    if (table.Scene == null || !table.Scene.RuntimeKeyIsValid())
                        throw new InvalidOperationException("The application fallback table requires an authored scene reference.");
                    sceneGuid = table.Scene.AssetGUID;
                }
                else sceneGuid = AssetDatabase.AssetPathToGUID(EncounterScenePaths[0]);
                if (string.IsNullOrEmpty(sceneGuid))
                    throw new InvalidOperationException("Create the default encounter scene before repairing application references.");
                guid.stringValue = sceneGuid;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            if (changed || legacy.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Cannot save the application scene reference repair.");
            }
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }

        [MenuItem("Dice/Repair Authored Tray Render Targets")]
        public static void RepairAuthoredRenderTargets()
        {
            PreserveUntitledScenes();
            for (int i = 0; i < TablePaths.Length; ++i)
            {
                var table = Load<EncounterTablePresentationAsset>(TablePaths[i]);
                var target = RepairRigTarget(table.TrayPresentation);
                RepairSceneTarget(EncounterScenePaths[i], target);
            }
            EditorSceneManager.playModeStartScene = Load<SceneAsset>(ApplicationScenePath);
            AssetDatabase.SaveAssets();
        }

        private static RenderTexture RepairRigTarget(SpiritTrayPresentationAsset presentation)
        {
            if (presentation?.RigPrefab == null)
                throw new InvalidOperationException("The presentation requires an authored rig before assigning a render target.");
            string path = AssetDatabase.GetAssetPath(presentation.RigPrefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var rig = root.GetComponent<SpiritTrayScene>();
                var camera = rig.RenderCamera;
                if (camera == null) throw new InvalidOperationException("The rig requires its authored camera: " + path);
                var target = camera.targetTexture;
                if (target == null || !EditorUtility.IsPersistent(target)) target = CreateRenderTarget(presentation);
                if (camera.targetTexture != target || !camera.enabled)
                {
                    camera.targetTexture = target;
                    camera.enabled = true;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                return target;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static RenderTexture CreateRenderTarget(SpiritTrayPresentationAsset presentation)
        {
            const string folder = "Assets/Dice/Content/Presentation/Rigs/RenderTargets";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Dice/Content/Presentation/Rigs", "RenderTargets");
            string name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(presentation)) + "RenderTarget";
            string path = folder + "/" + name + ".renderTexture";
            var existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (existing != null) return existing;
            if (File.Exists(path)) throw new InvalidOperationException("The render target path contains another asset: " + path);
            var target = new RenderTexture(presentation.TextureSize.x, presentation.TextureSize.y, 24, RenderTextureFormat.ARGB32)
            {
                name = name, antiAliasing = 4, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                useMipMap = false, autoGenerateMips = false
            };
            AssetDatabase.CreateAsset(target, path);
            return target;
        }

        private static void RepairSceneTarget(string path, RenderTexture target)
        {
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty)
                throw new InvalidOperationException("Save the encounter scene before assigning its render target: " + path);
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var view = Find<SpiritEncounterView>(scene)
                ?? throw new InvalidOperationException("The encounter scene requires its authored screen: " + path);
            if (view.TrayScene?.RenderCamera == null || view.GameplayHud?.DiceTrayImage == null)
                throw new InvalidOperationException("The encounter screen requires its authored rig camera and tray image: " + path);
            var camera = view.TrayScene.RenderCamera;
            if (camera.targetTexture != null && EditorUtility.IsPersistent(camera.targetTexture)) target = camera.targetTexture;
            bool changed = camera.targetTexture != target || !camera.enabled || view.GameplayHud.DiceTrayImage.texture != target;
            if (changed)
            {
                camera.targetTexture = target;
                camera.enabled = true;
                view.GameplayHud.DiceTrayImage.texture = target;
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view.GameplayHud.DiceTrayImage);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save the encounter render target: " + path);
            }
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }

        private static void PreserveUntitledScenes()
        {
            string run = Guid.NewGuid().ToString("N");
            string backupDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Docs", "work",
                "application-authoring", run));
            string assetDirectory = "Assets/Addressables_TempApplicationAuthoring/" + run;
            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || !string.IsNullOrEmpty(scene.path)) continue;
                if (!AssetDatabase.IsValidFolder("Assets/Addressables_TempApplicationAuthoring"))
                    AssetDatabase.CreateFolder("Assets", "Addressables_TempApplicationAuthoring");
                if (!AssetDatabase.IsValidFolder(assetDirectory))
                    AssetDatabase.CreateFolder("Assets/Addressables_TempApplicationAuthoring", run);
                Directory.CreateDirectory(backupDirectory);
                string fileName = "Untitled-" + i + ".unity";
                string assetPath = assetDirectory + "/" + fileName;
                if (!EditorSceneManager.SaveScene(scene, assetPath))
                    throw new InvalidOperationException("Cannot preserve the untitled scene before authoring: " + assetPath);
                string backupPath = Path.Combine(backupDirectory, fileName);
                File.Copy(assetPath, backupPath);
                if (File.Exists(assetPath + ".meta")) File.Copy(assetPath + ".meta", backupPath + ".meta");
                Debug.Log("Preserved untitled scene: " + backupPath);
            }
        }

        private static T Load<T>(string path) where T : Object
            => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing authored asset: " + path);

        private static T Instantiate<T>(T prefab, Transform parent) where T : Component
            => ((GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent)).GetComponent<T>();

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? serialized.FindProperty("<" + field + ">k__BackingField");
            if (property == null) throw new InvalidOperationException("Missing authored reference: " + target.GetType().Name + "." + field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSceneReference(Object target, string field, string scenePath)
        {
            var serialized = new SerializedObject(target);
            var reference = serialized.FindProperty(field);
            var guid = reference.FindPropertyRelative("m_AssetGUID");
            if (!string.IsNullOrEmpty(guid.stringValue)) return;
            guid.stringValue = AssetDatabase.AssetPathToGUID(scenePath);
            if (string.IsNullOrEmpty(guid.stringValue)) throw new InvalidOperationException("The scene has not been saved: " + scenePath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateScene(string path, Action author)
        {
            if (File.Exists(path)) return;
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            author();
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Cannot save authored scene: " + path);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static RenderPipelineAsset EnsurePipeline()
        {
            const string rendererPath = "Assets/Settings/SpiritPrototypeRenderer.asset";
            const string pipelinePath = "Assets/Settings/SpiritPrototypePipeline.asset";
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (existing != null) return existing;
            if (File.Exists(pipelinePath)) throw new InvalidOperationException("The pipeline asset has a different type: " + pipelinePath);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                if (File.Exists(rendererPath)) throw new InvalidOperationException("The renderer asset has a different type: " + rendererPath);
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 4;
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
            return pipeline;
        }

        private static SpiritStartupView CreateStartup()
        {
            string path = UiFolder + "SpiritStartupView.prefab";
            if (File.Exists(path)) return Load<SpiritStartupView>(path);
            var root = SpiritPrefabUi.Full("ApplicationStartup", null);
            root.gameObject.AddComponent<SpiritUiContext>().Configure(SpiritPrefabUi.Theme);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540, 960);
            scaler.matchWidthOrHeight = 1;
            root.gameObject.AddComponent<GraphicRaycaster>();
            root.gameObject.AddComponent<Image>().color = new Color32(12, 18, 16, 255);
            var blocker = root.gameObject.AddComponent<CanvasGroup>();
            blocker.interactable = blocker.blocksRaycasts = true;
            var view = root.gameObject.AddComponent<SpiritStartupView>();
            Set(view, "Root", root);
            Set(view, "Status", SpiritPrefabUi.Label("StartupStatus", root, "Starting...", 22,
                new Vector2(.10f, .35f), new Vector2(.90f, .65f), TextAnchor.MiddleCenter));
            try { return PrefabUtility.SaveAsPrefabAsset(root.gameObject, path).GetComponent<SpiritStartupView>(); }
            finally { Object.DestroyImmediate(root.gameObject); }
        }

        private static SpiritFrontendView CreateFrontend()
        {
            string path = UiFolder + "SpiritFrontendScreen.prefab";
            if (File.Exists(path)) return Load<SpiritFrontendView>(path);
            var root = SpiritPrefabUi.Full("Frontend", null);
            root.gameObject.AddComponent<SpiritUiContext>().Configure(SpiritPrefabUi.Theme);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540, 960);
            scaler.matchWidthOrHeight = 1;
            root.gameObject.AddComponent<GraphicRaycaster>();
            SpiritPrefabUi.Panel("Letterbox", root, Vector2.zero, Vector2.one, new Color32(12, 18, 16, 255));
            var safeArea = SpiritPrefabUi.Full("SafeArea", root);
            var portrait = SpiritPrefabUi.Rect("Portrait", safeArea, new Vector2(.5f, 0), new Vector2(.5f, 1));
            portrait.sizeDelta = new Vector2(540, 0);
            SpiritPrefabUi.Panel("Background", portrait, Vector2.zero, Vector2.one, SpiritPrefabUi.Theme.panelColor);
            var view = root.gameObject.AddComponent<SpiritFrontendView>();
            Set(view, "Portrait", portrait);
            Set(view, "MainMenu", Instantiate(Load<SpiritMainMenu>(UiFolder + "SpiritMainMenu.prefab"), portrait));
            var board = Instantiate(Load<SpiritInvestigationBoard>(UiFolder + "SpiritInvestigationBoard.prefab"), portrait);
            Set(view, "Board", board);
            board.Root.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(board.gameObject);
            var collection = Instantiate(Load<SpiritCollectionView>(UiFolder + "SpiritCollectionView.prefab"), portrait);
            Set(view, "Collection", collection);
            collection.Root.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(collection.gameObject);
            var fit = root.gameObject.AddComponent<SpiritPortraitSafeArea>();
            Set(fit, "safeArea", safeArea);
            Set(fit, "portrait", portrait);
            try { return PrefabUtility.SaveAsPrefabAsset(root.gameObject, path).GetComponent<SpiritFrontendView>(); }
            finally { Object.DestroyImmediate(root.gameObject); }
        }

        private static void ApplicationScene(SpiritStartupView startupPrefab, RenderPipelineAsset pipeline)
        {
            var root = new GameObject("Application");
            var controller = root.AddComponent<SpiritEncounterController>();
            var campaign = root.AddComponent<SpiritCampaignController>();
            var router = root.AddComponent<SpiritApplicationPresentation>();
            var bootstrap = root.AddComponent<DiceBootstrap>();
            var environment = root.AddComponent<SpiritPresentationEnvironment>();
            Set(environment, "pipeline", pipeline);
            var startup = Instantiate(startupPrefab, root.transform);
            Set(bootstrap, "controller", controller);
            Set(bootstrap, "campaign", campaign);
            Set(bootstrap, "presentation", router);
            Set(bootstrap, "StartupView", startup);
            Set(router, "controller", controller);
            Set(router, "campaign", campaign);
            Set(router, "startup", startup);
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(12, 18, 16, 255);
            camera.cullingMask = ~(1 << SpiritDiceTray.RenderLayer);
            camera.allowHDR = false;
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            EventSystem(root.transform);
        }

        private static void EncounterScene(EncounterTablePresentationAsset table)
        {
            if (table.TrayPresentation?.RigPrefab == null || table.HostPresentation?.PortraitPrefab == null)
                throw new InvalidOperationException("The table requires authored rig and host prefabs: " + table.name);
            var root = new GameObject(table.name + " Presentation");
            var view = Instantiate(Load<SpiritEncounterView>(UiFolder + "SpiritEncounterScreen.prefab"), root.transform);
            var rig = Instantiate(table.TrayPresentation.RigPrefab, root.transform);
            rig.transform.localPosition = new Vector3(14000, 0, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig.transform);
            var host = Instantiate(table.HostPresentation.PortraitPrefab, view.Portrait);
            host.transform.SetSiblingIndex(view.TableImage.transform.GetSiblingIndex() + 1);
            PrefabUtility.RecordPrefabInstancePropertyModifications(host.transform);
            Set(view, "tablePresentation", table);
            Set(view, "trayPresentation", table.TrayPresentation);
            Set(view, "TrayScene", rig);
            Set(view, "DiceTray", rig.DiceTray);
            Set(view, "hostPortrait", host);
            Set(view, "roomTexture", table.RoomTexture);
            var texture = table.SceneLighting != null ? table.SceneLighting.BaseTexture : table.RoomTexture;
            view.RoomImage.texture = view.TableImage.texture = texture;
            view.RoomImage.color = view.TableImage.color = texture != null ? Color.white : Color.clear;
            view.RoomImage.uvRect = new Rect(0, table.RoomTableEdge, 1, 1 - table.RoomTableEdge);
            view.TableImage.uvRect = new Rect(0, 0, 1, table.RoomTableEdge);
            view.RoomImage.rectTransform.anchorMin = new Vector2(0, table.RoomSplit);
            view.TableImage.rectTransform.anchorMax = new Vector2(1, table.RoomSplit);
            Rect placement = table.TrayPresentation.GetCompositionRect(540f / 960);
            var playingPanel = view.GameplayHud.PlayingPanel;
            Vector2 panelMin = playingPanel.anchorMin;
            Vector2 panelSize = playingPanel.anchorMax - panelMin;
            view.GameplayHud.DiceTrayFrame.anchorMin = new Vector2((placement.xMin - panelMin.x) / panelSize.x,
                (placement.yMin - panelMin.y) / panelSize.y);
            view.GameplayHud.DiceTrayFrame.anchorMax = new Vector2((placement.xMax - panelMin.x) / panelSize.x,
                (placement.yMax - panelMin.y) / panelSize.y);
            view.GameplayHud.DiceTrayFrame.offsetMin = view.GameplayHud.DiceTrayFrame.offsetMax = Vector2.zero;
            PrefabUtility.RecordPrefabInstancePropertyModifications(view.RoomImage);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view.TableImage);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view.RoomImage.rectTransform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view.TableImage.rectTransform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view.GameplayHud.DiceTrayFrame);
        }

        private static void MigrateDiagnostic(MechanicalCatalogAsset catalog)
        {
            if (!File.Exists(DiagnosticScenePath))
            {
                CreateScene(DiagnosticScenePath, () =>
                {
                    var root = new GameObject("Diagnostic Encounter");
                    var controller = root.AddComponent<DiagnosticEncounterController>();
                    Set(controller, "catalogAsset", catalog);
                    Set(controller, "view", Instantiate(Load<DiagnosticEncounterView>(SpiritDiagnosticPrefabAuthoring.PrefabPath), root.transform));
                    new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                    EventSystem(null);
                });
                return;
            }
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(DiagnosticScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty)
                throw new InvalidOperationException("Save the diagnostic scene before migrating its authored UI.");
            if (opened) scene = EditorSceneManager.OpenScene(DiagnosticScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var controllerInScene = Find<DiagnosticEncounterController>(scene)
                ?? throw new InvalidOperationException("The diagnostic scene has no encounter controller.");
            var serialized = new SerializedObject(controllerInScene);
            var current = serialized.FindProperty("view").objectReferenceValue as DiagnosticEncounterView;
            if (current != null && new SerializedObject(current).FindProperty("seedInput").objectReferenceValue != null)
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened) EditorSceneManager.CloseScene(scene, true);
                return;
            }
            var legacy = controllerInScene.GetComponent<DiagnosticEncounterView>();
            var view = Instantiate(Load<DiagnosticEncounterView>(SpiritDiagnosticPrefabAuthoring.PrefabPath), controllerInScene.transform);
            Set(controllerInScene, "view", view);
            if (legacy != null) Object.DestroyImmediate(legacy);
            if (Find<UnityEngine.EventSystems.EventSystem>(scene) == null) EventSystem(null);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Cannot save migrated diagnostic scene.");
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var value = root.GetComponentInChildren<T>(true);
                if (value != null) return value;
            }
            return null;
        }

        private static void EventSystem(Transform parent)
        {
            var root = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(InputSystemUIInputModule));
            root.transform.SetParent(parent, false);
            var module = root.GetComponent<InputSystemUIInputModule>();
            var actions = Load<InputActionAsset>(InputActionsPath);
            var references = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath).OfType<InputActionReference>().ToArray();
            module.actionsAsset = actions;
            module.point = ActionReference(references, actions, "Point");
            module.leftClick = ActionReference(references, actions, "Click");
            module.middleClick = ActionReference(references, actions, "MiddleClick");
            module.rightClick = ActionReference(references, actions, "RightClick");
            module.scrollWheel = ActionReference(references, actions, "ScrollWheel");
            module.move = ActionReference(references, actions, "Navigate");
            module.submit = ActionReference(references, actions, "Submit");
            module.cancel = ActionReference(references, actions, "Cancel");
            module.trackedDevicePosition = ActionReference(references, actions, "TrackedDevicePosition");
            module.trackedDeviceOrientation = ActionReference(references, actions, "TrackedDeviceOrientation");
        }

        private static InputActionReference ActionReference(InputActionReference[] references, InputActionAsset actions, string name)
        {
            var action = actions.FindAction("UI/" + name, true);
            return references.FirstOrDefault(reference => reference.action?.id == action.id)
                ?? throw new InvalidOperationException("The input action has no persistent reference: UI/" + name);
        }

        private static void ConfigureAddressables(DiceApplicationConfig configuration)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup("Dice Presentation Scenes");
            if (group == null)
            {
                group = settings.CreateGroup("Dice Presentation Scenes", false, false, true, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                schema.IncludeInBuild = true;
                schema.IncludeAddressInCatalog = schema.IncludeGUIDInCatalog = true;
                schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                EditorUtility.SetDirty(schema);
            }
            Register(settings, group, FrontendScenePath, "dice.scene.frontend");
            Register(settings, group, EncounterScenePaths[0], "dice.scene.fox");
            Register(settings, group, EncounterScenePaths[1], "dice.scene.watchtower");
            Register(settings, group, EncounterScenePaths[2], "dice.scene.moth");
            string configurationGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(configuration));
            if (settings.FindAssetEntry(configurationGuid) == null)
                settings.CreateOrMoveEntry(configurationGuid, settings.DefaultGroup).address = DiceBootstrap.ConfigurationAddress;
            EditorUtility.SetDirty(settings);
        }

        private static void Register(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string address)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (settings.FindAssetEntry(guid) != null) return;
            settings.CreateOrMoveEntry(guid, group).address = address;
            EditorUtility.SetDirty(group);
        }

        private static void ConfigureBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.path != ApplicationScenePath &&
                scene.path != FrontendScenePath && !EncounterScenePaths.Contains(scene.path)).ToList();
            foreach (var scene in scenes)
                if (scene.path == SpiritPrototypeSetup.ScenePath) scene.enabled = false;
            scenes.Insert(0, new EditorBuildSettingsScene(ApplicationScenePath, true));
            if (!scenes.Any(scene => scene.path == DiagnosticScenePath))
                scenes.Add(new EditorBuildSettingsScene(DiagnosticScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
