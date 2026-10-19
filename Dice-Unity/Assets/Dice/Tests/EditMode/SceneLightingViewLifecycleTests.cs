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
        public void TableSwitchesAndDisableReleaseSharedLightingAndOwnedMaterial()
        {
            var root = Root("Table lighting lifecycle", preview, typeof(RectTransform));
            var view = root.AddComponent<SpiritEncounterView>();
            Set(view, "uiTheme", AssetDatabase.LoadAssetAtPath<SpiritUiTheme>(
                "Assets/Dice/Presentation/Shared/SpiritHouseTheme.asset"));
            Set(view, "trayPresentation", AssetDatabase.LoadAssetAtPath<SpiritTrayPresentationAsset>(
                "Assets/Dice/Content/Presentation/SpiritHouse/LanternTrayPresentation.asset"));
            Invoke(view, "Build");
            view.RewardsPanel.Root.gameObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(view.DiceTray.RenderRoot.gameObject, preview);
            SceneManager.MoveGameObjectToScene(view.ModifierHistory.Renderer.RenderRoot.gameObject, preview);
            view.DiceTray.RenderCamera.scene = preview;
            view.ModifierHistory.Renderer.RenderCamera.scene = preview;
            view.DiceTray.RenderCamera.enabled = false;
            view.ModifierHistory.Renderer.RenderCamera.enabled = false;
            var lit = UnityEngine.ScriptableObject.CreateInstance<EncounterTablePresentationAsset>();
            var unlit = UnityEngine.ScriptableObject.CreateInstance<EncounterTablePresentationAsset>();
            resources.Add(lit);
            resources.Add(unlit);
            Set(lit, "sceneLighting", first);
            Set(unlit, "roomTexture", second.BaseTexture);
            var room = Field<RawImage>(view, "roomImage");
            var table = Field<RawImage>(view, "tableImage");
            Invoke(view, "ConfigureTable", lit);
            Rect roomUv = room.uvRect;
            Rect tableUv = table.uvRect;
            Invoke(view, "LateUpdate");
            Material material = room.material;
            var sampled = Field<SceneLightingState>(view, "sceneLightingState");
            Require(material.shader.name == "Dice/Scene Lighting" && table.material == material,
                "The table's split images did not share their compositor.");
            Require(sampled.Asset == first && room.texture == first.BaseTexture && table.texture == first.BaseTexture,
                "The table did not bind its sampled lighting base texture.");
            Require(Field<SceneLightingState>(view.DiceTray, "sceneLighting") == sampled &&
                Field<SceneLightingState>(view.ModifierHistory.Renderer, "sceneLighting") == sampled,
                "The table did not pass one sampled state to both receiver rigs.");
            first.Lights[0].Intensity = .57f;
            Invoke(view, "LateUpdate");
            Require(Mathf.Abs(material.GetFloat("_LightIntensity0") - .57f) < .00001f,
                "An unchanged portrait size stopped the table from updating lighting.");
            Require(room.uvRect == roomUv && table.uvRect == tableUv, "Lighting altered the authored split UVs.");
            Invoke(view, "ConfigureTable", unlit);
            Require(sampled.Asset == null && sampled.Count == 0 && room.material == room.defaultMaterial &&
                table.material == table.defaultMaterial && material == null,
                "Switching to an unlit table retained sampled state or its owned compositor.");
            Invoke(view, "LateUpdate");
            Require(room.texture == second.BaseTexture && table.texture == second.BaseTexture &&
                Field<SceneLightingState>(view.DiceTray, "sceneLighting") == null &&
                Field<SceneLightingState>(view.ModifierHistory.Renderer, "sceneLighting") == null,
                "An unlit table retained receiver lighting or the previous room texture.");
            Set(lit, "sceneLighting", second);
            Invoke(view, "ConfigureTable", lit);
            Invoke(view, "LateUpdate");
            Require(sampled.Asset == second && room.texture == second.BaseTexture,
                "Switching back to a lit table retained the earlier asset.");
            root.SetActive(false);
            // Runtime MonoBehaviours do not receive editor lifecycle callbacks automatically.
            Invoke(view, "OnDisable");
            Require(sampled.Asset == null && room.material == room.defaultMaterial &&
                Field<SceneLightingState>(view.DiceTray, "sceneLighting") == null,
                "Disabling the table retained its lighting state.");
            root.SetActive(true);
            Invoke(view, "OnEnable");
            Invoke(view, "LateUpdate");
            Require(sampled.Asset == second, "Re-enabling the table did not restore current authored lighting.");
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
            var root = Root("Journey lighting lifecycle", preview, typeof(RectTransform));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(540, 960);
            var view = root.AddComponent<EncounterJourneyView>();
            root.AddComponent<SpiritUiContext>().Configure(AssetDatabase.LoadAssetAtPath<SpiritUiTheme>(
                "Assets/Dice/Presentation/Shared/SpiritHouseTheme.asset"));
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
            public bool CanSkipIntro => false;
            public bool HasActiveEncounter => false;
            public bool CanResume => false;
            public string JourneyStatus => "";
            public IReadOnlyList<string> VisitedEncounterIds => Array.Empty<string>();
            public IReadOnlyList<string> HighlightedEncounterIds => Array.Empty<string>();
            public IReadOnlyList<string> CompletedStoryIds => Array.Empty<string>();
            public bool IsJourneyAvailable(string id) => true;
            public event Action JourneyChanged { add { } remove { } }
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
