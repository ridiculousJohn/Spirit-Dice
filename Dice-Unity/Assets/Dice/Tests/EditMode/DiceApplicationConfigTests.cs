using System;
using System.Linq;
using Dice.App;
using Dice.Presentation;
using Dice.UnityContent;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dice.Tests
{
    public sealed class DiceApplicationConfigTests
    {
        private const string ConfigPath = "Assets/Dice/Content/DiceApplicationConfig.asset";
        private DiceApplicationConfig configuration;

        [SetUp]
        public void SetUp()
        {
            configuration = ScriptableObject.CreateInstance<DiceApplicationConfig>();
            Set("catalogAsset", AssetDatabase.LoadAssetAtPath<MechanicalCatalogAsset>(
                "Assets/Dice/Content/Mechanical/Standard.asset"));
            Set("encounterId", "encounter.standard");
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(configuration);

        [Test]
        public void DirectEncounterConfigurationNeedsNoJourneyCatalog()
        {
            Assert.That(configuration.JourneyCatalog, Is.Null);
            Assert.DoesNotThrow(configuration.Validate);
        }

        [Test]
        public void JourneyConfigurationNeedsNoDirectEncounterId()
        {
            Set("encounterId", string.Empty);
            Set("journeyCatalog", AssetDatabase.LoadAssetAtPath<EncounterJourneyCatalogAsset>(
                "Assets/Dice/Content/Presentation/SpiritHouse/SpiritJourneyCatalog.asset"));
            Assert.That(configuration.JourneyCatalog, Is.Not.Null);
            Assert.DoesNotThrow(configuration.Validate);
        }

        [Test]
        public void MissingMechanicalCatalogIsRejected()
        {
            Set("catalogAsset", null);
            Assert.Throws<InvalidOperationException>(configuration.Validate);
        }

        [TestCase("modifierLabel")]
        [TestCase("modifierPresentationLabel")]
        [TestCase("encounterId")]
        public void RequiredContentKeysRejectWhitespace(string property)
        {
            Set(property, " \t");
            Assert.Throws<InvalidOperationException>(configuration.Validate);
        }

        [TestCase("")]
        [TestCase("-1")]
        [TestCase("not a seed")]
        [TestCase("18446744073709551616")]
        public void InvalidSeedIsRejectedOnlyWhenFixedSeedIsEnabled(string seed)
        {
            Set("initialSeed", seed);
            Assert.DoesNotThrow(configuration.Validate);
            Set("useFixedSeed", true);
            Assert.Throws<InvalidOperationException>(configuration.Validate);
        }

        [TestCase("0")]
        [TestCase("18446744073709551615")]
        public void FixedSeedAcceptsTheEntireUnsignedRange(string seed)
        {
            Set("useFixedSeed", true);
            Set("initialSeed", seed);
            Assert.DoesNotThrow(configuration.Validate);
        }

        [Test]
        public void AuthoredConfigurationResolvesExistingCatalogsAndLabels()
        {
            var authored = AssetDatabase.LoadAssetAtPath<DiceApplicationConfig>(ConfigPath);
            Assert.That(authored, Is.Not.Null);
            Assert.That(authored.CatalogAsset, Is.SameAs(AssetDatabase.LoadAssetAtPath<MechanicalCatalogAsset>(
                "Assets/Dice/Content/Mechanical/Standard.asset")));
            Assert.That(authored.JourneyCatalog, Is.SameAs(AssetDatabase.LoadAssetAtPath<EncounterJourneyCatalogAsset>(
                "Assets/Dice/Content/Presentation/SpiritHouse/SpiritJourneyCatalog.asset")));
            Assert.That(authored.ModifierLabel, Is.EqualTo(ModifierAssetLoader.Label));
            Assert.That(authored.ModifierPresentationLabel, Is.EqualTo(ModifierPresentationLoader.Label));
            Assert.That(authored.UseFixedSeed, Is.False);
            Assert.That(authored.InitialSeed, Is.EqualTo("60"));
            Assert.DoesNotThrow(authored.Validate);
            var catalog = authored.CatalogAsset.Build(TestContent.ModifierAssets());
            Assert.DoesNotThrow(() => authored.JourneyCatalog.Validate(catalog));
        }

        [Test]
        public void ConfigurationHasOneLocalAddressIncludedInThePlayerBuild()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.That(settings, Is.Not.Null);
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(ConfigPath));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.address, Is.EqualTo(DiceBootstrap.ConfigurationAddress));
            Assert.That(entry.parentGroup.Name, Is.EqualTo("Default Local Group"));
            Assert.That(settings.groups.Where(group => group != null).SelectMany(group => group.entries)
                .Count(candidate => candidate.address == DiceBootstrap.ConfigurationAddress), Is.EqualTo(1));
            var schema = entry.parentGroup.GetSchema<BundledAssetGroupSchema>();
            Assert.That(schema, Is.Not.Null);
            Assert.That(schema.IncludeInBuild, Is.True);
            Assert.That(schema.IncludeAddressInCatalog, Is.True);
        }

        [Test]
        public void StartupSceneUsesBootstrapAndControllerHasNoSerializedContentSettings()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Application.unity");
            try
            {
                var controllers = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SpiritEncounterController>(true)).ToArray();
                Assert.That(controllers.Length, Is.EqualTo(1));
                var controller = controllers[0];
                Assert.That(controller.GetComponent<DiceBootstrap>(), Is.Not.Null);
                using var serialized = new SerializedObject(controller);
                foreach (string field in new[] { "catalogAsset", "journeyCatalog", "encounterId", "modifierLabel",
                    "modifierPresentationLabel", "useFixedSeed", "initialSeed" })
                    Assert.That(serialized.FindProperty(field), Is.Null, field);
                Assert.That(EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == scene.path), Is.True);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private void Set(string name, object value)
        {
            using var serialized = new SerializedObject(configuration);
            var property = serialized.FindProperty(name);
            Assert.That(property, Is.Not.Null, name);
            if (property.propertyType == SerializedPropertyType.String) property.stringValue = (string)value;
            else if (property.propertyType == SerializedPropertyType.Boolean) property.boolValue = (bool)value;
            else property.objectReferenceValue = (UnityEngine.Object)value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
