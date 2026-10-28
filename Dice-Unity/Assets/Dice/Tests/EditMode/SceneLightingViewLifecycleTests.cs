using System;
using System.Collections.Generic;
using System.Reflection;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
            var approach = new EncounterJourneyBeat();
            var arrival = new EncounterJourneyBeat();
            Set(approach, "sceneLighting", first);
            Set(approach, "duration", 0f);
            Set(arrival, "sceneLighting", second);
            Set(arrival, "transition", EncounterJourneyTransition.PartingPanels);
            Set(arrival, "duration", 20f);
            Set(journey, "beats", new[] { approach, arrival });
            var controls = new JourneyControls { SelectedJourney = journey, JourneyPhase = EncounterJourneyPhase.Sequence };
            var prefab = AssetDatabase.LoadAssetAtPath<EncounterJourneyView>("Assets/Dice/Content/Presentation/UI/EncounterJourneyView.prefab");
            Assert.That(prefab, Is.Not.Null);
            var view = UnityEngine.Object.Instantiate(prefab);
            var root = view.gameObject;
            SceneManager.MoveGameObjectToScene(root, preview);
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(540, 960);
            view.Build(null);
            view.Bind(controls, null);
            view.Refresh();
            Invoke(view, "LateUpdate");
            var background = Field<RawImage>(view, "background");
            var left = Field<RawImage>(view, "leftPanel");
            var right = Field<RawImage>(view, "rightPanel");
            Material approachMaterial = background.material;
            controls.BeatIndex = 1;
            view.Refresh();
            Invoke(view, "LateUpdate");
            Require(left.gameObject.activeSelf && right.gameObject.activeSelf &&
                left.material == approachMaterial && right.material == approachMaterial && background.material != approachMaterial,
                "Departing panels did not retain the previous beat's compositor independently.");
            Require(left.texture == first.BaseTexture && background.texture == second.BaseTexture &&
                left.uvRect == new Rect(0, 0, .5f, 1) && right.uvRect == new Rect(.5f, 0, .5f, 1),
                "Departing panels lost their matching texture or half of the image.");
            var currentState = Field<SceneLightingState>(view, "sceneLightingState");
            var previousState = Field<SceneLightingState>(view, "previousSceneLightingState");
            Require(currentState.Asset == second && previousState.Asset == first && currentState.SampleTime == previousState.SampleTime,
                "Current and departing backgrounds were not sampled from the same clock.");
            view.Bind(controls, null);
            Require(!view.IsAnimating && !left.gameObject.activeSelf && !right.gameObject.activeSelf &&
                approachMaterial == null && left.material == left.defaultMaterial && right.material == right.defaultMaterial &&
                currentState.Asset == null && previousState.Asset == null && background.material == background.defaultMaterial,
                "Rebinding the journey retained canceled panel or current lighting.");
            controls.BeatIndex = 0;
            view.Refresh();
            Invoke(view, "LateUpdate");
            approachMaterial = background.material;
            controls.BeatIndex = 1;
            Set(arrival, "duration", 0f);
            view.Refresh();
            Invoke(view, "LateUpdate");
            Require(!view.IsAnimating && !left.gameObject.activeSelf && !right.gameObject.activeSelf &&
                approachMaterial == null && Field<SceneLightingState>(view, "previousSceneLightingState").Asset == null,
                "A completed transition did not release its departing compositor.");
            root.SetActive(false);
            Invoke(view, "OnDisable");
            Require(Field<SceneLightingState>(view, "sceneLightingState").Asset == null &&
                background.material == background.defaultMaterial, "Disabling the journey retained current lighting.");
            root.SetActive(true);
            controls.BeatIndex = 0;
            view.Refresh();
            Invoke(view, "LateUpdate");
            Require(Field<SceneLightingState>(view, "sceneLightingState").Asset == first,
                "Re-enabling the journey did not bind the current beat's lighting.");
            controls.JourneyPhase = EncounterJourneyPhase.Investigation;
            view.Refresh();
            Require(Field<SceneLightingState>(view, "sceneLightingState").Asset == null &&
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
            asset.Shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Dice/Presentation/Shared/SceneLighting.shader");
            asset.hideFlags = texture.hideFlags = contribution.hideFlags = HideFlags.HideAndDontSave;
            asset.BaseTexture = texture;
            asset.Lights = new[] { new SceneLightLayer { Contribution = contribution, Intensity = intensity, FlickerAmount = 0f } };
            resources.Add(asset); resources.Add(texture); resources.Add(contribution);
            return asset;
        }

        private static GameObject Root(string name, Scene scene, params Type[] components)
        {
            var root = new GameObject(name, components) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        private static T Field<T>(object instance, string name)
            => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        private static void Set(object instance, string name, object value)
            => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
        private static void Invoke(object instance, string name, params object[] arguments)
            => instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, arguments);
        private static void Require(bool value, string message) => Assert.That(value, Is.True, message);

        private sealed class JourneyControls : IEncounterJourneyControls
        {
            public EncounterJourneyPhase JourneyPhase { get; set; }
            public EncounterJourneyAsset SelectedJourney { get; set; }
            public int BeatIndex { get; set; }
            public EncounterJourneyCatalogAsset JourneyCatalog => null;
            public EncounterChapterAsset SelectedChapter => null;
            public bool CanSkipIntro => false;
            public bool HasActiveEncounter => false;
            public bool CanResume => false;
            public SpiritStatus JourneyStatus => SpiritStatus.None;
            public IReadOnlyList<string> VisitedEncounterIds => Array.Empty<string>();
            public IReadOnlyList<string> HighlightedEncounterIds => Array.Empty<string>();
            public IReadOnlyList<string> CompletedStoryIds => Array.Empty<string>();
            public bool IsJourneyAvailable(string id) => true;
            public bool IsChapterAvailable(string id) => false;
            public bool IsChapterCompleted(string id) => false;
            public event Action JourneyChanged { add { } remove { } }
            public void SelectChapter(string id) { }
            public void SelectEncounter(string encounterId) { }
            public void AdvanceJourney() { }
            public void SkipIntro() { }
            public void AcceptEncounter() { }
            public void ReturnToInvestigation() { }
            public void ResumeEncounter() { }
            public void RetryEncounter() { }
        }
    }
}

