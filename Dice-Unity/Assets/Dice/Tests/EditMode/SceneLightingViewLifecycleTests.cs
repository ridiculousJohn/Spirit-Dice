using System.Collections.Generic;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dice.Tests
{
    public sealed class SceneLightingViewLifecycleTests
    {
        private Scene original, preview;
        private bool originalDirty;
        private readonly List<UnityEngine.Object> resources = new List<UnityEngine.Object>();
        private SceneLightingAsset first, second;

        [SetUp]
        public void SetUp()
        {
            original = SceneManager.GetActiveScene();
            originalDirty = original.isDirty;
            preview = EditorSceneManager.NewPreviewScene();
            first = Lighting(resources, new Color(.1f, .12f, .14f), .35f);
            second = Lighting(resources, new Color(.12f, .08f, .04f), .8f);
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
        public void JourneyPanelsRetainPriorLightingAndReleaseItWhenCanceledOrCompleted()
        {
            var journey = UnityEngine.ScriptableObject.CreateInstance<EncounterJourneyAsset>();
            resources.Add(journey);
            TestJourneys.SetBeats(journey, TestJourneys.Beat("approach", EncounterJourneyBeatKind.Illustration),
                TestJourneys.Beat("arrival", EncounterJourneyBeatKind.Illustration));
            TestJourneys.SetBeat(journey, 0, "sceneLighting", first);
            TestJourneys.SetBeat(journey, 0, "duration", 0f);
            TestJourneys.SetBeat(journey, 1, "sceneLighting", second);
            TestJourneys.SetBeat(journey, 1, "transition", EncounterJourneyTransition.PartingPanels);
            TestJourneys.SetBeat(journey, 1, "duration", 20f);
            var controls = new TestJourneyControls { SelectedJourney = journey, JourneyPhase = EncounterJourneyPhase.Sequence };
            var prefab = AssetDatabase.LoadAssetAtPath<EncounterJourneyView>("Assets/Dice/Content/Presentation/UI/EncounterJourneyView.prefab");
            Assert.That(prefab, Is.Not.Null);
            var view = UnityEngine.Object.Instantiate(prefab);
            var root = view.gameObject;
            SceneManager.MoveGameObjectToScene(root, preview);
            root.GetComponent<RectTransform>().sizeDelta = SpiritPortraitSafeArea.DefaultReferenceSize;
            view.Build(null);
            view.Bind(controls, null);
            view.Refresh();
            view.LateUpdate();
            var background = view.Background;
            var left = view.LeftPanel;
            var right = view.RightPanel;
            Material approachMaterial = background.material;
            controls.BeatIndex = 1;
            view.Refresh();
            view.LateUpdate();
            Require(left.gameObject.activeSelf && right.gameObject.activeSelf &&
                left.material == approachMaterial && right.material == approachMaterial && background.material != approachMaterial,
                "Departing panels did not retain the previous beat's compositor independently.");
            Require(left.texture == first.BaseTexture && background.texture == second.BaseTexture &&
                left.uvRect == new Rect(0, 0, .5f, 1) && right.uvRect == new Rect(.5f, 0, .5f, 1),
                "Departing panels lost their matching texture or half of the image.");
            var currentState = view.SceneLighting;
            var previousState = view.PreviousSceneLighting;
            Require(currentState.Asset == second && previousState.Asset == first && currentState.SampleTime == previousState.SampleTime,
                "Current and departing backgrounds were not sampled from the same clock.");
            view.Bind(controls, null);
            Require(!view.IsAnimating && !left.gameObject.activeSelf && !right.gameObject.activeSelf &&
                approachMaterial == null && left.material == left.defaultMaterial && right.material == right.defaultMaterial &&
                currentState.Asset == null && previousState.Asset == null && background.material == background.defaultMaterial,
                "Rebinding the journey retained canceled panel or current lighting.");
            controls.BeatIndex = 0;
            view.Refresh();
            view.LateUpdate();
            approachMaterial = background.material;
            controls.BeatIndex = 1;
            TestJourneys.SetBeat(journey, 1, "duration", 0f);
            view.Refresh();
            view.LateUpdate();
            Require(!view.IsAnimating && !left.gameObject.activeSelf && !right.gameObject.activeSelf &&
                approachMaterial == null && view.PreviousSceneLighting.Asset == null,
                "A completed transition did not release its departing compositor.");
            root.SetActive(false);
            view.OnDisable();
            Require(view.SceneLighting.Asset == null &&
                background.material == background.defaultMaterial, "Disabling the journey retained current lighting.");
            root.SetActive(true);
            controls.BeatIndex = 0;
            view.Refresh();
            view.LateUpdate();
            Require(view.SceneLighting.Asset == first,
                "Re-enabling the journey did not bind the current beat's lighting.");
            controls.JourneyPhase = EncounterJourneyPhase.Investigation;
            view.Refresh();
            Require(view.SceneLighting.Asset == null &&
                background.material == background.defaultMaterial,
                "Returning to investigation retained sequence lighting.");
        }

        private static SceneLightingAsset Lighting(List<UnityEngine.Object> resources, Color ambient, float intensity)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var contribution = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; ++i) pixels[i] = ambient;
            texture.SetPixels(pixels); texture.Apply();
            for (int i = 0; i < pixels.Length; ++i) pixels[i] = new Color(.2f, .15f, .08f);
            contribution.SetPixels(pixels); contribution.Apply();
            var asset = UnityEngine.ScriptableObject.CreateInstance<SceneLightingAsset>();
            asset.Shader = TestShaders.Load(TestShaders.SceneLighting);
            asset.hideFlags = texture.hideFlags = contribution.hideFlags = HideFlags.HideAndDontSave;
            asset.BaseTexture = texture;
            asset.Lights = new[] { new SceneLightLayer { Contribution = contribution, Intensity = intensity, FlickerAmount = 0f } };
            resources.Add(asset); resources.Add(texture); resources.Add(contribution);
            return asset;
        }

        private static void Require(bool value, string message) => Assert.That(value, Is.True, message);
    }
}

