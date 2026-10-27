using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dice.Tests
{
    public sealed class SpiritEncounterTestLightTests
    {
        private const float Tolerance = .00001f;
        private string tablePath;
        private Scene original, preview;
        private bool originalDirty;
        private readonly List<UnityEngine.Object> resources = new List<UnityEngine.Object>();
        private SpiritEncounterView view;
        private SceneLightingState sampled;
        private EncounterTablePresentationAsset bound, unbound;

        [SetUp]
        public void SetUp()
        {
            original = SceneManager.GetActiveScene();
            originalDirty = original.isDirty;
            tablePath = TestContent.LitTablePath();
            var authoredTable = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(tablePath);
            Assert.That(authoredTable, Is.Not.Null, tablePath);
            Assert.That(authoredTable.Scene.RuntimeKeyIsValid(), Is.True, tablePath + " must reference its encounter scene.");
            preview = EditorSceneManager.OpenPreviewScene(AssetDatabase.GUIDToAssetPath(authoredTable.Scene.AssetGUID));
            // Opening a preview scene can unload assets held only by test locals, so the table is resolved again.
            authoredTable = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(tablePath);
            Assert.That(authoredTable, Is.Not.Null, tablePath);
            view = preview.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<SpiritEncounterView>(true)).Single();
            var prop = new GameObject("Table Piece", typeof(RectTransform));
            prop.transform.SetParent(view.Portrait, false);
            var piece = prop.AddComponent<SpiritTablePieceView>();
            Set(piece, "pieceId", "piece.test.prop");
            Set(piece, "interactionId", "position");
            Field<List<SpiritTablePieceView>>(view, "tablePieces").Add(piece);
            Invoke(view, "Build");
            // A preview scene neither sizes nor scales its overlay canvas; the portrait takes its reference size as in the composition preview.
            view.Canvas.transform.localScale = Vector3.one;
            view.Portrait.anchorMin = view.Portrait.anchorMax = new Vector2(.5f, .5f);
            view.Portrait.sizeDelta = new Vector2(540, 960);
            foreach (var rect in view.GetComponentsInChildren<RectTransform>(true)) rect.ForceUpdateRectTransforms();
            view.RewardsPanel.Root.gameObject.SetActive(false);
            view.DiceTray.RenderCamera.scene = preview;
            view.ModifierHistory.Renderer.RenderCamera.scene = preview;
            view.DiceTray.RenderCamera.enabled = false;
            view.ModifierHistory.Renderer.RenderCamera.enabled = false;
            var lighting = Lighting(.5f, .8f);
            bound = UnityEngine.Object.Instantiate(authoredTable);
            unbound = UnityEngine.Object.Instantiate(authoredTable);
            resources.Add(bound);
            resources.Add(unbound);
            Set(bound, "sceneLighting", lighting);
            Set(bound, "pieceLights", new[]
            {
                new EncounterTablePieceLight
                {
                    LightIndex = 0, PieceId = "piece.test.prop", InteractionId = "position", ReferencePosition = new Vector2(.5f, .5f)
                }
            });
            Set(unbound, "sceneLighting", lighting);
            Set(unbound, "pieceLights", Array.Empty<EncounterTablePieceLight>());
            // The authored resting levels index the authored lights; the synthetic lights rest at full intensity.
            Set(bound, "restingLightLevels", Array.Empty<float>());
            Set(unbound, "restingLightLevels", Array.Empty<float>());
            sampled = Field<SceneLightingState>(view, "sceneLightingState");
        }

        [TearDown]
        public void TearDown()
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            foreach (var resource in resources)
                if (resource != null) UnityEngine.Object.DestroyImmediate(resource);
            resources.Clear();
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(original));
            Assert.That(original.isDirty, Is.EqualTo(originalDirty));
        }

        [Test]
        public void PieceLightsHoldOverridesInPlaceOfTheAmbientMultiplier()
        {
            ConfigureAuthoredTable(view, bound);
            AssertOverridesHold();
        }

        [Test]
        public void SceneLightsHoldOverridesInPlaceOfTheAmbientMultiplier()
        {
            ConfigureAuthoredTable(view, unbound);
            AssertOverridesHold();
        }

        [Test]
        public void InvalidOverridesAreRejected()
        {
            // Building the view configures it with its authored table, so it offers exactly that table's scene lights.
            var authored = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(tablePath);
            int authoredLights = authored.SceneLighting != null && authored.SceneLighting.Lights != null
                ? authored.SceneLighting.Lights.Length : 0;
            Assert.That(view.TestLightCount, Is.EqualTo(authoredLights), "The built view offered lights its authored table lacks.");
            Assert.Throws<ArgumentOutOfRangeException>(() => view.SetTestLightOverride(authoredLights, 1f));
            ConfigureAuthoredTable(view, bound);
            Assert.That(view.TestLightCount, Is.EqualTo(2));
            view.SetTestLightOverride(1, .75f);
            Assert.Throws<ArgumentOutOfRangeException>(() => view.SetTestLightOverride(-1, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => view.SetTestLightOverride(2, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => view.GetTestLightOverride(-1, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => view.GetTestLightOverride(2, out _));
            foreach (float invalid in new[] { -.1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => view.SetTestLightOverride(1, invalid), invalid.ToString());
            Assert.That(view.GetTestLightOverride(1, out float held), Is.True);
            Assert.That(held, Is.EqualTo(.75f), "A rejected value replaced the held override.");
            Assert.That(view.GetTestLightOverride(0, out _), Is.False, "A rejected value held a light.");
        }

        [Test]
        public void ReconfiguringAndResettingPresentationReleaseOverrides()
        {
            ConfigureAuthoredTable(view, bound);
            view.SetTestLightOverride(0, 0f);
            view.SetTestLightOverride(1, 2f);
            ConfigureAuthoredTable(view, unbound);
            Assert.That(view.TestLightCount, Is.EqualTo(2));
            Assert.That(view.GetTestLightOverride(0, out _) || view.GetTestLightOverride(1, out _), Is.False,
                "Configuring another table retained testing overrides.");
            Invoke(view, "LateUpdate");
            Assert.That(sampled.GetIntensity(0), Is.EqualTo(.5f).Within(Tolerance));
            Assert.That(sampled.GetIntensity(1), Is.EqualTo(.8f).Within(Tolerance));
            view.SetTestLightOverride(0, 0f);
            Invoke(view, "ResetPresentation", true);
            Assert.That(view.GetTestLightOverride(0, out float multiplier), Is.False, "Resetting presentation retained an override.");
            Assert.That(multiplier, Is.EqualTo(1f));
        }

        private void AssertOverridesHold()
        {
            var activity = (IAmbientLightingTarget)view;
            activity.SetLightIntensity(0, .25f);
            activity.SetLightIntensity(1, .5f);
            Invoke(view, "LateUpdate");
            AssertSampled(.5f * .25f, .8f * .5f, "The ambient multipliers did not reach the sampled lights.");
            Assert.That(view.GetTestLightOverride(0, out float ambient), Is.False);
            Assert.That(ambient, Is.EqualTo(.25f), "A released light did not report the ambient multiplier.");

            view.SetTestLightOverride(0, 0f);
            view.SetTestLightOverride(1, 1.5f);
            // The activity restoring full intensity must not end a held override.
            activity.SetLightIntensity(0, 1f);
            activity.SetLightIntensity(1, 1f);
            Invoke(view, "LateUpdate");
            AssertSampled(0f, .8f * 1.5f, "A held override did not replace the ambient multiplier.");
            Assert.That(view.GetTestLightOverride(0, out float held), Is.True);
            Assert.That(held, Is.EqualTo(0f));
            Assert.That(activity.GetLightIntensity(0), Is.EqualTo(1f), "An override changed the activity's multiplier.");

            view.ReleaseTestLightOverrides();
            Invoke(view, "LateUpdate");
            AssertSampled(.5f, .8f, "Releasing overrides did not restore the activity's multipliers.");
            Assert.That(view.GetTestLightOverride(1, out float released), Is.False);
            Assert.That(released, Is.EqualTo(1f));
        }

        private void AssertSampled(float first, float second, string message)
        {
            Assert.That(sampled.GetIntensity(0), Is.EqualTo(first).Within(Tolerance), message);
            Assert.That(sampled.GetIntensity(1), Is.EqualTo(second).Within(Tolerance), message);
        }

        private static void ConfigureAuthoredTable(SpiritEncounterView view, EncounterTablePresentationAsset table)
        {
            Set(view, "tablePresentation", table);
            view.RoomImage.texture = view.TableImage.texture = table.SceneLighting.BaseTexture;
            Invoke(view, "ConfigureTable", table);
        }

        private SceneLightingAsset Lighting(float first, float second)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var contribution = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; ++i) pixels[i] = new Color(.1f, .12f, .14f);
            texture.SetPixels(pixels); texture.Apply();
            for (int i = 0; i < pixels.Length; ++i) pixels[i] = new Color(.2f, .15f, .08f);
            contribution.SetPixels(pixels); contribution.Apply();
            var asset = UnityEngine.ScriptableObject.CreateInstance<SceneLightingAsset>();
            asset.Shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Dice/Presentation/Shared/SceneLighting.shader");
            asset.hideFlags = texture.hideFlags = contribution.hideFlags = HideFlags.HideAndDontSave;
            asset.BaseTexture = texture;
            asset.Lights = new[]
            {
                new SceneLightLayer { Contribution = contribution, Intensity = first, FlickerAmount = 0f },
                new SceneLightLayer { Contribution = contribution, Intensity = second, FlickerAmount = 0f }
            };
            resources.Add(asset); resources.Add(texture); resources.Add(contribution);
            return asset;
        }

        private static T Field<T>(object instance, string name)
            => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        private static void Set(object instance, string name, object value)
            => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
        private static void Invoke(object instance, string name, params object[] arguments)
            => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, arguments);
    }
}
