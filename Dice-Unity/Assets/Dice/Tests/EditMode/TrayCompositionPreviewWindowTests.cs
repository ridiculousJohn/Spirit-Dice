using System.Collections;
using System.Linq;
using Dice.Editor;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Dice.Tests
{
    public sealed class TrayCompositionPreviewWindowTests
    {
        private TrayCompositionPreviewWindow window;
        private RenderPipelineAsset previousPipeline;
        private Scene original;
        private bool originalDirty;

        [SetUp]
        public void SetUp()
        {
            original = SceneManager.GetActiveScene();
            originalDirty = original.isDirty;
            previousPipeline = QualitySettings.renderPipeline;
            window = ScriptableObject.CreateInstance<TrayCompositionPreviewWindow>();
        }

        [TearDown]
        public void TearDown()
        {
            if (window != null) Object.DestroyImmediate(window);
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(previousPipeline));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(original));
            Assert.That(original.isDirty, Is.EqualTo(originalDirty));
        }

        /** The authored lit table, loaded from its asset path. */
        private static EncounterTablePresentationAsset LoadTable(string path)
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(path);
            Assert.That(table, Is.Not.Null, path);
            return table;
        }

        [Test]
        public void AuthoredTablePreviewRendersSplitArtworkAndSharedRuntimeBaysAtBothWidths()
        {
            string path = TestContent.LitTablePath();
            foreach (bool narrow in new[] { false, true })
            {
                var table = LoadTable(path);
                window.table = table; window.narrow = narrow; window.historyCount = 6;
                window.EnsureStage();
                var view = window.view;
                var scene = view.TrayScene;
                var dice = scene.DiceTray;
                var history = scene.HistoryRenderer;
                Assert.That(view.enabled, Is.False, "A preview does not bind a gameplay session.");
                Assert.That(dice.RenderCamera, Is.SameAs(scene.RenderCamera));
                Assert.That(history.RenderCamera, Is.SameAs(scene.RenderCamera));
                Assert.That(dice.Texture, Is.SameAs(history.Texture));
                Assert.That(history.Count, Is.EqualTo(6));
                Assert.That(view.GameplayHud.DiceTrayImage.texture, Is.SameAs(scene.Texture));
                var output = window.output;
                Assert.That(output.width, Is.EqualTo(SpiritEncounterPreviewScene.Width(narrow)));
                Assert.That(output.height, Is.EqualTo(SpiritEncounterPreviewScene.PreviewHeight));
                Assert.That(view.Portrait.rect.size, Is.EqualTo(new Vector2(output.width, output.height)));
                var source = EditorSceneManager.OpenPreviewScene(AssetDatabase.GUIDToAssetPath(table.Scene.AssetGUID));
                try
                {
                    var authored = source.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpiritEncounterView>(true)).Single();
                    AssertRect(view.GameplayHud.DiceTrayFrame, authored.GameplayHud.DiceTrayFrame);
                    Assert.That(scene.RenderCamera.transform.localPosition, Is.EqualTo(authored.TrayScene.RenderCamera.transform.localPosition));
                    Assert.That(scene.RenderCamera.transform.localRotation, Is.EqualTo(authored.TrayScene.RenderCamera.transform.localRotation));
                    Assert.That(view.RoomImage.uvRect, Is.EqualTo(authored.RoomImage.uvRect));
                    Assert.That(view.TableImage.uvRect, Is.EqualTo(authored.TableImage.uvRect));
                }
                finally { EditorSceneManager.ClosePreviewScene(source); }
                RenderAndCheckPixels(scene, output);
                // Each width builds its own stage, as a freshly opened preview would.
                window.ReleaseStage();
            }
        }

        [Test]
        public void GameplayOverlayUsesLiveControlsAndTogglesWithoutRebuildingTray()
        {
            string path = TestContent.LitTablePath();
            foreach (bool narrow in new[] { false, true })
            {
                window.table = LoadTable(path);
                window.narrow = narrow;
                window.EnsureStage();
                var hud = window.view.GameplayHud;
                var scene = window.view.TrayScene;
                var camera = scene.RenderCamera;
                Assert.That(hud.GetComponentsInChildren<UnityEngine.UI.Text>(true).Single(label => label.name == "BaseScoreLabel").text,
                    Is.EqualTo("0"));
                Assert.That(hud.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                float shownAlpha = hud.GetComponent<CanvasGroup>().alpha;
                window.showGameplayUi = false;
                window.EnsureStage();
                Assert.That(hud.GetComponent<CanvasGroup>().alpha, Is.Zero);
                Assert.That(hud.DiceTrayFrame.GetComponent<CanvasGroup>().ignoreParentGroups, Is.True);
                Assert.That(hud.DiceTrayImage.isActiveAndEnabled, Is.True);
                window.showGameplayUi = true;
                window.EnsureStage();
                Assert.That(window.view.GameplayHud, Is.SameAs(hud));
                Assert.That(hud.GetComponent<CanvasGroup>().alpha, Is.EqualTo(shownAlpha), "Showing the overlay restores the HUD's authored opacity.");
                Assert.That(scene.RenderCamera, Is.SameAs(camera));
                window.RenderPreview();
                // Each width builds its own stage, as a freshly opened preview would.
                window.ReleaseStage();
            }
        }

        [Test]
        public void PreviewControlsRefreshExistingObjectsAndSampleHostTimeDeterministically()
        {
            window.table = LoadTable(TestContent.LitTablePath());
            window.EnsureStage();
            var view = window.view;
            var scene = view.TrayScene;
            var host = view.HostPortrait;
            var output = window.output;
            window.narrow = true; window.historyCount = 12;
            window.historyScroll = 1f; window.sampleTime = 2.5f;
            int[] faces = window.faces;
            for (int i = 0; i < faces.Length; ++i) faces[i] = 6;
            window.EnsureStage(); window.RenderPreview();
            Assert.That(window.view, Is.SameAs(view));
            Assert.That(view.TrayScene, Is.SameAs(scene));
            Assert.That(view.HostPortrait, Is.SameAs(host));
            Assert.That(window.output, Is.SameAs(output));
            Assert.That(output.width, Is.EqualTo(SpiritEncounterPreviewScene.NarrowPreviewWidth));
            Assert.That(scene.HistoryRenderer.Count, Is.EqualTo(12));
            var transforms = scene.DiceTray.Dice;
            for (int i = 0; i < faces.Length; ++i)
            {
                Vector3 resultNormal = transforms[i].TransformDirection(Vector3.back);
                Assert.That(Vector3.Dot(resultNormal, scene.Geometry.DiceSlots[i].up), Is.GreaterThan(.999f), "Die " + i);
                Vector3 towardCamera = scene.RenderCamera.orthographic ? -scene.RenderCamera.transform.forward
                    : (scene.RenderCamera.transform.position - transforms[i].position).normalized;
                // A shallow authored camera can make a side face more prominent than the upward result.
                Assert.That(Vector3.Dot(resultNormal, towardCamera), Is.GreaterThan(0), "Die " + i + " result visibility");
            }
            Assert.That(host.EffectAge, Is.EqualTo(2.5));
            window.RenderPreview();
            Assert.That(host.EffectAge, Is.EqualTo(2.5));
        }

        [UnityTest]
        public IEnumerator UndoAndRedoRestoreScenePlacementCameraAndSurfaceOverrides()
        {
            string tablePath = TestContent.LitTablePath();
            var table = LoadTable(tablePath);
            string path = AssetDatabase.GUIDToAssetPath(table.Scene.AssetGUID);
            Assert.That(SceneManager.GetSceneByPath(path).isLoaded, Is.False, "This fixture opens its own source scene.");
            var source = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            // Opening a scene can unload assets held only by test locals, so the table is resolved again.
            table = LoadTable(tablePath);
            var authored = source.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpiritEncounterView>(true)).Single();
            var frame = authored.GameplayHud.DiceTrayFrame;
            var camera = authored.TrayScene.RenderCamera;
            var geometry = authored.TrayScene.Geometry.transform;
            Vector2 originalAnchor = frame.anchorMin;
            Quaternion originalRotation = camera.transform.localRotation;
            Color originalBackground = camera.backgroundColor;
            CameraClearFlags originalClear = camera.clearFlags;
            Vector3 originalPosition = geometry.localPosition;
            try
            {
                window.table = table;
                window.EnsureStage();
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Edit authored encounter presentation");
                Undo.RecordObjects(new Object[] { frame, camera, camera.transform, geometry }, "Edit authored encounter presentation");
                frame.anchorMin += new Vector2(.03f, .04f);
                camera.transform.localRotation = Quaternion.Euler(37, 14, 9);
                camera.backgroundColor = new Color(.12f, .23f, .34f, .45f);
                camera.clearFlags = CameraClearFlags.Depth;
                geometry.localPosition += new Vector3(.2f, .1f, -.3f);
                Undo.FlushUndoRecordObjects();
                Undo.IncrementCurrentGroup();
                window.EnsureStage();
                var edited = window.view;
                AssertRect(edited.GameplayHud.DiceTrayFrame, frame);
                Assert.That(edited.TrayScene.RenderCamera.backgroundColor, Is.EqualTo(camera.backgroundColor));
                Assert.That(edited.TrayScene.RenderCamera.clearFlags, Is.EqualTo(camera.clearFlags));
                Assert.That(edited.TrayScene.RenderCamera.transform.localRotation, Is.EqualTo(camera.transform.localRotation));
                Assert.That(edited.TrayScene.Geometry.transform.localPosition, Is.EqualTo(geometry.localPosition));
                Assert.That(edited.TrayScene.Geometry, Is.Not.SameAs(authored.TrayScene.Geometry));
                Undo.PerformUndo();
                yield return null;
                window.EnsureStage();
                var undone = window.view;
                Assert.That(frame.anchorMin, Is.EqualTo(originalAnchor));
                Assert.That(undone.GameplayHud.DiceTrayFrame.anchorMin, Is.EqualTo(originalAnchor));
                Assert.That(undone.TrayScene.RenderCamera.transform.localRotation, Is.EqualTo(originalRotation));
                Assert.That(undone.TrayScene.RenderCamera.backgroundColor, Is.EqualTo(originalBackground));
                Assert.That(undone.TrayScene.RenderCamera.clearFlags, Is.EqualTo(originalClear));
                Assert.That(undone.TrayScene.Geometry.transform.localPosition, Is.EqualTo(originalPosition));
                Undo.PerformRedo();
                yield return null;
                window.EnsureStage();
                var redone = window.view;
                AssertRect(redone.GameplayHud.DiceTrayFrame, frame);
                Assert.That(redone.TrayScene.RenderCamera.backgroundColor, Is.EqualTo(camera.backgroundColor));
                Assert.That(redone.TrayScene.Geometry.transform.localPosition, Is.EqualTo(geometry.localPosition));
            }
            finally
            {
                window.ReleaseStage();
                Undo.ClearUndo(frame); Undo.ClearUndo(camera); Undo.ClearUndo(camera.transform); Undo.ClearUndo(geometry);
                EditorSceneManager.CloseScene(source, true);
                SceneManager.SetActiveScene(original);
            }
        }

        [Test]
        public void RebuildingAndClosingReleaseOwnedStageWhilePreservingAuthoredAssetsAndPipeline()
        {
            window.table = LoadTable(TestContent.LitTablePath());
            window.EnsureStage();
            var scene = window.view.TrayScene;
            var camera = scene.RenderCamera;
            var texture = scene.Texture;
            var output = window.output;
            RenderPipelineAsset acquired = QualitySettings.renderPipeline;
            window.rebuild = true; window.EnsureStage();
            Assert.That(scene == null && camera == null && texture == null && output == null, Is.True);
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(acquired));
            Assert.That(window.view.TrayScene.Geometry, Is.Not.Null);
            Object.DestroyImmediate(window); window = null;
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(previousPipeline));
        }

        private void RenderAndCheckPixels(SpiritTrayScene scene, RenderTexture output)
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            RenderTexture previous = RenderTexture.active;
            bool srgb = GL.sRGBWrite;
            var sentinel = new RenderTexture(8, 8, 0);
            var readback = new Texture2D(scene.Texture.width, scene.Texture.height, TextureFormat.RGBA32, false);
            try
            {
                sentinel.Create(); RenderTexture.active = sentinel; GL.sRGBWrite = !srgb;
                window.RenderPreview();
                Assert.That(RenderTexture.active, Is.SameAs(sentinel));
                Assert.That(GL.sRGBWrite, Is.EqualTo(!srgb));
                RenderTexture.active = scene.Texture;
                readback.ReadPixels(new Rect(0, 0, scene.Texture.width, scene.Texture.height), 0, 0); readback.Apply();
                Assert.That(readback.GetPixels32().Count(pixel => pixel.a > 200),
                    Is.GreaterThan(scene.Texture.width * scene.Texture.height / 100));
                Assert.That(output.IsCreated(), Is.True);
            }
            finally
            {
                RenderTexture.active = previous; GL.sRGBWrite = srgb;
                sentinel.Release(); Object.DestroyImmediate(sentinel); Object.DestroyImmediate(readback);
            }
        }

        private static void AssertRect(RectTransform actual, RectTransform expected)
        {
            Assert.That(actual.anchorMin, Is.EqualTo(expected.anchorMin));
            Assert.That(actual.anchorMax, Is.EqualTo(expected.anchorMax));
            Assert.That(actual.offsetMin, Is.EqualTo(expected.offsetMin));
            Assert.That(actual.offsetMax, Is.EqualTo(expected.offsetMax));
            Assert.That(actual.pivot, Is.EqualTo(expected.pivot));
            Assert.That(actual.localScale, Is.EqualTo(expected.localScale));
        }
    }
}
