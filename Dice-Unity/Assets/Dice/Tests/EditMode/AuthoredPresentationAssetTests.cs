using System;
using System.IO;
using System.Linq;
using Dice.App;
using Dice.Engine;
using Dice.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class AuthoredPresentationAssetTests
    {
        private const string Folder = "Assets/Dice/Content/Presentation/UI/";

        [TestCase("SpiritRewardsPanel")]
        [TestCase("SpiritOwnedCharmsPanel")]
        [TestCase("SpiritCollectionView")]
        [TestCase("SpiritHandRanksPanel")]
        [TestCase("SpiritEncounterCharmsScreen")]
        public void LargeCharmSurfacesKeepOpaqueBackingAndAuthoredDarkFrame(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
            var theme = prefab.GetComponent<SpiritUiContext>().Theme;
            var backing = prefab.GetComponent<Image>();
            Assert.That(backing.sprite, Is.Null);
            Assert.That(backing.color, Is.EqualTo(theme.panelColor));
            var frames = prefab.GetComponentsInChildren<Image>(true).Where(image => image.transform.parent == prefab.transform &&
                image.sprite == theme.darkFrame.sprite && image.GetComponent<LayoutElement>()?.ignoreLayout == true).ToArray();
            Assert.That(frames, Has.Length.EqualTo(1));
            AssertDarkFrame(frames[0], theme);
            Assert.That(frames[0].raycastTarget, Is.False);
            Assert.That(frames[0].GetComponent<LayoutElement>().ignoreLayout, Is.True);
        }

        [TestCase("SpiritOwnedCharmCard")]
        [TestCase("SpiritModifierFamilyRow")]
        public void CharmTemplatesKeepTheirAuthoredTexturedBackground(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
            var frame = prefab.GetComponentsInChildren<Image>(true).Single(image => image.transform.parent == prefab.transform &&
                image.GetComponent<LayoutElement>()?.ignoreLayout == true);
            AssertDarkFrame(frame, prefab.GetComponent<SpiritUiContext>().Theme);
            Assert.That(prefab.GetComponent<Image>().sprite, Is.Null);
            var card = prefab.GetComponent<SpiritOwnedCharmCard>();
            if (card != null) Assert.That(card.Button.targetGraphic, Is.SameAs(frame));
        }

        [Test]
        public void RewardModesKeepOriginalBackgroundsAndAuthoredSelectionColorsWithoutChangingTypography()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<SpiritRewardCard>(Folder + "SpiritRewardCard.prefab");
            var card = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var theme = card.GetComponent<SpiritUiContext>().Theme;
                var modifierBackground = Authored<Image>(card, "modifierBackground");
                var handBackground = Authored<Image>(card, "handBackground");
                var title = Authored<Text>(card, "title");
                var handTitle = Authored<Text>(card, "handTitle");
                var level = Authored<Text>(card, "handLevelText");
                var marker = Authored<Graphic>(card, "selectionMarker");
                AssertDarkFrame(modifierBackground, theme);
                Assert.That(handBackground.sprite, Is.Null);
                using (var image = new SerializedObject(handBackground))
                    Assert.That(image.FindProperty("m_Material").objectReferenceValue, Is.Null);
                Assert.That(handBackground.type, Is.EqualTo(Image.Type.Simple));
                Assert.That(marker, Is.InstanceOf<Image>());
                Assert.That(marker.rectTransform.anchorMax.x, Is.Zero);
                Assert.That(marker.rectTransform.sizeDelta.x, Is.EqualTo(3));
                Assert.That(card.Button.colors.highlightedColor, Is.EqualTo(theme.highlightedTint));
                Assert.That(card.Button.colors.pressedColor, Is.EqualTo(theme.pressedTint));
                Assert.That(card.Button.colors.disabledColor, Is.EqualTo(theme.disabledTint));
                int objects = card.GetComponentsInChildren<Transform>(true).Length;
                title.fontSize = 31;
                handTitle.fontSize = 27;
                using (var styles = new SerializedObject(card))
                {
                    styles.FindProperty("modifierSelectedColor").colorValue = Color.magenta;
                    styles.FindProperty("handSelectedColor").colorValue = Color.cyan;
                    styles.ApplyModifiedPropertiesWithoutUndo();
                }
                var definition = new ModifierDefinition("authored.style", "authored.style", ModifierPhase.PlayHand,
                    new AlwaysCondition(), new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 5) });
                card.RenderModifier(definition, 1, 2);
                Assert.That(Authored<GameObject>(card, "modifierRoot").activeSelf, Is.True);
                Assert.That(Authored<GameObject>(card, "handRoot").activeSelf, Is.False);
                Assert.That(modifierBackground.color, Is.EqualTo(Color.white));
                card.SetModifierSelected(true);
                Assert.That(modifierBackground.color, Is.EqualTo(Color.magenta));
                Assert.That(title.color, Is.EqualTo(theme.amber));
                Assert.That(marker.gameObject.activeSelf, Is.True);
                card.SetModifierSelected(false);
                Assert.That(title.color, Is.EqualTo(theme.cream));
                card.RenderHandUpgrade(TestContent.Hands()[0], 0, 3, 4);
                Assert.That(Authored<GameObject>(card, "modifierRoot").activeSelf, Is.False);
                Assert.That(Authored<GameObject>(card, "handRoot").activeSelf, Is.True);
                Assert.That(handBackground.color, Is.EqualTo((Color)new Color32(26, 40, 32, 215)));
                Assert.That(level.color, Is.EqualTo(theme.muted));
                card.SetHandUpgradeSelected(true);
                Assert.That(handBackground.color, Is.EqualTo(Color.cyan));
                Assert.That(handTitle.color, Is.EqualTo(theme.amber));
                Assert.That(level.color, Is.EqualTo(theme.amber));
                card.SetHandUpgradeSelected(false);
                Assert.That(handBackground.color, Is.EqualTo((Color)new Color32(26, 40, 32, 215)));
                Assert.That(handTitle.color, Is.EqualTo(theme.cream));
                Assert.That(level.color, Is.EqualTo(theme.muted));
                Assert.That(title.fontSize, Is.EqualTo(31));
                Assert.That(handTitle.fontSize, Is.EqualTo(27));
                Assert.That(card.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
                AssertDarkFrame(modifierBackground, theme);
            }
            finally { UnityEngine.Object.DestroyImmediate(card.gameObject); }
        }

        [Test]
        public void CollectionAndOwnedButtonsRetainTheirLocalAuthoredAppearance()
        {
            var owned = AssetDatabase.LoadAssetAtPath<SpiritOwnedCharmsPanel>(Folder + "SpiritOwnedCharmsPanel.prefab");
            var theme = owned.GetComponent<SpiritUiContext>().Theme;
            AssertDarkFrame((Image)owned.CloseButton.targetGraphic, theme);
            var card = AssetDatabase.LoadAssetAtPath<SpiritOwnedCharmCard>(Folder + "SpiritOwnedCharmCard.prefab");
            Assert.That(card.NormalColor, Is.EqualTo(Color.white));
            Assert.That(card.SelectedColor, Is.EqualTo((Color)new Color32(255, 232, 179, 255)));
            Assert.That(card.SelectionOutline.color, Is.EqualTo((Color)new Color32(255, 215, 119, 255)));
            var collection = AssetDatabase.LoadAssetAtPath<SpiritCollectionView>(Folder + "SpiritCollectionView.prefab");
            foreach (var field in new[] { "closeButton", "filterButton" })
            {
                var image = (Image)Authored<Button>(collection, field).targetGraphic;
                Assert.That(image.sprite, Is.Null);
                Assert.That(image.color, Is.EqualTo(theme.panelColor));
                var border = image.GetComponent<Outline>();
                Assert.That(border, Is.Not.Null);
                Assert.That(border.effectColor, Is.EqualTo(new Color(theme.amber.r, theme.amber.g, theme.amber.b, .6f)));
            }
            var family = AssetDatabase.LoadAssetAtPath<SpiritModifierFamilyRow>(Folder + "SpiritModifierFamilyRow.prefab");
            foreach (var button in family.GetComponentsInChildren<Button>(true))
            {
                Assert.That(button.colors.highlightedColor, Is.EqualTo(theme.highlightedTint));
                Assert.That(button.colors.pressedColor, Is.EqualTo(theme.pressedTint));
                Assert.That(button.colors.fadeDuration, Is.EqualTo(.12f));
                Assert.That(button.GetComponent<Outline>().effectColor,
                    Is.EqualTo(new Color(theme.muted.r, theme.muted.g, theme.muted.b, .3f)));
            }
        }

        private static T Authored<T>(Component component, string name) where T : UnityEngine.Object
        {
            using var serialized = new SerializedObject(component);
            var reference = serialized.FindProperty(name).objectReferenceValue as T;
            Assert.That(reference, Is.Not.Null, component.name + "." + name);
            return reference;
        }

        private static void AssertDarkFrame(Image image, SpiritUiTheme theme)
        {
            Assert.That(image.sprite, Is.SameAs(theme.darkFrame.sprite));
            Assert.That(image.material, Is.SameAs(theme.darkFrame.surfaceMaterial));
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(theme.darkFrame.pixelsPerUnitMultiplier));
        }

        [Test]
        public void ApplicationSceneReferencesPresentationScenesWithoutLoadingTheirContent()
        {
            const string path = "Assets/Scenes/Application.unity";
            foreach (string dependency in AssetDatabase.GetDependencies(path, true))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(dependency);
                Assert.That(asset, Is.Not.InstanceOf<EncounterTablePresentationAsset>(), dependency);
                Assert.That(asset, Is.Not.InstanceOf<SpiritHostPresentationAsset>(), dependency);
                Assert.That(asset, Is.Not.InstanceOf<SpiritTrayPresentationAsset>(), dependency);
                if (asset is GameObject prefab)
                {
                    Assert.That(prefab.GetComponentInChildren<SpiritHostPortrait>(true), Is.Null, dependency);
                    Assert.That(prefab.GetComponentInChildren<SpiritTrayScene>(true), Is.Null, dependency);
                    Assert.That(prefab.GetComponentInChildren<SpiritTrayGeometry>(true), Is.Null, dependency);
                    Assert.That(prefab.GetComponentInChildren<SpiritDiceTray>(true), Is.Null, dependency);
                }
                if (asset is SceneAsset)
                    Assert.That(dependency, Is.EqualTo(path), "Presentation scenes must be loaded through their GUID references.");
            }

            var config = AssetDatabase.LoadAssetAtPath<DiceApplicationConfig>("Assets/Dice/Content/DiceApplicationConfig.asset");
            Assert.That(config, Is.Not.Null);
            Assert.That(config.FrontendScene, Is.Not.Null);
            Assert.That(AssetDatabase.GUIDToAssetPath(config.FrontendScene.AssetGUID), Is.EqualTo("Assets/Scenes/Frontend.unity"));
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                AssertNoMissingScripts(scene);
                var router = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpiritApplicationPresentation>(true)).Single();
                using var serialized = new SerializedObject(router);
                var fallback = serialized.FindProperty("defaultEncounterScene");
                Assert.That(fallback, Is.Not.Null);
                Assert.That(AssetDatabase.GUIDToAssetPath(fallback.FindPropertyRelative("m_AssetGUID").stringValue),
                    Is.EqualTo("Assets/Scenes/FoxEncounter.unity"));
                foreach (var root in scene.GetRootGameObjects())
                {
                    Assert.That(root.GetComponentInChildren<SpiritEncounterView>(true), Is.Null);
                    Assert.That(root.GetComponentInChildren<SpiritHostPortrait>(true), Is.Null);
                    Assert.That(root.GetComponentInChildren<SpiritTrayScene>(true), Is.Null);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [TestCase("FoxEncounter", "SpiritHouse/LastLanternTable")]
        [TestCase("WatchtowerEncounter", "Watchtower/WatchtowerTable")]
        [TestCase("MothEncounter", "MothCrossroads/MothCrossroadsTable")]
        public void EncounterScenesContainTheirAuthoredTableHostAndSharedCameraTarget(string name, string tablePath)
        {
            string path = "Assets/Scenes/" + name + ".unity";
            var table = AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>("Assets/Dice/Content/Presentation/" + tablePath + ".asset");
            Assert.That(table, Is.Not.Null);
            Assert.That(table.Scene, Is.Not.Null);
            Assert.That(table.Scene.AssetGUID, Is.EqualTo(AssetDatabase.AssetPathToGUID(path)));
            Assert.That(table.HostPresentation, Is.Not.Null);
            Assert.That(table.TrayPresentation, Is.Not.Null);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                AssertNoMissingScripts(scene);
                var roots = scene.GetRootGameObjects();
                var view = roots.SelectMany(root => root.GetComponentsInChildren<SpiritEncounterView>(true)).Single();
                var rig = roots.SelectMany(root => root.GetComponentsInChildren<SpiritTrayScene>(true)).Single();
                var host = roots.SelectMany(root => root.GetComponentsInChildren<SpiritHostPortrait>(true)).Single();
                Assert.That(view.TablePresentation, Is.SameAs(table));
                Assert.That(view.TrayPresentation, Is.SameAs(table.TrayPresentation));
                Assert.That(view.TrayScene, Is.SameAs(rig));
                Assert.That(view.HostPortrait, Is.SameAs(host));
                Assert.That(host.transform.IsChildOf(view.Portrait), Is.True);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(host), Is.SameAs(table.HostPresentation.PortraitPrefab));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(rig), Is.SameAs(table.TrayPresentation.RigPrefab));
                Assert.That(rig.Presentation, Is.SameAs(table.TrayPresentation));
                Assert.That(rig.DiceTray, Is.Not.Null);
                Assert.That(view.DiceTray, Is.SameAs(rig.DiceTray));
                Assert.That(rig.Geometry, Is.Not.Null);
                Assert.That(rig.HistoryRenderer, Is.Not.Null);
                Assert.That(rig.RenderCamera, Is.Not.Null);
                Assert.That(rig.RenderCamera.enabled, Is.True, "The saved scene must render its tray before runtime binding.");
                var target = rig.RenderCamera.targetTexture;
                Assert.That(target, Is.Not.Null, "The camera requires a saved render target.");
                Assert.That(EditorUtility.IsPersistent(target), Is.True);
                Assert.That(target.width, Is.EqualTo(table.TrayPresentation.TextureSize.x));
                Assert.That(target.height, Is.EqualTo(table.TrayPresentation.TextureSize.y));
                Assert.That(view.GameplayHud, Is.Not.Null);
                Assert.That(view.GameplayHud.DiceTrayImage, Is.Not.Null);
                Assert.That(view.GameplayHud.DiceTrayImage.texture, Is.SameAs(target));
                var popupLayout = view.CharmPopup.Panel.GetComponent<SpiritCharmPopupLayout>();
                Assert.That(popupLayout, Is.Not.Null);
                using var serializedLayout = new SerializedObject(popupLayout);
                Assert.That(serializedLayout.FindProperty("widthReference").objectReferenceValue, Is.SameAs(view.Portrait));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static void AssertNoMissingScripts(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero,
                        scene.path + ": " + transform.name);
        }

        [Test]
        public void RuntimeComponentsHaveDedicatedImportableMonoScripts()
        {
            var scripts = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Dice" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(script => script.GetClass() != null).ToArray();
            foreach (var type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>().Where(type => !type.IsAbstract &&
                (type.Assembly.GetName().Name == "Dice.Presentation" || type.Assembly.GetName().Name == "Dice.App")))
            {
                var script = scripts.SingleOrDefault(candidate => candidate.GetClass() == type);
                Assert.That(script, Is.Not.Null, type.FullName + " requires its own importable MonoScript.");
                Assert.That(Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(script)), Is.EqualTo(type.Name));
            }
        }

        [TestCase(typeof(SpiritCharmPopup), "SpiritCharmPopup")]
        [TestCase(typeof(SpiritOwnedCharmsPanel), "SpiritOwnedCharmsPanel")]
        [TestCase(typeof(SpiritRewardsPanel), "SpiritRewardsPanel")]
        [TestCase(typeof(SpiritRewardCard), "SpiritRewardCard")]
        [TestCase(typeof(SpiritCollectionView), "SpiritCollectionView")]
        [TestCase(typeof(SpiritModifierFamilyRow), "SpiritModifierFamilyRow")]
        [TestCase(typeof(SpiritEncounterCharmsScreen), "SpiritEncounterCharmsScreen")]
        [TestCase(typeof(SpiritHandRanksPanel), "SpiritHandRanksPanel")]
        [TestCase(typeof(SpiritOwnedCharmCard), "SpiritOwnedCharmCard")]
        [TestCase(typeof(SpiritInvitationRuleView), "SpiritInvitationRuleView")]
        [TestCase(typeof(EncounterJourneyView), "EncounterJourneyView")]
        public void ScreenAndTemplateReferencesAreAuthoredOnTheirPrefabRoot(Type type, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
            Assert.That(prefab, Is.Not.Null, name);
            var component = prefab.GetComponent(type);
            Assert.That(component, Is.Not.Null, "The screen component belongs on its authored root.");
            Assert.That(prefab.GetComponent<RectTransform>(), Is.Not.Null);
            foreach (var behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                Assert.That(behaviour, Is.Not.Null, name + " contains a missing script.");
            using var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                Assert.That(property.objectReferenceValue, Is.Not.Null, name + "." + property.propertyPath);
            }
        }

        [Test]
        public void CharmPopupBindingAndDataRenderingPreserveAuthoredTypographyAndPlacement()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<SpiritCharmPopup>(Folder + "SpiritCharmPopup.prefab");
            Assert.That(prefab, Is.Not.Null);
            int authoredFontSize = prefab.Title.fontSize;
            Vector2 authoredAnchorMin = prefab.Panel.anchorMin;
            Vector2 authoredAnchorMax = prefab.Panel.anchorMax;
            var popup = UnityEngine.Object.Instantiate(prefab);
            try
            {
                Assert.That(popup, Is.Not.SameAs(prefab));
                Assert.That(EditorUtility.IsPersistent(popup), Is.False);
                popup.Title.fontSize = 31;
                popup.Panel.anchorMin = new Vector2(.13f, .5f);
                popup.Panel.anchorMax = new Vector2(.87f, .5f);
                int objects = popup.GetComponentsInChildren<Transform>(true).Length;
                var modifier = new ModifierDefinition("authored.popup", "authored.popup", ModifierPhase.PlayHand,
                    new AlwaysCondition(), new[] { new ModifierEffect(ModifierEffectKind.AddBaseScore, 5) });
                var encounter = new EncounterDefinition(TestContent.EncounterId,
                    new[] { new StageDefinition(100000, 3, 3) }, startingModifiers: new[] { modifier.Id });
                var catalog = new MechanicalCatalog("authored.popup", TestContent.Hands(), new[] { encounter }, new[] { modifier });
                var snapshot = EncounterSession.CreateEncounter(catalog, encounter.Id, 42).Snapshot;
                var status = SpiritCharmStatusPresentation.Create(snapshot.Modifiers[0], modifier, snapshot.Phase, null);
                popup.Build(popup.Root, popup.Root);
                popup.Build(popup.Root, popup.Root);
                popup.Show(snapshot.Modifiers[0].InstanceId, modifier, status, snapshot.Phase);
                Assert.That(popup.Title.fontSize, Is.EqualTo(31));
                Assert.That(popup.Panel.anchorMin, Is.EqualTo(new Vector2(.13f, .5f)));
                Assert.That(popup.Panel.anchorMax, Is.EqualTo(new Vector2(.87f, .5f)));
                Assert.That(popup.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objects));
                popup.CloseButton.onClick.Invoke();
                Assert.That(popup.IsOpen, Is.False);
                Assert.That(popup.InstanceId, Is.EqualTo(-1));
            }
            finally { UnityEngine.Object.DestroyImmediate(popup.gameObject); }
            Assert.That(prefab.Title.fontSize, Is.EqualTo(authoredFontSize));
            Assert.That(prefab.Panel.anchorMin, Is.EqualTo(authoredAnchorMin));
            Assert.That(prefab.Panel.anchorMax, Is.EqualTo(authoredAnchorMax));
        }

        [TestCase("SpiritCharmPopup")]
        [TestCase("SpiritOwnedCharmsPanel")]
        [TestCase("SpiritRewardsPanel")]
        [TestCase("SpiritRewardCard")]
        [TestCase("SpiritCollectionView")]
        [TestCase("SpiritModifierFamilyRow")]
        [TestCase("SpiritEncounterCharmsScreen")]
        [TestCase("SpiritHandRanksPanel")]
        [TestCase("EncounterJourneyView")]
        public void RuntimeScreensBindAuthoredObjectsWithoutConstructingTheirUi(string name)
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Dice", "Presentation", name + ".cs"));
            foreach (string factory in new[] { "new GameObject(", ".AddComponent<", "SpiritUi.Rect(", "SpiritUi.Full(",
                "SpiritUi.Panel(", "SpiritUi.Label(", "SpiritUi.Heading(", "SpiritUi.Button(", "SpiritUi.Scroll(" })
                Assert.That(source, Does.Not.Contain(factory), name + ": " + factory);
        }
    }
}
