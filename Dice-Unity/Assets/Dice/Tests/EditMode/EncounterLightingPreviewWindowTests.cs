using System.Linq;
using Dice.Editor;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dice.Tests
{
    public sealed class EncounterLightingPreviewWindowTests
    {
        private EncounterLightingPreviewWindow window;
        private SceneLightingAsset lighting;
        private EncounterTablePresentationAsset table;
        private Texture2D texture;

        [SetUp]
        public void SetUp()
        {
            texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            texture.SetPixels(Enumerable.Repeat(new Color(.55f, .35f, .2f, 1f), 64).ToArray());
            texture.Apply();
            lighting = ScriptableObject.CreateInstance<SceneLightingAsset>();
            lighting.Shader = TestShaders.Load(TestShaders.SceneLighting);
            lighting.BaseTexture = texture;
            window = ScriptableObject.CreateInstance<EncounterLightingPreviewWindow>();
            // The entry preview never reads a table; table previews select theirs explicitly.
            window.table = null;
            window.entry = lighting;
            window.animate = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (window != null) UnityEngine.Object.DestroyImmediate(window);
            if (table != null) UnityEngine.Object.DestroyImmediate(table);
            UnityEngine.Object.DestroyImmediate(lighting);
            UnityEngine.Object.DestroyImmediate(texture);
        }

        /** A copy of the authored table that keeps its encounter scene and tray but draws this fixture's synthetic lighting. */
        private void UseTable()
        {
            string tablePath = TestContent.AuthoredTablePath();
            var authored = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(tablePath);
            Assert.That(authored, Is.Not.Null, tablePath);
            table = UnityEngine.Object.Instantiate(authored);
            SyntheticUi.Set(table, "roomTexture", texture);
            SyntheticUi.Set(table, "sceneLighting", lighting);
            window.table = table;
        }

        /** Checks the entry preview draws its lighting artwork and restores the caller's render target, sRGB write and pipeline / fails if the preview stays dark or leaks render state. */
        [Test]
        public void EntryPreviewRendersItsArtworkAndRestoresRenderState()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "This test requires graphics-enabled Unity.");
            RenderTexture previous = RenderTexture.active;
            bool previousSrgb = GL.sRGBWrite;
            RenderPipelineAsset previousPipeline = QualitySettings.renderPipeline;
            var sentinel = new RenderTexture(8, 8, 0);
            Texture2D readback = null;
            try
            {
                sentinel.Create();
                window.showTable = false;
                window.EnsureStage();
                RenderTexture.active = sentinel;
                GL.sRGBWrite = !previousSrgb;
                window.RenderPreview();
                Assert.That(RenderTexture.active, Is.SameAs(sentinel));
                Assert.That(GL.sRGBWrite, Is.EqualTo(!previousSrgb));
                Assert.That(QualitySettings.renderPipeline, Is.SameAs(previousPipeline));
                var output = window.output;
                Assert.That(output, Is.Not.Null);
                RenderTexture.active = output;
                readback = new Texture2D(output.width, output.height, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, output.width, output.height), 0, 0); readback.Apply();
                Assert.That(readback.GetPixels32().Count(pixel => pixel.r > 64 || pixel.g > 64 || pixel.b > 64),
                    Is.GreaterThan(output.width * output.height / 2), "The synthetic artwork should fill the preview, not just its clear color.");
            }
            finally
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousSrgb;
                sentinel.Release(); UnityEngine.Object.DestroyImmediate(sentinel);
                if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
            }
        }

        [Test]
        public void ProductionTablePreviewKeepsPipelineAcrossStageRebuildAndReleasesOwnedResources()
        {
            UseTable();
            RenderPipelineAsset previous = QualitySettings.renderPipeline;
            window.showTable = true;
            window.EnsureStage();
            RenderPipelineAsset acquired = QualitySettings.renderPipeline;
            Assert.That(acquired, Is.Not.Null);
            Assert.That(window.pipeline.Asset, Is.SameAs(acquired));
            var output = window.output;
            var copiedLighting = window.lighting;
            var material = window.compositor.Material;
            var diceRoot = window.tray.RenderRoot;
            var historyRoot = window.history.RenderRoot;
            window.ReleaseStage();
            Assert.That(output == null && copiedLighting == null && material == null && diceRoot == null && historyRoot == null, Is.True);
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(acquired), "A stage rebuild must not repeatedly tear down URP.");
            Assert.That(texture != null && lighting != null && table.SceneLighting == lighting, Is.True, "Authored resources remain borrowed.");
            window.showTable = false;
            window.EnsureStage();
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(acquired));
            UnityEngine.Object.DestroyImmediate(window);
            window = null;
            Assert.That(QualitySettings.renderPipeline, Is.SameAs(previous), "Closing the preview releases its retained pipeline override.");
        }
    }
}
