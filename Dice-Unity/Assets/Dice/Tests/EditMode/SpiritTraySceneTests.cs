using System;
using System.Collections.Generic;
using System.Linq;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class SpiritTraySceneTests
    {
        private Scene preview;
        private TestTrayRig rig;
        private readonly List<UnityEngine.Object> resources = new List<UnityEngine.Object>();

        private TestTrayRig Rig => rig ?? (rig = new TestTrayRig(preview));

        [SetUp]
        public void SetUp() => preview = EditorSceneManager.NewPreviewScene();

        [TearDown]
        public void TearDown()
        {
            rig?.Dispose();
            rig = null;
            foreach (var root in preview.GetRootGameObjects())
                foreach (var scene in root.GetComponentsInChildren<SpiritTrayScene>(true)) scene.ReleasePresentation();
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            foreach (UnityEngine.Object resource in resources)
                if (resource != null) UnityEngine.Object.DestroyImmediate(resource);
            resources.Clear();
        }

        /** Checks the dice and modifier bays render through the scene's one camera and texture / fails if a consumer renders through its own or the texture ignores the presentation size. */
        [Test]
        public void ConsumersRenderThroughTheSceneCameraAndTexture()
        {
            var dice = Rig.StartDice();
            var history = Rig.StartHistory();
            var scene = Rig.TrayScene;
            Assert.That(scene.RenderCamera, Is.SameAs(Rig.Camera));
            Assert.That(dice.RenderCamera, Is.SameAs(scene.RenderCamera));
            Assert.That(history.RenderCamera, Is.SameAs(scene.RenderCamera));
            Assert.That(dice.Texture, Is.SameAs(scene.Texture));
            Assert.That(history.Texture, Is.SameAs(scene.Texture));
            Assert.That(Rig.Camera.targetTexture, Is.SameAs(scene.Texture));
            Assert.That(new Vector2Int(scene.Texture.width, scene.Texture.height), Is.EqualTo(Rig.Presentation.TextureSize));
        }

        /** Checks initialization renders into a new texture with the authored target's sampling and release restores the target / fails if the authored target is drawn into, its sampling is lost or it is not restored. */
        [Test]
        public void InitializationCopiesTheAuthoredTargetAndReleaseRestoresIt()
        {
            var target = new RenderTexture(64, 32, 24)
            {
                filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Mirror, wrapModeW = TextureWrapMode.Clamp, anisoLevel = 3
            };
            resources.Add(target);
            Rig.Camera.targetTexture = target;
            var scene = Rig.Initialize();
            Assert.That(scene.Texture, Is.Not.SameAs(target));
            Assert.That(Rig.Camera.targetTexture, Is.SameAs(scene.Texture));
            Assert.That(scene.Texture.filterMode, Is.EqualTo(target.filterMode));
            Assert.That(scene.Texture.wrapModeU, Is.EqualTo(target.wrapModeU));
            Assert.That(scene.Texture.wrapModeV, Is.EqualTo(target.wrapModeV));
            Assert.That(scene.Texture.wrapModeW, Is.EqualTo(target.wrapModeW));
            Assert.That(scene.Texture.anisoLevel, Is.EqualTo(target.anisoLevel));
            scene.ReleasePresentation();
            Assert.That(Rig.Camera.targetTexture, Is.SameAs(target));
            Assert.That(scene.Texture, Is.Null);
        }

        /** Checks the stage draws instances of its materials, so lighting never reaches the authored ones, and release restores them / fails if lighting writes to an authored material or release leaves instances behind. */
        [Test]
        public void StageDrawsMaterialInstancesAndReleaseRestoresTheAuthoredOnes()
        {
            var scene = Rig.Initialize();
            Material instance = Rig.SurfaceRenderer.sharedMaterial;
            Assert.That(instance, Is.Not.SameAs(Rig.SurfaceMaterial));
            var lighting = ScriptableObject.CreateInstance<SceneLightingAsset>();
            resources.Add(lighting);
            lighting.Shader = TestShaders.Load(TestShaders.SceneLighting);
            lighting.BaseTexture = Texture2D.whiteTexture;
            lighting.Lights = new SceneLightLayer[0];
            var state = new SceneLightingState();
            state.Sample(lighting, 1);
            scene.SetSceneLighting(state);
            Assert.That(instance.GetFloat("_SceneLightingEnabled"), Is.EqualTo(1f));
            Assert.That(Rig.SurfaceMaterial.GetFloat("_SceneLightingEnabled"), Is.Zero);
            scene.ReleasePresentation();
            Assert.That(Rig.SurfaceRenderer.sharedMaterial, Is.SameAs(Rig.SurfaceMaterial));
            Assert.That(instance == null, Is.True, "Release destroys the material instance.");
        }

        /** Checks initialization keeps the authored camera pose, geometry pose and hierarchy / fails if binding reframes the camera, moves the geometry or adds objects. */
        [Test]
        public void InitializationKeepsTheAuthoredCameraGeometryAndHierarchy()
        {
            Rig.Camera.transform.localPosition = new Vector3(2, 11, -13);
            Rig.Camera.transform.localRotation = Quaternion.Euler(37, 14, 9);
            Rig.Geometry.transform.localPosition = new Vector3(.3f, -.2f, .4f);
            Vector3 position = Rig.Camera.transform.localPosition;
            Quaternion rotation = Rig.Camera.transform.localRotation;
            Vector3 geometry = Rig.Geometry.transform.localPosition;
            int count = Rig.TrayScene.GetComponentsInChildren<Transform>(true).Length;
            Rig.Initialize();
            Assert.That(Rig.Camera.transform.localPosition, Is.EqualTo(position));
            Assert.That(Rig.Camera.transform.localRotation, Is.EqualTo(rotation));
            Assert.That(Rig.Geometry.transform.localPosition, Is.EqualTo(geometry));
            Assert.That(Rig.TrayScene.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
        }

        /** Checks a texture size change resizes the shared texture in place and keeps the camera's framing / fails if it replaces the camera or texture, leaves a stale aspect or reframes the camera. */
        [Test]
        public void TextureResizeKeepsTheCameraTextureAndFraming()
        {
            var scene = Rig.Initialize();
            Camera camera = scene.RenderCamera;
            RenderTexture texture = scene.Texture;
            Vector3 position = camera.transform.localPosition;
            float size = camera.orthographicSize;
            float near = camera.nearClipPlane;
            float far = camera.farClipPlane;
            Rig.Presentation.TextureSize = new Vector2Int(96, 96);
            scene.ApplyPresentationSettings();
            Assert.That(scene.RenderCamera, Is.SameAs(camera));
            Assert.That(scene.Texture, Is.SameAs(texture));
            Assert.That(camera.targetTexture, Is.SameAs(texture));
            Assert.That(texture.IsCreated(), Is.True);
            Assert.That(new Vector2Int(texture.width, texture.height), Is.EqualTo(new Vector2Int(96, 96)));
            Assert.That(camera.aspect, Is.EqualTo(1f).Within(.0001f));
            Assert.That(camera.transform.localPosition, Is.EqualTo(position));
            Assert.That(camera.orthographicSize, Is.EqualTo(size));
            Assert.That(camera.nearClipPlane, Is.EqualTo(near));
            Assert.That(camera.farClipPlane, Is.EqualTo(far));
        }

        /** Checks reconfiguring the scene with another presentation replaces only its texture / fails if it rebuilds the stage or camera or leaks the previous texture. */
        [Test]
        public void ReconfiguringTheSceneReplacesOnlyItsTexture()
        {
            var scene = Rig.Initialize();
            Transform root = scene.RenderRoot;
            Camera camera = scene.RenderCamera;
            RenderTexture previous = scene.Texture;
            var second = UnityEngine.Object.Instantiate(Rig.Presentation);
            resources.Add(second);
            scene.Configure(second);
            scene.Initialize();
            Assert.That(scene.Presentation, Is.SameAs(second));
            Assert.That(scene.RenderRoot, Is.SameAs(root));
            Assert.That(scene.RenderCamera, Is.SameAs(camera));
            Assert.That(previous == null, Is.True, "The previous texture is destroyed.");
            Assert.That(scene.Texture != null && scene.Texture.IsCreated(), Is.True);
        }

        /** Checks only the scene's own disable hides the shared stage while consumers hide just their content / fails if a consumer's disable or destruction hides the stage or releases its texture. */
        [Test]
        public void OnlyTheSceneHidesTheSharedStage()
        {
            var dice = Rig.StartDice();
            var history = Rig.StartHistory();
            var scene = Rig.TrayScene;
            var definition = TestTrayRig.Definition("test.tray.consumer");
            history.Add(TestTrayRig.Contribution(definition), Rig.Charms(definition), definition, false);
            Transform token = history.GetDieTransform(0);
            scene.OnEnable();
            history.OnDisable();
            Assert.That(token.gameObject.activeInHierarchy, Is.False, "Disabling the modifier renderer hides its tokens.");
            Assert.That(Rig.Stage.gameObject.activeSelf, Is.True);
            dice.OnDisable();
            Assert.That(Rig.Stage.gameObject.activeSelf, Is.True);
            RenderTexture texture = scene.Texture;
            UnityEngine.Object.DestroyImmediate(dice.gameObject);
            Assert.That(texture != null && texture.IsCreated() && scene.RenderCamera != null, Is.True);
            scene.OnDisable();
            Assert.That(Rig.Stage.gameObject.activeSelf, Is.False);
            scene.OnEnable();
            Assert.That(Rig.Stage.gameObject.activeSelf, Is.True);
        }

        /** Checks HUD animations and reset return labels to the scales captured at the first build / fails if rebinding captures a scale left by an interrupted pulse. */
        [Test]
        public void HudAnimationsAndResetReturnToTheScalesCapturedAtFirstBuild()
        {
            var hud = CreateHud(out RectTransform[] scaled);
            var authored = scaled.Select((rect, index) => rect.localScale = new Vector3(.7f + index * .03f, 1.4f + index * .02f, .9f))
                .ToArray();
            hud.BuildPlayingHud(() => { }, () => { });
            foreach (RectTransform rect in scaled) rect.localScale = Vector3.one * 2;
            hud.BuildPlayingHud(() => { }, () => { });
            Assert.That(hud.AnimateScoringStep(new SpiritScoringStep { RevealsTotal = true, Total = 100 }, 0).MoveNext(), Is.False);
            RectTransform total = scaled.Single(rect => rect.name == "HandScoreLabel");
            Assert.That(total.localScale, Is.EqualTo(authored[Array.IndexOf(scaled, total)]));
            hud.ResetTransientVisuals();
            for (int i = 0; i < scaled.Length; ++i) Assert.That(scaled[i].localScale, Is.EqualTo(authored[i]), scaled[i].name);
        }

        /** Checks each token draws its own copy of the token prefab's body material with its surface texture / fails if tokens share or modify the prefab's material. */
        [Test]
        public void EachTokenDrawsItsOwnCopyOfTheTokenMaterial()
        {
            var history = Rig.StartHistory();
            var definition = TestTrayRig.Definition("test.tray.material");
            history.Add(TestTrayRig.Contribution(definition), Rig.Charms(definition), definition, false);
            history.Add(TestTrayRig.Contribution(definition, 2), Rig.Charms(definition), definition, false);
            Material first = history.GetDieRenderer(0).sharedMaterial;
            Material second = history.GetDieRenderer(1).sharedMaterial;
            Assert.That(first, Is.Not.SameAs(Rig.TokenMaterial));
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(first.GetTexture("_SurfaceTexture"), Is.SameAs(Rig.TokenSurface));
            Assert.That(first.GetFloat("_ShowFaceIcon"), Is.EqualTo(1f));
            Assert.That(Rig.TokenMaterial.GetFloat("_ShowFaceIcon"), Is.Zero, "Binding a token leaves the prefab's material unchanged.");
        }

        /** Checks a token's scale follows the bay's die size relative to the prefab's / fails if tokens ignore the bay size or resize the prefab. */
        [Test]
        public void TokensScaleWithTheBayDieSize()
        {
            Rig.Geometry.ModifierDieSize = .63f;
            var history = Rig.StartHistory();
            Vector3 body = Rig.Token.Body.transform.localScale;
            var definition = TestTrayRig.Definition("test.tray.size");
            history.Add(TestTrayRig.Contribution(definition), Rig.Charms(definition), definition, false);
            AssertVector(history.GetDieTransform(0).localScale, body * (.63f / SpiritModifierHistoryRenderer.DieSize));
            Assert.That(Rig.Token.Body.transform.localScale, Is.EqualTo(body));
        }

        /** Checks a resting token touches the bay while a lifted one rises and spreads its shadow in the prefab's proportions / fails if tokens float or sink at rest, or the shadow ignores lift or distorts. */
        [Test]
        public void RestingTokensTouchTheBayAndLiftedTokensSpreadTheirShadow()
        {
            var history = Rig.StartHistory();
            Vector3 shadow = Rig.Token.Shadow.transform.localScale;
            var definition = TestTrayRig.Definition("test.tray.contact");
            history.Add(TestTrayRig.Contribution(definition), Rig.Charms(definition), definition, false);
            Assert.That(Gap(), Is.EqualTo(0f).Within(.00001f));
            Vector3 resting = ShadowScale();
            history.SetResolving(0);
            Assert.That(Gap(), Is.GreaterThan(0f));
            Assert.That(ShadowScale().x, Is.GreaterThan(resting.x));

            // Height of the token's lowest point above the bay surface.
            float Gap()
            {
                Transform die = history.GetDieTransform(0);
                float bottom = die.GetComponent<MeshFilter>().sharedMesh.vertices
                    .Min(point => (die.localRotation * Vector3.Scale(point, die.localScale)).y);
                return die.localPosition.y + bottom;
            }

            // The shadow keeps the prefab's thickness and footprint proportions at any lift.
            Vector3 ShadowScale()
            {
                Vector3 scale = history.GetShadowRenderer(0).transform.localScale;
                Assert.That(scale.y, Is.EqualTo(shadow.y).Within(.00001f));
                Assert.That(scale.x / scale.z, Is.EqualTo(shadow.x / shadow.z).Within(.00001f));
                return scale;
            }
        }

        /** Checks a categorized token shows its category glyph and ink over the untinted bone while an uncategorized one hides category marks and keeps its icon / fails if category ink tints the bone, the glyph is missing or uncategorized tokens show category marks. */
        [Test]
        public void CategorizedTokensShowTheirGlyphAndInkWithoutTintingTheBone()
        {
            Color ink = new Color(.2f, .45f, .7f, 1f);
            var categorized = TestTrayRig.Definition("test.tray.categorized", effect: new AddMultiplierEffect(1));
            var categorizedCharms = Rig.Synthetic.For(Rig.Synthetic.Category(Rig.Synthetic.Icon(), ink: ink), categorized);
            var plain = TestTrayRig.Definition("test.tray.uncategorized");
            var plainCharms = Rig.Synthetic.For(Rig.Synthetic.Category(), plain);
            var history = Rig.StartHistory();
            history.Add(TestTrayRig.Contribution(categorized), categorizedCharms, categorized, false);
            history.Add(TestTrayRig.Contribution(plain, 2), plainCharms, plain, false);
            Material marked = history.GetDieRenderer(0).sharedMaterial;
            Material unmarked = history.GetDieRenderer(1).sharedMaterial;
            Sprite glyph = categorizedCharms.GetCategorySprite(categorized);
            Assert.That(glyph, Is.Not.Null);
            Assert.That(marked.GetFloat("_ShowCategory"), Is.EqualTo(1f));
            Assert.That(marked.GetTexture("_CategoryIcon"), Is.SameAs(glyph.texture));
            AssertColor(marked.GetColor("_CategoryColor"), ink);
            AssertColor(marked.GetColor("_FaceIconColor"), ink);
            AssertColor(marked.GetColor("_BaseColor"), Rig.TokenMaterial.GetColor("_BaseColor"));
            Assert.That(unmarked.GetFloat("_ShowCategory"), Is.Zero);
            Assert.That(unmarked.GetFloat("_ShowFaceIcon"), Is.EqualTo(1f));
            Assert.That(unmarked.GetTexture("_FaceIcon"), Is.Not.Null);
        }

        /** Checks scrolling moves tokens along the bay and clips those outside it / fails if overflow tokens stay visible or scrolling does not reveal them. */
        [Test]
        public void ScrollingRevealsTokensAndClipsThoseOutsideTheBay()
        {
            var history = Rig.StartHistory();
            var definition = TestTrayRig.Definition("test.tray.scroll");
            int last = history.Capacity + 4;
            for (int i = 0; i <= last; ++i) history.Add(TestTrayRig.Contribution(definition, i + 1), Rig.Charms(definition), definition, false);
            history.SetScroll(0);
            Assert.That(history.GetDieViewportRect(0).width, Is.GreaterThan(0));
            Assert.That(history.GetDieViewportRect(last), Is.EqualTo(Rect.zero));
            history.SetScroll(1);
            Assert.That(history.GetDieViewportRect(0), Is.EqualTo(Rect.zero));
            Assert.That(history.GetDieViewportRect(last).width, Is.GreaterThan(0));
        }

        /** Checks token materials clip to the bay's width in the bay's own coordinates / fails if a rotated bay clips in world or stage coordinates. */
        [Test]
        public void TokenMaterialsClipToTheBayInItsLocalSpace()
        {
            Rig.Geometry.ModifierRoot.localRotation = Quaternion.Euler(0, 27, 0);
            var history = Rig.StartHistory();
            var definition = TestTrayRig.Definition("test.tray.clip");
            history.Add(TestTrayRig.Contribution(definition), Rig.Charms(definition), definition, false);
            Material material = history.GetDieRenderer(0).sharedMaterial;
            Vector4 clip = material.GetVector("_ClipLocalX");
            Assert.That(clip.x, Is.EqualTo(-Rig.Geometry.ModifierWidth * .5f));
            Assert.That(clip.y, Is.EqualTo(Rig.Geometry.ModifierWidth * .5f));
            Vector3 local = new Vector3(.23f, .4f, -.1f);
            Vector3 world = Rig.Geometry.ModifierRoot.TransformPoint(local);
            AssertVector(material.GetMatrix("_ClipWorldToLocal").MultiplyPoint3x4(world), local, .001f);
        }

        /** Checks the resolving token alone lifts and takes the highlight colour and clearing restores it / fails if other tokens change or clearing leaves the token raised or tinted. */
        [Test]
        public void ResolvingLiftsAndTintsOnlyThatToken()
        {
            var history = Rig.StartHistory();
            var definition = TestTrayRig.Definition("test.tray.resolving");
            for (int i = 0; i < 3; ++i) history.Add(TestTrayRig.Contribution(definition, i + 1), Rig.Charms(definition), definition, false);
            Color bone = history.GetDieRenderer(0).sharedMaterial.GetColor("_BaseColor");
            Vector3 first = history.GetDieTransform(0).localPosition;
            Vector3 resting = history.GetDieTransform(1).localPosition;
            history.SetResolving(1);
            Assert.That(history.ResolvingIndex, Is.EqualTo(1));
            Assert.That(history.GetDieTransform(1).localPosition.y, Is.GreaterThan(resting.y));
            Assert.That(history.GetDieRenderer(1).sharedMaterial.GetColor("_BaseColor"), Is.Not.EqualTo(bone));
            AssertVector(history.GetDieTransform(0).localPosition, first);
            AssertColor(history.GetDieRenderer(0).sharedMaterial.GetColor("_BaseColor"), bone);
            history.SetResolving(-1);
            AssertVector(history.GetDieTransform(1).localPosition, resting);
            AssertColor(history.GetDieRenderer(1).sharedMaterial.GetColor("_BaseColor"), bone);
        }

        /** Checks clearing the modifier renderer destroys its tokens and their materials / fails if cleared tokens or materials leak. */
        [Test]
        public void ClearingDestroysTokensAndTheirMaterials()
        {
            var history = Rig.StartHistory();
            var definition = TestTrayRig.Definition("test.tray.clear");
            history.Add(TestTrayRig.Contribution(definition), Rig.Charms(definition), definition, false);
            Transform token = history.GetDieTransform(0);
            Material material = history.GetDieRenderer(0).sharedMaterial;
            Material shadow = history.GetShadowRenderer(0).sharedMaterial;
            history.Clear();
            Assert.That(history.Count, Is.Zero);
            Assert.That(token == null && material == null && shadow == null, Is.True);
        }

        /** Checks pointer targets sit over their projected tokens and hide for clipped ones / fails if a target misses its token or a clipped token stays tappable. */
        [Test]
        public void PointerTargetsFollowProjectedTokensAndHideClippedOnes()
        {
            var tray = Rig.CreateHistoryTray();
            var definition = TestTrayRig.Definition("test.tray.pointer");
            int last = tray.Renderer.Capacity + 4;
            for (int i = 0; i <= last; ++i) tray.Add(TestTrayRig.Contribution(definition, i + 1), Rig.Charms(definition), definition, false);
            tray.LateUpdate();
            var root = (RectTransform)tray.transform;
            var visible = (RectTransform)tray.Content.GetChild(0);
            Rect projected = tray.Renderer.GetDieViewportRect(0);
            Assert.That(visible.gameObject.activeSelf, Is.True);
            Vector3 expected = root.TransformPoint(root.rect.min + Vector2.Scale(projected.center, root.rect.size));
            Assert.That(Vector3.Distance(visible.position, expected), Is.LessThan(.01f));
            Assert.That(Vector2.Distance(visible.sizeDelta, Vector2.Scale(projected.size, root.rect.size)), Is.LessThan(.01f));
            Assert.That(tray.Content.GetChild(last).gameObject.activeSelf, Is.False);
        }

        /** Checks ready tokens show only PlayHand contributions, including ones granting only rerolls / fails if roll-phase contributions appear or resource-only ones are dropped. */
        [Test]
        public void ReadyTokensShowOnlyPlayHandContributions()
        {
            var tray = Rig.CreateHistoryTray();
            var catalog = ReadyCatalog(out ModifierDefinition roll, out ModifierDefinition score, out ModifierDefinition grant);
            var charms = Rig.Synthetic.For(catalog);
            var contributions = new[]
            {
                TestTrayRig.Contribution(roll, 1), TestTrayRig.Contribution(score, 2, 0, 2),
                TestTrayRig.Contribution(grant, 3, 0, 0, 2)
            };
            tray.SynchronizeReady(contributions, catalog, charms);
            Assert.That(tray.Contributions.Select(value => value.InstanceId), Is.EqualTo(new[] { 2L, 3L }));
            Assert.That(tray.Renderer.Count, Is.EqualTo(2));
        }

        /** Checks unchanged ready contributions keep their tokens while changed ones rebuild them / fails if every refresh rebuilds the tokens or a change leaves stale ones. */
        [Test]
        public void UnchangedReadyContributionsKeepTheirTokens()
        {
            var tray = Rig.CreateHistoryTray();
            var catalog = ReadyCatalog(out _, out ModifierDefinition score, out ModifierDefinition grant);
            var charms = Rig.Synthetic.For(catalog);
            tray.SynchronizeReady(new[] { TestTrayRig.Contribution(score, 2, 0, 2), TestTrayRig.Contribution(grant, 3, 0, 0, 2) }, catalog, charms);
            Transform die = tray.Renderer.GetDieTransform(0);
            tray.SynchronizeReady(new[] { TestTrayRig.Contribution(score, 2, 0, 2), TestTrayRig.Contribution(grant, 3, 0, 0, 2) }, catalog, charms);
            Assert.That(tray.Renderer.GetDieTransform(0), Is.SameAs(die));
            tray.SynchronizeReady(new[] { TestTrayRig.Contribution(grant, 3, 0, 0, 2) }, catalog, charms);
            Assert.That(tray.Count, Is.EqualTo(1));
            Assert.That(die == null, Is.True, "A changed list replaces its tokens.");
        }

        /** Checks tapping a token reports its own contribution and definition / fails if a tap reports another token or nothing. */
        [Test]
        public void TokenTapReportsItsContribution()
        {
            var tray = Rig.CreateHistoryTray();
            var first = TestTrayRig.Definition("test.tray.tap.first");
            var second = TestTrayRig.Definition("test.tray.tap.second");
            tray.Add(TestTrayRig.Contribution(first, 1), Rig.Charms(first), first, false);
            tray.Add(TestTrayRig.Contribution(second, 2), Rig.Charms(second), second, false);
            ModifierContribution selected = default;
            ModifierDefinition selectedDefinition = null;
            tray.TokenSelected += (contribution, definition) => { selected = contribution; selectedDefinition = definition; };
            tray.Content.GetChild(1).GetComponent<Button>().onClick.Invoke();
            Assert.That(selected.InstanceId, Is.EqualTo(2));
            Assert.That(selectedDefinition, Is.SameAs(second));
        }

        /** Checks resolving selects the requested occurrence of a repeated contribution and an absent one clears the highlight / fails if the wrong occurrence lifts or an absent contribution keeps a highlight. */
        [Test]
        public void ResolvingSelectsTheRequestedOccurrence()
        {
            var tray = Rig.CreateHistoryTray();
            var repeated = TestTrayRig.Definition("test.tray.repeated");
            var other = TestTrayRig.Definition("test.tray.other");
            var absent = TestTrayRig.Definition("test.tray.absent");
            ModifierContribution contribution = TestTrayRig.Contribution(repeated, 1);
            tray.Add(contribution, Rig.Charms(repeated), repeated, false);
            tray.Add(TestTrayRig.Contribution(other, 2), Rig.Charms(other), other, false);
            tray.Add(contribution, Rig.Charms(repeated), repeated, false);
            Assert.That(tray.SetResolving(contribution, 1), Is.True);
            Assert.That(tray.ResolvingIndex, Is.EqualTo(2));
            Assert.That(tray.SetResolving(contribution), Is.True);
            Assert.That(tray.ResolvingIndex, Is.EqualTo(0));
            Assert.That(tray.SetResolving(TestTrayRig.Contribution(absent, 3)), Is.False);
            Assert.That(tray.ResolvingIndex, Is.EqualTo(-1));
        }

        /** Checks resolving a token beyond the visible bay scrolls it into view / fails if the resolving token stays clipped. */
        [Test]
        public void ResolvingAnOverflowTokenScrollsItIntoTheBay()
        {
            var tray = Rig.CreateHistoryTray();
            var definition = TestTrayRig.Definition("test.tray.reveal");
            ModifierContribution contribution = TestTrayRig.Contribution(definition);
            int last = tray.Renderer.Capacity + 4;
            for (int i = 0; i <= last; ++i) tray.Add(contribution, Rig.Charms(definition), definition, false);
            Canvas.ForceUpdateCanvases();
            Assert.That(tray.Renderer.GetDieViewportRect(last), Is.EqualTo(Rect.zero));
            Assert.That(tray.SetResolving(contribution, last), Is.True);
            Assert.That(tray.ResolvingIndex, Is.EqualTo(last));
            Assert.That(tray.Renderer.GetDieViewportRect(last).width, Is.GreaterThan(0));
        }

        /** Checks clearing a highlight after its token or the whole stage was destroyed neither throws nor rebuilds anything / fails if shutdown order breaks clearing or recreates the destroyed presentation. */
        [TestCase(false)]
        [TestCase(true)]
        public void ClearingResolvingAfterStageDestructionDoesNotRebuild(bool destroyWholeStage)
        {
            var tray = Rig.CreateHistoryTray();
            var definition = TestTrayRig.Definition("test.tray.shutdown");
            ModifierContribution contribution = TestTrayRig.Contribution(definition);
            tray.Add(contribution, Rig.Charms(definition), definition, false);
            Assert.That(tray.SetResolving(contribution), Is.True);
            var renderer = tray.Renderer;
            Transform stage = renderer.RenderRoot;
            if (destroyWholeStage) UnityEngine.Object.DestroyImmediate(stage.gameObject);
            else UnityEngine.Object.DestroyImmediate(renderer.GetDieTransform(0).gameObject);
            Assert.DoesNotThrow(() => tray.ClearResolving());
            Assert.That(tray.ResolvingIndex, Is.EqualTo(-1));
            if (destroyWholeStage) Assert.That(renderer == null, Is.True);
            else Assert.That(renderer.RenderRoot, Is.SameAs(stage));
            if (!destroyWholeStage) Assert.That(renderer.GetDieTransform(0) == null, Is.True);
        }

        private GameObject Root(string name, params Type[] components)
        {
            var root = new GameObject(name, components) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(root, preview);
            return root;
        }

        /** A catalog of a roll-phase charm, a scoring charm and a charm that grants only rerolls. */
        private static MechanicalCatalog ReadyCatalog(out ModifierDefinition roll, out ModifierDefinition score, out ModifierDefinition grant)
        {
            roll = TestTrayRig.Definition("test.ready.roll", ModifierPhase.InitialRoll);
            score = TestTrayRig.Definition("test.ready.score", ModifierPhase.PlayHand, new AddMultiplierEffect(1));
            grant = TestTrayRig.Definition("test.ready.grant", ModifierPhase.PlayHand, new AddRerollsEffect(1));
            return new MechanicalCatalog("test.ready", TestContent.Hands(),
                new[] { new EncounterDefinition(TestEncounters.IdOf("test.ready"), new[] { new StageDefinition(1, 1, 0) }) }, new[] { roll, score, grant });
        }

        /** A gameplay HUD built from test-only UI objects; scaled lists the graphics whose scales it captures. */
        private SpiritGameplayHud CreateHud(out RectTransform[] scaled)
        {
            var theme = ScriptableObject.CreateInstance<SpiritUiTheme>();
            resources.Add(theme);
            var root = Root("Gameplay HUD", typeof(RectTransform));
            var wording = SyntheticWording.Create();
            resources.Add(wording);
            root.AddComponent<SpiritUiContext>().Configure(theme, wording);
            var hud = root.AddComponent<SpiritGameplayHud>();
            T Ui<T>(string name, Transform parent = null) where T : Component
            {
                var child = new GameObject(name, typeof(RectTransform));
                child.transform.SetParent(parent != null ? parent : root.transform, false);
                return child.AddComponent<T>();
            }
            var graphics = new List<Graphic>();
            Text Label(string property)
            {
                var label = Ui<Text>(property);
                SyntheticUi.Set(hud, "<" + property + ">k__BackingField", label);
                graphics.Add(label);
                return label;
            }
            var icon = Ui<Image>("ModifierActivationIcon");
            SyntheticUi.Set(hud, "<ModifierActivationIcon>k__BackingField", icon);
            graphics.Add(icon);
            foreach (string property in new[] { "SelectionLabel", "ScoreDetail", "BaseScoreLabel", "MultiplierLabel",
                "HandScoreLabel", "TargetLabel" })
                Label(property);
            SyntheticUi.Set(hud, "<CategoryLabel>k__BackingField", Ui<Text>("CategoryLabel"));
            SyntheticUi.Set(hud, "<ModifierEmblem>k__BackingField", Ui<Image>("ModifierEmblem"));
            SyntheticUi.Set(hud, "<RerollButton>k__BackingField", Ui<Button>("RerollButton"));
            SyntheticUi.Set(hud, "<PlayButton>k__BackingField", Ui<Button>("PlayButton"));
            var badge = Ui<SpiritModifierCategoryBadge>("ModifierActivationCategory");
            var accent = Ui<Image>("Accent", badge.transform);
            SyntheticUi.Set(badge, "<Accent>k__BackingField", accent);
            SyntheticUi.Set(badge, "<Glyph>k__BackingField", Ui<Image>("Glyph", badge.transform));
            SyntheticUi.Set(badge, "border", accent.gameObject.AddComponent<Outline>());
            SyntheticUi.Set(hud, "modifierActivationCategory", badge);
            foreach (string name in new[] { "BaseScoreHighlight", "MultiplierHighlight", "RerollsHighlight", "HandsHighlight" })
            {
                var label = Ui<Text>(name + " Label");
                var highlightIcon = Ui<Image>(name + " Icon");
                SyntheticUi.Set(hud, "<" + name + ">k__BackingField.Label", label);
                SyntheticUi.Set(hud, "<" + name + ">k__BackingField.Caption", Ui<Text>(name + " Caption"));
                SyntheticUi.Set(hud, "<" + name + ">k__BackingField.Icon", highlightIcon);
                graphics.Add(label);
                graphics.Add(highlightIcon);
            }
            scaled = graphics.Select(graphic => graphic.rectTransform).ToArray();
            return hud;
        }

        private static void AssertVector(Vector3 actual, Vector3 expected, float tolerance = .00001f)
            => Assert.That(Vector3.Distance(actual, expected), Is.LessThan(tolerance), actual + " expected " + expected);

        private static void AssertColor(Color actual, Color expected)
            => Assert.That(Vector4.Distance(actual, expected), Is.LessThan(.00001f), actual + " expected " + expected);
    }

    /**
     * A tray rig built from test-only objects in a preview scene: a shared stage with its camera, geometry with five
     * dice slots, label anchors and a modifier bay, the dice and modifier renderers, and a modifier token template.
     */
    internal sealed class TestTrayRig : IDisposable
    {
        private const string DiceShaderName = "Dice/Spirit Dice Surface";
        private readonly Scene preview;
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        /** Test-only charm presentations, destroyed with the rig. */
        internal SyntheticCharms Synthetic { get; } = new SyntheticCharms();

        /** A presentation of the given test-only charms. */
        internal ModifierPresentationCatalog Charms(params ModifierDefinition[] definitions) => Synthetic.For(definitions);

        internal SpiritTrayPresentationAsset Presentation { get; }
        internal SpiritTrayScene TrayScene { get; }
        internal Transform Stage { get; }
        internal Camera Camera { get; }
        internal SpiritTrayGeometry Geometry { get; }
        internal MeshRenderer SurfaceRenderer { get; }
        internal Material SurfaceMaterial { get; }
        internal SpiritDiceTray Dice { get; }
        internal GameObject[] Outlines { get; } = new GameObject[DiceValues.Count];
        internal GameObject[] Frames { get; } = new GameObject[DiceValues.Count];
        internal SpiritModifierHistoryRenderer History { get; }
        internal SpiritModifierToken Token { get; }
        internal Material TokenMaterial { get; }
        internal Texture2D TokenSurface { get; }

        internal TestTrayRig(Scene preview)
        {
            this.preview = preview;
            Shader shader = Shader.Find(DiceShaderName);
            Assert.That(shader, Is.Not.Null, DiceShaderName);
            Presentation = Own(ScriptableObject.CreateInstance<SpiritTrayPresentationAsset>());
            Presentation.TextureSize = new Vector2Int(64, 32);
            SurfaceMaterial = Own(new Material(shader) { name = "Test Tray Surface" });
            var dieMaterial = Own(new Material(shader) { name = "Test Die" });
            var shadowMaterial = Own(new Material(shader) { name = "Test Contact Shadow" });
            shadowMaterial.SetFloat("_SurfaceKind", 5);

            var root = Root("Test Tray Rig");
            TrayScene = root.AddComponent<SpiritTrayScene>();
            Stage = Child("Tray Stage", root.transform);
            Camera = Child("Tray Camera", Stage).gameObject.AddComponent<Camera>();
            Camera.enabled = false;
            Camera.scene = preview;
            // The camera is authored to frame the surface, the dice through their roll and the modifier bay, as an
            // encounter rig's camera is; binding the tray never moves it.
            Camera.transform.localPosition = new Vector3(0, 5.2f, -3.05f);
            Camera.transform.localRotation = Quaternion.Euler(55, 0, 0);
            Geometry = Child("Tray Geometry", Stage).gameObject.AddComponent<SpiritTrayGeometry>();
            SurfaceRenderer = Primitive(PrimitiveType.Cube, "Tray Surface", Geometry.transform, new Vector3(0, -.1f, .6f),
                new Vector3(8, .2f, 4), SurfaceMaterial);

            var dice = new Transform[DiceValues.Count];
            var shadows = new MeshRenderer[DiceValues.Count];
            for (int i = 0; i < DiceValues.Count; ++i)
            {
                Transform slot = Child("Dice Slot " + i, Geometry.transform);
                slot.localPosition = new Vector3((i - 2) * 1.45f, 0, 0);
                Geometry.DiceSlots[i] = slot;
                Transform label = Child("Dice Label " + i, Geometry.transform);
                label.localPosition = new Vector3((i - 2) * 1.45f, .35f, -1.13f);
                Geometry.DiceLabelAnchors[i] = label;
                dice[i] = Primitive(PrimitiveType.Cube, "Die " + i, slot, Vector3.up * .5f, Vector3.one, dieMaterial).transform;
                Outlines[i] = Child("Selection Outline", dice[i]).gameObject;
                Frames[i] = Child("Selection Frame", slot).gameObject;
                Outlines[i].SetActive(false);
                Frames[i].SetActive(false);
                shadows[i] = Primitive(PrimitiveType.Quad, "Contact Shadow", slot, Vector3.up * .002f,
                    new Vector3(1.1f, 1.1f, 1), shadowMaterial);
                shadows[i].transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            Geometry.ModifierRoot = Child("Modifier Bay", Geometry.transform);
            Geometry.ModifierRoot.localPosition = new Vector3(0, 0, 1.8f);

            var dicePresentation = Child("Dice Presentation", Stage).gameObject;
            Dice = dicePresentation.AddComponent<SpiritDiceTray>();
            SyntheticUi.Set(Dice, "dice", dice);
            SyntheticUi.Set(Dice, "selectionOutlines", Outlines);
            SyntheticUi.Set(Dice, "selectionFrames", Frames);
            SyntheticUi.Set(Dice, "contactShadows", shadows);
            SyntheticUi.Set(Dice, "renderRoot", dicePresentation);

            var template = Root("Modifier Token Template");
            Token = template.AddComponent<SpiritModifierToken>();
            TokenSurface = Own(new Texture2D(2, 2));
            TokenMaterial = Own(new Material(shader) { name = "Test Token Bone" });
            TokenMaterial.SetColor("_BaseColor", new Color(.9f, .85f, .7f, 1));
            TokenMaterial.SetTexture("_SurfaceTexture", TokenSurface);
            Token.Body = Primitive(PrimitiveType.Cube, "Body", template.transform, Vector3.zero,
                Vector3.one * SpiritModifierHistoryRenderer.DieSize, TokenMaterial);
            Token.Shadow = Primitive(PrimitiveType.Quad, "Shadow", template.transform, Vector3.zero,
                new Vector3(1.3f, .8f, .7f), shadowMaterial);

            var historyPresentation = Child("Modifier Presentation", Stage).gameObject;
            History = historyPresentation.AddComponent<SpiritModifierHistoryRenderer>();
            var tokens = Child("Modifier Tokens", Geometry.ModifierRoot).gameObject;
            SyntheticUi.Set(History, "renderRoot", tokens);
            SyntheticUi.Set(History, "contentRoot", tokens.transform);
            SyntheticUi.Set(History, "tokenPrefab", Token);

            SyntheticUi.Set(TrayScene, "renderRoot", Stage.gameObject);
            SyntheticUi.Set(TrayScene, "renderCamera", Camera);
            SyntheticUi.Set(TrayScene, "geometry", Geometry);
        }

        /** Configures and initializes the shared scene. */
        internal SpiritTrayScene Initialize()
        {
            TrayScene.Configure(Presentation);
            TrayScene.Initialize();
            return TrayScene;
        }

        internal SpiritDiceTray StartDice()
        {
            Initialize();
            Dice.Configure(Presentation, TrayScene);
            Dice.Initialize();
            return Dice;
        }

        internal SpiritModifierHistoryRenderer StartHistory()
        {
            Initialize();
            History.Configure(Presentation, TrayScene);
            History.Initialize();
            return History;
        }

        /** A modifier history tray over this rig's renderer, with a fixed 700 by 350 world-space bay and token buttons. */
        internal SpiritModifierHistoryTray CreateHistoryTray()
        {
            Initialize();
            var canvas = Root("History Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(700, 350);
            RectTransform trayRect = UiChild("Modifier History", canvas.transform);
            var tray = trayRect.gameObject.AddComponent<SpiritModifierHistoryTray>();
            RectTransform frame = UiChild("Bay Frame", trayRect);
            RectTransform viewport = UiChild("Viewport", trayRect);
            RectTransform content = UiChild("Content", viewport);
            content.anchorMin = Vector2.zero;
            content.anchorMax = new Vector2(0, 1);
            content.pivot = new Vector2(0, .5f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            var scroll = trayRect.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            var button = Root("Token Button", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            SyntheticUi.Set(tray, "root", trayRect);
            SyntheticUi.Set(tray, "content", content);
            SyntheticUi.Set(tray, "scroll", scroll);
            SyntheticUi.Set(tray, "frame", frame);
            SyntheticUi.Set(tray, "tokenPrefab", button);
            tray.BindRenderer(History);
            tray.Configure(Presentation, TrayScene);
            tray.Build();
            return tray;
        }

        internal static ModifierDefinition Definition(string id, ModifierPhase phase = ModifierPhase.PlayHand,
            ModifierEffect effect = null)
            => new ModifierDefinition(TestModifiers.IdOf(id), TestFamilies.Of(id), phase, new AlwaysCondition(), new[] { effect ?? new AddBaseScoreEffect(1) });

        /** A contribution of the definition, made through the engine's own constructor. */
        internal static ModifierContribution Contribution(ModifierDefinition definition, long instanceId = 1, long baseScore = 1,
            long multiplier = 0, int rerolls = 0)
            => new ModifierContribution(instanceId, definition, baseScore, multiplier, rerolls);

        public void Dispose()
        {
            if (History != null) History.ReleasePresentation();
            if (Dice != null) Dice.ReleasePresentation();
            if (TrayScene != null) TrayScene.ReleasePresentation();
            Synthetic.Dispose();
            for (int i = owned.Count - 1; i >= 0; --i)
                if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        private T Own<T>(T value) where T : UnityEngine.Object
        {
            owned.Add(value);
            return value;
        }

        private GameObject Root(string name, params Type[] components)
        {
            var root = Own(new GameObject(name, components));
            SceneManager.MoveGameObjectToScene(root, preview);
            return root;
        }

        private static Transform Child(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static RectTransform UiChild(string name, Transform parent)
        {
            var child = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            child.SetParent(parent, false);
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.sizeDelta = Vector2.zero;
            return child;
        }

        private static MeshRenderer Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale,
            Material material)
        {
            var primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            UnityEngine.Object.DestroyImmediate(primitive.GetComponent<Collider>());
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            var renderer = primitive.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }
    }
}
