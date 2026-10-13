using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dice.Tests
{
    public sealed class SceneLightingReceiverIntegrationTests
    {
        private Scene scene;
        private readonly List<UnityEngine.Object> resources = new List<UnityEngine.Object>();
        private SpiritDiceTray tray;
        private SpiritModifierHistoryRenderer history;
        private SpiritHostPortrait host;
        private SceneLightingAsset lighting;
        private SceneLightingMaterial compositor;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            tray = Root("Lighting dice").AddComponent<SpiritDiceTray>();
            tray.Initialize();
            MoveRenderer(tray.RenderRoot, tray.RenderCamera);
            history = Root("Lighting history").AddComponent<SpiritModifierHistoryRenderer>();
            history.Initialize();
            MoveRenderer(history.RenderRoot, history.RenderCamera);
            AddHistory();
            var parent = Root("Lighting host", typeof(RectTransform)).GetComponent<RectTransform>();
            parent.sizeDelta = new Vector2(128, 128);
            var presentation = Own(ScriptableObject.CreateInstance<SpiritHostPresentationAsset>());
            presentation.ReferenceSize = parent.sizeDelta;
            presentation.Layers = new[]
            {
                new SpiritPortraitLayer { Id = "body", Texture = Texture2D.whiteTexture, Size = new Vector2(128, 128) },
                new SpiritPortraitLayer { Id = "glow", Texture = Texture2D.whiteTexture, Size = new Vector2(16, 16),
                    Tint = new Color(.2f, .4f, .8f, .42f), ReceiveSceneLighting = false }
            };
            host = presentation.Create(parent);
            lighting = Own(ScriptableObject.CreateInstance<SceneLightingAsset>());
            lighting.BaseTexture = Texture2D.whiteTexture;
            lighting.ReceiverAmbient = new Color(.15f, .15f, .15f);
            lighting.ReceiverKeyColor = Color.black;
            lighting.Lights = new[]
            {
                new SceneLightLayer { Contribution = Texture2D.whiteTexture, FlickerAmount = .45f, Seed = 41,
                    ReceiverPosition = new Vector3(-2, 4, -3), ReceiverColor = Color.red, ReceiverIntensity = 4f, ReceiverRange = 15f },
                new SceneLightLayer { Contribution = Texture2D.whiteTexture, FlickerAmount = .35f, Seed = 97,
                    ReceiverPosition = new Vector3(2, 4, -3), ReceiverColor = Color.green, ReceiverIntensity = 4f, ReceiverRange = 15f }
            };
            compositor = new SceneLightingMaterial();
        }

        [TearDown]
        public void TearDown()
        {
            compositor?.Dispose();
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            foreach (var resource in resources) if (resource != null) UnityEngine.Object.DestroyImmediate(resource);
            resources.Clear();
        }

        [Test]
        public void CompositorHostDiceAndHistoryUseOneSampleAndRigLocalLightCoordinates()
        {
            var state = new SceneLightingState();
            bool distinctLevels = false;
            for (int sample = 0; sample < 12; ++sample)
            {
                state.Sample(lighting, sample * .419);
                compositor.Apply(state);
                Apply(state);
                Assert.That(Field<SceneLightingState>(tray, "sceneLighting"), Is.SameAs(state));
                Assert.That(Field<SceneLightingState>(history, "sceneLighting"), Is.SameAs(state));
                distinctLevels |= Mathf.Abs(state.GetIntensity(0) - state.GetIntensity(1)) > .002f;
                for (int i = 0; i < state.Count; ++i)
                {
                    Assert.That(compositor.Material.GetFloat("_LightIntensity" + i), Is.EqualTo(state.GetIntensity(i)));
                    CheckRig(tray.RenderRoot, state, i);
                    CheckRig(history.RenderRoot, state, i);
                }
                Color expected = state.EvaluateReceiver(Vector3.zero, Vector3.back);
                Color actual = host.GetLayer("body").color.linear;
                Assert.That(actual.r, Is.EqualTo(expected.r).Within(.001f));
                Assert.That(actual.g, Is.EqualTo(expected.g).Within(.001f));
                Assert.That(actual.b, Is.EqualTo(expected.b).Within(.001f));
                Assert.That(host.GetLayer("glow").color, Is.EqualTo(new Color(.2f, .4f, .8f, .42f)));
            }
            Assert.That(distinctLevels, Is.True, "The two authored seeds should produce independent light levels.");
        }

        [Test]
        public void RemovingLightsClearsSlotsAndNewHistoryKeepsTheCurrentSampleAcrossReenable()
        {
            var state = new SceneLightingState();
            state.Sample(lighting, 12.375);
            Apply(state);
            Material oldDie = history.GetDieRenderer(0).sharedMaterial;
            history.Clear();
            Assert.That(oldDie == null, Is.True, "Clearing entries must release their owned material.");
            AddHistory();
            Material arrived = history.GetDieRenderer(0).sharedMaterial;
            Assert.That(arrived.GetVector("_SceneLightParameters0").x, Is.EqualTo(state.GetIntensity(0)));
            history.gameObject.SetActive(false);
            Invoke(history, "OnDisable");
            Assert.That(history.RenderRoot.gameObject.activeSelf, Is.False);
            history.gameObject.SetActive(true);
            Invoke(history, "OnEnable");
            Assert.That(history.RenderRoot.gameObject.activeSelf, Is.True);
            Assert.That(arrived.GetVector("_SceneLightParameters1").x, Is.EqualTo(state.GetIntensity(1)));
            lighting.Lights = new[] { lighting.Lights[0] };
            state.Sample(lighting, 13);
            compositor.Apply(state);
            Apply(state);
            foreach (Material material in Materials())
                for (int i = 1; i < SceneLightingAsset.MaximumLights; ++i) AssertEmptySlot(material, i);
            for (int i = 1; i < SceneLightingAsset.MaximumLights; ++i)
                Assert.That(compositor.Material.GetFloat("_LightIntensity" + i), Is.Zero);
            state.Sample(null, 14);
            Apply(state);
            foreach (Material material in Materials())
            {
                Assert.That(material.GetFloat("_SceneLightingEnabled"), Is.Zero);
                Assert.That(material.GetVector("_SceneLightingAmbient"), Is.EqualTo(Vector4.zero));
                Assert.That(material.GetVector("_SceneLightingKeyColor"), Is.EqualTo(Vector4.zero));
                for (int i = 0; i < SceneLightingAsset.MaximumLights; ++i) AssertEmptySlot(material, i);
            }
            Assert.That(host.GetLayer("body").color, Is.EqualTo(Color.white));
            history.Clear(); AddHistory();
            Assert.That(history.GetDieRenderer(0).sharedMaterial.GetFloat("_SceneLightingEnabled"), Is.Zero);
        }

        private void Apply(SceneLightingState state)
        {
            tray.SetSceneLighting(state);
            history.SetSceneLighting(state);
            host.SetSceneLighting(state, Vector3.zero);
        }

        private static void CheckRig(Transform root, SceneLightingState state, int index)
        {
            foreach (Material material in RigMaterials(root))
            {
                Assert.That(material.GetFloat("_SceneLightingEnabled"), Is.EqualTo(1f));
                Assert.That(material.GetVector("_SceneLightParameters" + index).x, Is.EqualTo(state.GetIntensity(index)));
                SceneLightLayer light = state.Asset.Lights[index];
                Vector3 world = root.TransformPoint(light.ReceiverPosition);
                Assert.That(material.GetVector("_SceneLightPosition" + index),
                    Is.EqualTo(new Vector4(world.x, world.y, world.z, light.ReceiverRange)));
            }
        }

        private static void AssertEmptySlot(Material material, int index)
        {
            Assert.That(material.GetVector("_SceneLightPosition" + index), Is.EqualTo(Vector4.zero));
            Assert.That(material.GetVector("_SceneLightColor" + index), Is.EqualTo(Vector4.zero));
            Assert.That(material.GetVector("_SceneLightParameters" + index), Is.EqualTo(Vector4.zero));
        }

        private IEnumerable<Material> Materials() => RigMaterials(tray.RenderRoot).Concat(RigMaterials(history.RenderRoot));
        private static Material[] RigMaterials(Transform root)
        {
            Material[] result = root.GetComponentsInChildren<MeshRenderer>(true).Select(renderer => renderer.sharedMaterial)
                .Where(material => material != null && material.HasProperty("_SceneLightingEnabled")
                    && material.GetFloat("_SurfaceKind") != 5f && material.GetFloat("_SurfaceKind") != 6f).Distinct().ToArray();
            Assert.That(result, Is.Not.Empty);
            return result;
        }

        private void AddHistory()
        {
            var definition = new ModifierDefinition("lighting.test", "lighting", ModifierPhase.PlayHand,
                new ModifierCondition(ModifierConditionKind.Always), new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 1) });
            var contribution = (ModifierContribution)Activator.CreateInstance(typeof(ModifierContribution),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 1L, definition, 1L, 0L, 0 }, null);
            history.Add(contribution, definition, false);
        }

        private void MoveRenderer(Transform root, Camera camera)
        {
            SceneManager.MoveGameObjectToScene(root.gameObject, scene);
            camera.scene = scene;
            camera.enabled = false;
        }

        private GameObject Root(string name, params Type[] components)
        {
            var result = new GameObject(name, components) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(result, scene);
            return result;
        }

        private T Own<T>(T resource) where T : UnityEngine.Object { resources.Add(resource); return resource; }
        private static T Field<T>(object instance, string name)
            => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        private static void Invoke(object instance, string name)
            => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);
    }
}
