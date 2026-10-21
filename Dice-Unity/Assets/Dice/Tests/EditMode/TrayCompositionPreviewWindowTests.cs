using System.Collections;
using System.Linq;
using System.Reflection;
using Dice.Editor;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

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

        [TestCase("SpiritHouse/LastLanternTable.asset", false)]
        [TestCase("SpiritHouse/LastLanternTable.asset", true)]
        [TestCase("Watchtower/WatchtowerTable.asset", false)]
        [TestCase("Watchtower/WatchtowerTable.asset", true)]
        [TestCase("MothCrossroads/MothCrossroadsTable.asset", false)]
        [TestCase("MothCrossroads/MothCrossroadsTable.asset", true)]
        [TestCase("HauntedMerchant/MerchantUnquietCartTable.asset", false)]
        [TestCase("HauntedMerchant/MerchantUnquietCartTable.asset", true)]
        public void AuthoredTablePreviewRendersSplitArtworkAndSharedRuntimeBaysAtBothWidths(string path, bool narrow)
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>("Assets/Dice/Content/Presentation/" + path);
            Set(window, "table", table); Set(window, "narrow", narrow); Set(window, "historyCount", 6);
            Invoke(window, "EnsureStage");
            var view = Field<SpiritEncounterView>(window, "view");
            var scene = Field<SpiritTrayScene>(window, "trayScene");
            var dice = Field<SpiritDiceTray>(window, "dice");
            var history = Field<SpiritModifierHistoryRenderer>(window, "history");
            Assert.That(scene, Is.SameAs(view.TrayScene));
            Assert.That(Field<SpiritGameplayHud>(window, "gameplayUi"), Is.SameAs(view.GameplayHud));
            Assert.That(Field<SpiritHostPortrait>(window, "host"), Is.SameAs(view.HostPortrait));
            Assert.That(view.enabled, Is.False, "A preview does not bind a gameplay session.");
            Assert.That(dice.RenderCamera, Is.SameAs(scene.RenderCamera));
            Assert.That(history.RenderCamera, Is.SameAs(scene.RenderCamera));
            Assert.That(dice.Texture, Is.SameAs(history.Texture));
            Assert.That(history.Count, Is.EqualTo(6));
            Assert.That(view.GameplayHud.DiceTrayImage.texture, Is.SameAs(scene.Texture));
            var output = Field<RenderTexture>(window, "output");
            Assert.That(output.width, Is.EqualTo(narrow ? 443 : 540));
            Assert.That(output.height, Is.EqualTo(960));
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
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GameplayOverlayUsesLiveControlsAndTogglesWithoutRebuildingTray(bool narrow)
        {
            Set(window, "narrow", narrow);
            Invoke(window, "EnsureStage");
            var hud = Field<SpiritGameplayHud>(window, "gameplayUi");
            var scene = Field<SpiritTrayScene>(window, "trayScene");
            var camera = scene.RenderCamera;
            Assert.That(hud.PlayButton.GetComponentInChildren<Text>().text, Is.EqualTo("Play Hand"));
            Assert.That(hud.RerollButton.GetComponentInChildren<Text>().text, Is.EqualTo("Reroll"));
            Assert.That(hud.BaseScoreLabel.text, Is.EqualTo("0"));
            Assert.That(hud.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Set(window, "showGameplayUi", false);
            Invoke(window, "EnsureStage");
            Assert.That(hud.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(hud.DiceTrayFrame.GetComponent<CanvasGroup>().ignoreParentGroups, Is.True);
            Assert.That(hud.DiceTrayImage.isActiveAndEnabled, Is.True);
            Set(window, "showGameplayUi", true);
            Invoke(window, "EnsureStage");
            Assert.That(Field<SpiritGameplayHud>(window, "gameplayUi"), Is.SameAs(hud));
            Assert.That(hud.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            Assert.That(scene.RenderCamera, Is.SameAs(camera));
            Invoke(window, "RenderPreview");
        }

        [Test]
        public void PreviewControlsRefreshExistingObjectsAndSampleHostTimeDeterministically()
        {
            Invoke(window, "EnsureStage");
            var stage = Field<GameObject>(window, "stage");
            var scene = Field<SpiritTrayScene>(window, "trayScene");
            var host = Field<SpiritHostPortrait>(window, "host");
            var output = Field<RenderTexture>(window, "output");
            Set(window, "narrow", true); Set(window, "historyCount", 12);
            Set(window, "historyScroll", 1f); Set(window, "sampleTime", 2.5f);
            int[] faces = Field<int[]>(window, "faces");
            for (int i = 0; i < faces.Length; ++i) faces[i] = 6;
            Invoke(window, "EnsureStage"); Invoke(window, "RenderPreview");
            Assert.That(Field<GameObject>(window, "stage"), Is.SameAs(stage));
            Assert.That(Field<SpiritTrayScene>(window, "trayScene"), Is.SameAs(scene));
            Assert.That(Field<SpiritHostPortrait>(window, "host"), Is.SameAs(host));
            Assert.That(Field<RenderTexture>(window, "output"), Is.SameAs(output));
            Assert.That(output.width, Is.EqualTo(443));
            Assert.That(Field<SpiritModifierHistoryRenderer>(window, "history").Count, Is.EqualTo(12));
            var transforms = Field<Transform[]>(scene.DiceTray, "dice");
            for (int i = 0; i < faces.Length; ++i)
            {
                Vector3 resultNormal = transforms[i].TransformDirection(Vector3.back);
                Assert.That(Vector3.Dot(resultNormal, scene.Geometry.DiceSlots[i].up), Is.GreaterThan(.999f), "Die " + i);
                Vector3 towardCamera = scene.RenderCamera.orthographic ? -scene.RenderCamera.transform.forward
                    : (scene.RenderCamera.transform.position - transforms[i].position).normalized;
                // A shallow authored camera can make a side face more prominent than the upward result.
                Assert.That(Vector3.Dot(resultNormal, towardCamera), Is.GreaterThan(0), "Die " + i + " result visibility");
            }
            Assert.That(Field<double>(host, "effectAge"), Is.EqualTo(2.5));
            Invoke(window, "RenderPreview");
            Assert.That(Field<double>(host, "effectAge"), Is.EqualTo(2.5));
        }

        [Test]
        public void LegacyPlacementCameraAndGeometrySeedsCannotOverrideTheAuthoredScene()
        {
            var table = Object.Instantiate(AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(
                "Assets/Dice/Content/Presentation/SpiritHouse/LastLanternTable.asset"));
            var presentation = Object.Instantiate(table.TrayPresentation);
            Set(table, "trayPresentation", presentation);
            Set(window, "table", table);
            try
            {
                Invoke(window, "EnsureStage");
                var view = Field<SpiritEncounterView>(window, "view");
                Vector2 anchor = view.GameplayHud.DiceTrayFrame.anchorMin;
                Quaternion rotation = view.TrayScene.RenderCamera.transform.localRotation;
                var geometry = view.TrayScene.Geometry;
                presentation.CompositionRect = new Rect(.6f, .7f, .2f, .1f);
                presentation.CameraEulerAngles = new Vector3(1, 2, 3);
                presentation.FieldOfView = 120;
                presentation.GeometryPrefab = null;
                Invoke(window, "EnsureStage");
                view = Field<SpiritEncounterView>(window, "view");
                Assert.That(view.GameplayHud.DiceTrayFrame.anchorMin, Is.EqualTo(anchor));
                Assert.That(view.TrayScene.RenderCamera.transform.localRotation, Is.EqualTo(rotation));
                Assert.That(view.TrayScene.Geometry, Is.SameAs(geometry));
            }
            finally { Invoke(window, "ReleaseStage"); Object.DestroyImmediate(presentation); Object.DestroyImmediate(table); }
        }

        [UnityTest]
        public IEnumerator UndoAndRedoRestoreScenePlacementCameraAndSurfaceOverrides()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(
                "Assets/Dice/Content/Presentation/SpiritHouse/LastLanternTable.asset");
            string path = AssetDatabase.GUIDToAssetPath(table.Scene.AssetGUID);
            Assert.That(SceneManager.GetSceneByPath(path).isLoaded, Is.False, "This fixture opens its own source scene.");
            var source = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
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
                Set(window, "table", table);
                Invoke(window, "EnsureStage");
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
                Invoke(window, "EnsureStage");
                var edited = Field<SpiritEncounterView>(window, "view");
                AssertRect(edited.GameplayHud.DiceTrayFrame, frame);
                Assert.That(edited.TrayScene.RenderCamera.backgroundColor, Is.EqualTo(camera.backgroundColor));
                Assert.That(edited.TrayScene.RenderCamera.clearFlags, Is.EqualTo(camera.clearFlags));
                Assert.That(edited.TrayScene.RenderCamera.transform.localRotation, Is.EqualTo(camera.transform.localRotation));
                Assert.That(edited.TrayScene.Geometry.transform.localPosition, Is.EqualTo(geometry.localPosition));
                Assert.That(edited.TrayScene.Geometry, Is.Not.SameAs(authored.TrayScene.Geometry));
                Undo.PerformUndo();
                yield return null;
                Invoke(window, "EnsureStage");
                var undone = Field<SpiritEncounterView>(window, "view");
                Assert.That(frame.anchorMin, Is.EqualTo(originalAnchor));
                Assert.That(undone.GameplayHud.DiceTrayFrame.anchorMin, Is.EqualTo(originalAnchor));
                Assert.That(undone.TrayScene.RenderCamera.transform.localRotation, Is.EqualTo(originalRotation));
                Assert.That(undone.TrayScene.RenderCamera.backgroundColor, Is.EqualTo(originalBackground));
                Assert.That(undone.TrayScene.RenderCamera.clearFlags, Is.EqualTo(originalClear));
                Assert.That(undone.TrayScene.Geometry.transform.localPosition, Is.EqualTo(originalPosition));
                Undo.PerformRedo();
                yield return null;
                Invoke(window, "EnsureStage");
                var redone = Field<SpiritEncounterView>(window, "view");
                AssertRect(redone.GameplayHud.DiceTrayFrame, frame);
                Assert.That(redone.TrayScene.RenderCamera.backgroundColor, Is.EqualTo(camera.backgroundColor));
                Assert.That(redone.TrayScene.Geometry.transform.localPosition, Is.EqualTo(geometry.localPosition));
            }
            finally
            {
                Invoke(window, "ReleaseStage");
                Undo.ClearUndo(frame); Undo.ClearUndo(camera); Undo.ClearUndo(camera.transform); Undo.ClearUndo(geometry);
                EditorSceneManager.CloseScene(source, true);
                SceneManager.SetActiveScene(original);
            }
        }

        [Test]
        public void RebuildingAndClosingReleaseOwnedStageWhilePreservingAuthoredAssetsAndPipeline()
        {
            Invoke(window, "EnsureStage");
            var scene = Field<SpiritTrayScene>(window, "trayScene");
            var camera = scene.RenderCamera;
            var texture = scene.Texture;
            var output = Field<RenderTexture>(window, "output");
            RenderPipelineAsset acquired = QualitySettings.renderPipeline;
            Set(window, "rebuild", true); Invoke(window, "EnsureStage");
            Assert.That(scene == null && camera == null && texture == null && output == null, Is.True);
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(acquired));
            Assert.That(Field<SpiritTrayScene>(window, "trayScene").Geometry, Is.Not.Null);
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
                Invoke(window, "RenderPreview");
                Assert.That(RenderTexture.active, Is.SameAs(sentinel));
                Assert.That(GL.sRGBWrite, Is.EqualTo(!srgb));
                RenderTexture.active = scene.Texture;
                readback.ReadPixels(new Rect(0, 0, scene.Texture.width, scene.Texture.height), 0, 0); readback.Apply();
                Assert.That(readback.GetPixels32().Count(pixel => pixel.a > 200),
                    Is.GreaterThan(scene.Texture.width * scene.Texture.height / 100));
                Assert.That(output.IsCreated(), Is.True);
                string captureRoot = System.Environment.GetEnvironmentVariable("DICE_SPIRIT_CAPTURE_ROOT");
                if (!string.IsNullOrEmpty(captureRoot))
                {
                    System.IO.Directory.CreateDirectory(captureRoot);
                    var table = Field<EncounterTablePresentationAsset>(window, "table");
                    string path = System.IO.Path.Combine(captureRoot,
                        "authored-preview-" + table.name + "-" + output.width + "x" + output.height + ".png");
                    Assert.That(System.IO.File.Exists(path), Is.False, "Use a fresh capture directory.");
                    var capture = new Texture2D(output.width, output.height, TextureFormat.RGBA32, false);
                    try
                    {
                        RenderTexture.active = output;
                        capture.ReadPixels(new Rect(0, 0, output.width, output.height), 0, 0);
                        capture.Apply();
                        System.IO.File.WriteAllBytes(path, capture.EncodeToPNG());
                        TestContext.WriteLine("Authored encounter preview: " + path);
                    }
                    finally { Object.DestroyImmediate(capture); }
                }
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

        private static T Field<T>(object instance, string name)
            => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        private static void Set(object instance, string name, object value)
            => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
        private static void Invoke(object instance, string name)
            => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);
    }
}
