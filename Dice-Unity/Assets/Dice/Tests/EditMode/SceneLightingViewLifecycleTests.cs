using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
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

        /** Opens the authored lit table's encounter scene in place of the empty preview and returns the table. */
        private EncounterTablePresentationAsset OpenTableScene()
        {
            string tablePath = TestContent.LitTablePath();
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(tablePath);
            Assert.That(table, Is.Not.Null, tablePath);
            Assert.That(table.Scene.RuntimeKeyIsValid(), Is.True, tablePath + " must reference its encounter scene.");
            string scenePath = AssetDatabase.GUIDToAssetPath(table.Scene.AssetGUID);
            EditorSceneManager.ClosePreviewScene(preview);
            preview = EditorSceneManager.OpenPreviewScene(scenePath);
            // Opening a preview scene can unload assets held only by test locals, so the table is resolved again.
            table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(tablePath);
            Assert.That(table, Is.Not.Null, tablePath);
            return table;
        }

        [Test]
        public void TableSwitchesAndDisableReleaseSharedLightingAndOwnedMaterial()
        {
            var authoredTable = OpenTableScene();
            var view = preview.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<SpiritEncounterView>(true)).Single();
            var root = view.gameObject;
            Invoke(view, "Build");
            view.RewardsPanel.Root.gameObject.SetActive(false);
            Assert.That(view.DiceTray.RenderRoot.gameObject.scene, Is.EqualTo(preview));
            Assert.That(view.ModifierHistory.Renderer.RenderRoot.gameObject.scene, Is.EqualTo(preview));
            view.DiceTray.RenderCamera.scene = preview;
            view.ModifierHistory.Renderer.RenderCamera.scene = preview;
            view.DiceTray.RenderCamera.enabled = false;
            view.ModifierHistory.Renderer.RenderCamera.enabled = false;
            var lit = UnityEngine.Object.Instantiate(authoredTable);
            var unlit = UnityEngine.Object.Instantiate(authoredTable);
            resources.Add(lit);
            resources.Add(unlit);
            UseSyntheticLighting(lit);
            UseSyntheticLighting(unlit);
            Set(lit, "sceneLighting", first);
            Set(unlit, "roomTexture", second.BaseTexture);
            Set(unlit, "sceneLighting", null);
            var room = Field<RawImage>(view, "roomImage");
            var table = Field<RawImage>(view, "tableImage");
            ConfigureAuthoredTable(view, lit);
            Rect roomUv = room.uvRect;
            Rect tableUv = table.uvRect;
            Invoke(view, "LateUpdate");
            Material material = room.material;
            var sampled = Field<SceneLightingState>(view, "sceneLightingState");
            Require(material.shader.name == "Dice/Scene Lighting" && table.material == material,
                "The table's split images did not share their compositor.");
            Require(sampled.Asset == first && room.texture == first.BaseTexture && table.texture == first.BaseTexture,
                "The table did not bind its sampled lighting base texture.");
            Require(Field<SceneLightingState>(view.TrayScene, "sceneLighting") == sampled &&
                Field<SceneLightingState>(view.ModifierHistory.Renderer, "sceneLighting") == sampled,
                "The table did not pass one sampled state to both receiver rigs.");
            first.Lights[0].Intensity = .57f;
            Invoke(view, "LateUpdate");
            Require(Mathf.Abs(material.GetFloat("_LightIntensity0") - .57f) < .00001f,
                "An unchanged portrait size stopped the table from updating lighting.");
            Require(room.uvRect == roomUv && table.uvRect == tableUv, "Lighting altered the authored split UVs.");
            ConfigureAuthoredTable(view, unlit);
            Require(sampled.Asset == null && sampled.Count == 0 && room.material == room.defaultMaterial &&
                table.material == table.defaultMaterial && material == null,
                "Switching to an unlit table retained sampled state or its owned compositor.");
            Invoke(view, "LateUpdate");
            Require(room.texture == second.BaseTexture && table.texture == second.BaseTexture &&
                Field<SceneLightingState>(view.TrayScene, "sceneLighting") == null &&
                Field<SceneLightingState>(view.ModifierHistory.Renderer, "sceneLighting") == null,
                "An unlit table retained receiver lighting or the previous room texture.");
            Set(lit, "sceneLighting", second);
            ConfigureAuthoredTable(view, lit);
            Invoke(view, "LateUpdate");
            Require(sampled.Asset == second && room.texture == second.BaseTexture,
                "Switching back to a lit table retained the earlier asset.");
            root.SetActive(false);
            // Runtime MonoBehaviours do not receive editor lifecycle callbacks automatically.
            Invoke(view, "OnDisable");
            Require(sampled.Asset == null && room.material == room.defaultMaterial &&
                Field<SceneLightingState>(view.TrayScene, "sceneLighting") == null,
                "Disabling the table retained its lighting state.");
            root.SetActive(true);
            Invoke(view, "OnEnable");
            Invoke(view, "LateUpdate");
            Require(sampled.Asset == second, "Re-enabling the table did not restore current authored lighting.");
        }

        [Test]
        public void TablePieceLightsFollowTheirPropThroughTheSplitRoomTexture()
        {
            var authoredTable = OpenTableScene();
            var view = preview.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<SpiritEncounterView>(true)).Single();
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
            first.Lights[0].UvOffset = new Vector2(.01f, .02f);
            first.Lights[0].ReceiverPosition = new Vector3(1, 2, 3);
            var reference = new Vector2(.25f, .3f);
            var bound = UnityEngine.Object.Instantiate(authoredTable);
            var unbound = UnityEngine.Object.Instantiate(authoredTable);
            resources.Add(bound);
            resources.Add(unbound);
            UseSyntheticLighting(bound);
            UseSyntheticLighting(unbound);
            Set(bound, "sceneLighting", first);
            Set(bound, "pieceLights", new[]
            {
                new EncounterTablePieceLight
                {
                    LightIndex = 0, PieceId = "piece.test.prop", InteractionId = "position", ReferencePosition = reference,
                    ReceiverOffsetX = new Vector3(10, 0, 0), ReceiverOffsetY = new Vector3(0, 8, 0)
                }
            });
            Set(unbound, "sceneLighting", first);
            var room = Field<RawImage>(view, "roomImage");
            var table = Field<RawImage>(view, "tableImage");
            var sampled = Field<SceneLightingState>(view, "sceneLightingState");
            ConfigureAuthoredTable(view, bound);

            // A prop on the table image samples the table's slice of the room texture.
            Place(prop, table, new Vector2(.25f, .1f));
            Invoke(view, "LateUpdate");
            AssertPieceLight(sampled, table, new Vector2(.25f, .1f), reference);

            // A prop above the table samples the room image's slice.
            Place(prop, room, new Vector2(.75f, .9f));
            Assert.That(table.rectTransform.rect.Contains(table.rectTransform.InverseTransformPoint(prop.transform.position)), Is.False,
                "The room sample point must not be covered by the table image.");
            Invoke(view, "LateUpdate");
            AssertPieceLight(sampled, room, new Vector2(.75f, .9f), reference);

            prop.SetActive(false);
            Invoke(view, "LateUpdate");
            Require(sampled.GetIntensity(0) == 0f, "A hidden prop retained its light.");
            prop.SetActive(true);

            // Tables without piece lights keep every authored light position.
            ConfigureAuthoredTable(view, unbound);
            Invoke(view, "LateUpdate");
            Require(sampled.Asset == first && sampled.GetUvOffset(0) == new Vector2(.01f, .02f) &&
                sampled.GetReceiverPosition(0) == new Vector3(1, 2, 3), "An unbound light moved.");
        }

        private static void Place(GameObject prop, RawImage image, Vector2 normalized)
        {
            Rect rect = image.rectTransform.rect;
            prop.transform.position = image.rectTransform.TransformPoint(
                new Vector3(rect.xMin + rect.width * normalized.x, rect.yMin + rect.height * normalized.y, 0));
        }

        private static void AssertPieceLight(SceneLightingState sampled, RawImage image, Vector2 normalized, Vector2 reference)
        {
            Vector2 delta = image.uvRect.min + Vector2.Scale(image.uvRect.size, normalized) - reference;
            Require((sampled.GetUvOffset(0) - (new Vector2(.01f, .02f) + delta)).magnitude < .001f,
                "The light did not follow the prop: " + sampled.GetUvOffset(0));
            Require((sampled.GetReceiverPosition(0) - new Vector3(1 + 10 * delta.x, 2 + 8 * delta.y, 3)).magnitude < .01f,
                "The receiver light did not follow the prop: " + sampled.GetReceiverPosition(0));
            Require(sampled.GetIntensity(0) > 0f, "A resting prop dimmed its light.");
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

        /** A table copy lit by this fixture's synthetic lights: the authored piece lights, resting levels, ambient
            light actors and rule light flares index the authored lights, so the copy starts without them. */
        private static void UseSyntheticLighting(EncounterTablePresentationAsset table)
        {
            Set(table, "pieceLights", Array.Empty<EncounterTablePieceLight>());
            Set(table, "restingLightLevels", Array.Empty<float>());
            Set(table, "ambientActivity", null);
            Set(table, "rulePresentations", Array.Empty<EncounterRulePresentationAsset>());
        }

        private static void ConfigureAuthoredTable(SpiritEncounterView view, EncounterTablePresentationAsset table)
        {
            Set(view, "tablePresentation", table);
            // The scene authors split background textures while the view binds their sampled lighting.
            var texture = table.SceneLighting != null ? table.SceneLighting.BaseTexture : table.RoomTexture;
            view.RoomImage.texture = view.TableImage.texture = texture;
            Invoke(view, "ConfigureTable", table);
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

