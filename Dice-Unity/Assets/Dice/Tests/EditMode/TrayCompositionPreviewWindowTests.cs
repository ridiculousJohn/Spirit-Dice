using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Dice.Editor;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
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
            if (window != null) UnityEngine.Object.DestroyImmediate(window);
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
        public void AuthoredTablePreviewRendersSplitArtworkAndSharedRuntimeBaysAtBothWidths(string path, bool narrow)
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>("Assets/Dice/Content/Presentation/" + path);
            Set(window, "table", table); Set(window, "narrow", narrow); Set(window, "historyCount", 6);
            Invoke(window, "EnsureStage");
            var scene = Field<SpiritTrayScene>(window, "trayScene");
            var dice = Field<SpiritDiceTray>(window, "dice");
            var history = Field<SpiritModifierHistoryRenderer>(window, "history");
            Assert.That(dice.RenderCamera, Is.SameAs(scene.RenderCamera));
            Assert.That(history.RenderCamera, Is.SameAs(scene.RenderCamera));
            Assert.That(dice.Texture, Is.SameAs(history.Texture));
            Assert.That(history.Count, Is.EqualTo(6));
            Assert.That(scene.RenderRoot.GetComponentsInChildren<Camera>(true).Length, Is.EqualTo(1));
            var output = Field<RenderTexture>(window, "output");
            Assert.That(output.width, Is.EqualTo(narrow ? 443 : 540));
            Assert.That(output.height, Is.EqualTo(960));
            var stage = Field<GameObject>(window, "stage");
            var tray = stage.GetComponentsInChildren<RawImage>().Single(image => image.name == "SharedTray");
            var frame = (RectTransform)tray.transform.parent;
            Rect composition = table.TrayPresentation.GetCompositionRect((float)output.width / output.height);
            Assert.That(frame.anchorMin, Is.EqualTo(composition.min));
            Assert.That(frame.anchorMax, Is.EqualTo(composition.max));
            var room = Field<RawImage>(window, "roomImage");
            var foreground = Field<RawImage>(window, "tableImage");
            Assert.That(room.uvRect, Is.EqualTo(new Rect(0, table.RoomTableEdge, 1, 1 - table.RoomTableEdge)));
            Assert.That(foreground.uvRect, Is.EqualTo(new Rect(0, 0, 1, table.RoomTableEdge)));
            RenderTexture active = RenderTexture.active;
            bool srgb = GL.sRGBWrite;
            var sentinel = new RenderTexture(8, 8, 0);
            Texture2D readback = null;
            try
            {
                sentinel.Create(); RenderTexture.active = sentinel; GL.sRGBWrite = !srgb;
                Invoke(window, "RenderPreview");
                Assert.That(RenderTexture.active, Is.SameAs(sentinel));
                Assert.That(GL.sRGBWrite, Is.EqualTo(!srgb));
                Assert.That(room.texture, Is.SameAs(table.SceneLighting != null ? table.SceneLighting.BaseTexture : table.RoomTexture));
                if (table.SceneLighting != null)
                    Assert.That(room.material, Is.SameAs(foreground.material));
                RenderTexture.active = output;
                readback = new Texture2D(output.width, output.height, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, output.width, output.height), 0, 0); readback.Apply();
                Assert.That(readback.GetPixels32().Count(pixel => pixel.r > 45 || pixel.g > 45 || pixel.b > 45),
                    Is.GreaterThan(output.width * output.height / 4));
                string captures = Path.Combine(Path.GetDirectoryName(Application.consoleLogPath), "captures");
                Directory.CreateDirectory(captures);
                File.WriteAllBytes(Path.Combine(captures, Path.GetFileNameWithoutExtension(path) + "-" + output.width + ".png"),
                    readback.EncodeToPNG());
                RenderTexture.active = scene.Texture;
                UnityEngine.Object.DestroyImmediate(readback);
                readback = new Texture2D(scene.Texture.width, scene.Texture.height, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, scene.Texture.width, scene.Texture.height), 0, 0); readback.Apply();
                Assert.That(readback.GetPixels32().Count(pixel => pixel.a > 200),
                    Is.GreaterThan(scene.Texture.width * scene.Texture.height / 10), "Authored tray geometry must render.");
                var surfaces = table.TrayPresentation.GeometryPrefab.GetComponentsInChildren<Renderer>()
                    .Select(renderer => scene.Geometry.transform.Find(AnimationUtility.CalculateTransformPath(
                        renderer.transform, table.TrayPresentation.GeometryPrefab.transform)).GetComponent<Renderer>()).ToArray();
                try
                {
                    foreach (var surface in surfaces) surface.enabled = false;
                    Invoke(window, "RenderPreview");
                    RenderTexture.active = scene.Texture;
                    readback.ReadPixels(new Rect(0, 0, scene.Texture.width, scene.Texture.height), 0, 0); readback.Apply();
                    Assert.That(readback.GetPixels32().Count(pixel => pixel.a > 200),
                        Is.GreaterThan(scene.Texture.width * scene.Texture.height / 100),
                        "The dice and history silhouettes must render independently of artwork and illumination.");
                }
                finally { foreach (var surface in surfaces) surface.enabled = true; }
            }
            finally
            {
                RenderTexture.active = active; GL.sRGBWrite = srgb;
                sentinel.Release(); UnityEngine.Object.DestroyImmediate(sentinel);
                if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
            }
        }

        [UnityTest]
        public IEnumerator UndoAndRedoRestorePlacementAndViewAngleAfterSerializedAuthoring()
        {
            CreateAuthoringAssets(out var table, out var presentation);
            try
            {
                Vector3 originalAngle = presentation.CameraEulerAngles;
                Rect originalComposition = presentation.CompositionRect;
                var editedAngle = new Vector3(37, 61, 23);
                Invoke(window, "EnsureStage");
                AssertPreviewComposition(window, originalAngle, originalComposition);
                var stage = Field<GameObject>(window, "stage");
                Camera camera = Field<SpiritTrayScene>(window, "trayScene").RenderCamera;
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Edit tray placement and view angle");
                var serialized = new SerializedObject(presentation);
                serialized.Update();
                SpiritTrayPresentationAssetEditor.SetPlacement(serialized, new Vector2(.46f, .38f), .84f);
                serialized.FindProperty("CameraEulerAngles").vector3Value = editedAngle;
                Assert.That(Invoke<bool>(window, "ApplyAuthoring", serialized), Is.True);
                Rect editedComposition = presentation.CompositionRect;
                Invoke(window, "EnsureStage");
                yield return null;
                yield return null;
                AssertPreviewComposition(window, editedAngle, editedComposition);

                Undo.IncrementCurrentGroup();
                Undo.PerformUndo();
                Assert.That(presentation.CameraEulerAngles, Is.EqualTo(originalAngle));
                Assert.That(presentation.CompositionRect, Is.EqualTo(originalComposition));
                Invoke(window, "EnsureStage");
                yield return null;
                AssertPreviewComposition(window, originalAngle, originalComposition);

                Undo.PerformRedo();
                Assert.That(presentation.CameraEulerAngles, Is.EqualTo(editedAngle));
                Assert.That(presentation.CompositionRect, Is.EqualTo(editedComposition));
                Invoke(window, "EnsureStage");
                yield return null;
                AssertPreviewComposition(window, editedAngle, editedComposition);
                Assert.That(Field<GameObject>(window, "stage"), Is.SameAs(stage));
                Assert.That(Field<SpiritTrayScene>(window, "trayScene").RenderCamera, Is.SameAs(camera));
            }
            finally { ReleaseAuthoringAssets(table, presentation); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GameplayOverlayUsesLiveControlsAndTogglesWithoutRebuildingTray(bool narrow)
        {
            Set(window, "narrow", narrow);
            Invoke(window, "EnsureStage");
            var root = Field<RectTransform>(window, "gameplayUiRoot");
            var view = Field<SpiritEncounterView>(window, "gameplayUi");
            var scene = Field<SpiritTrayScene>(window, "trayScene");
            var camera = scene.RenderCamera;
            Assert.That(root.gameObject.activeSelf, Is.True);
            Assert.That(root.GetSiblingIndex(), Is.EqualTo(root.parent.childCount - 1));
            Assert.That(view.PlayButton.GetComponentInChildren<Text>().text, Is.EqualTo("Play Hand"));
            Assert.That(view.RerollButton.GetComponentInChildren<Text>().text, Is.EqualTo("Reroll"));
            Assert.That(view.CharmsButton, Is.Not.Null);
            Assert.That(view.HandRanksButton, Is.Not.Null);
            Assert.That(view.BaseScoreLabel.text, Is.EqualTo("0"));
            Assert.That(view.MultiplierLabel.text, Is.EqualTo("0"));
            Assert.That(view.HandScoreLabel.text, Is.EqualTo("0"));
            Assert.That(root.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(root.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<SpiritTrayScene>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<SpiritDiceTray>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Canvas>(true), Is.Empty);
            Invoke(window, "RenderPreview");

            Set(window, "showGameplayUi", false);
            Invoke(window, "EnsureStage");
            Assert.That(root.gameObject.activeSelf, Is.False);
            Set(window, "showGameplayUi", true);
            Invoke(window, "EnsureStage");
            Assert.That(Field<RectTransform>(window, "gameplayUiRoot"), Is.SameAs(root));
            Assert.That(root.gameObject.activeSelf, Is.True);
            Assert.That(scene.RenderCamera, Is.SameAs(camera));

            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(
                "Assets/Dice/Content/Presentation/MothCrossroads/MothCrossroadsTable.asset");
            Set(window, "table", table);
            Invoke(window, "EnsureStage");
            Assert.That(Field<SpiritEncounterView>(window, "gameplayUi"), Is.SameAs(view));
            Assert.That(root.GetComponentsInChildren<Text>().Single(value => value.name == "EncounterTitle").text,
                Is.EqualTo(table.DisplayTitle));
            Assert.That(root.GetSiblingIndex(), Is.EqualTo(root.parent.childCount - 1));
            Invoke(window, "RenderPreview");
        }

        [UnityTest]
        public IEnumerator SerializedPlacementAndAngleSurviveImmediatePreviewRefreshAndEditorUpdates()
        {
            CreateAuthoringAssets(out var table, out var presentation);
            try
            {
                var centers = new[] { presentation.CompositionRect.center, new Vector2(.42f, .32f),
                    new Vector2(.47f, .38f), new Vector2(.5f, .4f) };
                var widths = new[] { presentation.CompositionRect.width, .64f, .78f, .9f };
                var angles = new[] { presentation.CameraEulerAngles, new Vector3(45, -8, 5),
                    new Vector3(58, 18, -9), new Vector3(37, 61, 23) };
                var compositions = new Rect[centers.Length];
                compositions[0] = presentation.CompositionRect;
                Invoke(window, "EnsureStage");
                var persistentStage = Field<GameObject>(window, "stage");
                var persistentCamera = Field<SpiritTrayScene>(window, "trayScene").RenderCamera;
                for (int i = 1; i < centers.Length; ++i)
                {
                    Undo.IncrementCurrentGroup();
                    Undo.SetCurrentGroupName("Edit tray placement and angle " + i);
                    var serialized = new SerializedObject(presentation);
                    serialized.Update();
                    SpiritTrayPresentationAssetEditor.SetPlacement(serialized, centers[i], widths[i]);
                    serialized.FindProperty("CameraEulerAngles").vector3Value = angles[i];
                    Assert.That(Invoke<bool>(window, "ApplyAuthoring", serialized), Is.True);
                    compositions[i] = presentation.CompositionRect;
                    Assert.That(Vector2.Distance(compositions[i].center, centers[i]), Is.LessThan(.00001f));
                    Assert.That(compositions[i].width, Is.EqualTo(widths[i]).Within(.00001f));
                    // Repaint and editor updates must preserve each authored Undo operation.
                    Invoke(window, "EnsureStage");
                    yield return null;
                    yield return null;
                    AssertPreviewComposition(window, angles[i], compositions[i]);
                    Assert.That(Field<GameObject>(window, "stage"), Is.SameAs(persistentStage));
                    Assert.That(Field<SpiritTrayScene>(window, "trayScene").RenderCamera, Is.SameAs(persistentCamera));
                }

                Undo.IncrementCurrentGroup();
                for (int i = centers.Length - 2; i >= 0; --i)
                {
                    Undo.PerformUndo();
                    Assert.That(presentation.CompositionRect, Is.EqualTo(compositions[i]),
                        "Every committed placement edit must remain independently undoable.");
                    Assert.That(presentation.CameraEulerAngles, Is.EqualTo(angles[i]));
                    Invoke(window, "EnsureStage");
                    yield return null;
                    AssertPreviewComposition(window, angles[i], compositions[i]);
                }
                for (int i = 1; i < centers.Length; ++i)
                {
                    Undo.PerformRedo();
                    Assert.That(presentation.CompositionRect, Is.EqualTo(compositions[i]));
                    Assert.That(presentation.CameraEulerAngles, Is.EqualTo(angles[i]));
                    Invoke(window, "EnsureStage");
                    yield return null;
                    AssertPreviewComposition(window, angles[i], compositions[i]);
                }
                Assert.That(Field<GameObject>(window, "stage"), Is.SameAs(persistentStage));
                Assert.That(Field<SpiritTrayScene>(window, "trayScene").RenderCamera, Is.SameAs(persistentCamera));
            }
            finally { ReleaseAuthoringAssets(table, presentation); }
        }

        [UnityTest]
        public IEnumerator PlacementDragKeepsOneUndoGroupAcrossPreviewUpdates()
        {
            CreateAuthoringAssets(out var table, out var presentation);
            try
            {
                Rect originalComposition = presentation.CompositionRect;
                Invoke(window, "EnsureStage");
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Drag tray placement");
                int group = Undo.GetCurrentGroup();
                for (int i = 1; i <= 3; ++i)
                {
                    var serialized = new SerializedObject(presentation);
                    serialized.Update();
                    SpiritTrayPresentationAssetEditor.SetPlacement(serialized,
                        new Vector2(.4f + i * .02f, .32f + i * .02f), .6f + i * .05f);
                    Assert.That(Invoke<bool>(window, "ApplyAuthoring", serialized), Is.True);
                    Invoke(window, "EnsureStage");
                    yield return null;
                    Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(group),
                        "Preview refresh must not split a placement drag into separate undo groups.");
                }
                Rect editedComposition = presentation.CompositionRect;
                Assert.That(Vector2.Distance(editedComposition.center, new Vector2(.46f, .38f)), Is.LessThan(.00001f));
                Assert.That(editedComposition.width, Is.EqualTo(.75f).Within(.00001f));
                Undo.IncrementCurrentGroup();
                Undo.PerformUndo();
                Assert.That(presentation.CompositionRect, Is.EqualTo(originalComposition),
                    "A single undo must restore the complete placement drag.");
                Invoke(window, "EnsureStage");
                yield return null;
                AssertPreviewComposition(window, presentation.CameraEulerAngles, originalComposition);

                Undo.PerformRedo();
                Assert.That(presentation.CompositionRect, Is.EqualTo(editedComposition));
                Invoke(window, "EnsureStage");
                yield return null;
                AssertPreviewComposition(window, presentation.CameraEulerAngles, editedComposition);
            }
            finally { ReleaseAuthoringAssets(table, presentation); }
        }

        [UnityTest]
        public IEnumerator GeometryReplacementAndTextureResizeKeepSceneAliveAndRemainUndoable()
        {
            var source = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(
                "Assets/Dice/Content/Presentation/SpiritHouse/LastLanternTable.asset");
            var alternate = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(
                "Assets/Dice/Content/Presentation/MothCrossroads/MothCrossroadsTable.asset");
            var table = UnityEngine.Object.Instantiate(source);
            var presentation = UnityEngine.Object.Instantiate(source.TrayPresentation);
            table.hideFlags = presentation.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Set(table, "trayPresentation", presentation);
                Set(window, "table", table);
                Invoke(window, "EnsureStage");
                Scene preview = Field<Scene>(window, "previewScene");
                var stage = Field<GameObject>(window, "stage");
                var scene = Field<SpiritTrayScene>(window, "trayScene");
                Camera camera = scene.RenderCamera;
                RenderTexture texture = scene.Texture;
                SpiritTrayGeometry geometry = scene.Geometry;
                SpiritTrayGeometry originalPrefab = presentation.GeometryPrefab;
                Vector2Int originalSize = presentation.TextureSize;

                Undo.IncrementCurrentGroup();
                var serialized = new SerializedObject(presentation);
                serialized.FindProperty("GeometryPrefab").objectReferenceValue = alternate.TrayPresentation.GeometryPrefab;
                serialized.FindProperty("TextureSize").vector2IntValue = new Vector2Int(512, 256);
                Assert.That(Invoke<bool>(window, "ApplyAuthoring", serialized), Is.True);
                Invoke(window, "EnsureStage");
                yield return null;
                yield return null;
                Assert.That(geometry == null, Is.True, "Replacing the prefab must release its previous instance.");
                Assert.That(scene.Geometry.ModifierWidth, Is.EqualTo(alternate.TrayPresentation.GeometryPrefab.ModifierWidth));
                Assert.That(scene.Texture.width, Is.EqualTo(512));
                Assert.That(scene.Texture.height, Is.EqualTo(256));
                Assert.That(scene.Texture, Is.SameAs(texture));
                Assert.That(scene.RenderCamera, Is.SameAs(camera));
                Assert.That(Field<GameObject>(window, "stage"), Is.SameAs(stage));
                Assert.That(Field<Scene>(window, "previewScene"), Is.EqualTo(preview));
                Assert.That(Field<SpiritDiceTray>(window, "dice").Texture, Is.SameAs(texture));
                Assert.That(Field<SpiritModifierHistoryRenderer>(window, "history").Texture, Is.SameAs(texture));
                Invoke(window, "RenderPreview");

                Undo.IncrementCurrentGroup();
                Undo.PerformUndo();
                Assert.That(presentation.GeometryPrefab, Is.SameAs(originalPrefab));
                Assert.That(presentation.TextureSize, Is.EqualTo(originalSize));
                Invoke(window, "EnsureStage");
                yield return null;
                Assert.That(scene.Geometry.ModifierWidth, Is.EqualTo(originalPrefab.ModifierWidth));
                Assert.That(scene.Texture.width, Is.EqualTo(originalSize.x));
                Assert.That(scene.Texture.height, Is.EqualTo(originalSize.y));
                Assert.That(scene.RenderCamera, Is.SameAs(camera));
                Assert.That(Field<Scene>(window, "previewScene"), Is.EqualTo(preview));

                Undo.PerformRedo();
                Assert.That(presentation.GeometryPrefab, Is.SameAs(alternate.TrayPresentation.GeometryPrefab));
                Invoke(window, "EnsureStage");
                yield return null;
                Assert.That(scene.Texture.width, Is.EqualTo(512));
                Assert.That(Field<Scene>(window, "previewScene"), Is.EqualTo(preview));
                Invoke(window, "RenderPreview");
            }
            finally
            {
                Invoke(window, "ReleaseStage");
                Undo.ClearUndo(presentation);
                Undo.ClearUndo(table);
                UnityEngine.Object.DestroyImmediate(presentation);
                UnityEngine.Object.DestroyImmediate(table);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PlacementSizeControlsEffectiveImageWidthAndPositionAtBothPortraitWidths(bool narrow)
        {
            CreateAuthoringAssets(out var table, out var presentation);
            try
            {
                Set(window, "narrow", narrow);
                presentation.CompositionRect = new Rect(.2f, .375f, .6f, .05f);
                Invoke(window, "EnsureStage");
                AssertPreviewComposition(window, presentation.CameraEulerAngles, presentation.CompositionRect);
                AssertImagePlacement(window, new Vector2(.5f, .4f), .6f);
                Camera camera = Field<SpiritTrayScene>(window, "trayScene").RenderCamera;
                Vector3 fittedPosition = camera.transform.localPosition;

                var serialized = new SerializedObject(presentation);
                serialized.Update();
                SpiritTrayPresentationAssetEditor.SetPlacement(serialized, new Vector2(.5f, .4f), .9f);
                Assert.That(Invoke<bool>(window, "ApplyAuthoring", serialized), Is.True);
                Assert.That(presentation.CompositionRect.height,
                    Is.EqualTo(.9f * (540f / 960f) / ((float)presentation.TextureSize.x / presentation.TextureSize.y)).Within(.00001f));
                Invoke(window, "EnsureStage");
                AssertPreviewComposition(window, presentation.CameraEulerAngles, presentation.CompositionRect);
                AssertImagePlacement(window, new Vector2(.5f, .4f), .9f);
                Assert.That(camera.transform.localPosition, Is.EqualTo(fittedPosition),
                    "Portrait size must scale the complete fitted tray image.");

                serialized.Update();
                SpiritTrayPresentationAssetEditor.SetPlacement(serialized, new Vector2(.5f, .7f), .9f);
                Assert.That(Invoke<bool>(window, "ApplyAuthoring", serialized), Is.True);
                Invoke(window, "EnsureStage");
                AssertImagePlacement(window, new Vector2(.5f, .7f), .9f);
                Assert.That(camera.transform.localPosition, Is.EqualTo(fittedPosition));
            }
            finally { ReleaseAuthoringAssets(table, presentation); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ViewAngleAutomaticallyFitsGeometryAndIgnoresLegacyCameraPosition(bool orthographic)
        {
            CreateAuthoringAssets(out var table, out var presentation);
            try
            {
                presentation.Orthographic = orthographic;
                presentation.CameraEulerAngles = new Vector3(37, 61, 23);
                Invoke(window, "EnsureStage");
                AssertPreviewComposition(window, presentation.CameraEulerAngles, presentation.CompositionRect);
                Camera camera = Field<SpiritTrayScene>(window, "trayScene").RenderCamera;
                Vector3 fittedPosition = camera.transform.localPosition;
                var serialized = new SerializedObject(presentation);
                serialized.Update();
                serialized.FindProperty("CameraPosition").vector3Value = new Vector3(10000, -10000, 50000);
                Assert.That(Invoke<bool>(window, "ApplyAuthoring", serialized), Is.True);
                Invoke(window, "EnsureStage");
                Assert.That(camera.transform.localPosition, Is.EqualTo(fittedPosition),
                    "Camera position must be derived from the complete tray geometry.");

                serialized.Update();
                var editedAngle = new Vector3(58, -29, -17);
                serialized.FindProperty("CameraEulerAngles").vector3Value = editedAngle;
                Assert.That(Invoke<bool>(window, "ApplyAuthoring", serialized), Is.True);
                Invoke(window, "EnsureStage");
                AssertPreviewComposition(window, editedAngle, presentation.CompositionRect);
                Assert.That(Field<SpiritTrayScene>(window, "trayScene").RenderCamera, Is.SameAs(camera));
            }
            finally { ReleaseAuthoringAssets(table, presentation); }
        }

        [Test]
        public void PreviewControlsRefreshExistingObjectsAndSampleHostTimeDeterministically()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(
                "Assets/Dice/Content/Presentation/MothCrossroads/MothCrossroadsTable.asset");
            Set(window, "table", table);
            Invoke(window, "EnsureStage");
            var stage = Field<GameObject>(window, "stage");
            var scene = Field<SpiritTrayScene>(window, "trayScene");
            var host = Field<SpiritHostPortrait>(window, "host");
            var dice = Field<SpiritDiceTray>(window, "dice");
            var history = Field<SpiritModifierHistoryRenderer>(window, "history");
            var output = Field<RenderTexture>(window, "output");
            Set(window, "narrow", true);
            Set(window, "historyCount", 12);
            Set(window, "historyScroll", 1f);
            Set(window, "sampleTime", 2.5f);
            int[] faces = Field<int[]>(window, "faces");
            for (int i = 0; i < faces.Length; ++i) faces[i] = 6;

            Invoke(window, "EnsureStage");
            Invoke(window, "RenderPreview");
            Assert.That(Field<GameObject>(window, "stage"), Is.SameAs(stage));
            Assert.That(Field<SpiritTrayScene>(window, "trayScene"), Is.SameAs(scene));
            Assert.That(Field<SpiritHostPortrait>(window, "host"), Is.SameAs(host));
            Assert.That(Field<RenderTexture>(window, "output"), Is.SameAs(output));
            Assert.That(output.width, Is.EqualTo(443));
            Assert.That(history.Count, Is.EqualTo(12));
            for (int i = 0; i < faces.Length; ++i) Assert.That(dice.GetVisibleFace(i), Is.EqualTo(6));
            Assert.That(Field<double>(host, "effectAge"), Is.EqualTo(2.5));
            Invoke(window, "RenderPreview");
            Assert.That(Field<double>(host, "effectAge"), Is.EqualTo(2.5), "Repaint must not advance the sampled effect time.");
        }

        [Test]
        public void RebuildingAndClosingReleaseOwnedStageWhilePreservingAuthoredAssetsAndPipeline()
        {
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(
                "Assets/Dice/Content/Presentation/MothCrossroads/MothCrossroadsTable.asset");
            Set(window, "table", table);
            Invoke(window, "EnsureStage");
            var acquiredPipeline = QualitySettings.renderPipeline;
            var scene = Field<SpiritTrayScene>(window, "trayScene");
            Transform root = scene.RenderRoot;
            Camera camera = scene.RenderCamera;
            RenderTexture texture = scene.Texture;
            RenderTexture output = Field<RenderTexture>(window, "output");
            Material compositor = Field<SceneLightingMaterial>(window, "compositor").Material;
            Invoke(window, "ReleaseStage");
            Assert.That(root == null, Is.True, "The preview scene root survived release.");
            Assert.That(camera == null, Is.True, "The shared camera survived release.");
            Assert.That(texture == null, Is.True, "The shared render texture survived release.");
            Assert.That(output == null, Is.True, "The portrait render texture survived release.");
            Assert.That(compositor == null, Is.True, "The owned compositor material survived release.");
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(acquiredPipeline));
            Assert.That(table != null && table.TrayPresentation.GeometryPrefab != null && table.SceneLighting.BaseTexture != null, Is.True);
            Invoke(window, "EnsureStage");
            Assert.That(Field<SpiritTrayScene>(window, "trayScene").RenderRoot, Is.Not.Null);
            UnityEngine.Object.DestroyImmediate(window); window = null;
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(previousPipeline));
        }

        private void CreateAuthoringAssets(out EncounterTablePresentationAsset table,
            out SpiritTrayPresentationAsset presentation)
        {
            var source = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(
                "Assets/Dice/Content/Presentation/SpiritHouse/LastLanternTable.asset");
            table = UnityEngine.Object.Instantiate(source);
            presentation = UnityEngine.Object.Instantiate(source.TrayPresentation);
            table.hideFlags = presentation.hideFlags = HideFlags.HideAndDontSave;
            Set(table, "trayPresentation", presentation);
            Set(window, "table", table);
        }

        private void ReleaseAuthoringAssets(EncounterTablePresentationAsset table, SpiritTrayPresentationAsset presentation)
        {
            Invoke(window, "ReleaseStage");
            Undo.ClearUndo(presentation);
            Undo.ClearUndo(table);
            UnityEngine.Object.DestroyImmediate(presentation);
            UnityEngine.Object.DestroyImmediate(table);
        }

        private static void AssertPreviewComposition(TrayCompositionPreviewWindow preview, Vector3 angle, Rect composition)
        {
            var scene = Field<SpiritTrayScene>(preview, "trayScene");
            Camera camera = scene.RenderCamera;
            var presentation = Field<EncounterTablePresentationAsset>(preview, "table").TrayPresentation;
            Assert.That(Quaternion.Angle(camera.transform.localRotation, Quaternion.Euler(angle)), Is.LessThan(.01f));
            Assert.That(camera.orthographic, Is.EqualTo(presentation.Orthographic));
            Assert.That(camera.fieldOfView, Is.EqualTo(presentation.FieldOfView));
            var output = Field<RenderTexture>(preview, "output");
            Rect renderedComposition = presentation.GetCompositionRect((float)output.width / output.height);
            Assert.That(Vector2.Distance(renderedComposition.center, composition.center), Is.LessThan(.00001f));
            Assert.That(renderedComposition.width, Is.EqualTo(composition.width).Within(.00001f));
            Assert.That(renderedComposition.height, Is.EqualTo(composition.width * ((float)output.width / output.height) /
                ((float)presentation.TextureSize.x / presentation.TextureSize.y)).Within(.00001f));
            var stage = Field<GameObject>(preview, "stage");
            var tray = stage.GetComponentsInChildren<RawImage>().Single(image => image.name == "SharedTray");
            var frame = (RectTransform)tray.transform.parent;
            Assert.That(frame.anchorMin, Is.EqualTo(renderedComposition.min));
            Assert.That(frame.anchorMax, Is.EqualTo(renderedComposition.max));
            var renderers = scene.Geometry.GetComponentsInChildren<Renderer>();
            Assert.That(renderers, Is.Not.Empty);
            foreach (Renderer renderer in renderers)
            {
                Bounds bounds = renderer.localBounds;
                for (int corner = 0; corner < 8; ++corner)
                {
                    Vector3 local = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    Vector3 projected = camera.WorldToViewportPoint(renderer.transform.TransformPoint(local));
                    Assert.That(projected.x, Is.InRange(-.001f, 1.001f), renderer.name + " extends beyond the fitted width.");
                    Assert.That(projected.y, Is.InRange(-.001f, 1.001f), renderer.name + " extends beyond the fitted height.");
                    Assert.That(projected.z, Is.InRange(camera.nearClipPlane, camera.farClipPlane),
                        renderer.name + " extends beyond the fitted clipping range.");
                }
            }
        }

        private static void AssertImagePlacement(TrayCompositionPreviewWindow preview, Vector2 center, float width)
        {
            Canvas.ForceUpdateCanvases();
            var stage = Field<GameObject>(preview, "stage");
            var tray = stage.GetComponentsInChildren<RawImage>().Single(image => image.name == "SharedTray");
            var corners = new Vector3[4];
            ((RectTransform)tray.transform).GetWorldCorners(corners);
            Camera camera = Field<Camera>(preview, "camera");
            var output = Field<RenderTexture>(preview, "output");
            Vector3 minimum = camera.WorldToViewportPoint(corners[0]);
            Vector3 maximum = camera.WorldToViewportPoint(corners[2]);
            Assert.That((maximum.x - minimum.x) * output.width, Is.EqualTo(width * output.width).Within(.5f),
                "Tray size must control the effective image width independently of the stored rectangle height.");
            Assert.That((maximum.x + minimum.x) * .5f * output.width, Is.EqualTo(center.x * output.width).Within(.5f));
            Assert.That((maximum.y + minimum.y) * .5f * output.height, Is.EqualTo(center.y * output.height).Within(.5f));
        }

        private static T Field<T>(object target, string name)
            => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Invoke(object target, string name, params object[] arguments)
            => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
        private static T Invoke<T>(object target, string name, params object[] arguments)
            => (T)target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    }
}
