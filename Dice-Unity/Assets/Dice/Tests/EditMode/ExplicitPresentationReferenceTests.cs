using System;
using System.Linq;
using Dice.Presentation;
using Dice.App;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Dice.Tests
{
    public sealed class ExplicitPresentationReferenceTests
    {
        private const string ScenePath = "Assets/Scenes/Application.unity";
        private const string SharedPath = "Assets/Dice/Presentation/Shared/";

        [Test]
        public void PrototypeSceneReferencesItsThemeTrayTexturesFontsAndCustomShaders()
        {
            var tables = AssetDatabase.FindAssets("t:EncounterTablePresentationAsset", new[] { "Assets/Dice" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<EncounterTablePresentationAsset>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();
            var scenePaths = tables.Select(table => AssetDatabase.GUIDToAssetPath(table.Scene.AssetGUID)).Append(ScenePath).ToArray();
            string[] dependencies = scenePaths.SelectMany(path => AssetDatabase.GetDependencies(path, true)).Distinct().ToArray();
            string[] required =
            {
                "SpiritHouseTheme.asset",
                "SpiritUiGreen.png", "SpiritUiAmber.png", "SpiritUiDark.png", "SpiritUiWood.png",
                "SpiritBoneSurface.png", "SpiritWoodSurface.png",
                "Fonts/CormorantGaramond-SemiBold.ttf", "Fonts/SourceSans3-Regular.ttf",
                "SpiritDiceSurface.shader", "SpiritSilhouette.shader", "SpiritSmoke.shader"
            };
            foreach (string path in required)
                Assert.That(dependencies, Does.Contain(SharedPath + path), "Scene dependency: " + path);
            foreach (var table in tables)
            {
                Assert.That(table.TrayPresentation, Is.Not.Null, table.name);
                Assert.That(table.TrayPresentation.RigPrefab, Is.Not.Null, table.name);
                Assert.That(dependencies, Does.Contain(AssetDatabase.GetAssetPath(table.TrayPresentation)), table.name);
                Assert.That(dependencies, Does.Contain(AssetDatabase.GetAssetPath(table.TrayPresentation.RigPrefab)), table.name);
                Assert.DoesNotThrow(table.TrayPresentation.Validate);
            }
            Assert.That(dependencies.Where(IsResourcesPath), Is.Empty);

            string[] applicationDependencies = AssetDatabase.GetDependencies("Assets/Dice/Content/DiceApplicationConfig.asset", true);
            Assert.That(applicationDependencies, Does.Contain(SharedPath + "SceneLighting.shader"),
                "The Addressable configuration must retain the journeys' lighting shader.");
            Assert.That(applicationDependencies.Where(IsResourcesPath), Is.Empty);

            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                var roots = scene.GetRootGameObjects();
                var views = roots.SelectMany(root => root.GetComponentsInChildren<SpiritEncounterView>(true)).ToArray();
                Assert.That(views, Is.Empty, "The application scene retains only startup and routing presentation.");
                var router = roots.SelectMany(root => root.GetComponentsInChildren<SpiritApplicationPresentation>(true)).Single();
                Assert.That(router, Is.Not.Null);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void AuthoredPresentationAssetsCarryTheShadersTheirRenderersUse()
        {
            var hosts = AssetDatabase.FindAssets("t:SpiritHostPresentationAsset", new[] { "Assets/Dice" });
            Assert.That(hosts, Is.Not.Empty);
            foreach (string guid in hosts)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var host = AssetDatabase.LoadAssetAtPath<SpiritHostPresentationAsset>(path);
                Assert.That(host.Layers, Is.Not.Null, path);
                foreach (SpiritPortraitLayer layer in host.Layers)
                {
                    Assert.That(layer, Is.Not.Null, path);
                    if (layer.Silhouette) Assert.That(host.SilhouetteShader, Is.Not.Null, path + ": " + layer.Id);
                    if (layer.Smoke != null && layer.Smoke.Enabled)
                        Assert.That(host.SmokeShader, Is.Not.Null, path + ": " + layer.Id);
                }
            }
            var lightingAssets = AssetDatabase.FindAssets("t:SceneLightingAsset", new[] { "Assets/Dice" });
            Assert.That(lightingAssets, Is.Not.Empty);
            foreach (string guid in lightingAssets)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var lighting = AssetDatabase.LoadAssetAtPath<SceneLightingAsset>(path);
                Assert.That(lighting.Shader, Is.Not.Null, path);
            }
        }

        [Test]
        public void ProjectAssetsHaveNoResourcesDirectories()
        {
            Assert.That(AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
                .Where(IsResourcesPath), Is.Empty);
        }

        [Test]
        public void SeparateUiHierarchiesKeepTheirOwnFontsPaletteAndButtonTints()
        {
            SpiritUiTheme authored = Load<SpiritUiTheme>("SpiritHouseTheme.asset");
            var first = ScriptableObject.CreateInstance<SpiritUiTheme>();
            var second = ScriptableObject.CreateInstance<SpiritUiTheme>();
            GameObject firstRoot = null;
            GameObject secondRoot = null;
            try
            {
                first.headingFont = authored.headingFont;
                first.bodyFont = authored.bodyFont;
                first.cream = Color.red;
                first.amber = Color.green;
                first.muted = Color.blue;
                first.panelColor = Color.magenta;
                first.highlightedTint = Color.yellow;
                first.pressedTint = Color.cyan;
                first.disabledTint = Color.gray;
                second.headingFont = authored.bodyFont;
                second.bodyFont = authored.headingFont;
                second.cream = Color.blue;
                second.amber = Color.magenta;
                second.muted = Color.green;
                second.panelColor = Color.cyan;
                second.highlightedTint = Color.red;
                second.pressedTint = Color.yellow;
                second.disabledTint = Color.white;
                Assert.That(first.headingFont, Is.Not.SameAs(second.headingFont));

                firstRoot = Hierarchy("First theme", first);
                var prefab = AssetDatabase.LoadAssetAtPath<SpiritRewardCard>("Assets/Dice/Content/Presentation/UI/SpiritRewardCard.prefab");
                Assert.That(prefab, Is.Not.Null);
                var firstTemplate = UnityEngine.Object.Instantiate(prefab, firstRoot.transform, false);
                ConfigureCardTemplate(firstTemplate, first);
                var firstCard = SpiritRewardCard.Create(firstTemplate, "First reward", firstRoot.transform, (offer, option) => { });
                secondRoot = Hierarchy("Second theme", second);
                var secondTemplate = UnityEngine.Object.Instantiate(prefab, secondRoot.transform, false);
                ConfigureCardTemplate(secondTemplate, second);
                var secondCard = SpiritRewardCard.Create(secondTemplate, "Second reward", secondRoot.transform, (offer, option) => { });
                var nextFirstCard = SpiritRewardCard.Create(firstTemplate, "Next first reward", firstRoot.transform, (offer, option) => { });
                CheckCard(firstCard, first);
                CheckCard(secondCard, second);
                CheckCard(nextFirstCard, first);
            }
            finally
            {
                if (firstRoot != null) UnityEngine.Object.DestroyImmediate(firstRoot);
                if (secondRoot != null) UnityEngine.Object.DestroyImmediate(secondRoot);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        private static GameObject Hierarchy(string name, SpiritUiTheme theme)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(SpiritUiContext));
            root.GetComponent<SpiritUiContext>().Configure(theme);
            return root;
        }

        private static Text CardText(SpiritRewardCard card, string name)
            => card.GetComponentsInChildren<Text>(true).First(label => label.name == name);

        private static void ConfigureCardTemplate(SpiritRewardCard card, SpiritUiTheme theme)
        {
            card.GetComponent<SpiritUiContext>().Configure(theme);
            CardText(card, "Title").font = theme.headingFont;
            CardText(card, "Condition").font = theme.bodyFont;
            CardText(card, "Title").color = CardText(card, "Condition").color = theme.cream;
            CardText(card, "Effects").color = theme.amber;
            CardText(card, "Level").color = theme.muted;
            card.GetComponent<Image>().color = theme.panelColor;
            var colors = card.Button.colors;
            colors.highlightedColor = colors.selectedColor = theme.highlightedTint;
            colors.pressedColor = theme.pressedTint;
            colors.disabledColor = theme.disabledTint;
            card.Button.colors = colors;
        }

        private static void CheckCard(SpiritRewardCard card, SpiritUiTheme theme)
        {
            Text title = CardText(card, "Title");
            Text condition = CardText(card, "Condition");
            Assert.That(title.font, Is.SameAs(theme.headingFont));
            Assert.That(condition.font, Is.SameAs(theme.bodyFont));
            Assert.That(title.color, Is.EqualTo(theme.cream));
            Assert.That(condition.color, Is.EqualTo(theme.cream));
            Assert.That(CardText(card, "Effects").color, Is.EqualTo(theme.amber));
            Assert.That(CardText(card, "Level").color, Is.EqualTo(theme.muted));
            Assert.That(card.GetComponent<Image>().color, Is.EqualTo(theme.panelColor));
            Assert.That(card.Button.colors.highlightedColor, Is.EqualTo(theme.highlightedTint));
            Assert.That(card.Button.colors.selectedColor, Is.EqualTo(theme.highlightedTint));
            Assert.That(card.Button.colors.pressedColor, Is.EqualTo(theme.pressedTint));
            Assert.That(card.Button.colors.disabledColor, Is.EqualTo(theme.disabledTint));
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(SharedPath + path);
            Assert.That(asset, Is.Not.Null, SharedPath + path);
            return asset;
        }

        private static bool IsResourcesPath(string path)
            => path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0
                || path.EndsWith("/Resources", StringComparison.OrdinalIgnoreCase);
    }
}
