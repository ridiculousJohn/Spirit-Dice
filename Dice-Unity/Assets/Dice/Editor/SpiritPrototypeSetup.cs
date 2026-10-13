using System.Linq;
using Dice.App;
using Dice.Presentation;
using Dice.UnityContent;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Dice.Editor
{
    public static class SpiritPrototypeSetup
    {
        public const string ScenePath = "Assets/Scenes/SpiritHousePrototype.unity";
        private const string ArtPath = "Assets/Dice/Art/SpiritHousePrototype/";

        [MenuItem("Dice/Open Spirit House Prototype")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) Create();
            else EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Create()
        {
            AssetDatabase.Refresh();
            foreach (string name in new[] { "FoxBodySeated", "FoxHeadRest", "FoxHeadTurn", "LanternRoomSeated" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath + name + ".png");
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = !name.StartsWith("LanternRoom", System.StringComparison.Ordinal);
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }

            const string rendererPath = "Assets/Settings/SpiritPrototypeRenderer.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            const string pipelinePath = "Assets/Settings/SpiritPrototypePipeline.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.supportsHDR = false;
                pipeline.msaaSampleCount = 4;
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.04f, .065f, .05f);
                camera.cullingMask = ~(1 << SpiritDiceTray.RenderLayer);
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
                var root = new GameObject("Spirit Encounter");
                Assign(root.AddComponent<SpiritPresentationEnvironment>(), "pipeline", pipeline);
                var trayPresentation = AssetDatabase.LoadAssetAtPath<SpiritTrayPresentationAsset>(
                    "Assets/Dice/Presentation/Shared/SpiritTrayPresentation.asset");
                root.AddComponent<SpiritDiceTray>().Configure(trayPresentation);
                var view = root.AddComponent<SpiritEncounterView>();
                Assign(view, "uiTheme", AssetDatabase.LoadAssetAtPath<SpiritUiTheme>(
                    "Assets/Dice/Presentation/Shared/SpiritHouseTheme.asset"));
                Assign(view, "trayPresentation", trayPresentation);
                Assign(view, "tablePresentation", AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(
                    "Assets/Dice/Content/Presentation/SpiritHouse/LastLanternTable.asset"));
                var controller = root.AddComponent<SpiritEncounterController>();
                Assign(controller, "catalogAsset",
                    AssetDatabase.LoadAssetAtPath<MechanicalCatalogAsset>("Assets/Dice/Content/Mechanical/Standard.asset"));
                Assign(controller, "journeyCatalog", AssetDatabase.LoadAssetAtPath<EncounterJourneyCatalogAsset>(
                    "Assets/Dice/Content/Presentation/SpiritHouse/SpiritJourneyCatalog.asset"));
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            if (!EditorBuildSettings.scenes.Any(scene => scene.path == ScenePath))
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                    .Concat(EditorBuildSettings.scenes).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Spirit house prototype ready: " + ScenePath);
        }

        private static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath + name + ".png");

        private static void Assign(Object target, string name, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
