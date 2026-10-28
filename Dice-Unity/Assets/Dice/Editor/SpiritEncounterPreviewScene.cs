using System;
using System.Linq;
using System.Text;
using Dice.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dice.Editor
{
    /** Isolated copy of the same authored presentation scene used by gameplay. */
    internal sealed class SpiritEncounterPreviewScene : IDisposable
    {
        internal Scene Scene { get; private set; }
        internal GameObject Root { get; private set; }
        internal SpiritEncounterView View { get; private set; }
        internal Camera Camera { get; private set; }
        internal RenderTexture Output { get; private set; }
        // Pixel height of the preview portrait, the encounter's reference height.
        private const int PreviewHeight = 960;

        internal static string PathFor(EncounterTablePresentationAsset table)
        {
            string path = table?.Scene != null ? AssetDatabase.GUIDToAssetPath(table.Scene.AssetGUID) : null;
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("The table requires an authored encounter scene.");
            return path;
        }

        private static SpiritEncounterView OpenSource(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || scene.path != path || EditorSceneManager.IsPreviewScene(scene)) continue;
                return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpiritEncounterView>(true)).Single();
            }
            return null;
        }

        internal static string SourceSettings(EncounterTablePresentationAsset table)
        {
            string path = PathFor(table);
            var settings = new StringBuilder(AssetDatabase.GetAssetDependencyHash(path).ToString());
            var source = OpenSource(path);
            if (source != null)
                foreach (var component in source.transform.root.GetComponentsInChildren<Component>(true))
                    if (component != null) settings.Append(EditorJsonUtility.ToJson(component));
            return Hash128.Compute(settings.ToString()).ToString();
        }

        internal static SpiritEncounterPreviewScene Open(EncounterTablePresentationAsset table, bool narrow)
        {
            var result = new SpiritEncounterPreviewScene();
            try
            {
                string path = PathFor(table);
                var source = OpenSource(path);
                if (source != null)
                {
                    result.Scene = EditorSceneManager.NewPreviewScene();
                    result.Root = UnityEngine.Object.Instantiate(source.transform.root.gameObject);
                    SceneManager.MoveGameObjectToScene(result.Root, result.Scene);
                    result.View = result.Root.GetComponentInChildren<SpiritEncounterView>(true);
                }
                else
                {
                    result.Scene = EditorSceneManager.OpenPreviewScene(path);
                    result.View = result.Scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<SpiritEncounterView>(true)).Single();
                    result.Root = result.View.transform.root.gameObject;
                }
                var view = result.View;
                if (view.Canvas == null || view.Portrait == null || view.GameplayHud == null || view.TrayScene == null || view.HostPortrait == null)
                    throw new InvalidOperationException("The encounter preview requires the scene's authored screen, HUD, rig, and host references.");
                if (!view.TrayScene.transform.IsChildOf(result.Root.transform) || !view.HostPortrait.transform.IsChildOf(result.Root.transform))
                    throw new InvalidOperationException("The encounter rig and host must belong to its presentation root.");
                view.enabled = false;
                foreach (var audio in result.Root.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
                var capture = new GameObject("Encounter Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(capture, result.Scene);
                result.Camera = capture.AddComponent<Camera>();
                result.Camera.scene = result.Scene;
                result.Camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(result.Scene);
                result.Camera.enabled = false;
                result.Camera.transform.position = new Vector3(0, 0, -10);
                result.Camera.orthographic = true;
                result.Camera.orthographicSize = 480;
                result.Camera.nearClipPlane = .1f;
                result.Camera.farClipPlane = 30;
                result.Camera.cullingMask = ~(1 << SpiritDiceTray.RenderLayer);
                result.Camera.clearFlags = CameraClearFlags.SolidColor;
                result.Camera.backgroundColor = new Color(.04f, .055f, .05f, 1);
                result.Camera.allowHDR = result.Camera.allowMSAA = false;
                result.Output = new RenderTexture(narrow ? 443 : 540, PreviewHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "Encounter Scene Preview", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
                };
                result.Output.Create();
                result.Camera.targetTexture = result.Output;
                result.Camera.aspect = (float)result.Output.width / PreviewHeight;
                view.PreparePreview(result.Camera, new Vector2(result.Output.width, PreviewHeight), table.HostPresentation);
                // The tray renders on request, into the preview scene only.
                view.TrayScene.RenderCamera.scene = result.Scene;
                view.TrayScene.RenderCamera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(result.Scene);
                view.TrayScene.RenderCamera.enabled = false;
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        internal void Resize(bool narrow)
        {
            int width = narrow ? 443 : 540;
            if (Output.width != width)
            {
                Camera.targetTexture = null;
                Output.Release();
                Output.width = width;
                Output.Create();
                Camera.targetTexture = Output;
            }
            Camera.aspect = (float)width / PreviewHeight;
            View.SizePreview(new Vector2(width, PreviewHeight));
        }

        internal void ShowHud(bool visible) => View.GameplayHud.ShowInPreview(visible);

        public void Dispose()
        {
            if (View != null && View.TrayScene != null)
            {
                View.TrayScene.DiceTray?.ReleasePresentation();
                View.TrayScene.HistoryRenderer?.ReleasePresentation();
                View.TrayScene.ReleasePresentation();
            }
            if (Scene.IsValid()) EditorSceneManager.ClosePreviewScene(Scene);
            if (Output != null) { Output.Release(); UnityEngine.Object.DestroyImmediate(Output); }
            Output = null;
            Scene = default;
            Root = null;
            View = null;
            Camera = null;
        }
    }
}
