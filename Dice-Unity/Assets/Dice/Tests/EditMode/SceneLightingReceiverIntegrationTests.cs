using System.Linq;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dice.Tests
{
    public sealed class SceneLightingReceiverIntegrationTests
    {
        private Scene preview;
        private TestTrayRig rig;
        private SceneLightingAsset lighting;

        [SetUp]
        public void SetUp()
        {
            preview = EditorSceneManager.NewPreviewScene();
            rig = new TestTrayRig(preview);
            rig.Stage.localPosition = new Vector3(3, -2, 5);
            rig.Stage.localRotation = Quaternion.Euler(0, 30, 0);
            lighting = ScriptableObject.CreateInstance<SceneLightingAsset>();
            lighting.BaseTexture = Texture2D.whiteTexture;
            lighting.Lights = new[]
            {
                new SceneLightLayer { Contribution = Texture2D.whiteTexture, Intensity = .6f, FlickerAmount = 0f,
                    ReceiverPosition = new Vector3(-2, 4, -3), ReceiverColor = Color.red, ReceiverIntensity = 4f, ReceiverRange = 15f },
                new SceneLightLayer { Contribution = Texture2D.whiteTexture, Intensity = .9f, FlickerAmount = 0f,
                    ReceiverPosition = new Vector3(2, 4, -3), ReceiverColor = Color.green, ReceiverIntensity = 3f, ReceiverRange = 9f }
            };
        }

        [TearDown]
        public void TearDown()
        {
            rig.Dispose();
            Object.DestroyImmediate(lighting);
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
        }

        /** Checks every stage material receives the sampled lights placed in the stage's own space / fails if a material misses the sample or lights ignore the stage transform. */
        [Test]
        public void StageMaterialsReceiveTheSampleInStageSpace()
        {
            var scene = rig.Start();
            var state = new SceneLightingState();
            state.Sample(lighting, 2.5);
            scene.SetSceneLighting(state);
            Material[] materials = Receivers(rig.Stage);
            Assert.That(materials, Is.Not.Empty);
            foreach (Material material in materials)
            {
                Assert.That(material.GetFloat("_SceneLightingEnabled"), Is.EqualTo(1f), material.name);
                for (int i = 0; i < state.Count; ++i) AssertLight(material, state, i, rig.Stage);
            }
        }

        /** Checks lighting with fewer lights empties the unused slots and no lighting disables receivers / fails if a removed light or an unlit table leaves light behind. */
        [Test]
        public void StageMaterialsClearSlotsTheSampleDoesNotLight()
        {
            var scene = rig.Start();
            var state = new SceneLightingState();
            state.Sample(lighting, 2.5);
            scene.SetSceneLighting(state);
            lighting.Lights = new[] { lighting.Lights[0] };
            state.Sample(lighting, 3);
            scene.SetSceneLighting(state);
            foreach (Material material in Receivers(rig.Stage))
            {
                AssertLight(material, state, 0, rig.Stage);
                for (int i = 1; i < SceneLightingAsset.MaximumLights; ++i) AssertEmptySlot(material, i);
            }
            scene.SetSceneLighting(null);
            foreach (Material material in Receivers(rig.Stage))
            {
                Assert.That(material.GetFloat("_SceneLightingEnabled"), Is.Zero);
                Assert.That(material.GetVector("_SceneLightingAmbient"), Is.EqualTo(Vector4.zero));
                for (int i = 0; i < SceneLightingAsset.MaximumLights; ++i) AssertEmptySlot(material, i);
            }
        }

        /** Checks modifier tokens, existing and newly added, receive the current sample in the shared stage's space / fails if a token misses the sample or places lights relative to its bay. */
        [Test]
        public void ModifierTokensReceiveTheCurrentSampleInStageSpace()
        {
            rig.Geometry.ModifierRoot.localRotation = Quaternion.Euler(0, 40, 0);
            var history = rig.StartHistory();
            var definition = TestTrayRig.Definition("test.lighting.token");
            history.Add(TestTrayRig.Contribution(definition, 1), rig.Charms(definition), definition, false);
            var state = new SceneLightingState();
            state.Sample(lighting, 4.25);
            history.SetSceneLighting(state);
            history.Add(TestTrayRig.Contribution(definition, 2), rig.Charms(definition), definition, false);
            for (int token = 0; token < history.Count; ++token)
            {
                Material material = history.GetDieRenderer(token).sharedMaterial;
                Assert.That(material.GetFloat("_SceneLightingEnabled"), Is.EqualTo(1f), "Token " + token);
                for (int i = 0; i < state.Count; ++i) AssertLight(material, state, i, rig.Stage);
            }
        }

        private static Material[] Receivers(Transform root) => root.GetComponentsInChildren<MeshRenderer>(true)
            .Select(renderer => renderer.sharedMaterial)
            .Where(material => material != null && material.HasProperty("_SceneLightingEnabled")).Distinct().ToArray();

        private static void AssertLight(Material material, SceneLightingState state, int index, Transform space)
        {
            SceneLightLayer light = state.Asset.Lights[index];
            Vector3 world = space.TransformPoint(state.GetReceiverPosition(index));
            Assert.That(material.GetVector("_SceneLightParameters" + index).x, Is.EqualTo(state.GetIntensity(index)).Within(.00001f));
            Assert.That(Vector4.Distance(material.GetVector("_SceneLightPosition" + index),
                new Vector4(world.x, world.y, world.z, light.ReceiverRange)), Is.LessThan(.0001f), material.name + " light " + index);
        }

        private static void AssertEmptySlot(Material material, int index)
        {
            Assert.That(material.GetVector("_SceneLightPosition" + index), Is.EqualTo(Vector4.zero));
            Assert.That(material.GetVector("_SceneLightColor" + index), Is.EqualTo(Vector4.zero));
            Assert.That(material.GetVector("_SceneLightParameters" + index), Is.EqualTo(Vector4.zero));
        }
    }
}
